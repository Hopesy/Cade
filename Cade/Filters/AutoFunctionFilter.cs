using Cade.Interfaces;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Spectre.Console;

namespace Cade.Filters;

/// <summary>
/// 自动函数调用过滤器 - 显示 AI 的思考过程
/// </summary>
public class AutoFunctionFilter : IAutoFunctionInvocationFilter
{
    private readonly IUserInterface _ui;

    public AutoFunctionFilter(IUserInterface ui)
    {
        _ui = ui;
    }

    public async Task OnAutoFunctionInvocationAsync(
        AutoFunctionInvocationContext context,
        Func<AutoFunctionInvocationContext, Task> next)
    {
        // 获取 AI 的思考内容（在调用工具之前的文本）
        var chatHistory = context.ChatHistory;
        if (chatHistory.Count > 0)
        {
            var lastMessage = chatHistory[^1];
            // 如果最后一条消息是助手消息且有内容，显示思考过程
            if (lastMessage.Role == AuthorRole.Assistant && !string.IsNullOrWhiteSpace(lastMessage.Content))
            {
                var thought = lastMessage.Content.Trim();
                if (thought.Length > 100)
                    thought = thought[..100] + "...";
                
                _ui.SafeRender(() =>
                {
                    AnsiConsole.MarkupLine($"[grey]💭 {Markup.Escape(thought)}[/]");
                });
            }
        }

        await next(context);
    }
}
