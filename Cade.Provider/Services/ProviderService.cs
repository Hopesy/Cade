using System.ClientModel;
using System.Runtime.CompilerServices;
using Anthropic.SDK;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Cade.Provider.Models;
using Cade.Provider.Services.Interfaces;
using OpenAI;

namespace Cade.Provider.Services;

// AI 服务管理实现
//【重点】发送消息的时候根据模型ID查找对应的供应商ID,根据供应商ID查找对应的客户端。
public class ProviderService(ILogger<ProviderService> logger) : IProviderService
{
    private readonly Dictionary<string, IChatClient> _chatClients = new();
    private readonly Dictionary<string, ModelConfig> _modelConfigs = new();
    //预留字典，后期方便拓展：支持多配置同时加载(目前每次切换加载都会ClearModels());提供商管理功能(列表、查询、更新等);
    private readonly Dictionary<string, ProviderConfig> _providerConfigs = new();
    //切换配置文件的时候会触发，实例化IChatClient
    public async Task LoadModelsFromConfigAsync(ProviderConfig configuration, string configName)
    {
        try
        {
            // 清除现有配置
            ClearModels();
            if (!configuration.IsEnabled)
            {
                logger.LogInformation("配置 {ConfigName} 未启用，跳过加载", configName);
                return;
            }
            logger.LogInformation("开始加载配置: {ConfigName}, 类型: {Type}", configName, configuration.Type);
            configuration.Id = Guid.NewGuid().ToString();
            configuration.Name = configName;
            _providerConfigs[configuration.Id] = configuration;
            try
            {
                switch (configuration.Type)
                {
                    case ProviderType.OpenAI or ProviderType.OpenAICompatible:
                        // 根据配置创建对应的IchatClient客户端
                        await InitOpenAIProviderAsync(configuration);
                        break;
                    case ProviderType.Anthropic or ProviderType.AnthropicCompatible:
                        await InitAnthropicProviderAsync(configuration);
                        break;
                }
                logger.LogInformation("成功初始化提供商: {ConfigName} ({Type})", configName, configuration.Type);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "初始化提供商失败: {ConfigName}", configName);
                throw;
            }
            var modelIds = configuration.ModelIds.Split(',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var modelId in modelIds)
            {
                if (string.IsNullOrWhiteSpace(modelId))
                    continue;
                var modelConfig = new ModelConfig
                {
                    Id = $"{configuration.Id}_{modelId.Trim()}",
                    DisplayName = modelId.Trim(),
                    ModelId = modelId.Trim(),
                    ProviderId = configuration.Id,
                    IsEnabled = true,
                    Description = $"{configuration.Type} 模型"
                };
                _modelConfigs[modelConfig.Id] = modelConfig;
            }
            logger.LogInformation("成功加载 {ModelCount} 个模型从配置 {ConfigName}", _modelConfigs.Count, configName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "加载模型配置失败: {ConfigName}", configName);
            throw;
        }
    }
    private Task InitOpenAIProviderAsync(ProviderConfig provider)
    {
        try
        {
            var baseAddress = string.IsNullOrEmpty(provider.BaseUrl)
                ? new Uri("https://api.openai.com/v1/")
                : new Uri(provider.BaseUrl);
            var openAIClient = new OpenAIClient(new ApiKeyCredential(provider.ApiKey), new OpenAIClientOptions
            {
                Endpoint = baseAddress
            });
            
            // 使用 OpenAIChatClient 包装器 (Microsoft.Extensions.AI.OpenAI)
            // 注意：这里我们不在这里指定 ModelId，而是在 SendMessageAsync 时通过 ChatOptions 指定
            // 但 OpenAIChatClient 构造函数需要一个默认 ModelId，我们可以给一个占位符，
            // 或者更好的方式是为每个模型创建一个 IChatClient，但这里架构是每个 Provider 一个 Client。
            // 参考 MinoChat 实现，它使用了 AsIChatClient() 扩展方法。
            // 但根据之前的尝试，OpenAI SDK 2.0+ 的 client.GetChatClient(id).AsChatClient() 才是正解。
            // 然而为了兼容多个模型，我们在这里可能需要调整策略，或者使用一个默认模型ID初始化，
            // 然后在请求时覆盖（如果 SDK 支持）。
            // 
            // 查看 MinoChat 代码: _chatClients[provider.Id] = openAIClient.GetChatClient(provider.Id).AsIChatClient();
            // 这里 provider.Id 其实不仅是 ProviderId，它被用作 GetChatClient 的参数？
            // 不，ProviderConfig.Id 是 GUID。GetChatClient 应该接收 ModelId。
            // 但 MinoChat 的实现似乎有点奇怪，或者它假设 provider.Id 可以作为默认 ModelId？
            // 让我们再次检查 MinoChat 的代码。
            // InitOpenAIProviderAsync: _chatClients[provider.Id] = openAIClient.GetChatClient(provider.Id).AsIChatClient();
            // 确实如此。但这可能是一个 Bug 或者特殊的用法。
            // 通常 OpenAIClient.GetChatClient(modelId) 获取特定模型的客户端。
            // 如果我们想动态切换模型，可能需要针对每个 ModelId 创建一个 IChatClient，
            // 或者使用支持动态 ModelId 的 IChatClient 实现。
            // 
                         // 在 Microsoft.Extensions.AI 中，ChatOptions.ModelId 可以覆盖默认值。
                        // 所以我们只需要一个能工作的 IChatClient。
                        // 我们可以用 "gpt-4o" 或任意字符串作为初始 ModelId。
                         
                         _chatClients[provider.Id] = openAIClient.GetChatClient("default").AsIChatClient();
                         
                        logger.LogInformation("OpenAI提供商初始化成功: {ProviderName}", provider.Name);        }
        catch (Exception ex)
        {
            logger.LogError(ex, "初始化OpenAI提供商失败: {ProviderName}", provider.Name);
            throw;
        }
        return Task.CompletedTask;
    }
    private Task InitAnthropicProviderAsync(ProviderConfig provider)
    {
        try
        {
            var baseUrl = string.IsNullOrEmpty(provider.BaseUrl)
                ? "https://api.anthropic.com"
                : provider.BaseUrl.TrimEnd('/');
            var handler = new AnthropicHttpHandler(baseUrl);
            var httpClient = new HttpClient(handler, disposeHandler: true);
            var anthropicClient = new AnthropicClient(new APIAuthentication(provider.ApiKey), httpClient);
            
            // Anthropic SDK 集成需要适配器。MinoChat 使用了 .AsBuilder()...Build()。
            // 确保我们引用了 Microsoft.Extensions.AI.Anthropic 或者类似的包，或者 MinoChat 自己实现了适配？
            // 检查 MinoChat.Provider.csproj: 只有 Anthropic.SDK。
            // 看起来 Anthropic.SDK 可能自带了 Microsoft.Extensions.AI 的适配？
            // 或者 MinoChat 用的是社区的扩展？
            // 让我们再次看 MinoChat 的代码：
            // _chatClients[provider.Id] = anthropicClient.Messages.AsBuilder().UseFunctionInvocation().Build();
            // 这看起来像是 Anthropic SDK 提供的扩展方法。
            
            _chatClients[provider.Id] = anthropicClient.Messages
                .AsBuilder()
                .UseFunctionInvocation()
                .Build();
                
            logger.LogInformation("Anthropic 提供商初始化成功: {ProviderName}", provider.Name);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "初始化 Anthropic 提供商失败: {ProviderName}", provider.Name);
            throw;
        }
        return Task.CompletedTask;
    }
    public async Task<string> SendMessageAsync(string modelId,
        List<ChatMessage> historyMessages, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_modelConfigs.TryGetValue(modelId, out var modelConfig))
            {
                throw new ArgumentException($"未找到模型配置: {modelId}");
            }
            logger.LogInformation("发送消息到模型: {ModelName} ({ModelId})", modelConfig.DisplayName, modelConfig.ModelId);
            //一个ProviderId代表了一个配置文件，代表了一个客户端IChatClient
            if (!_chatClients.TryGetValue(modelConfig.ProviderId, out var chatClient))
            {
                throw new InvalidOperationException($"聊天客户端未初始化: {modelConfig.ProviderId}");
            }
            var response = await chatClient.GetResponseAsync(historyMessages, new ChatOptions
            {
                ModelId = modelConfig.ModelId,
                MaxOutputTokens = 4096
            }, cancellationToken);
            logger.LogInformation("请求成功，响应长度: {Length}", response.Text?.Length ?? 0);
            return response.Text ?? string.Empty;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "发送消息失败: {ModelId}", modelId);
            throw;
        }
    }
    public async IAsyncEnumerable<string> SendMessageStreamAsync(string modelId, List<ChatMessage> histtoryMessages, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!_modelConfigs.TryGetValue(modelId, out var modelConfig))
        {
            throw new ArgumentException($"未找到模型配置: {modelId}");
        }
        logger.LogInformation("流式发送消息到模型: {ModelName}-({ModelId})", modelConfig.DisplayName, modelConfig.ModelId);
        if (!_chatClients.TryGetValue(modelConfig.ProviderId, out var chatClient))
        {
            throw new InvalidOperationException($"聊天客户端未初始化: {modelConfig.ProviderId}");
        }
        await foreach (var update in chatClient.GetStreamingResponseAsync(histtoryMessages, new ChatOptions
        {
            ModelId = modelConfig.ModelId,
            MaxOutputTokens = 4096
        }, cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                yield return update.Text;
            }
        }
    }
    public List<ModelConfig> GetAvailableModels() => _modelConfigs.Values.ToList();
    public int GetLoadedModelCount() => _modelConfigs.Count;
    //清除所有配置
    public void ClearModels()
    {
        _chatClients.Clear();
        _modelConfigs.Clear();
        _providerConfigs.Clear();
        logger.LogInformation("已清除所有模型配置");
    }
}
