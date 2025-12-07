using FreeSql;
using Microsoft.Extensions.Logging;
using Cade.Data.Entities;

namespace Cade.Data.Configuration;

/// <summary>
/// FreeSql 配置
/// </summary>
public static class FreeSqlConfig
{
    /// <summary>
    /// 创建 FreeSql 实例
    /// 数据库存放在 ~/.cade/data/cade.db
    /// </summary>
    public static IFreeSql CreateInstance(ILogger? logger = null)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var dataDirectory = Path.Combine(userProfile, ".cade", "data");
        Directory.CreateDirectory(dataDirectory);
        
        var dbPath = Path.Combine(dataDirectory, "cade.db");
        var connectionString = $"Data Source={dbPath};Pooling=true;Max Pool Size=10;";
        
        logger?.LogInformation("初始化 FreeSql，数据库路径: {DbPath}", dbPath);

        var freeSql = new FreeSqlBuilder()
            .UseConnectionString(DataType.Sqlite, connectionString)
            .UseAutoSyncStructure(true)
            .UseNoneCommandParameter(true)
            .Build();

        // 同步实体结构
        freeSql.CodeFirst.SyncStructure(
            typeof(ChatSession),
            typeof(ChatMessage)
        );

        logger?.LogInformation("FreeSql 初始化完成");
        return freeSql;
    }
}
