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

    [KernelFunction, Description("创建目录")]
    public string CreateDirectory([Description("目录路径")] string path)
    {
        if (Directory.Exists(path))
            return $"目录已存在: {path}";

        Directory.CreateDirectory(path);
        return $"成功创建目录: {path}";
    }

    [KernelFunction, Description("删除文件或目录")]
    public string Delete(
        [Description("文件或目录路径")] string path,
        [Description("如果是目录，是否递归删除")] bool recursive = false)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
            return $"成功删除文件: {path}";
        }

        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive);
            return $"成功删除目录: {path}";
        }

        return $"错误: 路径不存在 - {path}";
    }

    [KernelFunction, Description("移动或重命名文件/目录")]
    public string Move(
        [Description("源路径")] string source,
        [Description("目标路径")] string destination)
    {
        if (File.Exists(source))
        {
            var destDir = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            File.Move(source, destination);
            return $"成功移动文件: {source} -> {destination}";
        }

        if (Directory.Exists(source))
        {
            Directory.Move(source, destination);
            return $"成功移动目录: {source} -> {destination}";
        }

        return $"错误: 源路径不存在 - {source}";
    }

    [KernelFunction, Description("复制文件或目录")]
    public string Copy(
        [Description("源路径")] string source,
        [Description("目标路径")] string destination,
        [Description("是否覆盖已存在的文件")] bool overwrite = false)
    {
        if (File.Exists(source))
        {
            var destDir = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            File.Copy(source, destination, overwrite);
            return $"成功复制文件: {source} -> {destination}";
        }

        if (Directory.Exists(source))
        {
            CopyDirectory(source, destination, overwrite);
            return $"成功复制目录: {source} -> {destination}";
        }

        return $"错误: 源路径不存在 - {source}";
    }

    [KernelFunction, Description("获取文件或目录的详细信息")]
    public string GetInfo([Description("文件或目录路径")] string path)
    {
        var sb = new StringBuilder();

        if (File.Exists(path))
        {
            var info = new FileInfo(path);
            sb.AppendLine($"类型: 文件");
            sb.AppendLine($"路径: {info.FullName}");
            sb.AppendLine($"大小: {FormatSize(info.Length)}");
            sb.AppendLine($"创建时间: {info.CreationTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"修改时间: {info.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"只读: {info.IsReadOnly}");
            return sb.ToString();
        }

        if (Directory.Exists(path))
        {
            var info = new DirectoryInfo(path);
            var files = info.GetFiles("*", SearchOption.AllDirectories);
            var dirs = info.GetDirectories("*", SearchOption.AllDirectories);
            var totalSize = files.Sum(f => f.Length);

            sb.AppendLine($"类型: 目录");
            sb.AppendLine($"路径: {info.FullName}");
            sb.AppendLine($"文件数: {files.Length}");
            sb.AppendLine($"子目录数: {dirs.Length}");
            sb.AppendLine($"总大小: {FormatSize(totalSize)}");
            sb.AppendLine($"创建时间: {info.CreationTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"修改时间: {info.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
            return sb.ToString();
        }

        return $"错误: 路径不存在 - {path}";
    }

    [KernelFunction, Description("在文件中替换文本内容")]
    public async Task<string> ReplaceInFile(
        [Description("文件路径")] string path,
        [Description("要查找的文本")] string search,
        [Description("替换为的文本")] string replace)
    {
        if (!File.Exists(path))
            return $"错误: 文件不存在 - {path}";

        var content = await File.ReadAllTextAsync(path);
        var count = Regex.Matches(content, Regex.Escape(search)).Count;

        if (count == 0)
            return $"未找到匹配的文本: {search}";

        var newContent = content.Replace(search, replace);
        await File.WriteAllTextAsync(path, newContent);

        return $"成功替换 {count} 处匹配，文件: {path}";
    }

    [KernelFunction, Description("追加内容到文件末尾")]
    public async Task<string> AppendToFile(
        [Description("文件路径")] string path,
        [Description("要追加的内容")] string content)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        await File.AppendAllTextAsync(path, content);
        return $"成功追加内容到文件: {path}";
    }

    private static void CopyDirectory(string source, string destination, bool overwrite)
    {
        var dir = new DirectoryInfo(source);
        Directory.CreateDirectory(destination);

        foreach (var file in dir.GetFiles())
            file.CopyTo(Path.Combine(destination, file.Name), overwrite);

        foreach (var subDir in dir.GetDirectories())
            CopyDirectory(subDir.FullName, Path.Combine(destination, subDir.Name), overwrite);
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
