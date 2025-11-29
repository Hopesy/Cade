namespace MinoChat.Provider.Services;

/// <summary>
/// Anthropic HTTP 请求拦截器
/// 用于重定向 Anthropic SDK 的请求到自定义 URL
/// </summary>
public class AnthropicHttpHandler : DelegatingHandler
{
    private readonly string? _customBaseUrl;
    private const string DefaultAnthropicUrl = "https://api.anthropic.com";

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="customBaseUrl">自定义的 BaseUrl（兼容服务使用）</param>
    /// <param name="innerHandler">内部处理器</param>
    public AnthropicHttpHandler(string? customBaseUrl, HttpMessageHandler? innerHandler = null)
    {
        _customBaseUrl = customBaseUrl?.TrimEnd('/');
        InnerHandler = innerHandler ?? new HttpClientHandler();
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // 如果配置了自定义 BaseUrl，则拦截并重定向指向官方 API 的请求
        if (!string.IsNullOrEmpty(_customBaseUrl) && request.RequestUri != null)
        {
            var originalUri = request.RequestUri.ToString();

            // 只拦截指向 Anthropic 官方 API 的请求
            if (originalUri.StartsWith(DefaultAnthropicUrl, StringComparison.OrdinalIgnoreCase))
            {
                // 将官方 API URL 替换为自定义 URL，保留完整路径和查询字符串
                var newUri = originalUri.Replace(DefaultAnthropicUrl, _customBaseUrl);
                request.RequestUri = new Uri(newUri);
            }
        }

        // 继续执行请求
        return await base.SendAsync(request, cancellationToken);
    }
}
