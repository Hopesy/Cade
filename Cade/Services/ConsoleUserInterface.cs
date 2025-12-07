using System;
using System.Text;
using Cade.Models;
using Cade.Services.Interfaces;
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
    
    // 历史消息存储，用于窗口大小变化时重绘
    private readonly List<HistoryItem> _history = new();
    
    private record HistoryItem(HistoryType Type, string Content, string? Header = null);
    private enum HistoryType { UserMessage, Response, ToolCall, Error }

    // 命令补全状态
    private CommandDefinition[] _matchedCommands = [];
    private int _selectedCommandIndex = -1;
    
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

        // 提示信息
        AnsiConsole.MarkupLine("[bold]Tips:[/]");
        AnsiConsole.MarkupLine("  [grey]•[/] Ask questions, edit files, or run commands");
        AnsiConsole.MarkupLine("  [grey]•[/] [cyan]Tab[/] toggle think mode, [cyan]Esc[/] cancel task");
        AnsiConsole.MarkupLine("  [grey]•[/] [cyan]/help[/] for commands, [cyan]/model[/] switch model");

        AnsiConsole.WriteLine();

        RenderBottomArea();
    }

    private bool _showThink = false;

    public void SetStatus(string path, string modelId, bool showThink = false)
    {
        lock (_consoleLock)
        {
            bool changed = _currentPath != path || _currentModelId != modelId || _showThink != showThink;
            _currentPath = path;
            _currentModelId = modelId;
            _showThink = showThink;
            
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
            // 检测窗口大小变化，如果变化则清屏并重绘历史消息
            int currentWidth = Console.WindowWidth;
            if (_lastWindowWidth != currentWidth && _lastWindowWidth > 0)
            {
                _lastWindowWidth = currentWidth;
                _bottomAreaStartLine = -1;
                RedrawAll();
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
            var currentInput = _inputBuffer.ToString();
            var isCommandMode = currentInput.StartsWith("/");

            if (keyInfo.Key == ConsoleKey.Enter)
            {
                if (_inputBuffer.Length > 0)
                {
                    string input = _inputBuffer.ToString();
                    
                    // 如果在命令补全模式且有选中的命令，使用选中的命令
                    if (isCommandMode && _selectedCommandIndex >= 0 && _selectedCommandIndex < _matchedCommands.Length)
                    {
                        input = _matchedCommands[_selectedCommandIndex].Name;
                    }
                    
                    _inputBuffer.Clear();
                    _cursorPosition = 0;
                    _matchedCommands = [];
                    _selectedCommandIndex = -1;

                    PrintUserMessage(input);
                    RenderBottomArea();

                    return input;
                }
            }
            else if (keyInfo.Key == ConsoleKey.Tab)
            {
                // Tab 补全：如果有匹配的命令，选择第一个或当前选中的
                if (isCommandMode && _matchedCommands.Length > 0)
                {
                    var idx = _selectedCommandIndex >= 0 ? _selectedCommandIndex : 0;
                    var cmd = _matchedCommands[idx].Name;
                    _inputBuffer.Clear();
                    _inputBuffer.Append(cmd);
                    _cursorPosition = cmd.Length;
                    UpdateCommandCompletion();
                    RenderBottomArea(overwrite: true);
                }
            }
            else if (keyInfo.Key == ConsoleKey.UpArrow)
            {
                // 上键：在命令列表中向上选择
                if (isCommandMode && _matchedCommands.Length > 0)
                {
                    _selectedCommandIndex = _selectedCommandIndex <= 0 
                        ? _matchedCommands.Length - 1 
                        : _selectedCommandIndex - 1;
                    RenderBottomArea(overwrite: true);
                }
            }
            else if (keyInfo.Key == ConsoleKey.DownArrow)
            {
                // 下键：在命令列表中向下选择
                if (isCommandMode && _matchedCommands.Length > 0)
                {
                    _selectedCommandIndex = (_selectedCommandIndex + 1) % _matchedCommands.Length;
                    RenderBottomArea(overwrite: true);
                }
            }
            else if (keyInfo.Key == ConsoleKey.Backspace)
            {
                if (_cursorPosition > 0 && _inputBuffer.Length > 0)
                {
                    _inputBuffer.Remove(_cursorPosition - 1, 1);
                    _cursorPosition--;
                    UpdateCommandCompletion();
                    RenderBottomArea(overwrite: true);
                }
            }
            else if (keyInfo.Key == ConsoleKey.Delete)
            {
                if (_cursorPosition < _inputBuffer.Length)
                {
                    _inputBuffer.Remove(_cursorPosition, 1);
                    UpdateCommandCompletion();
                    RenderBottomArea(overwrite: true);
                }
            }
            else if (keyInfo.Key == ConsoleKey.LeftArrow)
            {
                if (_cursorPosition > 0)
                {
                    _cursorPosition--;
                    RenderBottomArea(overwrite: true);
                }
            }
            else if (keyInfo.Key == ConsoleKey.RightArrow)
            {
                if (_cursorPosition < _inputBuffer.Length)
                {
                    _cursorPosition++;
                    RenderBottomArea(overwrite: true);
                }
            }
            else if (keyInfo.Key == ConsoleKey.Home)
            {
                if (_cursorPosition != 0)
                {
                    _cursorPosition = 0;
                    RenderBottomArea(overwrite: true);
                }
            }
            else if (keyInfo.Key == ConsoleKey.End)
            {
                if (_cursorPosition != _inputBuffer.Length)
                {
                    _cursorPosition = _inputBuffer.Length;
                    RenderBottomArea(overwrite: true);
                }
            }
            else if (!char.IsControl(keyInfo.KeyChar))
            {
                _inputBuffer.Insert(_cursorPosition, keyInfo.KeyChar);
                _cursorPosition++;
                UpdateCommandCompletion();
                RenderBottomArea(overwrite: true);
            }
        }
        return null;
    }

    private void UpdateCommandCompletion()
    {
        var input = _inputBuffer.ToString();
        if (input.StartsWith("/"))
        {
            _matchedCommands = CommandDefinition.Match(input);
            _selectedCommandIndex = _matchedCommands.Length > 0 ? 0 : -1;
        }
        else
        {
            _matchedCommands = [];
            _selectedCommandIndex = -1;
        }
    }

    public void SafeRender(Action action)
    {
        lock (_consoleLock)
        {
            _logger.LogInformation("SafeRender START: _bottomAreaStartLine={StartLine}, CursorTop={CursorTop}", _bottomAreaStartLine, Console.CursorTop);
            
            // 记录底部区域起始位置，用于后续恢复
            int savedBottomStart = _bottomAreaStartLine;
            
            // 清除底部区域
            ClearBottomArea();
            
            _logger.LogInformation("SafeRender after ClearBottomArea: CursorTop={CursorTop}, savedBottomStart={SavedStart}", Console.CursorTop, savedBottomStart);
            
            // 执行输出操作
            action();
            
            // 确保输出后有换行，避免底部区域覆盖最后一行
            int afterActionTop = Console.CursorTop;
            int afterActionLeft = Console.CursorLeft;
            
            // 如果光标不在行首，说明最后一行没有换行，需要换行
            if (afterActionLeft > 0)
            {
                Console.WriteLine();
                afterActionTop = Console.CursorTop;
            }
            
            _logger.LogInformation("SafeRender after action: CursorTop={CursorTop}, CursorLeft={CursorLeft}", afterActionTop, afterActionLeft);
            
            // 渲染底部区域
            RenderBottomArea();
            
            _logger.LogInformation("SafeRender END: _bottomAreaStartLine={StartLine}, CursorTop={CursorTop}", _bottomAreaStartLine, Console.CursorTop);
        }
    }

    private void RedrawAll()
    {
        AnsiConsole.Clear();
        
        // 重绘欢迎界面（简化版，不显示完整 logo）
        AnsiConsole.MarkupLine($"[{PrimaryColor.ToMarkup()}]Cade Code[/] - AI 编程助手\n");
        
        // 重绘历史消息
        foreach (var item in _history)
        {
            switch (item.Type)
            {
                case HistoryType.UserMessage:
                    Console.WriteLine($"\x1b[32m->\x1b[0m \x1b[1;37m{item.Content}\x1b[0m");
                    break;
                case HistoryType.Response:
                    if (!string.IsNullOrEmpty(item.Header))
                        AnsiConsole.MarkupLine($"[{PrimaryColor.ToMarkup()}]⋮[/] {Markup.Escape(item.Header)}");
                    if (!string.IsNullOrWhiteSpace(item.Content))
                    {
                        try
                        {
                            var parsed = MarkdownRenderer.Parse(item.Content);
                            if (parsed.Elements.Count > 0)
                                AnsiConsole.Write(new Rows(parsed.Elements));
                        }
                        catch
                        {
                            AnsiConsole.WriteLine(item.Content);
                        }
                    }
                    break;
                case HistoryType.ToolCall:
                    AnsiConsole.MarkupLine(item.Content); // 已格式化的工具调用
                    break;
                case HistoryType.Error:
                    AnsiConsole.MarkupLine($"[bold red]Error:[/] {Markup.Escape(item.Content)}");
                    break;
            }
        }
        
        RenderBottomArea();
    }

    private void PrintUserMessage(string message)
    {
        // 保存到历史
        _history.Add(new HistoryItem(HistoryType.UserMessage, message));
        
        _messageCount++;
        int msgNum = _messageCount;
        
        _logger.LogInformation("PrintUserMessage #{MsgNum} START: message='{Message}', _bottomAreaStartLine={StartLine}, CursorTop={CursorTop}", 
            msgNum, message, _bottomAreaStartLine, Console.CursorTop);
        
        // 清除底部区域并获取正确的写入位置
        int writePosition = Console.CursorTop;
        
        if (_bottomAreaStartLine >= 0)
        {
            const int maxTotalLines = 10; // 清除足够多的行
            int startTop = _bottomAreaStartLine;
            int safeWidth = Math.Max(0, Console.WindowWidth - 1);
            string clearLine = new string(' ', safeWidth);
            
            _logger.LogInformation("PrintUserMessage #{MsgNum}: Clearing {TotalLines} lines from {StartTop}", msgNum, maxTotalLines, startTop);
            
            for (int i = 0; i < maxTotalLines; i++)
            {
                int lineToC = startTop + i;
                if (lineToC >= 0 && lineToC < Console.BufferHeight)
                {
                    Console.SetCursorPosition(0, lineToC);
                    Console.Write(clearLine);
                }
            }
            
            // 写入位置应该是底部区域的起始位置（这是内容区域的结束位置）
            writePosition = startTop;
            _bottomAreaStartLine = -1;
            
            _logger.LogInformation("PrintUserMessage #{MsgNum}: After clear, writePosition={WritePos}", msgNum, writePosition);
            
            Console.Out.Flush();
        }
        else
        {
            _logger.LogInformation("PrintUserMessage #{MsgNum}: No bottom area to clear, writePosition={WritePos}", msgNum, writePosition);
        }

        // 设置光标到写入位置
        Console.SetCursorPosition(0, writePosition);

        // 确保有足够的空间
        int bufferHeight = Console.BufferHeight;
        int neededLines = 10; // 预留足够空间
        
        if (writePosition + neededLines > bufferHeight)
        {
            int linesToScroll = writePosition + neededLines - bufferHeight;
            _logger.LogInformation("PrintUserMessage #{MsgNum}: Need to scroll {Lines} lines for space", msgNum, linesToScroll);
            
            // 移动到缓冲区底部进行滚动
            Console.SetCursorPosition(0, bufferHeight - 1);
            for (int i = 0; i < linesToScroll; i++)
            {
                Console.WriteLine();
            }
            // 滚动后，写入位置需要调整
            writePosition = bufferHeight - neededLines;
            Console.SetCursorPosition(0, writePosition);
            _logger.LogInformation("PrintUserMessage #{MsgNum}: After scroll, writePosition={WritePos}", msgNum, writePosition);
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
        
        // 布局定义 (动态行数):
        // 行偏移 0: [Status] (Processing 时显示动画，否则为空行)
        // 行偏移 1: Top Line (───)
        // 行偏移 2~N: Input (>> ...) <- 可能多行
        // 行偏移 N+1~M: Command completion list (如果有)
        // 行偏移 M+1: Bottom Line (───)
        // 行偏移 M+2: Status Bar (路径 | 模型)

        int safeWidth = Math.Max(1, Console.WindowWidth - 1);
        string lineStr = new string('─', safeWidth);
        string clearLine = new string(' ', safeWidth);

        // 计算输入文本需要的行数
        string inputText = _inputBuffer.ToString();
        int inputDisplayWidth = 3 + GetDisplayWidth(inputText); // ">> " + 文本
        int inputLines = Math.Max(1, (inputDisplayWidth + safeWidth - 1) / safeWidth); // 向上取整
        
        // 限制最大输入行数，避免占满整个屏幕
        const int maxInputLines = 5;
        inputLines = Math.Min(inputLines, maxInputLines);

        // 命令补全列表行数（如果有命令补全，则不显示状态栏）
        int completionLines = _matchedCommands.Length;
        int statusBarLines = completionLines > 0 ? 0 : 1;
        
        // 总行数 = 状态行(1) + 上横线(1) + 输入行(N) + 下横线(1) + (补全列表 或 状态栏)
        int totalLines = 3 + inputLines + Math.Max(completionLines, statusBarLines);
        int inputLineOffset = 2; // 输入行从第3行开始（索引2）

        int startTop;
        int bufferHeight = Console.BufferHeight;

        if (!overwrite)
        {
            startTop = Console.CursorTop;
            
            // 如果空间不够，需要滚动屏幕
            if (startTop + totalLines > bufferHeight)
            {
                int linesToScroll = startTop + totalLines - bufferHeight;
                // 通过输出空行来滚动屏幕
                Console.SetCursorPosition(0, bufferHeight - 1);
                for (int i = 0; i < linesToScroll; i++)
                {
                    Console.WriteLine();
                }
                // 滚动后，startTop 需要调整
                startTop = bufferHeight - totalLines;
            }
            
            _logger.LogInformation("RenderBottomArea: startTop={StartTop}, totalLines={TotalLines}, inputLines={InputLines}, bufferHeight={BufferHeight}", startTop, totalLines, inputLines, bufferHeight);
        }
        else
        {
            // 重绘模式：检查之前记录的位置是否仍然有效
            if (_bottomAreaStartLine >= 0)
            {
                startTop = _bottomAreaStartLine;
                
                // 如果当前光标位置超出了底部区域，说明有新内容输出，需要重新计算
                int currentTop = Console.CursorTop;
                if (currentTop > startTop + totalLines)
                {
                    // 新内容已经超出底部区域，需要从当前位置重新开始
                    startTop = currentTop - totalLines + 1;
                    if (startTop < 0) startTop = 0;
                }
            }
            else
            {
                int currentTop = Console.CursorTop;
                startTop = currentTop - inputLineOffset;
            }

            if (startTop < 0) startTop = 0;
            if (startTop + totalLines > bufferHeight)
            {
                startTop = Math.Max(0, bufferHeight - totalLines);
            }
        }

        // --- 开始绘制 ---
        try
        {
            bool wasCursorVisible = Console.CursorVisible;
            Console.CursorVisible = false;

            // 清除整个底部区域（可能比之前多或少）
            for (int i = 0; i < totalLines + 2; i++) // +2 以防之前行数更多
            {
                int lineY = startTop + i;
                if (lineY >= 0 && lineY < Console.BufferHeight)
                {
                    Console.SetCursorPosition(0, lineY);
                    Console.Write(clearLine);
                }
            }

            // [Status Line]
            Console.SetCursorPosition(0, startTop);
            if (_isProcessing)
            {
                var elapsed = DateTime.Now - _processStartTime;
                string timeStr = $"({elapsed.TotalSeconds:F1}s)";
                string spinner = _spinnerFrames[_spinnerFrame];
                AnsiConsole.Markup($"[blue]{spinner}[/] {_statusTitle} [grey]{timeStr}[/]");
            }
            Console.WriteLine();

            // [Top Line]
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write("\x1b[90m" + lineStr + "\x1b[0m");
            Console.WriteLine();

            // [Input Lines] - 支持多行
            int inputRowTop = Console.CursorTop;
            Console.SetCursorPosition(0, inputRowTop);
            AnsiConsole.Markup($"[grey]>>[/] ");
            Console.Write(inputText);
            
            // 移动到输入区域结束后的下一行
            int inputEndRow = inputRowTop + inputLines - 1;
            Console.SetCursorPosition(0, inputEndRow + 1);

            // [Bottom Line]
            Console.Write("\x1b[90m" + lineStr + "\x1b[0m");
            Console.WriteLine();

            // [Command Completion List] - 命令补全列表（在底部横线下方）
            if (_matchedCommands.Length > 0)
            {
                for (int i = 0; i < _matchedCommands.Length; i++)
                {
                    var cmd = _matchedCommands[i];
                    var isSelected = i == _selectedCommandIndex;
                    var prefix = isSelected ? "› " : "  ";
                    var cmdStyle = isSelected ? "[cyan]" : "[dim]";
                    var descStyle = "[dim]";
                    
                    Console.SetCursorPosition(0, Console.CursorTop);
                    AnsiConsole.Markup($"{prefix}{cmdStyle}{Markup.Escape(cmd.Name)}[/]  {descStyle}{Markup.Escape(cmd.Description)}[/]");
                    Console.WriteLine();
                }
            }
            else
            {
                // [Status Bar] - 只在没有命令补全时显示
                Console.SetCursorPosition(0, Console.CursorTop);
                
                string pathDisplay = string.IsNullOrEmpty(_currentPath) ? "" : _currentPath;
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(userProfile) && pathDisplay.StartsWith(userProfile, StringComparison.OrdinalIgnoreCase))
                {
                    pathDisplay = "~" + pathDisplay.Substring(userProfile.Length);
                }
                
                // 构建右侧显示：Think: On/Off + 模型名称
                string modelDisplay = "";
                if (!string.IsNullOrEmpty(_currentModelId))
                {
                    int underscoreIndex = _currentModelId.IndexOf('_');
                    modelDisplay = underscoreIndex >= 0 ? _currentModelId.Substring(underscoreIndex + 1) : _currentModelId;
                }
                
                string thinkDisplay = _showThink ? "Think: On | " : "";
                string rightDisplay = thinkDisplay + modelDisplay;
                
                int maxPathLen = safeWidth - rightDisplay.Length - 3;
                if (maxPathLen > 0 && pathDisplay.Length > maxPathLen)
                {
                    pathDisplay = "..." + pathDisplay.Substring(pathDisplay.Length - maxPathLen + 3);
                }
                
                int rightStartPos = safeWidth - rightDisplay.Length;
                if (rightStartPos < pathDisplay.Length + 1) rightStartPos = pathDisplay.Length + 1;
                
                AnsiConsole.Markup($"[grey]{Markup.Escape(pathDisplay)}[/]");
                
                if (!string.IsNullOrEmpty(rightDisplay) && rightStartPos < safeWidth)
                {
                    Console.SetCursorPosition(rightStartPos, Console.CursorTop);
                    if (_showThink)
                    {
                        AnsiConsole.Markup($"[green]Think: On[/] | [cyan]{Markup.Escape(modelDisplay)}[/]");
                    }
                    else
                    {
                        AnsiConsole.Markup($"[cyan]{Markup.Escape(modelDisplay)}[/]");
                    }
                }
            }

            // --- 恢复光标 ---
            // 计算光标在多行输入中的位置
            string textBeforeCursor = inputText.Substring(0, Math.Min(_cursorPosition, inputText.Length));
            int totalWidthBeforeCursor = 3 + GetDisplayWidth(textBeforeCursor); // ">> " + 文本
            int cursorRow = inputRowTop + (totalWidthBeforeCursor / safeWidth);
            int cursorCol = totalWidthBeforeCursor % safeWidth;
            
            // 确保光标在有效范围内
            cursorRow = Math.Min(cursorRow, inputEndRow);
            if (cursorCol >= safeWidth) cursorCol = safeWidth - 1;

            if (cursorRow >= 0 && cursorRow < Console.BufferHeight)
            {
                Console.SetCursorPosition(cursorCol, cursorRow);
            }

            Console.CursorVisible = wasCursorVisible || !_isProcessing;
            
            _bottomAreaStartLine = startTop;
            _lastWindowWidth = Console.WindowWidth;
            
            _logger.LogInformation("RenderBottomArea END: startTop={StartTop}, _bottomAreaStartLine={BottomLine}", startTop, _bottomAreaStartLine);
        }
        catch (Exception ex)
        {
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
        
        // 动态计算行数（清除足够多的行以覆盖可能的多行输入）
        const int maxTotalLines = 10; // 最大可能的行数
        
        int startTop = _bottomAreaStartLine;
        
        _logger.LogInformation("ClearBottomArea: startTop={StartTop}, CursorTop={CursorTop}", startTop, Console.CursorTop);
        
        // 清除底部区域
        int safeWidth = Math.Max(0, Console.WindowWidth - 1);
        string clearLine = new string(' ', safeWidth);
        
        for (int i = 0; i < maxTotalLines; i++)
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
        
        _logger.LogInformation("ClearBottomArea END: CursorTop={CursorTop}", Console.CursorTop);
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

    public void ShowReasoning(string reasoningContent)
    {
        if (string.IsNullOrWhiteSpace(reasoningContent)) return;

        SafeRender(() =>
        {
            // 使用折叠面板显示思维链内容
            var panel = new Panel(new Text(reasoningContent))
            {
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Grey),
                Padding = new Padding(1, 0, 1, 0),
                Header = new PanelHeader(" 💭 [dim]思维链 (Reasoning)[/] ", Justify.Left)
            };
            AnsiConsole.Write(panel);
            AnsiConsole.WriteLine();
        });
    }

    public void ShowResponse(string content, string? header = null)
    {
        // 停止动画
        _showingResponseHeader = false;

        // 恢复光标
        Console.CursorVisible = true;

        // 保存到历史
        _history.Add(new HistoryItem(HistoryType.Response, content, header));

        SafeRender(() =>
        {
            // 如果有标题，先显示标题
            if (!string.IsNullOrEmpty(header))
            {
                AnsiConsole.MarkupLine($"[{PrimaryColor.ToMarkup()}]⋮[/] {Markup.Escape(header)}");
            }

            // 显示内容
            if (!string.IsNullOrWhiteSpace(content))
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

                // 直接渲染内容
                AnsiConsole.Write(contentRenderable);
            }
        });
    }

    public void ShowError(string message)
    {
        // 保存到历史
        _history.Add(new HistoryItem(HistoryType.Error, message));
        
        SafeRender(() => AnsiConsole.MarkupLine($"[bold red]Error:[/] {Markup.Escape(message)}"));
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

    public void AddToolCallToHistory(string formattedContent)
    {
        lock (_consoleLock)
        {
            _history.Add(new HistoryItem(HistoryType.ToolCall, formattedContent));
        }
    }

    public string? ShowSelectionMenu(string title, string? description, IEnumerable<(string Display, string Value)> options)
    {
        var optionsList = options.ToList();
        if (optionsList.Count == 0)
            return null;

        // 先清除底部区域（在锁内）
        lock (_consoleLock)
        {
            ClearBottomArea();
        }
        
        // 显示标题和描述
        AnsiConsole.MarkupLine($"[bold]{Markup.Escape(title)}[/]");
        if (!string.IsNullOrEmpty(description))
        {
            AnsiConsole.MarkupLine($"[dim]{Markup.Escape(description)}[/]");
        }
        AnsiConsole.MarkupLine("[dim]↑↓ 选择, Enter 确认, Esc 取消[/]");
        AnsiConsole.WriteLine();

        // 自定义选择逻辑（因为 SelectionPrompt 可能有问题）
        var choices = optionsList.Select(o => o.Display).ToArray();
        int selectedIndex = 0;
        
        Console.CursorVisible = false;
        int startLine = Console.CursorTop;
        
        // 初始渲染
        RenderChoices(choices, selectedIndex, startLine);
        
        while (true)
        {
            var key = Console.ReadKey(true);
            
            if (key.Key == ConsoleKey.UpArrow)
            {
                selectedIndex = selectedIndex <= 0 ? choices.Length - 1 : selectedIndex - 1;
                RenderChoices(choices, selectedIndex, startLine);
            }
            else if (key.Key == ConsoleKey.DownArrow)
            {
                selectedIndex = (selectedIndex + 1) % choices.Length;
                RenderChoices(choices, selectedIndex, startLine);
            }
            else if (key.Key == ConsoleKey.Enter)
            {
                Console.CursorVisible = true;
                Console.SetCursorPosition(0, startLine + choices.Length);
                
                // 重新渲染底部区域
                lock (_consoleLock)
                {
                    RenderBottomArea();
                }
                
                return optionsList[selectedIndex].Value;
            }
            else if (key.Key == ConsoleKey.Escape)
            {
                Console.CursorVisible = true;
                Console.SetCursorPosition(0, startLine + choices.Length);
                
                // 重新渲染底部区域
                lock (_consoleLock)
                {
                    RenderBottomArea();
                }
                
                return null;
            }
        }
    }

    private void RenderChoices(string[] choices, int selectedIndex, int startLine)
    {
        for (int i = 0; i < choices.Length; i++)
        {
            Console.SetCursorPosition(0, startLine + i);
            Console.Write(new string(' ', Console.WindowWidth - 1)); // 清除行
            Console.SetCursorPosition(0, startLine + i);
            
            if (i == selectedIndex)
            {
                AnsiConsole.Markup($"[cyan]› {Markup.Escape(choices[i])}[/]");
            }
            else
            {
                AnsiConsole.Markup($"[dim]  {Markup.Escape(choices[i])}[/]");
            }
        }
    }

    public string GetCurrentInput()
    {
        lock (_consoleLock)
        {
            return _inputBuffer.ToString();
        }
    }
}