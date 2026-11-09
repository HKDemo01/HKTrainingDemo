<template>
  <el-container class="app-layout">
    <el-aside v-if="!isMobile" width="210px" class="app-aside">
      <div class="app-logo">工作台</div>
      <AppMenu />
    </el-aside>

    <el-drawer
      v-else
      v-model="drawerVisible"
      body-class="app-drawer-body"
      direction="ltr"
      size="210px"
      :with-header="false"
    >
      <div class="app-logo">工作台</div>
      <AppMenu @select="drawerVisible = false" />
    </el-drawer>

    <el-container>
      <el-header class="app-header">
        <div class="app-header-left">
          <el-button v-if="isMobile" class="app-menu-toggle" link @click="drawerVisible = true">
            <el-icon><Menu /></el-icon>
          </el-button>
          <span class="app-header-title">{{ route.meta.title }}</span>
        </div>

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
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { ArrowDown, Menu } from '@element-plus/icons-vue'
import { useAuthStore } from '@/stores/auth'
import AppMenu from './AppMenu.vue'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

const drawerVisible = ref(false)

// 窄屏（≤768px）时把左侧菜单收进抽屉
const mobileQuery = window.matchMedia('(max-width: 768px)')
const isMobile = ref(mobileQuery.matches)

function onMediaChange(event: MediaQueryListEvent) {
  isMobile.value = event.matches
  if (!event.matches) {
    drawerVisible.value = false
  }
}

onMounted(async () => {
  mobileQuery.addEventListener('change', onMediaChange)
  // 刷新页面后用持久化的 token 校验一次会话有效性；
  // token 失效时 axios 拦截器会清空登录态并跳回 /login
  await auth.fetchMe()
})

onBeforeUnmount(() => {
  mobileQuery.removeEventListener('change', onMediaChange)
})

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

.app-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  background: #fff;
  border-bottom: 1px solid #e6e6e6;
}

.app-header-left {
  display: flex;
  align-items: center;
  gap: 8px;
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

/* 抽屉内的菜单需要铺满，去掉默认内边距；抽屉内容由子组件渲染，
   不带父组件的 scoped 属性，只能用全局选择器命中 */
:global(.app-drawer-body) {
  padding: 0;
}
</style>
