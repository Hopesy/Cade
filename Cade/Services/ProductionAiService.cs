using Cade.Interfaces;
using Cade.Provider.Services.Interfaces;
using Microsoft.Extensions.AI;
using Cade.Tool;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace Cade.Services;

public class ProductionAiService : IAiService
{
    private readonly IProviderService _providerService;
    private readonly ToolManager _toolManager;

    public event EventHandler<ToolCallEventArgs>? ToolCalled;

    public ProductionAiService(IProviderService providerService, ToolManager toolManager)
    {
        _providerService = providerService;
        _toolManager = toolManager;
    }

    public async Task<string> GetResponseAsync(string input, string modelId)
    {
        if (string.IsNullOrEmpty(modelId) || modelId == "Loading..." || modelId == "No Models Available")
        {
             // 再次尝试获取第一个可用模型作为 fallback
             var models = _providerService.GetAvailableModels();
             if (models.Any())
             {
                 modelId = models.First().Id;
             }
             else
             {
                 return "没有可用的模型，请检查配置。";
             }
        }

        // 构建包含工具定义的系统提示
        var systemPrompt = BuildSystemPromptWithTools();

        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, input)
        };

        // 最多允许 5 轮工具调用
        for (int i = 0; i < 5; i++)
        {
            var response = await _providerService.SendMessageAsync(modelId, messages);

            // 检查是否包含工具调用
            var toolCall = ParseToolCall(response);
            if (toolCall == null)
            {
                // 没有工具调用，直接返回响应
                return CleanResponse(response);
            }

            // 执行工具调用
            var toolResult = await _toolManager.ExecuteToolAsync(toolCall.Value.toolName, toolCall.Value.parameters);

            // 触发工具调用事件
            ToolCalled?.Invoke(this, new ToolCallEventArgs
            {
                ToolName = toolCall.Value.toolName,
                Parameters = toolCall.Value.parameters,
                Output = toolResult.Success ? toolResult.Content : (toolResult.Error ?? "未知错误"),
                Success = toolResult.Success
            });

            // 将工具结果添加到对话历史
            messages.Add(new ChatMessage(ChatRole.Assistant, response));
            messages.Add(new ChatMessage(ChatRole.User, $"工具 '{toolCall.Value.toolName}' 执行结果:\n{(toolResult.Success ? toolResult.Content : $"错误: {toolResult.Error}")}"));
        }

        return "已达到最大工具调用次数限制。";
    }

    private string BuildSystemPromptWithTools()
    {
        var toolsJson = _toolManager.GetToolDefinitions();
        return $$"""
你是一个智能助手，可以调用以下工具来帮助用户：

可用工具:
{{toolsJson}}

工具调用格式：
如果你需要调用工具，请使用以下格式：
```tool
{
  "tool": "工具名称",
  "parameters": { 参数JSON对象 }
}
```

注意事项：
1. 每次只能调用一个工具
2. 工具调用后，你会收到执行结果
3. 根据工具结果继续回答用户的问题
4. 如果不需要调用工具，直接回答用户的问题即可
""";
    }

    private (string toolName, string parameters)? ParseToolCall(string response)
    {
        // 解析工具调用格式: ```tool ... ```
        var match = Regex.Match(response, @"```tool\s*\n(.*?)\n```", RegexOptions.Singleline);
        if (!match.Success)
            return null;

        try
        {
            var json = match.Groups[1].Value.Trim();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var toolName = root.GetProperty("tool").GetString();
            var parameters = root.GetProperty("parameters").GetRawText();

            if (string.IsNullOrEmpty(toolName))
                return null;

            return (toolName, parameters ?? "{}");
        }
        catch
        {
            return null;
        }
    }

    private string CleanResponse(string response)
    {
        // 移除可能残留的工具调用标记
        return Regex.Replace(response, @"```tool\s*\n.*?\n```", "", RegexOptions.Singleline).Trim();
    }
}
