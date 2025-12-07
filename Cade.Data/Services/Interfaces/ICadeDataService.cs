using Cade.Data.Entities;

namespace Cade.Data.Services.Interfaces;

/// <summary>
/// Cade 数据服务接口
/// </summary>
public interface IChatDataService
{
    /// <summary>
    /// 根据工作目录获取会话
    /// </summary>
    Task<ChatSession?> GetSessionByWorkDirectoryAsync(string workDirectory);

    /// <summary>
    /// 保存会话（包含消息）
    /// </summary>
    Task SaveSessionAsync(ChatSession session);

    /// <summary>
    /// 添加消息到会话
    /// </summary>
    Task AddMessageAsync(Guid sessionId, MessageType type, string content, string? modelId = null, string? toolName = null);

    /// <summary>
    /// 创建新会话
    /// </summary>
    Task<ChatSession> CreateSessionAsync(string workDirectory, string model);

    /// <summary>
    /// 更新会话最后活动时间
    /// </summary>
    Task UpdateLastActiveTimeAsync(Guid sessionId);

    /// <summary>
    /// 删除会话
    /// </summary>
    Task DeleteSessionAsync(Guid sessionId);

    /// <summary>
    /// 清空会话消息
    /// </summary>
    Task ClearMessagesAsync(Guid sessionId);
}
