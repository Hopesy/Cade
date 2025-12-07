namespace MinoChat.Data.Services.Interfaces;

/// <summary>
/// 聊天统计服务接口
/// </summary>
public interface IChatStatisticsService
{
    /// <summary>
    /// 获取总消息数
    /// </summary>
    Task<long> GetTotalMessageCountAsync();

    /// <summary>
    /// 获取总会话数
    /// </summary>
    Task<long> GetTotalSessionCountAsync();

    /// <summary>
    /// 获取用户消息数
    /// </summary>
    Task<long> GetUserMessageCountAsync();

    /// <summary>
    /// 获取AI消息数
    /// </summary>
    Task<long> GetAiMessageCountAsync();

    /// <summary>
    /// 按日期统计消息数
    /// </summary>
    /// <param name="date">日期</param>
    /// <returns>消息数量</returns>
    Task<long> GetMessageCountByDateAsync(DateTime date);

    /// <summary>
    /// 获取最常用的模型
    /// </summary>
    /// <returns>模型名称及使用次数</returns>
    Task<Dictionary<string, int>> GetModelUsageStatisticsAsync();

    /// <summary>
    /// 获取平均对话长度（每个会话的平均消息数）
    /// </summary>
    Task<double> GetAverageChatLengthAsync();

    /// <summary>
    /// 获取最活跃的日期（按消息数排序）
    /// </summary>
    /// <param name="topCount">返回前N个日期，默认10</param>
    /// <returns>日期及消息数</returns>
    Task<Dictionary<DateTime, int>> GetMostActiveDatesAsync(int topCount = 10);

    /// <summary>
    /// 获取最近N天的消息统计
    /// </summary>
    /// <param name="days">天数</param>
    /// <returns>每天的消息数</returns>
    Task<Dictionary<DateTime, int>> GetRecentDaysStatisticsAsync(int days = 7);
}
