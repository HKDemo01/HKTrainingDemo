<template>
  <el-menu
    class="app-menu"
    :default-active="route.path"
    router
    background-color="#1f2d3d"
    text-color="#c0c4cc"
    active-text-color="#409eff"
    @select="emit('select')"
  >
    <el-menu-item index="/dashboard">
      <el-icon><HomeFilled /></el-icon>
      <span>工作台</span>
    </el-menu-item>
    <el-menu-item index="/users">
      <el-icon><User /></el-icon>
      <span>用户管理</span>
    </el-menu-item>
    <el-menu-item index="/roles">
      <el-icon><Avatar /></el-icon>
      <span>角色管理</span>
    </el-menu-item>
  </el-menu>
</template>

<script setup lang="ts">
import { useRoute } from 'vue-router'
import { Avatar, HomeFilled, User } from '@element-plus/icons-vue'

const emit = defineEmits<{ (e: 'select'): void }>()

const route = useRoute()
</script>

<style scoped>
.app-menu {
  border-right: none;
}
</style>
