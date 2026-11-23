import {describe,it,expect,vi,afterEach} from 'vitest';
import {api,setTokens,clearTokens} from './client';
afterEach(()=>{vi.unstubAllGlobals();clearTokens();});
describe('API transport',()=>{
 it('persists and clears both tokens in cookies',()=>{
  const writes:string[]=[];
  vi.stubGlobal('document',{get cookie(){return '';},set cookie(value:string){writes.push(value);}});
  vi.stubGlobal('location',{protocol:'https:'});
  setTokens('access value','refresh value');clearTokens();
  expect(writes).toEqual(expect.arrayContaining([
   expect.stringContaining('pulse_access_token=access%20value'),
   expect.stringContaining('pulse_refresh_token=refresh%20value'),
   expect.stringContaining('pulse_access_token=; Max-Age=0'),
   expect.stringContaining('pulse_refresh_token=; Max-Age=0'),
  ]));
  expect(writes.every(value=>value.includes('SameSite=Strict; Secure'))).toBe(true);
 });
 it('attaches the in-memory bearer token',async()=>{
  const fetch=vi.fn().mockResolvedValue(new Response(JSON.stringify({ok:true}),{status:200}));vi.stubGlobal('fetch',fetch);
  setTokens('access','refresh');await expect(api('/v1/auth/me')).resolves.toEqual({ok:true});
  expect(fetch.mock.calls[0][1].headers.Authorization).toBe('Bearer access');
 });
 it('rotates expired credentials and retries once',async()=>{
  const fetch=vi.fn().mockResolvedValueOnce(new Response('',{status:401})).mockResolvedValueOnce(new Response(JSON.stringify({accessToken:'new',refreshToken:'rotated'}))).mockResolvedValueOnce(new Response(JSON.stringify({id:1})));
  vi.stubGlobal('fetch',fetch);setTokens('expired','refresh');await expect(api('/v1/devices')).resolves.toEqual({id:1});expect(fetch).toHaveBeenCalledTimes(3);expect(fetch.mock.calls[2][1].headers.Authorization).toBe('Bearer new');
 });
 it('surfaces backend failures',async()=>{vi.stubGlobal('fetch',vi.fn().mockResolvedValue(new Response('Unavailable',{status:503})));await expect(api('/v1/devices')).rejects.toThrow('Unavailable');});
 it('formats unavailable dependency responses without exposing trace details',async()=>{
  const problem={title:'Service Unavailable',status:503,detail:'Redis unavailable; requests fail closed.',traceId:'secret-trace'};
  vi.stubGlobal('fetch',vi.fn().mockResolvedValue(new Response(JSON.stringify(problem),{status:503})));
  await expect(api('/v1/devices')).rejects.toThrow('Redis is unavailable');
 });
});
