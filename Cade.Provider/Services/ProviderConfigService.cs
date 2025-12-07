using Microsoft.Extensions.Logging;
using Cade.Provider.Models;
using Cade.Provider.Services.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cade.Provider.Services;

/// <summary>
/// 配置文件管理服务
/// 配置存储位置：~/.cade/settings.json
/// </summary>
public class ProviderConfigService : IProviderConfigService
{
    private readonly ILogger<ProviderConfigService> _logger;
    private readonly string _userConfigDirectory;
    private readonly string _settingsFilePath;
    private readonly string _appSettingsFilePath;
    private string _currentConfigFileName = "default";

    public ProviderConfigService(ILogger<ProviderConfigService> logger)
    {
        _logger = logger;

        // 用户配置目录：~/.cade
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _userConfigDirectory = Path.Combine(userProfile, ".cade");
        _settingsFilePath = Path.Combine(_userConfigDirectory, "settings.json");

        // appsettings.json 文件路径（用于保存当前选择的模型等）
        _appSettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

        // 确保目录存在
        Directory.CreateDirectory(_userConfigDirectory);

        // 加载当前配置文件名
        LoadCurrentConfigFileName();
    }

    public Task<List<string>> GetAllConfigFilesAsync()
    {
        // 简化：只有一个配置文件
        return Task.FromResult(new List<string> { "default" });
    }

    /// <summary>
    /// 从 ~/.cade/settings.json 加载配置
    /// </summary>
    public async Task<ProviderConfig?> LoadConfigurationAsync(string fileName)
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                _logger.LogWarning("配置文件不存在: {Path}，请创建 ~/.cade/settings.json", _settingsFilePath);
                return null;
            }

            var json = await File.ReadAllTextAsync(_settingsFilePath);
            var settingsObj = JObject.Parse(json);
            var env = settingsObj["env"];

            if (env == null)
            {
                _logger.LogWarning("settings.json 中缺少 env 配置节");
                return null;
            }

            var apiKey = env["CADE_AUTH_TOKEN"]?.ToString();
            var baseUrl = env["CADE_BASE_URL"]?.ToString();
            var modelIds = env["CADE_DEFAULT_MODEL"]?.ToString();
            var providerTypeStr = env["CADE_PROVIDE_TYPE"]?.ToString();

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("settings.json 中缺少 CADE_AUTH_TOKEN");
                return null;
            }

            // 设置默认模型
            if (string.IsNullOrWhiteSpace(modelIds))
                modelIds = "gpt-4o";

            // 解析 ProviderType
            if (!Enum.TryParse<ProviderType>(providerTypeStr, true, out var providerType))
                providerType = ProviderType.OpenAICompatible;

            // 处理 BaseUrl，确保以 /v1 结尾
            if (!string.IsNullOrWhiteSpace(baseUrl) && !baseUrl.EndsWith("/v1") && !baseUrl.EndsWith("/v1/"))
                baseUrl = baseUrl.TrimEnd('/') + "/v1";

            var config = new ProviderConfig
            {
                Type = providerType,
                ApiKey = apiKey,
                BaseUrl = baseUrl,
                ModelIds = modelIds,
                IsEnabled = true
            };

            _logger.LogInformation("成功从 {Path} 加载配置", _settingsFilePath);
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载配置文件失败: {Path}", _settingsFilePath);
            return null;
        }
    }

    public async Task SaveConfigurationAsync(ProviderConfig configuration, string fileName)
    {
        try
        {
            // 读取现有配置或创建新的
            JObject settingsObj;
            if (File.Exists(_settingsFilePath))
            {
                var existingJson = await File.ReadAllTextAsync(_settingsFilePath);
                settingsObj = JObject.Parse(existingJson);
            }
            else
            {
                settingsObj = new JObject();
            }

            // 更新 env 节
            settingsObj["env"] = new JObject
            {
                ["CADE_AUTH_TOKEN"] = configuration.ApiKey,
                ["CADE_BASE_URL"] = configuration.BaseUrl?.TrimEnd('/').Replace("/v1", "") ?? "",
                ["CADE_DEFAULT_MODEL"] = configuration.ModelIds,
                ["CADE_PROVIDE_TYPE"] = configuration.Type.ToString()
            };

            await File.WriteAllTextAsync(_settingsFilePath, settingsObj.ToString(Formatting.Indented));
            _logger.LogInformation("成功保存配置到 {Path}", _settingsFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存配置文件失败");
            throw;
        }
    }

    public async Task<ProviderConfig> CreateNewConfigAsync(string fileName)
    {
        var newConfig = new ProviderConfig
        {
            Type = ProviderType.OpenAICompatible,
            ApiKey = "your-api-key-here",
            BaseUrl = "https://api.openai.com",
            ModelIds = "gpt-4o",
            IsEnabled = true
        };
        await SaveConfigurationAsync(newConfig, fileName);
        return newConfig;
    }

    public Task DeleteConfigAsync(string fileName)
    {
        // 不支持删除，只有一个配置文件
        throw new InvalidOperationException("不能删除默认配置文件");
    }

    public Task<string?> ValidateProviderConfigAsync(ProviderConfig providerConfig)
    {
        if (string.IsNullOrWhiteSpace(providerConfig.ApiKey))
            return Task.FromResult<string?>("API密钥不能为空");

        if (string.IsNullOrWhiteSpace(providerConfig.ModelIds))
            return Task.FromResult<string?>("模型ID不能为空");

        if ((providerConfig.Type == ProviderType.AnthropicCompatible ||
             providerConfig.Type == ProviderType.OpenAICompatible) &&
            string.IsNullOrWhiteSpace(providerConfig.BaseUrl))
            return Task.FromResult<string?>("兼容服务必须提供BaseUrl");

        return Task.FromResult<string?>(null);
    }

    /// <summary>
    /// 确保配置文件存在，如果不存在则创建示例配置
    /// </summary>
    public async Task EnsureDefaultConfigExistsAsync()
    {
        if (File.Exists(_settingsFilePath))
            return;

        _logger.LogInformation("配置文件不存在，正在创建示例配置: {Path}", _settingsFilePath);

        var exampleSettings = new JObject
        {
            ["env"] = new JObject
            {
                ["CADE_AUTH_TOKEN"] = "your-api-key-here",
                ["CADE_BASE_URL"] = "https://api.openai.com",
                ["CADE_DEFAULT_MODEL"] = "gpt-4o",
                ["CADE_PROVIDE_TYPE"] = "OpenAICompatible"
            }
        };

        await File.WriteAllTextAsync(_settingsFilePath, exampleSettings.ToString(Formatting.Indented));
        _logger.LogInformation("已创建示例配置文件，请编辑 {Path} 填入正确的 API 密钥", _settingsFilePath);
    }

    public string GetCurrentConfigFileName() => _currentConfigFileName;

    public async Task SetCurrentConfigFileNameAsync(string fileName)
    {
        _currentConfigFileName = fileName;
        await UpdateAppSettingsAsync("CurrentProviderConfig", fileName);
    }

    private void LoadCurrentConfigFileName()
    {
        try
        {
            if (File.Exists(_appSettingsFilePath))
            {
                var json = File.ReadAllText(_appSettingsFilePath);
                var jObject = JObject.Parse(json);
                var appSettings = jObject["AppSettings"];
                if (appSettings?["CurrentProviderConfig"] != null)
                {
                    _currentConfigFileName = appSettings["CurrentProviderConfig"]!.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "加载当前配置文件名失败，使用默认值");
            _currentConfigFileName = "default";
        }
    }

    private async Task UpdateAppSettingsAsync(string key, string value)
    {
        try
        {
            var json = File.Exists(_appSettingsFilePath) ? await File.ReadAllTextAsync(_appSettingsFilePath) : "{}";
            var jObject = JObject.Parse(json);
            jObject["AppSettings"] ??= new JObject();
            jObject["AppSettings"]![key] = value;
            await File.WriteAllTextAsync(_appSettingsFilePath, jObject.ToString(Formatting.Indented));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新 appsettings.json 失败");
        }
    }
}
