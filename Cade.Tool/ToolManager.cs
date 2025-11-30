namespace Cade.Tool;

/// <summary>
/// 工具管理器，负责工具的注册和调用
/// </summary>
public class ToolManager
{
    private readonly Dictionary<string, ITool> _tools = new();

    /// <summary>
    /// 注册工具
    /// </summary>
    public void RegisterTool(ITool tool)
    {
        _tools[tool.Name] = tool;
    }

    /// <summary>
    /// 获取所有已注册的工具
    /// </summary>
    public IEnumerable<ITool> GetAllTools()
    {
        return _tools.Values;
    }

    /// <summary>
    /// 根据名称获取工具
    /// </summary>
    public ITool? GetTool(string name)
    {
        return _tools.TryGetValue(name, out var tool) ? tool : null;
    }

    /// <summary>
    /// 执行指定的工具
    /// </summary>
    /// <param name="toolName">工具名称</param>
    /// <param name="parameters">工具参数</param>
    /// <returns>执行结果</returns>
    public async Task<ToolResult> ExecuteToolAsync(string toolName, string parameters)
    {
        var tool = GetTool(toolName);
        if (tool == null)
        {
            return ToolResult.CreateFailure($"未找到工具: {toolName}");
        }

        return await tool.ExecuteAsync(parameters);
    }

    /// <summary>
    /// 获取所有工具的定义（用于发送给 AI）
    /// </summary>
    public string GetToolDefinitions()
    {
        var tools = _tools.Values.Select(t => new
        {
            name = t.Name,
            description = t.Description
        });

        return System.Text.Json.JsonSerializer.Serialize(tools, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
}
