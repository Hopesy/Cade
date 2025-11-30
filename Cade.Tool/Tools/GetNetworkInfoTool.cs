using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace Cade.Tool.Tools;

/// <summary>
/// 获取网络信息工具
/// </summary>
public class GetNetworkInfoTool : ToolBase
{
    public override string Name => "get_network_info";
    public override string Description => "获取当前系统的网络信息";

    public override Task<ToolResult> ExecuteAsync(string parameters)
    {
        return SafeExecuteAsync(async () =>
        {
            await Task.CompletedTask; // 保持异步接口一致性

            var sb = new StringBuilder();
            sb.AppendLine("=== 网络接口信息 ===");

            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var iface in interfaces.Where(i => i.OperationalStatus == OperationalStatus.Up))
            {
                sb.AppendLine($"\n接口名称: {iface.Name}");
                sb.AppendLine($"  类型: {iface.NetworkInterfaceType}");
                sb.AppendLine($"  状态: {iface.OperationalStatus}");
                sb.AppendLine($"  速度: {iface.Speed / 1_000_000} Mbps");

                var ipProps = iface.GetIPProperties();
                foreach (var ip in ipProps.UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        sb.AppendLine($"  IPv4 地址: {ip.Address}");
                    }
                    else if (ip.Address.AddressFamily == AddressFamily.InterNetworkV6)
                    {
                        sb.AppendLine($"  IPv6 地址: {ip.Address}");
                    }
                }
            }

            return ToolResult.CreateSuccess(sb.ToString());
        });
    }
}
