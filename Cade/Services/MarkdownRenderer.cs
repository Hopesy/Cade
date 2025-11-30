using System.Text;
using System.Text.RegularExpressions;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Cade.Services;

/// <summary>
/// Markdown 渲染结果
/// </summary>
public class MarkdownContent
{
    public List<IRenderable> Elements { get; set; } = new();
}

/// <summary>
/// 简单的 Markdown 到 Spectre.Console Markup 转换器
/// </summary>
public static class MarkdownRenderer
{
    /// <summary>
    /// 将 Markdown 文本解析为可渲染的元素列表
    /// </summary>
    public static MarkdownContent Parse(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return new MarkdownContent();

        var content = new MarkdownContent();
        var parts = SplitCodeBlocks(markdown);

        foreach (var part in parts)
        {
            if (part.IsCodeBlock)
            {
                // 渲染代码块为 Panel
                content.Elements.Add(CreateCodeBlockPanel(part.Content, part.Language));
            }
            else
            {
                // 渲染普通文本
                var markup = ConvertToMarkup(part.Content);
                if (!string.IsNullOrWhiteSpace(markup))
                {
                    content.Elements.Add(new Markup(markup));
                }
            }
        }

        return content;
    }

    private class TextPart
    {
        public string Content { get; set; } = string.Empty;
        public bool IsCodeBlock { get; set; }
        public string Language { get; set; } = string.Empty;
    }

    private static List<TextPart> SplitCodeBlocks(string markdown)
    {
        var parts = new List<TextPart>();
        var pattern = @"```(\w*)\n(.*?)\n```";
        var matches = Regex.Matches(markdown, pattern, RegexOptions.Singleline);

        if (matches.Count == 0)
        {
            parts.Add(new TextPart { Content = markdown, IsCodeBlock = false });
            return parts;
        }

        int lastIndex = 0;
        foreach (Match match in matches)
        {
            // 添加代码块之前的文本
            if (match.Index > lastIndex)
            {
                var text = markdown.Substring(lastIndex, match.Index - lastIndex);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    parts.Add(new TextPart { Content = text, IsCodeBlock = false });
                }
            }

            // 添加代码块
            parts.Add(new TextPart
            {
                Content = match.Groups[2].Value,
                IsCodeBlock = true,
                Language = match.Groups[1].Value
            });

            lastIndex = match.Index + match.Length;
        }

        // 添加最后的文本
        if (lastIndex < markdown.Length)
        {
            var text = markdown.Substring(lastIndex);
            if (!string.IsNullOrWhiteSpace(text))
            {
                parts.Add(new TextPart { Content = text, IsCodeBlock = false });
            }
        }

        return parts;
    }

    private static Panel CreateCodeBlockPanel(string code, string language)
    {
        // 转义 Markup 特殊字符
        code = EscapeMarkup(code.TrimEnd());

        var languageLabel = string.IsNullOrWhiteSpace(language) ? "code" : language;

        var panel = new Panel(new Markup($"[grey]{code}[/]"))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey),
            Padding = new Padding(1, 0, 1, 0),
            Header = new PanelHeader($" [dim]{languageLabel}[/] ", Justify.Left)
        };

        return panel;
    }

    /// <summary>
    /// 将普通文本转换为 Markup
    /// </summary>
    private static string ConvertToMarkup(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var result = text;

        // 1. 行内代码 (`)
        result = Regex.Replace(result, @"`([^`]+)`", match =>
        {
            var code = EscapeMarkup(match.Groups[1].Value);
            return $"[yellow on grey15]{code}[/]";
        });

        // 2. 粗体 (**)
        result = Regex.Replace(result, @"\*\*([^\*]+)\*\*", "[bold]$1[/]");

        // 3. 斜体 (*)
        result = Regex.Replace(result, @"\*([^\*]+)\*", "[italic]$1[/]");

        // 4. 链接 [text](url)
        result = Regex.Replace(result, @"\[([^\]]+)\]\(([^\)]+)\)", "[link]$1[/] [dim]($2)[/]");

        // 5. 标题 (#)
        result = Regex.Replace(result, @"^####\s+(.+)$", "[bold white]$1[/]", RegexOptions.Multiline);
        result = Regex.Replace(result, @"^###\s+(.+)$", "[bold blue]$1[/]", RegexOptions.Multiline);
        result = Regex.Replace(result, @"^##\s+(.+)$", "[bold cyan]$1[/]", RegexOptions.Multiline);
        result = Regex.Replace(result, @"^#\s+(.+)$", "[bold green]$1[/]", RegexOptions.Multiline);

        // 6. 列表项 (- 或 *)
        result = Regex.Replace(result, @"^[\*\-]\s+(.+)$", "  [grey]•[/] $1", RegexOptions.Multiline);

        // 7. 有序列表 (1. 2. 3.)
        result = Regex.Replace(result, @"^(\d+)\.\s+(.+)$", "  [grey]$1.[/] $2", RegexOptions.Multiline);

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
