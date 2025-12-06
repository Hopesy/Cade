using System.Runtime.CompilerServices;
using System.Text;
using Cade.Filters;
using Cade.Interfaces;
using Cade.Provider.Services.Interfaces;
using Cade.Tool.Plugins;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Cade.Services;

public class ProductionAiService : IAiService
{
    private readonly IProviderService _providerService;
    private readonly IUserInterface _ui;
    private readonly ILogger<ProductionAiService> _logger;
    private readonly FileSystemPlugin _fileSystemPlugin;
    private readonly SystemPlugin _systemPlugin;
    private readonly ChatHistory _chatHistory = new();

    // 工具调用由 ToolCallFilter 处理
    public event EventHandler<ToolCallEventArgs>? ToolCalled { add { } remove { } }

    public ProductionAiService(
        IProviderService providerService,
        IUserInterface ui,
        ILogger<ProductionAiService> logger,
        FileSystemPlugin fileSystemPlugin,
        SystemPlugin systemPlugin)
    {
        _providerService = providerService;
        _ui = ui;
        _logger = logger;
        _fileSystemPlugin = fileSystemPlugin;
        _systemPlugin = systemPlugin;

        _chatHistory.AddSystemMessage("""
            你是 Cade，一个高级 AI 编程助手。你可以使用以下工具帮助用户：

            文件操作：
            - ReadFile: 读取文件内容
            - WriteFile: 写入文件
            - AppendToFile: 追加内容到文件
            - ReplaceInFile: 替换文件中的文本
            - CreateDirectory: 创建目录
            - Delete: 删除文件或目录
            - Move: 移动/重命名
            - Copy: 复制文件或目录
            - ListDirectory: 列出目录内容
            - SearchFiles: 搜索文件
            - Grep: 在文件中搜索文本
            - GetInfo: 获取文件/目录信息

            系统操作：
            - ExecuteCommand: 执行Shell命令（如 dotnet new, npm init, git clone, pip install 等）
            - GetSystemInfo: 获取系统信息
            - GetTime: 获取当前时间
            - GetNetworkInfo: 获取网络信息

            当用户需要创建项目、安装依赖、执行构建等操作时，使用 ExecuteCommand 执行相应的命令。
            回复时简洁明了，优先使用工具完成任务。
            """);
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
            _logger.LogInformation("开始获取AI响应...");
            var response = await chatService.GetChatMessageContentAsync(_chatHistory, settings, kernel);
            var result = response.Content ?? string.Empty;
            _logger.LogInformation("AI响应完成，内容长度: {Length}", result.Length);
            _chatHistory.AddAssistantMessage(result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI请求失败");
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

        // 注册自动函数调用过滤器（显示思考过程）
        if (!kernel.AutoFunctionInvocationFilters.Any(f => f is AutoFunctionFilter))
            kernel.AutoFunctionInvocationFilters.Add(new AutoFunctionFilter(_ui));
    }
}
