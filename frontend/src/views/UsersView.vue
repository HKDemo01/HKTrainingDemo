<template>
  <el-card shadow="never">
    <el-form :inline="true" class="search-bar">
      <el-form-item label="关键字">
        <el-input
          v-model="query.keyword"
          placeholder="用户名 / 姓名 / 邮箱"
          clearable
          style="width: 220px"
          @keyup.enter="search"
        />
      </el-form-item>
      <el-form-item label="角色">
        <el-select v-model="query.roleId" placeholder="全部角色" clearable style="width: 160px">
          <el-option v-for="role in roleOptions" :key="role.id" :label="role.name" :value="role.id" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-button type="primary" :icon="Search" @click="search">查询</el-button>
        <el-button :icon="Refresh" @click="reset">重置</el-button>
      </el-form-item>
    </el-form>

    <div class="toolbar">
      <el-button type="primary" :icon="Plus" @click="openCreate">新增用户</el-button>
    </div>

    <el-table v-loading="loading" :data="rows" border stripe>
      <el-table-column prop="username" label="用户名" min-width="130" />
      <el-table-column prop="displayName" label="姓名" min-width="120" />
      <el-table-column prop="email" label="邮箱" min-width="180" />
      <el-table-column label="角色" min-width="110">
        <template #default="{ row }">
          <el-tag v-if="row.roleName" type="primary" effect="plain">{{ row.roleName }}</el-tag>
          <span v-else>-</span>
        </template>
      </el-table-column>
      <el-table-column label="状态" min-width="90">
        <template #default="{ row }">
          <el-tag :type="row.enabled ? 'success' : 'info'">{{ row.enabled ? '启用' : '停用' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="createdAt" label="创建时间" min-width="170" />
      <el-table-column label="操作" width="160" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" @click="openEdit(row)">编辑</el-button>
          <el-button link type="danger" @click="remove(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-pagination
      v-model:current-page="query.page"
      v-model:page-size="query.pageSize"
      class="pagination"
      :total="total"
      :page-sizes="[10, 20, 50]"
      layout="total, sizes, prev, pager, next, jumper"
      @size-change="load"
      @current-change="load"
    />

    <el-dialog v-model="dialogVisible" :title="form.id ? '编辑用户' : '新增用户'" width="480px">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="80px">
        <el-form-item label="用户名" prop="username">
          <el-input v-model="form.username" :disabled="!!form.id" placeholder="登录账号，创建后不可修改" />
        </el-form-item>
        <el-form-item label="姓名" prop="displayName">
          <el-input v-model="form.displayName" placeholder="显示名称" />
        </el-form-item>
        <el-form-item label="密码" prop="password">
          <el-input
            v-model="form.password"
            type="password"
            show-password
            :placeholder="form.id ? '留空表示不修改密码' : '初始密码'"
          />
        </el-form-item>
        <el-form-item label="邮箱" prop="email">
          <el-input v-model="form.email" placeholder="可选" />
        </el-form-item>
        <el-form-item label="角色" prop="roleId">
          <el-select v-model="form.roleId" placeholder="不分配角色" clearable style="width: 100%">
            <el-option v-for="role in roleOptions" :key="role.id" :label="role.name" :value="role.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="状态" prop="enabled">
          <el-switch v-model="form.enabled" active-text="启用" inactive-text="停用" :disabled="isEditingSelf" />
          <span v-if="isEditingSelf" class="hint">不能停用当前登录用户</span>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="save">保存</el-button>
      </template>
    </el-dialog>
  </el-card>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { Plus, Refresh, Search } from '@element-plus/icons-vue'
import { rolesApi } from '@/api/roles'
import { usersApi } from '@/api/users'
import { useAuthStore } from '@/stores/auth'
import type { RoleOptionDto, UserDto } from '@/types'

const auth = useAuthStore()

const loading = ref(false)
const saving = ref(false)
const dialogVisible = ref(false)
const formRef = ref<FormInstance>()

const rows = ref<UserDto[]>([])
const total = ref(0)
const roleOptions = ref<RoleOptionDto[]>([])

const query = reactive({ keyword: '', roleId: null as number | null, page: 1, pageSize: 20 })

const form = reactive({
  id: 0,
  username: '',
  password: '',
  displayName: '',
  email: '',
  roleId: null as number | null,
  enabled: true,
})

// 编辑自己时禁用状态开关：停用当前登录用户会被后端拒绝（会造成账号锁死）
const isEditingSelf = computed(() => form.id !== 0 && form.id === auth.user?.id)

const rules: FormRules = {
  username: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
  displayName: [{ required: true, message: '请输入姓名', trigger: 'blur' }],
  password: [
    {
      validator: (_rule, value, callback) => {
        if (!form.id && !value) callback(new Error('请输入初始密码'))
        else callback()
      },
      trigger: 'blur',
    },
  ],
  email: [{ type: 'email', message: '邮箱格式不正确', trigger: 'blur' }],
}

async function load() {
  loading.value = true
  try {
    const result = await usersApi.getPaged({
      keyword: query.keyword || undefined,
      roleId: query.roleId,
      page: query.page,
      pageSize: query.pageSize,
    })
    rows.value = result.items
    total.value = result.total
  } catch {
    // 错误提示已由 axios 拦截器统一处理
  } finally {
    loading.value = false
  }
}

function search() {
  query.page = 1
  load()
}

function reset() {
  query.keyword = ''
  query.roleId = null
  query.page = 1
  load()
}

function openCreate() {
  Object.assign(form, {
    id: 0,
    username: '',
    password: '',
    displayName: '',
    email: '',
    roleId: null,
    enabled: true,
  })
  formRef.value?.clearValidate()
  dialogVisible.value = true
}

function openEdit(row: UserDto) {
  Object.assign(form, {
    id: row.id,
    username: row.username,
    password: '',
    displayName: row.displayName,
    email: row.email ?? '',
    roleId: row.roleId,
    enabled: row.enabled,
  })
  formRef.value?.clearValidate()
  dialogVisible.value = true
}

async function save() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return

  saving.value = true
  try {
    if (form.id) {
      await usersApi.update(form.id, {
        password: form.password || null,
        displayName: form.displayName,
        email: form.email || null,
        roleId: form.roleId,
        enabled: form.enabled,
      })
      ElMessage.success('修改成功')
    } else {
      await usersApi.create({
        username: form.username,
        password: form.password,
        displayName: form.displayName,
        email: form.email || null,
        roleId: form.roleId,
        enabled: form.enabled,
      })
      ElMessage.success('新增成功')
    }
    dialogVisible.value = false
    load()
  } catch {
    // 错误提示已由 axios 拦截器统一处理
  } finally {
    saving.value = false
  }
}

async function remove(row: UserDto) {
  if (row.id === auth.user?.id) {
    ElMessage.warning('不能删除当前登录用户')
    return
  }
  try {
    await ElMessageBox.confirm(`确定要删除用户「${row.displayName}」吗？`, '删除确认', {
      type: 'warning',
      confirmButtonText: '确定',
      cancelButtonText: '取消',
    })
  } catch {
    return
  }
  try {
    await usersApi.remove(row.id)
  } catch {
    // 错误提示已由 axios 拦截器统一处理
    return
  }
  ElMessage.success('删除成功')
  load()
}

onMounted(async () => {
  try {
    roleOptions.value = await rolesApi.getAll()
  } catch {
    // 错误提示已由 axios 拦截器统一处理
  }
  await load()
})
</script>

<style scoped>
.search-bar {
  margin-bottom: 4px;
}

.toolbar {
  margin-bottom: 12px;
}

.pagination {
  margin-top: 16px;
  justify-content: flex-end;
}

.hint {
  margin-left: 10px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}
</style>
