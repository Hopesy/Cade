using MinoChat.Data.Entities;

namespace MinoChat.Data.Services.Interfaces;

/// <summary>
/// 聊天数据服务接口
/// </summary>
public interface IChatDataService
{
    /// <summary>
    /// 加载所有聊天会话（包含消息）
    /// </summary>
    /// <returns>聊天会话列表</returns>
    Task<IEnumerable<ChatSession>> LoadChatSessionsAsync();

    /// <summary>
    /// 保存单个聊天会话（包含消息）
    /// </summary>
    /// <param name="chatSession">聊天会话</param>
    Task SaveChatSessionAsync(ChatSession chatSession);

    /// <summary>
    /// 批量保存聊天会话
    /// </summary>
    /// <param name="chatSessions">聊天会话列表</param>
    Task SaveChatSessionsAsync(IEnumerable<ChatSession> chatSessions);

    /// <summary>
    /// 删除聊天会话（级联删除关联的消息）
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    Task DeleteChatSessionAsync(Guid sessionId);

    /// <summary>
    /// 根据ID获取单个会话（包含消息）
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns>聊天会话，不存在则返回null</returns>
    Task<ChatSession?> GetChatSessionByIdAsync(Guid sessionId);
}
