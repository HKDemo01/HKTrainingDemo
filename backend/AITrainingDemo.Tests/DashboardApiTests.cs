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

    /// <summary>
    /// CORS 预检（OPTIONS）必须由 CORS 中间件直接放行，
    /// 不能落到授权中间件上被拦成 401 —— 这是前后端分离联调的前提。
    /// </summary>
    [Fact]
    public async Task Preflight_Returns204WithAllowedOrigin()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/dashboard/stats");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "authorization");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains("http://localhost:5173", response.Headers.GetValues("Access-Control-Allow-Origin"));
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
