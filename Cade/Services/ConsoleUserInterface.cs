using System;
using System.Text;
using Cade.Interfaces;
using Spectre.Console;
using Spectre.Console.Json;

namespace Cade.Services;

public class ConsoleUserInterface : IUserInterface
{
    private readonly StringBuilder _inputBuffer = new StringBuilder();
    private readonly object _consoleLock = new object();
    private bool _isProcessing = false;
    private string _statusTitle = "Thinking...";
    private DateTime _processStartTime = DateTime.Now;

    // Spinner state
    private readonly string[] _spinnerFrames = {
        "[[   ]]", "[[=  ]]", "[[== ]]", "[[===]]", "[[ ==]]", "[[  =]]"
    };
    private int _spinnerFrame = 0;
    private DateTime _lastSpinnerTick = DateTime.MinValue;

    // AI 回复动画点（Gemini 风格脉动效果）
    // 动画效果：空 → 1个点 → 2个点 → 3个点 → 2个点 → 1个点 → 空（循环）
    private readonly string[] _aiResponseDots = {
        " ",   // 空
        "·",   // 1个点
        ":",   // 2个点
        "⋮",   // 3个点
        ":",   // 2个点
        "·"    // 1个点
    };
    private int _aiDotFrame = 0;
    private bool _showingResponseHeader = false;
    private string _currentResponseSummary = string.Empty;
    private int _responseHeaderLine = -1; // 记录回复头部所在的行号
    private DateTime _responseHeaderStartTime = DateTime.MinValue;
    private readonly TimeSpan _responseHeaderDuration = TimeSpan.FromSeconds(2); // 动画持续时间

    // Colors
    private static readonly Color PrimaryColor = new Color(217, 119, 87); // #D97757
    private static readonly Color SecondaryColor = Color.Orange1;
    private static readonly Color AccentColor = Color.LightSlateGrey;

    public bool KeyAvailable => Console.KeyAvailable;

    public void ShowWelcome()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(
            new FigletText("Cade Code")
                .Color(PrimaryColor)
                .LeftJustified());

        AnsiConsole.WriteLine();

        // 提示信息
        AnsiConsole.MarkupLine("[bold]Tips for getting started:[/]");
        AnsiConsole.MarkupLine("  [grey]1.[/] Ask questions, edit files, or run commands.");
        AnsiConsole.MarkupLine("  [grey]2.[/] Be specific for the best results.");
        AnsiConsole.MarkupLine("  [grey]3.[/] [cyan]/help[/] for more information.");

        AnsiConsole.WriteLine();

        RenderBottomArea();
    }

    public void SetStatus(string path, string modelId)
    {
        // No-op for now or implement status bar if needed
    }

    public void SetProcessing(bool isProcessing, string? title = null)
    {
        lock (_consoleLock)
        {
            if (isProcessing && !_isProcessing)
            {
                _processStartTime = DateTime.Now;
                _spinnerFrame = 0;
                Console.CursorVisible = false; // 隐藏光标
            }

            // 必须在改变状态前清除旧区域，因为 ClearBottomArea 依赖 _isProcessing 来计算高度
            ClearBottomArea();

            _isProcessing = isProcessing;
            if (title != null) _statusTitle = title;

            // 停止处理时恢复光标
            if (!isProcessing)
            {
                Console.CursorVisible = true; // 恢复光标
            }

            // Re-render to show/hide status line
            RenderBottomArea();
        }
    }

    public void Update()
    {
        bool needRender = false;

        // 更新 AI 回复头部动画
        if (_showingResponseHeader)
        {
            // Gemini 风格：更快的脉动效果
            if ((DateTime.Now - _lastSpinnerTick).TotalMilliseconds > 150)
            {
                _aiDotFrame = (_aiDotFrame + 1) % _aiResponseDots.Length;
                _lastSpinnerTick = DateTime.Now;

                lock (_consoleLock)
                {
                    UpdateResponseHeader();
                }
            }
        }
        else if (_isProcessing)
        {
            // 更新底部处理状态（思考动画）
            if ((DateTime.Now - _lastSpinnerTick).TotalMilliseconds > 100)
            {
                _spinnerFrame = (_spinnerFrame + 1) % _spinnerFrames.Length;
                _lastSpinnerTick = DateTime.Now;
                needRender = true;
            }

            if (needRender)
            {
                lock (_consoleLock)
                {
                    RenderBottomArea(overwrite: true);
                }
            }
        }
    }

    private void UpdateResponseHeader()
    {
        if (_responseHeaderLine < 0) return;

        // 保存当前光标位置
        var currentTop = Console.CursorTop;
        var currentLeft = Console.CursorLeft;

        // 移动到回复头部行
        Console.SetCursorPosition(0, _responseHeaderLine);

        // 清除该行
        ClearCurrentLine();

        // 重新绘制动画点 + 总结
        var dots = _aiResponseDots[_aiDotFrame];
        Console.Write($"\x1b[38;2;{PrimaryColor.R};{PrimaryColor.G};{PrimaryColor.B}m{dots}\x1b[0m");
        if (!string.IsNullOrEmpty(_currentResponseSummary))
        {
            Console.Write(_currentResponseSummary);
        }

        // 恢复光标位置
        if (currentTop < Console.BufferHeight && currentLeft < Console.WindowWidth)
        {
            try
            {
                Console.SetCursorPosition(currentLeft, currentTop);
            }
            catch
            {
                // 忽略光标位置错误
            }
        }
    }

    public string? HandleKeyPress(ConsoleKeyInfo keyInfo)
    {
        lock (_consoleLock)
        {
            if (keyInfo.Key == ConsoleKey.Enter)
            {
                if (_inputBuffer.Length > 0)
                {
                    string input = _inputBuffer.ToString();
                    _inputBuffer.Clear();

                    PrintUserMessage(input);
                    RenderBottomArea();

                    return input;
                }
            }
            else if (keyInfo.Key == ConsoleKey.Backspace)
            {
                if (_inputBuffer.Length > 0)
                {
                    _inputBuffer.Remove(_inputBuffer.Length - 1, 1);
                    RenderBottomArea(overwrite: true);
                }
            }
            else if (!char.IsControl(keyInfo.KeyChar))
            {
                _inputBuffer.Append(keyInfo.KeyChar);
                RenderBottomArea(overwrite: true);
            }
        }
        return null;
    }

    public void SafeRender(Action action)
    {
        lock (_consoleLock)
        {
            ClearBottomArea();
            action();
            RenderBottomArea();
        }
    }

    private void PrintUserMessage(string message)
    {
        ClearBottomArea();
        
        // 简单渲染用户消息: -> message
        AnsiConsole.MarkupLine($"[green]->[/] [bold white]{Markup.Escape(message)}[/]");
    }

    private void RenderBottomArea(bool overwrite = false)
    {
        // 布局定义:
        // 行偏移 0: [Status] (仅当 Processing 时存在)
        // 行偏移 1: Top Line (───)
        // 行偏移 2: Input (>> ...) <- 光标驻留在此
        // 行偏移 3: Bottom Line (───)

        int statusLines = _isProcessing ? 1 : 0;
        int inputLineOffset = statusLines + 1; 
        int totalLines = statusLines + 3; 

        int safeWidth = Math.Max(0, Console.WindowWidth - 1); 
        string lineStr = new string('─', safeWidth);
        string clearLine = new string(' ', safeWidth); 

        int startTop;

        if (!overwrite)
        {
            // --- 关键修正：空间预留 ---
            // 在首次绘制前，检查剩余空间是否足够。如果不够，主动滚屏。
            // 这样可以防止在绘制过程中触发隐式滚动，导致坐标错乱。
            
            int currentTop = Console.CursorTop;
            int windowHeight = Console.WindowHeight;
            int bufferHeight = Console.BufferHeight;
            
            // 计算可视区域剩余行数
            // 注意：在某些终端中 WindowTop 可能为 0，我们主要关注 Buffer 底部
            int remainingLines = windowHeight - (currentTop % windowHeight) - 1; 
            
            // 如果是在 Buffer 的最后几行，也需要判断
            // 简单策略：如果当前行 + 需要的行数 >= BufferHeight，或者接近 Window 底部
            // 我们直接打印换行符来“推”屏幕
            
            // 更稳健的做法：
            // 预演一下：如果我们在 currentTop 开始画，画 totalLines 行，会不会超过 BufferHeight?
            
            // 修正：避免无限循环。如果是 BufferHeight 不足，WriteLine 会自动滚动 Buffer。
            // 关键是我们需要确保 startTop + totalLines - 1 < Console.BufferHeight
            // 如果 currentTop 已经在最后一行，我们需要滚动 totalLines 次才能腾出空间
            
            int linesNeeded = totalLines;
            // 检查从当前位置往下写 linesNeeded 行是否会越界
            // 实际上，只要当前行 + totalLines > BufferHeight，就会触发滚动
            
            int availableLinesBelow = Console.BufferHeight - currentTop;
            if (availableLinesBelow <= linesNeeded)
            {
                // 需要滚动的行数
                int scrollAmount = linesNeeded - availableLinesBelow + 1;
                // 限制最大滚动数，防止异常
                scrollAmount = Math.Min(scrollAmount, 10); 
                
                for(int i=0; i<scrollAmount; i++)
                {
                    Console.WriteLine();
                }
            }
            
            // 再次获取调整后的 Top
            startTop = Console.CursorTop;
            // 如果还是太靠下（因为 WriteLine 也会把 CursorTop 推到最后），
            // 说明我们实际上是在 Buffer 底部操作，startTop 应该是 BufferHeight - totalLines
            // 但最安全的做法是直接用 CursorTop。
            
            // 修正：如果 CursorTop 位于 BufferHeight - 1，我们无法向下写 3 行。
            // 这种情况下，我们应该把 startTop 往上移。
            if (startTop + totalLines > Console.BufferHeight)
            {
                startTop = Console.BufferHeight - totalLines;
            }
        }
        else
        {
            // 重绘模式：回溯到起始位置
            // 此时我们假设之前的空间预留是成功的，直接计算偏移
            int currentTop = Console.CursorTop;
            startTop = currentTop - inputLineOffset;
            
            // 保护性检查：如果用户疯狂调整窗口导致 startTop 变为负数
            if (startTop < 0) startTop = 0;
        }

        // --- 开始绘制 ---

        // [Status Line]
        if (_isProcessing)
        {
            Console.SetCursorPosition(0, startTop);
            Console.Write(clearLine); 
            Console.SetCursorPosition(0, startTop);
            
            var elapsed = DateTime.Now - _processStartTime;
            string timeStr = $"({elapsed.TotalSeconds:F1}s)";
            string spinner = _spinnerFrames[_spinnerFrame];
            AnsiConsole.Markup($"[blue]{spinner}[/] {_statusTitle} [grey]{timeStr}[/]");
            Console.WriteLine(); 
        }
        else
        {
            // 如果没有 Status，但为了逻辑统一，我们确保光标位置正确
            // 如果 statusLines=0, startTop 就是 TopLine 的位置
            Console.SetCursorPosition(0, startTop);
        }

        // [Top Line]
        Console.Write(clearLine); 
        Console.SetCursorPosition(0, Console.CursorTop);
        Console.Write("\x1b[90m" + lineStr + "\x1b[0m");
        Console.WriteLine();

        // [Input Line]
        int inputRowTop = Console.CursorTop; 
        Console.Write(clearLine); 
        Console.SetCursorPosition(0, inputRowTop);
        
        AnsiConsole.Markup($"[grey]>>[/] ");
        Console.Write(_inputBuffer.ToString());
        Console.WriteLine();

        // [Bottom Line]
        Console.Write(clearLine);
        Console.SetCursorPosition(0, Console.CursorTop);
        Console.Write("\x1b[90m" + lineStr + "\x1b[0m");
        // 最后一行不 WriteLine
        
        // --- 恢复光标 ---
        int cursorLeft = 3 + _inputBuffer.Length;
        if (cursorLeft >= safeWidth) cursorLeft = safeWidth - 1;

        // 再次检查 inputRowTop 是否有效 (虽然我们预留了空间，但以防万一)
        if (inputRowTop < Console.BufferHeight)
        {
            Console.SetCursorPosition(cursorLeft, inputRowTop);
        }
    }

    private void ClearBottomArea()
    {
        // 用于在输出新消息前，彻底清除底部的输入区
        // 逻辑：根据当前状态计算高度，向上清除
        
        int statusLines = _isProcessing ? 1 : 0;
        int inputLineOffset = statusLines + 1;
        int totalLines = statusLines + 3;

        int currentTop = Console.CursorTop;
        // 假设光标目前在 Input 行 (因为 RenderBottomArea 总是把光标放回那里)
        int startTop = currentTop - inputLineOffset;

        if (startTop < 0) startTop = 0;

        // 逐行清除
        for (int i = 0; i < totalLines; i++)
        {
            Console.SetCursorPosition(0, startTop + i);
            Console.Write(new string(' ', Console.WindowWidth));
        }

        // 将光标重置回起始位置，以便后续的正常输出（PrintUserMessage 等）从这里开始写
        Console.SetCursorPosition(0, startTop);
    }

    private void ClearCurrentLine()
    {
        Console.Write("\r" + new string(' ', Console.WindowWidth - 1) + "\r");
    }

    public void ShowResponseHeader(string summary)
    {
        SafeRender(() =>
        {
            // 既然动画逻辑已移除，这里只需简单显示总结头部即可
            AnsiConsole.MarkupLine($"[{PrimaryColor.ToMarkup()}]⋮[/] {Markup.Escape(summary)}");
            AnsiConsole.WriteLine();
        });
    }

    public void ShowResponse(string content)
    {
        // 停止动画
        _showingResponseHeader = false;

        // 恢复光标
        Console.CursorVisible = true;

        SafeRender(() =>
        {
            Spectre.Console.Rendering.IRenderable contentRenderable;
            try
            {
                var parsed = MarkdownRenderer.Parse(content);
                if (parsed.Elements.Count > 0)
                {
                    contentRenderable = new Rows(parsed.Elements);
                }
                else
                {
                    contentRenderable = new Text(string.Empty);
                }
            }
            catch
            {
                // 如果解析失败，则作为纯文本显示
                contentRenderable = new Text(content);
            }

            // 直接渲染内容，移除 Panel 边框
            AnsiConsole.Write(contentRenderable);
            AnsiConsole.WriteLine();
        });
    }

    public void ShowError(string message)
    {
        SafeRender(() => AnsiConsole.MarkupLine($"[bold red]Error:[/] {message}"));
    }

    public void ShowToolLog(string toolName, string command, string output)
    {
        SafeRender(() => 
        {
            var panelContent = new Text(output);
            var panel = new Panel(panelContent)
            {
                Border = BoxBorder.Heavy, // Claude style border
                BorderStyle = new Style(Color.Yellow),
                Padding = new Padding(1, 1, 1, 1),
                Header = new PanelHeader($" 🔨 [bold yellow]{toolName}[/]([grey]{Markup.Escape(command)}[/]) ", Justify.Left)
            };
            AnsiConsole.Write(panel);
        });
    }

    public void ShowLog(string message)
    {
        SafeRender(() => AnsiConsole.MarkupLine($"[grey]  {Markup.Escape(message)}[/]"));
    }
}
