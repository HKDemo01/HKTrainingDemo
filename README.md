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

## 已知限制

- **角色编码区分大小写**：SQLite 唯一索引默认按二进制比较，`ADMIN` 与 `admin` 视为两个不同角色；关键字搜索同样区分大小写。设计文档未定义编码的大小写策略，故保持现状未做归一化。
- **SQLite 不强制字段长度**：应用层已按模型约束校验用户名 / 姓名 / 角色编码 / 角色名称不超过 50 字符，但数据库层面不会截断或拒绝更长的值。
- **登录接口没有限流**：项目规范禁止引入 Redis / MQ / 外部服务，未做失败次数锁定，弱口令仍可被暴力尝试。生产部署应在网关层补充限流。
- **角色编码创建后不可修改**：角色编码是唯一标识，编辑弹窗中该输入框为禁用态。

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
