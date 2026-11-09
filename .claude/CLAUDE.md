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
