using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AITrainingDemo.Api.Dtos;

namespace AITrainingDemo.Tests;

public class DashboardApiTests(AppFactory factory) : IClassFixture<AppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetStats_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/dashboard/stats");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetStats_WithToken_ReturnsCounts()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/dashboard/stats");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = body.GetProperty("data");
        Assert.True(data.GetProperty("userCount").GetInt32() >= 2);
        Assert.True(data.GetProperty("roleCount").GetInt32() >= 2);
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
