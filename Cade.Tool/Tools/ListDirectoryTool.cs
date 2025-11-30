using System.Text;
using System.Text.Json;

namespace Cade.Tool.Tools;

/// <summary>
/// 列出目录内容工具
/// </summary>
public class ListDirectoryTool : ToolBase
{
    public override string Name => "list_directory";
    public override string Description => "列出指定目录下的文件和子目录";

    public override Task<ToolResult> ExecuteAsync(string parameters)
    {
        return SafeExecuteAsync(async () =>
        {
            await Task.CompletedTask; // 保持异步接口一致性

            var options = JsonSerializer.Deserialize<ListDirectoryOptions>(parameters);
            var path = options?.DirectoryPath ?? Environment.CurrentDirectory;

            if (!Directory.Exists(path))
            {
                return ToolResult.CreateFailure($"目录不存在: {path}");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"目录: {path}\n");

            // 列出子目录
            var dirs = Directory.GetDirectories(path);
            if (dirs.Length > 0)
            {
                sb.AppendLine("【目录】:");
                foreach (var dir in dirs)
                {
                    var dirInfo = new DirectoryInfo(dir);
                    sb.AppendLine($"  📁 {dirInfo.Name}");
                }
                sb.AppendLine();
            }

            // 列出文件
            var files = Directory.GetFiles(path);
            if (files.Length > 0)
            {
                sb.AppendLine("【文件】:");
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    var size = FormatFileSize(fileInfo.Length);
                    sb.AppendLine($"  📄 {fileInfo.Name} ({size})");
                }
            }

            if (dirs.Length == 0 && files.Length == 0)
            {
                sb.AppendLine("（空目录）");
            }

            return ToolResult.CreateSuccess(sb.ToString());
        });
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private class ListDirectoryOptions
    {
        public string? DirectoryPath { get; set; }
    }
}
