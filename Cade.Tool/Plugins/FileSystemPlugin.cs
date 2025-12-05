using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;

namespace Cade.Tool.Plugins;

/// <summary>
/// 文件系统操作插件
/// </summary>
public class FileSystemPlugin
{
    [KernelFunction, Description("读取指定路径的文件内容")]
    public string ReadFile([Description("文件路径")] string path)
    {
        if (!File.Exists(path))
            return $"错误: 文件不存在 - {path}";

        return File.ReadAllText(path);
    }

    [KernelFunction, Description("将内容写入到指定文件")]
    public string WriteFile(
        [Description("文件路径")] string path,
        [Description("要写入的内容")] string content)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(path, content);
        return $"成功写入文件: {path}";
    }

    [KernelFunction, Description("列出指定目录下的文件和子目录")]
    public string ListDirectory([Description("目录路径，默认当前目录")] string? path = null)
    {
        var dirPath = path ?? Environment.CurrentDirectory;
        if (!Directory.Exists(dirPath))
            return $"错误: 目录不存在 - {dirPath}";

        var sb = new StringBuilder();
        sb.AppendLine($"目录: {dirPath}\n");

        foreach (var dir in Directory.GetDirectories(dirPath))
            sb.AppendLine($"📁 {Path.GetFileName(dir)}/");

        foreach (var file in Directory.GetFiles(dirPath))
        {
            var info = new FileInfo(file);
            sb.AppendLine($"📄 {info.Name} ({FormatSize(info.Length)})");
        }

        return sb.ToString();
    }

    [KernelFunction, Description("搜索匹配模式的文件")]
    public string SearchFiles(
        [Description("搜索目录")] string directory,
        [Description("文件模式，如 *.cs")] string pattern,
        [Description("是否递归搜索")] bool recursive = true)
    {
        if (!Directory.Exists(directory))
            return $"错误: 目录不存在 - {directory}";

        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.GetFiles(directory, pattern, option);

        var sb = new StringBuilder();
        sb.AppendLine($"找到 {files.Length} 个文件:\n");

        foreach (var file in files.Take(50))
            sb.AppendLine($"  {Path.GetRelativePath(directory, file)}");

        if (files.Length > 50)
            sb.AppendLine($"\n... 还有 {files.Length - 50} 个文件");

        return sb.ToString();
    }

    [KernelFunction, Description("在文件中搜索匹配的文本")]
    public async Task<string> Grep(
        [Description("搜索的文本或正则表达式")] string pattern,
        [Description("文件或目录路径")] string path,
        [Description("文件匹配模式")] string filePattern = "*.*")
    {
        var regex = new Regex(pattern, RegexOptions.IgnoreCase);
        var sb = new StringBuilder();
        var count = 0;

        IEnumerable<string> files = File.Exists(path)
            ? [path]
            : Directory.Exists(path)
                ? Directory.GetFiles(path, filePattern, SearchOption.AllDirectories)
                : [];

        foreach (var file in files)
        {
            var lines = await File.ReadAllLinesAsync(file);
            for (int i = 0; i < lines.Length && count < 50; i++)
            {
                if (regex.IsMatch(lines[i]))
                {
                    sb.AppendLine($"{Path.GetFileName(file)}:{i + 1}: {lines[i].Trim()}");
                    count++;
                }
            }
        }

        return count == 0 ? "未找到匹配内容" : $"找到 {count} 处匹配:\n{sb}";
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1) { order++; len /= 1024; }
        return $"{len:0.#} {sizes[order]}";
    }
}
