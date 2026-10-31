import path from 'node:path'
import { defineConfig } from 'vite'
import react, { reactCompilerPreset } from '@vitejs/plugin-react'
import babel from '@rolldown/plugin-babel'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    babel({ presets: [reactCompilerPreset()] }),
    tailwindcss(),
  ],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    // 编辑器/工具用「临时文件 + 改名」落盘时，Windows 上 vite 监听这些临时文件会抛 EBUSY 直接把 dev server 弄挂。
    watch: {
      ignored: ['**/*.tmpdir/**', '**/*.tmp'],
    },
    proxy: {
      // 将 /api 转发到 ASP.NET Core 后端，去掉 /api 前缀
      '/api': {
        target: 'http://localhost:5271',
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/api/, ''),
      },
    },
  },
})
