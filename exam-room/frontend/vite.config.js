import {defineConfig} from 'vite';
import {fileURLToPath} from 'node:url';

export default defineConfig({
  root: fileURLToPath(new URL('.', import.meta.url)),
  define: {'process.env.NODE_ENV': JSON.stringify('production')},
  build: {
    outDir: '../web/assets',
    emptyOutDir: false,
    target: 'es2022',
    lib: {
      entry: fileURLToPath(new URL('./src/admin.jsx', import.meta.url)),
      formats: ['es'],
      fileName: () => 'admin-react.js',
    },
  },
});
