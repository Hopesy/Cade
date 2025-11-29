using System.Reflection;
using Microsoft.Extensions.Logging;
using MinoChat.Provider.Models;
using MinoChat.Provider.Services.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace MinoChat.Provider.Services;

// 配置文件管理服务实现，服务中的方式是被主项目SettingPage页面的ViewModel所调用
// 配置存储位置：{AppDirectory}/Data/Providers/*.json
// 当前配置跟踪：通过appsettings.json中的AppSettings.CurrentProviderConfig跟记当前使用的配置
// 默认配置处理：首次运行时从嵌入资源 MinoChat.Provider.Resources.default.json 自动提取默认配置
public class ProviderConfigService : IProviderConfigService
{
    private readonly ILogger<ProviderConfigService> _logger;
    private readonly string _configDirectory;
    private readonly string _appSettingsFilePath;
    private string _currentConfigFileName = "default";
    public ProviderConfigService(ILogger<ProviderConfigService> logger)
    {
        _logger = logger;
        // 配置文件目录：{AppDirectory}/Data/Providers
        var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        _configDirectory = Path.Combine(appDirectory, "Data", "Providers");
        // appsettings.json 文件路径
        _appSettingsFilePath = Path.Combine(appDirectory, "appsettings.json");
        // 确保目录存在
        Directory.CreateDirectory(_configDirectory);
        // 从 appsettings.json 加载当前配置文件名
        LoadCurrentConfigFileName();
    }
    public async Task<List<string>> GetAllConfigFilesAsync()
    {
        try
        {
            await EnsureDefaultConfigExistsAsync();
            var files = Directory.GetFiles(_configDirectory, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => !string.IsNullOrEmpty(name))
                .ToList();
            return files!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取配置文件列表失败");
            return new List<string>();
        }
    }
    // 加载指定名称的配置文件，返回ProviderConfig对象
    public async Task<ProviderConfig?> LoadConfigurationAsync(string fileName)
    {
        try
        {
            var filePath = Path.Combine(_configDirectory, $"{fileName}.json");
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("配置文件不存在: {FileName}", fileName);
                return null;
            }
            var json = await File.ReadAllTextAsync(filePath);
            // 将json配置文件反序列化为配置对象
            var config = JsonConvert.DeserializeObject<ProviderConfig>(json);
            _logger.LogInformation("成功加载配置文件: {FileName}", fileName);
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载配置文件失败: {FileName}", fileName);
            return null;
        }
    }
    // 保存配置到JSON文件，将用户修改后的ProviderConfig对象写入文件，
    public async Task SaveConfigurationAsync(ProviderConfig configuration, string fileName)
    {
        try
        {
            var filePath = Path.Combine(_configDirectory, $"{fileName}.json");
            var json = JsonConvert.SerializeObject(configuration, Formatting.Indented);
            await File.WriteAllTextAsync(filePath, json);
            _logger.LogInformation("成功保存配置文件: {FileName}", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存配置文件失败: {FileName}", fileName);
            throw;
        }
    }
    // 创建新的提供商配置(默认为OpenAICompatible类型)
    // 对应SettingPage的保存配置操作，内部调用SaveConfigurationAsync
    public async Task<ProviderConfig> CreateNewConfigAsync(string fileName)
    {
        try
        {
            var newConfig = new ProviderConfig
            {
                Type = ProviderType.OpenAICompatible,
                ApiKey = "your-api-key-here",
                BaseUrl = null,
                ModelIds = "gpt-4,gpt-4o",
                IsEnabled = true
            };
            await SaveConfigurationAsync(newConfig, fileName);
            _logger.LogInformation("成功创建新配置文件: {FileName}", fileName);
            return newConfig;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建配置文件失败: {FileName}", fileName);
            throw;
        }
    }
    // 删除配置文件(保护默认配置不被删除)
    public async Task DeleteConfigAsync(string fileName)
    {
        try
        {
            // 不允许删除default.json配置文件
            if (fileName.Equals("default", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("不能删除默认配置文件");
            }
            var filePath = Path.Combine(_configDirectory, $"{fileName}.json");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("成功删除配置文件: {FileName}", fileName);
                // 如果删除的是当前配置，切换回默认配置:更新appsetting.json中的配置项
                if (_currentConfigFileName.Equals(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    await SetCurrentConfigFileNameAsync("default");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除配置文件失败: {FileName}", fileName);
            throw;
        }
    }
    // 验证配置的完整性和正确性
    public Task<string?> ValidateProviderConfigAsync(ProviderConfig providerConfig)
    {
        try
        {
            // 基本验证
            if (string.IsNullOrWhiteSpace(providerConfig.ApiKey))
            {
                return Task.FromResult<string?>("API密钥不能为空");
            }
            // 验证模型ID
            if (string.IsNullOrWhiteSpace(providerConfig.ModelIds))
            {
                return Task.FromResult<string?>("模型ID不能为空");
            }
            // 兼容服务必须提供 BaseUrl
            if ((providerConfig.Type == ProviderType.AnthropicCompatible ||
                 providerConfig.Type == ProviderType.OpenAICompatible) &&
                string.IsNullOrWhiteSpace(providerConfig.BaseUrl))
            {
                return Task.FromResult<string?>("兼容服务必须提供BaseUrl");
            }
            // 官方服务不应该有 BaseUrl
            if ((providerConfig.Type == ProviderType.Anthropic ||
                 providerConfig.Type == ProviderType.OpenAI) &&
                !string.IsNullOrWhiteSpace(providerConfig.BaseUrl))
            {
                _logger.LogWarning("官方服务不需要BaseUrl，将被忽略");
            }
            _logger.LogInformation("配置验证通过: {ProviderType}", providerConfig.Type);
            return Task.FromResult<string?>(null); // null 表示验证成功
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "配置验证失败: {ProviderType}", providerConfig.Type);
            return Task.FromResult<string?>($"验证失败: {ex.Message}");
        }
    }
    // 确保default.json配置文件存在，如果不存在手动从嵌入的资源中提取
    public async Task EnsureDefaultConfigExistsAsync()
    {
        try
        {
            var defaultConfigPath = Path.Combine(_configDirectory, "default.json");
            // 如果默认配置已存在，则不需要提取
            if (File.Exists(defaultConfigPath))
            {
                return;
            }
            _logger.LogInformation("默认配置不存在，正在从嵌入资源中提取...");
            // 从嵌入资源中读取 default.json
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "MinoChat.Provider.Resources.default.json";
            await using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                _logger.LogError("无法找到嵌入资源: {ResourceName}", resourceName);
                throw new FileNotFoundException($"嵌入资源不存在: {resourceName}");
            }
            // 读取并写入到文件
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();
            await File.WriteAllTextAsync(defaultConfigPath, content);
            _logger.LogInformation("成功提取默认配置文件");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "确保默认配置存在时发生错误");
            throw;
        }
    }
    public string GetCurrentConfigFileName()
    {
        return _currentConfigFileName;
    }
    public async Task SetCurrentConfigFileNameAsync(string fileName)
    {
        try
        {
            _currentConfigFileName = fileName;

            // 保存到 appsettings.json
            await UpdateAppSettingsAsync("CurrentProviderConfig", fileName);

            _logger.LogInformation("已切换到配置文件: {FileName}", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置当前配置文件名失败: {FileName}", fileName);
            throw;
        }
    }
    // 从appsettings.json加载当前配置文件名(直接读取的文件)
    private void LoadCurrentConfigFileName()
    {
        try
        {
            if (File.Exists(_appSettingsFilePath))
            {
                var json = File.ReadAllText(_appSettingsFilePath);
                var jObject = JObject.Parse(json);
                var appSettings = jObject["AppSettings"];
                if (appSettings != null && appSettings["CurrentProviderConfig"] != null)
                {
                    _currentConfigFileName = appSettings["CurrentProviderConfig"]!.ToString();
                    _logger.LogInformation("从appsettings.json加载当前使用的配置文件名: {FileName}", _currentConfigFileName);
                }
                else
                {
                    _logger.LogInformation("appsettings.json 中未找到 CurrentProviderConfig，使用默认值");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "从 appsettings.json 加载当前配置文件名失败，使用默认值");
            _currentConfigFileName = "default";
        }
    }
    // 更新appsettings.json中的配置项
    private async Task UpdateAppSettingsAsync(string key, string value)
    {
        try
        {
            // 读取或创建JSON内容
            var json = File.Exists(_appSettingsFilePath) ? await File.ReadAllTextAsync(_appSettingsFilePath) : "{}";
            var jObject = JObject.Parse(json);
            // 确保AppSettings节点存在
            jObject["AppSettings"] ??= new JObject();
            // 更新指定的键值对
            jObject["AppSettings"]![key] = value;
            // 写回文件
            await File.WriteAllTextAsync(_appSettingsFilePath, jObject.ToString(Formatting.Indented));
            _logger.LogInformation("成功更新appsettings.json: {Key} = {Value}", key, value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新 appsettings.json 失败");
            throw;
        }
    }
}
