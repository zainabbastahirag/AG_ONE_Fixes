(function () {
  const canvas = document.getElementById("hero-canvas");
  if (!canvas || typeof THREE === "undefined") return;

  const scene = new THREE.Scene();
  const camera = new THREE.PerspectiveCamera(60, window.innerWidth / window.innerHeight, 0.1, 1000);
  camera.position.z = 42;

  const renderer = new THREE.WebGLRenderer({ canvas, alpha: true, antialias: true });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
  renderer.setSize(window.innerWidth, window.innerHeight);

  const nodes = [];
  const nodeGroup = new THREE.Group();
  const count = 28;
  const geo = new THREE.SphereGeometry(0.35, 16, 16);
  const mat = new THREE.MeshBasicMaterial({ color: 0x38bdf8 });

  for (let i = 0; i < count; i++) {
    const mesh = new THREE.Mesh(geo, mat.clone());
    mesh.material.color.setHSL(0.55 + Math.random() * 0.15, 0.8, 0.6);
    mesh.position.set(
      (Math.random() - 0.5) * 50,
      (Math.random() - 0.5) * 30,
      (Math.random() - 0.5) * 40
    );
    nodes.push({ mesh, speed: 0.2 + Math.random() * 0.5, phase: Math.random() * Math.PI * 2 });
    nodeGroup.add(mesh);
  }
  scene.add(nodeGroup);

  const lineMat = new THREE.LineBasicMaterial({ color: 0x818cf8, transparent: true, opacity: 0.35 });
  const lines = [];
  for (let i = 0; i < count; i++) {
    for (let j = i + 1; j < count; j++) {
      if (Math.random() > 0.92) {
        const geom = new THREE.BufferGeometry().setFromPoints([nodes[i].mesh.position, nodes[j].mesh.position]);
        const line = new THREE.Line(geom, lineMat);
        lines.push({ line, a: i, b: j });
        scene.add(line);
      }
    }
  }

  const cardGeo = new THREE.PlaneGeometry(4, 2.5);
  const cards = [];
  for (let c = 0; c < 4; c++) {
    const card = new THREE.Mesh(
      cardGeo,
      new THREE.MeshBasicMaterial({
        color: 0x1e293b,
        transparent: true,
        opacity: 0.55,
        side: THREE.DoubleSide
      })
    );
    card.position.set((c - 1.5) * 8, Math.sin(c) * 4, -5 + c * 3);
    card.rotation.y = 0.4;
    cards.push(card);
    nodeGroup.add(card);
  }

  const particles = new THREE.BufferGeometry();
  const pCount = 400;
  const positions = new Float32Array(pCount * 3);
  for (let i = 0; i < pCount * 3; i++) positions[i] = (Math.random() - 0.5) * 80;
  particles.setAttribute("position", new THREE.BufferAttribute(positions, 3));
  scene.add(new THREE.Points(particles, new THREE.PointsMaterial({ color: 0x38bdf8, size: 0.12, transparent: true, opacity: 0.6 })));

  let mouseX = 0, mouseY = 0;
  document.addEventListener("mousemove", (e) => {
    mouseX = (e.clientX / window.innerWidth - 0.5) * 2;
    mouseY = (e.clientY / window.innerHeight - 0.5) * 2;
  });

  const clock = new THREE.Clock();
  function animate() {
    requestAnimationFrame(animate);
    const t = clock.getElapsedTime();
    nodeGroup.rotation.y = t * 0.08 + mouseX * 0.15;
    nodeGroup.rotation.x = mouseY * 0.08;
    nodes.forEach((n, i) => {
      n.mesh.position.y += Math.sin(t * n.speed + n.phase) * 0.02;
    });
    lines.forEach(({ line, a, b }) => {
      const pts = [nodes[a].mesh.position, nodes[b].mesh.position];
      line.geometry.setFromPoints(pts);
    });
    cards.forEach((card, i) => {
      card.rotation.y = 0.4 + Math.sin(t * 0.5 + i) * 0.3;
      card.position.y = Math.sin(t * 0.4 + i * 2) * 2;
    });
    camera.position.x += (mouseX * 6 - camera.position.x) * 0.02;
    camera.position.y += (-mouseY * 4 - camera.position.y) * 0.02;
    camera.lookAt(0, 0, 0);
    renderer.render(scene, camera);
  }
  animate();

  window.addEventListener("resize", () => {
    camera.aspect = window.innerWidth / window.innerHeight;
    camera.updateProjectionMatrix();
    renderer.setSize(window.innerWidth, window.innerHeight);
  });
})();
