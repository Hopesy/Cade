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
        // 确保 prompt 只是 >>
        prompt = ">>";
        var borderColor = "grey dim";
        var width = AnsiConsole.Profile.Width;
        var contentWidth = width > 2 ? width - 2 : 10;

        // 1. Render Top Rounded Border
        // ╭──────────────────╮
        // 使用 string 构造，避免 Rule 的直线
        AnsiConsole.MarkupLine($"[{borderColor}]╭{new string('─', contentWidth)}╮[/]");

        // 2. Ask Input with Left Border
        // │ >> 
        var input = AnsiConsole.Prompt(
            new TextPrompt<string>($"[{borderColor}]│[/] [bold {SecondaryColor.ToMarkup()}]{prompt}[/] ")
                .PromptStyle("white")
                .AllowEmpty());

        // 3. Render Bottom Rounded Border
        // ╰──────────────────╯
        AnsiConsole.MarkupLine($"[{borderColor}]╰{new string('─', contentWidth)}╯[/]");

        // 4. Render Status Bar (Below Input)
        var grid = new Grid();
        grid.Width(width);
        grid.AddColumn(new GridColumn().LeftAligned().PadRight(2));
        grid.AddColumn(new GridColumn().RightAligned());

        var pathMarkup = $"[grey]{Markup.Escape(path)}[/]";
        // 模型ID不带前缀，只显示ID
        var modelMarkup = $"[bold {PrimaryColor.ToMarkup()}]{modelId}[/]";

        grid.AddRow(pathMarkup, modelMarkup);
        AnsiConsole.Write(grid);
        AnsiConsole.WriteLine(); // 增加一点间距
        
        return input;
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
