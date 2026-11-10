# AITrainingDemo · 工作台

一个「工作台」基础应用：.NET 10 WebAPI + EF Core + SQLite 后端，Vue 3 + Element Plus + Axios 前端。
除 SQLite 文件外**不依赖任何外部服务**。

## 功能

- 登录页（JWT 鉴权，Token 持久化到 localStorage）
- 工作台首页：用户总数 / 角色总数统计、快捷入口
- 用户管理：搜索、分页、新增、编辑、删除
- 角色管理：搜索、分页、新增、编辑、删除（被用户引用时禁止删除）
- 响应式布局（左侧菜单 + 顶栏，≤768px 时菜单折叠为抽屉）

## 技术栈

| 层 | 选型 |
|---|---|
| 后端 | .NET 10 (`net10.0`)、EF Core 10、SQLite、JWT Bearer |
| 接口文档 | 内置 OpenAPI 生成器 + Scalar 交互式 UI（仅开发环境） |
| 前端 | Vue 3 + TypeScript + Vite + Element Plus + Vue Router + Pinia + Axios |
| 测试 | xUnit + `WebApplicationFactory`（SQLite 内存库；并发用例改用临时文件库） |

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

### 接口文档

后端用 .NET 10 内置的 OpenAPI 生成器产出规范，再由 Scalar 提供交互式 UI：

| 地址 | 内容 |
|---|---|
| <http://localhost:5080/scalar> | Scalar 交互式接口文档（可直接发请求调试） |
| <http://localhost:5080/openapi/v1.json> | OpenAPI 3.1 规范 JSON |

调试受保护接口的步骤：先在 Scalar 里调 `POST /api/auth/login` 拿到 `token`，再在任意接口的 **Auth → Bearer** 里填入该 token（也可以在 **Headers** 里手填 `Authorization: Bearer <token>`）。

> 接口文档**只在 Development 环境暴露**，生产环境不提供——文档是无鉴权的公开资源，对外提供等于泄露接口清单。

### 生产部署注意

- **必须用环境变量提供 `Jwt:SigningKey`**，例如 `Jwt__SigningKey`。仓库内的签名密钥只存在于 `appsettings.Development.json`（仅开发环境加载，且**不会**随 `dotnet publish` 进入发布产物）。
- 后端有三重启动保护：密钥缺失或短于 32 字节 → 拒绝启动；**非 Development 环境检测到仓库内的演示密钥 → 拒绝启动**；Development 环境使用演示密钥 → 启动日志显式告警（该密钥是公开的，token 可被伪造）。
- **`EnsureCreated()` 不会演进既有表结构**。修改实体字段后需要删除 `App_Data/workbench.db` 重建数据库，种子数据会自动重新写入。
- **生产环境不提供接口文档**：`/scalar` 与 `/openapi/v1.json` 仅在 Development 环境注册。

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

## 已知限制

- **角色编码区分大小写**：SQLite 唯一索引默认按二进制比较，`ADMIN` 与 `admin` 视为两个不同角色；关键字搜索同样区分大小写。设计文档未定义编码的大小写策略，故保持现状未做归一化。
- **SQLite 不强制字段长度**：应用层已按模型约束校验用户名 / 姓名 / 角色编码 / 角色名称不超过 50 字符，但数据库层面不会截断或拒绝更长的值。
- **登录接口没有限流**：项目规范禁止引入 Redis / MQ / 外部服务，未做失败次数锁定，弱口令仍可被暴力尝试。生产部署应在网关层补充限流。
- **角色编码创建后不可修改**：角色编码是唯一标识，编辑弹窗中该输入框为禁用态。
- **所有登录用户权限相同**：角色字段（`admin` / `editor`）目前只用于展示与「角色被引用时禁止删除」的判定，**不参与接口鉴权**——设计文档 §1.2 明确把「RBAC 权限判定」列为本次范围外。因此任何一个登录成功的用户都能增删改全部用户与角色，不能当作权限边界使用。
- **用户名也区分大小写**：与角色编码同理，SQLite 默认按二进制比较，`Admin` 与 `admin` 是两个不同的账号，登录与关键字搜索都区分大小写。
- **更新用户是整量替换（PUT）语义**：请求体必须带全字段。漏传 `enabled` 会被当作 `false`（账号被停用），漏传 `roleId` 会清空该用户的角色（`roleId: null` 表示「不分配角色」，是有效取值）。前端始终带全字段，直接调用接口时需注意。

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
