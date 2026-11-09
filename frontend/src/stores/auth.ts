import { defineStore } from 'pinia'
import { authApi } from '@/api/auth'
import type { LoginUserInfo } from '@/types'

const TOKEN_KEY = 'workbench_token'
const USER_KEY = 'workbench_user'

export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: localStorage.getItem(TOKEN_KEY) ?? '',
    user: JSON.parse(localStorage.getItem(USER_KEY) ?? 'null') as LoginUserInfo | null,
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
