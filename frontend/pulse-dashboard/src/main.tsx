import React from 'react';
import {createRoot} from 'react-dom/client';
import {BrowserRouter} from 'react-router-dom';
import {QueryClient,QueryClientProvider} from '@tanstack/react-query';
import {CssBaseline,ThemeProvider,createTheme} from '@mui/material';
import App from './App';
const theme=createTheme({palette:{mode:'dark',primary:{main:'#65dfc2'},background:{default:'#0b121c',paper:'#121e2c'}},typography:{fontFamily:'Inter, Segoe UI, sans-serif'},shape:{borderRadius:12}});
createRoot(document.getElementById('root')!).render(<React.StrictMode><ThemeProvider theme={theme}><CssBaseline/><QueryClientProvider client={new QueryClient()}><BrowserRouter><App/></BrowserRouter></QueryClientProvider></ThemeProvider></React.StrictMode>);
