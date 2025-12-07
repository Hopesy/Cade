using Cade.Provider.Models;
using Microsoft.SemanticKernel;

namespace Cade.Provider.Services.Interfaces;

/// <summary>
/// AI 服务管理接口 (基于 Semantic Kernel)
/// </summary>
public interface IProviderService
{
    /// <summary>
    /// 从配置加载 Kernel
    /// </summary>
    Task LoadModelsFromConfigAsync(ProviderConfig configuration, string configName);

    /// <summary>
    /// 获取所有可用的模型列表
    /// </summary>
    List<ModelConfig> GetAvailableModels();

    /// <summary>
    /// 获取当前 Kernel 实例
    /// </summary>
    Kernel? GetKernel();

    /// <summary>
    /// 获取当前选中的模型ID
    /// </summary>
    string? GetCurrentModelId();

    /// <summary>
    /// 设置当前使用的模型
    /// </summary>
    void SetCurrentModel(string modelConfigId);

    /// <summary>
    /// 添加插件到 Kernel
    /// </summary>
    void AddPlugin(KernelPlugin plugin);

    /// <summary>
    /// 获取当前加载的模型数量
    /// </summary>
    int GetLoadedModelCount();

    /// <summary>
    /// 清除所有已加载的模型
    /// </summary>
    void ClearModels();

    /// <summary>
    /// 获取当前配置
    /// </summary>
    ProviderConfig? GetCurrentConfig();
}
