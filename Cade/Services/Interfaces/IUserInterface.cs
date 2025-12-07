namespace Cade.Services.Interfaces;

public interface IUserInterface
{
    void ShowWelcome();
    void ShowResponse(string content, string? header = null);
    void ShowResponseHeader(string summary); // 新增：显示回复头部（带动画的点 + 总结）
    void ShowError(string message);
    void ShowToolLog(string toolName, string command, string output);
    void ShowLog(string message);

    // Non-blocking Input & Thread-safe Output Support
    bool KeyAvailable { get; }
    string? HandleKeyPress(ConsoleKeyInfo keyInfo);
    void SetProcessing(bool isProcessing, string? title = null);
    void SetStatus(string path, string modelId);
    void SafeRender(Action action);
    void Update();
    
    /// <summary>
    /// 添加工具调用到历史记录（用于窗口大小变化时重绘）
    /// </summary>
    void AddToolCallToHistory(string formattedContent);
}
