import * as T from './vendor/three.module.js';
import { roundedBox } from './geometry.mjs';

function plaque(text,width=4) {
  const canvas=document.createElement('canvas');canvas.width=1024;canvas.height=320;
  const context=canvas.getContext('2d');context.fillStyle='#142d42';context.fillRect(0,0,1024,320);
  context.strokeStyle='#c2a362';context.lineWidth=9;context.strokeRect(8,8,1008,304);
  context.font='bold 60px Tahoma';context.textAlign='center';context.textBaseline='middle';context.fillStyle='#fff0d4';
  const words=text.split(' ');const lines=[];let line='';
  for(const word of words){const candidate=`${line} ${word}`.trim();if(context.measureText(candidate).width>930){lines.push(line);line=word;}else line=candidate;}lines.push(line);
  lines.forEach((content,index)=>context.fillText(content,512,160+(index-(lines.length-1)/2)*76));
  const texture=new T.CanvasTexture(canvas);texture.colorSpace=T.SRGBColorSpace;
  const sign=new T.Mesh(new T.PlaneGeometry(width,width*.3125),new T.MeshBasicMaterial({map:texture}));
  sign.userData.ownsMaterial=true;return sign;
}

export function buildMechanism(scene,palette) {
  const group=new T.Group();group.visible=false;scene.add(group);
  const stone=new T.MeshStandardMaterial({color:0xb5a58b,map:palette.stone.map,roughness:.55});
  const brass=new T.MeshStandardMaterial({color:0xb99454,metalness:.7,roughness:.3});
  const teal=new T.MeshStandardMaterial({color:0x1e8f94,metalness:.45,roughness:.3});
  const active=new T.MeshStandardMaterial({color:0x4cffff,emissive:0x21c5c8,emissiveIntensity:1.1});
  const meshes=[];let dial;let targetRotation=0;let state;let pulse=0;
  function add(geometry,material,position,parent=group) {
    const object=new T.Mesh(geometry,material);object.position.set(...position);object.castShadow=true;object.receiveShadow=true;parent.add(object);return object;
  }
  function clear() {
    group.traverse(object=>{if(object.isMesh){object.geometry.dispose();if(object.userData.ownsMaterial){object.material.map.dispose();object.material.dispose();}}});
    group.clear();meshes.length=0;
  }
  function rebuild(nextState) {
    clear();state=nextState;group.visible=true;group.position.set(0,0,3);
    add(new T.CylinderGeometry(2.2,2.5,.5,64),stone,[0,.27,0]);
    add(new T.CylinderGeometry(1.25,1.45,.6,48),teal,[0,.8,0]);
    dial=new T.Group();dial.position.set(0,1.2,0);group.add(dial);
    const wheel=add(new T.TorusGeometry(1.04,.09,12,64),brass,[0,0,0],dial);wheel.rotation.x=Math.PI/2;wheel.userData.action="rotate";
    for(let index=0;index<6;index++){const spoke=add(new T.BoxGeometry(.08,.08,1.9),brass,[0,0,0],dial);spoke.rotation.y=index*Math.PI/3;}
    add(new T.SphereGeometry(.21,24,16),active,[0,.08,0],dial);
    const pointer=add(new T.ConeGeometry(.18,.5,16),active,[0,.05,-1.1],dial);pointer.rotation.x=-Math.PI/2;
    for(let index=0;index<nextState.choices.length;index++) {
      const offset=(index-(nextState.choices.length-1)/2)*3;
      const position=new T.Vector3(offset,0,-5);
      add(new T.CylinderGeometry(.95,1.15,.5,32),stone,[offset,.25,-5]);
      add(roundedBox([1.65,1.6,1.2],.05),stone,[offset,1.3,-5]);
      add(roundedBox([.46,.92,.06],.08),teal,[offset,1,-4.37]);
      for(const side of [-.66,-.29,.29,.66]) {
        add(new T.CylinderGeometry(.07,.09,1.45,16),stone,[offset+side,1.33,-4.27]);
        add(new T.BoxGeometry(.2,.09,.2),brass,[offset+side,2.02,-4.27]);
      }
      add(roundedBox([1.97,.14,1.45],.03),stone,[offset,2.12,-4.94]);
      const roof=add(new T.ConeGeometry(1.15,.48,4),stone,[offset,2.42,-4.94]);roof.rotation.y=Math.PI/4;
      add(new T.SphereGeometry(.1,16,12),brass,[offset,2.74,-4.94]);
      const sign=plaque(nextState.choices[index],2.7);sign.position.set(offset,3.15,-4.6);group.add(sign);
      const curve=new T.QuadraticBezierCurve3(new T.Vector3(0,.57,-.7),new T.Vector3(offset,.57,-2.2),position.clone().setY(.57));
      const tube=add(new T.TubeGeometry(curve,24,.055,8,false),index===nextState.choice?active:teal,[0,0,0]);meshes.push(tube);
    }
    const task=plaque(nextState.label,3.7);task.position.set(-4.6,3.1,1.5);group.add(task);
    targetRotation=Math.atan2(-(nextState.choice-(nextState.choices.length-1)/2)*3,5);
    dial.rotation.y=targetRotation;
  }
  return {
    show:rebuild,
    hit(raycaster){return raycaster.intersectObject(group,true).some(hit=>hit.object.userData.action==='rotate');},
    hide(){group.visible=false;},
    rotate(choice){state.choice=choice;targetRotation=Math.atan2(-(choice-(state.choices.length-1)/2)*3,5);meshes.forEach((route,index)=>{route.material=index===choice?active:teal;});},
    success(){pulse=1.8;},
    update(delta){if(!group.visible)return;dial.rotation.y=T.MathUtils.lerp(dial.rotation.y,targetRotation,1-Math.exp(-delta*8));pulse=Math.max(0,pulse-delta);active.emissiveIntensity=pulse?1.3+Math.sin(pulse*12)*.35:.85;},
  };
}
