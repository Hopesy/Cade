using FreeSql;
using Microsoft.Extensions.Logging;
using MinoChat.Data.Entities;

namespace MinoChat.Data.Configuration;

// FreeSql配置类
/*
 * builder.Services.AddFreeSql(options =>
   {
     options.UseConnectionString(DataType.Sqlite, connectionString)
           .UseAutoSyncStructure(true) // 自动同步结构（开发时方便）
           .UseLazyLoading(true);
           // .UseMonitorCommand(cmd => Console.WriteLine(cmd.CommandText)); // 监听SQL
   });
*/

public static class FreeSqlConfig
{
    // DI容器负责单例管理，创建FreeSql实例
    public static IFreeSql CreateInstance(ILogger? logger = null)
    {
        // 确定数据库文件路径：程序目录/Data/minochat.db
        var dataDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        Directory.CreateDirectory(dataDirectory); // 确保目录存在
        var dbPath = Path.Combine(dataDirectory, "minochat.db");
        var connectionString = $"Data Source={dbPath};Pooling=true;Max Pool Size=10;";
        logger?.LogInformation("初始化FreeSql，数据库路径: {DbPath}", dbPath);

        // 创建FreeSql实例
        var freeSql = new FreeSqlBuilder()
            .UseConnectionString(DataType.Sqlite, connectionString)
            .UseAutoSyncStructure(true) // 自动同步实体结构到数据库（CodeFirst）
            .UseNoneCommandParameter(true) // 不使用参数化查询（SQLite 推荐）
            .Build();

        // 配置日志输出（如果提供了 logger）
        if (logger != null)
        {
            freeSql.Aop.CurdBefore += (s, e) =>
            {
                logger.LogDebug("执行了SQL: {Sql}", e.Sql);
            };
        }
        // 同步实体结构（自动建表）
        freeSql.CodeFirst.SyncStructure(
            typeof(ChatSession),
            typeof(ChatMessage)
        );

        logger?.LogInformation("FreeSql初始化完成，数据库表结构已同步");

        return freeSql;
    }
    // 获取数据库文件完整路径
    public static string GetDatabasePath()
    {
        var dataDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        return Path.Combine(dataDirectory, "minochat.db");
    }
}
