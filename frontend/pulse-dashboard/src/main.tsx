import {Component,useMemo,useState,type ErrorInfo,type ReactNode} from 'react';
import React from 'react';
import {createRoot} from 'react-dom/client';
import {BrowserRouter} from 'react-router-dom';
import {QueryClient,QueryClientProvider} from '@tanstack/react-query';
import {CssBaseline,ThemeProvider,createTheme} from '@mui/material';
import App from './App';

const queryClient=new QueryClient();
type Mode='light'|'dark';
function buildTheme(mode:Mode){
 const dark=mode==='dark';
 return createTheme({palette:{mode,primary:{main:dark?'#5eead4':'#0f766e',contrastText:dark?'#06231f':'#ffffff'},secondary:{main:'#6366f1'},background:{default:dark?'#0b1220':'#f5f7fa',paper:dark?'#111c2e':'#ffffff'},text:{primary:dark?'#e6edf7':'#162338',secondary:dark?'#9eacc1':'#5d6b7c'},success:{main:'#16a34a'},warning:{main:'#d97706'},error:{main:'#dc2626'}},shape:{borderRadius:10},typography:{fontFamily:'Inter, Roboto, Arial, sans-serif',h4:{fontWeight:700},h5:{fontWeight:700},button:{fontWeight:600,textTransform:'none'}},components:{MuiButton:{styleOverrides:{root:{borderRadius:9}}},MuiCard:{styleOverrides:{root:{backgroundImage:'none'}}},MuiPaper:{styleOverrides:{root:{backgroundImage:'none'}}}}});
}
function Root(){
 const [mode,setMode]=useState<Mode>(()=>localStorage.getItem('pulse-theme')==='light'?'light':'dark');
 const theme=useMemo(()=>buildTheme(mode),[mode]);
 const toggleTheme=()=>setMode(current=>{const next=current==='dark'?'light':'dark';localStorage.setItem('pulse-theme',next);return next;});
 return <ThemeProvider theme={theme}><CssBaseline/><QueryClientProvider client={queryClient}><BrowserRouter><App mode={mode} onToggleTheme={toggleTheme}/></BrowserRouter></QueryClientProvider></ThemeProvider>;
}
class ApplicationErrorBoundary extends Component<{children:ReactNode},{error:Error|null}>{
 state={error:null as Error|null};
 static getDerivedStateFromError(error:Error){return {error};}
 componentDidCatch(_error:Error,_info:ErrorInfo){}
 render(){if(this.state.error)return <div style={{minHeight:'100vh',display:'grid',placeItems:'center',padding:'24px',fontFamily:'Arial, sans-serif',background:'#f5f7fa',color:'#162338'}}><section style={{maxWidth:'560px',padding:'24px',border:'1px solid #f1b6b6',borderRadius:'12px',background:'#fff'}}><strong>The dashboard could not render.</strong><p style={{marginBottom:'16px'}}>{this.state.error.message}</p><button type="button" onClick={()=>this.setState({error:null})}>Try again</button></section></div>;return this.props.children;}
}
createRoot(document.getElementById('root')!).render(<React.StrictMode><ApplicationErrorBoundary><Root/></ApplicationErrorBoundary></React.StrictMode>);
