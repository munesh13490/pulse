import {defineConfig,loadEnv} from 'vite';
import react from '@vitejs/plugin-react';
export default defineConfig(({mode})=>{
 const apiProxyTarget=loadEnv(mode,'','VITE_').VITE_API_PROXY_TARGET??'http://localhost:5080';
 const proxyTarget={target:apiProxyTarget,changeOrigin:true};
 return {plugins:[react()],build:{rollupOptions:{output:{manualChunks:{charts:['recharts'],ui:['@mui/material','@emotion/react','@emotion/styled']}}}},server:{proxy:{'/v1':proxyTarget,'/health':proxyTarget}}};
});
