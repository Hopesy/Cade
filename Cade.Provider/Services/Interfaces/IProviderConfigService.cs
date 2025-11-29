using Cade.Provider.Models;

namespace Cade.Provider.Services.Interfaces;

/// <summary>
/// 配置文件管理服务接口
/// </summary>
public interface IProviderConfigService
{
    /// <summary>
    /// 获取所有配置文件名称列表
    /// </summary>
    /// <returns>配置文件名称列表（不含扩展名）</returns>
    Task<List<string>> GetAllConfigFilesAsync();

    /// <summary>
    /// 加载指定的配置文件
    /// </summary>
    /// <param name="fileName">配置文件名（不含扩展名）</param>
    /// <returns>配置对象，如果文件不存在则返回null</returns>
    Task<ProviderConfig?> LoadConfigurationAsync(string fileName);

    /// <summary>
    /// 保存配置文件
    /// </summary>
    /// <param name="configuration">配置对象</param>
    /// <param name="fileName">配置文件名（不含扩展名）</param>
    Task SaveConfigurationAsync(ProviderConfig configuration, string fileName);

    /// <summary>
    /// 创建新的配置文件
    /// </summary>
    /// <param name="fileName">配置文件名（不含扩展名）</param>
    /// <returns>新创建的配置对象</returns>
    Task<ProviderConfig> CreateNewConfigAsync(string fileName);

    /// <summary>
    /// 删除配置文件
    /// </summary>
    /// <param name="fileName">配置文件名（不含扩展名）</param>
    Task DeleteConfigAsync(string fileName);

    /// <summary>
    /// 验证配置是否有效（测试API连接）
    /// </summary>
    /// <param name="providerConfig">提供商配置</param>
    /// <returns>验证结果：成功返回null，失败返回错误信息</returns>
    Task<string?> ValidateProviderConfigAsync(ProviderConfig providerConfig);

    /// <summary>
    /// 确保默认配置文件存在
    /// 如果不存在，则从嵌入资源中提取
    /// </summary>
    Task EnsureDefaultConfigExistsAsync();

    /// <summary>
    /// 获取当前使用的配置文件名
    /// </summary>
    string GetCurrentConfigFileName();

    /// <summary>
    /// 设置当前使用的配置文件名
    /// </summary>
    /// <param name="fileName">配置文件名（不含扩展名）</param>
    Task SetCurrentConfigFileNameAsync(string fileName);
}
