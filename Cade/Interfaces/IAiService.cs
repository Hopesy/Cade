namespace Cade.Interfaces;

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

public interface IAiService
{
    /// <summary>
    /// 工具调用事件
    /// </summary>
    event EventHandler<ToolCallEventArgs>? ToolCalled;

    Task<string> GetResponseAsync(string input, string modelId);
}
