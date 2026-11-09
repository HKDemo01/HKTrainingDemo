using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AITrainingDemo.Api.Dtos;

namespace AITrainingDemo.Tests;

public class UserApiTests(AppFactory factory) : IClassFixture<AppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetUsers_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUsers_ReturnsPagedResult()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/users?page=1&pageSize=10");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, body.GetProperty("code").GetInt32());
        var data = body.GetProperty("data");
        Assert.True(data.GetProperty("total").GetInt32() >= 2);
        Assert.Equal(10, data.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task GetUsers_FiltersByKeyword()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/users?keyword=editor");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("data").GetProperty("items");

        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal("editor", items[0].GetProperty("username").GetString());
    }

    [Fact]
    public async Task CreateUser_Succeeds_AndNeverLeaksPasswordHash()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/users", new CreateUserRequest(
            "tester01", "Test@123456", "测试用户", "tester@example.com", null, true));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("passwordHash", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Test@123456", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateUser_WithDuplicateUsername_Returns409()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/users", new CreateUserRequest(
            "admin", "Test@123456", "重复用户", null, null, true));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateUser_WithUnknownRoleId_Returns400()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/users", new CreateUserRequest(
            "tester02", "Test@123456", "无角色用户", null, 99999, true));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_ChangesFields_AndKeepsPasswordWhenOmitted()
    {
        var client = await CreateAuthenticatedClientAsync();
        var created = await CreateUserAsync(client, "updateme01");

        var response = await client.PutAsJsonAsync($"/api/users/{created}", new UpdateUserRequest(
            null, "改名后", "changed@example.com", null, false));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var data = body.GetProperty("data");
        Assert.Equal("改名后", data.GetProperty("displayName").GetString());
        Assert.False(data.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task DeleteUser_Succeeds()
    {
        var client = await CreateAuthenticatedClientAsync();
        var created = await CreateUserAsync(client, "deleteme01");

        var response = await client.DeleteAsync($"/api/users/{created}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Delete_SelfUser_IsRejected()
    {
        var client = await CreateAuthenticatedClientAsync();
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        var myId = me.GetProperty("data").GetProperty("id").GetInt32();

        var response = await client.DeleteAsync($"/api/users/{myId}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_NonExistingUser_Returns404()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.DeleteAsync("/api/users/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetUsers_IncludesRolelessUser_WithNullRoleName()
    {
        var client = await CreateAuthenticatedClientAsync();
        await CreateUserAsync(client, "norole01");

        var response = await client.GetAsync("/api/users?keyword=norole01");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = body.GetProperty("data").GetProperty("items")[0];
        Assert.Equal(JsonValueKind.Null, item.GetProperty("roleId").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("roleName").ValueKind);
    }

    [Fact]
    public async Task GetUsers_ClampsPageSizeTo100()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/users?pageSize=500");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = body.GetProperty("data");
        Assert.Equal(100, data.GetProperty("pageSize").GetInt32());
        // 钳制只影响分页大小，不影响命中总数
        Assert.True(data.GetProperty("total").GetInt32() >= 2);
    }

    [Fact]
    public async Task CreateUser_WithBlankUsername_Returns400()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/users", new CreateUserRequest(
            "   ", "Test@123456", "空白用户名", null, null, true));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateUser_WithBlankDisplayName_Returns400()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/users", new CreateUserRequest(
            "blankname01", "Test@123456", "   ", null, null, true));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_WithBlankDisplayName_Returns400()
    {
        var client = await CreateAuthenticatedClientAsync();
        var created = await CreateUserAsync(client, "blankupdate01");

        var response = await client.PutAsJsonAsync($"/api/users/{created}", new UpdateUserRequest(
            null, "   ", null, null, true));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<int> CreateUserAsync(HttpClient client, string username)
    {
        var response = await client.PostAsJsonAsync("/api/users", new CreateUserRequest(
            username, "Test@123456", "临时用户", null, null, true));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("data").GetProperty("id").GetInt32();
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "admin123"));
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("data").GetProperty("token").GetString());
        return client;
    }
}
