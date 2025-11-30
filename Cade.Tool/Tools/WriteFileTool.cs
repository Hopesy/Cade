using System.Text.Json;

namespace Cade.Tool.Tools;

/// <summary>
/// 写入文件工具
/// </summary>
public class WriteFileTool : ToolBase
{
    public override string Name => "write_file";
    public override string Description => "将内容写入到指定路径的文件";

    public override Task<ToolResult> ExecuteAsync(string parameters)
    {
        return SafeExecuteAsync(async () =>
        {
            var options = JsonSerializer.Deserialize<WriteFileOptions>(parameters);
            if (options == null || string.IsNullOrEmpty(options.FilePath) || options.Content == null)
            {
                return ToolResult.CreateFailure("缺少必要参数: FilePath 或 Content");
            }

            // 确保目录存在
            var directory = Path.GetDirectoryName(options.FilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(options.FilePath, options.Content);
            return ToolResult.CreateSuccess($"文件已成功写入: {options.FilePath}");
        });
    }

    private class WriteFileOptions
    {
        public string FilePath { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
}
