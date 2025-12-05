using System.Runtime.CompilerServices;
using System.Text;
using Cade.Filters;
using Cade.Interfaces;
using Cade.Provider.Services.Interfaces;
using Cade.Tool.Plugins;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Cade.Services;

public class ProductionAiService : IAiService
{
    private readonly IProviderService _providerService;
    private readonly IUserInterface _ui;
    private readonly FileSystemPlugin _fileSystemPlugin;
    private readonly SystemPlugin _systemPlugin;
    private readonly ChatHistory _chatHistory = new();

    // 工具调用由 ToolCallFilter 处理
    public event EventHandler<ToolCallEventArgs>? ToolCalled { add { } remove { } }

    public ProductionAiService(
        IProviderService providerService,
        IUserInterface ui,
        FileSystemPlugin fileSystemPlugin,
        SystemPlugin systemPlugin)
    {
        _providerService = providerService;
        _ui = ui;
        _fileSystemPlugin = fileSystemPlugin;
        _systemPlugin = systemPlugin;

        _chatHistory.AddSystemMessage("你是一个名为 'Cade' 的高级 AI 编程助手，可以使用工具来帮助用户完成任务。");
    }

    public async Task<string> GetResponseAsync(string input, string modelId)
    {
        var kernel = _providerService.GetKernel();
        if (kernel == null)
            return "Kernel 未初始化，请检查配置。";

        RegisterPlugins(kernel);

        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var settings = new OpenAIPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            Temperature = 0.1,
            MaxTokens = 4096
        };

        _chatHistory.AddUserMessage(input);

        try
        {
            var response = await chatService.GetChatMessageContentAsync(_chatHistory, settings, kernel);
            var result = response.Content ?? string.Empty;
            _chatHistory.AddAssistantMessage(result);
            return result;
        }
        catch (Exception ex)
        {
            return $"请求失败: {ex.Message}";
        }
    }

    public async IAsyncEnumerable<string> GetStreamingResponseAsync(
        string input,
        string modelId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var kernel = _providerService.GetKernel();
        if (kernel == null)
        {
            yield return "Kernel 未初始化，请检查配置。";
            yield break;
        }

        RegisterPlugins(kernel);

        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var settings = new OpenAIPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            Temperature = 0.1,
            MaxTokens = 4096
        };

        _chatHistory.AddUserMessage(input);
        var responseBuilder = new StringBuilder();

        await foreach (var chunk in chatService.GetStreamingChatMessageContentsAsync(
            _chatHistory, settings, kernel, cancellationToken))
        {
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                responseBuilder.Append(chunk.Content);
                yield return chunk.Content;
            }
        }

        _chatHistory.AddAssistantMessage(responseBuilder.ToString());
    }

    private void RegisterPlugins(Kernel kernel)
    {
        if (!kernel.Plugins.Any(p => p.Name == nameof(FileSystemPlugin)))
            kernel.Plugins.AddFromObject(_fileSystemPlugin);

        if (!kernel.Plugins.Any(p => p.Name == nameof(SystemPlugin)))
            kernel.Plugins.AddFromObject(_systemPlugin);

        // 注册工具调用过滤器
        if (!kernel.FunctionInvocationFilters.Any(f => f is ToolCallFilter))
            kernel.FunctionInvocationFilters.Add(new ToolCallFilter(_ui));
    }
}
