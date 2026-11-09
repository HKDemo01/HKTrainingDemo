# 工作台前端

Vue 3 + TypeScript + Vite + Element Plus + Vue Router 4 + Pinia + Axios。

## 常用命令

```bash
npm install     # 安装依赖
npm run dev     # 本地开发（http://localhost:5173）
npm run build   # 类型检查 + 生产构建
```

## 约定

- 接口一律走相对路径 `/api`，由 `vite.config.ts` 的 proxy 转发到 `http://localhost:5080`。
- 所有 HTTP 请求必须使用 `@/api/request.ts` 导出的 axios 实例，组件内禁止直接 `import axios`。
- 接口封装放在 `src/api/*.ts`，类型定义放在 `src/types/index.ts`。
- 登录态存于 `localStorage`，键名为 `workbench_token` 与 `workbench_user`。
