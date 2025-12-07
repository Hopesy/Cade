using FreeSql;
using Microsoft.Extensions.Logging;
using Cade.Data.Entities;
using Cade.Data.Services.Interfaces;

namespace Cade.Data.Services;

/// <summary>
/// 聊天数据服务实现
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

    public async Task<ChatSession?> GetSessionByWorkDirectoryAsync(string workDirectory)
    {
        try
        {
            var session = await _freeSql.Select<ChatSession>()
                .Where(s => s.WorkDirectory == workDirectory)
                .IncludeMany(s => s.Messages, then => then.OrderBy(m => m.SequenceNumber))
                .FirstAsync();

            if (session != null)
            {
                _logger.LogInformation("找到会话: {WorkDir}, 消息数: {Count}", 
                    workDirectory, session.Messages.Count);
            }

            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取会话失败: {WorkDir}", workDirectory);
            return null;
        }
    }

    public async Task SaveSessionAsync(ChatSession session)
    {
        try
        {
            var existing = await _freeSql.Select<ChatSession>()
                .Where(s => s.Id == session.Id)
                .FirstAsync();

            if (existing == null)
            {
                await _freeSql.Insert(session).ExecuteAffrowsAsync();
            }
            else
            {
                await _freeSql.Update<ChatSession>()
                    .SetSource(session)
                    .ExecuteAffrowsAsync();
            }

            // 保存消息
            foreach (var message in session.Messages)
            {
                message.SessionId = session.Id;
                var existingMsg = await _freeSql.Select<ChatMessage>()
                    .Where(m => m.Id == message.Id)
                    .FirstAsync();

                if (existingMsg == null)
                {
                    await _freeSql.Insert(message).ExecuteAffrowsAsync();
                }
            }

            _logger.LogInformation("保存会话成功: {SessionId}", session.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存会话失败: {SessionId}", session.Id);
            throw;
        }
    }

    public async Task AddMessageAsync(Guid sessionId, MessageType type, string content, string? modelId = null, string? toolName = null)
    {
        try
        {
            // 获取当前最大序号
            var maxSeq = await _freeSql.Select<ChatMessage>()
                .Where(m => m.SessionId == sessionId)
                .MaxAsync(m => m.SequenceNumber);

            var message = new ChatMessage
            {
                SessionId = sessionId,
                Type = type,
                Content = content,
                ModelId = modelId,
                ToolName = toolName,
                SequenceNumber = maxSeq + 1,
                Timestamp = DateTime.Now
            };

            await _freeSql.Insert(message).ExecuteAffrowsAsync();

            // 更新会话最后活动时间
            await UpdateLastActiveTimeAsync(sessionId);

            _logger.LogInformation("添加消息成功: {SessionId}, Type: {Type}", sessionId, type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加消息失败: {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<ChatSession> CreateSessionAsync(string workDirectory, string model)
    {
        try
        {
            var session = new ChatSession
            {
                WorkDirectory = workDirectory,
                Title = Path.GetFileName(workDirectory) ?? "New Session",
                Model = model,
                CreatedTime = DateTime.Now,
                LastMessageTime = DateTime.Now
            };

            await _freeSql.Insert(session).ExecuteAffrowsAsync();
            _logger.LogInformation("创建会话成功: {WorkDir}", workDirectory);

            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建会话失败: {WorkDir}", workDirectory);
            throw;
        }
    }

    public async Task UpdateLastActiveTimeAsync(Guid sessionId)
    {
        try
        {
            await _freeSql.Update<ChatSession>()
                .Where(s => s.Id == sessionId)
                .Set(s => s.LastMessageTime, DateTime.Now)
                .ExecuteAffrowsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新会话时间失败: {SessionId}", sessionId);
        }
    }

    public async Task DeleteSessionAsync(Guid sessionId)
    {
        try
        {
            await _freeSql.Delete<ChatMessage>()
                .Where(m => m.SessionId == sessionId)
                .ExecuteAffrowsAsync();

            await _freeSql.Delete<ChatSession>()
                .Where(s => s.Id == sessionId)
                .ExecuteAffrowsAsync();

            _logger.LogInformation("删除会话成功: {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除会话失败: {SessionId}", sessionId);
            throw;
        }
    }

    public async Task ClearMessagesAsync(Guid sessionId)
    {
        try
        {
            await _freeSql.Delete<ChatMessage>()
                .Where(m => m.SessionId == sessionId)
                .ExecuteAffrowsAsync();

            _logger.LogInformation("清空会话消息成功: {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清空会话消息失败: {SessionId}", sessionId);
            throw;
        }
    }
}
