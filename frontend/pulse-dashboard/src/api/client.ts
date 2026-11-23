import createClient from 'openapi-fetch';
import type { paths } from './schema';
const accessTokenCookie = 'pulse_access_token';
const refreshTokenCookie = 'pulse_refresh_token';
const canUseCookies = () => typeof document !== 'undefined';
const readCookie = (name:string) => {
 if (!canUseCookies()) return '';
 const entry = document.cookie.split('; ').find(cookie => cookie.startsWith(`${name}=`));
 return entry ? decodeURIComponent(entry.slice(name.length + 1)) : '';
};
const cookieAttributes = () => `Path=/; SameSite=Strict${typeof location !== 'undefined' && location.protocol === 'https:' ? '; Secure' : ''}`;
const writeCookie = (name:string,value:string) => {
 if (canUseCookies()) document.cookie = `${name}=${encodeURIComponent(value)}; ${cookieAttributes()}`;
};
const deleteCookie = (name:string) => {
 if (canUseCookies()) document.cookie = `${name}=; Max-Age=0; ${cookieAttributes()}`;
};
let accessToken = readCookie(accessTokenCookie);
let refreshToken = readCookie(refreshTokenCookie);
let renewal:Promise<void>|null=null;
const apiBaseUrl=(import.meta.env.VITE_API_BASE_URL??'').replace(/\/$/,'');
const apiUrl=(path:string)=>apiBaseUrl+path;
type ProblemDetails={title?:string;status?:number;detail?:string};
async function responseError(response:Response){
 const body=await response.text();
 try{
  const problem=JSON.parse(body) as ProblemDetails;
  if(response.status===503)return new Error('Service temporarily unavailable. Redis is unavailable, so the API is refusing requests. Try again shortly.');
  return new Error(problem.detail||problem.title||`Request failed: ${response.status}`);
 }catch{return new Error(body||`Request failed: ${response.status}`);}
}
export function setTokens(access: string, refresh: string) { accessToken=access; refreshToken=refresh; writeCookie(accessTokenCookie,access);writeCookie(refreshTokenCookie,refresh); }
export function clearTokens() { accessToken=''; refreshToken=''; deleteCookie(accessTokenCookie);deleteCookie(refreshTokenCookie); }
export const client=createClient<paths>({baseUrl:apiBaseUrl});
client.use({onRequest({request}){if(accessToken)request.headers.set('Authorization','Bearer '+accessToken);return request;}});
export async function api<T>(path:string,init:RequestInit={}):Promise<T> {
 const send=()=>fetch(apiUrl(path),{...init,headers:{'Content-Type':'application/json',...init.headers,Authorization:'Bearer '+accessToken}});
 let response=await send();
 if(response.status===401 && refreshToken && path!='/v1/auth/login'){
   if(!renewal)renewal=(async()=>{
     const renewed=await fetch(apiUrl('/v1/auth/refresh'),{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({refreshToken})});
     if(!renewed.ok){clearTokens();throw new Error('Session expired. Sign in again.');}
     const pair=await renewed.json();setTokens(pair.accessToken,pair.refreshToken);
   })().finally(()=>{renewal=null;});
   await renewal;response=await send();
 }
 if(!response.ok)throw await responseError(response);
 return response.status===204?undefined as T:response.json();
}
export async function logout(){if(refreshToken)await api('/v1/auth/logout',{method:'POST',body:JSON.stringify({refreshToken})});clearTokens();}
export async function stream(signal:AbortSignal,onSnapshot:(data:unknown[])=>void){
 while(!signal.aborted){
 const response=await fetch(apiUrl('/v1/alerts/stream'),{headers:{Authorization:'Bearer '+accessToken},signal});
 if(response.status===401){await api('/v1/auth/me');continue;}
 if(!response.ok||!response.body)throw new Error('Live stream failed');
 const reader=response.body.getReader();const decoder=new TextDecoder();let buffer='';
 while(true){const result=await reader.read();if(result.done)break;buffer+=decoder.decode(result.value,{stream:true});
 let end:number;while((end=buffer.indexOf('\n\n'))>=0){const message=buffer.slice(0,end);buffer=buffer.slice(end+2);const data=message.split('\n').find(l=>l.startsWith('data: '));if(data)onSnapshot(JSON.parse(data.slice(6)));}}
 await new Promise(resolve=>setTimeout(resolve,1000));
 }
}
