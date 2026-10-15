// This is a visual bridge only. It never creates an administration capability.
const palettes = {
  light: {canvas:'#F6F7F8',surface:'#FFFFFF',ink:'#0A1D3D',muted:'#4C596A',line:'#DCE1E6',accent:'#087B7B',subtle:'#EEF1F4',success:'#0B695F',successSurface:'#E8F4F2',warning:'#75500B',warningSurface:'#FFF5DD',error:'#9F2121',errorSurface:'#FFEEEE',nav:'#0A1D3D',textOnNav:'#E6EDF5',onPrimary:'#FFFFFF'},
  dark: {canvas:'#0C1220',surface:'#141E2F',ink:'#E6EDF5',muted:'#ADBCCD',line:'#344257',accent:'#64D9CC',subtle:'#1E2B40',success:'#77DFC4',successSurface:'#15352D',warning:'#FFD58A',warningSurface:'#3C2C17',error:'#FFADAA',errorSurface:'#3B222B',nav:'#0A1020',textOnNav:'#E6EDF5',onPrimary:'#092625'},
};
const variables = {canvas:'canvas',surface:'paper',ink:'ink',muted:'muted',line:'line',accent:'teal',subtle:'subtle',success:'success',successSurface:'teal-soft',warning:'warning',warningSurface:'warning-soft',error:'danger',errorSurface:'danger-soft',nav:'center-nav',textOnNav:'center-text-on-nav',onPrimary:'on-primary'};

export function resolveCenterTheme(input) {
  const mode=input?.mode==='dark'?'dark':'light';
  const colors={...palettes[mode]};
  for(const key of Object.keys(colors)) {
    const value=input?.colors?.[key] ?? (key==='onPrimary'?input?.onPrimary:undefined);
    if(typeof value==='string' && /^#[\da-f]{6}$/i.test(value))colors[key]=value;
  }
  return {mode,colors};
}

export function installCenterEmbeddedTheme(window,document) {
  if(new URLSearchParams(window.location.search).get('embedded')!=='center')return false;
  document.documentElement.classList.add('center-embedded-root');
  document.body.classList.add('center-embedded');
  const apply=input=>{
    const theme=resolveCenterTheme(input);
    const root=document.documentElement;
    root.dataset.centerTheme=theme.mode;
    root.style.colorScheme=theme.mode;
    for(const [key,variable] of Object.entries(variables))root.style.setProperty(`--${variable}`,theme.colors[key]);
    // Existing exam components use navy as their foreground role.
    root.style.setProperty('--navy',theme.colors.ink);
    return true;
  };
  window.applyMassarCenterTheme=apply;
  apply(window.__massarCenterTheme || {mode:new URLSearchParams(window.location.search).get('theme')});
  return true;
}
