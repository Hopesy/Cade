using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Cade.Services;

/// <summary>
/// NuGet 更新检查服务
/// </summary>
public class UpdateChecker
{
    private const string PackageId = "Cade";
    private const string NuGetApiUrl = "https://api.nuget.org/v3-flatcontainer/{0}/index.json";
    
    private readonly ILogger<UpdateChecker>? _logger;
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    public UpdateChecker(ILogger<UpdateChecker>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// 获取当前版本
    /// </summary>
    public static Version GetCurrentVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version ?? new Version(1, 0, 0);
    }

    /// <summary>
    /// 检查是否有新版本
    /// </summary>
    /// <returns>如果有新版本返回版本号，否则返回 null</returns>
    public async Task<string?> CheckForUpdateAsync()
    {
        try
        {
            var currentVersion = GetCurrentVersion();
            var latestVersion = await GetLatestVersionAsync();
            
            if (latestVersion == null) return null;
            
            if (Version.TryParse(latestVersion, out var latest) && latest > currentVersion)
            {
                _logger?.LogInformation("发现新版本: {Latest}, 当前版本: {Current}", latestVersion, currentVersion);
                return latestVersion;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "检查更新失败");
            return null;
        }
    }

    /// <summary>
    /// 从 NuGet 获取最新版本
    /// </summary>
    private async Task<string?> GetLatestVersionAsync()
    {
        try
        {
            var url = string.Format(NuGetApiUrl, PackageId.ToLowerInvariant());
            var response = await _httpClient.GetFromJsonAsync<NuGetVersionResponse>(url);
            
            if (response?.Versions == null || response.Versions.Length == 0)
                return null;
            
            // 获取最新的稳定版本（排除预发布版本）
            var latestStable = response.Versions
                .Where(v => !v.Contains('-')) // 排除预发布版本
                .OrderByDescending(v => Version.TryParse(v, out var ver) ? ver : new Version(0, 0, 0))
                .FirstOrDefault();
            
            return latestStable ?? response.Versions.LastOrDefault();
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "获取 NuGet 版本失败");
            return null;
        }
    }

    /// <summary>
    /// 获取更新提示消息
    /// </summary>
    public static string GetUpdateMessage(string newVersion)
    {
        var current = GetCurrentVersion();
        return $"[yellow]⬆ 发现新版本 v{newVersion}[/] [dim](当前 v{current.Major}.{current.Minor}.{current.Build})[/]\n" +
               $"  运行 [cyan]dotnet tool update -g {PackageId}[/] 更新";
    }

    private class NuGetVersionResponse
    {
        [JsonPropertyName("versions")]
        public string[] Versions { get; set; } = [];
    }
}
