# 「工作台」基础应用 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在空仓库中交付一个可直接运行的「工作台」应用 —— .NET 10 WebAPI + EF Core + SQLite 后端，Vue 3 + Element Plus + Axios 前端，具备登录、工作台首页统计、用户与角色两个菜单的完整增删改查。

**Architecture:** 后端为单个 ASP.NET Core 项目（`AITrainingDemo.Api`），Controller → Service → EF Core `AppDbContext` → SQLite 文件；所有接口统一返回 `ApiResult<T>` 包装，前端 Axios 实例统一解包。前端为独立 Vite 工程，dev server 通过 `/api` 代理转发到后端 `http://localhost:5080`。后端测试用 `WebApplicationFactory` + SQLite 内存库覆盖核心链路。

**Tech Stack:** .NET SDK 10.0.301 / EF Core 10 (`Microsoft.EntityFrameworkCore.Sqlite`) / `Microsoft.AspNetCore.Authentication.JwtBearer` / xUnit + `Microsoft.AspNetCore.Mvc.Testing` / Node v24.14.0 / npm 11.9.0 / Vue 3 + TypeScript + Vite + Element Plus + Vue Router 4 + Pinia + Axios

**Spec:** `docs/superpowers/specs/2026-11-09-workbench-app-design.md`（本计划以该设计文档为准，两者冲突时以设计文档为准）

---

## Global Constraints

以下约束来自设计文档，**每个任务都隐含包含本节要求**：

- **后端目标框架**：`net10.0`。**数据库**：仅 SQLite，连接串 `Data Source=App_Data/workbench.db`，文件位于 `backend/AITrainingDemo.Api/App_Data/`，**必须加入 `.gitignore`**。
- **禁止任何外部运行依赖服务**：不得引入 Redis、消息队列、云服务、外部数据库、第三方登录。
- **不得引入 `dotnet-ef` 全局工具**：建库使用 `Database.EnsureCreated()` + 幂等种子数据。
- **不引入 Swagger / OpenAPI**：设计文档的 NuGet 依赖清单中不包含 `Swashbuckle` 或 `Microsoft.AspNetCore.OpenApi`，脚手架生成的默认包必须移除。
- **后端 NuGet 白名单**：`Microsoft.EntityFrameworkCore.Sqlite`、`Microsoft.AspNetCore.Authentication.JwtBearer`、`System.IdentityModel.Tokens.Jwt`、`Microsoft.EntityFrameworkCore.Design`；测试项目额外允许 `Microsoft.AspNetCore.Mvc.Testing`、`Microsoft.NET.Test.Sdk`、`xunit`、`xunit.runner.visualstudio`。
- **统一响应包装**：所有控制器返回 `ApiResult<T>`，JSON 字段为 `code` / `message` / `data`；`code === 0` 表示成功，非 0 时 HTTP 状态码与 `code` 一致。
- **分页结构**：`{ items, total, page, pageSize }`，`page` 默认 1，`pageSize` 默认 20、最大 100。
- **密码**：一律经 `IPasswordHasher`（PBKDF2-HMAC-SHA256，100000 迭代，16 字节随机盐，存储格式 `"{saltBase64}.{hashBase64}"`）；**任何接口、任何日志都不得返回或记录 `PasswordHash` 与明文密码**。
- **JWT**：HS256，Claim 使用短名 `sub` / `name` / `role`；`JwtBearerOptions.MapInboundClaims = false`（显式关闭，代码中一律读 `"sub"`）。
- **后端端口** `http://localhost:5080`；**前端端口** `http://localhost:5173`；前端 API 一律走相对路径 `/api`（由 Vite proxy 转发），**禁止在组件或 `api/*.ts` 中硬编码 `http://localhost:5080`**。
- **CORS**：后端允许来源 `http://localhost:5173`。
- **种子数据**：`admin/admin123`（角色 `admin` 管理员）、`editor/editor123`（角色 `editor` 编辑者）；种子必须幂等，按 `Code` / `Username` 判重。
- **命名约定**：后端类型 PascalCase、接口路径 kebab-case 复数资源名；前端组件文件 PascalCase（`UsersView.vue`），接口封装按模块分文件（`src/api/users.ts`）。
- **输出语言**：所有面向用户的文案、注释、提交信息使用**简体中文**；代码标识符、文件名、API 路径保持英文。
- **提交信息**：Conventional Commits，如 `feat: add user crud api`、`chore: scaffold backend project`。
- **后端分层铁律**：Controller 只做「参数接收 → 调用 Service → 返回 `ApiResult<T>`」，**禁止在 Controller 内写校验或业务逻辑**；所有业务校验抛 `BusinessException(code, message)`，由全局 `ApiExceptionFilter` 转成 HTTP 状态码。
- **前端分层铁律**：所有 HTTP 调用必须经过 `@/api/request.ts` 的 axios 实例；**禁止在 `.vue` 组件内直接 `import axios`**；接口地址统一定义在 `src/api/*.ts`。
- **前端 Element Plus 中文**：应用级配置 `zhCn` locale。

---

## Review Focus

设计文档未逐条覆盖、但使用本软件的人最可能撞上的输入与边界 —— 每条都已在下面对应任务中落为测试：

1. **错误密码与不存在的用户名返回同样的 401 与同样的提示**（不得区分「用户不存在」与「密码错误」，否则可枚举用户名）→ Task 3 测试 `Login_WithWrongPassword_And_WithUnknownUser_ReturnSame401`。
2. **JWT 过期或被篡改后，接口必须返回 401 而不是 500** → Task 3 测试 `ProtectedEndpoint_WithGarbageToken_Returns401`。
3. **删除当前登录用户本人必须被拒绝**（否则管理员会把唯一的管理员账号删掉，导致系统锁死）→ Task 4 测试 `Delete_SelfUser_IsRejected`。
4. **创建用户时角色 id 不存在必须报 400 而不是抛外键异常变成 500** → Task 4 测试 `CreateUser_WithUnknownRoleId_Returns400`。
5. **删除被用户引用的角色必须被拒绝（409）**，且角色列表要显示关联用户数 → Task 5 测试 `Delete_RoleWithUsers_Returns409`。

---

## File Structure

**新建文件全景**

```
.claude/CLAUDE.md                              项目规范（Task 1）
.gitignore                                     Task 1
README.md                                      Task 1（Task 11 补充运行说明）
backend/
  AITrainingDemo.sln                           Task 2
  AITrainingDemo.Api/
    AITrainingDemo.Api.csproj                  Task 2
    Program.cs                                 Task 2（含 Task 3/4/5/6 增量修改）
    appsettings.json                           Task 2
    Common/ApiResult.cs                        Task 2
    Common/BusinessException.cs                Task 2
    Common/ApiExceptionFilter.cs               Task 2
    Entities/User.cs                           Task 2
    Entities/Role.cs                           Task 2
    Data/AppDbContext.cs                       Task 2
    Data/DbInitializer.cs                      Task 2
    Dtos/CommonDtos.cs                         Task 2（PagedResult<T>）
    Dtos/AuthDtos.cs                           Task 3
    Dtos/UserDtos.cs                           Task 4
    Dtos/RoleDtos.cs                           Task 5
    Dtos/DashboardDtos.cs                      Task 6
    Services/IPasswordHasher.cs                Task 2
    Services/PasswordHasher.cs                 Task 2
    Services/IAuthService.cs                   Task 3
    Services/AuthService.cs                    Task 3
    Services/IUserService.cs                   Task 4
    Services/UserService.cs                    Task 4
    Services/IRoleService.cs                   Task 5
    Services/RoleService.cs                    Task 5
    Controllers/AuthController.cs              Task 3
    Controllers/UsersController.cs             Task 4
    Controllers/RolesController.cs             Task 5
    Controllers/DashboardController.cs         Task 6
  AITrainingDemo.Tests/
    AITrainingDemo.Tests.csproj                Task 2
    AppFactory.cs                              Task 2
    AuthApiTests.cs                            Task 3
    UserApiTests.cs                            Task 4
    RoleApiTests.cs                            Task 5
    DashboardApiTests.cs                       Task 6
frontend/
  package.json / vite.config.ts / index.html   Task 7
  src/main.ts / src/App.vue                    Task 7
  src/api/request.ts                           Task 7
  src/api/auth.ts / users.ts / roles.ts        Task 7
  src/stores/auth.ts                           Task 7
  src/router/index.ts                          Task 7
  src/views/LoginView.vue                      Task 7
  src/layout/AppLayout.vue                     Task 8
  src/views/DashboardView.vue                  Task 8
  src/views/UsersView.vue                      Task 9
  src/views/RolesView.vue                      Task 10
  src/types/index.ts                           Task 7
```

**职责边界**：`Services` 只依赖 `AppDbContext` 与 `IPasswordHasher`，不依赖 Controller 与 DTO 之外的层；`Controllers` 只依赖 Service；`api/*.ts` 只负责 HTTP 与类型转换，不含 UI 逻辑。

---

## Task 1: 仓库骨架与项目规范

**Files:**
- Create: `.claude/CLAUDE.md`
- Create: `.gitignore`
- Create: `README.md`

**Interfaces:**
- Consumes: 无
- Produces: 仓库根目录约定；后续所有任务都在此骨架上添加文件。

- [ ] **Step 1: 创建 `.gitignore`**

写入 `M:\ClaudeCode\AITrainingDemo\.gitignore`：

```gitignore
# .NET
bin/
obj/
*.user

# SQLite 数据库
backend/AITrainingDemo.Api/App_Data/
*.db
*.db-shm
*.db-wal

# Node
node_modules/
dist/
dist-ssr/
*.local

# 编辑器
.vs/
.idea/
.DS_Store

# 日志
*.log
```

- [ ] **Step 2: 创建 `.claude/CLAUDE.md`**

写入 `M:\ClaudeCode\AITrainingDemo\.claude\CLAUDE.md`：

```markdown
# 项目规范 · AITrainingDemo

本文件是本仓库的项目级规范，继承全局规范 `C:\Users\hua.w\.claude\CLAUDE.md`。
全局规范中的全部硬性规则（尤其「输出语言必须为简体中文」「最小代码」「手术式修改」）在本项目同样生效；本文件只做**项目专属补充**。

## 1. 技术栈（不得擅自替换）

| 层 | 选型 |
|---|---|
| 后端 | .NET 10（`net10.0`）WebAPI + EF Core 10 + SQLite |
| 前端 | Vue 3 + TypeScript + Vite + Element Plus + Vue Router 4 + Pinia + Axios |
| 测试 | xUnit + `WebApplicationFactory`（仅后端） |

数据库**仅限 SQLite**（`backend/AITrainingDemo.Api/App_Data/workbench.db`），**禁止引入任何外部运行依赖服务**（Redis / MQ / 云服务 / 外部数据库）。

## 2. 目录结构

```
backend/AITrainingDemo.Api/    Controller / Service / Entity / DTO / Common / Data
backend/AITrainingDemo.Tests/  xUnit 集成测试
frontend/src/                  api / stores / router / layout / views
```

新增文件时**按职责就近放置**，不要新建 `utils/`、`helpers/`、`common/` 之类的大杂烩目录。

## 3. 后端分层规则

- **Controller**：只做「接收参数 → 调用 Service → 返回 `ApiResult<T>`」。禁止写校验、禁止写 LINQ 查询、禁止直接注入 `AppDbContext` 做业务判断。
- **Service**：承载全部业务规则与校验。校验失败一律 `throw new BusinessException(code, message)`，由 `ApiExceptionFilter` 转 HTTP 状态码。
- **Entity**：不做业务逻辑，不加 `[NotMapped]` 计算属性以外的展示字段。
- **DTO**：对外暴露的一律用 DTO，**禁止把 EF 实体直接返回给前端**（`User.PasswordHash` 绝不能出现在任何响应里）。
- **数据库变更**：改实体必须同步改 `AppDbContext.OnModelCreating` 的模型配置（唯一索引、长度、外键删除行为）。
- 建库使用 `Database.EnsureCreated()` + 幂等种子，**不使用 `dotnet-ef` 迁移**。

## 4. API 约定

- 路径：`/api/{复数资源名}`，资源名 kebab-case（`/api/users`、`/api/roles`、`/api/dashboard/stats`）。
- 统一响应：`{ "code": 0, "message": "ok", "data": ... }`。`code === 0` 成功；失败时 `code` 等于 HTTP 状态码。
- 分页：`{ items, total, page, pageSize }`。`pageSize` 默认 20、上限 100。
- 需要登录的接口标注 `[Authorize]`；当前用户 id 从 JWT 的 `"sub"` claim 读取（**注意：`MapInboundClaims = false`**）。

## 5. 前端约定

- 所有 HTTP 调用必须走 `@/api/request.ts` 创建的 axios 实例；**组件内禁止直接 `import axios`**。
- 接口地址统一写在 `src/api/*.ts`，组件只调用 `xxxApi.yyy()`。
- 接口出入参类型统一放在 `src/types/index.ts`。
- 组件用 `<script setup lang="ts">`；组件文件名 PascalCase（`UsersView.vue`），页面放 `src/views/`，布局放 `src/layout/`。
- 路由必须挂 `meta.title`（顶栏显示）与 `meta.requiresAuth`（守卫用）。
- 登录态存 `localStorage`：`workbench_token`、`workbench_user`；401 时必须清空并跳回 `/login`。

## 6. 提交与文案

- 提交信息用 Conventional Commits：`feat:` / `fix:` / `chore:` / `docs:` / `refactor:`。
- 所有界面文案、代码注释、提交信息用**简体中文**；标识符与 API 路径保持英文。
```

- [ ] **Step 3: 创建 `README.md` 占位（Task 11 会补全运行说明）**

写入 `M:\ClaudeCode\AITrainingDemo\README.md`：

```markdown
# AITrainingDemo · 工作台

.NET 10 WebAPI + EF Core + SQLite 后端，Vue 3 + Element Plus 前端。

> 运行说明见本文件末尾（由实施阶段补全）。
```

- [ ] **Step 4: 提交**

```bash
git add .gitignore .claude/CLAUDE.md README.md
git commit -m "chore: add repo skeleton, project conventions and gitignore"
```

---

## Task 2: 后端脚手架、实体与数据层

**Files:**
- Create: `backend/AITrainingDemo.sln`（由 dotnet 命令生成）
- Create: `backend/AITrainingDemo.Api/AITrainingDemo.Api.csproj`
- Create: `backend/AITrainingDemo.Api/Program.cs`
- Create: `backend/AITrainingDemo.Api/appsettings.json`
- Create: `backend/AITrainingDemo.Api/Common/ApiResult.cs`
- Create: `backend/AITrainingDemo.Api/Common/BusinessException.cs`
- Create: `backend/AITrainingDemo.Api/Common/ApiExceptionFilter.cs`
- Create: `backend/AITrainingDemo.Api/Entities/User.cs`
- Create: `backend/AITrainingDemo.Api/Entities/Role.cs`
- Create: `backend/AITrainingDemo.Api/Data/AppDbContext.cs`
- Create: `backend/AITrainingDemo.Api/Data/DbInitializer.cs`
- Create: `backend/AITrainingDemo.Api/Dtos/CommonDtos.cs`
- Create: `backend/AITrainingDemo.Api/Services/IPasswordHasher.cs`
- Create: `backend/AITrainingDemo.Api/Services/PasswordHasher.cs`
- Create: `backend/AITrainingDemo.Tests/AITrainingDemo.Tests.csproj`
- Create: `backend/AITrainingDemo.Tests/AppFactory.cs`
- Create: `backend/AITrainingDemo.Tests/DataLayerTests.cs`

**Interfaces:**
- Consumes: 无
- Produces:
  - `AITrainingDemo.Api.Common.ApiResult<T>` — `static ApiResult<T> Ok(T data)`、`static ApiResult<T> Fail(int code, string message)`；属性 `int Code`、`string Message`、`T? Data`。
  - `AITrainingDemo.Api.Common.BusinessException(int code, string message)` — 属性 `int Code`。
  - `AITrainingDemo.Api.Common.ApiExceptionFilter : IExceptionFilter` — DI 生命周期 Scoped。
  - `AITrainingDemo.Api.Data.AppDbContext : DbContext` — `DbSet<User> Users`、`DbSet<Role> Roles`。
  - `AITrainingDemo.Api.Data.DbInitializer` — `static void Initialize(AppDbContext db, IPasswordHasher hasher)`（幂等，建库 + 种子）。
  - `AITrainingDemo.Api.Dtos.PagedResult<T>` — `IReadOnlyList<T> Items`、`int Total`、`int Page`、`int PageSize`。
  - `AITrainingDemo.Api.Services.IPasswordHasher` — `string Hash(string password)`、`bool Verify(string password, string storedHash)`。
  - `AITrainingDemo.Api.Entities.User` — `int Id`、`string Username`、`string PasswordHash`、`string DisplayName`、`string? Email`、`int? RoleId`、`Role? Role`、`bool Enabled`、`DateTime CreatedAt`。
  - `AITrainingDemo.Api.Entities.Role` — `int Id`、`string Code`、`string Name`、`string? Description`、`DateTime CreatedAt`、`ICollection<User> Users`。
  - `backend/AITrainingDemo.Tests/AppFactory : WebApplicationFactory<Program>` — 供后续任务复用。

- [ ] **Step 1: 创建解决方案与 WebAPI 项目**

```bash
cd /m/ClaudeCode/AITrainingDemo
dotnet new sln -n AITrainingDemo -o backend
dotnet new webapi -n AITrainingDemo.Api -o backend/AITrainingDemo.Api --use-controllers -f net10.0
dotnet sln backend/AITrainingDemo.sln add backend/AITrainingDemo.Api/AITrainingDemo.Api.csproj
```

- [ ] **Step 2: 移除脚手架默认的 OpenAPI 依赖与示例控制器**

```bash
cd /m/ClaudeCode/AITrainingDemo/backend/AITrainingDemo.Api
dotnet remove package Microsoft.AspNetCore.OpenApi
rm -f Controllers/WeatherForecastController.cs
rm -f WeatherForecast.cs
```

（若 `rm` 不可用，改用 `git rm` 或直接删除对应文件。）

- [ ] **Step 3: 添加 NuGet 依赖**

```bash
cd /m/ClaudeCode/AITrainingDemo/backend/AITrainingDemo.Api
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 10.0.0
dotnet add package Microsoft.EntityFrameworkCore.Design --version 10.0.0
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer --version 10.0.0
dotnet add package System.IdentityModel.Tokens.Jwt --version 8.3.0
```

> 若指定版本在源上不存在，去掉 `--version` 让 NuGet 解析当前最新 10.x。

- [ ] **Step 4: 创建测试项目**

```bash
cd /m/ClaudeCode/AITrainingDemo
dotnet new xunit -n AITrainingDemo.Tests -o backend/AITrainingDemo.Tests -f net10.0
dotnet sln backend/AITrainingDemo.sln add backend/AITrainingDemo.Tests/AITrainingDemo.Tests.csproj
dotnet add backend/AITrainingDemo.Tests/AITrainingDemo.Tests.csproj reference backend/AITrainingDemo.Api/AITrainingDemo.Api.csproj
dotnet add backend/AITrainingDemo.Tests package Microsoft.AspNetCore.Mvc.Testing --version 10.0.0
```

- [ ] **Step 5: 创建实体**

`backend/AITrainingDemo.Api/Entities/Role.cs`：

```csharp
namespace AITrainingDemo.Api.Entities;

public class Role
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<User> Users { get; set; } = new List<User>();
}
```

`backend/AITrainingDemo.Api/Entities/User.cs`：

```csharp
namespace AITrainingDemo.Api.Entities;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public int? RoleId { get; set; }
    public Role? Role { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
```

- [ ] **Step 6: 创建 `AppDbContext`**

`backend/AITrainingDemo.Api/Data/AppDbContext.cs`：

```csharp
using AITrainingDemo.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace AITrainingDemo.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).IsRequired().HasMaxLength(50);
            b.Property(x => x.Name).IsRequired().HasMaxLength(50);
            b.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<User>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Username).IsRequired().HasMaxLength(50);
            b.Property(x => x.DisplayName).IsRequired().HasMaxLength(50);
            b.HasIndex(x => x.Username).IsUnique();
            b.HasOne(x => x.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
```

- [ ] **Step 7: 创建密码哈希服务**

`backend/AITrainingDemo.Api/Services/IPasswordHasher.cs`：

```csharp
namespace AITrainingDemo.Api.Services;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string storedHash);
}
```

`backend/AITrainingDemo.Api/Services/PasswordHasher.cs`：

```csharp
using System.Security.Cryptography;

namespace AITrainingDemo.Api.Services;

public class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string storedHash)
    {
        var parts = storedHash.Split('.');
        if (parts.Length != 2)
        {
            return false;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[0]);
            expected = Convert.FromBase64String(parts[1]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
```

- [ ] **Step 8: 创建统一响应与异常处理**

`backend/AITrainingDemo.Api/Common/ApiResult.cs`：

```csharp
namespace AITrainingDemo.Api.Common;

public record ApiResult<T>
{
    public int Code { get; init; }
    public string Message { get; init; } = "ok";
    public T? Data { get; init; }

    public static ApiResult<T> Ok(T data) => new() { Code = 0, Message = "ok", Data = data };
    public static ApiResult<T> Fail(int code, string message) => new() { Code = code, Message = message, Data = default };
}
```

`backend/AITrainingDemo.Api/Common/BusinessException.cs`：

```csharp
namespace AITrainingDemo.Api.Common;

public class BusinessException(int code, string message) : Exception(message)
{
    public int Code { get; } = code;
}
```

`backend/AITrainingDemo.Api/Common/ApiExceptionFilter.cs`：

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AITrainingDemo.Api.Common;

public class ApiExceptionFilter(ILogger<ApiExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var ex = context.Exception;
        var (code, message) = ex is BusinessException be
            ? (be.Code, be.Message)
            : (500, "服务器内部错误");

        if (ex is not BusinessException)
        {
            logger.LogError(ex, "未处理异常: {Path}", context.HttpContext.Request.Path);
        }

        context.Result = new ObjectResult(ApiResult<object>.Fail(code, message)) { StatusCode = code };
        context.ExceptionHandled = true;
    }
}
```

- [ ] **Step 9: 创建分页 DTO 与种子初始化**

`backend/AITrainingDemo.Api/Dtos/CommonDtos.cs`：

```csharp
namespace AITrainingDemo.Api.Dtos;

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
```

`backend/AITrainingDemo.Api/Data/DbInitializer.cs`：

```csharp
using AITrainingDemo.Api.Entities;
using AITrainingDemo.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AITrainingDemo.Api.Data;

public static class DbInitializer
{
    public static void Initialize(AppDbContext db, IPasswordHasher hasher)
    {
        // SQLite 会自动创建数据库文件，但不会创建目录
        var dataSource = db.Database.GetConnectionString();
        if (!string.IsNullOrWhiteSpace(dataSource))
        {
            var builder = new SqliteConnectionStringBuilder(dataSource);
            if (!string.IsNullOrWhiteSpace(builder.DataSource) && builder.DataSource != ":memory:")
            {
                var directory = Path.GetDirectoryName(builder.DataSource);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
        }

        db.Database.EnsureCreated();
        Seed(db, hasher);
    }

    public static void Seed(AppDbContext db, IPasswordHasher hasher)
    {
        var adminRole = db.Roles.FirstOrDefault(r => r.Code == "admin");
        if (adminRole is null)
        {
            adminRole = new Role { Code = "admin", Name = "管理员", Description = "系统管理员", CreatedAt = DateTime.UtcNow };
            db.Roles.Add(adminRole);
        }

        var editorRole = db.Roles.FirstOrDefault(r => r.Code == "editor");
        if (editorRole is null)
        {
            editorRole = new Role { Code = "editor", Name = "编辑者", Description = "普通编辑", CreatedAt = DateTime.UtcNow };
            db.Roles.Add(editorRole);
        }

        db.SaveChanges();

        if (!db.Users.Any(u => u.Username == "admin"))
        {
            db.Users.Add(new User
            {
                Username = "admin",
                PasswordHash = hasher.Hash("admin123"),
                DisplayName = "管理员",
                Email = "admin@example.com",
                RoleId = adminRole.Id,
                Enabled = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (!db.Users.Any(u => u.Username == "editor"))
        {
            db.Users.Add(new User
            {
                Username = "editor",
                PasswordHash = hasher.Hash("editor123"),
                DisplayName = "编辑者",
                Email = "editor@example.com",
                RoleId = editorRole.Id,
                Enabled = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        db.SaveChanges();
    }
}
```

- [ ] **Step 10: 写 `appsettings.json`**

`backend/AITrainingDemo.Api/appsettings.json`：

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=App_Data/workbench.db"
  },
  "Jwt": {
    "Issuer": "AITrainingDemo",
    "Audience": "AITrainingDemo.Client",
    "SigningKey": "dev-only-signing-key-change-me-in-production-0123456789",
    "ExpiresHours": 8
  },
  "Cors": {
    "AllowedOrigins": "http://localhost:5173"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

- [ ] **Step 11: 写 `Program.cs`（本任务先不含 JWT 鉴权，Task 3 补全）**

`backend/AITrainingDemo.Api/Program.cs`：

```csharp
using AITrainingDemo.Api.Common;
using AITrainingDemo.Api.Data;
using AITrainingDemo.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options => options.Filters.Add<ApiExceptionFilter>())
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = _ =>
            new BadRequestObjectResult(ApiResult<object>.Fail(400, "请求参数校验失败"));
    });

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();

var app = builder.Build();

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
```

- [ ] **Step 12: 写测试基座 `AppFactory`**

`backend/AITrainingDemo.Tests/AppFactory.cs`：

```csharp
using AITrainingDemo.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace AITrainingDemo.Tests;

public class AppFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public AppFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // 直接注入配置，避免依赖 appsettings.json 在测试进程中的定位
        builder.UseSetting("Jwt:Issuer", "AITrainingDemo");
        builder.UseSetting("Jwt:Audience", "AITrainingDemo.Client");
        builder.UseSetting("Jwt:SigningKey", "test-signing-key-012345678901234567890123456789");
        builder.UseSetting("Cors:AllowedOrigins", "http://localhost:5173");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Data Source=:memory:");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
```

- [ ] **Step 13: 写数据层测试**

`backend/AITrainingDemo.Tests/DataLayerTests.cs`：

```csharp
using AITrainingDemo.Api.Data;
using AITrainingDemo.Api.Services;
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
```

> 需要 `using Microsoft.EntityFrameworkCore;`（`AnyAsync` / `CountAsync`）与 `using Xunit;`（若模板未全局导入 xunit，在文件顶部补 `using Xunit;`）。

- [ ] **Step 14: 运行测试**

```bash
cd /m/ClaudeCode/AITrainingDemo
dotnet test backend/AITrainingDemo.Tests/AITrainingDemo.Tests.csproj
```

Expected: 全部 PASS（4 个用例）。

- [ ] **Step 15: 提交**

```bash
git add backend/ .gitignore
git commit -m "feat: scaffold backend project with entity, dbcontext and seed data"
```

---

## Task 3: JWT 登录鉴权

> **结转项（来自 Task 2 审查的裁决，必须遵守）**
>
> 1. **NuGet 版本**：`Microsoft.AspNetCore.Authentication.JwtBearer` **已在 Task 2 中装好并升到 `10.0.14`**（与 EF Core 对齐、`--vulnerable` 无告警），本任务**不需要也不允许**再动包版本或新增任何包。本计划正文里 `dotnet add package ... --version 10.0.0` 那一步**已过时，跳过**。想自检就跑一次 `dotnet list package --vulnerable --include-transitive`。
> 2. **异常过滤器注册路径唯一**：`Program.cs` 中 `ApiExceptionFilter` 的注册**只保留一条路径**（`builder.Services.AddScoped<ApiExceptionFilter>();` + `options.Filters.AddService<ApiExceptionFilter>();`）。**不得**再出现 `options.Filters.Add<ApiExceptionFilter>()`，否则异常会被处理两次。
> 3. **补一条异常映射测试**：新增自动化测试，断言 Service 抛出的 `BusinessException(code, message)` 经 `ApiExceptionFilter` 后，HTTP 状态码 == `code`，且响应体是 `ApiResult` 形状（`code`/`message`/`data` 三字段，`code` 等于业务码、`message` 一致）。
> 4. **`Jwt:SigningKey` 的来源**：配置键名是 `Jwt:SigningKey`（`appsettings.json` 已有），测试环境由 `AppFactory` 用 `builder.UseSetting("Jwt:SigningKey", ...)` 注入，两边键名必须一致。
> 5. **登录失败必须是 401，不能是 500**：`AuthService.LoginAsync` 对「用户不存在 / 已停用 / 密码错误」三条路径统一抛 `BusinessException(401, "用户名或密码错误")`，且**错误提示文案必须完全一致**（简报里的 `Login_WithWrongPassword_And_WithUnknownUser_ReturnSame401` 用例断言两个响应体逐字相等）。另外注意 `PasswordHasher.Verify` 收到 `null` 口令时 `Rfc2898DeriveBytes.Pbkdf2` 会抛 `ArgumentNullException` → 500。模型绑定通常会在 `null` 时先返回 400（`InvalidModelStateResponseFactory`），但**空字符串**口令会走到 Service。**补一条测试**：用空口令登录应返回 401（不是 500）；若因此需要在 `PasswordHasher.Verify` 里加 `string.IsNullOrEmpty(password)` 防护，可以加。

**Files:**
- Create: `backend/AITrainingDemo.Api/Dtos/AuthDtos.cs`
- Create: `backend/AITrainingDemo.Api/Services/IAuthService.cs`
- Create: `backend/AITrainingDemo.Api/Services/AuthService.cs`
- Create: `backend/AITrainingDemo.Api/Controllers/AuthController.cs`
- Create: `backend/AITrainingDemo.Tests/AuthApiTests.cs`
- Modify: `backend/AITrainingDemo.Api/Program.cs`

**Interfaces:**
- Consumes: `ApiResult<T>`、`BusinessException`、`IPasswordHasher`、`AppDbContext`（Task 2）。
- Produces:
  - `AITrainingDemo.Api.Dtos.LoginRequest(string Username, string Password)`。
  - `AITrainingDemo.Api.Dtos.LoginUserInfo(int Id, string Username, string DisplayName, string? RoleName)`。
  - `AITrainingDemo.Api.Dtos.LoginResponse(string Token, DateTime ExpiresAt, LoginUserInfo User)`。
  - `AITrainingDemo.Api.Services.IAuthService` — `Task<LoginResponse> LoginAsync(LoginRequest request)`、`Task<LoginUserInfo> GetCurrentUserAsync(int userId)`。
  - `POST /api/auth/login`（匿名）、`GET /api/auth/me`（需 Bearer）。
  - 约定：JWT Claim 短名 `sub`（用户 Id）、`name`（用户名）、`role`（角色 Code）。

- [ ] **Step 1: 写 DTO**

`backend/AITrainingDemo.Api/Dtos/AuthDtos.cs`：

```csharp
namespace AITrainingDemo.Api.Dtos;

public record LoginRequest(string Username, string Password);

public record LoginUserInfo(int Id, string Username, string DisplayName, string? RoleName);

public record LoginResponse(string Token, DateTime ExpiresAt, LoginUserInfo User);
```

- [ ] **Step 2: 写失败测试**

`backend/AITrainingDemo.Tests/AuthApiTests.cs`：

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AITrainingDemo.Api.Dtos;

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
        var content = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("passwordHash", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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

    private async Task<string> LoginAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "admin123"));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("data").GetProperty("token").GetString()!;
    }
}
```

- [ ] **Step 3: 运行测试验证失败**

```bash
cd /m/ClaudeCode/AITrainingDemo
dotnet test backend/AITrainingDemo.Tests/AITrainingDemo.Tests.csproj --filter "FullyQualifiedName~AuthApiTests"
```

Expected: FAIL —— `401`/`404` 等，因为 `/api/auth/login` 尚未实现（404 Not Found）。

- [ ] **Step 4: 实现 `IAuthService` / `AuthService`**

`backend/AITrainingDemo.Api/Services/IAuthService.cs`：

```csharp
using AITrainingDemo.Api.Dtos;

namespace AITrainingDemo.Api.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request);
    Task<LoginUserInfo> GetCurrentUserAsync(int userId);
}
```

`backend/AITrainingDemo.Api/Services/AuthService.cs`：

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AITrainingDemo.Api.Common;
using AITrainingDemo.Api.Data;
using AITrainingDemo.Api.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace AITrainingDemo.Api.Services;

public class AuthService(AppDbContext db, IPasswordHasher hasher, IConfiguration configuration) : IAuthService
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var user = await db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == request.Username);

        // 用户不存在、已停用、密码错误一律返回同一提示，避免用户名枚举
        if (user is null || !user.Enabled || !hasher.Verify(request.Password, user.PasswordHash))
        {
            throw new BusinessException(401, "用户名或密码错误");
        }

        var expiresAt = DateTime.UtcNow.AddHours(8);
        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("name", user.Username)
        };

        if (user.Role is not null)
        {
            claims.Add(new Claim("role", user.Role.Code));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"]!));
        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new LoginResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt,
            new LoginUserInfo(user.Id, user.Username, user.DisplayName, user.Role?.Name));
    }

    public async Task<LoginUserInfo> GetCurrentUserAsync(int userId)
    {
        var user = await db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
        {
            throw new BusinessException(404, "用户不存在");
        }

        return new LoginUserInfo(user.Id, user.Username, user.DisplayName, user.Role?.Name);
    }
}
```

- [ ] **Step 5: 实现 `AuthController`**

`backend/AITrainingDemo.Api/Controllers/AuthController.cs`：

```csharp
using System.Security.Claims;
using AITrainingDemo.Api.Common;
using AITrainingDemo.Api.Dtos;
using AITrainingDemo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AITrainingDemo.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<ApiResult<LoginResponse>>> Login(LoginRequest request)
    {
        var result = await authService.LoginAsync(request);
        return Ok(ApiResult<LoginResponse>.Ok(result));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<ApiResult<LoginUserInfo>>> Me()
    {
        var userId = int.Parse(User.FindFirstValue("sub")!);
        var result = await authService.GetCurrentUserAsync(userId);
        return Ok(ApiResult<LoginUserInfo>.Ok(result));
    }
}
```

- [ ] **Step 6: 在 `Program.cs` 注册 JWT 鉴权**

在 `backend/AITrainingDemo.Api/Program.cs` 中，在 `builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();` 这一行**之后追加**以下代码块（**保留文件中已有的其他所有行，不要删除或改动**；同时在文件顶部补 `using Microsoft.AspNetCore.Authentication.JwtBearer;`、`using Microsoft.IdentityModel.Tokens;`、`using System.Text;`）：

> ⚠️ **注意**：下面代码块第一行 `builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();` 只是**定位锚点**，表示「从这一行往后插入」——**文件里已经有一行完全相同的注册，不要再写第二遍**（重复注册会覆盖同一个服务，虽不报错但属冗余）。实际需要**新增**的是从 `builder.Services.AddScoped<IAuthService, AuthService>();` 开始的全部内容。

```csharp
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IAuthService, AuthService>();

var jwtIssuer = builder.Configuration["Jwt:Issuer"]!;
var jwtAudience = builder.Configuration["Jwt:Audience"]!;
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"]!;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
```

再把 `using` 区补上：

```csharp
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
```

并在 `app.MapControllers();` **之前**插入：

```csharp
app.UseAuthentication();
app.UseAuthorization();
```

- [ ] **Step 7: 运行测试验证通过**

```bash
cd /m/ClaudeCode/AITrainingDemo
dotnet test backend/AITrainingDemo.Tests/AITrainingDemo.Tests.csproj --filter "FullyQualifiedName~AuthApiTests"
```

Expected: 6 个用例全部 PASS。

- [ ] **Step 8: 提交**

```bash
git add backend/
git commit -m "feat: add jwt authentication and auth api"
```

---

## Task 4: 用户 CRUD

> **实现注意点**
>
> 1. **无角色用户的列表查询**：`UserService.GetPagedAsync` 的投影里写了 `u.Role!.Name`。用户可以**不分配角色**（`RoleId` 为 null），此时 `u.Role` 是 null。EF Core 会把 `u.Role.Name` 翻译成 LEFT JOIN 并得到 null，`UserDto.RoleName` 是 `string?` 所以能接住 —— 但这依赖 EF 的翻译行为，**必须用一个「无角色用户」实测**：新增一个 `roleId` 为 null 的用户后，`GET /api/users` 仍返回 200，该行的 `roleName` 为 null，且列表接口不抛异常。
> 2. **`page`/`pageSize` 钳制**：全局约束要求 `page` 默认 1、`pageSize` 默认 20、**上限 100**。简报给的 `GetPagedAsync` 代码已含 `Math.Min(query.PageSize, MaxPageSize)`，照做即可；**再补一条断言**：请求 `pageSize=500` 时，响应里的 `pageSize` 字段应为 100（不是 500），`total` 不受影响。
> 3. **`DELETE /api/users/{id}` 删除自己**：`DeleteAsync(id, currentUserId)` 在 id == currentUserId 时抛 `BusinessException(400, "不能删除当前登录用户")`。简报测试预期写的是 400，确认一致即可。

**Files:**
- Create: `backend/AITrainingDemo.Api/Dtos/UserDtos.cs`
- Create: `backend/AITrainingDemo.Api/Services/IUserService.cs`
- Create: `backend/AITrainingDemo.Api/Services/UserService.cs`
- Create: `backend/AITrainingDemo.Api/Controllers/UsersController.cs`
- Create: `backend/AITrainingDemo.Tests/UserApiTests.cs`
- Modify: `backend/AITrainingDemo.Api/Program.cs`

**Interfaces:**
- Consumes: `ApiResult<T>`、`BusinessException`、`IPasswordHasher`、`AppDbContext`、`IAuthService`（Task 2/3）。
- Produces:
  - `AITrainingDemo.Api.Dtos.UserDto(int Id, string Username, string DisplayName, string? Email, int? RoleId, string? RoleName, bool Enabled, DateTime CreatedAt)`。
  - `AITrainingDemo.Api.Dtos.CreateUserRequest(string Username, string Password, string DisplayName, string? Email, int? RoleId, bool Enabled)`。
  - `AITrainingDemo.Api.Dtos.UpdateUserRequest(string? Password, string DisplayName, string? Email, int? RoleId, bool Enabled)`。
  - `AITrainingDemo.Api.Dtos.UserQuery(string? Keyword, int? RoleId, int Page, int PageSize)`。
  - `AITrainingDemo.Api.Services.IUserService` — `Task<PagedResult<UserDto>> GetPagedAsync(UserQuery query)`、`Task<UserDto> CreateAsync(CreateUserRequest request)`、`Task<UserDto> UpdateAsync(int id, UpdateUserRequest request)`、`Task DeleteAsync(int id, int currentUserId)`。
  - `GET/POST /api/users`、`PUT/DELETE /api/users/{id}`。

- [ ] **Step 1: 写 DTO**

`backend/AITrainingDemo.Api/Dtos/UserDtos.cs`：

```csharp
namespace AITrainingDemo.Api.Dtos;

public record UserDto(
    int Id,
    string Username,
    string DisplayName,
    string? Email,
    int? RoleId,
    string? RoleName,
    bool Enabled,
    DateTime CreatedAt);

public record CreateUserRequest(
    string Username,
    string Password,
    string DisplayName,
    string? Email,
    int? RoleId,
    bool Enabled);

public record UpdateUserRequest(
    string? Password,
    string DisplayName,
    string? Email,
    int? RoleId,
    bool Enabled);

public record UserQuery(string? Keyword, int? RoleId, int Page = 1, int PageSize = 20);
```

- [ ] **Step 2: 写失败测试**

`backend/AITrainingDemo.Tests/UserApiTests.cs`：

```csharp
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
```

- [ ] **Step 3: 运行测试验证失败**

```bash
cd /m/ClaudeCode/AITrainingDemo
dotnet test backend/AITrainingDemo.Tests/AITrainingDemo.Tests.csproj --filter "FullyQualifiedName~UserApiTests"
```

Expected: FAIL —— 404，`/api/users` 尚未实现。

- [ ] **Step 4: 实现 `IUserService` / `UserService`**

`backend/AITrainingDemo.Api/Services/IUserService.cs`：

```csharp
using AITrainingDemo.Api.Dtos;

namespace AITrainingDemo.Api.Services;

public interface IUserService
{
    Task<PagedResult<UserDto>> GetPagedAsync(UserQuery query);
    Task<UserDto> CreateAsync(CreateUserRequest request);
    Task<UserDto> UpdateAsync(int id, UpdateUserRequest request);
    Task DeleteAsync(int id, int currentUserId);
}
```

`backend/AITrainingDemo.Api/Services/UserService.cs`：

```csharp
using AITrainingDemo.Api.Common;
using AITrainingDemo.Api.Data;
using AITrainingDemo.Api.Dtos;
using AITrainingDemo.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace AITrainingDemo.Api.Services;

public class UserService(AppDbContext db, IPasswordHasher hasher) : IUserService
{
    private const int MaxPageSize = 100;

    public async Task<PagedResult<UserDto>> GetPagedAsync(UserQuery query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 20 : Math.Min(query.PageSize, MaxPageSize);

        IQueryable<User> q = db.Users.Include(u => u.Role);

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            q = q.Where(u => u.Username.Contains(keyword)
                            || u.DisplayName.Contains(keyword)
                            || (u.Email != null && u.Email.Contains(keyword)));
        }

        if (query.RoleId.HasValue)
        {
            q = q.Where(u => u.RoleId == query.RoleId.Value);
        }

        var total = await q.CountAsync();

        var items = await q
            .OrderBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserDto(u.Id, u.Username, u.DisplayName, u.Email, u.RoleId, u.Role!.Name, u.Enabled, u.CreatedAt))
            .ToListAsync();

        return new PagedResult<UserDto>(items, total, page, pageSize);
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request)
    {
        var username = request.Username.Trim();

        if (await db.Users.AnyAsync(u => u.Username == username))
        {
            throw new BusinessException(409, "用户名已存在");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new BusinessException(400, "密码不能为空");
        }

        await EnsureRoleExistsAsync(request.RoleId);

        var user = new User
        {
            Username = username,
            PasswordHash = hasher.Hash(request.Password),
            DisplayName = request.DisplayName.Trim(),
            Email = request.Email,
            RoleId = request.RoleId,
            Enabled = request.Enabled,
            CreatedAt = DateTime.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return await GetByIdAsync(user.Id);
    }

    public async Task<UserDto> UpdateAsync(int id, UpdateUserRequest request)
    {
        var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            throw new BusinessException(404, "用户不存在");
        }

        await EnsureRoleExistsAsync(request.RoleId);

        user.DisplayName = request.DisplayName.Trim();
        user.Email = request.Email;
        user.RoleId = request.RoleId;
        user.Enabled = request.Enabled;

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.PasswordHash = hasher.Hash(request.Password);
        }

        await db.SaveChangesAsync();

        return await GetByIdAsync(user.Id);
    }

    public async Task DeleteAsync(int id, int currentUserId)
    {
        if (id == currentUserId)
        {
            throw new BusinessException(400, "不能删除当前登录用户");
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            throw new BusinessException(404, "用户不存在");
        }

        db.Users.Remove(user);
        await db.SaveChangesAsync();
    }

    private async Task EnsureRoleExistsAsync(int? roleId)
    {
        if (roleId is null)
        {
            return;
        }

        if (!await db.Roles.AnyAsync(r => r.Id == roleId.Value))
        {
            throw new BusinessException(400, "角色不存在");
        }
    }

    private async Task<UserDto> GetByIdAsync(int id)
    {
        var user = await db.Users
            .Include(u => u.Role)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user is null)
        {
            throw new BusinessException(404, "用户不存在");
        }

        return new UserDto(user.Id, user.Username, user.DisplayName, user.Email, user.RoleId, user.Role?.Name, user.Enabled, user.CreatedAt);
    }
}
```

- [ ] **Step 5: 实现 `UsersController`**

`backend/AITrainingDemo.Api/Controllers/UsersController.cs`：

```csharp
using System.Security.Claims;
using AITrainingDemo.Api.Common;
using AITrainingDemo.Api.Dtos;
using AITrainingDemo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AITrainingDemo.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController(IUserService userService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResult<PagedResult<UserDto>>>> GetPaged(
        [FromQuery] string? keyword,
        [FromQuery] int? roleId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await userService.GetPagedAsync(new UserQuery(keyword, roleId, page, pageSize));
        return Ok(ApiResult<PagedResult<UserDto>>.Ok(result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResult<UserDto>>> Create(CreateUserRequest request)
    {
        var result = await userService.CreateAsync(request);
        return Ok(ApiResult<UserDto>.Ok(result));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResult<UserDto>>> Update(int id, UpdateUserRequest request)
    {
        var result = await userService.UpdateAsync(id, request);
        return Ok(ApiResult<UserDto>.Ok(result));
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResult<object>>> Delete(int id)
    {
        var currentUserId = int.Parse(User.FindFirstValue("sub")!);
        await userService.DeleteAsync(id, currentUserId);
        return Ok(ApiResult<object>.Ok(new { }));
    }
}
```

- [ ] **Step 6: 在 `Program.cs` 注册 `IUserService`**

在 `builder.Services.AddScoped<IAuthService, AuthService>();` 下一行加入：

```csharp
builder.Services.AddScoped<IUserService, UserService>();
```

- [ ] **Step 7: 运行测试验证通过**

```bash
cd /m/ClaudeCode/AITrainingDemo
dotnet test backend/AITrainingDemo.Tests/AITrainingDemo.Tests.csproj --filter "FullyQualifiedName~UserApiTests"
```

Expected: 9 个用例全部 PASS。

- [ ] **Step 8: 提交**

```bash
git add backend/
git commit -m "feat: add user crud api with pagination and role filter"
```

---

## Task 5: 角色 CRUD

**Files:**
- Create: `backend/AITrainingDemo.Api/Dtos/RoleDtos.cs`
- Create: `backend/AITrainingDemo.Api/Services/IRoleService.cs`
- Create: `backend/AITrainingDemo.Api/Services/RoleService.cs`
- Create: `backend/AITrainingDemo.Api/Controllers/RolesController.cs`
- Create: `backend/AITrainingDemo.Tests/RoleApiTests.cs`
- Modify: `backend/AITrainingDemo.Api/Program.cs`

**Interfaces:**
- Consumes: Task 2/3/4 产出。
- Produces:
  - `AITrainingDemo.Api.Dtos.RoleDto(int Id, string Code, string Name, string? Description, int UserCount, DateTime CreatedAt)`。
  - `AITrainingDemo.Api.Dtos.RoleOptionDto(int Id, string Code, string Name)`。
  - `AITrainingDemo.Api.Dtos.CreateRoleRequest(string Code, string Name, string? Description)`。
  - `AITrainingDemo.Api.Dtos.UpdateRoleRequest(string Name, string? Description)`。
  - `AITrainingDemo.Api.Dtos.RoleQuery(string? Keyword, int Page = 1, int PageSize = 20)`。
  - `AITrainingDemo.Api.Services.IRoleService` — `Task<PagedResult<RoleDto>> GetPagedAsync(RoleQuery query)`、`Task<IReadOnlyList<RoleOptionDto>> GetAllAsync()`、`Task<RoleDto> CreateAsync(CreateRoleRequest request)`、`Task<RoleDto> UpdateAsync(int id, UpdateRoleRequest request)`、`Task DeleteAsync(int id)`。
  - `GET /api/roles`（分页）、`GET /api/roles/all`（全量选项）、`POST /api/roles`、`PUT/DELETE /api/roles/{id}`。

- [ ] **Step 1: 写 DTO**

`backend/AITrainingDemo.Api/Dtos/RoleDtos.cs`：

```csharp
namespace AITrainingDemo.Api.Dtos;

public record RoleDto(int Id, string Code, string Name, string? Description, int UserCount, DateTime CreatedAt);

public record RoleOptionDto(int Id, string Code, string Name);

public record CreateRoleRequest(string Code, string Name, string? Description);

public record UpdateRoleRequest(string Name, string? Description);

public record RoleQuery(string? Keyword, int Page = 1, int PageSize = 20);
```

- [ ] **Step 2: 写失败测试**

`backend/AITrainingDemo.Tests/RoleApiTests.cs`：

```csharp
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
```

- [ ] **Step 3: 运行测试验证失败**

```bash
cd /m/ClaudeCode/AITrainingDemo
dotnet test backend/AITrainingDemo.Tests/AITrainingDemo.Tests.csproj --filter "FullyQualifiedName~RoleApiTests"
```

Expected: FAIL —— 404，`/api/roles` 尚未实现。

- [ ] **Step 4: 实现 `IRoleService` / `RoleService`**

`backend/AITrainingDemo.Api/Services/IRoleService.cs`：

```csharp
using AITrainingDemo.Api.Dtos;

namespace AITrainingDemo.Api.Services;

public interface IRoleService
{
    Task<PagedResult<RoleDto>> GetPagedAsync(RoleQuery query);
    Task<IReadOnlyList<RoleOptionDto>> GetAllAsync();
    Task<RoleDto> CreateAsync(CreateRoleRequest request);
    Task<RoleDto> UpdateAsync(int id, UpdateRoleRequest request);
    Task DeleteAsync(int id);
}
```

`backend/AITrainingDemo.Api/Services/RoleService.cs`：

```csharp
using AITrainingDemo.Api.Common;
using AITrainingDemo.Api.Data;
using AITrainingDemo.Api.Dtos;
using AITrainingDemo.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace AITrainingDemo.Api.Services;

public class RoleService(AppDbContext db) : IRoleService
{
    private const int MaxPageSize = 100;

    public async Task<PagedResult<RoleDto>> GetPagedAsync(RoleQuery query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 20 : Math.Min(query.PageSize, MaxPageSize);

        IQueryable<Role> q = db.Roles;

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            q = q.Where(r => r.Code.Contains(keyword) || r.Name.Contains(keyword));
        }

        var total = await q.CountAsync();

        var items = await q
            .OrderBy(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new RoleDto(
                r.Id,
                r.Code,
                r.Name,
                r.Description,
                r.Users.Count,
                r.CreatedAt))
            .ToListAsync();

        return new PagedResult<RoleDto>(items, total, page, pageSize);
    }

    public async Task<IReadOnlyList<RoleOptionDto>> GetAllAsync()
    {
        return await db.Roles
            .AsNoTracking()
            .OrderBy(r => r.Id)
            .Select(r => new RoleOptionDto(r.Id, r.Code, r.Name))
            .ToListAsync();
    }

    public async Task<RoleDto> CreateAsync(CreateRoleRequest request)
    {
        var code = request.Code.Trim();

        if (await db.Roles.AnyAsync(r => r.Code == code))
        {
            throw new BusinessException(409, "角色编码已存在");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessException(400, "角色名称不能为空");
        }

        var role = new Role
        {
            Code = code,
            Name = request.Name.Trim(),
            Description = request.Description,
            CreatedAt = DateTime.UtcNow
        };

        db.Roles.Add(role);
        await db.SaveChangesAsync();

        return new RoleDto(role.Id, role.Code, role.Name, role.Description, 0, role.CreatedAt);
    }

    public async Task<RoleDto> UpdateAsync(int id, UpdateRoleRequest request)
    {
        var role = await db.Roles.Include(r => r.Users).FirstOrDefaultAsync(r => r.Id == id);
        if (role is null)
        {
            throw new BusinessException(404, "角色不存在");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessException(400, "角色名称不能为空");
        }

        role.Name = request.Name.Trim();
        role.Description = request.Description;

        await db.SaveChangesAsync();

        return new RoleDto(role.Id, role.Code, role.Name, role.Description, role.Users.Count, role.CreatedAt);
    }

    public async Task DeleteAsync(int id)
    {
        var role = await db.Roles.Include(r => r.Users).FirstOrDefaultAsync(r => r.Id == id);
        if (role is null)
        {
            throw new BusinessException(404, "角色不存在");
        }

        if (role.Users.Count > 0)
        {
            throw new BusinessException(409, $"该角色已被 {role.Users.Count} 个用户引用，无法删除");
        }

        db.Roles.Remove(role);
        await db.SaveChangesAsync();
    }
}
```

- [ ] **Step 5: 实现 `RolesController`**

`backend/AITrainingDemo.Api/Controllers/RolesController.cs`：

```csharp
using AITrainingDemo.Api.Common;
using AITrainingDemo.Api.Dtos;
using AITrainingDemo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AITrainingDemo.Api.Controllers;

[ApiController]
[Route("api/roles")]
[Authorize]
public class RolesController(IRoleService roleService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResult<PagedResult<RoleDto>>>> GetPaged(
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await roleService.GetPagedAsync(new RoleQuery(keyword, page, pageSize));
        return Ok(ApiResult<PagedResult<RoleDto>>.Ok(result));
    }

    [HttpGet("all")]
    public async Task<ActionResult<ApiResult<IReadOnlyList<RoleOptionDto>>>> GetAll()
    {
        var result = await roleService.GetAllAsync();
        return Ok(ApiResult<IReadOnlyList<RoleOptionDto>>.Ok(result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResult<RoleDto>>> Create(CreateRoleRequest request)
    {
        var result = await roleService.CreateAsync(request);
        return Ok(ApiResult<RoleDto>.Ok(result));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResult<RoleDto>>> Update(int id, UpdateRoleRequest request)
    {
        var result = await roleService.UpdateAsync(id, request);
        return Ok(ApiResult<RoleDto>.Ok(result));
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResult<object>>> Delete(int id)
    {
        await roleService.DeleteAsync(id);
        return Ok(ApiResult<object>.Ok(new { }));
    }
}
```

- [ ] **Step 6: 在 `Program.cs` 注册 `IRoleService`**

在 `builder.Services.AddScoped<IUserService, UserService>();` 下一行加入：

```csharp
builder.Services.AddScoped<IRoleService, RoleService>();
```

- [ ] **Step 7: 运行测试验证通过**

```bash
cd /m/ClaudeCode/AITrainingDemo
dotnet test backend/AITrainingDemo.Tests/AITrainingDemo.Tests.csproj --filter "FullyQualifiedName~RoleApiTests"
```

Expected: 8 个用例全部 PASS。

- [ ] **Step 8: 提交**

```bash
git add backend/
git commit -m "feat: add role crud api with reference protection"
```

---

## Task 6: 工作台统计接口与跨域联调配置

**Files:**
- Create: `backend/AITrainingDemo.Api/Dtos/DashboardDtos.cs`
- Create: `backend/AITrainingDemo.Api/Controllers/DashboardController.cs`
- Create: `backend/AITrainingDemo.Tests/DashboardApiTests.cs`
- Modify: `backend/AITrainingDemo.Api/Program.cs`
- Modify: `backend/AITrainingDemo.Api/Properties/launchSettings.json`（固定监听地址 5080）

**Interfaces:**
- Consumes: Task 2/3 产出。
- Produces:
  - `AITrainingDemo.Api.Dtos.DashboardStatsDto(int UserCount, int RoleCount)`。
  - `GET /api/dashboard/stats`（需 Bearer）。
  - 后端监听 `http://localhost:5080`；CORS 允许 `http://localhost:5173`。

- [ ] **Step 1: 写 DTO 与失败测试**

`backend/AITrainingDemo.Api/Dtos/DashboardDtos.cs`：

```csharp
namespace AITrainingDemo.Api.Dtos;

public record DashboardStatsDto(int UserCount, int RoleCount);
```

`backend/AITrainingDemo.Tests/DashboardApiTests.cs`：

```csharp
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
```

- [ ] **Step 2: 运行测试验证失败**

```bash
cd /m/ClaudeCode/AITrainingDemo
dotnet test backend/AITrainingDemo.Tests/AITrainingDemo.Tests.csproj --filter "FullyQualifiedName~DashboardApiTests"
```

Expected: FAIL —— 404。

- [ ] **Step 3: 实现 `DashboardController`**

`backend/AITrainingDemo.Api/Controllers/DashboardController.cs`：

```csharp
using AITrainingDemo.Api.Common;
using AITrainingDemo.Api.Data;
using AITrainingDemo.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AITrainingDemo.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController(AppDbContext db) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<ActionResult<ApiResult<DashboardStatsDto>>> GetStats()
    {
        var userCount = await db.Users.CountAsync();
        var roleCount = await db.Roles.CountAsync();
        return Ok(ApiResult<DashboardStatsDto>.Ok(new DashboardStatsDto(userCount, roleCount)));
    }
}
```

- [ ] **Step 4: 在 `Program.cs` 开启 CORS**

在 `Program.cs` 的 `using` 区加入 `using Microsoft.AspNetCore.Cors;`（若尚未存在）。

在 `builder.Services.AddAuthorization();` 之后加入：

```csharp
var allowedOrigins = (builder.Configuration.GetSection("Cors:AllowedOrigins").Value ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));
```

在 `var app = builder.Build();` 之后、`app.MapControllers();` 之前加入：

```csharp
app.UseCors();
```

> ⚠️ **中间件顺序（关键）**：Task 3 已经在 `app.MapControllers();` 之前插入了 `app.UseAuthentication();` 和 `app.UseAuthorization();`。`app.UseCors();` **必须放在 `app.UseAuthentication()` 之前**，不能简单地追加到 `app.MapControllers();` 的紧前位置 —— 否则 CORS 预检（OPTIONS，不带 Authorization 头）会先被授权中间件拦成 401，浏览器直接判定跨域失败。
>
> 正确做法：把 `app.UseCors();` 放在 `app.UseAuthentication();` 的**上一行**。最终顺序应为：
> ```
> app.UseCors();
> app.UseAuthentication();
> app.UseAuthorization();
> app.MapControllers();
> ```

- [ ] **Step 5: 固定后端监听地址为 5080**

用以下内容覆盖 `backend/AITrainingDemo.Api/Properties/launchSettings.json`：

```json
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "applicationUrl": "http://localhost:5080",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

> `dotnet run` 会优先采用 `launchSettings.json` 的 `applicationUrl`，因此必须改这里；只改 csproj 的 `<ApplicationUrl>` 不生效。

- [ ] **Step 6: 运行全部后端测试**

```bash
cd /m/ClaudeCode/AITrainingDemo
dotnet test backend/AITrainingDemo.Tests/AITrainingDemo.Tests.csproj
```

Expected: 全部 PASS（4 + 6 + 9 + 8 + 2 = 29 个用例）。

- [ ] **Step 7: 提交**

```bash
git add backend/
git commit -m "feat: add dashboard stats api and cors setup"
```

---

## Task 7: 前端脚手架、axios 层与登录页

> **实现注意点**
>
> `src/api/request.ts` → `src/stores/auth.ts` → `src/api/auth.ts` → `src/api/request.ts` 构成**循环依赖**，同时 `request.ts` 还引入了 `@/router`，而 `router/index.ts` 又引入 `stores/auth`。ESM 下这通常能工作（引用都在函数体内、模块求值期不触发），但**必须实测**：构建通过 ≠ 运行时可用。
>
> 因此本任务除 `npm run build` 外，**必须**额外做一次运行时验证：启动 `npm run dev`，确认登录页能加载、`/login` 路由不白屏、控制台无 `Cannot access '...' before initialization` 之类的循环依赖报错。若确实报错，用最小改动打破循环（例如把 `router` 的引用改成惰性 `import('@/router')`，或把 store 的取用改成在拦截器回调内动态获取）。
>
> 另：`npm create vite@latest` 在非交互终端可能卡住。**已实测可用**的非交互命令是：
> ```bash
> npm create vite@latest frontend -- --template vue-ts --yes
> ```
> （已验证 create-vite 9.x 能正确生成 `index.html` / `package.json` / `vite.config.ts` / `tsconfig*.json` / `src/`，无需任何交互输入。）
>
> 注意脚手架模板的 `build` 脚本是 `vue-tsc -b && vite build`，**含 TypeScript 类型检查** —— 这很好，但意味着 `npm run build` 会因任何类型错误失败。模板自带的示例代码（如 `src/components/HelloWorld.vue`、`src/style.css`、`src/assets/vue.svg`）如果没被覆盖，可能引入未使用的导入或类型错误，**按需删除或替换**。

**Files:**
- Create: `frontend/package.json`（脚手架生成后修改）
- Create: `frontend/vite.config.ts`
- Create: `frontend/index.html`
- Create: `frontend/src/main.ts`
- Create: `frontend/src/App.vue`
- Create: `frontend/src/types/index.ts`
- Create: `frontend/src/api/request.ts`
- Create: `frontend/src/api/auth.ts`
- Create: `frontend/src/api/users.ts`
- Create: `frontend/src/api/roles.ts`
- Create: `frontend/src/stores/auth.ts`
- Create: `frontend/src/router/index.ts`
- Create: `frontend/src/views/LoginView.vue`

**Interfaces:**
- Consumes: 后端 `POST /api/auth/login`、`GET /api/auth/me`（Task 3）。
- Produces:
  - `src/types/index.ts` 导出 `ApiResult<T>`、`PagedResult<T>`、`LoginRequest`、`LoginUserInfo`、`LoginResponse`、`UserDto`、`RoleDto`、`RoleOptionDto`、`DashboardStatsDto`、`CreateUserRequest`、`UpdateUserRequest`、`CreateRoleRequest`、`UpdateRoleRequest`。
  - `src/api/request.ts` 导出 `request`（axios 实例，默认解包 `data.data`）。
  - `src/api/auth.ts` 导出 `authApi.login(payload)`、`authApi.me()`。
  - `src/api/users.ts` 导出 `usersApi.getPaged(params)`、`usersApi.create(payload)`、`usersApi.update(id, payload)`、`usersApi.remove(id)`。
  - `src/api/roles.ts` 导出 `rolesApi.getPaged(params)`、`rolesApi.getAll()`、`rolesApi.create(payload)`、`rolesApi.update(id, payload)`、`rolesApi.remove(id)`。
  - `src/stores/auth.ts` 导出 `useAuthStore()`（`token`、`user`、`isLoggedIn`、`setSession()`、`clear()`、`fetchMe()`）。
  - `src/router/index.ts` 默认导出 router，路由：`/login`、`/`（redirect `/dashboard`）、`/dashboard`、`/users`、`/roles`。
  - `src/views/LoginView.vue` —— 登录表单。

- [ ] **Step 1: 脚手架生成前端工程**

```bash
cd /m/ClaudeCode/AITrainingDemo
npm create vite@latest frontend -- --template vue-ts --yes
cd frontend
npm install
npm install element-plus@^2.14.8 @element-plus/icons-vue@^2.3.2 vue-router@^4.6.4 pinia@^3.0.4 axios@^1.21.0
```

> ⚠️ **版本必须显式指定**：registry 上 `vue-router` 的 `latest` 已经是 **5.x**、`pinia` 的 `latest` 是 **4.x**，而项目规范（`.claude/CLAUDE.md` §1 与 §8）明确要求 **Vue Router 4**。裸写 `npm install vue-router` 会装到 v5，**违反规范**。请按上面给出的显式版本号安装。
>
> 已实测（2026-11-09，本机 npm registry = registry.npmmirror.com）：`npm create vite@latest frontend -- --template vue-ts --yes` 非交互可用，create-vite 9.3.4 生成 `index.html` / `package.json` / `vite.config.ts` / `tsconfig*.json` / `src/`，无任何交互输入。安装后 `npm ls --depth=0` 得到：vue 3.5.45、vue-router 4.6.4、pinia 3.0.4、element-plus 2.14.8、axios 1.21.0、vite 8.3.7、typescript 6.0.3、vue-tsc 3.3.14。
>
> 脚手架模板的 `build` 脚本是 `vue-tsc -b && vite build`，**含 TypeScript 类型检查** —— 任何类型错误都会让构建失败。模板自带的示例代码（`src/components/HelloWorld.vue`、`src/style.css`、`src/assets/vue.svg`、`src/types.ts`）按需删除或替换。

- [ ] **Step 2: 配置 `vite.config.ts`**

用以下内容覆盖 `frontend/vite.config.ts`：

```ts
import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5080',
        changeOrigin: true,
      },
    },
  },
})
```

- [ ] **Step 3: 写类型定义 `src/types/index.ts`**

```ts
export interface ApiResult<T> {
  code: number
  message: string
  data: T
}

export interface PagedResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

export interface LoginRequest {
  username: string
  password: string
}

export interface LoginUserInfo {
  id: number
  username: string
  displayName: string
  roleName: string | null
}

export interface LoginResponse {
  token: string
  expiresAt: string
  user: LoginUserInfo
}

export interface UserDto {
  id: number
  username: string
  displayName: string
  email: string | null
  roleId: number | null
  roleName: string | null
  enabled: boolean
  createdAt: string
}

export interface RoleDto {
  id: number
  code: string
  name: string
  description: string | null
  userCount: number
  createdAt: string
}

export interface RoleOptionDto {
  id: number
  code: string
  name: string
}

export interface DashboardStatsDto {
  userCount: number
  roleCount: number
}

export interface CreateUserRequest {
  username: string
  password: string
  displayName: string
  email: string | null
  roleId: number | null
  enabled: boolean
}

export interface UpdateUserRequest {
  password: string | null
  displayName: string
  email: string | null
  roleId: number | null
  enabled: boolean
}

export interface CreateRoleRequest {
  code: string
  name: string
  description: string | null
}

export interface UpdateRoleRequest {
  name: string
  description: string | null
}
```

- [ ] **Step 4: 写 axios 实例 `src/api/request.ts`**

```ts
import axios from 'axios'
import { ElMessage } from 'element-plus'
import { useAuthStore } from '@/stores/auth'
import router from '@/router'
import type { ApiResult } from '@/types'

const request = axios.create({
  baseURL: '/api',
  timeout: 15000,
})

request.interceptors.request.use((config) => {
  const auth = useAuthStore()
  if (auth.token) {
    config.headers.Authorization = `Bearer ${auth.token}`
  }
  return config
})

request.interceptors.response.use(
  (response) => {
    const body = response.data as ApiResult<unknown>
    if (body && typeof body.code === 'number' && body.code !== 0) {
      ElMessage.error(body.message || '请求失败')
      return Promise.reject(new Error(body.message))
    }
    return response
  },
  (error) => {
    const status = error.response?.status
    const message = error.response?.data?.message
    if (status === 401) {
      // 401 既可能是「登录失败」也可能是「会话过期」，两种情况都必须给出提示
      ElMessage.error(message || '登录状态已失效，请重新登录')
      if (router.currentRoute.value.path !== '/login') {
        useAuthStore().clear()
        router.push({ path: '/login', query: { redirect: router.currentRoute.value.fullPath } })
      }
    } else {
      ElMessage.error(message || '请求失败，请稍后重试')
    }
    return Promise.reject(error)
  },
)

export default request
```

- [ ] **Step 5: 写 API 封装**

`src/api/auth.ts`：

```ts
import request from './request'
import type { ApiResult, LoginRequest, LoginResponse, LoginUserInfo } from '@/types'

export const authApi = {
  login(payload: LoginRequest) {
    return request.post<ApiResult<LoginResponse>>('/auth/login', payload).then((r) => r.data.data)
  },
  me() {
    return request.get<ApiResult<LoginUserInfo>>('/auth/me').then((r) => r.data.data)
  },
}
```

`src/api/users.ts`：

```ts
import request from './request'
import type { ApiResult, CreateUserRequest, PagedResult, UpdateUserRequest, UserDto } from '@/types'

export interface UserQueryParams {
  keyword?: string
  roleId?: number | null
  page?: number
  pageSize?: number
}

export const usersApi = {
  getPaged(params: UserQueryParams) {
    return request.get<ApiResult<PagedResult<UserDto>>>('/users', { params }).then((r) => r.data.data)
  },
  create(payload: CreateUserRequest) {
    return request.post<ApiResult<UserDto>>('/users', payload).then((r) => r.data.data)
  },
  update(id: number, payload: UpdateUserRequest) {
    return request.put<ApiResult<UserDto>>(`/users/${id}`, payload).then((r) => r.data.data)
  },
  remove(id: number) {
    return request.delete<ApiResult<object>>(`/users/${id}`).then((r) => r.data.data)
  },
}
```

`src/api/roles.ts`：

```ts
import request from './request'
import type { ApiResult, CreateRoleRequest, PagedResult, RoleDto, RoleOptionDto, UpdateRoleRequest } from '@/types'

export interface RoleQueryParams {
  keyword?: string
  page?: number
  pageSize?: number
}

export const rolesApi = {
  getPaged(params: RoleQueryParams) {
    return request.get<ApiResult<PagedResult<RoleDto>>>('/roles', { params }).then((r) => r.data.data)
  },
  getAll() {
    return request.get<ApiResult<RoleOptionDto[]>>('/roles/all').then((r) => r.data.data)
  },
  create(payload: CreateRoleRequest) {
    return request.post<ApiResult<RoleDto>>('/roles', payload).then((r) => r.data.data)
  },
  update(id: number, payload: UpdateRoleRequest) {
    return request.put<ApiResult<RoleDto>>(`/roles/${id}`, payload).then((r) => r.data.data)
  },
  remove(id: number) {
    return request.delete<ApiResult<object>>(`/roles/${id}`).then((r) => r.data.data)
  },
}
```

- [ ] **Step 6: 写 `src/stores/auth.ts`**

```ts
import { defineStore } from 'pinia'
import { authApi } from '@/api/auth'
import type { LoginUserInfo } from '@/types'

const TOKEN_KEY = 'workbench_token'
const USER_KEY = 'workbench_user'

export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: localStorage.getItem(TOKEN_KEY) ?? '',
    user: (JSON.parse(localStorage.getItem(USER_KEY) ?? 'null') as LoginUserInfo | null),
  }),
  getters: {
    isLoggedIn: (state) => !!state.token,
  },
  actions: {
    setSession(token: string, user: LoginUserInfo) {
      this.token = token
      this.user = user
      localStorage.setItem(TOKEN_KEY, token)
      localStorage.setItem(USER_KEY, JSON.stringify(user))
    },
    clear() {
      this.token = ''
      this.user = null
      localStorage.removeItem(TOKEN_KEY)
      localStorage.removeItem(USER_KEY)
    },
    async fetchMe() {
      try {
        this.user = await authApi.me()
      } catch {
        this.clear()
      }
    },
  },
})
```

- [ ] **Step 7: 写 `src/router/index.ts`**

```ts
import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/login',
      name: 'login',
      component: () => import('@/views/LoginView.vue'),
      meta: { requiresAuth: false, title: '登录' },
    },
    {
      path: '/',
      component: () => import('@/layout/AppLayout.vue'),
      children: [
        { path: '', redirect: '/dashboard' },
        {
          path: 'dashboard',
          name: 'dashboard',
          component: () => import('@/views/DashboardView.vue'),
          meta: { requiresAuth: true, title: '工作台' },
        },
        {
          path: 'users',
          name: 'users',
          component: () => import('@/views/UsersView.vue'),
          meta: { requiresAuth: true, title: '用户管理' },
        },
        {
          path: 'roles',
          name: 'roles',
          component: () => import('@/views/RolesView.vue'),
          meta: { requiresAuth: true, title: '角色管理' },
        },
      ],
    },
    { path: '/:pathMatch(.*)*', redirect: '/dashboard' },
  ],
})

router.beforeEach((to) => {
  const auth = useAuthStore()
  if (to.meta.requiresAuth === false) {
    if (auth.isLoggedIn) {
      return { path: '/dashboard' }
    }
    return true
  }
  if (!auth.isLoggedIn) {
    return { path: '/login', query: { redirect: to.fullPath } }
  }
  return true
})

export default router
```

> 后续 Task 8/9/10 会创建 `AppLayout.vue`、`DashboardView.vue`、`UsersView.vue`、`RolesView.vue`。本任务结束前先创建 4 个占位组件，避免路由动态 import 报错。分别新建 `src/layout/AppLayout.vue`、`src/views/DashboardView.vue`、`src/views/UsersView.vue`、`src/views/RolesView.vue`，内容均为：
>
> ```vue
> <template>
>   <div />
> </template>
> ```

- [ ] **Step 8: 写 `src/main.ts` 与 `src/App.vue`**

`src/main.ts`：

```ts
import { createApp } from 'vue'
import { createPinia } from 'pinia'
import ElementPlus from 'element-plus'
import zhCn from 'element-plus/es/locale/lang/zh-cn'
import * as ElementPlusIconsVue from '@element-plus/icons-vue'
import 'element-plus/dist/index.css'

import App from './App.vue'
import router from './router'

const app = createApp(App)

for (const [key, component] of Object.entries(ElementPlusIconsVue)) {
  app.component(key, component)
}

app.use(createPinia())
app.use(router)
app.use(ElementPlus, { locale: zhCn })

app.mount('#app')
```

`src/App.vue`：

```vue
<template>
  <router-view />
</template>
```

- [ ] **Step 9: 写登录页 `src/views/LoginView.vue`**

```vue
<template>
  <div class="login-page">
    <el-card class="login-card">
      <h2 class="login-title">工作台</h2>
      <p class="login-subtitle">请使用账号登录</p>

      <el-form ref="formRef" :model="form" :rules="rules" size="large" @keyup.enter="submit">
        <el-form-item prop="username">
          <el-input v-model="form.username" placeholder="用户名" :prefix-icon="User" clearable />
        </el-form-item>
        <el-form-item prop="password">
          <el-input
            v-model="form.password"
            type="password"
            placeholder="密码"
            :prefix-icon="Lock"
            show-password
          />
        </el-form-item>
        <el-form-item>
          <el-button type="primary" class="login-submit" :loading="loading" @click="submit">
            登 录
          </el-button>
        </el-form-item>
      </el-form>

      <p class="login-tip">默认账号：admin / admin123</p>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, type FormInstance, type FormRules } from 'element-plus'
import { Lock, User } from '@element-plus/icons-vue'
import { authApi } from '@/api/auth'
import { useAuthStore } from '@/stores/auth'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

const formRef = ref<FormInstance>()
const loading = ref(false)

const form = reactive({
  username: 'admin',
  password: 'admin123',
})

const rules: FormRules = {
  username: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
  password: [{ required: true, message: '请输入密码', trigger: 'blur' }],
}

async function submit() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return

  loading.value = true
  try {
    const result = await authApi.login({ username: form.username, password: form.password })
    auth.setSession(result.token, result.user)
    ElMessage.success('登录成功')
    const redirect = route.query.redirect
    await router.replace(typeof redirect === 'string' ? redirect : '/dashboard')
  } catch {
    // 错误提示已由 axios 拦截器统一处理
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.login-page {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 100vh;
  background: linear-gradient(135deg, #1f2d3d 0%, #2c5364 100%);
}

.login-card {
  width: 380px;
  padding: 8px 12px;
}

.login-title {
  margin: 0;
  text-align: center;
  font-size: 24px;
}

.login-subtitle {
  margin: 6px 0 20px;
  text-align: center;
  color: #909399;
}

.login-submit {
  width: 100%;
}

.login-tip {
  margin: 0;
  text-align: center;
  color: #c0c4cc;
  font-size: 12px;
}
</style>
```

- [ ] **Step 10: 构建验证**

```bash
cd /m/ClaudeCode/AITrainingDemo/frontend
npm run build
```

Expected: 构建成功，无 TypeScript 报错。

- [ ] **Step 11: 提交**

```bash
git add frontend/
git commit -m "feat: scaffold frontend with axios, pinia, router and login page"
```

---

## Task 8: 应用布局与工作台首页

**Files:**
- Create: `frontend/src/api/dashboard.ts`
- Create（覆盖 Task 7 的占位组件）: `frontend/src/layout/AppLayout.vue`
- Create（覆盖 Task 7 的占位组件）: `frontend/src/views/DashboardView.vue`

**Interfaces:**
- Consumes: `useAuthStore()`、Task 6 的 `GET /api/dashboard/stats`。
- Produces:
  - `src/api/dashboard.ts` 导出 `dashboardApi.getStats()`。
  - `src/layout/AppLayout.vue` —— 侧边栏菜单（工作台 / 用户管理 / 角色管理）+ 顶栏（标题 + 用户下拉退出）。
  - `src/views/DashboardView.vue` —— 欢迎语 + 用户/角色统计卡 + 快捷入口。

- [ ] **Step 1: 写 `src/api/dashboard.ts`**

```ts
import request from './request'
import type { ApiResult, DashboardStatsDto } from '@/types'

export const dashboardApi = {
  getStats() {
    return request.get<ApiResult<DashboardStatsDto>>('/dashboard/stats').then((r) => r.data.data)
  },
}
```

- [ ] **Step 2: 写布局 `src/layout/AppLayout.vue`**

```vue
<template>
  <el-container class="app-layout">
    <el-aside width="210px" class="app-aside">
      <div class="app-logo">工作台</div>
      <el-menu
        :default-active="route.path"
        router
        background-color="#1f2d3d"
        text-color="#c0c4cc"
        active-text-color="#409eff"
      >
        <el-menu-item index="/dashboard">
          <el-icon><HomeFilled /></el-icon>
          <span>工作台</span>
        </el-menu-item>
        <el-menu-item index="/users">
          <el-icon><User /></el-icon>
          <span>用户管理</span>
        </el-menu-item>
        <el-menu-item index="/roles">
          <el-icon><Avatar /></el-icon>
          <span>角色管理</span>
        </el-menu-item>
      </el-menu>
    </el-aside>

    <el-container>
      <el-header class="app-header">
        <span class="app-header-title">{{ route.meta.title }}</span>
        <el-dropdown @command="handleCommand">
          <span class="app-user">
            {{ auth.user?.displayName ?? '未登录' }}
            <el-icon><ArrowDown /></el-icon>
          </span>
          <template #dropdown>
            <el-dropdown-menu>
              <el-dropdown-item command="logout">退出登录</el-dropdown-item>
            </el-dropdown-menu>
          </template>
        </el-dropdown>
      </el-header>

      <el-main class="app-main">
        <router-view />
      </el-main>
    </el-container>
  </el-container>
</template>

<script setup lang="ts">
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { ArrowDown, Avatar, HomeFilled, User } from '@element-plus/icons-vue'
import { useAuthStore } from '@/stores/auth'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

function handleCommand(command: string) {
  if (command === 'logout') {
    auth.clear()
    ElMessage.success('已退出登录')
    router.push('/login')
  }
}
</script>

<style scoped>
.app-layout {
  height: 100vh;
}

.app-aside {
  background-color: #1f2d3d;
}

.app-logo {
  height: 56px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  font-size: 18px;
  font-weight: 600;
}

.app-aside :deep(.el-menu) {
  border-right: none;
}

.app-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  background: #fff;
  border-bottom: 1px solid #e6e6e6;
}

.app-header-title {
  font-size: 16px;
  font-weight: 500;
}

.app-user {
  display: flex;
  align-items: center;
  gap: 4px;
  cursor: pointer;
  color: #606266;
}

.app-main {
  background: #f0f2f5;
}
</style>
```

- [ ] **Step 3: 写工作台首页 `src/views/DashboardView.vue`**

```vue
<template>
  <div>
    <el-card shadow="never" class="welcome-card">
      <h2 class="welcome-title">你好，{{ auth.user?.displayName ?? '朋友' }}</h2>
      <p class="welcome-sub">
        当前角色：{{ auth.user?.roleName ?? '无' }} ｜ 欢迎使用工作台
      </p>
    </el-card>

    <el-row :gutter="16" class="stat-row">
      <el-col :xs="24" :sm="12">
        <el-card shadow="never">
          <div class="stat-label">用户总数</div>
          <div class="stat-value">{{ stats.userCount }}</div>
        </el-card>
      </el-col>
      <el-col :xs="24" :sm="12">
        <el-card shadow="never">
          <div class="stat-label">角色总数</div>
          <div class="stat-value">{{ stats.roleCount }}</div>
        </el-card>
      </el-col>
    </el-row>

    <el-card shadow="never" class="quick-card">
      <template #header>快捷入口</template>
      <el-space wrap>
        <el-button type="primary" plain @click="router.push('/users')">用户管理</el-button>
        <el-button type="primary" plain @click="router.push('/roles')">角色管理</el-button>
      </el-space>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive } from 'vue'
import { useRouter } from 'vue-router'
import { dashboardApi } from '@/api/dashboard'
import { useAuthStore } from '@/stores/auth'

const router = useRouter()
const auth = useAuthStore()

const stats = reactive({ userCount: 0, roleCount: 0 })

onMounted(async () => {
  const result = await dashboardApi.getStats()
  stats.userCount = result.userCount
  stats.roleCount = result.roleCount
})
</script>

<style scoped>
.welcome-card {
  margin-bottom: 16px;
}

.welcome-title {
  margin: 0 0 6px;
  font-size: 20px;
}

.welcome-sub {
  margin: 0;
  color: #909399;
}

.stat-row {
  margin-bottom: 16px;
}

.stat-label {
  color: #909399;
  font-size: 13px;
}

.stat-value {
  margin-top: 6px;
  font-size: 28px;
  font-weight: 600;
}
</style>
```

- [ ] **Step 4: 构建验证**

```bash
cd /m/ClaudeCode/AITrainingDemo/frontend
npm run build
```

Expected: 构建成功。

- [ ] **Step 5: 提交**

```bash
git add frontend/
git commit -m "feat: add app layout and dashboard home"
```

---

## Task 9: 用户管理页

**Files:**
- Create: `frontend/src/views/UsersView.vue`

**Interfaces:**
- Consumes: `usersApi.getPaged/create/update/remove`（Task 7）、`rolesApi.getAll`（Task 7）、`useAuthStore()`。
- Produces: 用户管理页 —— 搜索栏、表格、分页、新增/编辑弹窗、删除确认。

- [ ] **Step 1: 写页面 `src/views/UsersView.vue`**

```vue
<template>
  <el-card shadow="never">
    <el-form :inline="true" class="search-bar">
      <el-form-item label="关键字">
        <el-input
          v-model="query.keyword"
          placeholder="用户名 / 姓名 / 邮箱"
          clearable
          style="width: 220px"
          @keyup.enter="search"
        />
      </el-form-item>
      <el-form-item label="角色">
        <el-select v-model="query.roleId" placeholder="全部角色" clearable style="width: 160px">
          <el-option v-for="role in roleOptions" :key="role.id" :label="role.name" :value="role.id" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-button type="primary" :icon="Search" @click="search">查询</el-button>
        <el-button :icon="Refresh" @click="reset">重置</el-button>
      </el-form-item>
    </el-form>

    <div class="toolbar">
      <el-button type="primary" :icon="Plus" @click="openCreate">新增用户</el-button>
    </div>

    <el-table v-loading="loading" :data="rows" border stripe>
      <el-table-column prop="username" label="用户名" min-width="130" />
      <el-table-column prop="displayName" label="姓名" min-width="120" />
      <el-table-column prop="email" label="邮箱" min-width="180" />
      <el-table-column label="角色" min-width="110">
        <template #default="{ row }">
          <el-tag v-if="row.roleName" type="primary" effect="plain">{{ row.roleName }}</el-tag>
          <span v-else>-</span>
        </template>
      </el-table-column>
      <el-table-column label="状态" min-width="90">
        <template #default="{ row }">
          <el-tag :type="row.enabled ? 'success' : 'info'">{{ row.enabled ? '启用' : '停用' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="createdAt" label="创建时间" min-width="170" />
      <el-table-column label="操作" width="160" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" @click="openEdit(row)">编辑</el-button>
          <el-button link type="danger" @click="remove(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-pagination
      v-model:current-page="query.page"
      v-model:page-size="query.pageSize"
      class="pagination"
      :total="total"
      :page-sizes="[10, 20, 50]"
      layout="total, sizes, prev, pager, next, jumper"
      @size-change="load"
      @current-change="load"
    />

    <el-dialog v-model="dialogVisible" :title="form.id ? '编辑用户' : '新增用户'" width="480px">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="80px">
        <el-form-item label="用户名" prop="username">
          <el-input v-model="form.username" :disabled="!!form.id" placeholder="登录账号，创建后不可修改" />
        </el-form-item>
        <el-form-item label="姓名" prop="displayName">
          <el-input v-model="form.displayName" placeholder="显示名称" />
        </el-form-item>
        <el-form-item label="密码" prop="password">
          <el-input
            v-model="form.password"
            type="password"
            show-password
            :placeholder="form.id ? '留空表示不修改密码' : '初始密码'"
          />
        </el-form-item>
        <el-form-item label="邮箱" prop="email">
          <el-input v-model="form.email" placeholder="可选" />
        </el-form-item>
        <el-form-item label="角色" prop="roleId">
          <el-select v-model="form.roleId" placeholder="不分配角色" clearable style="width: 100%">
            <el-option v-for="role in roleOptions" :key="role.id" :label="role.name" :value="role.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="状态" prop="enabled">
          <el-switch v-model="form.enabled" active-text="启用" inactive-text="停用" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="save">保存</el-button>
      </template>
    </el-dialog>
  </el-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { Plus, Refresh, Search } from '@element-plus/icons-vue'
import { rolesApi } from '@/api/roles'
import { usersApi } from '@/api/users'
import { useAuthStore } from '@/stores/auth'
import type { RoleOptionDto, UserDto } from '@/types'

const auth = useAuthStore()

const loading = ref(false)
const saving = ref(false)
const dialogVisible = ref(false)
const formRef = ref<FormInstance>()

const rows = ref<UserDto[]>([])
const total = ref(0)
const roleOptions = ref<RoleOptionDto[]>([])

const query = reactive({ keyword: '', roleId: null as number | null, page: 1, pageSize: 20 })

const form = reactive({
  id: 0,
  username: '',
  password: '',
  displayName: '',
  email: '',
  roleId: null as number | null,
  enabled: true,
})

const rules: FormRules = {
  username: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
  displayName: [{ required: true, message: '请输入姓名', trigger: 'blur' }],
  password: [
    {
      validator: (_rule, value, callback) => {
        if (!form.id && !value) callback(new Error('请输入初始密码'))
        else callback()
      },
      trigger: 'blur',
    },
  ],
  email: [{ type: 'email', message: '邮箱格式不正确', trigger: 'blur' }],
}

async function load() {
  loading.value = true
  try {
    const result = await usersApi.getPaged({
      keyword: query.keyword || undefined,
      roleId: query.roleId,
      page: query.page,
      pageSize: query.pageSize,
    })
    rows.value = result.items
    total.value = result.total
  } finally {
    loading.value = false
  }
}

function search() {
  query.page = 1
  load()
}

function reset() {
  query.keyword = ''
  query.roleId = null
  query.page = 1
  load()
}

function openCreate() {
  Object.assign(form, {
    id: 0,
    username: '',
    password: '',
    displayName: '',
    email: '',
    roleId: null,
    enabled: true,
  })
  formRef.value?.clearValidate()
  dialogVisible.value = true
}

function openEdit(row: UserDto) {
  Object.assign(form, {
    id: row.id,
    username: row.username,
    password: '',
    displayName: row.displayName,
    email: row.email ?? '',
    roleId: row.roleId,
    enabled: row.enabled,
  })
  formRef.value?.clearValidate()
  dialogVisible.value = true
}

async function save() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return

  saving.value = true
  try {
    if (form.id) {
      await usersApi.update(form.id, {
        password: form.password || null,
        displayName: form.displayName,
        email: form.email || null,
        roleId: form.roleId,
        enabled: form.enabled,
      })
      ElMessage.success('修改成功')
    } else {
      await usersApi.create({
        username: form.username,
        password: form.password,
        displayName: form.displayName,
        email: form.email || null,
        roleId: form.roleId,
        enabled: form.enabled,
      })
      ElMessage.success('新增成功')
    }
    dialogVisible.value = false
    load()
  } catch {
    // 错误提示已由 axios 拦截器统一处理
  } finally {
    saving.value = false
  }
}

async function remove(row: UserDto) {
  if (row.id === auth.user?.id) {
    ElMessage.warning('不能删除当前登录用户')
    return
  }
  try {
    await ElMessageBox.confirm(`确定要删除用户「${row.displayName}」吗？`, '删除确认', {
      type: 'warning',
      confirmButtonText: '确定',
      cancelButtonText: '取消',
    })
  } catch {
    return
  }
  await usersApi.remove(row.id)
  ElMessage.success('删除成功')
  load()
}

onMounted(async () => {
  roleOptions.value = await rolesApi.getAll()
  await load()
})
</script>

<style scoped>
.search-bar {
  margin-bottom: 4px;
}

.toolbar {
  margin-bottom: 12px;
}

.pagination {
  margin-top: 16px;
  justify-content: flex-end;
}
</style>
```

- [ ] **Step 2: 构建验证**

```bash
cd /m/ClaudeCode/AITrainingDemo/frontend
npm run build
```

Expected: 构建成功。

- [ ] **Step 3: 提交**

```bash
git add frontend/
git commit -m "feat: add user management page"
```

---

## Task 10: 角色管理页

**Files:**
- Create: `frontend/src/views/RolesView.vue`

**Interfaces:**
- Consumes: `rolesApi.getPaged/getAll/create/update/remove`（Task 7）。
- Produces: 角色管理页 —— 搜索栏、表格（含关联用户数）、分页、新增/编辑弹窗、删除确认（被引用时禁用）。

- [ ] **Step 1: 写页面 `src/views/RolesView.vue`**

```vue
<template>
  <el-card shadow="never">
    <el-form :inline="true" class="search-bar">
      <el-form-item label="关键字">
        <el-input
          v-model="query.keyword"
          placeholder="角色编码 / 名称"
          clearable
          style="width: 220px"
          @keyup.enter="search"
        />
      </el-form-item>
      <el-form-item>
        <el-button type="primary" :icon="Search" @click="search">查询</el-button>
        <el-button :icon="Refresh" @click="reset">重置</el-button>
      </el-form-item>
    </el-form>

    <div class="toolbar">
      <el-button type="primary" :icon="Plus" @click="openCreate">新增角色</el-button>
    </div>

    <el-table v-loading="loading" :data="rows" border stripe>
      <el-table-column prop="code" label="编码" min-width="140" />
      <el-table-column prop="name" label="名称" min-width="140" />
      <el-table-column prop="description" label="描述" min-width="200" show-overflow-tooltip />
      <el-table-column label="关联用户" width="100">
        <template #default="{ row }">
          <el-tag :type="row.userCount > 0 ? 'warning' : 'info'" effect="plain">{{ row.userCount }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="createdAt" label="创建时间" min-width="170" />
      <el-table-column label="操作" width="160" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" @click="openEdit(row)">编辑</el-button>
          <el-button
            link
            type="danger"
            :disabled="row.userCount > 0"
            :title="row.userCount > 0 ? '该角色已被引用，无法删除' : ''"
            @click="remove(row)"
          >
            删除
          </el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-pagination
      v-model:current-page="query.page"
      v-model:page-size="query.pageSize"
      class="pagination"
      :total="total"
      :page-sizes="[10, 20, 50]"
      layout="total, sizes, prev, pager, next, jumper"
      @size-change="load"
      @current-change="load"
    />

    <el-dialog v-model="dialogVisible" :title="form.id ? '编辑角色' : '新增角色'" width="440px">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="80px">
        <el-form-item label="编码" prop="code">
          <el-input v-model="form.code" :disabled="!!form.id" placeholder="唯一标识，创建后不可修改" />
        </el-form-item>
        <el-form-item label="名称" prop="name">
          <el-input v-model="form.name" placeholder="角色名称" />
        </el-form-item>
        <el-form-item label="描述" prop="description">
          <el-input v-model="form.description" type="textarea" :rows="3" placeholder="可选" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="save">保存</el-button>
      </template>
    </el-dialog>
  </el-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { Plus, Refresh, Search } from '@element-plus/icons-vue'
import { rolesApi } from '@/api/roles'
import type { RoleDto } from '@/types'

const loading = ref(false)
const saving = ref(false)
const dialogVisible = ref(false)
const formRef = ref<FormInstance>()

const rows = ref<RoleDto[]>([])
const total = ref(0)

const query = reactive({ keyword: '', page: 1, pageSize: 20 })

const form = reactive({ id: 0, code: '', name: '', description: '' })

const rules: FormRules = {
  code: [{ required: true, message: '请输入角色编码', trigger: 'blur' }],
  name: [{ required: true, message: '请输入角色名称', trigger: 'blur' }],
}

async function load() {
  loading.value = true
  try {
    const result = await rolesApi.getPaged({
      keyword: query.keyword || undefined,
      page: query.page,
      pageSize: query.pageSize,
    })
    rows.value = result.items
    total.value = result.total
  } finally {
    loading.value = false
  }
}

function search() {
  query.page = 1
  load()
}

function reset() {
  query.keyword = ''
  query.page = 1
  load()
}

function openCreate() {
  Object.assign(form, { id: 0, code: '', name: '', description: '' })
  formRef.value?.clearValidate()
  dialogVisible.value = true
}

function openEdit(row: RoleDto) {
  Object.assign(form, {
    id: row.id,
    code: row.code,
    name: row.name,
    description: row.description ?? '',
  })
  formRef.value?.clearValidate()
  dialogVisible.value = true
}

async function save() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return

  saving.value = true
  try {
    if (form.id) {
      await rolesApi.update(form.id, { name: form.name, description: form.description || null })
      ElMessage.success('修改成功')
    } else {
      await rolesApi.create({
        code: form.code,
        name: form.name,
        description: form.description || null,
      })
      ElMessage.success('新增成功')
    }
    dialogVisible.value = false
    load()
  } catch {
    // 错误提示已由 axios 拦截器统一处理
  } finally {
    saving.value = false
  }
}

async function remove(row: RoleDto) {
  if (row.userCount > 0) {
    ElMessage.warning('该角色已被引用，无法删除')
    return
  }
  try {
    await ElMessageBox.confirm(`确定要删除角色「${row.name}」吗？`, '删除确认', {
      type: 'warning',
      confirmButtonText: '确定',
      cancelButtonText: '取消',
    })
  } catch {
    return
  }
  await rolesApi.remove(row.id)
  ElMessage.success('删除成功')
  load()
}

onMounted(load)
</script>

<style scoped>
.search-bar {
  margin-bottom: 4px;
}

.toolbar {
  margin-bottom: 12px;
}

.pagination {
  margin-top: 16px;
  justify-content: flex-end;
}
</style>
```

- [ ] **Step 2: 构建验证**

```bash
cd /m/ClaudeCode/AITrainingDemo/frontend
npm run build
```

Expected: 构建成功。

- [ ] **Step 3: 提交**

```bash
git add frontend/
git commit -m "feat: add role management page"
```

---

## Task 11: 端到端联调冒烟与运行说明

**Files:**
- Modify: `README.md`
- Verify: `backend/`、`frontend/` 全量运行

**Interfaces:**
- Consumes: Task 2–10 全部产出。
- Produces: 可复现的运行说明；一次通过的真实前后端联调记录。

- [ ] **Step 1: 跑全部后端测试与前端构建**

```bash
cd /m/ClaudeCode/AITrainingDemo
dotnet test backend/AITrainingDemo.Tests/AITrainingDemo.Tests.csproj
cd frontend && npm run build
```

Expected: 测试全部 PASS；前端构建成功。

- [ ] **Step 2: 启动后端**

```bash
cd /m/ClaudeCode/AITrainingDemo/backend/AITrainingDemo.Api
dotnet run
```

预期输出：`Now listening on: http://localhost:5080`。

- [ ] **Step 3: 用 curl 验证登录接口**

```bash
curl -s -X POST http://localhost:5080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}'
```

预期返回：`{"code":0,"message":"ok","data":{"token":"eyJ...","expiresAt":"...","user":{"id":1,...}}}`。

- [ ] **Step 4: 用 curl 验证受保护接口（不带 token 应 401）**

```bash
curl -s -o /dev/null -w "%{http_code}" http://localhost:5080/api/users
```

预期：`401`。

- [ ] **Step 5: 启动前端并做浏览器冒烟**

```bash
cd /m/ClaudeCode/AITrainingDemo/frontend
npm run dev
```

浏览器打开 `http://localhost:5173`，逐条走查并记录结果：

| # | 操作 | 期望 |
|---|---|---|
| 1 | 未登录直接访问 `/dashboard` | 自动跳转到 `/login` |
| 2 | 输入错误密码点登录 | 提示「用户名或密码错误」 |
| 3 | 用 `admin/admin123` 登录 | 进入工作台首页，统计卡数字与数据库实际条数一致 |
| 4 | 左侧点「用户管理」 | 表格展示 admin / editor 两条记录，角色列显示角色名 |
| 5 | 新增用户（填用户名、姓名、密码、选角色） | 提示新增成功，表格出现新行 |
| 6 | 编辑该用户姓名 | 表格同步更新 |
| 7 | 删除该用户 | 提示删除成功，行消失 |
| 8 | 在角色管理页新增角色 `auditor` | 表格出现，关联用户数为 0，删除按钮可用 |
| 9 | 删除 `auditor` | 删除成功 |
| 10 | 删除 `admin` 角色 | 提示「该角色已被 1 个用户引用，无法删除」，按钮为禁用态 |
| 11 | 刷新浏览器 | 仍处于登录态（token 已持久化），且不再重复调用 `/api/auth/me` 报错 |
| 12 | 顶栏用户下拉 → 退出登录 | 回到 `/login`，再访问 `/users` 跳回 `/login` |
| 13 | 停止后端再启动 | 数据仍在（SQLite 文件持久化） |

> 第 13 条验证时注意 `backend/AITrainingDemo.Api/App_Data/workbench.db` 已生成且未被 git 跟踪。

- [ ] **Step 6: 补全 `README.md`**

用以下内容覆盖 `README.md`：

````markdown
# AITrainingDemo · 工作台

一个「工作台」基础应用：.NET 10 WebAPI + EF Core + SQLite 后端，Vue 3 + Element Plus + Axios 前端。
除 SQLite 文件外**不依赖任何外部服务**。

## 功能

- 登录页（JWT 鉴权，Token 持久化到 localStorage）
- 工作台首页：用户总数 / 角色总数统计、快捷入口
- 用户管理：搜索、分页、新增、编辑、删除
- 角色管理：搜索、分页、新增、编辑、删除（被用户引用时禁止删除）
- 响应式布局（左侧菜单 + 顶栏）

## 技术栈

| 层 | 选型 |
|---|---|
| 后端 | .NET 10 (`net10.0`)、EF Core 10、SQLite、JWT Bearer |
| 前端 | Vue 3 + TypeScript + Vite + Element Plus + Vue Router + Pinia + Axios |
| 测试 | xUnit + `WebApplicationFactory`（SQLite 内存库） |

## 环境要求

- .NET SDK 10.x
- Node.js 20+（含 npm）

## 启动后端

```bash
cd backend/AITrainingDemo.Api
dotnet restore
dotnet run
```

后端监听 `http://localhost:5080`。首次启动会自动创建 `App_Data/workbench.db` 并写入种子数据。

> ⚠️ 必须在 `backend/AITrainingDemo.Api` 目录下启动后端。SQLite 连接串中的路径是相对当前工作目录的，从仓库根目录启动会把数据库建到错误位置。

### 生产部署注意

- **必须用环境变量覆盖 `Jwt:SigningKey`**，例如 `Jwt__SigningKey`。仓库内 `appsettings.json` 中的签名密钥仅供开发演示。
- **`EnsureCreated()` 不会演进既有表结构**。修改实体字段后需要删除 `App_Data/workbench.db` 重建数据库，种子数据会自动重新写入。

## 启动前端

```bash
cd frontend
npm install
npm run dev
```

前端监听 `http://localhost:5173`，通过 Vite proxy 把 `/api` 转发到 `http://localhost:5080`。

## 默认账号

| 用户名 | 密码 | 角色 |
|---|---|---|
| admin | admin123 | 管理员 |
| editor | editor123 | 编辑者 |

## 运行测试

```bash
dotnet test backend/AITrainingDemo.Tests/AITrainingDemo.Tests.csproj
```

## 目录结构

```
backend/
  AITrainingDemo.Api/     Controller / Service / Entity / DTO / Common / Data
  AITrainingDemo.Tests/   xUnit 集成测试
frontend/
  src/api/                axios 实例与接口封装
  src/stores/             Pinia 状态
  src/router/             路由与登录守卫
  src/layout/             应用布局
  src/views/              页面
docs/superpowers/         设计文档与实施计划
```

项目规范见 [`.claude/CLAUDE.md`](.claude/CLAUDE.md)。
````

- [ ] **Step 7: 确认没有把数据库文件提交进仓库**

```bash
git status --porcelain
```

预期：输出中不包含 `backend/AITrainingDemo.Api/App_Data/` 下的任何文件（已被 `.gitignore` 忽略）。

- [ ] **Step 8: 提交**

```bash
git add README.md
git commit -m "docs: add project readme with run instructions"
```

---

## 附:任务依赖顺序

```
Task 1 (骨架/规范)
  └─ Task 2 (后端脚手架/实体/种子)
       ├─ Task 3 (JWT 登录)  ──┐
       │    └─ Task 4 (用户 CRUD) ──┬─ Task 6 (统计接口 + CORS)
       │         └─ Task 5 (角色 CRUD) ──┘
       └──────────────────────────────┐
Task 7 (前端脚手架/登录页) ───────────┴─ Task 8 (布局/首页)
                                        └─ Task 9 (用户页)
                                             └─ Task 10 (角色页)
                                                  └─ Task 11 (端到端冒烟 + README)
```

Task 2 → 6 可按序执行；Task 7 → 10 依赖 Task 2 的后端可运行（联调需 Task 6 完成）。


