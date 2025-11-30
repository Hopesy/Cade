using System.Text.Json;

namespace Cade.Tool.Tools;

/// <summary>
/// 读取文件工具
/// </summary>
public class ReadFileTool : ToolBase
{
    public override string Name => "read_file";
    public override string Description => "读取指定路径的文件内容";

    public override Task<ToolResult> ExecuteAsync(string parameters)
    {
        return SafeExecuteAsync(async () =>
        {
            var options = JsonSerializer.Deserialize<ReadFileOptions>(parameters);
            if (options == null || string.IsNullOrEmpty(options.FilePath))
            {
                return ToolResult.CreateFailure("缺少必要参数: FilePath");
            }

            if (!File.Exists(options.FilePath))
            {
                return ToolResult.CreateFailure($"文件不存在: {options.FilePath}");
            }

            var content = await File.ReadAllTextAsync(options.FilePath);
            return ToolResult.CreateSuccess(content);
        });
    }

    private class ReadFileOptions
    {
        public string FilePath { get; set; } = string.Empty;
    }
}
