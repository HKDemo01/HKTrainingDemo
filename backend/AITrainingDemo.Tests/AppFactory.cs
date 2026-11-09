using AITrainingDemo.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace AITrainingDemo.Tests;

public class AppFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public AppFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // 直接注入配置，避免依赖 appsettings.json 在测试进程中的定位
        builder.UseSetting("Jwt:Issuer", "AITrainingDemo");
        builder.UseSetting("Jwt:Audience", "AITrainingDemo.Client");
        builder.UseSetting("Jwt:SigningKey", "test-signing-key-012345678901234567890123456789");
        builder.UseSetting("Cors:AllowedOrigins", "http://localhost:5173");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Data Source=:memory:");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
