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

            _isProcessing = isProcessing;
            if (title != null) _statusTitle = title;

            // 停止处理时恢复光标
            if (!isProcessing)
            {
                Console.CursorVisible = true; // 恢复光标
            }

            // Re-render to show/hide status line
            ClearBottomArea();
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
        var grid = new Grid();
        grid.AddColumn(new GridColumn().Padding(0, 0, 1, 0));
        grid.AddColumn(new GridColumn());
        grid.AddRow(new Markup("[green]➜[/]"), new Markup($"[bold white]{Markup.Escape(message)}[/]"));
        AnsiConsole.Write(grid);
        AnsiConsole.WriteLine(); // 确保换行
    }

    private void RenderBottomArea(bool overwrite = false)
    {
        // Logic:
        // 1. Calculate needed lines.
        //    - If Processing: Status Line + Input Line.
        //    - If Not: Input Line.
        // 2. If overwrite=true, we assume the previous frame had the SAME height/state
        //    and we just move cursor up and redraw.
        // 3. If overwrite=false (e.g. SafeRender), we assume cursor is at a "clean" line 
        //    (history printed) and we just draw downwards.
        
        // Actually, SafeRender calls ClearBottomArea first. 
        // ClearBottomArea moves cursor to the "Top" of the bottom area.
        // So RenderBottomArea simply draws from current cursor.
        
        if (overwrite)
        {
            // Move cursor up to start of BottomArea
            int linesUp = _isProcessing ? 1 : 0;
            if (Console.CursorTop > linesUp)
                Console.CursorTop -= linesUp;
            Console.CursorLeft = 0;
        }

        // 1. Render Status Line (if processing)
        if (_isProcessing)
        {
            var elapsed = DateTime.Now - _processStartTime;
            string timeStr = $"({elapsed.TotalSeconds:F1}s)";
            string spinner = _spinnerFrames[_spinnerFrame];
            
            // Clear line first to remove artifacts?
            // ClearLine(); 
            // Using MarkupLine will overwrite, but if shorter, artifacts remain.
            // Better to clear.
            ClearCurrentLine();
            
            AnsiConsole.MarkupLine($"[blue]{spinner}[/] {_statusTitle} [grey]{timeStr}[/]");
        }

        // 2. Render Input Line
        ClearCurrentLine();
        AnsiConsole.Markup($"[grey]>>[/] ");
        Console.Write(_inputBuffer.ToString());
        
        // Clean up right side (if text was deleted)
        // ClearCurrentLine handles the whole line, but we just wrote partially.
        // We need to ensure no artifacts to the right.
        int currentLeft = Console.CursorLeft;
        int spaces = Math.Max(0, Console.WindowWidth - currentLeft - 1);
        Console.Write(new string(' ', spaces));
        Console.CursorLeft = currentLeft;
        
        // No newline at the end, cursor stays at end of input
    }

    private void ClearBottomArea()
    {
        // Clear 2 lines if processing, 1 if not.
        // Assumes cursor is at the END of the Input Line.
        
        int linesToClear = _isProcessing ? 2 : 1;
        
        // Move up (linesToClear - 1) because we are on the last line.
        // e.g. 1 line: Move up 0. 2 lines: Move up 1.
        
        int currentLine = Console.CursorTop;
        
        // Careful about top of buffer
        int targetTop = currentLine - (linesToClear - 1);
        if (targetTop < 0) targetTop = 0;
        
        Console.SetCursorPosition(0, targetTop);
        
        for (int i = 0; i < linesToClear; i++)
        {
            ClearCurrentLine();
            if (i < linesToClear - 1) Console.WriteLine();
        }
        
        // Move back to top
        Console.SetCursorPosition(0, targetTop);
    }

    private void ClearCurrentLine()
    {
        Console.Write("\r" + new string(' ', Console.WindowWidth - 1) + "\r");
    }

    public void ShowResponseHeader(string summary)
    {
        SafeRender(() =>
        {
            // 记录当前行位置和开始时间
            _responseHeaderLine = Console.CursorTop;
            _currentResponseSummary = summary;
            _showingResponseHeader = true;
            _aiDotFrame = 0;
            _lastSpinnerTick = DateTime.Now;
            _responseHeaderStartTime = DateTime.Now;

            // 隐藏光标
            Console.CursorVisible = false;

            // 首次显示
            var dots = _aiResponseDots[_aiDotFrame];
            AnsiConsole.MarkupLine($"[{PrimaryColor.ToMarkup()}]{dots}[/] {Markup.Escape(summary)}");
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
            // 解析 Markdown 内容
            try
            {
                var parsed = MarkdownRenderer.Parse(content);

                // 渲染所有元素
                foreach (var element in parsed.Elements)
                {
                    AnsiConsole.Write(element);
                    AnsiConsole.WriteLine();
                }
            }
            catch
            {
                // 如果解析失败，则作为纯文本显示
                AnsiConsole.WriteLine(content);
            }

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
