namespace Cade.Tool.Tools;

/// <summary>
/// 获取当前时间工具
/// </summary>
public class GetTimeTool : ToolBase
{
    public override string Name => "get_time";
    public override string Description => "获取当前系统时间";

    public override Task<ToolResult> ExecuteAsync(string parameters)
    {
        return SafeExecuteAsync(async () =>
        {
            await Task.CompletedTask; // 保持异步接口一致性

            var now = DateTime.Now;
            var result = $"当前时间: {now:yyyy-MM-dd HH:mm:ss}\n" +
                        $"日期: {now:yyyy年MM月dd日}\n" +
                        $"时间: {now:HH:mm:ss}\n" +
                        $"星期: {GetDayOfWeekInChinese(now.DayOfWeek)}";

            return ToolResult.CreateSuccess(result);
        });
    }

    private static string GetDayOfWeekInChinese(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Monday => "星期一",
            DayOfWeek.Tuesday => "星期二",
            DayOfWeek.Wednesday => "星期三",
            DayOfWeek.Thursday => "星期四",
            DayOfWeek.Friday => "星期五",
            DayOfWeek.Saturday => "星期六",
            DayOfWeek.Sunday => "星期日",
            _ => "未知"
        };
    }
}
