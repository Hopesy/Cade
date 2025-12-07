using FreeSql.DataAnnotations;

namespace Cade.Data.Entities;

/// <summary>
/// 聊天会话实体
/// </summary>
[Table(Name = "chat_sessions")]
[Index("idx_work_dir", "WorkDirectory", IsUnique = true)]
[Index("idx_last_message_time", "LastMessageTime DESC")]
public class ChatSession
{
    /// <summary>
    /// 会话唯一标识
    /// </summary>
    [Column(IsPrimary = true)]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 工作目录路径（唯一索引，用于按目录缓存会话）
    /// </summary>
    [Column(StringLength = 500, IsNullable = false)]
    public string WorkDirectory { get; set; } = string.Empty;

    /// <summary>
    /// 会话标题
    /// </summary>
    [Column(StringLength = 200, IsNullable = false)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 使用的模型名称
    /// </summary>
    [Column(StringLength = 100, IsNullable = false)]
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// 最后一条消息的时间
    /// </summary>
    [Column(IsNullable = false)]
    public DateTime LastMessageTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 创建时间
    /// </summary>
    [Column(IsNullable = false)]
    public DateTime CreatedTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 导航属性：关联的消息列表
    /// </summary>
    [Navigate(nameof(ChatMessage.SessionId))]
    public List<ChatMessage> Messages { get; set; } = new();
}
