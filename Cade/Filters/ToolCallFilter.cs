using System.Diagnostics;
using System.Text;
using Cade.Interfaces;
using Microsoft.SemanticKernel;
using Spectre.Console;

namespace Cade.Filters;

/// <summary>
/// 工具调用过滤器 - Claude Code 风格
/// 执行时：闪烁圆圈 + 工具名 + 执行时间
/// 完成后：显示结果
/// </summary>
public class ToolCallFilter : IFunctionInvocationFilter
{
    private readonly IUserInterface _ui;

    public ToolCallFilter(IUserInterface ui)
    {
        _ui = ui;
    }

    public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
    {
        var functionName = context.Function.Name;
        var argsDisplay = BuildArgsDisplay(context.Arguments);
        var displayName = string.IsNullOrEmpty(argsDisplay) ? functionName : $"{functionName}({argsDisplay})";

        var stopwatch = Stopwatch.StartNew();

        // 显示开始执行（转义特殊字符避免 Markup 解析问题，并限制长度避免换行）
        var safeDisplayName = displayName.Replace("[", "[[").Replace("]", "]]");
        // 限制状态行长度，避免换行导致渲染问题
        var maxLen = Math.Max(20, Console.WindowWidth - 20);
        if (safeDisplayName.Length > maxLen)
            safeDisplayName = safeDisplayName[..maxLen] + "...";
        _ui.SetProcessing(true, $"● {safeDisplayName}");

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            _ui.SetProcessing(false);

            // 使用 SafeRender 确保线程安全
            var elapsed = stopwatch.Elapsed.TotalSeconds;
            var resultValue = context.Result?.GetValue<object>()?.ToString() ?? "";

            _ui.SafeRender(() =>
            {
                // 显示完成状态
                AnsiConsole.MarkupLine($"[green]●[/] [white]{Markup.Escape(displayName)}[/] [dim]({elapsed:F1}s)[/]");

                // 显示执行结果 (L型线条只在第一行)
                if (!string.IsNullOrWhiteSpace(resultValue))
                {
                    var output = resultValue.Length > 500 ? resultValue[..500] + "..." : resultValue;
                    var lines = output.Split('\n').Take(10).ToArray();

                    for (int i = 0; i < lines.Length; i++)
                    {
                        var prefix = i == 0 ? "╰─" : "  ";
                        AnsiConsole.MarkupLine($"[dim]{prefix} {Markup.Escape(lines[i])}[/]");
                    }

                    if (output.Split('\n').Length > 10)
                    {
                        AnsiConsole.MarkupLine($"[dim]   ...[/]");
                    }
                }
            });
        }
    }

    // 不应该显示的参数名（内容类参数）
    private static readonly HashSet<string> ExcludedArgNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "content", "text", "body", "data", "input", "code", "script", "json", "xml", "html"
    };

    private static string BuildArgsDisplay(KernelArguments? arguments)
    {
        if (arguments == null || arguments.Count == 0)
            return "";

        // 只显示第一个非内容类参数（通常是路径、名称等）
        var firstArg = arguments
            .Where(a => !ExcludedArgNames.Contains(a.Key))
            .Take(1)
            .Select(a =>
            {
                var value = a.Value?.ToString() ?? "null";
                if (value.Length > 40)
                    value = value[..40] + "...";
                return value;
            })
            .FirstOrDefault();

        return firstArg ?? "";
    }
}
