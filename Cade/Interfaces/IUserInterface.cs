using Spectre.Console;

namespace Cade.Interfaces;

public interface IUserInterface
{
    void ShowWelcome();
    string GetInput(string prompt, string path, string modelId);
    void ShowResponse(string content);
    void ShowError(string message);
    Task ShowThinkingAsync(Func<Task> action);

    // 新增：用于显示工具执行过程
    void ShowToolLog(string toolName, string command);
    void ShowLog(string message);
}
