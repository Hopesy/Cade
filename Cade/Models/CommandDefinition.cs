namespace Cade.Models;

/// <summary>
/// 命令定义
/// </summary>
public record CommandDefinition(string Name, string Description, string[]? Aliases = null)
{
    /// <summary>
    /// 所有可用命令
    /// </summary>
    public static readonly CommandDefinition[] AllCommands =
    [
        new("/model", "切换 AI 模型"),
        new("/help", "显示帮助信息"),
        new("/clear", "清空对话历史"),
        new("/exit", "退出程序", ["/quit"])
    ];

    /// <summary>
    /// 根据输入匹配命令
    /// </summary>
    public static CommandDefinition[] Match(string input)
    {
        // 只有 "/" 时不显示列表，需要输入更多字符
        if (string.IsNullOrEmpty(input) || !input.StartsWith("/") || input.Length < 2)
            return [];

        var search = input.ToLower();
        return AllCommands
            .Where(c => c.Name.StartsWith(search) || 
                       (c.Aliases?.Any(a => a.StartsWith(search)) ?? false))
            .ToArray();
    }

    /// <summary>
    /// 精确匹配命令
    /// </summary>
    public static CommandDefinition? ExactMatch(string input)
    {
        if (string.IsNullOrEmpty(input))
            return null;

        var search = input.ToLower().Trim();
        return AllCommands.FirstOrDefault(c => 
            c.Name == search || (c.Aliases?.Contains(search) ?? false));
    }
}
