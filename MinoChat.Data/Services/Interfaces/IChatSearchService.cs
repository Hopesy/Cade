using MinoChat.Data.Entities;

namespace MinoChat.Data.Services.Interfaces;

/// <summary>
/// 聊天搜索服务接口
/// </summary>
public interface IChatSearchService
{
    /// <summary>
    /// 全文搜索消息内容
    /// </summary>
    /// <param name="keyword">搜索关键词</param>
    /// <returns>匹配的消息列表（包含所属会话信息）</returns>
    Task<IEnumerable<ChatMessage>> SearchMessagesAsync(string keyword);

    /// <summary>
    /// 按会话标题搜索
    /// </summary>
    /// <param name="keyword">搜索关键词</param>
    /// <returns>匹配的会话列表</returns>
    Task<IEnumerable<ChatSession>> SearchSessionsByTitleAsync(string keyword);

    /// <summary>
    /// 按日期范围查询消息
    /// </summary>
    /// <param name="startDate">开始日期</param>
    /// <param name="endDate">结束日期</param>
    /// <returns>匹配的消息列表</returns>
    Task<IEnumerable<ChatMessage>> GetMessagesByDateRangeAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// 搜索用户消息
    /// </summary>
    /// <param name="keyword">搜索关键词</param>
    /// <returns>匹配的用户消息列表</returns>
    Task<IEnumerable<ChatMessage>> SearchUserMessagesAsync(string keyword);

    /// <summary>
    /// 搜索AI消息
    /// </summary>
    /// <param name="keyword">搜索关键词</param>
    /// <returns>匹配的AI消息列表</returns>
    Task<IEnumerable<ChatMessage>> SearchAiMessagesAsync(string keyword);
}
