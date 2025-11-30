using Cade.Tool.Tools;

namespace Cade.Tool;

/// <summary>
/// 工具扩展方法，用于快速注册所有内置工具
/// </summary>
public static class ToolExtensions
{
    /// <summary>
    /// 注册所有内置工具
    /// </summary>
    public static void RegisterDefaultTools(this ToolManager manager)
    {
        manager.RegisterTool(new ReadFileTool());
        manager.RegisterTool(new WriteFileTool());
        manager.RegisterTool(new GetTimeTool());
        manager.RegisterTool(new GetNetworkInfoTool());
        manager.RegisterTool(new ListDirectoryTool());
    }
}
