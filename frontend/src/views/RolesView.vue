<template>
  <el-card shadow="never">
    <el-form :inline="true" class="search-bar">
      <el-form-item label="关键字">
        <el-input
          v-model="query.keyword"
          placeholder="角色编码 / 名称"
          clearable
          style="width: 220px"
          @keyup.enter="search"
        />
      </el-form-item>
      <el-form-item>
        <el-button type="primary" :icon="Search" @click="search">查询</el-button>
        <el-button :icon="Refresh" @click="reset">重置</el-button>
      </el-form-item>
    </el-form>

    <div class="toolbar">
      <el-button type="primary" :icon="Plus" @click="openCreate">新增角色</el-button>
    </div>

    <el-table v-loading="loading" :data="rows" border stripe>
      <el-table-column prop="code" label="编码" min-width="140" />
      <el-table-column prop="name" label="名称" min-width="140" />
      <el-table-column prop="description" label="描述" min-width="200" show-overflow-tooltip />
      <el-table-column label="关联用户" width="100">
        <template #default="{ row }">
          <el-tag :type="row.userCount > 0 ? 'warning' : 'info'" effect="plain">{{ row.userCount }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="createdAt" label="创建时间" min-width="170" />
      <el-table-column label="操作" width="160" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" @click="openEdit(row)">编辑</el-button>
          <el-button
            link
            type="danger"
            :disabled="row.userCount > 0"
            :title="row.userCount > 0 ? '该角色已被引用，无法删除' : ''"
            @click="remove(row)"
          >
            删除
          </el-button>
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

    <el-dialog v-model="dialogVisible" :title="form.id ? '编辑角色' : '新增角色'" width="440px">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="80px">
        <el-form-item label="编码" prop="code">
          <el-input v-model="form.code" :disabled="!!form.id" placeholder="唯一标识，创建后不可修改" />
        </el-form-item>
        <el-form-item label="名称" prop="name">
          <el-input v-model="form.name" placeholder="角色名称" />
        </el-form-item>
        <el-form-item label="描述" prop="description">
          <el-input v-model="form.description" type="textarea" :rows="3" placeholder="可选" />
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
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { Plus, Refresh, Search } from '@element-plus/icons-vue'
import { rolesApi } from '@/api/roles'
import type { RoleDto } from '@/types'

const loading = ref(false)
const saving = ref(false)
const dialogVisible = ref(false)
const formRef = ref<FormInstance>()

const rows = ref<RoleDto[]>([])
const total = ref(0)

const query = reactive({ keyword: '', page: 1, pageSize: 20 })

const form = reactive({ id: 0, code: '', name: '', description: '' })

const rules: FormRules = {
  code: [{ required: true, message: '请输入角色编码', trigger: 'blur' }],
  name: [{ required: true, message: '请输入角色名称', trigger: 'blur' }],
}

async function load() {
  loading.value = true
  try {
    const result = await rolesApi.getPaged({
      keyword: query.keyword || undefined,
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
  query.page = 1
  load()
}

function openCreate() {
  Object.assign(form, { id: 0, code: '', name: '', description: '' })
  formRef.value?.clearValidate()
  dialogVisible.value = true
}

function openEdit(row: RoleDto) {
  Object.assign(form, {
    id: row.id,
    code: row.code,
    name: row.name,
    description: row.description ?? '',
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
      await rolesApi.update(form.id, { name: form.name, description: form.description || null })
      ElMessage.success('修改成功')
    } else {
      await rolesApi.create({
        code: form.code,
        name: form.name,
        description: form.description || null,
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

async function remove(row: RoleDto) {
  if (row.userCount > 0) {
    ElMessage.warning('该角色已被引用，无法删除')
    return
  }
  try {
    await ElMessageBox.confirm(`确定要删除角色「${row.name}」吗？`, '删除确认', {
      type: 'warning',
      confirmButtonText: '确定',
      cancelButtonText: '取消',
    })
  } catch {
    return
  }
  try {
    await rolesApi.remove(row.id)
  } catch {
    // 错误提示已由 axios 拦截器统一处理
    return
  }
  ElMessage.success('删除成功')
  load()
}

onMounted(load)
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
</style>
