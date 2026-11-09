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
        var items = data.GetProperty("items");
        var total = data.GetProperty("total").GetInt32();
        // 种子账号 admin / editor 必须出现在结果里 —— 「查询恒空」的实现也会被这条断言抓住
        var usernames = items.EnumerateArray().Select(i => i.GetProperty("username").GetString()).ToArray();
        Assert.Contains("admin", usernames);
        Assert.Contains("editor", usernames);
        // 返回条数必须与命中总数、每页条数三者自洽
        Assert.Equal(Math.Min(total, 10), items.GetArrayLength());
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
        // 钳制只影响分页大小，不影响命中总数；且命中总数必须与实际返回条数自洽
        var total = data.GetProperty("total").GetInt32();
        var items = data.GetProperty("items");
        Assert.True(total >= 2);
        Assert.Equal(Math.Min(total, 100), items.GetArrayLength());
    }

    [Fact]
    public async Task GetUsers_HugePage_ReturnsEmptyItems()
    {
        var client = await CreateAuthenticatedClientAsync();

        var first = await client.GetAsync("/api/users?page=1&pageSize=100");
        var firstTotal = (await first.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("total").GetInt32();

        var response = await client.GetAsync("/api/users?page=2147483647&pageSize=100");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, body.GetProperty("data").GetProperty("items").GetArrayLength());
        // 越界页只影响 items，命中总数不能被一并清零
        Assert.Equal(firstTotal, body.GetProperty("data").GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task CreateUser_CreatedAt_IsMarkedAsUtc()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/users", new CreateUserRequest(
            "tzuser01", "Test@123456", "时区用户", null, null, true));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.EndsWith("Z", body.GetProperty("data").GetProperty("createdAt").GetString()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateUser_CreatedAt_IsMarkedAsUtc()
    {
        var client = await CreateAuthenticatedClientAsync();
        var created = await CreateUserAsync(client, "tzupdate01");

        var response = await client.PutAsJsonAsync($"/api/users/{created}", new UpdateUserRequest(
            null, "改名用户", null, null, true));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.EndsWith("Z", body.GetProperty("data").GetProperty("createdAt").GetString()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateUser_WithExactly50CharUsername_Returns200()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/users", new CreateUserRequest(
            "u" + new string('u', 49), "Test@123456", "边界用户名", null, null, true));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetUsers_CreatedAt_IsMarkedAsUtc()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/users");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = body.GetProperty("data").GetProperty("items")[0];
        Assert.EndsWith("Z", item.GetProperty("createdAt").GetString()!, StringComparison.Ordinal);
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

    [Fact]
    public async Task CreateUser_WithTooLongUsername_Returns400()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/users", new CreateUserRequest(
            new string('a', 51), "Test@123456", "超长用户名", null, null, true));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateUser_WithTooLongDisplayName_Returns400()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/users", new CreateUserRequest(
            "toolongname01", "Test@123456", new string('名', 51), null, null, true));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_WithTooLongDisplayName_Returns400()
    {
        var client = await CreateAuthenticatedClientAsync();
        var created = await CreateUserAsync(client, "longupdate01");

        var response = await client.PutAsJsonAsync($"/api/users/{created}", new UpdateUserRequest(
            null, new string('名', 51), null, null, true));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_DisablingSelf_Returns400_AndKeepsSessionAlive()
    {
        var client = await CreateAuthenticatedClientAsync();
        var myId = await GetCurrentUserIdAsync(client);

        // 停用自己会让账号既登不进来、已签发的 token 也立即失效，界面上误点一次就能锁死系统
        var response = await client.PutAsJsonAsync($"/api/users/{myId}", new UpdateUserRequest(
            null, "管理员", null, null, false));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, body.GetProperty("code").GetInt32());

        // 拒绝之后原 token 仍然可用，说明账号没有被真的停用
        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    private static async Task<int> GetCurrentUserIdAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/auth/me");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("data").GetProperty("id").GetInt32();
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
