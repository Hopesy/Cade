using Cade.Interfaces;
using Spectre.Console;

namespace Cade.Services;

public class ConsoleUserInterface : IUserInterface
{
    // 定义主题颜色
    private static readonly Color PrimaryColor = new Color(217, 119, 87); // #D97757
    private static readonly Color SecondaryColor = Color.Orange1;
    private static readonly Color AccentColor = Color.LightSlateGrey;

    public void ShowWelcome()
    {
        AnsiConsole.Clear();

        // 渲染 Logo
        AnsiConsole.Write(
            new FigletText("Cade Code")
                .Color(PrimaryColor)
                .LeftJustified());

        // 渲染欢迎语和状态检查
        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("green"))
            .Start("正在初始化系统模块...", ctx =>
            {
                Thread.Sleep(800);
                ctx.Status("加载核心神经网络...");
                Thread.Sleep(800);
                ctx.Status("建立安全连接...");
                Thread.Sleep(600);
            });

        AnsiConsole.MarkupLine($"[bold {PrimaryColor.ToMarkup()}]欢迎使用 Cade CLI[/] - [grey]v1.0.0[/]");
        AnsiConsole.MarkupLine("[grey]Type /help for more information.[/]");
        AnsiConsole.Write(new Rule("[grey]Ready[/]").LeftJustified());
        AnsiConsole.WriteLine();
    }

    public string GetInput(string prompt, string path, string modelId)
    {
        prompt = ">>";
        var width = AnsiConsole.Profile.Width;
        var contentWidth = Math.Max(0, width - 2); // 减去左右边框
        
        var borderColor = "[grey]";
        var promptColor = $"[bold {SecondaryColor.ToMarkup()}]";
        var primaryColor = $"[bold {PrimaryColor.ToMarkup()}]";

        // 1. 渲染顶部边框
        AnsiConsole.MarkupLine($"{borderColor}╭{new string('─', contentWidth)}╮[/]");

        // 2. 渲染中间行 (Prompt)
        // 我们先打印左边框和提示符，不换行
        AnsiConsole.Markup($"{borderColor}│[/] {promptColor}{prompt}[/] ");
        
        // 记录输入光标的起始位置
        var (inputLeft, inputTop) = Console.GetCursorPosition();

        // 为了打印底部边框和状态栏，我们需要先换行
        Console.WriteLine(); 

        // 3. 渲染底部边框
        AnsiConsole.MarkupLine($"{borderColor}╰{new string('─', contentWidth)}╯[/]");

        // 4. 渲染状态栏
        var grid = new Grid();
        grid.Width(width);
        grid.AddColumn(new GridColumn().LeftAligned().PadRight(2));
        grid.AddColumn(new GridColumn().RightAligned());
        grid.AddRow($"[grey]{Markup.Escape(path)}[/]", $"{primaryColor}{modelId}[/]");
        AnsiConsole.Write(grid);
        
        // 记录结束位置
        var (endLeft, endTop) = Console.GetCursorPosition();

        // 5. 将光标移动回输入位置
        try 
        {
            Console.SetCursorPosition(inputLeft, inputTop);
        }
        catch
        {
            // 如果发生异常（如滚屏导致坐标失效），则不回溯，直接在当前位置输入（降级体验）
        }
        
        // 6. 读取用户输入
        var input = Console.ReadLine();

        // 7. 恢复光标到UI组件之后，防止后续输出覆盖底部边框
        try
        {
            // Console.ReadLine 会导致光标下移一行（进入底部边框行）
            // 我们需要确保光标移动到状态栏之后
            if (Console.CursorTop < endTop)
            {
                Console.SetCursorPosition(0, endTop);
            }
        }
        catch
        {
            // 忽略光标移动错误
        }
        
        return input ?? string.Empty;
    }

    public void ShowResponse(string content)
    {
        // 使用 Markup 对象
        var markup = new Markup(content);

        // 使用 Panel 包裹回复
        var panel = new Panel(markup)
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(AccentColor),
            Padding = new Padding(1, 1, 1, 1),
            Header = new PanelHeader("[bold]Cade[/]", Justify.Left)
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    public void ShowError(string message)
    {
        AnsiConsole.MarkupLine($"[bold red]Error:[/] {message}");
    }

    public void ShowThinking(Action action)
    {
        AnsiConsole.MarkupLine(""); 
        AnsiConsole.Status()
            .Spinner(Spinner.Known.BouncingBar)
            .SpinnerStyle(Style.Parse("yellow"))
            .Start("Cade 正在思考...", _ => action());
    }

    public async Task ShowThinkingAsync(Func<Task> action)
    {
        AnsiConsole.MarkupLine("");
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.BouncingBar)
            .SpinnerStyle(Style.Parse("yellow"))
            .StartAsync("Cade 正在思考...", _ => action());
    }

    public void ShowToolLog(string toolName, string command)
    {
        // 模拟 Claude Code 的工具调用样式
        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap().PadRight(2));
        grid.AddColumn();

        grid.AddRow(
            $"[bold {AccentColor.ToMarkup()}]{toolName}[/]", 
            $"[grey]{Markup.Escape(command)}[/]"
        );

        AnsiConsole.Write(grid);
    }

    public void ShowLog(string message)
    {
        AnsiConsole.MarkupLine($"[grey]  {Markup.Escape(message)}[/]");
    }
}
