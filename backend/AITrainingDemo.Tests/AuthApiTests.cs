using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AITrainingDemo.Api.Data;
using AITrainingDemo.Api.Dtos;
using AITrainingDemo.Api.Entities;
using AITrainingDemo.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AITrainingDemo.Tests;

public class AuthApiTests(AppFactory factory) : IClassFixture<AppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Login_WithCorrectCredentials_ReturnsToken()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "admin123"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("code").GetInt32());
        var token = body.GetProperty("data").GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public async Task Login_WithWrongPassword_And_WithUnknownUser_ReturnSame401()
    {
        var wrongPassword = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "wrong"));
        var unknownUser = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("nobody", "wrong"));

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownUser.StatusCode);

        var m1 = await wrongPassword.Content.ReadAsStringAsync();
        var m2 = await unknownUser.Content.ReadAsStringAsync();
        Assert.Equal(m1, m2);
    }

    [Fact]
    public async Task Login_NeverReturnsPasswordHash()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "admin123"));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var data = body.GetProperty("data");

        // 断言 data 的字段集合恰好是预期的那几个，避免「换个字段名就漏检」
        var actualFields = data.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "expiresAt", "token", "user" }, actualFields);

        var userFields = data.GetProperty("user").EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "displayName", "id", "roleName", "username" }, userFields);
    }

    [Fact]
    public async Task Login_WithEmptyPassword_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", ""));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithEmptyPassword_Returns401_And_WithNullPassword_Returns400()
    {
        var emptyPassword = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", ""));
        var nullPassword = await _client.PostAsJsonAsync("/api/auth/login", new { username = "admin", password = (string?)null });

        Assert.Equal(HttpStatusCode.Unauthorized, emptyPassword.StatusCode);
        // null 口令在模型绑定阶段就被拦下（400），不会走到 Service，因此也不会是 500
        Assert.Equal(HttpStatusCode.BadRequest, nullPassword.StatusCode);
    }

    [Fact]
    public async Task Login_WithDisabledUser_ReturnsSame401Message()
    {
        try
        {
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var editor = await db.Users.FirstAsync(u => u.Username == "editor");
                editor.Enabled = false;
                await db.SaveChangesAsync();
            }

            var disabled = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("editor", "editor123"));
            var wrongPassword = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "wrong"));

            Assert.Equal(HttpStatusCode.Unauthorized, disabled.StatusCode);
            Assert.Equal(
                await wrongPassword.Content.ReadAsStringAsync(),
                await disabled.Content.ReadAsStringAsync());
        }
        finally
        {
            // 还原种子账号，避免影响同fixture下的其它用例
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var editor = await db.Users.FirstAsync(u => u.Username == "editor");
            editor.Enabled = true;
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// 未认证时由授权中间件直接返回 401（不经过异常过滤器），
    /// 响应体仍应保持 ApiResult 形状，避免前端拿到空 body。
    /// </summary>
    [Fact]
    public async Task Me_WithoutToken_Returns401WithUnifiedApiResultBody()
    {
        var response = await _client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(401, body.GetProperty("code").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("message").GetString()));
        Assert.True(body.TryGetProperty("data", out _));
    }

    [Fact]
    public async Task Me_WithGarbageToken_Returns401WithUnifiedApiResultBody()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-real-token");

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(401, body.GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task Me_WithGarbageToken_Returns401()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-real-token");

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithValidToken_ReturnsCurrentUser()
    {
        var token = await LoginAsync();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("admin", body.GetProperty("data").GetProperty("username").GetString());
    }

    /// <summary>
    /// Service 抛出 BusinessException(404) 后，应由 ApiExceptionFilter 转成
    /// HTTP 404，且响应体保持 ApiResult 形状（code / message / data）。
    /// </summary>
    [Fact]
    public async Task BusinessException_MapsToHttpStatusCodeAndApiResultBody()
    {
        var client = await CreateAuthenticatedClientAsync();

        // 删除不存在的用户，Service 层抛出 BusinessException(404, "用户不存在")
        var response = await client.DeleteAsync("/api/users/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(404, body.GetProperty("code").GetInt32());
        Assert.Equal("用户不存在", body.GetProperty("message").GetString());
        Assert.True(body.TryGetProperty("data", out _));
    }

    /// <summary>
    /// 用户被停用后，停用之前签发的 token 应立即失效（否则「停用」开关形同虚设）。
    /// </summary>
    [Fact]
    public async Task Token_IssuedBeforeUserDisabled_IsRejected()
    {
        var client = await CreateAuthenticatedClientAsync("editor", "editor123");

        try
        {
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var editor = await db.Users.FirstAsync(u => u.Username == "editor");
                editor.Enabled = false;
                await db.SaveChangesAsync();
            }

            var response = await client.GetAsync("/api/auth/me");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var editor = await db.Users.FirstAsync(u => u.Username == "editor");
            editor.Enabled = true;
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// 用户被删除后，已签发的 token 应立即失效。
    /// </summary>
    [Fact]
    public async Task Token_IssuedBeforeUserDeleted_IsRejected()
    {
        var username = "tmp-del-" + Guid.NewGuid().ToString("N");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            db.Users.Add(new User
            {
                Username = username,
                PasswordHash = hasher.Hash("tmp-pass"),
                DisplayName = "待删除用户",
                Enabled = true,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var token = await LoginAsync(username, "tmp-pass");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Username == username);
            db.Users.Remove(user);
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<string> LoginAsync()
    {
        return await LoginAsync("admin", "admin123");
    }

    private async Task<string> LoginAsync(string username, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("data").GetProperty("token").GetString()!;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string username = "admin", string password = "admin123")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await LoginAsync(username, password));
        return client;
    }
}
