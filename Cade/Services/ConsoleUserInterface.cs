using System;
using System.Text;
using Cade.Interfaces;
using Microsoft.Extensions.Logging;
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
    private int _bottomAreaStartLine = -1; // 记录底部区域的起始行号，-1 表示未渲染
    private int _lastWindowWidth = 0; // 记录上次的窗口宽度，用于检测窗口大小变化
    private string _currentPath = ""; // 当前路径
    private string _currentModelId = ""; // 当前模型ID
    
    private readonly ILogger<ConsoleUserInterface> _logger;
    private static int _messageCount = 0;
    
    public ConsoleUserInterface(ILogger<ConsoleUserInterface> logger)
    {
        _logger = logger;
    }

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
        lock (_consoleLock)
        {
            bool changed = _currentPath != path || _currentModelId != modelId;
            _currentPath = path;
            _currentModelId = modelId;
            
            // 如果状态改变且底部区域已渲染，则重绘
            if (changed && _bottomAreaStartLine >= 0)
            {
                RenderBottomArea(overwrite: true);
            }
        }
    }

    public void SetProcessing(bool isProcessing, string? title = null)
    {
        lock (_consoleLock)
        {
            bool wasProcessing = _isProcessing;
            
            if (isProcessing && !wasProcessing)
            {
                _processStartTime = DateTime.Now;
                _spinnerFrame = 0;
                Console.CursorVisible = false; // 隐藏光标
            }

            _isProcessing = isProcessing;
            if (title != null) _statusTitle = title;

            // 停止处理时恢复光标
            if (!isProcessing)
            {
                Console.CursorVisible = true; // 恢复光标
            }

            // 由于底部区域固定为4行，只需要重绘即可（不需要清除）
            if (_bottomAreaStartLine >= 0)
            {
                RenderBottomArea(overwrite: true);
            }
            else
            {
                RenderBottomArea();
            }
        }
    }

    public void Update()
    {
        lock (_consoleLock)
        {
            // 检测窗口大小变化，如果变化则清屏（避免横线换行导致的混乱）
            int currentWidth = Console.WindowWidth;
            if (_lastWindowWidth != currentWidth && _lastWindowWidth > 0)
            {
                _lastWindowWidth = currentWidth;
                // 窗口大小变化时，清屏并重新显示欢迎界面
                AnsiConsole.Clear();
                _bottomAreaStartLine = -1;
                ShowWelcome();
                return;
            }
            
            // 更新 AI 回复头部动画
            if (_showingResponseHeader)
            {
                // Gemini 风格：更快的脉动效果
                if ((DateTime.Now - _lastSpinnerTick).TotalMilliseconds > 150)
                {
                    _aiDotFrame = (_aiDotFrame + 1) % _aiResponseDots.Length;
                    _lastSpinnerTick = DateTime.Now;
                    UpdateResponseHeader();
                }
            }
            else if (_isProcessing && _bottomAreaStartLine >= 0)
            {
                // 更新底部处理状态（思考动画）
                // 只有在底部区域已渲染时才更新
                if ((DateTime.Now - _lastSpinnerTick).TotalMilliseconds > 100)
                {
                    _spinnerFrame = (_spinnerFrame + 1) % _spinnerFrames.Length;
                    _lastSpinnerTick = DateTime.Now;
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
            // 总是尝试清除底部区域
            ClearBottomArea();
            action();
            RenderBottomArea();
        }
    }

    private void PrintUserMessage(string message)
    {
        _messageCount++;
        int msgNum = _messageCount;
        
        _logger.LogInformation("PrintUserMessage #{MsgNum} START: message='{Message}', _bottomAreaStartLine={StartLine}, CursorTop={CursorTop}", 
            msgNum, message, _bottomAreaStartLine, Console.CursorTop);
        
        // 清除底部区域
        if (_bottomAreaStartLine >= 0)
        {
            const int totalLines = 5;
            int startTop = _bottomAreaStartLine;
            int safeWidth = Math.Max(0, Console.WindowWidth - 1);
            string clearLine = new string(' ', safeWidth);
            
            _logger.LogInformation("PrintUserMessage #{MsgNum}: Clearing {TotalLines} lines from {StartTop}", msgNum, totalLines, startTop);
            
            for (int i = 0; i < totalLines; i++)
            {
                int lineToC = startTop + i;
                if (lineToC >= 0 && lineToC < Console.BufferHeight)
                {
                    Console.SetCursorPosition(0, lineToC);
                    Console.Write(clearLine);
                }
            }
            
            // 光标回到起始位置
            Console.SetCursorPosition(0, startTop);
            _bottomAreaStartLine = -1;
            
            _logger.LogInformation("PrintUserMessage #{MsgNum}: After clear, CursorTop={CursorTop}", msgNum, Console.CursorTop);
            
            Console.Out.Flush();
        }
        else
        {
            _logger.LogInformation("PrintUserMessage #{MsgNum}: No bottom area to clear, CursorTop={CursorTop}", msgNum, Console.CursorTop);
        }

        // 确保有足够的空间：用户消息 1 行 + 底部区域 4 行 = 5 行
        int currentTop = Console.CursorTop;
        int bufferHeight = Console.BufferHeight;
        int neededLines = 6; // 1 行用户消息 + 5 行底部区域
        
        if (currentTop + neededLines > bufferHeight)
        {
            int linesToScroll = currentTop + neededLines - bufferHeight;
            _logger.LogInformation("PrintUserMessage #{MsgNum}: Need to scroll {Lines} lines for space", msgNum, linesToScroll);
            
            for (int i = 0; i < linesToScroll; i++)
            {
                Console.WriteLine();
            }
            // 滚屏后光标会在新位置，需要回到正确的位置
            // 滚屏后，原来的 currentTop 位置的内容向上移动了 linesToScroll 行
            // 新的写入位置应该是 bufferHeight - neededLines
            Console.SetCursorPosition(0, bufferHeight - neededLines);
            _logger.LogInformation("PrintUserMessage #{MsgNum}: After scroll, CursorTop={CursorTop}", msgNum, Console.CursorTop);
        }

        // 打印用户消息
        _logger.LogInformation("PrintUserMessage #{MsgNum}: Writing message at CursorTop={CursorTop}", msgNum, Console.CursorTop);
        Console.WriteLine($"\x1b[32m->\x1b[0m \x1b[1;37m{message}\x1b[0m");
        Console.Out.Flush();
        
        _logger.LogInformation("PrintUserMessage #{MsgNum} END: CursorTop={CursorTop}", msgNum, Console.CursorTop);
    }

    private void RenderBottomArea(bool overwrite = false)
    {
        _logger.LogInformation("RenderBottomArea START: overwrite={Overwrite}, _bottomAreaStartLine={StartLine}, CursorTop={CursorTop}", 
            overwrite, _bottomAreaStartLine, Console.CursorTop);
        
        // 布局定义 (固定5行):
        // 行偏移 0: [Status] (Processing 时显示动画，否则为空行)
        // 行偏移 1: Top Line (───)
        // 行偏移 2: Input (>> ...) <- 光标驻留在此
        // 行偏移 3: Bottom Line (───)
        // 行偏移 4: Status Bar (路径 | 模型)

        const int totalLines = 5;
        const int inputLineOffset = 2; // 输入行在第3行（索引2）

        int safeWidth = Math.Max(0, Console.WindowWidth - 1);
        string lineStr = new string('─', safeWidth);
        string clearLine = new string(' ', safeWidth);

        int startTop;

        if (!overwrite)
        {
            // 直接使用当前光标位置作为底部区域的起始位置
            // 不再滚屏，避免覆盖用户消息
            startTop = Console.CursorTop;
            
            // 如果空间不够，向上调整 startTop，但不能小于 0
            int bufferHeight = Console.BufferHeight;
            if (startTop + totalLines > bufferHeight)
            {
                startTop = Math.Max(0, bufferHeight - totalLines);
            }
            
            _logger.LogInformation("RenderBottomArea: startTop={StartTop}, bufferHeight={BufferHeight}", startTop, bufferHeight);
        }
        else
        {
            // 重绘模式：使用记录的起始行号
            if (_bottomAreaStartLine >= 0)
            {
                startTop = _bottomAreaStartLine;
            }
            else
            {
                // 如果没有记录，回溯到起始位置
                int currentTop = Console.CursorTop;
                startTop = currentTop - inputLineOffset;
            }

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

            // [Status Line] - 第1行，总是存在
            Console.SetCursorPosition(0, startTop);
            Console.Write(clearLine);
            Console.SetCursorPosition(0, startTop);

            if (_isProcessing)
            {
                var elapsed = DateTime.Now - _processStartTime;
                string timeStr = $"({elapsed.TotalSeconds:F1}s)";
                string spinner = _spinnerFrames[_spinnerFrame];
                AnsiConsole.Markup($"[blue]{spinner}[/] {_statusTitle} [grey]{timeStr}[/]");
            }
            // 如果不是 Processing，状态行为空
            Console.WriteLine();

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
            Console.WriteLine();

            // [Status Bar] - 左边路径，右边模型
            Console.Write(clearLine);
            Console.SetCursorPosition(0, Console.CursorTop);
            
            // 构建状态栏内容
            string pathDisplay = string.IsNullOrEmpty(_currentPath) ? "" : _currentPath;
            
            // 模型ID格式: uuid_modelname，只显示下划线后面的部分
            string modelDisplay = "";
            if (!string.IsNullOrEmpty(_currentModelId))
            {
                int underscoreIndex = _currentModelId.IndexOf('_');
                modelDisplay = underscoreIndex >= 0 ? _currentModelId.Substring(underscoreIndex + 1) : _currentModelId;
            }
            
            // 截断路径如果太长
            int maxPathLen = safeWidth - modelDisplay.Length - 3; // 留出空间给模型和分隔符
            if (maxPathLen > 0 && pathDisplay.Length > maxPathLen)
            {
                pathDisplay = "..." + pathDisplay.Substring(pathDisplay.Length - maxPathLen + 3);
            }
            
            // 计算右对齐的模型位置
            int modelStartPos = safeWidth - modelDisplay.Length;
            if (modelStartPos < pathDisplay.Length + 1) modelStartPos = pathDisplay.Length + 1;
            
            // 输出路径（灰色）
            AnsiConsole.Markup($"[grey]{Markup.Escape(pathDisplay)}[/]");
            
            // 输出模型（右对齐，青色）
            if (!string.IsNullOrEmpty(modelDisplay) && modelStartPos < safeWidth)
            {
                Console.SetCursorPosition(modelStartPos, Console.CursorTop);
                AnsiConsole.Markup($"[cyan]{Markup.Escape(modelDisplay)}[/]");
            }
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
            
            // 记录底部区域的起始行号和当前窗口宽度
            _bottomAreaStartLine = startTop;
            _lastWindowWidth = Console.WindowWidth;
            
            _logger.LogInformation("RenderBottomArea END: startTop={StartTop}, _bottomAreaStartLine={BottomLine}", startTop, _bottomAreaStartLine);
        }
        catch (Exception ex)
        {
            // 如果渲染过程中发生任何异常，确保光标可见
            Console.CursorVisible = true;
            _logger.LogError(ex, "RenderBottomArea ERROR");
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
        // 如果底部区域未渲染，无需清除
        if (_bottomAreaStartLine < 0) return;
        
        // 固定5行
        const int totalLines = 5;
        
        int startTop = _bottomAreaStartLine;
        
        // 清除底部区域
        int safeWidth = Math.Max(0, Console.WindowWidth - 1);
        string clearLine = new string(' ', safeWidth);
        
        for (int i = 0; i < totalLines; i++)
        {
            int lineToC = startTop + i;
            if (lineToC >= 0 && lineToC < Console.BufferHeight)
            {
                try
                {
                    Console.SetCursorPosition(0, lineToC);
                    Console.Write(clearLine);
                }
                catch
                {
                    // 忽略
                }
            }
        }

        // 光标回到起始位置
        Console.SetCursorPosition(0, startTop);
        _bottomAreaStartLine = -1;
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