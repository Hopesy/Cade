namespace Cade.Tool;

/// <summary>
/// 工具基类，提供通用功能
/// </summary>
public abstract class ToolBase : ITool
{
    public abstract string Name { get; }
    public abstract string Description { get; }

    public abstract Task<ToolResult> ExecuteAsync(string parameters);

    /// <summary>
    /// 安全执行工具操作，捕获异常
    /// </summary>
    protected async Task<ToolResult> SafeExecuteAsync(Func<Task<ToolResult>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            return ToolResult.CreateFailure($"工具执行失败: {ex.Message}");
        }
    }
}
