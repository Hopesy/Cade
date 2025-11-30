namespace Cade.Tool;

/// <summary>
/// 工具接口定义
/// </summary>
public interface ITool
{
    /// <summary>
    /// 工具名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 工具描述
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 执行工具操作
    /// </summary>
    /// <param name="parameters">工具参数（JSON格式）</param>
    /// <returns>执行结果</returns>
    Task<ToolResult> ExecuteAsync(string parameters);
}

/// <summary>
/// 工具执行结果
/// </summary>
public class ToolResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 结果内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 错误信息（如果有）
    /// </summary>
    public string? Error { get; set; }

    public static ToolResult CreateSuccess(string content) => new()
    {
        Success = true,
        Content = content
    };

    public static ToolResult CreateFailure(string error) => new()
    {
        Success = false,
        Error = error
    };
}
