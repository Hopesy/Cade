using Cade.Provider.Models;
using Cade.Provider.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Cade.Provider.Services;

/// <summary>
/// AI 服务管理实现 (基于 Semantic Kernel)
/// </summary>
public class ProviderService : IProviderService
{
    private readonly ILogger<ProviderService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Dictionary<string, ModelConfig> _modelConfigs = new();
    private readonly Dictionary<string, ProviderConfig> _providerConfigs = new();
    
    private Kernel? _kernel;
    private string? _currentModelId;

    public ProviderService(ILogger<ProviderService> logger, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    public async Task LoadModelsFromConfigAsync(ProviderConfig configuration, string configName)
    {
        try
        {
            ClearModels();

            if (!configuration.IsEnabled)
            {
                _logger.LogInformation("配置 {ConfigName} 未启用，跳过加载", configName);
                return;
            }

            _logger.LogInformation("开始加载配置: {ConfigName}, 类型: {Type}", configName, configuration.Type);

            configuration.Id = Guid.NewGuid().ToString();
            configuration.Name = configName;
            _providerConfigs[configuration.Id] = configuration;

            // 创建 Semantic Kernel
            _kernel = CreateKernel(configuration);

            // 解析模型列表
            var modelIds = configuration.ModelIds.Split(',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var modelId in modelIds)
            {
                if (string.IsNullOrWhiteSpace(modelId)) continue;

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

            // 默认选中第一个模型
            if (_modelConfigs.Count > 0)
            {
                _currentModelId = _modelConfigs.Values.First().ModelId;
            }

            _logger.LogInformation("成功加载 {ModelCount} 个模型从配置 {ConfigName}", _modelConfigs.Count, configName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载模型配置失败: {ConfigName}", configName);
            throw;
        }

        await Task.CompletedTask;
    }

    private Kernel CreateKernel(ProviderConfig config)
    {
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(_loggerFactory);

        var endpoint = string.IsNullOrEmpty(config.BaseUrl)
            ? null
            : new Uri(config.BaseUrl);

        // 获取第一个模型ID作为默认
        var defaultModelId = config.ModelIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? "gpt-4o";

        // 创建带超时的 HttpClient
        var httpClient = endpoint != null
            ? new HttpClient { BaseAddress = endpoint, Timeout = TimeSpan.FromMinutes(5) }
            : new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

        switch (config.Type)
        {
            case ProviderType.OpenAI:
                builder.AddOpenAIChatCompletion(
                    modelId: defaultModelId,
                    apiKey: config.ApiKey,
                    httpClient: httpClient);
                break;

            case ProviderType.OpenAICompatible:
                builder.AddOpenAIChatCompletion(
                    modelId: defaultModelId,
                    apiKey: config.ApiKey,
                    httpClient: httpClient);
                break;

            case ProviderType.Anthropic:
            case ProviderType.AnthropicCompatible:
                _logger.LogWarning("Anthropic 类型暂时使用 OpenAI 兼容模式");
                builder.AddOpenAIChatCompletion(
                    modelId: defaultModelId,
                    apiKey: config.ApiKey,
                    httpClient: httpClient);
                break;

            default:
                throw new ArgumentException($"不支持的提供商类型: {config.Type}");
        }

        _logger.LogInformation("Semantic Kernel 初始化成功: {ConfigName} ({Type})", config.Name, config.Type);
        return builder.Build();
    }

    public Kernel? GetKernel() => _kernel;

    public string? GetCurrentModelId() => _currentModelId;

    public void SetCurrentModel(string modelConfigId)
    {
        if (_modelConfigs.TryGetValue(modelConfigId, out var config))
        {
            _currentModelId = config.ModelId;
            _logger.LogInformation("切换到模型: {ModelId}", _currentModelId);
        }
    }

    public void AddPlugin(KernelPlugin plugin)
    {
        _kernel?.Plugins.Add(plugin);
        _logger.LogInformation("添加插件: {PluginName} ({Count} 个函数)", plugin.Name, plugin.Count());
    }

    public List<ModelConfig> GetAvailableModels() => _modelConfigs.Values.ToList();

    public int GetLoadedModelCount() => _modelConfigs.Count;

    public void ClearModels()
    {
        _kernel = null;
        _currentModelId = null;
        _modelConfigs.Clear();
        _providerConfigs.Clear();
        _logger.LogInformation("已清除所有模型配置");
    }
}