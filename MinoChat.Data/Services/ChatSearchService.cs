using FreeSql;
using Microsoft.Extensions.Logging;
using MinoChat.Data.Entities;
using MinoChat.Data.Services.Interfaces;

namespace MinoChat.Data.Services;

/// <summary>
/// 聊天搜索服务实现
/// </summary>
public class ChatSearchService : IChatSearchService
{
    private readonly IFreeSql _freeSql;
    private readonly ILogger<ChatSearchService> _logger;

    public ChatSearchService(IFreeSql freeSql, ILogger<ChatSearchService> logger)
    {
        _freeSql = freeSql;
        _logger = logger;
    }

    /// <summary>
    /// 全文搜索消息内容
    /// </summary>
    public async Task<IEnumerable<ChatMessage>> SearchMessagesAsync(string keyword)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return new List<ChatMessage>();

            _logger.LogInformation("搜索消息，关键词: {Keyword}", keyword);

            var messages = await _freeSql.Select<ChatMessage>()
                .Where(m => m.Content.Contains(keyword))
                .Include(m => m.Session)
                .OrderByDescending(m => m.Timestamp)
                .ToListAsync();

            _logger.LogInformation("搜索到 {Count} 条消息", messages.Count);
            return messages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "搜索消息失败");
            return new List<ChatMessage>();
        }
    }

    /// <summary>
    /// 按会话标题搜索
    /// </summary>
    public async Task<IEnumerable<ChatSession>> SearchSessionsByTitleAsync(string keyword)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return new List<ChatSession>();

            _logger.LogInformation("搜索会话标题，关键词: {Keyword}", keyword);

            var sessions = await _freeSql.Select<ChatSession>()
                .Where(s => s.Title.Contains(keyword))
                .IncludeMany(s => s.Messages, then => then.OrderBy(m => m.Timestamp))
                .OrderByDescending(s => s.LastMessageTime)
                .ToListAsync();

            _logger.LogInformation("搜索到 {Count} 个会话", sessions.Count);
            return sessions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "搜索会话失败");
            return new List<ChatSession>();
        }
    }

    /// <summary>
    /// 按日期范围查询消息
    /// </summary>
    public async Task<IEnumerable<ChatMessage>> GetMessagesByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            _logger.LogInformation("查询日期范围消息: {StartDate} - {EndDate}", startDate, endDate);

            var messages = await _freeSql.Select<ChatMessage>()
                .Where(m => m.Timestamp >= startDate && m.Timestamp <= endDate)
                .Include(m => m.Session)
                .OrderByDescending(m => m.Timestamp)
                .ToListAsync();

            _logger.LogInformation("查询到 {Count} 条消息", messages.Count);
            return messages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询日期范围消息失败");
            return new List<ChatMessage>();
        }
    }

    /// <summary>
    /// 搜索用户消息
    /// </summary>
    public async Task<IEnumerable<ChatMessage>> SearchUserMessagesAsync(string keyword)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return new List<ChatMessage>();

            _logger.LogInformation("搜索用户消息，关键词: {Keyword}", keyword);

            var messages = await _freeSql.Select<ChatMessage>()
                .Where(m => m.IsUserMessage && m.Content.Contains(keyword))
                .Include(m => m.Session)
                .OrderByDescending(m => m.Timestamp)
                .ToListAsync();

            _logger.LogInformation("搜索到 {Count} 条用户消息", messages.Count);
            return messages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "搜索用户消息失败");
            return new List<ChatMessage>();
        }
    }

    /// <summary>
    /// 搜索AI消息
    /// </summary>
    public async Task<IEnumerable<ChatMessage>> SearchAiMessagesAsync(string keyword)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return new List<ChatMessage>();

            _logger.LogInformation("搜索AI消息，关键词: {Keyword}", keyword);

            var messages = await _freeSql.Select<ChatMessage>()
                .Where(m => !m.IsUserMessage && m.Content.Contains(keyword))
                .Include(m => m.Session)
                .OrderByDescending(m => m.Timestamp)
                .ToListAsync();

            _logger.LogInformation("搜索到 {Count} 条AI消息", messages.Count);
            return messages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "搜索AI消息失败");
            return new List<ChatMessage>();
        }
    }
}
