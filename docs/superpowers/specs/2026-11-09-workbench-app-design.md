# 「工作台」基础应用 — 设计文档

- 日期：2026-11-09
- 状态：已与需求方确认设计，待复核
- 仓库：`M:\ClaudeCode\AITrainingDemo`（当前为空仓库，仅含 `.git`）

---

## 1. 目标与范围

### 1.1 目标

搭建一个可直接运行、可作为后续业务扩展基础的「工作台」应用：

- **后端**：.NET 10 WebAPI + EF Core + SQLite，不依赖任何外部服务（数据库为进程内文件，无 Redis/MQ/云服务）。
- **前端**：Vue 3 + Element Plus + Axios，调用后端 REST API。
- **功能**：登录页 + 工作台首页 + 「用户」「角色」两个菜单的完整增删改查。

### 1.2 明确不在范围内

以下内容本次不实现，避免投机性设计：

- 用户注册、找回密码、验证码、第三方登录
- RBAC 权限判定（角色字段先存在，暂不参与接口鉴权）
- 审计日志、操作记录、软删除、多租户
- 文件上传/下载、消息通知、WebSocket
- 前端单元测试框架、端到端自动化测试（Playwright 等）
- Docker/容器化、CI/CD、HTTPS 证书
- 多项目后端拆分（Core / Api / Infrastructure）

### 1.3 成功标准

1. `dotnet build` 与 `npm run build` 均无错误通过。
2. 后端测试套件全部通过（登录、用户 CRUD、角色删除冲突等核心用例）。
3. 浏览器中可完整走通：登录 → 工作台首页看到真实统计 → 新增/编辑/删除用户 → 新增/编辑/删除角色 → 退出登录。
4. 后端进程重启后数据仍在（SQLite 文件持久化）。
5. 仓库内存在 `.claude/CLAUDE.md`，含全局通用规范与本项目技术栈约定。

---

## 2. 架构总览

```
浏览器 (Vue 3 + Element Plus)
        │  Axios  /api/*  (Vite dev proxy → http://localhost:5080)
        ▼
ASP.NET Core WebAPI (.NET 10)
        │  JWT Bearer 鉴权
        ├── AuthController      登录 / 当前用户
        ├── UsersController    用户 CRUD
        └── RolesController    角色 CRUD
        │
        ▼
EF Core DbContext ──► SQLite 文件 (backend/AITrainingDemo.Api/App_Data/workbench.db)
```

单一 ASP.NET Core 进程托管 API；前端为独立 Vite 开发服务器，通过代理访问 API。后端同时开启 CORS，允许 `http://localhost:5173` 直连。

**分层约定（后端）**

```
Controllers  →  HTTP 边界：参数接收、状态码、鉴权特性，不写业务逻辑
Services     →  业务规则：校验、哈希、关联检查、分页查询
Entities     →  EF 实体与 DbContext
Common       →  ApiResult<T> 统一响应包装、全局异常过滤器
```

---

## 3. 后端设计

### 3.1 项目结构

```
backend/
├─ AITrainingDemo.sln
├─ AITrainingDemo.Api/
│  ├─ AITrainingDemo.Api.csproj
│  ├─ Program.cs
│  ├─ appsettings.json
│  ├─ appsettings.Development.json
│  ├─ Common/
│  │  ├─ ApiResult.cs
│  │  └─ ApiExceptionFilter.cs
│  ├─ Entities/
│  │  ├─ User.cs
│  │  └─ Role.cs
│  ├─ Data/
│  │  ├─ AppDbContext.cs
│  │  └─ DbInitializer.cs
│  ├─ Dtos/
│  │  ├─ AuthDtos.cs
│  │  ├─ UserDtos.cs
│  │  └─ RoleDtos.cs
│  ├─ Services/
│  │  ├─ IPasswordHasher.cs / PasswordHasher.cs
│  │  ├─ IAuthService.cs / AuthService.cs
│  │  ├─ IUserService.cs / UserService.cs
│  │  └─ IRoleService.cs / RoleService.cs
│  ├─ Controllers/
│  │  ├─ AuthController.cs
│  │  ├─ UsersController.cs
│  │  ├─ RolesController.cs
│  │  └─ DashboardController.cs
│  └─ App_Data/            # SQLite 数据库文件（gitignore）
└─ AITrainingDemo.Tests/
   ├─ AITrainingDemo.Tests.csproj
   └─ AuthApiTests.cs, UserApiTests.cs, RoleApiTests.cs
```

### 3.2 NuGet 依赖

| 包 | 用途 |
|---|---|
| `Microsoft.EntityFrameworkCore.Sqlite` | EF Core + SQLite 提供程序 |
| `Microsoft.EntityFrameworkCore.Design` | 设计时（可选，为将来接 Migrations 预留） |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | JWT Bearer 鉴权 |
| `System.IdentityModel.Tokens.Jwt` | JWT 签发 |
| `Microsoft.AspNetCore.Mvc.Testing` | 测试项目 WebApplicationFactory |
| `xunit` / `xunit.runner.visualstudio` | 测试框架 |

> 不使用 `dotnet-ef` 全局工具：建库使用 `EnsureCreated()` + 幂等种子数据，启动即用，无需额外工具与迁移文件，降低对环境的依赖。

### 3.3 数据模型

**Role**

| 字段 | 类型 | 约束 |
|---|---|---|
| Id | int | 主键，自增 |
| Code | string | 必填，唯一索引 |
| Name | string | 必填 |
| Description | string? | 可空 |
| CreatedAt | DateTime | 必填 |

**User**

| 字段 | 类型 | 约束 |
|---|---|---|
| Id | int | 主键，自增 |
| Username | string | 必填，唯一索引 |
| PasswordHash | string | 必填，格式 `PBKDF2` 派生值，不返回前端 |
| DisplayName | string | 必填 |
| Email | string? | 可空 |
| RoleId | int? | 外键 → Role.Id，可空 |
| Role | Role? | 导航属性 |
| Enabled | bool | 默认 true |
| CreatedAt | DateTime | 必填 |

**删除行为**：删除 Role 时若存在关联 User → 拒绝（409）。删除 User 不影响 Role。

**密码存储**：`PasswordHasher` 使用 `Rfc2898DeriveBytes.Pbkdf2`（SHA256、100000 迭代、16 字节随机盐），存储格式 `"{saltBase64}.{hashBase64}"`。校验使用固定时间比较。

### 3.4 种子数据

`DbInitializer.Initialize()` 在应用启动时执行，幂等（按 `Code` / `Username` 判断是否已存在）：

| Username | Password | Role Code | DisplayName |
|---|---|---|---|
| admin | admin123 | admin | 管理员 |
| editor | editor123 | editor | 编辑者 |

角色种子：`admin`（管理员）、`editor`（编辑者）。

### 3.5 API 契约

**统一响应包装**（所有接口返回，含错误）：

```jsonc
// 成功
{ "code": 0, "message": "ok", "data": { } }

// 失败
{ "code": 409, "message": "该角色已被 3 个用户引用，无法删除", "data": null }
```

`code` 语义：`0` 成功；非 0 为业务/HTTP 状态码。HTTP 状态码与 `code` 保持一致。

**分页数据结构**：

```jsonc
{ "items": [], "total": 0, "page": 1, "pageSize": 20 }
```

**端点清单**

| 方法 | 路径 | 鉴权 | 说明 |
|---|---|---|---|
| POST | `/api/auth/login` | 否 | 用户名 + 密码登录 |
| GET | `/api/auth/me` | 是 | 当前登录用户信息 |
| GET | `/api/users` | 是 | 分页 + 关键字 + 角色筛选 |
| POST | `/api/users` | 是 | 新增用户 |
| PUT | `/api/users/{id}` | 是 | 修改用户（密码留空则不修改） |
| DELETE | `/api/users/{id}` | 是 | 删除用户（禁删自己） |
| GET | `/api/roles` | 是 | 角色分页列表（管理页用） |
| GET | `/api/roles/all` | 是 | 全量角色数组（下拉框用，不分页） |
| POST | `/api/roles` | 是 | 新增角色 |
| PUT | `/api/roles/{id}` | 是 | 修改角色 |
| DELETE | `/api/roles/{id}` | 是 | 删除角色（有引用 → 409） |
| GET | `/api/dashboard/stats` | 是 | `{ userCount, roleCount }` 工作台统计 |

> 角色下拉用独立的 `/api/roles/all` 而非复用分页端点，保证 `GET /api/roles` 的响应结构恒为分页对象，前端无需按参数分支判断。

**用户列表查询参数**

| 参数 | 类型 | 说明 |
|---|---|---|
| keyword | string? | 模糊匹配 Username / DisplayName / Email |
| roleId | int? | 按角色筛选 |
| page | int | 默认 1 |
| pageSize | int | 默认 20，最大 100 |

**角色列表查询参数**：`keyword`（模糊匹配 Code / Name）、`page`、`pageSize`（同上）。

**关键 DTO**

- `UserDto`：`id, username, displayName, email, roleId, roleName, enabled, createdAt`（不含 PasswordHash）
- `RoleDto`：`id, code, name, description, userCount, createdAt`（`userCount` 为关联用户数，供列表展示与删除校验）
- `RoleOptionDto`：`id, code, name`（下拉框用，简化字段）
- `DashboardStatsDto`：`userCount, roleCount`

**登录请求/响应**

```jsonc
// POST /api/auth/login
// { "username": "admin", "password": "admin123" }

// 响应 data
{ "token": "<jwt>", "expiresAt": "2026-11-09T23:00:00Z", "user": { "id":1, "username":"admin", "displayName":"管理员", "roleName":"管理员" } }
```

**JWT 配置**：HS256，密钥取自 `Jwt:SigningKey`（`appsettings.Development.json`，非敏感演示值；生产需换环境变量），有效期 8 小时，Claim 包含 `sub`（用户 Id）、`name`（用户名）、`role`（角色 Code）。

### 3.6 错误处理

- 全局 `ApiExceptionFilter`（`IExceptionFilter`）捕获未处理异常 → 500，返回统一结构，异常细节不外泄。
- 业务错误：Service 层抛 `BusinessException(code, message)`，由过滤器转成对应 HTTP 状态码。
- 模型校验失败：配置 `ApiBehaviorOptions.InvalidModelStateResponseFactory`，返回 `code: 400` 的统一结构。
- 401（未登录/Token 失效）由 JwtBearer 处理器直接返回，前端 Axios 拦截器识别 401 后清 token 跳登录页。

---

## 4. 前端设计

### 4.1 技术栈与结构

- Vite + Vue 3（`<script setup>`）+ TypeScript + Element Plus + Vue Router 4 + Pinia + Axios
- 目录：

```
frontend/
├─ vite.config.ts        # dev server + /api proxy
├─ index.html
├─ src/
│  ├─ main.ts
│  ├─ App.vue
│  ├─ api/
│  │  ├─ request.ts      # axios 实例 + 拦截器
│  │  ├─ auth.ts
│  │  ├─ users.ts
│  │  └─ roles.ts
│  ├─ stores/auth.ts     # token + 用户信息 + 持久化
│  ├─ router/index.ts    # 路由表 + 守卫
│  ├─ layout/AppLayout.vue
│  └─ views/
│     ├─ LoginView.vue
│     ├─ DashboardView.vue
│     ├─ UsersView.vue
│     └─ RolesView.vue
```

### 4.2 Axios 与鉴权

- `baseURL: '/api'`，前端始终走相对路径，由 Vite proxy 转发，避免硬编码后端地址。
- 请求拦截器：存在 token 时附加 `Authorization: Bearer <token>`。
- 响应拦截器：
  - `code !== 0` → `ElMessage.error(message)`，把错误以 `Promise.reject` 抛出，页面可自行处理。
  - HTTP 401 → 清空 auth store 的 token，重定向到 `/login`。
- token 存储：`localStorage` 键 `workbench_token`，用户信息键 `workbench_user`。

### 4.3 路由与守卫

| 路径 | 组件 | meta |
|---|---|---|
| `/login` | LoginView | 免鉴权；已登录访问则重定向 `/` |
| `/` | AppLayout（重定向到 `/dashboard`） | 需鉴权 |
| `/dashboard` | DashboardView | 需鉴权 |
| `/users` | UsersView | 需鉴权 |
| `/roles` | RolesView | 需鉴权 |

守卫逻辑：`to.meta.requiresAuth !== false && !authStore.token` → 跳 `/login?redirect=<原路径>`。

### 4.4 页面设计

**登录页**
居中卡片，标题「工作台」，用户名/密码输入框 + 表单必填校验 + 密码显示切换，登录按钮带 loading 态。提交成功 → 存 token 与用户信息 → 跳 `redirect` 或 `/dashboard`。失败 → ElMessage 提示后端 message。

**工作台首页（Dashboard）**
- 顶部欢迎语：「你好，{displayName}」+ 当前角色。
- 两张统计卡片：用户总数、角色总数，来自 `GET /api/dashboard/stats`。
- 快捷入口卡片：跳转「用户管理」「角色管理」。

**用户管理页**
- 顶部搜索栏：关键字输入 + 角色下拉（来自 `GET /api/roles/all`）+ 查询/重置按钮。
- 表格列：用户名、姓名、邮箱、角色（tag）、状态（tag：启用/停用）、创建时间、操作（编辑/删除）。
- 分页：el-pagination，默认每页 20。
- 新增/编辑弹窗：用户名（编辑时只读）、姓名、邮箱、角色下拉、状态开关、密码（新增必填，编辑留空表示不修改）。
- 删除：`ElMessageBox.confirm` 二次确认后调用删除接口。

**角色管理页**
- 搜索栏：角色编码/名称关键字 + 查询/重置。
- 表格列：编码、名称、描述、关联用户数、创建时间、操作（编辑/删除）。
- 新增/编辑弹窗：编码（编辑时只读）、名称、描述。
- 删除：有关联用户时按钮禁用，hover 提示「该角色已被引用，无法删除」。

**布局（AppLayout）**
`el-container`：左侧 `el-aside` 深色菜单（工作台 / 用户管理 / 角色管理，当前项高亮），右侧 `el-container` 含 `el-header`（面包屑标题 + 右侧用户下拉：显示姓名 → 退出登录）与 `el-main`（`<router-view>`）。菜单在 ≤768px 折叠为抽屉。

### 4.5 联调配置

- Vite `server.port = 5173`，`server.proxy['/api'] = { target: 'http://localhost:5080', changeOrigin: true }`。
- 后端 CORS：`AllowAnyHeader` / `AllowAnyMethod` / `WithOrigins("http://localhost:5173")`。
- 后端监听 `http://localhost:5080`（`applicationUrl`）。

---

## 5. 测试策略

**后端（xUnit + WebApplicationFactory + SQLite 内存库）**

| 用例 | 期望 |
|---|---|
| 正确用户名密码登录 | 200，返回非空 token 与用户信息 |
| 错误密码登录 | 401，`code` 非 0 |
| 用户名不存在登录 | 401 |
| 新增用户 | 200（`data` 直接返回新建用户，可通过查询接口取回） |
| 新增重复用户名 | 409 |
| 分页查询用户 | 返回 `total` 与 `items` 长度正确 |
| 修改用户 | 修改后查询字段已更新 |
| 删除用户 | 删除后再查返回 404 |
| 删除当前登录用户 | 400 拒绝 |
| 删除被引用的角色 | 409 |
| 删除无引用角色 | 200 |
| 未带 Token 访问受保护接口 | 401 |

测试通过 `WebApplicationFactory` 覆盖 `Program`（使用 `public partial class Program { }`），并用测试替身把 SQLite 换成内存库（`DataSource=:memory:` + 保持连接），保证互不干扰。

**前端**：不引入单测框架（超出本次范围且与「最小代码」原则冲突）。验证方式 = `npm run build` 通过 + 浏览器端到端手工冒烟清单。

**端到端冒烟清单**（人工执行，记录结果）
1. 未登录访问 `/dashboard` → 跳 `/login`。
2. 错误密码 → 提示「用户名或密码错误」。
3. `admin/admin123` 登录 → 进入工作台，统计数字与库中实际条数一致。
4. 新增用户（分配角色）→ 列表出现该行。
5. 编辑该用户姓名与角色 → 列表同步更新。
6. 删除该用户 → 列表消失。
7. 新增角色 → 出现；删除该角色 → 成功（无引用）。
8. 给用户分配该角色后再删角色 → 提示被引用，无法删除。
9. 刷新浏览器 → 仍处于登录态（token 持久化）。
10. 退出登录 → 回到登录页，再访问 `/users` 跳回 `/login`。
11. 重启后端 → 数据仍在。

---

## 6. 风险与前置条件

| 风险 | 影响 | 应对 |
|---|---|---|
| 当前会话 Bash 命令被权限模式拦截 | 无法执行 `dotnet` / `npm` 命令 | 开工前切换到允许命令执行的权限模式 |
| 本机可能未安装 .NET 10 SDK | 无法构建后端 | 开工第一步先 `dotnet --list-sdks` 验证；若缺失需先安装 SDK |
| SQLite 文件随二进制目录生成 | 可能误提交 | `App_Data/` 加入 `.gitignore` |
| NuGet / npm 需联网下载 | 首次还原可能较慢或失败 | 依赖版本固定，失败时重试或改用本地缓存 |

**环境前置**：.NET 10 SDK、Node.js 20+（含 npm）。

---

## 7. 交付物清单

- `.claude/CLAUDE.md` — 项目规范（全局通用规范 + 本项目技术栈约定）
- `backend/` — 可 `dotnet build` / `dotnet test` 通过的 WebAPI + 测试项目
- `frontend/` — 可 `npm run build` 通过、且通过 Vite proxy 与后端联调的前端
- `docs/superpowers/specs/2026-11-09-workbench-app-design.md` — 本设计文档
- `docs/superpowers/plans/` — 实施计划文档
- `.gitignore` — 忽略 `bin/`, `obj/`, `node_modules/`, `dist/`, `App_Data/`
- `README.md` — 如何安装依赖、启动后端/前端、种子账号说明
