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
