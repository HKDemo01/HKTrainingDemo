import request from './request'
import type { ApiResult, DashboardStatsDto } from '@/types'

export const dashboardApi = {
  getStats() {
    return request.get<ApiResult<DashboardStatsDto>>('/dashboard/stats').then((r) => r.data.data)
  },
}
