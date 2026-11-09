import request from './request'
import type { ApiResult, CreateRoleRequest, PagedResult, RoleDto, RoleOptionDto, UpdateRoleRequest } from '@/types'

export interface RoleQueryParams {
  keyword?: string
  page?: number
  pageSize?: number
}

export const rolesApi = {
  getPaged(params: RoleQueryParams) {
    return request.get<ApiResult<PagedResult<RoleDto>>>('/roles', { params }).then((r) => r.data.data)
  },
  getAll() {
    return request.get<ApiResult<RoleOptionDto[]>>('/roles/all').then((r) => r.data.data)
  },
  create(payload: CreateRoleRequest) {
    return request.post<ApiResult<RoleDto>>('/roles', payload).then((r) => r.data.data)
  },
  update(id: number, payload: UpdateRoleRequest) {
    return request.put<ApiResult<RoleDto>>(`/roles/${id}`, payload).then((r) => r.data.data)
  },
  remove(id: number) {
    return request.delete<ApiResult<object>>(`/roles/${id}`).then((r) => r.data.data)
  },
}
