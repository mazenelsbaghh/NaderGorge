import { createDevToolsSuspensionScript } from './video-embed-devtools-guard.ts';

function escapeHtml(text: string): string {
  return text.replace(/[&<>"']/g, character => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[character]!);
}

export function generateYouTubeHlsEmbedHtml(playlistSource: string, studentName: string, studentPhone: string): string {
  // Only a protected session URL belongs in this document; video URLs arrive in native HLS playlists.
  if (!/^\/api\/video\/youtube-hls\?s=[0-9a-f-]{36}&playlist=master$/i.test(playlistSource)) {
    throw new Error('YouTube HLS requires a protected playback session.');
  }
  return `<!DOCTYPE html>
<html lang="ar" dir="rtl"><head>
<meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">
<meta name="referrer" content="strict-origin-when-cross-origin"><title>Massar HLS Player</title>
<style>
*{box-sizing:border-box;margin:0;padding:0}html,body,#wrap{width:100%;height:100%;overflow:hidden;background:#000}
#wrap{position:relative}#video{display:block;width:100%;height:100%;object-fit:contain;background:#000}
#wm{position:absolute;z-index:3;left:10%;top:12%;max-width:42%;color:rgba(255,255,255,.2);font:700 clamp(11px,2.8vw,18px)/1.35 system-ui,sans-serif;text-align:center;overflow-wrap:anywhere;pointer-events:none;text-shadow:0 1px 3px #000}
</style></head><body oncontextmenu="return false"><div id="wrap">
<video id="video" playsinline webkit-playsinline preload="metadata" disablepictureinpicture controlslist="nodownload noremoteplayback"></video>
<div id="wm"><b>Massar Academy</b><br>${escapeHtml(studentName)}<br><small>${escapeHtml(studentPhone)}</small></div>
</div><script src="/vendor/hlsjs/hls.min.js"></script><script>
(function(){
'use strict';
var source=${JSON.stringify(playlistSource)};
var serverClock=${Date.now()};
${youtubeHlsPlayerScript}
${createDevToolsSuspensionScript('suspendYouTubeHls')}
})();
</script></body></html>`;
}

const youtubeHlsPlayerScript = String.raw`
var video=document.getElementById('video');
var serverClockStarted=performance.now(), lastRenewedAt=serverClock, expiresAt=0, sessionExpiresAt=0;
var version='', levels=[], duration=0, quality='auto', readySent=false, terminal=false, lastMediaTime=0, hls=null;
var pendingPlayback=null, renewalPending=false, reloadAfterRenewal=false, recoveryUsed=false, mediaErrorRecoveryUsed=false;
var renewalTimer=null, loadTimer=null, stallTimer=null, requestController=null;
function post(type,payload){parent.postMessage({source:'video-embed',type:type,data:payload||{}},location.origin);}
function now(){return serverClock+Math.max(0,performance.now()-serverClockStarted);}
function state(){return {provider:'youtube-hls',currentTime:video.currentTime||0,duration:Number.isFinite(video.duration)?video.duration:duration,volume:Math.round(video.volume*100),isMuted:video.muted,state:video.ended?0:(video.paused?2:1),isPlaying:!video.paused&&!video.ended,playbackRate:video.playbackRate||1};}
function clearTimer(timer){if(timer!==null)clearTimeout(timer);}
function stopRequests(){
  clearTimer(renewalTimer);clearTimer(loadTimer);clearTimer(stallTimer);
  renewalTimer=loadTimer=stallTimer=null;
  if(requestController){requestController.abort();requestController=null;}
}
function suspendYouTubeHls(){terminal=true;stopRequests();if(hls){hls.destroy();hls=null;}video.pause();video.removeAttribute('src');video.load();}
function fail(status,message,phase){
  if(terminal)return;
  suspendYouTubeHls();
  post('error',{provider:'youtube-hls',code:status,phase:phase,message:message});
}
function authorizationMessage(status){return status===409?'تم فتح الفيديو في جلسة أخرى. تابع من الجلسة الأحدث.':'انتهى تصريح مشاهدة الفيديو. أعد فتح الدرس للمتابعة.';}
function emitQuality(){post('qualityLevels',{levels:levels,currentQuality:quality,auto:true});}
function capturePlayback(){return {time:video.currentTime||0,paused:video.paused,rate:video.playbackRate,volume:video.volume,muted:video.muted};}
function play(){video.play().catch(function(){if(!terminal)post('autoplayBlocked',{provider:'youtube-hls'});});}
function restorePlayback(){
  if(!pendingPlayback)return;
  var playback=pendingPlayback;pendingPlayback=null;
  if(playback.time>0)video.currentTime=Math.min(playback.time,Math.max(0,(video.duration||duration)-.1));
  video.playbackRate=playback.rate;video.volume=playback.volume;video.muted=playback.muted;
  if(!playback.paused)play();
}
function nextRenewalAt(){return Math.min(lastRenewedAt+1500000,expiresAt-120000,sessionExpiresAt||Infinity);}
function scheduleRenewal(){
  clearTimer(renewalTimer);
  if(!terminal)renewalTimer=setTimeout(requestRenewal,Math.max(60000,nextRenewalAt()-now()));
}
function requestRenewal(){
  if(terminal||renewalPending)return;
  renewalPending=true;clearTimer(renewalTimer);
  post('renewSourceRequired',{native:true});
  renewalTimer=setTimeout(function(){renewalFailed(0);},25000);
}
function renewalFailed(status){
  if(!renewalPending||terminal)return;
  renewalPending=false;
  fail(status,status===401||status===403||status===404||status===409||status===410?authorizationMessage(status):'تعذر تحديث تصريح الفيديو. اضغط «حاول مرة أخرى» للمتابعة.','source_authorization');
}
function checkRenewal(){if(version&&now()>=nextRenewalAt())requestRenewal();}
function recoverMedia(){
  if(terminal||renewalPending)return;
  if(recoveryUsed){fail(0,'تعذر تشغيل الفيديو مباشرة على هذا الجهاز أو الشبكة. أعد المحاولة أو تواصل مع الدعم.','native_media');return;}
  recoveryUsed=true;reloadAfterRenewal=true;
  pendingPlayback=pendingPlayback||capturePlayback();
  post('stateChange',{provider:'youtube-hls',state:3,isPlaying:false});
  clearTimer(loadTimer);clearTimer(stallTimer);requestRenewal();
}
function armLoadDeadline(){clearTimer(loadTimer);loadTimer=setTimeout(recoverMedia,45000);}
function armStallDeadline(){if(stallTimer===null&&!video.paused)stallTimer=setTimeout(function(){stallTimer=null;recoverMedia();},30000);}
function loadPlaylist(){
  if(terminal)return;
  pendingPlayback=pendingPlayback||capturePlayback();
  var playlist=new URL(source,location.origin);playlist.searchParams.set('v',version);
  if(hls)playlist.searchParams.set('relay','1');
  if(quality!=='auto'&&!hls)playlist.searchParams.set('quality',quality);
  post('stateChange',{provider:'youtube-hls',state:3,isPlaying:false});
  clearTimer(stallTimer);stallTimer=null;
  armLoadDeadline();
  if(hls)hls.loadSource(playlist.href);else{video.src=playlist.href;video.load();}
  emitQuality();
}
function applyMetadata(metadata,forceReload){
  if(!metadata||!Array.isArray(metadata.qualities)||typeof metadata.version!=='string'||!metadata.version
    ||!Number.isFinite(metadata.expiresAt)||!Number.isFinite(metadata.serverNowMs)||metadata.expiresAt<=metadata.serverNowMs){
    fail(0,'تعذر تجهيز جودات الفيديو. أعد المحاولة.','invalid_metadata');return;
  }
  serverClock=metadata.serverNowMs;serverClockStarted=performance.now();expiresAt=metadata.expiresAt;
  if(!version)lastRenewedAt=serverClock;
  levels=metadata.qualities.filter(function(level){return Number.isInteger(level.height)&&level.height>0;}).map(function(level){return {id:String(level.height),label:level.height+'p',height:level.height,bitrate:level.bandwidth};});
  if(!levels.length){fail(0,'لا توجد جودة قابلة للتشغيل لهذا الفيديو.','no_quality');return;}
  if(quality!=='auto'&&!levels.some(function(level){return level.id===quality;}))quality='auto';
  duration=Number(metadata.durationSeconds)||0;
  var changed=version!==metadata.version;version=metadata.version;
  scheduleRenewal();
  if(changed||forceReload)loadPlaylist();else emitQuality();
}
function fetchMetadata(forceReload){
  if(terminal)return;
  if(requestController)requestController.abort();
  var controller=new AbortController();requestController=controller;
  var deadline=setTimeout(function(){controller.abort();},20000);
  var endpoint=new URL(source,location.origin);endpoint.searchParams.delete('playlist');endpoint.searchParams.set('info','1');
  fetch(endpoint.href,{credentials:'same-origin',cache:'no-store',signal:controller.signal}).then(function(response){
    if(terminal||requestController!==controller)return null;
    if(!response.ok){fail(response.status,response.status===401||response.status===403||response.status===409||response.status===410?authorizationMessage(response.status):'تعذر تجهيز بث الفيديو. أعد المحاولة.','metadata');return null;}
    return response.json();
  }).then(function(metadata){if(!terminal&&requestController===controller&&metadata)applyMetadata(metadata,forceReload);})
    .catch(function(){if(!terminal&&requestController===controller)fail(0,'تعذر الاتصال لتجهيز الفيديو. تحقق من الاتصال ثم أعد المحاولة.','metadata_network');})
    .finally(function(){clearTimeout(deadline);if(requestController===controller)requestController=null;});
}
function renewSource(command){
  if(!renewalPending||terminal)return;
  var replacement;
  try{replacement=new URL(command.source,location.origin);}catch(error){renewalFailed(0);return;}
  if(replacement.href!==new URL(source,location.origin).href||!Number.isFinite(command.serverNowMs)
    ||!Number.isFinite(command.sessionExpiresAtMs)||command.sessionExpiresAtMs<=command.serverNowMs){renewalFailed(0);return;}
  clearTimer(renewalTimer);renewalTimer=null;renewalPending=false;
  serverClock=command.serverNowMs;serverClockStarted=performance.now();lastRenewedAt=now();sessionExpiresAt=command.sessionExpiresAtMs;
  var reload=reloadAfterRenewal;reloadAfterRenewal=false;fetchMetadata(reload);
}
function setQuality(selected){
  if(!version||selected===quality)return;
  if(selected!=='auto'&&!levels.some(function(level){return level.id===selected;}))return;
  quality=selected;
  if(renewalPending||now()>=nextRenewalAt()){
    pendingPlayback=pendingPlayback||capturePlayback();reloadAfterRenewal=true;requestRenewal();return;
  }
  if(hls&&hls.levels&&hls.levels.length){
    var selectedIndex=selected==='auto'?-1:hls.levels.findIndex(function(level){return String(level.height)===selected;});
    if(selectedIndex>=0||selected==='auto'){
      hls.currentLevel=selectedIndex;hls.nextLevel=selectedIndex;emitQuality();return;
    }
  }
  loadPlaylist();
}
video.addEventListener('loadedmetadata',function(){
  if(terminal)return;
  clearTimer(loadTimer);loadTimer=null;restorePlayback();
  if(!readySent){readySent=true;post('ready',state());}
  if(video.paused)post('stateChange',state());
  post('durationChange',state());emitQuality();
});
video.addEventListener('timeupdate',function(){
  if(terminal||pendingPlayback)return;
  if(video.currentTime>lastMediaTime+.01){clearTimer(stallTimer);stallTimer=null;}
  lastMediaTime=video.currentTime;post('timeUpdate',state());
});
video.addEventListener('playing',function(){clearTimer(stallTimer);stallTimer=null;if(!terminal)post('stateChange',state());});
['play','pause','ended'].forEach(function(name){video.addEventListener(name,function(){
  if(terminal||pendingPlayback)return;
  if(name==='play'){checkRenewal();armStallDeadline();}else{clearTimer(stallTimer);stallTimer=null;}
  post('stateChange',state());
});});
['waiting','stalled'].forEach(function(name){video.addEventListener(name,function(){
  if(terminal||video.paused)return;
  armStallDeadline();post('stateChange',{provider:'youtube-hls',state:3,isPlaying:false});
});});
video.addEventListener('error',recoverMedia);
video.addEventListener('ratechange',function(){if(!terminal)post('playbackRateChange',{provider:'youtube-hls',playbackRate:video.playbackRate});});
video.addEventListener('volumechange',function(){if(!terminal&&!pendingPlayback)post('timeUpdate',state());});
document.addEventListener('visibilitychange',function(){if(!document.hidden)checkRenewal();});
window.addEventListener('online',checkRenewal);
window.addEventListener('message',function(event){
  if(terminal||event.origin!==location.origin||event.source!==parent)return;
  var command=event.data||{};
  switch(command.type){
    case'renewSource':renewSource(command);break;
    case'sourceRenewalFailed':renewalFailed(Number(command.status)||0);break;
    case'play':checkRenewal();if(pendingPlayback)pendingPlayback.paused=false;else play();break;
    case'pause':if(pendingPlayback)pendingPlayback.paused=true;video.pause();break;
    case'togglePlay':if(pendingPlayback)pendingPlayback.paused=!pendingPlayback.paused;else video.paused?play():video.pause();break;
    case'seekTo':var time=Number(command.time);if(Number.isFinite(time)){time=Math.max(0,Math.min(time,video.duration||duration||time));if(pendingPlayback)pendingPlayback.time=time;else video.currentTime=time;}break;
    case'setVolume':var volume=Number(command.volume);if(Number.isFinite(volume)){video.volume=Math.max(0,Math.min(1,volume/100));if(pendingPlayback)pendingPlayback.volume=video.volume;}break;
    case'mute':case'unmute':video.muted=command.type==='mute';if(pendingPlayback)pendingPlayback.muted=video.muted;break;
    case'setPlaybackRate':var rate=Number(command.rate);if([.5,.75,1,1.25,1.5,1.75,2].indexOf(rate)>=0){video.playbackRate=rate;if(pendingPlayback)pendingPlayback.rate=rate;}break;
    case'setQuality':setQuality(String(command.quality));break;
    case'getQualityLevels':emitQuality();break;
  }
});
if(window.Hls&&window.Hls.isSupported()){
  try{
    hls=new window.Hls({enableWorker:true,capLevelToPlayerSize:true,startLevel:-1});
    hls.attachMedia(video);
    hls.on(window.Hls.Events.MANIFEST_PARSED,function(){
      if(quality!=='auto'){
        var selectedIndex=hls.levels.findIndex(function(level){return String(level.height)===quality;});
        if(selectedIndex>=0){hls.currentLevel=selectedIndex;hls.nextLevel=selectedIndex;}
      }
      emitQuality();
    });
    hls.on(window.Hls.Events.FRAG_BUFFERED,function(){
      clearTimer(loadTimer);loadTimer=null;
      if(pendingPlayback)restorePlayback();
    });
    hls.on(window.Hls.Events.LEVEL_SWITCHED,emitQuality);
    hls.on(window.Hls.Events.ERROR,function(_,data){
      if(!data||!data.fatal||terminal)return;
      if(data.type===window.Hls.ErrorTypes.MEDIA_ERROR&&!mediaErrorRecoveryUsed){
        mediaErrorRecoveryUsed=true;hls.recoverMediaError();return;
      }
      recoverMedia();
    });
    post('providerLoaded',{provider:'youtube-hls'});fetchMetadata(false);
  }catch(error){fail(0,'تعذر بدء مشغل HLS على هذا الجهاز. أعد المحاولة بعد تحديث المتصفح.','hlsjs_bootstrap');}
}else if(video.canPlayType('application/vnd.apple.mpegurl')){
  post('providerLoaded',{provider:'youtube-hls'});fetchMetadata(false);
}else{
  fail(0,'المتصفح لا يدعم تشغيل HLS. حدّث المتصفح ثم أعد المحاولة.','unsupported_browser');
}
`;
