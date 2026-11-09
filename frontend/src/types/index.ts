export interface ApiResult<T> {
  code: number
  message: string
  data: T
}

export interface PagedResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

export interface LoginRequest {
  username: string
  password: string
}

export interface LoginUserInfo {
  id: number
  username: string
  displayName: string
  roleName: string | null
}

export interface LoginResponse {
  token: string
  expiresAt: string
  user: LoginUserInfo
}

export interface UserDto {
  id: number
  username: string
  displayName: string
  email: string | null
  roleId: number | null
  roleName: string | null
  enabled: boolean
  createdAt: string
}

export interface RoleDto {
  id: number
  code: string
  name: string
  description: string | null
  userCount: number
  createdAt: string
}

export interface RoleOptionDto {
  id: number
  code: string
  name: string
}

export interface DashboardStatsDto {
  userCount: number
  roleCount: number
}

export interface CreateUserRequest {
  username: string
  password: string
  displayName: string
  email: string | null
  roleId: number | null
  enabled: boolean
}

export interface UpdateUserRequest {
  password: string | null
  displayName: string
  email: string | null
  roleId: number | null
  enabled: boolean
}

export interface CreateRoleRequest {
  code: string
  name: string
  description: string | null
}

export interface UpdateRoleRequest {
  name: string
  description: string | null
}
