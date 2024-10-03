
// See https://www.npmjs.com/package/vite-tsconfig-paths
import { defineConfig } from 'vite'
import tsconfigPaths from 'vite-tsconfig-paths'

/** @type {import('vite').UserConfig} */
export default defineConfig( 
{ 
    root: 'src',
    plugins: [tsconfigPaths()],
    build: {
        emptyOutDir: true,
        outDir: '../dist'
    }
})