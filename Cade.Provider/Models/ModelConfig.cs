namespace Cade.Provider.Models;

// 模型配置，一个配置文件创建一个客户端，加载多个模型
public class ModelConfig
{
    // 模型唯一ID（用于内部标识）
    public string Id { get; set; } = Guid.NewGuid().ToString();
    /// 显示名称（在UI中显示），目前展示的是真实模型ID，后期考虑做个映射
    public string DisplayName { get; set; } = string.Empty;
    // 真实模型ID（API调用时使用）
    public string ModelId { get; set; } = string.Empty;
    // 关联的提供商ID，即ProviderID,通过这个ID来区分调用的时候使用哪个客户端IChatClient
    public string ProviderId { get; set; } = string.Empty;
    // 模型是否启用
    public bool IsEnabled { get; set; } = true;
    /// 模型的描述信息
    public string? Description { get; set; }
}
