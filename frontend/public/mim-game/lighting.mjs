import * as T from './vendor/three.module.js';
import { RoomEnvironment } from './vendor/addons/environments/RoomEnvironment.js';
import { EffectComposer } from './vendor/addons/postprocessing/EffectComposer.js';
import { RenderPass } from './vendor/addons/postprocessing/RenderPass.js';
import { UnrealBloomPass } from './vendor/addons/postprocessing/UnrealBloomPass.js';
import { OutputPass } from './vendor/addons/postprocessing/OutputPass.js';

export function lightMuseum(scene, renderer) {
  const room = new RoomEnvironment();
  const generator = new T.PMREMGenerator(renderer);
  const environment = generator.fromScene(room,.025);
  scene.environment = environment.texture; scene.environmentIntensity = .26;
  room.dispose(); generator.dispose();
  scene.add(new T.HemisphereLight(0xb4c8e3,0x4f3620,.38));
  const key = new T.DirectionalLight(0xffd0a0,2.9); key.position.set(-7,13,-4); key.castShadow = true;
  key.shadow.mapSize.set(2048,2048); key.shadow.normalBias=.028; key.shadow.bias=-.0001;
  Object.assign(key.shadow.camera,{left:-16,right:16,top:16,bottom:-16,near:.5,far:45}); scene.add(key);
  const fill = new T.DirectionalLight(0xbad7e5,.65); fill.position.set(5,6,10); scene.add(fill);
  const rim = new T.SpotLight(0xffdc9e,150,25,.7,.85,1.6); rim.position.set(0,9,-9);
  rim.target.position.set(0,1,3); scene.add(rim,rim.target);
}

export function cinematicPipeline(renderer, scene, camera) {
  const composer = new EffectComposer(renderer);
  composer.addPass(new RenderPass(scene,camera));
  composer.addPass(new UnrealBloomPass(new T.Vector2(1,1),.32,.55,1.15));
  composer.addPass(new OutputPass());
  return composer;
}
