using AITrainingDemo.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AITrainingDemo.Tests;

/// <summary>
/// 基于文件型 SQLite 的测试宿主。
/// 并发场景必须使用它：<see cref="AppFactory"/> 的所有请求共用同一个内存连接，
/// 该连接非线程安全，并发写会偶发抛出与业务无关的 SQLite 异常（属测试工装限制，
/// 非产品缺陷）；文件库的每个请求各自开连接，才能真实复现生产并发行为。
/// </summary>
public class FileAppFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(),
        $"workbench-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("Jwt:Issuer", "AITrainingDemo");
        builder.UseSetting("Jwt:Audience", "AITrainingDemo.Client");
        builder.UseSetting("Jwt:SigningKey", "test-signing-key-012345678901234567890123456789");
        builder.UseSetting("Cors:AllowedOrigins", "http://localhost:5173");

        var connectionString = $"Data Source={_dbPath}";

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        // 连接池里可能还持有该文件的句柄，先清池再删文件，否则文件删不掉
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var path = _dbPath + suffix;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
