using Microsoft.Extensions.AI;
using Cade.Provider.Models;

namespace Cade.Provider.Services.Interfaces;

/// <summary>
/// AI 服务管理接口
/// </summary>
public interface IProviderService
{
    /// <summary>
    /// 从配置加载所有模型
    /// </summary>
    /// <param name="configuration">配置对象</param>
    /// <param name="configName">配置文件名</param>
    Task LoadModelsFromConfigAsync(ProviderConfig configuration, string configName);

    /// <summary>
    /// 获取所有可用的模型列表
    /// </summary>
    /// <returns>模型配置列表</returns>
    List<ModelConfig> GetAvailableModels();

    /// <summary>
    /// 发送消息到指定模型
    /// </summary>
    /// <param name="modelId">模型ID</param>
    /// <param name="historyMessages">聊天消息列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>AI响应</returns>
    Task<string> SendMessageAsync(string modelId, List<ChatMessage> historyMessages, CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送消息并获取流式响应
    /// </summary>
    /// <param name="modelId">模型ID</param>
    /// <param name="histtoryMessages">聊天消息列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>流式响应</returns>
    IAsyncEnumerable<string> SendMessageStreamAsync(string modelId, List<ChatMessage> histtoryMessages, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取当前加载的模型数量
    /// </summary>
    int GetLoadedModelCount();

    /// <summary>
    /// 清除所有已加载的模型
    /// </summary>
    void ClearModels();
}
