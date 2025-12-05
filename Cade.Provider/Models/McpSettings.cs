namespace Cade.Provider.Models;

/// <summary>
/// MCP 服务器配置 (兼容 Claude/Kiro 的 mcp.json 格式)
/// </summary>
public class McpSettings
{
    public Dictionary<string, McpServerConfig> McpServers { get; set; } = [];
}

public class McpServerConfig
{
    public string Command { get; set; } = "";
    public string[]? Args { get; set; }
    public Dictionary<string, string>? Env { get; set; }
    public bool Disabled { get; set; } = false;
}
