import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    strictPort: true,
    proxy: {
      '/api': {
        target: 'https://localhost:7056',
        // The local ASP.NET Core HTTPS profile uses a development certificate.
        secure: false,
      },
    },
  },
})
