namespace Cade.Services.Interfaces;

/// <summary>
/// 工具调用日志事件参数
/// </summary>
public class ToolCallEventArgs : EventArgs
{
    public string ToolName { get; set; } = string.Empty;
    public string Parameters { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;
    public bool Success { get; set; }
}

/// <summary>
/// AI 响应结果（包含内容和思维链）
/// </summary>
public class AiResponse
{
    public string Content { get; set; } = string.Empty;
    public string? ReasoningContent { get; set; }
    public bool HasReasoning => !string.IsNullOrEmpty(ReasoningContent);
}

public interface IAiService
{
    /// <summary>
    /// 工具调用事件
    /// </summary>
    event EventHandler<ToolCallEventArgs>? ToolCalled;

    /// <summary>
    /// 获取 AI 响应 (支持自动工具调用)
    /// </summary>
    Task<string> GetResponseAsync(string input, string modelId);

    /// <summary>
    /// 获取 AI 响应 (支持取消)
    /// </summary>
    Task<string> GetResponseAsync(string input, string modelId, CancellationToken cancellationToken);

    /// <summary>
    /// 获取 AI 响应（包含思维链）
    /// </summary>
    Task<AiResponse> GetResponseWithReasoningAsync(string input, string modelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 流式获取 AI 响应
    /// </summary>
    IAsyncEnumerable<string> GetStreamingResponseAsync(string input, string modelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 从数据库消息恢复对话历史
    /// </summary>
    void RestoreHistory(IEnumerable<Cade.Data.Entities.ChatMessage> messages);

    /// <summary>
    /// 清空对话历史
    /// </summary>
    void ClearHistory();

    /// <summary>
    /// 设置工具调用回调（用于保存到数据库）
    /// </summary>
    void SetToolCallCallback(Func<string, string, Task>? callback);
}
