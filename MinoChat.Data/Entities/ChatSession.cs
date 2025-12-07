using FreeSql.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MinoChat.Data.Entities;

// 聊天会话实体
// 将类映射到chat_sessions 表
[Table(Name = "chat_sessions")]
// 在LastMessageTime字段上创建了一个降序索引，对于"获取最近活跃的会话"这类查询非常有用
[Index("idx_last_message_time", "LastMessageTime DESC")]
public partial class ChatSession : ObservableObject
{
    private string _title = string.Empty;
    private string _model = string.Empty;

    // 会话唯一标识
    [Column(IsPrimary = true)]
    public Guid Id { get; set; } = Guid.NewGuid();

    // 会话标题
    [Column(StringLength = 200, IsNullable = false)]
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    // 使用的模型名称
    [Column(StringLength = 50, IsNullable = false)]
    public string Model
    {
        get => _model;
        set => SetProperty(ref _model, value);
    }

    // 最后一条消息的时间
    [Column(IsNullable = false)]
    public DateTime LastMessageTime { get; set; } = DateTime.Now;

    // 创建时间
    [Column(IsNullable = false)]
    public DateTime CreatedTime { get; set; } = DateTime.Now;

    // 导航属性：关联的消息列表
    // 定义了一个<<一对多>>的导航关系，ChatSession表的主键Id对应ChatMessage表的SessionId字段
    // 查询ChatSession并使用.IncludeMany(s => s.Messages)时
    // FreeSql知道如何去chat_messages表中查找所有匹配SessionId的消息
    [Navigate(nameof(ChatMessage.SessionId))]
    public List<ChatMessage> Messages { get; set; } = new();

    // 消息数量（计算属性，不映射到数据库）
    [Column(IsIgnore = true)]
    public int MessageCount => Messages?.Count ?? 0;
}
