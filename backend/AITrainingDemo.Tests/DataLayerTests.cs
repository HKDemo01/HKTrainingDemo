using AITrainingDemo.Api.Data;
using AITrainingDemo.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AITrainingDemo.Tests;

public class DataLayerTests
{
    [Fact]
    public async Task Initialize_CreatesDatabaseAndSeedsUsersAndRoles()
    {
        using var factory = new AppFactory();
        using var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.True(await db.Users.AnyAsync(u => u.Username == "admin"));
        Assert.True(await db.Users.AnyAsync(u => u.Username == "editor"));
        Assert.True(await db.Roles.AnyAsync(r => r.Code == "admin"));
        Assert.True(await db.Roles.AnyAsync(r => r.Code == "editor"));
    }

    [Fact]
    public async Task Seed_IsIdempotent_WhenCalledTwice()
    {
        using var factory = new AppFactory();
        using var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = new PasswordHasher();

        DbInitializer.Seed(db, hasher);
        DbInitializer.Seed(db, hasher);

        Assert.Equal(2, await db.Users.CountAsync());
        Assert.Equal(2, await db.Roles.CountAsync());
    }

    [Fact]
    public async Task SeededAdmin_CanBeVerified_WithSeedCredentials()
    {
        using var factory = new AppFactory();
        using var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var admin = await db.Users.FirstOrDefaultAsync(u => u.Username == "admin");

        Assert.NotNull(admin);
        Assert.True(hasher.Verify("admin123", admin!.PasswordHash), "种子账号 admin 的口令校验未通过");
    }

    [Fact]
    public void PasswordHasher_Verify_ReturnsTrueForSamePassword_AndFalseForOthers()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.Hash("admin123");

        Assert.True(hasher.Verify("admin123", hash));
        Assert.False(hasher.Verify("admin124", hash));
    }

    [Fact]
    public void PasswordHasher_Verify_ReturnsFalse_ForMalformedStoredHash()
    {
        var hasher = new PasswordHasher();

        // 畸形哈希一律判定为失败，不能让任意口令通过校验
        Assert.False(hasher.Verify("admin123", "c2FsdA==."));
        Assert.False(hasher.Verify("admin123", ".aGFzaA=="));
        Assert.False(hasher.Verify("admin123", "."));
        Assert.False(hasher.Verify("任意其他口令", "c2FsdA==."));
    }

    [Fact]
    public void PasswordHasher_ProducesDifferentHashes_ForSamePassword()
    {
        var hasher = new PasswordHasher();

        Assert.NotEqual(hasher.Hash("admin123"), hasher.Hash("admin123"));
    }
}
