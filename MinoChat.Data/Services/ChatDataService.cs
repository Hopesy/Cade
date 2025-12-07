using FreeSql;
using Microsoft.Extensions.Logging;
using MinoChat.Data.Entities;
using MinoChat.Data.Services.Interfaces;

namespace MinoChat.Data.Services;

/// <summary>
/// SQLite 聊天数据服务实现
/// </summary>
public class ChatDataService : IChatDataService
{
    private readonly IFreeSql _freeSql;
    private readonly ILogger<ChatDataService> _logger;

    public ChatDataService(IFreeSql freeSql, ILogger<ChatDataService> logger)
    {
        _freeSql = freeSql;
        _logger = logger;
    }

    /// <summary>
    /// 加载所有聊天会话（包含消息）
    /// </summary>
    public async Task<IEnumerable<ChatSession>> LoadChatSessionsAsync()
    {
        try
        {
            _logger.LogInformation("开始加载所有聊天会话");

            // 加载所有会话，并预加载关联的消息
            var sessions = await _freeSql.Select<ChatSession>()
                .IncludeMany(s => s.Messages, then => then.OrderBy(m => m.SequenceNumber))
                .OrderByDescending(s => s.LastMessageTime)
                .ToListAsync();

            _logger.LogInformation("成功加载 {Count} 个聊天会话", sessions.Count);
            return sessions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载聊天会话失败");
            return new List<ChatSession>();
        }
    }

    /// <summary>
    /// 保存单个聊天会话（包含消息）
    /// </summary>
    public async Task SaveChatSessionAsync(ChatSession chatSession)
    {
        try
        {
            _logger.LogInformation("开始保存会话: {SessionId}", chatSession.Id);

            // 检查会话是否存在
            var existingSession = await _freeSql.Select<ChatSession>()
                .Where(s => s.Id == chatSession.Id)
                .FirstAsync();

            if (existingSession == null)
            {
                // 新增会话
                await _freeSql.Insert(chatSession).ExecuteAffrowsAsync();
                _logger.LogInformation("新增会话: {SessionId}", chatSession.Id);
            }
            else
            {
                // 更新会话
                await _freeSql.Update<ChatSession>()
                    .SetSource(chatSession)
                    .ExecuteAffrowsAsync();
                _logger.LogInformation("更新会话: {SessionId}", chatSession.Id);
            }

            // 处理消息
            if (chatSession.Messages != null && chatSession.Messages.Any())
            {
                foreach (var message in chatSession.Messages)
                {
                    message.SessionId = chatSession.Id; // 确保外键正确

                    var existingMessage = await _freeSql.Select<ChatMessage>()
                        .Where(m => m.Id == message.Id)
                        .FirstAsync();

                    if (existingMessage == null)
                    {
                        await _freeSql.Insert(message).ExecuteAffrowsAsync();
                    }
                    else
                    {
                        await _freeSql.Update<ChatMessage>()
                            .SetSource(message)
                            .ExecuteAffrowsAsync();
                    }
                }
            }

            _logger.LogInformation("会话保存成功: {SessionId}, 消息数: {MessageCount}",
                chatSession.Id, chatSession.Messages?.Count ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存聊天会话失败: {SessionId}", chatSession.Id);
            throw;
        }
    }

    /// <summary>
    /// 批量保存聊天会话
    /// </summary>
    public async Task SaveChatSessionsAsync(IEnumerable<ChatSession> chatSessions)
    {
        try
        {
            _logger.LogInformation("开始批量保存 {Count} 个会话", chatSessions.Count());

            foreach (var session in chatSessions)
            {
                await SaveChatSessionAsync(session);
            }

            _logger.LogInformation("批量保存完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量保存聊天会话失败");
            throw;
        }
    }

    /// <summary>
    /// 删除聊天会话（级联删除关联的消息）
    /// </summary>
    public async Task DeleteChatSessionAsync(Guid sessionId)
    {
        try
        {
            _logger.LogInformation("开始删除会话: {SessionId}", sessionId);

            // 先删除关联的消息
            await _freeSql.Delete<ChatMessage>()
                .Where(m => m.SessionId == sessionId)
                .ExecuteAffrowsAsync();

            // 再删除会话
            var affectedRows = await _freeSql.Delete<ChatSession>()
                .Where(s => s.Id == sessionId)
                .ExecuteAffrowsAsync();

            if (affectedRows > 0)
            {
                _logger.LogInformation("成功删除会话: {SessionId}", sessionId);
            }
            else
            {
                _logger.LogWarning("会话不存在: {SessionId}", sessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除聊天会话失败: {SessionId}", sessionId);
            throw;
        }
    }

    /// <summary>
    /// 根据ID获取单个会话（包含消息）
    /// </summary>
    public async Task<ChatSession?> GetChatSessionByIdAsync(Guid sessionId)
    {
        try
        {
            _logger.LogInformation("查询会话: {SessionId}", sessionId);

            var session = await _freeSql.Select<ChatSession>()
                .Where(s => s.Id == sessionId)
                .IncludeMany(s => s.Messages, then => then.OrderBy(m => m.SequenceNumber))
                .FirstAsync();

            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询会话失败: {SessionId}", sessionId);
            return null;
        }
    }
}
