using Cade.Provider.Mcp;
using Cade.Provider.Models;
using Cade.Provider.Services.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cade.Provider.Services;

/// <summary>
/// MCP 服务器加载服务
/// </summary>
public class McpLoaderService : IHostedService
{
    private readonly IProviderService _providerService;
    private readonly McpBridge _mcpBridge;
    private readonly McpSettings _mcpSettings;
    private readonly ILogger<McpLoaderService> _logger;

    public McpLoaderService(
        IProviderService providerService,
        McpBridge mcpBridge,
        IOptions<McpSettings> mcpSettings,
        ILogger<McpLoaderService> logger)
    {
        _providerService = providerService;
        _mcpBridge = mcpBridge;
        _mcpSettings = mcpSettings.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var enabledServers = _mcpSettings.McpServers
            .Where(s => !s.Value.Disabled)
            .ToList();

        if (enabledServers.Count == 0)
        {
            _logger.LogInformation("未配置 MCP Servers");
            return;
        }

        foreach (var (name, config) in enabledServers)
        {
            try
            {
                _logger.LogInformation("正在连接 MCP: {Name}", name);
                var plugin = await _mcpBridge.ConnectAsync(
                    name, 
                    config.Command, 
                    config.Args, 
                    config.Env, 
                    cancellationToken);
                
                _providerService.AddPlugin(plugin);
                _logger.LogInformation("MCP: {Name} 已连接 ({Count} 个工具)", name, plugin.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MCP: {Name} 连接失败", name);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) 
        => _mcpBridge.DisposeAsync().AsTask();
}
