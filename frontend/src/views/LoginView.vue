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
