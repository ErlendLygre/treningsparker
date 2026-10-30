import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// VITE_BASE is set to "/treningsparker/" when building for GitHub Pages.
export default defineConfig({
  base: process.env.VITE_BASE ?? '/',
  plugins: [react()],
  server: {
    // Allow importing the shared seed data in ../data
    fs: { allow: ['..'] },
    proxy: {
      '/api': 'http://localhost:5080',
    },
  },
})
