using Cade.Interfaces;

namespace Cade.Services;

public class MockAiService : IAiService
{
    private readonly IUserInterface _ui;

    public MockAiService(IUserInterface ui)
    {
        _ui = ui;
    }

    public async Task<string> GetResponseAsync(string input, string modelId)
    {
        input = input.ToLower();

        // 场景 1: 模拟复杂任务（工具链不断调用）
        // 例如用户问 "检查项目状态" 或 "读取文件"
        if (input.Contains("检查") || input.Contains("check") || input.Contains("文件") || input.Contains("file"))
        {
            return await SimulateAgentLoopAsync(input);
        }

        // 场景 2: 普通对话 (快速回复)
        await Task.Delay(1000); // 简单思考

        if (input.Contains("你好") || input.Contains("hello"))
        {
            return "你好！我是 [bold #D97757]Cade Code[/]，你的智能终端助手。\n我可以帮你[bold]执行命令[/]、[bold]读取文件[/]或者[bold]编写代码[/]。";
        }

        if (input.Contains("代码") || input.Contains("code"))
        {
            return """
            这是一个 C# 示例：
            
            [green]public async Task[/] [yellow]ExecuteAsync[/]()
            {
                [purple]await[/] Agent.RunTool([cyan]"ls -la"[/]);
            }
            """;
        }

        return "我听到了。试着对我说：[green]\"帮我检查当前目录下的文件\"[/] 来体验工具链调用。";
    }

    private async Task<string> SimulateAgentLoopAsync(string input)
    {
        // 步骤 1: 规划 (思考)
        await Task.Delay(800);
        
        // 步骤 2: 工具调用 - ListFiles
        _ui.ShowToolLog("Bash", "ls -la .");
        await Task.Delay(600); // 模拟工具执行时间
        _ui.ShowLog("drwxr-xr-x  User  Interfaces");
        _ui.ShowLog("drwxr-xr-x  User  Services");
        _ui.ShowLog("drwxr-xr-x  User  ViewModels");
        _ui.ShowLog("-rw-r--r--  User  Program.cs");
        _ui.ShowLog("-rw-r--r--  User  Cade.csproj");
        
        await Task.Delay(500); // 观察结果

        // 步骤 3: 再次思考 (决定下一步)
        // 这里的 UI 效果由外部的 Spinner 维持，或者我们可以再次更新状态
        
        // 步骤 4: 工具调用 - ReadFile (假设 Agent 决定读取 Program.cs)
        _ui.ShowToolLog("File", "read Program.cs --lines 1-10");
        await Task.Delay(1200);
        _ui.ShowLog("using Microsoft.Extensions.DependencyInjection;");
        _ui.ShowLog("using Microsoft.Extensions.Hosting;");
        _ui.ShowLog("using Cade;");
        _ui.ShowLog("...");

        await Task.Delay(800); // 分析文件内容

        // 步骤 5: 最终总结
        return """
        已完成当前目录的检查。
        
        我发现这是一个基于 **.NET 8** 的项目，采用 **Clean Architecture** 结构：
        
        *   [bold]Interfaces/[/]: 定义了系统的核心抽象。
        *   [bold]Services/[/]: 包含具体的实现逻辑。
        *   [bold]Program.cs[/]: 使用 Generic Host 进行启动。
        
        系统配置看起来很正常，随时可以开始开发。
        """;
    }
}