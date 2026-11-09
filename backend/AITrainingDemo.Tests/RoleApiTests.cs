using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AITrainingDemo.Api.Dtos;

namespace AITrainingDemo.Tests;

public class RoleApiTests(AppFactory factory) : IClassFixture<AppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetRoles_ReturnsPagedResult_WithUserCount()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/roles");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = body.GetProperty("data");
        Assert.True(data.GetProperty("total").GetInt32() >= 2);
        var items = data.GetProperty("items");
        Assert.True(items.GetArrayLength() >= 2);
        Assert.True(items[0].TryGetProperty("userCount", out _));
    }

    [Fact]
    public async Task GetRolesAll_ReturnsArrayOfOptions()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/roles/all");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = body.GetProperty("data");
        Assert.Equal(JsonValueKind.Array, data.ValueKind);
        Assert.True(data.GetArrayLength() >= 2);
    }

    [Fact]
    public async Task GetRoles_FiltersByKeyword()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/roles?keyword=admin");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("data").GetProperty("items");

        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal("admin", items[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task CreateRole_Succeeds()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/roles",
            new CreateRoleRequest("auditor", "审计员", "只读审计"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateRole_WithDuplicateCode_Returns409()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/roles",
            new CreateRoleRequest("admin", "重复管理员", null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRole_ChangesName()
    {
        var client = await CreateAuthenticatedClientAsync();
        var created = await CreateRoleAsync(client, "updaterole01");

        var response = await client.PutAsJsonAsync($"/api/roles/{created}",
            new UpdateRoleRequest("改名角色", "新描述"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("改名角色", body.GetProperty("data").GetProperty("name").GetString());
    }

    [Fact]
    public async Task Delete_RoleWithUsers_Returns409()
    {
        var client = await CreateAuthenticatedClientAsync();
        var adminRoleId = await GetRoleIdAsync(client, "admin");

        var response = await client.DeleteAsync($"/api/roles/{adminRoleId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Delete_RoleWithoutUsers_Succeeds()
    {
        var client = await CreateAuthenticatedClientAsync();
        var created = await CreateRoleAsync(client, "deleterole01");

        var response = await client.DeleteAsync($"/api/roles/{created}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetRoles_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/roles");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetRoles_ReturnsActualUserCount()
    {
        var client = await CreateAuthenticatedClientAsync();
        var roleId = await CreateRoleAsync(client, "counted01");

        await CreateUserAsync(client, "counted_user01", roleId);
        await CreateUserAsync(client, "counted_user02", roleId);

        var response = await client.GetAsync("/api/roles?keyword=counted01");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = body.GetProperty("data").GetProperty("items")[0];
        Assert.Equal(2, item.GetProperty("userCount").GetInt32());
    }

    [Fact]
    public async Task GetRoles_HugePage_ReturnsEmptyItems()
    {
        var client = await CreateAuthenticatedClientAsync();

        var first = await client.GetAsync("/api/roles?page=1&pageSize=100");
        var firstTotal = (await first.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("total").GetInt32();

        var response = await client.GetAsync("/api/roles?page=2147483647&pageSize=100");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, body.GetProperty("data").GetProperty("items").GetArrayLength());
        // 越界页只影响 items，命中总数不能被一并清零
        Assert.Equal(firstTotal, body.GetProperty("data").GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task CreateRole_CreatedAt_IsMarkedAsUtc()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/roles",
            new CreateRoleRequest("tzrole01", "时区角色", null));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.EndsWith("Z", body.GetProperty("data").GetProperty("createdAt").GetString()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateRole_CreatedAt_IsMarkedAsUtc()
    {
        var client = await CreateAuthenticatedClientAsync();
        var created = await CreateRoleAsync(client, "tzupdate01");

        var response = await client.PutAsJsonAsync($"/api/roles/{created}",
            new UpdateRoleRequest("改名角色", null));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.EndsWith("Z", body.GetProperty("data").GetProperty("createdAt").GetString()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateRole_WithExactly50CharCode_Returns200()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/roles",
            new CreateRoleRequest("c" + new string('c', 49), "边界编码角色", null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetRoles_CreatedAt_IsMarkedAsUtc()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/roles");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = body.GetProperty("data").GetProperty("items")[0];
        Assert.EndsWith("Z", item.GetProperty("createdAt").GetString()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateRole_WithBlankCode_Returns400()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/roles",
            new CreateRoleRequest("   ", "空白编码角色", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateRole_WithBlankName_Returns400()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/roles",
            new CreateRoleRequest("blankname01", "   ", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateRole_WithTooLongCode_Returns400()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/roles",
            new CreateRoleRequest(new string('a', 51), "超长编码角色", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateRole_WithTooLongName_Returns400()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/roles",
            new CreateRoleRequest("toolongname01", new string('名', 51), null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRole_WithTooLongName_Returns400()
    {
        var client = await CreateAuthenticatedClientAsync();
        var created = await CreateRoleAsync(client, "longrole01");

        var response = await client.PutAsJsonAsync($"/api/roles/{created}",
            new UpdateRoleRequest(new string('名', 51), null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<int> CreateUserAsync(HttpClient client, string username, int roleId)
    {
        var response = await client.PostAsJsonAsync("/api/users",
            new CreateUserRequest(username, "Test@123456", "临时用户", null, roleId, true));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("data").GetProperty("id").GetInt32();
    }

    private static async Task<int> CreateRoleAsync(HttpClient client, string code)
    {
        var response = await client.PostAsJsonAsync("/api/roles",
            new CreateRoleRequest(code, "临时角色", null));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("data").GetProperty("id").GetInt32();
    }

    private static async Task<int> GetRoleIdAsync(HttpClient client, string code)
    {
        var response = await client.GetAsync($"/api/roles?keyword={code}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("data").GetProperty("items")[0].GetProperty("id").GetInt32();
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
