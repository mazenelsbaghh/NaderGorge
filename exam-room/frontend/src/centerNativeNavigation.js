const destinations=new Set(['room','exams','sheets','catalog','whatsapp','storage','guide']);
const bridges=new WeakMap();

function destination(view) {
  return view==='editor'?'exams':destinations.has(view)?view:null;
}

function post(window,bridge,payload) {
  window.chrome.webview.postMessage({kind:'massar-center-navigation',nonce:bridge.nonce,documentId:bridge.documentId,...payload});
}

export function publishCenterNativeNavigation(window,view,requestId) {
  const bridge=bridges.get(window),next=destination(view);
  if(!bridge||!next)return;
  bridge.view=next;
  post(window,bridge,{action:'state',destination:next,...(requestId?{requestId}:{})});
}

export function installCenterNativeNavigation(window,document,{navigate,canLeave,onError}) {
  const query=new URLSearchParams(window.location.search);
  const nonce=window.__massarCenterNavigationNonce;
  if(query.get('embedded')!=='center'||query.get('native-shell')!=='1'||
     window.location.protocol!=='http:'||window.location.hostname!=='127.0.0.1'||
     window.top!==window||typeof nonce!=='string'||!/^[a-f0-9]{48}$/.test(nonce)||
     !window.chrome?.webview?.postMessage||!window.chrome?.webview?.addEventListener)return()=>{};
  let bridge=bridges.get(window);
  if(!bridge){
    const bytes=window.crypto.getRandomValues(new Uint8Array(16));
    bridge={nonce,documentId:Array.from(bytes,value=>value.toString(16).padStart(2,'0')).join(''),view:'room',tail:Promise.resolve()};
    bridges.set(window,bridge);
  }
  bridge.navigate=navigate;bridge.canLeave=canLeave;bridge.onError=onError;
  document.body.classList.add('center-native-shell');
  const receive=event=>{
    const message=event.data;
    if(message?.kind!=='massar-center-navigation'||message.nonce!==bridge.nonce||
       message.documentId!==bridge.documentId||!Number.isSafeInteger(message.requestId)||message.requestId<1||
       !['navigate','leave'].includes(message.action)||
       (message.action==='navigate'&&!destinations.has(message.destination)))return;
    bridge.tail=bridge.tail.then(async()=>{
      try{
        if(message.action==='leave'){
          const allowed=await bridge.canLeave();
          post(window,bridge,{action:'leave-result',requestId:message.requestId,allowed:allowed===true});
        }else{
          const accepted=await bridge.navigate(message.destination);
          publishCenterNativeNavigation(window,accepted===true?message.destination:bridge.view,message.requestId);
        }
      }catch(error){
        if(message.action==='leave')post(window,bridge,{action:'leave-result',requestId:message.requestId,allowed:false});
        else publishCenterNativeNavigation(window,bridge.view,message.requestId);
        bridge.onError?.(error);
      }
    });
  };
  window.chrome.webview.addEventListener('message',receive);
  post(window,bridge,{action:'ready',destination:bridge.view});
  return()=>window.chrome.webview.removeEventListener('message',receive);
}
