import {useEffect,useState,type ReactNode} from 'react';
import {Navigate,Route,Routes,useLocation,useNavigate} from 'react-router-dom';
import {useQueryClient} from '@tanstack/react-query';
import {AppBar,Avatar,Box,Button,Chip,Container,Divider,Drawer,IconButton,LinearProgress,List,ListItemButton,ListItemIcon,ListItemText,Stack,Toolbar,Typography} from '@mui/material';
import DashboardOutlined from '@mui/icons-material/DashboardOutlined';
import DevicesOutlined from '@mui/icons-material/DevicesOutlined';
import NotificationsActiveOutlined from '@mui/icons-material/NotificationsActiveOutlined';
import RuleOutlined from '@mui/icons-material/RuleOutlined';
import PeopleOutline from '@mui/icons-material/PeopleOutline';
import KeyOutlined from '@mui/icons-material/KeyOutlined';
import Brightness4Outlined from '@mui/icons-material/Brightness4Outlined';
import LogoutOutlined from '@mui/icons-material/LogoutOutlined';
import SensorsOutlined from '@mui/icons-material/SensorsOutlined';
import SettingsOutlined from '@mui/icons-material/SettingsOutlined';
import ApartmentOutlined from '@mui/icons-material/ApartmentOutlined';
import {api,clearTokens,logout} from './api/client';
import {AlertsPage,LivePage,RulesPage} from './pages/AlertPages';
import {SettingsPage,TenantsPage,UsersPage} from './pages/AdministrationPages';
import {DevicesPage,TelemetryPage} from './pages/DevicePages';
import {LoginPage} from './pages/LoginPage';
import {OverviewPage} from './pages/OverviewPage';
import type {CurrentUser} from './types';

const drawerWidth=252;
type ThemeMode='light'|'dark';
type NavItem={label:string;path:string;icon:ReactNode;roles?:string[]};
export default function App({mode,onToggleTheme}:{mode:ThemeMode;onToggleTheme:()=>void}){
 const [user,setUser]=useState<CurrentUser|null|undefined>(undefined);const cache=useQueryClient();
 useEffect(()=>{let mounted=true;api<CurrentUser>('/v1/auth/me').then(profile=>mounted&&setUser(profile)).catch(()=>mounted&&setUser(null));return()=>{mounted=false;};},[]);
 if(user===undefined)return <Container maxWidth="sm" sx={{pt:'12vh'}}><LinearProgress/></Container>;
 if(!user)return <LoginPage onLogin={setUser}/>;
 return <AppShell user={user} mode={mode} onToggleTheme={onToggleTheme} onLogout={async()=>{try{await logout();}finally{clearTokens();cache.clear();setUser(null);}}}/>;
}
function AppShell({user,mode,onToggleTheme,onLogout}:{user:CurrentUser;mode:ThemeMode;onToggleTheme:()=>void;onLogout:()=>Promise<void>}){
 const location=useLocation();const navigate=useNavigate();const tenantScoped=Boolean(user.tenantId);const isPlatformAdmin=user.roles.includes('platform-admin');const isTenantAdmin=user.roles.includes('tenant-admin');const canManage=user.roles.some(role=>role==='platform-admin'||role==='tenant-admin'||role==='operator');
 const workspace:NavItem[]=[{label:'Overview',path:'/',icon:<DashboardOutlined/>},...(tenantScoped?[{label:'Devices',path:'/devices',icon:<DevicesOutlined/>},{label:'Telemetry',path:'/telemetry',icon:<SensorsOutlined/>},{label:'Alerts',path:'/alerts',icon:<NotificationsActiveOutlined/>},{label:'Live alert feed',path:'/live',icon:<SensorsOutlined/>},{label:'Alert rules',path:'/rules',icon:<RuleOutlined/>,roles:['manager']}]:[])];
 const administration:NavItem[]=[...(isPlatformAdmin?[{label:'Tenants',path:'/tenants',icon:<ApartmentOutlined/>}]:[]),...(isPlatformAdmin||isTenantAdmin?[{label:'Users & roles',path:'/users',icon:<PeopleOutline/>}]:[]),...(isTenantAdmin?[{label:'Settings',path:'/settings',icon:<SettingsOutlined/>}]:[])];
 const visible=(item:NavItem)=>!item.roles||canManage;
 return <Box sx={{display:'flex',minHeight:'100vh'}}><AppBar position="fixed" color="inherit" elevation={0} sx={{borderBottom:1,borderColor:'divider',zIndex:theme=>theme.zIndex.drawer+1}}><Toolbar sx={{gap:2}}><Stack direction="row" alignItems="center" spacing={1.2} sx={{width:{md:drawerWidth-24},minWidth:{md:drawerWidth-24}}}><Avatar variant="rounded" sx={{bgcolor:'primary.main',width:34,height:34}}><SensorsOutlined sx={{color:'#fff'}}/></Avatar><Typography variant="h6" fontWeight={850} letterSpacing={1}>PULSE</Typography></Stack><Chip size="small" label={tenantScoped?'Tenant workspace':'Platform workspace'} variant="outlined" sx={{display:{xs:'none',sm:'inline-flex'}}}/><Box sx={{flexGrow:1}}/><Chip size="small" color="success" label="System online"/><IconButton onClick={onToggleTheme} aria-label="Toggle color theme"><Brightness4Outlined/></IconButton><Typography variant="body2" sx={{display:{xs:'none',sm:'block'}}}>{user.name}</Typography><Button size="small" startIcon={<LogoutOutlined/>} onClick={onLogout}>Sign out</Button></Toolbar></AppBar><Drawer variant="permanent" sx={{width:{md:drawerWidth},flexShrink:0,display:{xs:'none',md:'block'},'& .MuiDrawer-paper':{width:drawerWidth,boxSizing:'border-box',pt:9,px:1.5,borderRight:1,borderColor:'divider',display:'flex',flexDirection:'column'}}}><NavigationSection label="WORKSPACE" items={workspace.filter(visible)} path={location.pathname} onNavigate={navigate}/>{administration.length>0&&<><Divider sx={{my:1.5}}/><NavigationSection label="ADMINISTRATION" items={administration} path={location.pathname} onNavigate={navigate}/></>}<Box sx={{mt:'auto',p:2}}><Divider sx={{mb:2}}/><Typography variant="caption" color="text.secondary">PULSE · DEVICE OPERATIONS</Typography><Typography variant="caption" display="block" color="text.secondary">v1.0 · {mode} mode</Typography></Box></Drawer><Box component="main" sx={{flexGrow:1,minWidth:0,p:{xs:2,sm:3,lg:4},mt:8}}><Box component="nav" sx={{display:{xs:'flex',md:'none'},overflowX:'auto',gap:1,mb:2}}>{workspace.filter(visible).map(item=><Button key={item.path} size="small" variant={location.pathname===item.path?'contained':'outlined'} onClick={()=>navigate(item.path)}>{item.label}</Button>)}</Box><Routes><Route path="/" element={<OverviewPage user={user}/>}/>{isPlatformAdmin&&<Route path="/tenants" element={<TenantsPage/>}/>} {(isPlatformAdmin||isTenantAdmin)&&<Route path="/users" element={<UsersPage user={user}/>}/>} {tenantScoped&&<><Route path="/devices" element={<DevicesPage user={user}/>}/><Route path="/telemetry" element={<TelemetryPage/>}/><Route path="/alerts" element={<AlertsPage user={user}/>}/><Route path="/live" element={<LivePage/>}/>{canManage&&<Route path="/rules" element={<RulesPage user={user}/>}/>} {isTenantAdmin&&<Route path="/settings" element={<SettingsPage/>}/>}</>}<Route path="*" element={<Navigate to="/" replace/>}/></Routes></Box></Box>;
}
function NavigationSection({label,items,path,onNavigate}:{label:string;items:NavItem[];path:string;onNavigate:(path:string)=>void}){return <><Typography variant="overline" color="text.secondary" sx={{px:2,pt:1}}>{label}</Typography><List dense>{items.map(item=><ListItemButton key={item.path} selected={item.path==='/'?path==='/' :path.startsWith(item.path)} onClick={()=>onNavigate(item.path)} sx={{borderRadius:2,mb:.5}}><ListItemIcon sx={{minWidth:40}}>{item.icon}</ListItemIcon><ListItemText primary={item.label}/></ListItemButton>)}</List></>}
