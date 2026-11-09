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
  try {
    const result = await dashboardApi.getStats()
    stats.userCount = result.userCount
    stats.roleCount = result.roleCount
  } catch {
    // 错误提示已由 axios 拦截器统一处理
  }
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
