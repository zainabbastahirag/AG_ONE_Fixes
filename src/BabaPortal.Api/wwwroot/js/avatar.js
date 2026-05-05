// 3D avatar with viseme lip-sync, blinking, and idle animation.
// - "robot" kind: procedural Three.js robot with morph-style mouth scaling.
// - "photo" kind: textured plane billboard with overlay mouth & eye animation.
// - "glb" kind: load GLB from URL (no shape-key blendshape support yet, but rotates & glows).
import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';

const VISEME_OPEN = {
  // approximate mouth-open factor 0..1 per phoneme group
  sil: 0.0,  pp: 0.05, ff: 0.15, th: 0.2,  dd: 0.3,
  kk: 0.25, ch: 0.3,  ss: 0.15, nn: 0.2,  rr: 0.3,
  aa: 0.95, e: 0.6,   i: 0.4,   o: 0.7,   u: 0.4,
};

function phonemeFromChar(c){
  c = c.toLowerCase();
  if ('aá'.includes(c)) return 'aa';
  if ('eé'.includes(c)) return 'e';
  if ('ií'.includes(c)) return 'i';
  if ('oó'.includes(c)) return 'o';
  if ('uúüy'.includes(c)) return 'u';
  if ('mbp'.includes(c)) return 'pp';
  if ('fv'.includes(c)) return 'ff';
  if ('t'.includes(c)) return 'dd';
  if ('d'.includes(c)) return 'dd';
  if ('s'.includes(c)) return 'ss';
  if ('z'.includes(c)) return 'ss';
  if ('n'.includes(c)) return 'nn';
  if ('r'.includes(c)) return 'rr';
  if ('l'.includes(c)) return 'dd';
  if ('k'.includes(c) || 'g'.includes(c) || 'q'.includes(c)) return 'kk';
  if ('ch'.includes(c) || 'sh'.includes(c) || 'j'.includes(c) || 'x'.includes(c)) return 'ch';
  if ('th'.includes(c)) return 'th';
  return 'sil';
}

export class Avatar {
  constructor(canvas){
    this.canvas = canvas;
    this.scene = new THREE.Scene();
    this.scene.background = null;
    this.renderer = new THREE.WebGLRenderer({ canvas, alpha:true, antialias:true });
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.camera = new THREE.PerspectiveCamera(40, 1, 0.1, 100);
    this.camera.position.set(0, 1.55, 3.0);
    this.camera.lookAt(0, 1.4, 0);

    const ambient = new THREE.AmbientLight(0x8888ff, 0.6); this.scene.add(ambient);
    const key = new THREE.DirectionalLight(0xffffff, 1.1); key.position.set(2,3,4); this.scene.add(key);
    const rim = new THREE.PointLight(0x7c3aed, 1.2); rim.position.set(-2,2,-2); this.scene.add(rim);

    this.root = new THREE.Group(); this.scene.add(this.root);
    this.color = new THREE.Color('#7c3aed');
    this._buildRobot();

    this._mouthTarget = 0; this._mouthCurrent = 0;
    this._blinkTimer = 0; this._blinkOpen = 1; // 1 open, 0 closed
    this._emotion = 'neutral'; // neutral/happy/sad/surprised/thinking
    this._speakingChars = [];
    this._lastSpeakTime = 0;
    this._listening = false;
    this._tickHandle = null;
    this._resize();
    window.addEventListener('resize', () => this._resize());
    this._animate();
  }

  _resize(){
    const r = this.canvas.getBoundingClientRect();
    const w = Math.max(2, r.width), h = Math.max(2, r.height);
    this.renderer.setSize(w, h, false);
    this.camera.aspect = w / h;
    this.camera.updateProjectionMatrix();
  }

  setColor(hex){
    this.color = new THREE.Color(hex);
    if (this.head) this.head.material.color.copy(this.color).multiplyScalar(0.4).addScalar(0.1);
    if (this.body) this.body.material.color.copy(this.color).multiplyScalar(0.5);
    if (this.glow) this.glow.material.color.copy(this.color);
    if (this.eyes) this.eyes.forEach(e => e.material.color.copy(this.color).addScalar(0.3));
  }

  // Replace contents based on avatar config { kind, primaryColor, imageUrl, modelUrl }
  setAvatar(cfg){
    while (this.root.children.length) this.root.remove(this.root.children[0]);
    this.head = this.body = this.mouth = this.glow = null; this.eyes = [];
    if (cfg?.primaryColor) this.color = new THREE.Color(cfg.primaryColor);
    if (cfg?.kind === 'photo' && cfg.imageUrl){
      this._buildPhoto(cfg.imageUrl);
    } else if (cfg?.kind === 'glb' && cfg.modelUrl){
      this._buildGlb(cfg.modelUrl);
    } else {
      this._buildRobot();
    }
    if (cfg?.primaryColor) this.setColor(cfg.primaryColor);
  }

  _buildRobot(){
    const head = new THREE.Mesh(
      new THREE.SphereGeometry(0.55, 48, 32),
      new THREE.MeshStandardMaterial({ color: this.color.clone().multiplyScalar(0.6), metalness: 0.5, roughness: 0.3 })
    );
    head.position.y = 1.6;
    this.root.add(head); this.head = head;

    const visor = new THREE.Mesh(
      new THREE.SphereGeometry(0.5, 48, 16, 0, Math.PI * 2, Math.PI * 0.45, Math.PI * 0.25),
      new THREE.MeshStandardMaterial({ color: 0x000000, metalness: 0.1, roughness: 0.1, emissive: 0x222244 })
    );
    visor.position.y = 1.62; visor.position.z = 0.06;
    this.root.add(visor);

    const eyeMat = new THREE.MeshStandardMaterial({ color: this.color.clone().addScalar(0.3), emissive: this.color, emissiveIntensity: 0.8 });
    const eyeL = new THREE.Mesh(new THREE.SphereGeometry(0.06, 16, 16), eyeMat.clone());
    const eyeR = new THREE.Mesh(new THREE.SphereGeometry(0.06, 16, 16), eyeMat.clone());
    eyeL.position.set(-0.16, 1.65, 0.45);
    eyeR.position.set( 0.16, 1.65, 0.45);
    this.root.add(eyeL); this.root.add(eyeR);
    this.eyes = [eyeL, eyeR];

    const mouth = new THREE.Mesh(
      new THREE.BoxGeometry(0.22, 0.05, 0.05),
      new THREE.MeshStandardMaterial({ color: 0x111122, emissive: 0x4f46e5, emissiveIntensity: 0.5 })
    );
    mouth.position.set(0, 1.42, 0.5);
    this.root.add(mouth); this.mouth = mouth;

    const body = new THREE.Mesh(
      new THREE.CylinderGeometry(0.45, 0.55, 1.1, 32),
      new THREE.MeshStandardMaterial({ color: this.color.clone().multiplyScalar(0.4), metalness: 0.6, roughness: 0.35 })
    );
    body.position.y = 0.55;
    this.root.add(body); this.body = body;

    const chest = new THREE.Mesh(
      new THREE.SphereGeometry(0.16, 24, 16),
      new THREE.MeshStandardMaterial({ color: 0xffffff, emissive: this.color, emissiveIntensity: 1.4 })
    );
    chest.position.set(0, 0.8, 0.45);
    this.root.add(chest); this.glow = chest;
  }

  _buildPhoto(url){
    const tex = new THREE.TextureLoader().load(url);
    tex.colorSpace = THREE.SRGBColorSpace;
    const plane = new THREE.Mesh(
      new THREE.PlaneGeometry(1.6, 2.0),
      new THREE.MeshBasicMaterial({ map: tex, transparent: true })
    );
    plane.position.y = 1.0;
    this.root.add(plane); this.head = plane;

    // overlay mouth approximation
    const mouth = new THREE.Mesh(
      new THREE.PlaneGeometry(0.35, 0.05),
      new THREE.MeshBasicMaterial({ color: 0x000000, opacity: 0.55, transparent: true })
    );
    mouth.position.set(0, 0.55, 0.01);
    this.root.add(mouth); this.mouth = mouth;
  }

  _buildGlb(url){
    const loader = new GLTFLoader();
    loader.load(url, gltf => {
      const m = gltf.scene;
      m.scale.setScalar(1.2);
      m.position.y = 0.0;
      this.root.add(m);
      this.head = m;
    }, undefined, err => {
      console.warn('GLB load failed', err);
      this._buildRobot();
    });
  }

  setListening(b){
    this._listening = !!b;
    if (this.glow){
      this.glow.material.emissiveIntensity = b ? 2.4 : 1.4;
    }
  }

  setEmotion(name){ this._emotion = name; }

  // Schedule visemes for a piece of streamed text.
  speakText(text){
    const now = performance.now();
    const start = Math.max(now, this._lastSpeakTime);
    const perChar = 60; // ms/char
    for (let i = 0; i < text.length; i++){
      const ph = phonemeFromChar(text[i]);
      const open = VISEME_OPEN[ph] ?? 0.1;
      this._speakingChars.push({ at: start + i * perChar, open });
    }
    this._lastSpeakTime = start + text.length * perChar;
  }

  stopSpeaking(){ this._speakingChars = []; this._lastSpeakTime = 0; this._mouthTarget = 0; }

  _animate = () => {
    const dt = 1/60;
    const now = performance.now();

    // viseme schedule
    while (this._speakingChars.length && this._speakingChars[0].at <= now){
      this._mouthTarget = this._speakingChars.shift().open;
    }
    if (!this._speakingChars.length && this._lastSpeakTime && now > this._lastSpeakTime + 80){
      this._mouthTarget = 0;
    }
    this._mouthCurrent += (this._mouthTarget - this._mouthCurrent) * 0.35;

    if (this.mouth){
      // For robot: scale mouth in Y. For photo: scale plane in Y.
      const baseY = this.mouth.geometry?.parameters?.height || 0.05;
      const factor = 1 + this._mouthCurrent * 6;
      this.mouth.scale.set(1, factor, 1);
      this.mouth.position.y = (this.mouth.position.y || 0); // unchanged
    }

    // blink
    this._blinkTimer -= dt;
    if (this._blinkTimer <= 0){
      this._blinkTimer = 2 + Math.random() * 4;
      this._blinkOpen = 0;
      setTimeout(() => { this._blinkOpen = 1; }, 110);
    }
    if (this.eyes){
      this.eyes.forEach(e => { e.scale.y = 0.2 + 0.8 * this._blinkOpen; });
    }

    // idle bob
    this.root.position.y = Math.sin(now * 0.0015) * 0.04;
    this.root.rotation.y = Math.sin(now * 0.0009) * 0.18;

    // emotional tilt
    if (this.head){
      const targetTilt = this._emotion === 'happy' ? 0.05 :
                         this._emotion === 'sad' ? -0.08 :
                         this._emotion === 'surprised' ? 0.0 :
                         this._emotion === 'thinking' ? -0.04 : 0;
      this.head.rotation.z += (targetTilt - this.head.rotation.z) * 0.05;
    }

    // listening pulse
    if (this.glow){
      const pulse = this._listening ? 1.4 + Math.sin(now*0.012)*0.6 : 1.4;
      this.glow.material.emissiveIntensity = pulse;
    }

    this.renderer.render(this.scene, this.camera);
    this._tickHandle = requestAnimationFrame(this._animate);
  }

  dispose(){
    cancelAnimationFrame(this._tickHandle);
    this.renderer.dispose();
  }
}
