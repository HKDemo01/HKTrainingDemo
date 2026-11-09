import request from './request'
import type { ApiResult, LoginRequest, LoginResponse, LoginUserInfo } from '@/types'

export const authApi = {
  login(payload: LoginRequest) {
    return request.post<ApiResult<LoginResponse>>('/auth/login', payload).then((r) => r.data.data)
  },
  me() {
    return request.get<ApiResult<LoginUserInfo>>('/auth/me').then((r) => r.data.data)
  },
}
