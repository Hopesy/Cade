using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Client;
using System.Text.Json;

namespace Cade.Provider.Mcp;

/// <summary>
/// MCP 到 Semantic Kernel 的桥接器
/// 将 MCP Server 的工具自动注册为 SK 插件
/// </summary>
public class McpBridge : IAsyncDisposable
{
    private readonly ILogger<McpBridge> _logger;
    private readonly List<McpClient> _clients = [];

    public McpBridge(ILogger<McpBridge> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 连接到 MCP Server 并返回可注册到 Kernel 的插件
    /// </summary>
    public async Task<KernelPlugin> ConnectAsync(
        string serverName,
        string command,
        string[]? args = null,
        Dictionary<string, string>? env = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("正在连接 MCP Server: {ServerName} ({Command})", serverName, command);

        var client = await McpClient.CreateAsync(
            new StdioClientTransport(new StdioClientTransportOptions
            {
                Command = command,
                Arguments = args ?? [],
                EnvironmentVariables = env?.ToDictionary(k => k.Key, v => (string?)v.Value)
            }),
            new McpClientOptions
            {
                ClientInfo = new() { Name = "Cade", Version = "1.0.0" }
            },
            cancellationToken: cancellationToken);

        _clients.Add(client);

        var tools = await client.ListToolsAsync();
        _logger.LogInformation("MCP Server {ServerName} 提供了 {Count} 个工具", serverName, tools.Count);

        var functions = new List<KernelFunction>();
        foreach (var tool in tools)
        {
            var function = CreateKernelFunction(client, tool);
            functions.Add(function);
            _logger.LogDebug("  - {ToolName}: {Description}", tool.Name, tool.Description);
        }

        return KernelPluginFactory.CreateFromFunctions(serverName, functions);
    }

    private KernelFunction CreateKernelFunction(McpClient client, McpClientTool tool)
    {
        async Task<string> InvokeToolAsync(Kernel kernel, KernelArguments arguments)
        {
            var mcpArgs = new Dictionary<string, object?>();
            foreach (var arg in arguments)
            {
                mcpArgs[arg.Key] = arg.Value;
            }

            _logger.LogInformation("[MCP调用] {ToolName} - 参数: {Args}", tool.Name, JsonSerializer.Serialize(mcpArgs));

            try
            {
                var result = await client.CallToolAsync(tool.Name, mcpArgs);

                var textParts = new List<string>();
                foreach (var content in result.Content)
                {
                    var json = JsonSerializer.Serialize(content);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("text", out var textElement))
                    {
                        textParts.Add(textElement.GetString() ?? "");
                    }
                }
                var textContent = string.Join("\n", textParts);

                _logger.LogInformation("[MCP调用] {ToolName} - 完成, 结果长度: {Length}", tool.Name, textContent.Length);
                return textContent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MCP调用] {ToolName} - 失败", tool.Name);
                return $"调用失败: {ex.Message}";
            }
        }

        var parameters = new List<KernelParameterMetadata>();
        var jsonSchema = tool.JsonSchema;

        if (jsonSchema.TryGetProperty("properties", out var props))
        {
            var required = jsonSchema.TryGetProperty("required", out var reqArray)
                ? reqArray.EnumerateArray().Select(e => e.GetString()).ToHashSet()
                : new HashSet<string?>();

            foreach (var prop in props.EnumerateObject())
            {
                var description = prop.Value.TryGetProperty("description", out var descElement)
                    ? descElement.GetString() ?? ""
                    : "";

                parameters.Add(new KernelParameterMetadata(prop.Name)
                {
                    Description = description,
                    IsRequired = required.Contains(prop.Name),
                    ParameterType = typeof(string)
                });
            }
        }

        return KernelFunctionFactory.CreateFromMethod(
            InvokeToolAsync,
            tool.Name,
            tool.Description,
            parameters,
            new KernelReturnParameterMetadata { Description = "工具执行结果" }
        );
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients)
        {
            await client.DisposeAsync();
        }
        _clients.Clear();
    }
}
