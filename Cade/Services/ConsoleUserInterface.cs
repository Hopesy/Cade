using System;
using System.Text;
using Cade.Interfaces;
using Spectre.Console;
using Spectre.Console.Json;

namespace Cade.Services;

public class ConsoleUserInterface : IUserInterface
{
    private readonly StringBuilder _inputBuffer = new StringBuilder();
    private int _cursorPosition = 0; // 光标在输入缓冲区中的位置（字符索引）
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
                    _cursorPosition = 0; // 重置光标位置

                    PrintUserMessage(input);
                    RenderBottomArea();

                    return input;
                }
            }
            else if (keyInfo.Key == ConsoleKey.Backspace)
            {
                // 在光标位置前删除一个字符
                if (_cursorPosition > 0 && _inputBuffer.Length > 0)
                {
                    _inputBuffer.Remove(_cursorPosition - 1, 1);
                    _cursorPosition--;
                    RenderBottomArea(overwrite: true);
                }
            }
            else if (keyInfo.Key == ConsoleKey.Delete)
            {
                // 删除光标位置的字符
                if (_cursorPosition < _inputBuffer.Length)
                {
                    _inputBuffer.Remove(_cursorPosition, 1);
                    RenderBottomArea(overwrite: true);
                }
            }
            else if (keyInfo.Key == ConsoleKey.LeftArrow)
            {
                // 向左移动光标
                if (_cursorPosition > 0)
                {
                    _cursorPosition--;
                    RenderBottomArea(overwrite: true);
                }
            }
            else if (keyInfo.Key == ConsoleKey.RightArrow)
            {
                // 向右移动光标
                if (_cursorPosition < _inputBuffer.Length)
                {
                    _cursorPosition++;
                    RenderBottomArea(overwrite: true);
                }
            }
            else if (keyInfo.Key == ConsoleKey.Home)
            {
                // 移动到行首
                if (_cursorPosition != 0)
                {
                    _cursorPosition = 0;
                    RenderBottomArea(overwrite: true);
                }
            }
            else if (keyInfo.Key == ConsoleKey.End)
            {
                // 移动到行尾
                if (_cursorPosition != _inputBuffer.Length)
                {
                    _cursorPosition = _inputBuffer.Length;
                    RenderBottomArea(overwrite: true);
                }
            }
            else if (!char.IsControl(keyInfo.KeyChar))
            {
                // 在光标位置插入字符
                _inputBuffer.Insert(_cursorPosition, keyInfo.KeyChar);
                _cursorPosition++;
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
        AnsiConsole.WriteLine(); // 添加空行，确保光标在新行
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
            // --- 关键修正：空间预留 (Space Reservation) ---
            // 确保有足够的缓冲区行数来绘制 totalLines。
            // 如果空间不足，主动 WriteLine 滚屏。

            int currentTop = Console.CursorTop;
            int bufferHeight = Console.BufferHeight;

            // 预测需要的底部位置
            int neededBottom = currentTop + totalLines;

            // 如果需要的底部超出了缓冲区高度
            if (neededBottom > bufferHeight)
            {
                // 需要滚动的行数
                int linesToScroll = neededBottom - bufferHeight;

                // 限制，防止无限循环
                linesToScroll = Math.Min(linesToScroll, 20);

                for (int i = 0; i < linesToScroll; i++)
                {
                    Console.WriteLine();
                }
            }

            // 滚屏后，CursorTop 会更新。重新获取起始位置。
            startTop = Console.CursorTop;

            // 再次检查边界：如果因为滚屏导致 CursorTop 顶到了 BufferHeight (极罕见情况)
            // 强制回退 startTop
            if (startTop + totalLines > bufferHeight)
            {
                startTop = Math.Max(0, bufferHeight - totalLines);
            }
        }
        else
        {
            // 重绘模式：回溯到起始位置
            int currentTop = Console.CursorTop;
            startTop = currentTop - inputLineOffset;

            // 保护性检查
            if (startTop < 0) startTop = 0;
            if (startTop + totalLines > Console.BufferHeight)
            {
                startTop = Math.Max(0, Console.BufferHeight - totalLines);
            }
        }

        // --- 开始绘制 ---
        try
        {
            // 隐藏光标以避免渲染过程中的闪烁
            bool wasCursorVisible = Console.CursorVisible;
            Console.CursorVisible = false;

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
            // 计算光标位置：使用到光标位置为止的文本宽度
            string textBeforeCursor = _inputBuffer.ToString().Substring(0, Math.Min(_cursorPosition, _inputBuffer.Length));
            int cursorLeft = 3 + GetDisplayWidth(textBeforeCursor);
            if (cursorLeft >= safeWidth) cursorLeft = safeWidth - 1;

            // 再次检查 inputRowTop 是否有效 (虽然我们预留了空间，但以防万一)
            if (inputRowTop >= 0 && inputRowTop < Console.BufferHeight)
            {
                Console.SetCursorPosition(cursorLeft, inputRowTop);
            }

            // 渲染完成后恢复光标显示
            Console.CursorVisible = wasCursorVisible || !_isProcessing;
        }
        catch (Exception)
        {
            // 如果渲染过程中发生任何异常，确保光标可见
            Console.CursorVisible = true;
        }
    }

    private int GetDisplayWidth(string s)
    {
        int width = 0;
        foreach (char c in s)
        {
            // 更精确的东亚字符宽度判断
            // 参考 Unicode East Asian Width 规范
            if (IsFullWidth(c))
                width += 2;
            else
                width += 1;
        }
        return width;
    }

    private bool IsFullWidth(char c)
    {
        // CJK统一汉字
        if (c >= 0x4E00 && c <= 0x9FFF) return true;
        // CJK扩展A
        if (c >= 0x3400 && c <= 0x4DBF) return true;
        // 全角ASCII和全角标点
        if (c >= 0xFF01 && c <= 0xFF60) return true;
        // 全角字符
        if (c >= 0xFFE0 && c <= 0xFFE6) return true;
        // CJK符号和标点
        if (c >= 0x3000 && c <= 0x303F) return true;
        // 平假名和片假名
        if (c >= 0x3040 && c <= 0x30FF) return true;
        // 谚文音节（韩文）
        if (c >= 0xAC00 && c <= 0xD7AF) return true;
        // CJK兼容字符
        if (c >= 0xF900 && c <= 0xFAFF) return true;

        return false;
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

        // 安全检查：确保不会越界
        if (startTop + totalLines > Console.BufferHeight)
        {
            startTop = Math.Max(0, Console.BufferHeight - totalLines);
        }

        // 逐行清除
        for (int i = 0; i < totalLines; i++)
        {
            int lineToC = startTop + i;
            if (lineToC >= 0 && lineToC < Console.BufferHeight)
            {
                try
                {
                    Console.SetCursorPosition(0, lineToC);
                    Console.Write(new string(' ', Math.Min(Console.WindowWidth, Console.BufferWidth)));
                }
                catch
                {
                    // 忽略位置设置错误，继续处理
                }
            }
        }

        // 将光标重置回起始位置，以便后续的正常输出（PrintUserMessage 等）从这里开始写
        try
        {
            if (startTop >= 0 && startTop < Console.BufferHeight)
            {
                Console.SetCursorPosition(0, startTop);
            }
        }
        catch
        {
            // 如果设置失败，不做处理
        }
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