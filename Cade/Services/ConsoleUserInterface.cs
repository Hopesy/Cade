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

        AnsiConsole.MarkupLine($"[bold {PrimaryColor.ToMarkup()}]欢迎使用 Cade CLI[/] - [grey]v1.0.0[/]");
        AnsiConsole.MarkupLine("[grey]Type /help for more information.[/]");
        AnsiConsole.Write(new Rule("[grey]Ready[/]").LeftJustified());
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
            }
            
            _isProcessing = isProcessing;
            if (title != null) _statusTitle = title;
            
            // Re-render to show/hide status line
            ClearBottomArea();
            RenderBottomArea();
        }
    }

    public void Update()
    {
        if (_isProcessing)
        {
            bool needRender = false;
            
            // Update Spinner
            if ((DateTime.Now - _lastSpinnerTick).TotalMilliseconds > 100)
            {
                _spinnerFrame = (_spinnerFrame + 1) % _spinnerFrames.Length;
                _lastSpinnerTick = DateTime.Now;
                needRender = true;
            }
            
            // Update Time (every 100ms is fine)
            if (needRender)
            {
                lock (_consoleLock)
                {
                    // We can optimize by only redrawing Status Line?
                    // But SafeRender/RenderBottomArea logic is coupled. 
                    // For simplicity, redraw both.
                    // We need to clear properly first.
                    // NOTE: We cannot use ClearBottomArea inside Update easily if we don't know PREVIOUS state?
                    // We know current state is Processing.
                    // So we assume 2 lines are currently drawn.
                    
                    // To avoid flickering, we can try to just overwrite the Status Line?
                    // Status Line is at (Current - 1).
                    // Input Line is at (Current).
                    
                    RenderBottomArea(overwrite: true);
                }
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

    public void ShowResponse(string content)
    {
        SafeRender(() => 
        {
            var markup = new Markup(content);
            var panel = new Panel(markup)
            {
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(AccentColor),
                Padding = new Padding(1, 1, 1, 1),
                Header = new PanelHeader("[bold]Cade[/]", Justify.Left)
            };
            AnsiConsole.Write(panel);
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
