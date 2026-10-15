import {test} from 'node:test';
import assert from 'node:assert/strict';
import {webcrypto} from 'node:crypto';
import {installCenterNativeNavigation,publishCenterNativeNavigation} from './centerNativeNavigation.js';

const flush=()=>new Promise(resolve=>setImmediate(resolve));
function webviewBoundary(){
 const messages=[],listeners=new Set(),classes=new Set();
 const window={location:{search:'?embedded=center&native-shell=1',protocol:'http:',hostname:'127.0.0.1'},crypto:webcrypto,__massarCenterNavigationNonce:'a'.repeat(48)};
 window.top=window;
 window.chrome={webview:{postMessage:message=>messages.push(message),addEventListener:(_,listener)=>listeners.add(listener),removeEventListener:(_,listener)=>listeners.delete(listener)}};
 const document={body:{classList:{add:className=>classes.add(className)}}};
 const send=overrides=>{
  const ready=messages.findLast(message=>message.action==='ready');
  const message={kind:'massar-center-navigation',nonce:ready.nonce,documentId:ready.documentId,requestId:1,action:'navigate',destination:'exams',...overrides};
  for(const listener of listeners)listener({data:message});
 };
 return {window,document,messages,classes,send};
}

// The browser/Windows message event is the external boundary. These tests keep
// the real protocol and model only the caller's asynchronous navigation result.
test('foreign, stale and malformed native requests cannot change the visible destination',async()=>{
 for(const location of [{search:'?embedded=center'},{search:'?native-shell=1'},{hostname:'outside.test'}]){
  const ordinary=webviewBoundary();Object.assign(ordinary.window.location,location);
  installCenterNativeNavigation(ordinary.window,ordinary.document,{navigate:async()=>true,canLeave:async()=>true});
  assert.equal(ordinary.messages.length,0);
  assert.equal(ordinary.classes.has('center-native-shell'),false);
 }
 const browser=webviewBoundary();
 const close=installCenterNativeNavigation(browser.window,browser.document,{navigate:async()=>true,canLeave:async()=>true});
 const invalid=[{nonce:'b'.repeat(48)},{documentId:'c'.repeat(32)},{destination:'editor'},{destination:'https://outside.test/'},{requestId:0},{requestId:'1'},{requestId:1.5},{requestId:Number.MAX_SAFE_INTEGER+1},{action:'authorize'},{kind:'other'}];
 for(const payload of invalid)browser.send(payload);
 await flush();
 assert.equal(browser.messages.length,1);
 browser.send({requestId:2});await flush();
 assert.deepEqual(browser.messages.at(-1),{...browser.messages[0],action:'state',destination:'exams',requestId:2});
 close();
});

test('cancelled navigation retains the editor destination and a failed leave recovers without approving it',async()=>{
 const browser=webviewBoundary();let dirty=true,failedOnce=false;const errors=[];
 installCenterNativeNavigation(browser.window,browser.document,{navigate:async()=>!dirty,canLeave:async()=>{if(!failedOnce){failedOnce=true;throw new Error('The draft check failed');}return true;},onError:error=>errors.push(error.message)});
 publishCenterNativeNavigation(browser.window,'editor');
 browser.send({requestId:1,destination:'room'});await flush();
 assert.equal(browser.messages.at(-1).destination,'exams');
 assert.equal(browser.messages.at(-1).requestId,1);
 browser.send({action:'leave',requestId:2});await flush();
 assert.equal(browser.messages.at(-1).action,'leave-result');
 assert.equal(browser.messages.at(-1).allowed,false);
 assert.deepEqual(errors,['The draft check failed']);
 browser.send({action:'leave',requestId:3});await flush();
 assert.equal(browser.messages.at(-1).allowed,true);
 dirty=false;browser.send({requestId:4,destination:'room'});await flush();
 assert.equal(browser.messages.at(-1).destination,'room');
 assert.equal(browser.messages.at(-1).requestId,4);
});

test('react subscription replacement keeps the document identity and handles each request once',async()=>{
 const browser=webviewBoundary();
 const handlers={navigate:async()=>true,canLeave:async()=>true};
 const closeFirst=installCenterNativeNavigation(browser.window,browser.document,handlers);
 publishCenterNativeNavigation(browser.window,'sheets');
 const firstDocument=browser.messages[0].documentId;
 closeFirst();
 const closeSecond=installCenterNativeNavigation(browser.window,browser.document,handlers);
 assert.equal(browser.messages.at(-1).documentId,firstDocument);
 assert.equal(browser.messages.at(-1).destination,'sheets');
 browser.send({requestId:7,destination:'storage'});await flush();
 assert.equal(browser.messages.filter(message=>message.requestId===7).length,1);
 assert.equal(browser.messages.at(-1).destination,'storage');
 closeSecond();const before=browser.messages.length;
 browser.send({requestId:8,destination:'room'});await flush();
 assert.equal(browser.messages.length,before);
});

test('rapid native requests wait for the earlier asynchronous navigation and acknowledge their actual order',async()=>{
 const browser=webviewBoundary();let finishFirst;
 const first=new Promise(resolve=>{finishFirst=resolve;});
 installCenterNativeNavigation(browser.window,browser.document,{navigate:async target=>{if(target==='exams')await first;return true;},canLeave:async()=>true});
 browser.send({requestId:1,destination:'exams'});
 browser.send({requestId:2,destination:'sheets'});
 browser.send({requestId:3,action:'leave'});
 await flush();
 assert.equal(browser.messages.filter(message=>message.requestId).length,0);
 finishFirst();await flush();
 assert.deepEqual(browser.messages.filter(message=>message.requestId).map(message=>[message.requestId,message.action,message.destination||message.allowed]),[[1,'state','exams'],[2,'state','sheets'],[3,'leave-result',true]]);
});
