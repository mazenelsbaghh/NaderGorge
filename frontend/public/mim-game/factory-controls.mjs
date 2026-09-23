export function bindFactoryControls(canMove) {
  const move = { x: 0, y: 0 },
    camera = { x: 0, y: 0 },
    keys = new Set(), actions = new Set();
  const listeners = new AbortController(),
    releases = [];
  const on = (node, event, action) =>
    node.addEventListener(event, action, { signal: listeners.signal });
  function bindStick(id, input) {
    const stick = document.getElementById(id),
      knob = stick.querySelector('.thumbstick-knob');
    let pointer = null;
    const release = (event) => {
      if (event && event.pointerId !== pointer) return;
      const captured = pointer;
      pointer = null;
      if (captured !== null && stick.hasPointerCapture(captured)) stick.releasePointerCapture(captured);
      input.x = input.y = 0;
      knob.style.transform = '';
      stick.classList.remove('active');
    };
    const update = (event) => {
      if (event.pointerId !== pointer) return;
      const bounds = stick.getBoundingClientRect(), radius = Math.max(1, bounds.width * 0.34);
      const x = (event.clientX - bounds.left - bounds.width / 2) / radius;
      const y = (event.clientY - bounds.top - bounds.height / 2) / radius;
      const length = Math.hypot(x, y), scale = Math.max(1, length);
      const intensity = Math.max(0, (Math.min(1, length) - 0.14) / 0.86);
      input.x = x / (length || 1) * intensity;
      input.y = y / (length || 1) * intensity;
      knob.style.transform = `translate(${x / scale * radius}px,${y / scale * radius}px)`;
    };
    releases.push(release);
    on(stick, 'pointerdown', (event) => {
      if (!canMove() || pointer !== null) return;
      event.preventDefault();
      pointer = event.pointerId;
      stick.setPointerCapture(pointer);
      stick.classList.add('active');
      update(event);
    });
    on(stick, 'pointermove', update);
    for (const event of ['pointerup', 'pointercancel', 'lostpointercapture'])
      on(stick, event, release);
  }
  bindStick('moveStick', move);
  bindStick('cameraStick', camera);
  for (const [id, action] of [['jumpButton', 'jump'], ['dashButton', 'dash'], ['grappleButton', 'grapple']]) {
    on(document.getElementById(id), 'pointerdown', (event) => {
      if (!canMove()) return;
      event.preventDefault();
      actions.add(action);
    });
  }
  on(document, 'keydown', (event) => {
    if (!canMove() || event.target.closest('button,a,select,input,textarea,dialog'))
      return;
    if ([' ', 'q', 'e'].includes(event.key.toLowerCase()) && !event.repeat) {
      event.preventDefault();
      actions.add(event.key === ' ' ? 'jump' : event.key.toLowerCase() === 'e' ? 'grapple' : 'dash');
    }
    if (
      [
        'arrowup',
        'arrowdown',
        'arrowleft',
        'arrowright',
        'w',
        'a',
        's',
        'd',
        'shift',
      ].includes(event.key.toLowerCase())
    ) {
      event.preventDefault();
      keys.add(event.key.toLowerCase());
    }
  });
  on(document, 'keyup', (event) => keys.delete(event.key.toLowerCase()));
  const reset = () => {
    keys.clear();
    actions.clear();
    releases.forEach((release) => release());
  };
  on(window, 'blur', reset);
  on(document, 'visibilitychange', reset);
  return {
    movement() {
      return {
        x:
          Number(keys.has('arrowright') || keys.has('d')) -
            Number(keys.has('arrowleft') || keys.has('a')) || move.x,
        y:
          Number(keys.has('arrowdown') || keys.has('s')) -
            Number(keys.has('arrowup') || keys.has('w')) || move.y,
      };
    },
    camera,
    sprinting() { return keys.has('shift'); },
    consume(action) {
      const pressed = actions.has(action);
      actions.delete(action);
      return pressed;
    },
    reset,
    dispose() {
      reset();
      listeners.abort();
    },
  };
}
