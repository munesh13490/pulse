import {defineConfig} from 'vite';
import react from '@vitejs/plugin-react';
export default defineConfig({plugins:[react()],build:{rollupOptions:{output:{manualChunks:{charts:['recharts'],ui:['@mui/material','@emotion/react','@emotion/styled']}}}},server:{proxy:{'/v1':'http://localhost:5080','/health':'http://localhost:5080'}}});
