using System.Text;
using System.Security.Claims;
using AITrainingDemo.Api.Common;
using AITrainingDemo.Api.Data;
using AITrainingDemo.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<ApiExceptionFilter>();

builder.Services.AddControllers(options => options.Filters.AddService<ApiExceptionFilter>())
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = _ =>
            new BadRequestObjectResult(ApiResult<object>.Fail(400, "请求参数校验失败"));
    });

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

var jwtIssuer = builder.Configuration["Jwt:Issuer"]!;
var jwtAudience = builder.Configuration["Jwt:Audience"]!;
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"];

// HS256 的密钥强度直接决定 token 是否可被伪造，缺失或过短时拒绝启动
if (string.IsNullOrWhiteSpace(jwtSigningKey) || Encoding.UTF8.GetByteCount(jwtSigningKey) < 32)
{
    throw new InvalidOperationException("Jwt:SigningKey 缺失或长度不足 32 字节，拒绝启动。");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.Events = new JwtBearerEvents
        {
            // 未认证 / token 失效时，授权中间件会直接返回 401 并绕过异常过滤器，
            // 这里补上统一响应体，保持 { code, message, data } 形状
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                context.Response.Headers.WWWAuthenticate = "Bearer";
                await context.Response.WriteAsJsonAsync(ApiResult<object>.Fail(401, "未登录或登录状态已过期"));
            },
            // 每次请求校验一次账号状态：用户被停用或删除后，已签发的 token 应立即失效
            OnTokenValidated = async context =>
            {
                var sub = context.Principal?.FindFirstValue("sub");
                if (!int.TryParse(sub, out var userId))
                {
                    context.Fail("无效的身份声明");
                    return;
                }

                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var enabled = await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.Enabled);
                if (!enabled)
                {
                    context.Fail("账号已停用或不存在");
                }
            }
        };
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ClockSkew = TimeSpan.Zero,
            // 签发时用的是短 claim 名 "role"，不显式指定的话默认按 ClaimTypes.Role 去找，导致按角色授权恒定 403
            RoleClaimType = "role"
        };
    });

builder.Services.AddAuthorization();

// 跨域来源从配置读取，便于按环境调整前端地址
var allowedOrigins = (builder.Configuration.GetSection("Cors:AllowedOrigins").Value ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

// 允许来源为空时不会抛异常，但跨域请求会被静默拒绝且后端无任何日志，
// 容易在部署到新环境时排查不到，因此在启动时显式告警
if (allowedOrigins.Length == 0)
{
    app.Logger.LogWarning("未配置 Cors:AllowedOrigins，所有跨域请求都会被拒绝，请检查该配置项。");
}

// CORS 必须先于认证/授权中间件，否则预检请求（OPTIONS 无 Authorization 头）会被拦成 401
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    DbInitializer.Initialize(db, hasher);
}

app.Run();

/// <summary>供 WebApplicationFactory 使用的入口标记。</summary>
public partial class Program { }
