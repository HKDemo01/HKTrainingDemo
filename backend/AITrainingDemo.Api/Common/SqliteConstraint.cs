using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AITrainingDemo.Api.Common;

/// <summary>
/// 识别 SQLite 的约束冲突。
/// 优先按扩展错误码判别 —— SQLite 的异常消息文案会随版本变化，不能只靠字符串匹配；
/// 拿不到扩展错误码时才退回到消息匹配作为兜底。
/// </summary>
public static class SqliteConstraint
{
    /// <summary>SQLITE_CONSTRAINT_UNIQUE：唯一索引冲突。</summary>
    private const int ExtendedUnique = 2067;

    /// <summary>SQLITE_CONSTRAINT_PRIMARYKEY：主键冲突。</summary>
    private const int ExtendedPrimaryKey = 1555;

    /// <summary>SQLITE_CONSTRAINT_FOREIGNKEY：外键约束冲突。</summary>
    private const int ExtendedForeignKey = 787;

    /// <summary>是否命中唯一索引约束。</summary>
    public static bool IsUnique(DbUpdateException exception)
    {
        return Match(exception, [ExtendedUnique, ExtendedPrimaryKey], "UNIQUE constraint failed");
    }

    /// <summary>是否命中外键约束。</summary>
    public static bool IsForeignKey(DbUpdateException exception)
    {
        return Match(exception, [ExtendedForeignKey], "FOREIGN KEY constraint failed");
    }

    private static bool Match(DbUpdateException exception, int[] extendedCodes, string messageFragment)
    {
        if (exception.InnerException is SqliteException sqlite
            && Array.IndexOf(extendedCodes, sqlite.SqliteExtendedErrorCode) >= 0)
        {
            return true;
        }

        return exception.InnerException?.Message.Contains(messageFragment, StringComparison.OrdinalIgnoreCase) == true;
    }
}
