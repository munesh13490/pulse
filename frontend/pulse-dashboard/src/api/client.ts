import createClient from 'openapi-fetch';
import type { paths } from './schema';
let accessToken = '';
let refreshToken = '';
let renewal:Promise<void>|null=null;
export function setTokens(access: string, refresh: string) { accessToken=access; refreshToken=refresh; }
export function clearTokens() { accessToken=''; refreshToken=''; }
export const client=createClient<paths>();
client.use({onRequest({request}){if(accessToken)request.headers.set('Authorization','Bearer '+accessToken);return request;}});
export async function api<T>(path:string,init:RequestInit={}):Promise<T> {
 const send=()=>fetch(path,{...init,headers:{'Content-Type':'application/json',...init.headers,Authorization:'Bearer '+accessToken}});
 let response=await send();
 if(response.status===401 && refreshToken && path!='/v1/auth/login'){
   if(!renewal)renewal=(async()=>{
     const renewed=await fetch('/v1/auth/refresh',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({refreshToken})});
     if(!renewed.ok){clearTokens();throw new Error('Session expired. Sign in again.');}
     const pair=await renewed.json();setTokens(pair.accessToken,pair.refreshToken);
   })().finally(()=>{renewal=null;});
   await renewal;response=await send();
 }
 if(!response.ok){const text=await response.text();throw new Error(text||'Request failed: '+response.status);}
 return response.status===204?undefined as T:response.json();
}
export async function logout(){if(refreshToken)await api('/v1/auth/logout',{method:'POST',body:JSON.stringify({refreshToken})});clearTokens();}
export async function stream(signal:AbortSignal,onSnapshot:(data:unknown[])=>void){
 while(!signal.aborted){
 const response=await fetch('/v1/alerts/stream',{headers:{Authorization:'Bearer '+accessToken},signal});
 if(response.status===401){await api('/v1/auth/me');continue;}
 if(!response.ok||!response.body)throw new Error('Live stream failed');
 const reader=response.body.getReader();const decoder=new TextDecoder();let buffer='';
 while(true){const result=await reader.read();if(result.done)break;buffer+=decoder.decode(result.value,{stream:true});
 let end:number;while((end=buffer.indexOf('\n\n'))>=0){const message=buffer.slice(0,end);buffer=buffer.slice(end+2);const data=message.split('\n').find(l=>l.startsWith('data: '));if(data)onSnapshot(JSON.parse(data.slice(6)));}}
 await new Promise(resolve=>setTimeout(resolve,1000));
 }
}
