import path from 'node:path'
import fs from 'node:fs'
import { defineConfig, type ViteDevServer } from 'vite'
import react, { reactCompilerPreset } from '@vitejs/plugin-react'
import babel from '@rolldown/plugin-babel'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    babel({ presets: [reactCompilerPreset()] }),
    tailwindcss(),
    {
      // 开发态官网首页与生产对齐：生产 nginx 把 / 指向 public/home.html 静态稿，
      // dev 让 / 也直接返回同一个静态稿，做到本地所见即生产所得。
      // /login 等其余路径仍走 React SPA（登录页依赖它）。
      name: 'dev-home-page',
      configureServer(server: ViteDevServer) {
        server.middlewares.use((req, res, next) => {
          if (req.method === 'GET' && req.url && req.url.split('?')[0] === '/') {
            const file = path.resolve(__dirname, 'public/home.html')
            if (fs.existsSync(file)) {
              res.setHeader('Content-Type', 'text/html; charset=utf-8')
              res.setHeader('Cache-Control', 'no-cache')
              res.end(fs.readFileSync(file, 'utf-8'))
              return
            }
          }
          next()
        })
      },
    },
  ],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    // 编辑器/工具用「临时文件 + 改名」落盘时，Windows 上 vite 监听这些临时文件会抛 EBUSY 直接把 dev server 弄挂。
    // public/downloads 下的大文件（安装包 zip）常被系统或杀软占用，同样会抛 EBUSY，一并忽略。
    watch: {
      ignored: ['**/*.tmpdir/**', '**/*.tmp', '**/public/downloads/**', '**/*.zip'],
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
