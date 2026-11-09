using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AITrainingDemo.Api.Dtos;
using Microsoft.IdentityModel.Tokens;

namespace AITrainingDemo.Tests;

/// <summary>
/// 覆盖两条容易被后续改动静默破坏的修复：
/// ① 并发同名创建必须有一个成功、其余 409（不能冒成 500）；
/// ② 非法 / 缺失的 sub 声明必须被拒成 401（不能冒成 500）。
/// </summary>
public class ConcurrencyAndClaimTests : IClassFixture<FileAppFactory>
{
    private readonly FileAppFactory _factory;

    public ConcurrencyAndClaimTests(FileAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateUser_ConcurrentlyWithSameUsername_OneSucceedsAndOthersConflict()
    {
        var client = await CreateAuthenticatedClientAsync();
        var username = "race" + Guid.NewGuid().ToString("N")[..8];

        var tasks = Enumerable.Range(0, 4)
            .Select(_ => client.PostAsJsonAsync("/api/users", new CreateUserRequest(
                username, "Test@123456", "并发用户", null, null, true)))
            .ToArray();

        var responses = await Task.WhenAll(tasks);
        var statuses = responses.Select(r => r.StatusCode).ToArray();

        Assert.Equal(1, statuses.Count(s => s == HttpStatusCode.OK));
        Assert.Equal(3, statuses.Count(s => s == HttpStatusCode.Conflict));
        // 关键回归点：并发冲突绝不能变成 500
        Assert.DoesNotContain(HttpStatusCode.InternalServerError, statuses);
    }

    [Fact]
    public async Task Request_WithNonNumericSubClaim_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken("not-a-number"));

        var response = await client.DeleteAsync("/api/users/999999");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_WithoutSubClaim_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(sub: null));

        var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_WithUnknownUserId_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken("999999"));

        var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>用与测试宿主相同的密钥自签一个 token，用于构造非法身份声明。</summary>
    private static string CreateToken(string? sub)
    {
        var claims = new List<Claim> { new("name", "probe") };
        if (sub is not null)
        {
            claims.Add(new Claim("sub", sub));
        }

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("test-signing-key-012345678901234567890123456789"));

        var token = new JwtSecurityToken(
            issuer: "AITrainingDemo",
            audience: "AITrainingDemo.Client",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "admin123"));
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("data").GetProperty("token").GetString());
        return client;
    }
}
