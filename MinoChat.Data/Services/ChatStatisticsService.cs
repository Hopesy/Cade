using FreeSql;
using Microsoft.Extensions.Logging;
using MinoChat.Data.Entities;
using MinoChat.Data.Services.Interfaces;

namespace MinoChat.Data.Services;

/// <summary>
/// 聊天统计服务实现
/// </summary>
public class ChatStatisticsService : IChatStatisticsService
{
    private readonly IFreeSql _freeSql;
    private readonly ILogger<ChatStatisticsService> _logger;

    public ChatStatisticsService(IFreeSql freeSql, ILogger<ChatStatisticsService> logger)
    {
        _freeSql = freeSql;
        _logger = logger;
    }

    /// <summary>
    /// 获取总消息数
    /// </summary>
    public async Task<long> GetTotalMessageCountAsync()
    {
        try
        {
            var count = await _freeSql.Select<ChatMessage>().CountAsync();
            _logger.LogInformation("总消息数: {Count}", count);
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取总消息数失败");
            return 0;
        }
    }

    /// <summary>
    /// 获取总会话数
    /// </summary>
    public async Task<long> GetTotalSessionCountAsync()
    {
        try
        {
            var count = await _freeSql.Select<ChatSession>().CountAsync();
            _logger.LogInformation("总会话数: {Count}", count);
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取总会话数失败");
            return 0;
        }
    }

    /// <summary>
    /// 获取用户消息数
    /// </summary>
    public async Task<long> GetUserMessageCountAsync()
    {
        try
        {
            var count = await _freeSql.Select<ChatMessage>()
                .Where(m => m.IsUserMessage)
                .CountAsync();
            _logger.LogInformation("用户消息数: {Count}", count);
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户消息数失败");
            return 0;
        }
    }

    /// <summary>
    /// 获取AI消息数
    /// </summary>
    public async Task<long> GetAiMessageCountAsync()
    {
        try
        {
            var count = await _freeSql.Select<ChatMessage>()
                .Where(m => !m.IsUserMessage)
                .CountAsync();
            _logger.LogInformation("AI消息数: {Count}", count);
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取AI消息数失败");
            return 0;
        }
    }

    /// <summary>
    /// 按日期统计消息数
    /// </summary>
    public async Task<long> GetMessageCountByDateAsync(DateTime date)
    {
        try
        {
            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1);

            var count = await _freeSql.Select<ChatMessage>()
                .Where(m => m.Timestamp >= startOfDay && m.Timestamp < endOfDay)
                .CountAsync();

            _logger.LogInformation("{Date} 的消息数: {Count}", date.ToShortDateString(), count);
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取指定日期消息数失败");
            return 0;
        }
    }

    /// <summary>
    /// 获取最常用的模型
    /// </summary>
    public async Task<Dictionary<string, int>> GetModelUsageStatisticsAsync()
    {
        try
        {
            var sessions = await _freeSql.Select<ChatSession>().ToListAsync();
            var statistics = sessions
                .GroupBy(s => s.Model)
                .ToDictionary(g => g.Key, g => g.Count())
                .OrderByDescending(kvp => kvp.Value)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            _logger.LogInformation("模型使用统计: {Statistics}", string.Join(", ", statistics.Select(s => $"{s.Key}:{s.Value}")));
            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取模型使用统计失败");
            return new Dictionary<string, int>();
        }
    }

    /// <summary>
    /// 获取平均对话长度
    /// </summary>
    public async Task<double> GetAverageChatLengthAsync()
    {
        try
        {
            var sessions = await _freeSql.Select<ChatSession>()
                .IncludeMany(s => s.Messages)
                .ToListAsync();

            if (!sessions.Any())
                return 0;

            var average = sessions.Average(s => s.Messages?.Count ?? 0);
            _logger.LogInformation("平均对话长度: {Average}", average);
            return average;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取平均对话长度失败");
            return 0;
        }
    }

    /// <summary>
    /// 获取最活跃的日期
    /// </summary>
    public async Task<Dictionary<DateTime, int>> GetMostActiveDatesAsync(int topCount = 10)
    {
        try
        {
            var messages = await _freeSql.Select<ChatMessage>().ToListAsync();
            var statistics = messages
                .GroupBy(m => m.Timestamp.Date)
                .OrderByDescending(g => g.Count())
                .Take(topCount)
                .ToDictionary(g => g.Key, g => g.Count());

            _logger.LogInformation("最活跃日期统计（Top {Count}）", topCount);
            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取最活跃日期失败");
            return new Dictionary<DateTime, int>();
        }
    }

    /// <summary>
    /// 获取最近N天的消息统计
    /// </summary>
    public async Task<Dictionary<DateTime, int>> GetRecentDaysStatisticsAsync(int days = 7)
    {
        try
        {
            var endDate = DateTime.Now.Date.AddDays(1);
            var startDate = endDate.AddDays(-days);

            var messages = await _freeSql.Select<ChatMessage>()
                .Where(m => m.Timestamp >= startDate && m.Timestamp < endDate)
                .ToListAsync();

            var statistics = new Dictionary<DateTime, int>();
            for (int i = 0; i < days; i++)
            {
                var date = startDate.AddDays(i).Date;
                var count = messages.Count(m => m.Timestamp.Date == date);
                statistics[date] = count;
            }

            _logger.LogInformation("最近 {Days} 天消息统计", days);
            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取最近N天统计失败");
            return new Dictionary<DateTime, int>();
        }
    }
}
