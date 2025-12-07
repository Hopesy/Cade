using FreeSql.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MinoChat.Data.Entities;

// 聊天消息实体
[Table(Name = "chat_messages")]
// 复合索引。优化查询某个特定会话SessionId的所有消息，并按时间倒序排列Timestamp DESC的性能
[Index("idx_session_time", "SessionId, Timestamp DESC")]
public partial class ChatMessage : ObservableObject
{
    // 消息唯一标识,主键
    [Column(IsPrimary = true)]
    public Guid Id { get; set; } = Guid.NewGuid();

    // 所属会话ID（外键）
    [Column(IsNullable = false)]
    public Guid SessionId { get; set; }

    // 消息内容
    private string _content = string.Empty;

    [Column(StringLength = -1, IsNullable = false)] // -1 表示TEXT类型，无长度限制
    public string Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }

    // 是否正在流式渲染中（不持久化到数据库，仅用于UI状态）
    private bool _isStreaming = false;

    [Column(IsIgnore = true)] // 不持久化到数据库
    public bool IsStreaming
    {
        get => _isStreaming;
        set => SetProperty(ref _isStreaming, value);
    }

    // 是否为用户消息（true: 用户消息, false: AI 消息）
    [Column(IsNullable = false)]
    public bool IsUserMessage { get; set; }

    // 消息时间戳
    [Column(IsNullable = false)]
    public DateTime Timestamp { get; set; } = DateTime.Now;

    // 消息序号（用于保证排序的稳定性，从0开始递增）
    [Column(IsNullable = false)]
    public int SequenceNumber { get; set; }

    // 模型ID（仅AI消息有值，用户消息为null）
    [Column(StringLength = 100, IsNullable = true)]
    public string? ModelId { get; set; }

    // 导航属性：所属会话
    // 定义了"多对一"的导航关系,告诉FreeSql使用本类中的SessionId字段去关联ChatSession表的主键
    // 可以通过message.Session属性延迟加载(如果开启)或贪婪加载（.Include(m => m.Session)）对应的会话
    [Navigate(nameof(SessionId))]
    public ChatSession? Session { get; set; }
}
