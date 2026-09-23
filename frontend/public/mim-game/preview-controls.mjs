export function bindPreviewControls(canMove) {
  const move = { x: 0, y: 0 },
    camera = { x: 0, y: 0 },
    keys = new Set();
  const listeners = new AbortController(),
    releases = [];
  const on = (node, event, action) =>
    node.addEventListener(event, action, { signal: listeners.signal });
  function bindStick(id, input) {
    const stick = document.getElementById(id),
      knob = stick.querySelector('.thumbstick-knob');
    let pointer = null;
    const release = () => {
      pointer = null;
      input.x = input.y = 0;
      knob.style.transform = '';
      stick.classList.remove('active');
    };
    releases.push(release);
    on(stick, 'pointerdown', (event) => {
      if (!canMove()) return;
      event.preventDefault();
      pointer = event.pointerId;
      stick.setPointerCapture(pointer);
      stick.classList.add('active');
    });
    on(stick, 'pointermove', (event) => {
      if (event.pointerId !== pointer) return;
      const bounds = stick.getBoundingClientRect(),
        radius = bounds.width * 0.34;
      const x = (event.clientX - bounds.left - bounds.width / 2) / radius,
        y = (event.clientY - bounds.top - bounds.height / 2) / radius;
      const size = Math.max(1, Math.hypot(x, y));
      input.x = x / size;
      input.y = y / size;
      if (Math.hypot(input.x, input.y) < 0.14) input.x = input.y = 0;
      knob.style.transform = `translate(${input.x * radius}px,${input.y * radius}px)`;
    });
    for (const event of ['pointerup', 'pointercancel', 'lostpointercapture'])
      on(stick, event, release);
  }
  bindStick('moveStick', move);
  bindStick('cameraStick', camera);
  on(document, 'keydown', (event) => {
    if (!canMove() || event.target.closest('button,select,input,dialog'))
      return;
    if (
      [
        'ArrowUp',
        'ArrowDown',
        'ArrowLeft',
        'ArrowRight',
        'w',
        'a',
        's',
        'd',
      ].includes(event.key)
    ) {
      event.preventDefault();
      keys.add(event.key);
    }
  });
  on(document, 'keyup', (event) => keys.delete(event.key));
  const reset = () => {
    keys.clear();
    releases.forEach((release) => release());
  };
  on(window, 'blur', reset);
  on(document, 'visibilitychange', reset);
  return {
    movement() {
      return {
        x:
          Number(keys.has('ArrowRight') || keys.has('d')) -
            Number(keys.has('ArrowLeft') || keys.has('a')) || move.x,
        y:
          Number(keys.has('ArrowDown') || keys.has('s')) -
            Number(keys.has('ArrowUp') || keys.has('w')) || move.y,
      };
    },
    camera,
    reset,
    dispose() {
      reset();
      listeners.abort();
    },
  };
}
