using System.Text;
using System.Text.RegularExpressions;

namespace Cade.Services;

/// <summary>
/// 简单的 Markdown 到 Spectre.Console Markup 转换器
/// </summary>
public static class MarkdownRenderer
{
    /// <summary>
    /// 将 Markdown 文本转换为 Spectre.Console 可渲染的 Markup
    /// </summary>
    public static string ToMarkup(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return string.Empty;

        var result = markdown;

        // 1. 代码块 (```)
        result = Regex.Replace(result, @"```(\w*)\n(.*?)\n```", match =>
        {
            var code = match.Groups[2].Value;
            // 转义代码中的特殊字符
            code = EscapeMarkup(code);
            return $"[grey on black]{code}[/]";
        }, RegexOptions.Singleline);

        // 2. 行内代码 (`)
        result = Regex.Replace(result, @"`([^`]+)`", match =>
        {
            var code = EscapeMarkup(match.Groups[1].Value);
            return $"[grey on black]{code}[/]";
        });

        // 3. 粗体 (**)
        result = Regex.Replace(result, @"\*\*([^\*]+)\*\*", "[bold]$1[/]");

        // 4. 斜体 (*)
        result = Regex.Replace(result, @"\*([^\*]+)\*", "[italic]$1[/]");

        // 5. 链接 [text](url)
        result = Regex.Replace(result, @"\[([^\]]+)\]\(([^\)]+)\)", "[link]$1[/] [grey]($2)[/]");

        // 6. 标题 (#)
        result = Regex.Replace(result, @"^###\s+(.+)$", "[bold blue]$1[/]", RegexOptions.Multiline);
        result = Regex.Replace(result, @"^##\s+(.+)$", "[bold cyan]$1[/]", RegexOptions.Multiline);
        result = Regex.Replace(result, @"^#\s+(.+)$", "[bold green]$1[/]", RegexOptions.Multiline);

        // 7. 列表项 (- 或 *)
        result = Regex.Replace(result, @"^[\*\-]\s+(.+)$", "  • $1", RegexOptions.Multiline);

        // 8. 有序列表 (1. 2. 3.)
        result = Regex.Replace(result, @"^\d+\.\s+(.+)$", "  $0", RegexOptions.Multiline);

        return result;
    }

    /// <summary>
    /// 转义 Spectre.Console Markup 特殊字符
    /// </summary>
    private static string EscapeMarkup(string text)
    {
        return text.Replace("[", "[[").Replace("]", "]]");
    }
}
