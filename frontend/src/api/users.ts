import request from './request'
import type { ApiResult, CreateUserRequest, PagedResult, UpdateUserRequest, UserDto } from '@/types'

export interface UserQueryParams {
  keyword?: string
  roleId?: number | null
  page?: number
  pageSize?: number
}

export const usersApi = {
  getPaged(params: UserQueryParams) {
    return request.get<ApiResult<PagedResult<UserDto>>>('/users', { params }).then((r) => r.data.data)
  },
  create(payload: CreateUserRequest) {
    return request.post<ApiResult<UserDto>>('/users', payload).then((r) => r.data.data)
  },
  update(id: number, payload: UpdateUserRequest) {
    return request.put<ApiResult<UserDto>>(`/users/${id}`, payload).then((r) => r.data.data)
  },
  remove(id: number) {
    return request.delete<ApiResult<object>>(`/users/${id}`).then((r) => r.data.data)
  },
}
