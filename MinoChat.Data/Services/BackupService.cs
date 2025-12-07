using System.Text.Json;
using Microsoft.Extensions.Logging;
using MinoChat.Data.Configuration;
using MinoChat.Data.Entities;
using MinoChat.Data.Services.Interfaces;

namespace MinoChat.Data.Services;

/// <summary>
/// 备份和恢复服务实现
/// </summary>
public class BackupService : IBackupService
{
    private readonly IChatDataService _chatDataService;
    private readonly ILogger<BackupService> _logger;
    private readonly string _backupDirectory;

    public BackupService(IChatDataService chatDataService, ILogger<BackupService> logger)
    {
        _chatDataService = chatDataService;
        _logger = logger;

        // 备份目录：程序目录/Data/Backups/
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        _backupDirectory = Path.Combine(baseDirectory, "Data", "Backups");
        Directory.CreateDirectory(_backupDirectory);
    }

    /// <summary>
    /// 备份数据库到指定路径
    /// </summary>
    public async Task<bool> BackupDatabaseAsync(string backupPath)
    {
        try
        {
            _logger.LogInformation("开始备份数据库到: {BackupPath}", backupPath);

            var dbPath = FreeSqlConfig.GetDatabasePath();

            if (!File.Exists(dbPath))
            {
                _logger.LogWarning("数据库文件不存在: {DbPath}", dbPath);
                return false;
            }

            // 确保备份目录存在
            var backupDir = Path.GetDirectoryName(backupPath);
            if (!string.IsNullOrEmpty(backupDir))
            {
                Directory.CreateDirectory(backupDir);
            }

            // 复制数据库文件
            await Task.Run(() => File.Copy(dbPath, backupPath, overwrite: true));

            _logger.LogInformation("数据库备份成功: {BackupPath}", backupPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "数据库备份失败");
            return false;
        }
    }

    /// <summary>
    /// 从备份文件恢复数据库
    /// </summary>
    public async Task<bool> RestoreDatabaseAsync(string backupPath)
    {
        try
        {
            _logger.LogInformation("开始从备份恢复数据库: {BackupPath}", backupPath);

            if (!File.Exists(backupPath))
            {
                _logger.LogWarning("备份文件不存在: {BackupPath}", backupPath);
                return false;
            }

            var dbPath = FreeSqlConfig.GetDatabasePath();

            // 备份当前数据库（如果存在）
            if (File.Exists(dbPath))
            {
                var tempBackup = $"{dbPath}.before_restore_{DateTime.Now:yyyyMMddHHmmss}";
                File.Copy(dbPath, tempBackup, overwrite: true);
                _logger.LogInformation("当前数据库已备份到: {TempBackup}", tempBackup);
            }

            // 恢复数据库
            await Task.Run(() => File.Copy(backupPath, dbPath, overwrite: true));

            _logger.LogInformation("数据库恢复成功");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "数据库恢复失败");
            return false;
        }
    }

    /// <summary>
    /// 导出数据到JSON文件
    /// </summary>
    public async Task<bool> ExportToJsonAsync(string exportPath)
    {
        try
        {
            _logger.LogInformation("开始导出数据到JSON: {ExportPath}", exportPath);

            var sessions = await _chatDataService.LoadChatSessionsAsync();

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var json = JsonSerializer.Serialize(sessions, options);
            await File.WriteAllTextAsync(exportPath, json);

            _logger.LogInformation("数据导出成功，共 {Count} 个会话", sessions.Count());
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出数据到JSON失败");
            return false;
        }
    }

    /// <summary>
    /// 从JSON文件导入数据
    /// </summary>
    public async Task<bool> ImportFromJsonAsync(string jsonPath)
    {
        try
        {
            _logger.LogInformation("开始从JSON导入数据: {JsonPath}", jsonPath);

            if (!File.Exists(jsonPath))
            {
                _logger.LogWarning("JSON文件不存在: {JsonPath}", jsonPath);
                return false;
            }

            var json = await File.ReadAllTextAsync(jsonPath);
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var sessions = JsonSerializer.Deserialize<List<ChatSession>>(json, options);

            if (sessions == null || !sessions.Any())
            {
                _logger.LogWarning("JSON文件中没有数据");
                return false;
            }

            // 批量保存
            await _chatDataService.SaveChatSessionsAsync(sessions);

            _logger.LogInformation("数据导入成功，共 {Count} 个会话", sessions.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从JSON导入数据失败");
            return false;
        }
    }

    /// <summary>
    /// 自动备份（保存到默认备份目录）
    /// </summary>
    public async Task<string?> AutoBackupAsync()
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupFileName = $"minochat_backup_{timestamp}.db";
            var backupPath = Path.Combine(_backupDirectory, backupFileName);

            var success = await BackupDatabaseAsync(backupPath);

            if (success)
            {
                _logger.LogInformation("自动备份完成: {BackupPath}", backupPath);
                return backupPath;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "自动备份失败");
            return null;
        }
    }

    /// <summary>
    /// 获取所有备份文件列表
    /// </summary>
    public async Task<List<(string FileName, DateTime CreateTime)>> GetBackupFilesAsync()
    {
        try
        {
            return await Task.Run(() =>
            {
                if (!Directory.Exists(_backupDirectory))
                    return new List<(string, DateTime)>();

                var files = Directory.GetFiles(_backupDirectory, "*.db")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .Select(f => (f.Name, f.CreationTime))
                    .ToList();

                _logger.LogInformation("找到 {Count} 个备份文件", files.Count);
                return files;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取备份文件列表失败");
            return new List<(string, DateTime)>();
        }
    }

    /// <summary>
    /// 删除指定的备份文件
    /// </summary>
    public async Task<bool> DeleteBackupAsync(string fileName)
    {
        try
        {
            var backupPath = Path.Combine(_backupDirectory, fileName);

            if (!File.Exists(backupPath))
            {
                _logger.LogWarning("备份文件不存在: {BackupPath}", backupPath);
                return false;
            }

            await Task.Run(() => File.Delete(backupPath));

            _logger.LogInformation("备份文件已删除: {FileName}", fileName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除备份文件失败");
            return false;
        }
    }
}
