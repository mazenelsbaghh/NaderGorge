import {defineConfig} from 'vite';
import {fileURLToPath} from 'node:url';
export default defineConfig({
 build:{
  outDir:'../web/assets',emptyOutDir:false,target:'es2015',
  lib:{entry:fileURLToPath(new URL('../web/assets/student.js',import.meta.url)),formats:['iife'],name:'MassarStudent',fileName:()=> 'student-compat.js'},
 },
});
