function escapeHtml(value: string): string {
  return value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;');
}



function embedErrorHtml(message: string) {
  const safeMessage = JSON.stringify(message);
  const visibleMessage = escapeHtml(message);
  return `<!DOCTYPE html>
<html lang="ar" dir="rtl">
<head><meta charset="UTF-8"><meta name="viewport" content="width=device-width, initial-scale=1.0"></head>
<body style="margin:0;background:#000;color:#fff;font-family:system-ui,sans-serif;display:grid;place-items:center;height:100vh;text-align:center;padding:24px">
<p>${visibleMessage}</p>
<script>
try {
  window.parent.postMessage({ source: 'video-embed', type: 'error', data: { message: ${safeMessage} } }, window.location.origin);
} catch (e) {}
</script>
</body>
</html>`;
}

export function generateBunnyHlsEmbedHtml(signedPlaylistUrl: string, studentName: string, studentPhone: string, options: { relaySource?: string; serverNowMs?: number } = {}): string {
  const relaySource = options.relaySource ?? '';
  const serverNowMs = options.serverNowMs ?? Date.now();
  let parsedUrl: URL;
  try {
    parsedUrl = new URL(signedPlaylistUrl);
  } catch {
    return embedErrorHtml('رابط بث Bunny HLS غير صالح.');
  }
  if (parsedUrl.protocol !== 'https:' || !/^[a-z0-9-]+\.b-cdn\.net$/i.test(parsedUrl.hostname) || parsedUrl.port || parsedUrl.username || parsedUrl.password || parsedUrl.search || parsedUrl.hash) {
    return embedErrorHtml('مصدر بث Bunny HLS غير مسموح.');
  }

  const safeSource = JSON.stringify(parsedUrl.toString());
  const signedExpirySeconds = Number(parsedUrl.pathname.match(/(?:^|&)expires=(\d+)(?:&|$)/)?.[1]);
  const signedSourceExpiresAtMs = Number.isSafeInteger(signedExpirySeconds) && signedExpirySeconds > 0
    ? signedExpirySeconds * 1000
    : 0;
  const watermarkBrand = escapeHtml('Massar Academy');
  const watermarkStudentName = escapeHtml(studentName);
  const watermarkStudentPhone = escapeHtml(studentPhone);
  return String.raw`<!DOCTYPE html>
<html lang="ar" dir="rtl"><head>
  <meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">
  <meta name="referrer" content="strict-origin-when-cross-origin"><title>Massar HLS Player</title>
  <style>
    *{box-sizing:border-box;margin:0;padding:0}html,body,#wrap{width:100%;height:100%;overflow:hidden;background:#000}
    #video{display:block;width:100%;height:100%;object-fit:contain;background:#000}
    #wm{position:absolute;z-index:3;left:10%;top:12%;max-width:42%;color:rgba(255,255,255,.2);font:700 clamp(11px,2.8vw,18px)/1.35 system-ui,sans-serif;text-align:center;overflow-wrap:anywhere;pointer-events:none;text-shadow:0 1px 3px #000;transition:transform 1.5s ease}
  </style></head>
<body oncontextmenu="return false"><div id="wrap">
  <video id="video" playsinline webkit-playsinline preload="metadata" disablepictureinpicture controlslist="nodownload noremoteplayback"></video>
  <div id="wm"><b>${watermarkBrand}</b><br>${watermarkStudentName}<br><small>${watermarkStudentPhone}</small></div>
</div>
<script src="/vendor/hlsjs/hls.min.js"></script>
<script>
(function(){
  'use strict';
  var serverClock=${serverNowMs}; var serverClockStarted=performance.now();
  function authorizationNow(){return serverClock+Math.max(0,performance.now()-serverClockStarted);}
  var source=${safeSource}; var signedSourceExpiresAtMs=${signedSourceExpiresAtMs}; var relaySource=${JSON.stringify(relaySource)}; var relayAttempted=false; var video=document.getElementById('video'); var hls=null; var readySent=false; var sourceReady=false; var relayResume=null;
  var nativeLevels=[]; var nativeCurrent='auto'; var masterSource=source; var mediaRecoveries=0; var terminalErrorSent=false; var nativePlayback=false; var loadDeadline=null; var playbackDeadline=null; var lastLoadPhase='bootstrap'; var lastMediaTime=0;
  var nativeGrantReady=false; var lastRenewedAt=authorizationNow(); var sessionExpiresAtMs=0; var renewalTimer=null; var renewalPending=false; var renewalAttempts=0; var renewalRestart=false; var relayAuthRecoveries=0; var directAuthRecoveries=0; var waitingLoaders=new Set();
  var signedScope=sourceScope(source); var originalScope=signedScope;
  var activeDownload=null; var observedDownloadBytes=0; var downloadProgressObserved=false; var playbackWaitStartedAt=null; var maxFragmentLoadMs=120000;
  // Relay headers arrive after session validation (15s) and the upstream playlist fetch (20s).
  var relayRequestTimeoutMs=45000; var relayStallRecoveries=0; var relayRecoveryMediaTime=0;
  function loadPolicy(maxLoadMs){return {default:{maxTimeToFirstByteMs:relayAttempted?relayRequestTimeoutMs:10000,maxLoadTimeMs:maxLoadMs,timeoutRetry:{maxNumRetry:0,retryDelayMs:0,maxRetryDelayMs:0},errorRetry:{maxNumRetry:0,retryDelayMs:0,maxRetryDelayMs:0}}};}
  function post(type,data){try{parent.postMessage({source:'video-embed',type:type,data:data||{}},location.origin)}catch(e){}}
  // Child playlists retain old signed URLs, so renew at the transport boundary without rebuilding MediaSource.
  function sourceScope(candidate){
    var url;try{url=new URL(candidate)}catch(error){return null;}
    var path=url.pathname.match(/^\/bcdn_token=[A-Za-z0-9_-]+&expires=(\d+)&token_path=%2F([0-9a-f-]{36})%2F\/([0-9a-f-]{36})\/playlist\.m3u8$/i);
    if(url.protocol!=='https:'||!path||path[2]!==path[3]||url.port||url.username||url.password||url.search||url.hash)return null;
    return {origin:url.origin,video:path[3],root:new URL('./',url).href,expires:Number(path[1])*1000};
  }
  function clearRenewalTimer(){if(renewalTimer){clearTimeout(renewalTimer);renewalTimer=null;}}
  function canRenew(){return !!signedScope&&!terminalErrorSent;}
  function canRecoverAuthorization(status){
    if(!canRenew())return false;
    if(relayAttempted&&status===401)return relayAuthRecoveries++<2;
    return status===403&&directAuthRecoveries++<2;
  }
  function renewalDueAt(){
    if(nativePlayback||relayAttempted)return Math.min(lastRenewedAt+180000,sessionExpiresAtMs||Infinity);
    // At the watch-session cap, an unchanged expiry must not create a renewal loop.
    if(sessionExpiresAtMs&&signedSourceExpiresAtMs+1000>=sessionExpiresAtMs)return signedSourceExpiresAtMs;
    return signedSourceExpiresAtMs-120000;
  }
  function scheduleRenewal(){
    clearRenewalTimer();if(!canRenew())return;
    renewalTimer=setTimeout(requestSourceRenewal,Math.max(1000,renewalDueAt()-authorizationNow()));
  }
  function requestSourceRenewal(){
    if(!canRenew()||renewalPending)return;
    clearRenewalTimer();renewalPending=true;renewalAttempts++;
    post('renewSourceRequired',{native:nativePlayback});renewalTimer=setTimeout(function(){sourceRenewalFailed(0)},20000);
  }
  function sourceRenewalFailed(status){
    if(!renewalPending||terminalErrorSent)return;renewalPending=false;clearRenewalTimer();
    if(status===409){failHls(status,'تم فتح الفيديو في جلسة أخرى. تابع من الجلسة الأحدث.','source_authorization');return;}
    if(status===401||status===403||status===404||status===410){failHls(status,'انتهى تصريح مشاهدة الفيديو. أعد فتح الدرس للمتابعة.','source_authorization');return;}
    if(renewalAttempts>=4){failHls(status,'تعذر تحديث رابط الفيديو. أعد المحاولة للمتابعة من مكانك.','source_renewal');return;}
    renewalTimer=setTimeout(requestSourceRenewal,Math.min(30000,5000*Math.pow(2,renewalAttempts-1)));
  }
  function renewSource(command){
    if(!canRenew())return;
    var replacement=sourceScope(command.source);
    if(!replacement||!originalScope||replacement.origin!==originalScope.origin||replacement.video!==originalScope.video){
      // Ignore stale/mismatched replies; keep the current authorized stream and retry.
      sourceRenewalFailed(0);return;
    }
    if(Number.isFinite(command.serverNowMs)){serverClock=command.serverNowMs;serverClockStarted=performance.now();}
    if(replacement.expires<=authorizationNow()){sourceRenewalFailed(0);return;}
    if(!relayAttempted){source=command.source;masterSource=source;}signedScope=replacement;signedSourceExpiresAtMs=replacement.expires;
    var watchExpiry=Number(command.sessionExpiresAtMs);sessionExpiresAtMs=isFinite(watchExpiry)&&watchExpiry>authorizationNow()?watchExpiry:0;
    renewalPending=false;renewalAttempts=0;lastRenewedAt=authorizationNow();scheduleRenewal();
    if(nativePlayback&&!nativeGrantReady){nativeGrantReady=true;loadStartedAt=Date.now();armLoadDeadline();startNativePlayer();}
    var loaders=Array.from(waitingLoaders);waitingLoaders.clear();loaders.forEach(function(loader){loader.resume();});
    if(renewalRestart&&hls){renewalRestart=false;if(!sourceReady)hls.loadSource(source);hls.startLoad(-1,true);if(!sourceReady){loadStartedAt=Date.now();armLoadDeadline();}}
    post('sourceRenewed',state());
  }
  function renewedResource(candidate){
    var url=new URL(candidate);
    var path=url.pathname.match(/^\/bcdn_token=[A-Za-z0-9_-]+&expires=\d+&token_path=%2F([0-9a-f-]{36})%2F\/([0-9a-f-]{36})\/(.+)$/i);
    if(!signedScope||!path||url.origin!==originalScope.origin||path[1]!==originalScope.video||path[2]!==originalScope.video
      ||url.username||url.password||url.search||url.hash||!/^[A-Za-z0-9_./-]+$/.test(path[3])||path[3].split('/').some(function(part){return part==='.'||part==='..';}))return null;
    return new URL(path[3],signedScope.root).href;
  }
  function renewableLoader(){
    return class extends window.Hls.DefaultConfig.loader {
      load(context,config,callbacks){
        if(!signedScope||relayAttempted){super.load(context,config,callbacks);return;}
        this.pendingRequest={context:context,config:config,callbacks:callbacks};this.resume();
      }
      resume(){
        if(!this.pendingRequest||terminalErrorSent)return;
        if(signedSourceExpiresAtMs<=authorizationNow()){waitingLoaders.add(this);requestSourceRenewal();return;}
        var request=this.pendingRequest;this.pendingRequest=null;var rewritten=renewedResource(request.context.url);
        if(!rewritten){request.callbacks.onError({code:403,text:'HLS resource outside video scope'},request.context,null,this.stats);return;}
        request.context.url=rewritten;super.load(request.context,request.config,request.callbacks);
      }
      abort(){waitingLoaders.delete(this);this.pendingRequest=null;super.abort();}
      destroy(){waitingLoaders.delete(this);this.pendingRequest=null;super.destroy();}
    };
  }
  function checkRenewal(){if(canRenew()&&authorizationNow()>=renewalDueAt())requestSourceRenewal();}
  document.addEventListener('visibilitychange',function(){if(!document.hidden)checkRenewal();});
  window.addEventListener('online',checkRenewal);
  var lastInteractionAt=-Infinity;
  function relayInteraction(event){var now=Date.now();if(event.type==='pointermove'&&now-lastInteractionAt<200)return;lastInteractionAt=now;post('playerInteraction');}
  ['pointerover','pointermove','pointerdown','touchstart','keydown'].forEach(function(name){document.addEventListener(name,relayInteraction,{passive:true});});
  function hlsErrorMessage(status){if(status===401||status===403)return 'Bunny رفض رابط HLS ('+status+'). راجع CDN Token Authentication Key وAllowed Domains.';if(status===404)return 'ملف HLS غير موجود على Bunny (404). راجع CDN hostname وانتظر اكتمال ترميز الفيديو.';if(status===0)return relayAttempted?'تعذر تحميل الفيديو عبر المنصة أيضًا. أعد المحاولة، وإذا استمرت المشكلة تواصل مع الدعم.':'تعذر تحميل الفيديو من Bunny على هذا الجهاز. جرّب شبكة أخرى ثم أعد المحاولة.';return status?'تعذر تحميل Bunny HLS (حالة '+status+').':'تعذر تحميل بث Bunny HLS.';}
  function nativePlaybackError(){return 'تعذر تشغيل الفيديو على مشغل الجهاز. لم يحدد المتصفح سبب التعطل. أعد المحاولة، وإذا تكرر توقف التشغيل تواصل مع الدعم.';}
  // Receiving an incomplete segment advances its byte counter before the media clock can move.
  function downloadAdvanced(){var loaded=Number(activeDownload&&activeDownload.stats.loaded)||0;var advanced=loaded>observedDownloadBytes;observedDownloadBytes=loaded;if(advanced)downloadProgressObserved=true;return advanced;}
  function clearPlaybackDeadline(){if(playbackDeadline){clearTimeout(playbackDeadline);playbackDeadline=null;}playbackWaitStartedAt=null;}
  function failHls(status,message,phase){if(terminalErrorSent)return;terminalErrorSent=true;var elapsedMs=Math.max(0,Math.min(120000,Date.now()-loadStartedAt));var online=typeof navigator==='undefined'||navigator.onLine!==false;var visibility=document.hidden?'hidden':'visible';clearRenewalTimer();waitingLoaders.clear();if(loadDeadline)clearTimeout(loadDeadline);clearPlaybackDeadline();if(hls)try{hls.destroy()}catch(e){}video.pause();if(nativePlayback){video.removeAttribute('src');video.load();}post('error',{provider:'bunny-hls',code:Number(status)||0,phase:((relayAttempted?'relay_':'')+String(phase||'unknown')).slice(0,80),elapsedMs:elapsedMs,online:online,visibility:visibility,message:message||hlsErrorMessage(Number(status)||0)});}
  function state(){return {currentTime:video.currentTime||0,duration:isFinite(video.duration)?video.duration:0,volume:Math.round(video.volume*100),isMuted:video.muted,state:video.ended?0:(video.paused?2:1),isPlaying:!video.paused&&!video.ended,playbackRate:video.playbackRate||1,provider:'bunny-hls',signedSourceExpiresAtMs:signedSourceExpiresAtMs,sourceRenewal:nativePlayback?'native':'in-place'};}
  function ready(){sourceReady=true;if(loadDeadline)clearTimeout(loadDeadline);if(readySent)return;readySent=true;post('ready',state());}
  function restoreRelayPlayback(){if(!relayResume||terminalErrorSent)return;var resume=relayResume;relayResume=null;video.playbackRate=resume.rate;video.volume=resume.volume;video.muted=resume.muted;if(resume.time>0)video.currentTime=Math.min(resume.time,Math.max(0,(video.duration||resume.time)-.1));lastMediaTime=Number(video.currentTime)||0;if(!resume.paused)video.play().catch(function(){post('autoplayBlocked',{provider:'bunny-hls'})});}
  function recoverRelayNetwork(status){
    if(!hls||!relayAttempted||terminalErrorSent||relayStallRecoveries>=1
      ||!(status===0||status===408||(status>=500&&status<=599)))return false;
    if(!sourceReady&&Date.now()-loadStartedAt>=maxFragmentLoadMs)return false;
    relayStallRecoveries++;relayRecoveryMediaTime=Number(video.currentTime)||0;
    activeDownload=null;observedDownloadBytes=0;
    if(sourceReady){hls.startLoad(relayRecoveryMediaTime,true);armPlaybackDeadline('network_retry');}
    else{hls.loadSource(source);armLoadDeadline();}
    return true;
  }
  function recoverRelayStall(){
    if(!hls||!relayAttempted||!sourceReady||relayStallRecoveries>=1||Date.now()-playbackWaitStartedAt>=maxFragmentLoadMs)return false;
    relayStallRecoveries++;relayRecoveryMediaTime=Number(video.currentTime)||0;
    hls.startLoad(relayRecoveryMediaTime,true);clearPlaybackDeadline();armPlaybackDeadline('recovery');return true;
  }
  function armPlaybackDeadline(phase){
    lastLoadPhase=phase||lastLoadPhase;if(playbackDeadline||terminalErrorSent)return;
    if(playbackWaitStartedAt===null){playbackWaitStartedAt=Date.now();downloadAdvanced();}
    playbackDeadline=setTimeout(function(){
      playbackDeadline=null;if(video.paused||video.ended)return;
      if(renewalAttempts>0&&(renewalRestart||waitingLoaders.size)){clearPlaybackDeadline();armPlaybackDeadline(lastLoadPhase);return;}
      if(downloadAdvanced()&&Date.now()-playbackWaitStartedAt<maxFragmentLoadMs){armPlaybackDeadline(lastLoadPhase);return;}
      if(!recoverRelayStall()&&!tryRelay(0))failHls(0,'توقف تحميل الفيديو ولم تصل بيانات جديدة. أعد المحاولة، وإذا استمرت المشكلة تواصل مع الدعم.','playback_timeout_'+lastLoadPhase);
    },Math.min(relayAttempted?relayRequestTimeoutMs:15000,maxFragmentLoadMs-(Date.now()-playbackWaitStartedAt)));
  }
  function hlsLevels(){if(!hls)return[];var seen={};return hls.levels.map(function(l,i){var h=Number(l.height)||0;var label=h?String(h)+'p':String(Math.round((l.bitrate||0)/1000))+'k';return {id:String(i),label:label,height:h,bitrate:l.bitrate||0};}).filter(function(l){var k=l.height||l.bitrate;if(seen[k])return false;seen[k]=true;return true;});}
  function emitQuality(){var levels=hls?hlsLevels():nativeLevels;var current='auto';if(hls&&hls.autoLevelEnabled===false&&hls.currentLevel>=0)current=String(hls.currentLevel);else if(!hls)current=nativeCurrent;post('qualityLevels',{levels:levels,currentQuality:current,auto:true});}
  function attachEvents(){
    video.addEventListener('loadedmetadata',function(){lastLoadPhase='metadata';restoreRelayPlayback();ready();post('durationChange',state());});
    video.addEventListener('canplay',function(){lastLoadPhase='canplay';ready();clearPlaybackDeadline();});
    video.addEventListener('seeking',function(){lastMediaTime=Number(video.currentTime)||0;});
    video.addEventListener('timeupdate',function(){if(relayResume)return;var current=Number(video.currentTime)||0;if(current>lastMediaTime+.01){clearPlaybackDeadline();if(current-relayRecoveryMediaTime>=20)relayStallRecoveries=0;}lastMediaTime=current;post('timeUpdate',state());});
    video.addEventListener('play',function(){checkRenewal();armPlaybackDeadline('play');post('stateChange',state());});
    video.addEventListener('pause',function(){clearPlaybackDeadline();post('stateChange',state());});
    video.addEventListener('ended',function(){clearPlaybackDeadline();post('stateChange',state());});
    video.addEventListener('waiting',function(){if(!video.paused)armPlaybackDeadline('waiting');post('stateChange',{state:3,isPlaying:false,provider:'bunny-hls'});});
    video.addEventListener('stalled',function(){if(!video.paused)armPlaybackDeadline('stalled');if(video.readyState<3)post('stateChange',{state:3,isPlaying:false,provider:'bunny-hls'});});
    video.addEventListener('playing',function(){clearPlaybackDeadline();post('stateChange',state());});
    video.addEventListener('ratechange',function(){post('playbackRateChange',{playbackRate:video.playbackRate,provider:'bunny-hls'});});
    video.addEventListener('error',function(){if(!terminalErrorSent)failHls(0,nativePlayback?nativePlaybackError():undefined,nativePlayback?'native_media_error':'media_error');});
  }
  function parseNativeMaster(text){var lines=text.split(/\r?\n/),result=[];for(var i=0;i<lines.length;i++){if(lines[i].indexOf('#EXT-X-STREAM-INF:')!==0)continue;var m=lines[i].match(/RESOLUTION=\d+x(\d+)/),next=(lines[i+1]||'').trim();if(!next||next.charAt(0)==='#')continue;var height=m?Number(m[1]):0;result.push({id:String(result.length),label:height?height+'p':'جودة '+(result.length+1),height:height,bitrate:0,url:new URL(next,source).toString()});}nativeLevels=result;emitQuality();}
  function loadNative(url,quality){var time=video.currentTime||0,paused=video.paused,rate=video.playbackRate,vol=video.volume,muted=video.muted;nativeCurrent=quality;video.src=url;video.load();video.addEventListener('loadedmetadata',function restore(){video.removeEventListener('loadedmetadata',restore);if(time>0)video.currentTime=Math.min(time,Math.max(0,(video.duration||time)-.1));video.playbackRate=rate;video.volume=vol;video.muted=muted;if(!paused)video.play().catch(function(){});emitQuality();});}
  function setQuality(id){if(id==='auto'){if(hls){hls.currentLevel=-1;hls.nextLevel=-1;}else if(nativeCurrent!=='auto')loadNative(masterSource,'auto');emitQuality();return;}if(hls){var index=Number(id);if(Number.isInteger(index)&&index>=0&&index<hls.levels.length){hls.currentLevel=index;hls.nextLevel=index;emitQuality();}}else{var level=nativeLevels.find(function(item){return item.id===id});if(level)loadNative(level.url,id);}}
  attachEvents();
  post('providerLoaded',{provider:'bunny-hls',signedSourceExpiresAtMs:signedSourceExpiresAtMs,sourceRenewal:window.Hls&&window.Hls.isSupported()?'in-place':'native'});
  var loadStartedAt=Date.now();var loadMilestones={};
  function armLoadDeadline(budget){
    if(loadDeadline)clearTimeout(loadDeadline);
    loadDeadline=setTimeout(function(){
      loadDeadline=null;
      if(renewalAttempts>0&&waitingLoaders.size){loadStartedAt=Date.now();armLoadDeadline(20000);return;}
      if(downloadAdvanced()&&Date.now()-loadStartedAt<maxFragmentLoadMs){armLoadDeadline(20000);return;}
      if(tryRelay(0))return;
      failHls(0,'انتهت مهلة تجهيز فيديو Bunny HLS قبل وصول بيانات التشغيل. أعد المحاولة، وإذا استمر التحميل تواصل مع الدعم.','load_timeout_'+lastLoadPhase);
    },Math.max(0,Math.min(Math.max(budget||20000,relayAttempted?relayRequestTimeoutMs:0),((relayAttempted||downloadProgressObserved)?maxFragmentLoadMs:60000)-(Date.now()-loadStartedAt))));
  }
  function loadProgress(phase,budget){if(sourceReady||terminalErrorSent||loadMilestones[phase])return;loadMilestones[phase]=true;lastLoadPhase=phase;armLoadDeadline(budget);}
  function tryRelay(status){
    if(status!==0||relayAttempted||!relaySource||terminalErrorSent)return false;
    relayResume={time:Number(video.currentTime)||0,paused:video.paused,rate:video.playbackRate,volume:video.volume,muted:video.muted};sourceReady=false;
    relayAttempted=true;clearRenewalTimer();waitingLoaders.clear();if(loadDeadline)clearTimeout(loadDeadline);clearPlaybackDeadline();if(hls){var previousHls=hls;hls=null;previousHls.destroy();}
    source=new URL(relaySource,location.origin).toString();masterSource=source;lastLoadPhase='relay_manifest';loadStartedAt=Date.now();loadMilestones={};
    activeDownload=null;observedDownloadBytes=0;downloadProgressObserved=false;
      armLoadDeadline();startPlayer();scheduleRenewal();return true;
  }
  function startPlayer(){
  if(window.Hls&&window.Hls.isSupported()){
    try{
      var playlistPolicy=loadPolicy(relayAttempted?relayRequestTimeoutMs:20000);
      hls=new window.Hls({loader:renewableLoader(),enableWorker:true,capLevelToPlayerSize:true,preferManagedMediaSource:true,startLevel:-1,startPosition:relayResume?relayResume.time:-1,manifestLoadPolicy:playlistPolicy,playlistLoadPolicy:playlistPolicy,keyLoadPolicy:playlistPolicy,fragLoadPolicy:loadPolicy(maxFragmentLoadMs)});hls.loadSource(source);hls.attachMedia(video);scheduleRenewal();
      var loadingInstance=hls;
      hls.on(window.Hls.Events.FRAG_LOADING,function(_,download){if(hls!==loadingInstance)return;activeDownload=download.part||download.frag;observedDownloadBytes=0;});
      hls.on(window.Hls.Events.MANIFEST_PARSED,function(){if(hls!==loadingInstance)return;loadProgress('manifest_parsed',20000);emitQuality();});
      if(window.Hls.Events.LEVEL_LOADED)hls.on(window.Hls.Events.LEVEL_LOADED,function(){if(hls===loadingInstance)loadProgress('level_loaded',40000);});
      if(window.Hls.Events.FRAG_LOADED)hls.on(window.Hls.Events.FRAG_LOADED,function(){if(hls===loadingInstance){relayAuthRecoveries=0;directAuthRecoveries=0;loadProgress('fragment_loaded',20000);}});
      hls.on(window.Hls.Events.LEVEL_SWITCHED,function(){emitQuality();});
      var instance=hls;
      hls.on(window.Hls.Events.ERROR,function(_,data){if(hls!==instance||!data||!data.fatal)return;if(data.type===window.Hls.ErrorTypes.MEDIA_ERROR&&mediaRecoveries<1){mediaRecoveries++;hls.recoverMediaError();return;}var status=data&&data.response?Number(data.response.code||data.response.status||0):0;var phase=data&&data.details?String(data.details):'network';if(data.type===window.Hls.ErrorTypes.NETWORK_ERROR&&canRecoverAuthorization(status)){renewalRestart=true;if(loadDeadline)clearTimeout(loadDeadline);clearPlaybackDeadline();requestSourceRenewal();return;}if(data.type===window.Hls.ErrorTypes.NETWORK_ERROR&&(tryRelay(status)||recoverRelayNetwork(status)))return;failHls(status,hlsErrorMessage(status)+' ['+phase+']',phase);});
    }catch(e){failHls(0,'تعذر بدء مشغل HLS على هذا الجهاز. حدّث المتصفح وAndroid System WebView ثم أعد المحاولة.','hlsjs_bootstrap');}
  }else if(video.canPlayType('application/vnd.apple.mpegurl')){
    nativePlayback=true;
    if(signedScope&&!relayAttempted&&!nativeGrantReady){if(loadDeadline)clearTimeout(loadDeadline);requestSourceRenewal();}
    else startNativePlayer();
  }else failHls(0,'المتصفح لا يدعم تشغيل HLS. حدّث Chrome وAndroid System WebView ثم أعد المحاولة.');
  }
  function startNativePlayer(){
    lastLoadPhase='native_manifest';var requestedSource=source;fetch(source,{credentials:'same-origin',referrerPolicy:'strict-origin-when-cross-origin'}).then(function(r){if(requestedSource!==source)return '';if(!r.ok){failHls(r.status,undefined,'native_manifest_http');return '';}return r.text();}).then(function(text){if(requestedSource!==source||terminalErrorSent||!text)return;lastLoadPhase='native_media';parseNativeMaster(text);video.src=source;video.load()}).catch(function(){if(requestedSource===source&&!tryRelay(0))failHls(0,undefined,'native_manifest_network')});
  }
  armLoadDeadline();startPlayer();
  window.addEventListener('message',function(event){
    if(event.origin!==location.origin||event.source!==parent||terminalErrorSent)return;var msg=event.data||{};
    switch(msg.type){
      case'renewSource':renewSource(msg);break;
      case'sourceRenewalFailed':sourceRenewalFailed(Number(msg.status)||0);break;
      case'play':checkRenewal();if(relayResume)relayResume.paused=false;else video.play().catch(function(){post('autoplayBlocked',{provider:'bunny-hls'})});break;
      case'pause':if(relayResume)relayResume.paused=true;video.pause();break;
      case'togglePlay':if(relayResume)relayResume.paused=!relayResume.paused;else video.paused?video.play().catch(function(){}):video.pause();break;
      case'seekTo':if(isFinite(Number(msg.time))){var seekTime=Math.max(0,Math.min(Number(msg.time),video.duration||Number(msg.time)));if(relayResume)relayResume.time=seekTime;else video.currentTime=seekTime;}break;
      case'setVolume':video.volume=Math.max(0,Math.min(1,Number(msg.volume)/100));if(relayResume)relayResume.volume=video.volume;break;
      case'mute':video.muted=true;if(relayResume)relayResume.muted=true;break;
      case'unmute':video.muted=false;if(relayResume)relayResume.muted=false;break;
      case'setPlaybackRate':var rate=Number(msg.rate);if([.5,.75,1,1.25,1.5,1.75,2].indexOf(rate)>=0){video.playbackRate=rate;if(relayResume)relayResume.rate=rate;}break;
      case'setQuality':setQuality(String(msg.quality||'auto'));break;
      case'getQualityLevels':emitQuality();break;
    }
  });
  setInterval(function(){var wm=document.getElementById('wm');if(wm&&!wm.dataset.configured)wm.style.transform='translate3d('+(Math.random()*38)+'vw,'+(Math.random()*50)+'vh,0)'},12000);
})();
</script></body></html>`;
}
