using AITrainingDemo.Api.Entities;
using AITrainingDemo.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AITrainingDemo.Api.Data;

public static class DbInitializer
{
    public static void Initialize(AppDbContext db, IPasswordHasher hasher)
    {
        // SQLite 会自动创建数据库文件，但不会创建目录
        var dataSource = db.Database.GetConnectionString();
        if (!string.IsNullOrWhiteSpace(dataSource))
        {
            var builder = new SqliteConnectionStringBuilder(dataSource);
            if (!string.IsNullOrWhiteSpace(builder.DataSource) && builder.DataSource != ":memory:")
            {
                var directory = Path.GetDirectoryName(builder.DataSource);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
        }

        db.Database.EnsureCreated();
        Seed(db, hasher);
    }

    public static void Seed(AppDbContext db, IPasswordHasher hasher)
    {
        var adminRole = db.Roles.FirstOrDefault(r => r.Code == "admin");
        if (adminRole is null)
        {
            adminRole = new Role { Code = "admin", Name = "管理员", Description = "系统管理员", CreatedAt = DateTime.UtcNow };
            db.Roles.Add(adminRole);
        }

        var editorRole = db.Roles.FirstOrDefault(r => r.Code == "editor");
        if (editorRole is null)
        {
            editorRole = new Role { Code = "editor", Name = "编辑者", Description = "普通编辑", CreatedAt = DateTime.UtcNow };
            db.Roles.Add(editorRole);
        }

        db.SaveChanges();

        if (!db.Users.Any(u => u.Username == "admin"))
        {
            db.Users.Add(new User
            {
                Username = "admin",
                PasswordHash = hasher.Hash("admin123"),
                DisplayName = "管理员",
                Email = "admin@example.com",
                RoleId = adminRole.Id,
                Enabled = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (!db.Users.Any(u => u.Username == "editor"))
        {
            db.Users.Add(new User
            {
                Username = "editor",
                PasswordHash = hasher.Hash("editor123"),
                DisplayName = "编辑者",
                Email = "editor@example.com",
                RoleId = editorRole.Id,
                Enabled = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        db.SaveChanges();
    }
}
