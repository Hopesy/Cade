namespace MinoChat.Data.Services.Interfaces;

/// <summary>
/// 备份和恢复服务接口
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// 备份数据库到指定路径
    /// </summary>
    /// <param name="backupPath">备份文件路径（完整路径，包含文件名）</param>
    /// <returns>是否成功</returns>
    Task<bool> BackupDatabaseAsync(string backupPath);

    /// <summary>
    /// 从备份文件恢复数据库
    /// </summary>
    /// <param name="backupPath">备份文件路径</param>
    /// <returns>是否成功</returns>
    Task<bool> RestoreDatabaseAsync(string backupPath);

    /// <summary>
    /// 导出数据到JSON文件（用于兼容性和数据交换）
    /// </summary>
    /// <param name="exportPath">导出文件路径</param>
    /// <returns>是否成功</returns>
    Task<bool> ExportToJsonAsync(string exportPath);

    /// <summary>
    /// 从JSON文件导入数据
    /// </summary>
    /// <param name="jsonPath">JSON文件路径</param>
    /// <returns>是否成功</returns>
    Task<bool> ImportFromJsonAsync(string jsonPath);

    /// <summary>
    /// 自动备份（保存到默认备份目录）
    /// </summary>
    /// <returns>备份文件路径</returns>
    Task<string?> AutoBackupAsync();

    /// <summary>
    /// 获取所有备份文件列表
    /// </summary>
    /// <returns>备份文件信息列表（文件名和创建时间）</returns>
    Task<List<(string FileName, DateTime CreateTime)>> GetBackupFilesAsync();

    /// <summary>
    /// 删除指定的备份文件
    /// </summary>
    /// <param name="fileName">备份文件名</param>
    /// <returns>是否成功</returns>
    Task<bool> DeleteBackupAsync(string fileName);
}
