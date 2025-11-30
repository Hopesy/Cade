using Spectre.Console;
using Spectre.Console.Json;
using System;
using System.Text;
using System.Threading.Tasks;

class Example
{
    // 输入缓存
    private static StringBuilder _inputBuffer = new StringBuilder();

    // 线程锁，防止后台AI输出时打乱前台输入框的渲染
    private static readonly object _consoleLock = new object();

    // 标记是否正在处理（影响 Prompt 颜色）
    private static bool _isProcessing = false;
    static async Task Main(string[] args)
    {
        // 1. 设置控制台编码，防止乱码666666
        Console.OutputEncoding = Encoding.UTF8;

        // 2. 初始化界面
        AnsiConsole.Clear();

        // 打印标题 (修复：使用 LeftJustified)
        AnsiConsole.Write(
            new FigletText("AI TERMINAL")
                .LeftJustified() // <--- 已修复
                .Color(Color.Purple));
        AnsiConsole.Write(new Rule("[yellow]System Initialized[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();

        // 3. 首次渲染输入框
        RenderInputLine();

        // 4. 主循环：监听按键
        while (true)
        {
            // 使用 KeyAvailable 避免阻塞，这样后台任务运行时 UI 依然响应
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true);
                HandleKeyPress(key);
            }

            // 短暂休眠以降低 CPU 占用
            await Task.Delay(10);
        }
    }
    /// <summary>
    /// 处理键盘输入
    /// </summary>
    private static void HandleKeyPress(ConsoleKeyInfo keyInfo)
    {
        // 加锁，防止和 AI 输出冲突
        lock (_consoleLock)
        {
            if (keyInfo.Key == ConsoleKey.Enter)
            {
                if (_inputBuffer.Length > 0)
                {
                    string input = _inputBuffer.ToString();
                    _inputBuffer.Clear();

                    // 1. 将用户的输入上推到历史记录区
                    PrintUserMessage(input);

                    // 2. 标记状态为处理中
                    _isProcessing = true;

                    // 3. 立即重绘输入框（清空 Buffer）
                    RenderInputLine();

                    // 4. 启动后台 AI 任务 (不等待它完成，让主循环继续)
                    Task.Run(() => RunAiToolChainAsync(input));
                }
            }
            else if (keyInfo.Key == ConsoleKey.Backspace)
            {
                if (_inputBuffer.Length > 0)
                {
                    _inputBuffer.Remove(_inputBuffer.Length - 1, 1);
                    RenderInputLine();
                }
            }
            // 忽略特殊控制键，只接受普通字符
            else if (!char.IsControl(keyInfo.KeyChar))
            {
                _inputBuffer.Append(keyInfo.KeyChar);
                RenderInputLine();
            }
        }
    }
    /// <summary>
    /// 在上方打印用户的历史消息
    /// </summary>
    private static void PrintUserMessage(string message)
    {
        ClearInputLine();
        var grid = new Grid();
        grid.AddColumn(new GridColumn().Padding(0, 0, 1, 0));
        grid.AddColumn(new GridColumn());

        // 用户图标和消息内容
        grid.AddRow(new Markup("[green]➜[/]"), new Markup($"[bold white]{Markup.Escape(message)}[/]"));
        AnsiConsole.Write(grid);
    }
    /// <summary>
    /// 【核心方法】线程安全地在输入框上方打印内容
    /// 这个方法会自动擦除输入框 -> 打印内容 -> 恢复输入框
    /// </summary>
    private static void SafePrint(Action renderAction)
    {
        lock (_consoleLock)
        {
            ClearInputLine();
            renderAction(); // 执行传入的 Spectre 渲染逻辑
            RenderInputLine();
        }
    }
    /// <summary>
    /// 模拟 AI 工具链执行过程 (后台线程)
    /// </summary>
    private static async Task RunAiToolChainAsync(string query)
    {
        // 模拟网络延迟
        await Task.Delay(500);

        // 阶段 1: 思考
        SafePrint(() =>
            AnsiConsole.MarkupLine($"[grey]Allocating neural pathways for query: '{Markup.Escape(query)}'...[/]"));
        await Task.Delay(800);

        // 阶段 2: 模拟工具调用 (展示 JSON)
        var toolJson = "{\"tool\": \"weather_api\", \"params\": {\"location\": \"Shanghai\", \"unit\": \"c\"}}";
        SafePrint(() =>
        {
            var panel = new Panel(new JsonText(toolJson))
                .Header(" invoking [bold yellow]WeatherTool[/] ")
                .BorderColor(Color.Yellow)
                .RoundedBorder();
            AnsiConsole.Write(panel);
        });
        await Task.Delay(1000);

        // 阶段 3: 模拟工具返回
        var resultJson = "{\"temperature\": 24, \"condition\": \"Cloudy\", \"wind\": \"NE 3m/s\"}";
        SafePrint(() =>
        {
            var panel = new Panel(new JsonText(resultJson))
                .Header(" [bold green]Tool Output[/] ")
                .BorderColor(Color.Green)
                .RoundedBorder();
            AnsiConsole.Write(panel);
        });
        await Task.Delay(600);

        // 阶段 4: 最终回复
        SafePrint(() =>
        {
            // 修复：使用 LeftJustified
            AnsiConsole.Write(new Rule("[cyan]AI Response[/]").LeftJustified());
            AnsiConsole.MarkupLine($"[cyan]AI[/]: 根据工具返回的数据，上海目前气温 [bold white]24°C[/]，多云。建议携带雨具。");
            AnsiConsole.WriteLine(); // 留白
        });

        // 任务结束，恢复输入框状态
        _isProcessing = false;
        lock (_consoleLock)
        {
            RenderInputLine();
        }
    }
    /// <summary>
    /// 渲染底部的输入行
    /// </summary>
    private static void RenderInputLine()
    {
        // 回到行首
        Console.Write("\r");
        if (_isProcessing)
        {
            AnsiConsole.Markup("[blue]AI Thinking...[/] > ");
        }
        else
        {
            AnsiConsole.Markup("[fuchsia]User Input[/] > ");
        }

        // 打印当前 Buffer
        Console.Write(_inputBuffer.ToString());

        // 清除光标右侧残留字符 (防止删除时显示错误)
        int currentLeft = Console.CursorLeft;
        int consoleWidth = Console.WindowWidth;
        int spacesToClear = Math.Max(0, consoleWidth - currentLeft - 1);
        Console.Write(new string(' ', spacesToClear));

        // 恢复光标位置
        Console.CursorLeft = currentLeft;
    }
    /// <summary>
    /// 擦除当前行（为了让上面的日志打印时不产生视觉残留）
    /// </summary>
    private static void ClearInputLine()
    {
        Console.Write("\r" + new string(' ', Console.WindowWidth - 1) + "\r");
    }
}