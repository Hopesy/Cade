using Cade.Interfaces;
using System.Text;
using System.Threading.Tasks;
using Spectre.Console;

namespace Cade.Services;

public class MockAiService : IAiService
{
    private readonly IUserInterface _ui;

#pragma warning disable CS0067 // Mock 服务不触发事件
    public event EventHandler<ToolCallEventArgs>? ToolCalled;
#pragma warning restore CS0067

    public MockAiService(IUserInterface ui)
    {
        _ui = ui;
    }

    public async Task<string> GetResponseAsync(string input, string modelId)
    {
        // 始终调用 SimulateAgentLoopAsync 来模拟工具链过程
        return await SimulateAgentLoopAsync(input);
    }

    private async Task<string> SimulateAgentLoopAsync(string input)
    {
        StringBuilder toolOutputBuilder = new StringBuilder();

        // 步骤 1: 规划 (思考)
        _ui.SetProcessing(true, "规划中...");
        await Task.Delay(800);
        
        // 步骤 2: 工具调用 - ListFiles
        _ui.SetProcessing(true, "执行 'ls -la .'...");
        await Task.Delay(600); // 模拟工具执行时间
        toolOutputBuilder.AppendLine("drwxr-xr-x  User  Interfaces");
        toolOutputBuilder.AppendLine("drwxr-xr-x  User  Services");
        toolOutputBuilder.AppendLine("drwxr-xr-x  User  ViewModels");
        toolOutputBuilder.AppendLine("-rw-r--r--  User  Program.cs");
        toolOutputBuilder.AppendLine("-rw-r--r--  User  Cade.csproj");
        _ui.ShowToolLog("Bash", "ls -la .", toolOutputBuilder.ToString().TrimEnd());
        toolOutputBuilder.Clear(); // Clear for next tool

        await Task.Delay(500); // 观察结果

        // 步骤 3: 再次思考 (决定下一步)
        _ui.SetProcessing(true, "分析结果，规划下一步...");
        await Task.Delay(800); 
        
        // 步骤 4: 工具调用 - ReadFile (假设 Agent 决定读取 Program.cs)
        _ui.SetProcessing(true, "执行 'read Program.cs --lines 1-10'...");
        await Task.Delay(1200);
        toolOutputBuilder.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        toolOutputBuilder.AppendLine("using Microsoft.Extensions.Hosting;");
        toolOutputBuilder.AppendLine("using Cade;");
        toolOutputBuilder.AppendLine("...");
        _ui.ShowToolLog("File", "read Program.cs --lines 1-10", toolOutputBuilder.ToString().TrimEnd());
        toolOutputBuilder.Clear();

        await Task.Delay(800); // 分析文件内容

        // 步骤 5: 最终总结
        _ui.SetProcessing(true, "生成回复中...");
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