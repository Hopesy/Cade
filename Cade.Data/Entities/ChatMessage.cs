using FreeSql.DataAnnotations;

namespace Cade.Data.Entities;

/// <summary>
/// 消息类型
/// </summary>
public enum MessageType
{
    User = 0,       // 用户消息
    Assistant = 1,  // AI 回复
    ToolCall = 2,   // 工具调用
    Reasoning = 3   // 思考内容
}

/// <summary>
/// 聊天消息实体
/// </summary>
[Table(Name = "chat_messages")]
[Index("idx_session_seq", "SessionId, SequenceNumber")]
public class ChatMessage
{
    /// <summary>
    /// 消息唯一标识
    /// </summary>
    [Column(IsPrimary = true)]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 所属会话ID
    /// </summary>
    [Column(IsNullable = false)]
    public Guid SessionId { get; set; }

    /// <summary>
    /// 消息类型
    /// </summary>
    [Column(IsNullable = false)]
    public MessageType Type { get; set; } = MessageType.User;

    /// <summary>
    /// 消息内容
    /// </summary>
    [Column(StringLength = -1, IsNullable = false)]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 是否为用户消息（兼容旧代码）
    /// </summary>
    [Column(IsIgnore = true)]
    public bool IsUserMessage => Type == MessageType.User;

    /// <summary>
    /// 消息时间戳
    /// </summary>
    [Column(IsNullable = false)]
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// 消息序号
    /// </summary>
    [Column(IsNullable = false)]
    public int SequenceNumber { get; set; }

    /// <summary>
    /// 模型ID（仅AI消息有值）
    /// </summary>
    [Column(StringLength = 100, IsNullable = true)]
    public string? ModelId { get; set; }

    /// <summary>
    /// 工具名称（仅工具调用有值）
    /// </summary>
    [Column(StringLength = 100, IsNullable = true)]
    public string? ToolName { get; set; }

    /// <summary>
    /// 导航属性：所属会话
    /// </summary>
    [Navigate(nameof(SessionId))]
    public ChatSession? Session { get; set; }
}
