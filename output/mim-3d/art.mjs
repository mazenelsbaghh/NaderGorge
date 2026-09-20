import * as T from './vendor/three.module.js';

function add(parent, geometry, surface, position) {
  const shape = new T.Mesh(geometry, surface); shape.position.set(...position);
  shape.castShadow = true; shape.receiveShadow = true; parent.add(shape); return shape;
}

export function marbleTexture() {
  const canvas = document.createElement('canvas'); canvas.width = canvas.height = 512;
  const context = canvas.getContext('2d'); context.fillStyle = '#a89e8b'; context.fillRect(0, 0, 512, 512);
  for (let index = 0; index < 65; index++) {
    context.beginPath(); context.strokeStyle = `rgba(60,64,63,${.015 + (index % 5) * .008})`; context.lineWidth = 1 + index % 3;
    for (let y = 0; y <= 512; y += 8) {
      const x = index * 29 % 512 + Math.sin(y * .013 + index) * 23 + Math.sin(y * .049) * 8;
      if (y === 0) context.moveTo(x, y); else context.lineTo(x, y);
    }
    context.stroke();
  }
  context.strokeStyle = '#716b61'; context.lineWidth = 3; context.strokeRect(1, 1, 510, 510);
  const texture = new T.CanvasTexture(canvas); texture.colorSpace = T.SRGBColorSpace;
  texture.wrapS = texture.wrapT = T.RepeatWrapping; texture.repeat.set(10, 10); return texture;
}

function arch(scene, x, palette) {
  const archShape = new T.Shape(); archShape.absarc(0, 0, 2.3, 0, Math.PI, false);
  archShape.lineTo(-1.92, 0); archShape.absarc(0, 0, 1.92, Math.PI, 0, true); archShape.closePath();
  add(scene, new T.ExtrudeGeometry(archShape, { depth: .4, bevelEnabled: true, bevelSize: .04, bevelThickness: .04, bevelSegments: 2, steps: 1 }), palette.light, [x, 5.7, -11.15]);
  for (const side of [-1, 1]) add(scene, new T.BoxGeometry(.38, 5.5, .5), palette.light, [x + side * 2.1, 2.95, -10.95]);
  add(scene, new T.BoxGeometry(3.7, 5.6, .2), palette.navy, [x, 3, -11.45]);
}

function urn(scene, position, palette) {
  const group = new T.Group(); group.position.set(...position); scene.add(group);
  const profile = [[.36,0],[.42,.08],[.3,.2],[.58,.5],[.65,.95],[.51,1.4],[.3,1.55],[.3,1.8],[.4,1.87]].map(([x,y]) => new T.Vector2(x,y));
  add(group, new T.LatheGeometry(profile, 48), palette.light, [0,0,0]);
  for (const y of [.17,.48,1.35,1.8]) {
    const ring = add(group, new T.TorusGeometry(y === .48 ? .58 : y === 1.35 ? .53 : .34, .035, 8, 48), palette.gold, [0,y,0]); ring.rotation.x = Math.PI / 2;
  }
}

function plant(scene, position, palette) {
  const group = new T.Group(); group.position.set(...position); scene.add(group);
  add(group, new T.CylinderGeometry(.65,.45,.85,24), palette.navy, [0,.42,0]);
  const leaves = new T.MeshStandardMaterial({ color: 0x344e38, roughness: .85, side: T.DoubleSide });
  for (let index = 0; index < 9; index++) {
    const angle = index * 2.4; const leaf = add(group, new T.SphereGeometry(1,12,8), leaves, [Math.sin(angle)*.4,1.4+(index%3)*.3,Math.cos(angle)*.4]);
    leaf.scale.set(.18,1.25,.065); leaf.rotation.set(Math.cos(angle)*.55,angle,Math.sin(angle)*.55);
  }
}

function ropes(scene, palette) {
  const velvet = new T.MeshStandardMaterial({color:0x623537,roughness:.95});
  for (const z of [-5,-2,1]) {
    add(scene,new T.CylinderGeometry(.06,.09,1.15,16),palette.gold,[-7.5,.6,z]);
    add(scene,new T.SphereGeometry(.13,16,12),palette.gold,[-7.5,1.2,z]);
    add(scene,new T.CylinderGeometry(.26,.32,.09,20),palette.gold,[-7.5,.07,z]);
    if (z === 1) continue;
    const path=new T.CatmullRomCurve3([new T.Vector3(-7.5,1.2,z),new T.Vector3(-7.5,.85,z+1.5),new T.Vector3(-7.5,1.2,z+3)]);
    add(scene,new T.TubeGeometry(path,24,.055,8,false),velvet,[0,0,0]);
  }
}

export function dressMuseum(scene, palette) {
  for (const x of [-9,9]) { arch(scene,x,palette); plant(scene,[x,0,-8.5],palette); }
  for (const x of [-5,5]) {
    add(scene,new T.BoxGeometry(.3,8.3,.25),palette.gold,[x,4.2,-10.85]);
    urn(scene,[x,0,-9.5],palette);
  }
  for (let index=0;index<19;index++) add(scene,new T.BoxGeometry(.55,.22,.5),palette.light,[-13.5+index*1.5,8.8,-11.35]);
  ropes(scene,palette);
}

function banner(scene,x,palette) {
  const shape=new T.Shape();shape.moveTo(-.85,0);shape.lineTo(.85,0);shape.lineTo(.85,-3);shape.lineTo(0,-3.45);shape.lineTo(-.85,-3);shape.closePath();
  const cloth=add(scene,new T.ShapeGeometry(shape,16),palette.navy,[x,7.4,-10.6]);
  cloth.material=palette.navy.clone();cloth.material.side=T.DoubleSide;cloth.material.roughness=.95;
  add(scene,new T.BoxGeometry(1.95,.07,.08),palette.gold,[x,7.5,-10.54]);
  const canvas=document.createElement('canvas');canvas.width=256;canvas.height=512;
  const context=canvas.getContext('2d');context.strokeStyle='#d8bf88';context.lineWidth=5;
  context.strokeRect(14,10,228,476);context.beginPath();context.moveTo(151,101);context.bezierCurveTo(29,187,224,183,112,274);context.lineWidth=18;context.stroke();
  context.fillStyle='#d8bf88';context.font='26px serif';context.textAlign='center';context.fillText('مسار',128,363);
  const texture=new T.CanvasTexture(canvas);texture.colorSpace=T.SRGBColorSpace;
  add(scene,new T.PlaneGeometry(1.62,3.05),new T.MeshBasicMaterial({map:texture,transparent:true}),[x,5.84,-10.58]);
}

export function finishArchitecture(scene,palette) {
  for(const x of [-6.45,6.45]) banner(scene,x,palette);
  add(scene,new T.BoxGeometry(29,.35,28),palette.stone,[0,10.4,1]);
  for(const z of [-10,-3,5,12]) {
    add(scene,new T.BoxGeometry(28,.45,.7),palette.light,[0,9.8,z]);
    add(scene,new T.BoxGeometry(28,.045,.77),palette.gold,[0,9.55,z]);
  }
  for(const x of [-13.6,13.6]) for(const z of [-6,2,10]) {
    const panel=add(scene,new T.BoxGeometry(.15,4,3.5),palette.light,[x,3.3,z]);
    for(const offset of [-1.65,1.65]) add(scene,new T.BoxGeometry(.23,3.9,.055),palette.gold,[x,3.3,z+offset]);
    for(const y of [1.4,5.2]) add(scene,new T.BoxGeometry(.23,.055,3.35),palette.gold,[x,y,z]);
    panel.material=palette.stone;
  }
}
