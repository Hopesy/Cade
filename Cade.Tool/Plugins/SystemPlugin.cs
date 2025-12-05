using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.SemanticKernel;

namespace Cade.Tool.Plugins;

/// <summary>
/// 系统操作插件
/// </summary>
public class SystemPlugin
{
    [KernelFunction, Description("获取当前系统时间")]
    public string GetTime()
    {
        var now = DateTime.Now;
        return $"当前时间: {now:yyyy-MM-dd HH:mm:ss} ({now.DayOfWeek})";
    }

    [KernelFunction, Description("获取系统基本信息")]
    public string GetSystemInfo()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"操作系统: {RuntimeInformation.OSDescription}");
        sb.AppendLine($"架构: {RuntimeInformation.OSArchitecture}");
        sb.AppendLine($".NET: {RuntimeInformation.FrameworkDescription}");
        sb.AppendLine($"机器名: {Environment.MachineName}");
        sb.AppendLine($"用户: {Environment.UserName}");
        sb.AppendLine($"CPU核心: {Environment.ProcessorCount}");
        sb.AppendLine($"当前目录: {Environment.CurrentDirectory}");
        return sb.ToString();
    }

    [KernelFunction, Description("获取网络接口信息")]
    public string GetNetworkInfo()
    {
        var sb = new StringBuilder();
        foreach (var iface in NetworkInterface.GetAllNetworkInterfaces()
            .Where(i => i.OperationalStatus == OperationalStatus.Up))
        {
            sb.AppendLine($"{iface.Name} ({iface.NetworkInterfaceType})");
            foreach (var ip in iface.GetIPProperties().UnicastAddresses
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork))
            {
                sb.AppendLine($"  IP: {ip.Address}");
            }
        }
        return sb.ToString();
    }

    [KernelFunction, Description("执行系统命令（30秒超时）")]
    public async Task<string> ExecuteCommand(
        [Description("要执行的命令")] string command,
        [Description("工作目录")] string? workingDirectory = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/bash",
            Arguments = OperatingSystem.IsWindows() ? $"/c {command}" : $"-c \"{command}\"",
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null) return "无法启动进程";

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        
        try
        {
            var outputTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var errorTask = process.StandardError.ReadToEndAsync(cts.Token);
            
            await process.WaitForExitAsync(cts.Token);
            
            var output = await outputTask;
            var error = await errorTask;

            var result = new StringBuilder();
            if (!string.IsNullOrEmpty(output)) result.AppendLine(output.TrimEnd());
            if (!string.IsNullOrEmpty(error)) result.AppendLine($"[stderr]: {error.TrimEnd()}");
            result.AppendLine($"[退出码: {process.ExitCode}]");

            return result.ToString();
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            return $"命令执行超时（30秒），已终止进程。";
        }
    }
}
