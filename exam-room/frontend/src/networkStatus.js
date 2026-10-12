import {useSyncExternalStore} from 'react';

let latestStatus=null;
const subscribers=new Set();

export function publishNetworkStatus(status) {
  latestStatus=status;
  subscribers.forEach(notify=>notify());
}

function subscribe(notify) {
  subscribers.add(notify);
  return()=>subscribers.delete(notify);
}

export function useNetworkStatus() {
  return useSyncExternalStore(subscribe,()=>latestStatus);
}

export function verifiedPortalUrl(status) {
  const portal=status?.capport;
  if(portal?.appliedMode!=='https'||!['ready','expiring'].includes(portal?.state))return '';
  const hostname=portal.installed?.hostname;
  if(typeof hostname!=='string'||!/^([a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z][a-z0-9-]+$/i.test(hostname))return '';
  return `https://${hostname.toLowerCase()}/`;
}
