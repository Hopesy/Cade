namespace Cade.Provider.Models;

/// <summary>
/// AI服务提供商类型
/// </summary>
public enum ProviderType
{
    /// <summary>
    /// Anthropic官方服务
    /// </summary>
    Anthropic,

    /// <summary>
    /// Anthropic兼容服务（需要自定义BaseUrl）
    /// </summary>
    AnthropicCompatible,

    /// <summary>
    /// OpenAI官方服务
    /// </summary>
    OpenAI,

    /// <summary>
    /// OpenAI兼容服务（需要自定义BaseUrl）
    /// </summary>
    OpenAICompatible
}
