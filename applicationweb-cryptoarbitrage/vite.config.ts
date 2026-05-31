import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const backendUrl = env.VITE_BACKEND_URL || 'http://localhost:5152'

  return {
    plugins: [react()],
    server: {
      port: 3000,
      proxy: {
        '/api': backendUrl,
        '/hubs': {
          target: backendUrl,
          ws: true,
        },
      },
    },
  }
})
