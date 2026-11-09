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

        var db = factory.Services.GetRequiredService<AppDbContext>();

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

        var db = factory.Services.GetRequiredService<AppDbContext>();
        var hasher = new PasswordHasher();

        DbInitializer.Seed(db, hasher);
        DbInitializer.Seed(db, hasher);

        Assert.Equal(2, await db.Users.CountAsync());
        Assert.Equal(2, await db.Roles.CountAsync());
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
    public void PasswordHasher_ProducesDifferentHashes_ForSamePassword()
    {
        var hasher = new PasswordHasher();

        Assert.NotEqual(hasher.Hash("admin123"), hasher.Hash("admin123"));
    }
}
