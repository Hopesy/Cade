using System;
using System.Threading.Tasks;

namespace Cade.Interfaces;

public interface IUserInterface
{
    void ShowWelcome();
    void ShowResponse(string content);
    void ShowError(string message);
    void ShowToolLog(string toolName, string command);
    void ShowLog(string message);

    // Non-blocking Input & Thread-safe Output Support
    bool KeyAvailable { get; }
    string? HandleKeyPress(ConsoleKeyInfo keyInfo);
    void SetProcessing(bool isProcessing, string? title = null);
    void SetStatus(string path, string modelId);
    void SafeRender(Action action);
    void Update();
}
