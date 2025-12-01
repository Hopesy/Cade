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
        code = code.TrimEnd();

        var languageLabel = string.IsNullOrWhiteSpace(language) ? "code" : language;

        // 使用简单的语法高亮功能
        IRenderable codeRenderable;

        try
        {
            var syntaxLanguage = MapLanguageToSyntax(language);

            if (!string.IsNullOrWhiteSpace(syntaxLanguage))
            {
                // 应用语法高亮
                var highlightedCode = ApplySyntaxHighlighting(code, syntaxLanguage);
                codeRenderable = new Markup(highlightedCode);
            }
            else
            {
                // 如果语言不支持，使用纯文本
                var escapedCode = EscapeMarkup(code);
                codeRenderable = new Markup($"[grey]{escapedCode}[/]");
            }
        }
        catch
        {
            // 如果高亮失败，降级到纯文本
            var escapedCode = EscapeMarkup(code);
            codeRenderable = new Markup($"[grey]{escapedCode}[/]");
        }

        var panel = new Panel(codeRenderable)
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey),
            Padding = new Padding(1, 0, 1, 0),
            Header = new PanelHeader($" [dim]{languageLabel}[/] ", Justify.Left)
        };

        return panel;
    }

    /// <summary>
    /// 应用简单的语法高亮
    /// </summary>
    private static string ApplySyntaxHighlighting(string code, string language)
    {
        // 先转义 Markup 特殊字符
        code = EscapeMarkup(code);

        // 根据语言应用不同的高亮规则
        return language switch
        {
            "csharp" => HighlightCSharp(code),
            "javascript" or "typescript" => HighlightJavaScript(code),
            "python" => HighlightPython(code),
            "json" => HighlightJson(code),
            "xml" or "html" => HighlightXml(code),
            "bash" or "powershell" => HighlightShell(code),
            "sql" => HighlightSql(code),
            _ => $"[white]{code}[/]" // 默认白色
        };
    }

    /// <summary>
    /// C# 语法高亮
    /// </summary>
    private static string HighlightCSharp(string code)
    {
        // 关键字
        var keywords = new[] {
            "using", "namespace", "class", "interface", "struct", "enum", "delegate",
            "public", "private", "protected", "internal", "static", "readonly", "const",
            "void", "int", "string", "bool", "double", "float", "long", "var", "object",
            "if", "else", "for", "foreach", "while", "do", "switch", "case", "return",
            "new", "this", "base", "null", "true", "false", "async", "await", "try", "catch",
            "finally", "throw", "get", "set", "value", "override", "virtual", "abstract"
        };

        // 高亮关键字（蓝色）
        foreach (var keyword in keywords)
        {
            code = Regex.Replace(code, $@"\b{keyword}\b", $"[blue]{keyword}[/]", RegexOptions.Multiline);
        }

        // 高亮字符串（黄色）
        code = Regex.Replace(code, @"""[^""]*""", m => $"[yellow]{m.Value}[/]", RegexOptions.Multiline);

        // 高亮注释（绿色）
        code = Regex.Replace(code, @"//.*$", m => $"[green]{m.Value}[/]", RegexOptions.Multiline);

        // 高亮数字（青色）
        code = Regex.Replace(code, @"\b\d+\.?\d*\b", m => $"[cyan]{m.Value}[/]");

        return code;
    }

    /// <summary>
    /// JavaScript/TypeScript 语法高亮
    /// </summary>
    private static string HighlightJavaScript(string code)
    {
        var keywords = new[] {
            "const", "let", "var", "function", "return", "if", "else", "for", "while",
            "switch", "case", "break", "continue", "class", "extends", "constructor",
            "this", "super", "new", "typeof", "instanceof", "import", "export", "from",
            "async", "await", "try", "catch", "finally", "throw", "null", "undefined",
            "true", "false", "interface", "type", "enum"
        };

        foreach (var keyword in keywords)
        {
            code = Regex.Replace(code, $@"\b{keyword}\b", $"[blue]{keyword}[/]", RegexOptions.Multiline);
        }

        code = Regex.Replace(code, @"'[^']*'|""[^""]*""|`[^`]*`", m => $"[yellow]{m.Value}[/]", RegexOptions.Multiline);
        code = Regex.Replace(code, @"//.*$", m => $"[green]{m.Value}[/]", RegexOptions.Multiline);
        code = Regex.Replace(code, @"\b\d+\.?\d*\b", m => $"[cyan]{m.Value}[/]");

        return code;
    }

    /// <summary>
    /// Python 语法高亮
    /// </summary>
    private static string HighlightPython(string code)
    {
        var keywords = new[] {
            "def", "class", "return", "if", "elif", "else", "for", "while", "in",
            "import", "from", "as", "try", "except", "finally", "raise", "with",
            "lambda", "yield", "async", "await", "None", "True", "False", "and",
            "or", "not", "is", "pass", "break", "continue"
        };

        foreach (var keyword in keywords)
        {
            code = Regex.Replace(code, $@"\b{keyword}\b", $"[blue]{keyword}[/]", RegexOptions.Multiline);
        }

        code = Regex.Replace(code, @"'[^']*'|""[^""]*""", m => $"[yellow]{m.Value}[/]", RegexOptions.Multiline);
        code = Regex.Replace(code, @"#.*$", m => $"[green]{m.Value}[/]", RegexOptions.Multiline);
        code = Regex.Replace(code, @"\b\d+\.?\d*\b", m => $"[cyan]{m.Value}[/]");

        return code;
    }

    /// <summary>
    /// JSON 语法高亮
    /// </summary>
    private static string HighlightJson(string code)
    {
        // 高亮键名（青色）
        code = Regex.Replace(code, @"""([^""]+)""\s*:", m =>
        {
            var key = m.Groups[1].Value;
            return $"[cyan]\"{key}\"[/]:";
        }, RegexOptions.Multiline);

        // 高亮字符串值（黄色）
        code = Regex.Replace(code, @":\s*""([^""]*)""", m =>
        {
            var value = m.Groups[1].Value;
            return $": [yellow]\"{value}\"[/]";
        }, RegexOptions.Multiline);

        // 高亮布尔值和 null（蓝色）
        code = Regex.Replace(code, @"\b(true|false|null)\b", m => $"[blue]{m.Value}[/]");

        // 高亮数字（青色）
        code = Regex.Replace(code, @"\b\d+\.?\d*\b", m => $"[cyan]{m.Value}[/]");

        return code;
    }

    /// <summary>
    /// XML/HTML 语法高亮
    /// </summary>
    private static string HighlightXml(string code)
    {
        // 高亮标签（蓝色）
        code = Regex.Replace(code, @"</?([a-zA-Z0-9]+)", m =>
        {
            var tag = m.Groups[1].Value;
            return $"[blue]<{tag}[/]";
        });

        // 高亮属性名（青色）
        code = Regex.Replace(code, @"\s([a-zA-Z-]+)=", m =>
        {
            var attr = m.Groups[1].Value;
            return $" [cyan]{attr}[/]=";
        });

        // 高亮属性值（黄色）
        code = Regex.Replace(code, @"""[^""]*""", m => $"[yellow]{m.Value}[/]");

        return code;
    }

    /// <summary>
    /// Shell 脚本语法高亮
    /// </summary>
    private static string HighlightShell(string code)
    {
        // 高亮注释（绿色）
        code = Regex.Replace(code, @"#.*$", m => $"[green]{m.Value}[/]", RegexOptions.Multiline);

        // 高亮字符串（黄色）
        code = Regex.Replace(code, @"'[^']*'|""[^""]*""", m => $"[yellow]{m.Value}[/]", RegexOptions.Multiline);

        // 高亮变量（青色）
        code = Regex.Replace(code, @"\$[a-zA-Z_][a-zA-Z0-9_]*", m => $"[cyan]{m.Value}[/]");

        return code;
    }

    /// <summary>
    /// SQL 语法高亮
    /// </summary>
    private static string HighlightSql(string code)
    {
        var keywords = new[] {
            "SELECT", "FROM", "WHERE", "INSERT", "UPDATE", "DELETE", "CREATE", "DROP",
            "ALTER", "TABLE", "INDEX", "JOIN", "LEFT", "RIGHT", "INNER", "OUTER",
            "ON", "AND", "OR", "NOT", "NULL", "IS", "IN", "LIKE", "ORDER", "BY",
            "GROUP", "HAVING", "AS", "DISTINCT", "COUNT", "SUM", "AVG", "MAX", "MIN"
        };

        foreach (var keyword in keywords)
        {
            code = Regex.Replace(code, $@"\b{keyword}\b", $"[blue]{keyword}[/]", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        }

        code = Regex.Replace(code, @"'[^']*'", m => $"[yellow]{m.Value}[/]", RegexOptions.Multiline);
        code = Regex.Replace(code, @"--.*$", m => $"[green]{m.Value}[/]", RegexOptions.Multiline);

        return code;
    }

    /// <summary>
    /// 映射 Markdown 语言标识到 Spectre.Console Syntax 支持的语言
    /// </summary>
    private static string MapLanguageToSyntax(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return string.Empty;

        // 转换为小写以便匹配
        var lang = language.ToLowerInvariant();

        // Spectre.Console 支持的语言映射
        return lang switch
        {
            // C# 和 .NET
            "c#" or "csharp" or "cs" => "csharp",

            // Web 开发
            "javascript" or "js" => "javascript",
            "typescript" or "ts" => "typescript",
            "html" or "htm" => "html",
            "css" => "css",
            "json" => "json",
            "xml" => "xml",

            // 其他流行语言
            "python" or "py" => "python",
            "java" => "java",
            "go" or "golang" => "go",
            "rust" or "rs" => "rust",
            "cpp" or "c++" or "cxx" => "cpp",
            "c" => "c",

            // 脚本和配置
            "bash" or "sh" or "shell" => "bash",
            "powershell" or "ps1" or "pwsh" => "powershell",
            "sql" => "sql",
            "yaml" or "yml" => "yaml",
            "toml" => "toml",

            // 标记语言
            "markdown" or "md" => "markdown",

            // 其他
            "diff" or "patch" => "diff",

            // 默认：尝试原样传递
            _ => lang
        };
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
