using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
namespace Cade.Provider.Models;

// 服务提供商配置，一个文件对应一个提供商
// 一个配置文件创建一个客户端，加载多个模型
public class ProviderConfig
{
    // 提供商唯一ID（仅内存使用，不序列化到JSON），用于内部索引和关联加载的模型
    [JsonIgnore]
    public string Id { get; set; } = string.Empty;
    // 配置文件名称（仅内存使用，不序列化到JSON），便于识别和调试
    [JsonIgnore]
    public string Name { get; set; } = string.Empty;
    // 提供商类型，后期会拓展DeepSeek,MinMax等
    [JsonConverter(typeof(StringEnumConverter))]
    public ProviderType Type { get; set; }

    // API密钥
    public string ApiKey { get; set; } = string.Empty;
    
    // 自定义BaseUrl（仅兼容服务需要）
    public string? BaseUrl { get; set; }
    // 模型ID，多个用逗号分隔（如：gpt-4,gpt-4o）
    public string ModelIds { get; set; } = string.Empty;
    // 是否启用
    public bool IsEnabled { get; set; } = true;
}
