// Browser reports are monitoring hints, not proof of device identity or split screen.
let deviceId;
try { deviceId=localStorage.getItem('massar-browser-id'); } catch { /* Private storage can be unavailable. */ }
if (!deviceId) {
 const bytes=new Uint8Array(16);crypto.getRandomValues(bytes);
 deviceId=Array.from(bytes,b=>b.toString(16).padStart(2,'0')).join('');
 try {localStorage.setItem('massar-browser-id',deviceId);}catch {/* This identifier lasts for this page only. */}
}
let armed=false,attemptId='',supportsFullscreen=false,entering=false;
export function screenGuardActive(session) {
 if(attemptId!==session?.id){attemptId=session?.id;armed=false;}
 return session?.state==='running'&&session.screenGuard;
}
export function screenGuardBlocked(session) {return screenGuardActive(session)&&!armed;}
export async function enterExamScreen() {
 entering=true;
 try {
  supportsFullscreen=Boolean(document.fullscreenEnabled&&document.documentElement.requestFullscreen);
  if(supportsFullscreen&&!document.fullscreenElement) await document.documentElement.requestFullscreen();
  // Let fullscreen layout settle before the first reference measurement.
  await new Promise(resolve=>setTimeout(resolve,500));
  armed=true;
  return supportsFullscreen;
 } finally {entering=false;}
}
export function screenMeasurement(session) {
 if(!screenGuardActive(session)||!armed||entering)return null;
 const editable=document.activeElement?.matches('input,textarea,[contenteditable="true"]');
 const vv=window.visualViewport;
 const keyboard=Boolean(editable&&(matchMedia('(pointer: coarse)').matches || vv&&vv.height<window.innerHeight*0.8));
 // A stable layout viewport excludes visual-only keyboard and zoom changes.
 return {deviceId,screenWidth:Math.round(screen.width),screenHeight:Math.round(screen.height),
  width:Math.max(100,Math.round(document.documentElement.clientWidth)),height:Math.max(100,Math.round(window.innerHeight)),
  keyboard,fullscreen:Boolean(document.fullscreenElement),fullscreenSupported:supportsFullscreen};
}
