import ws from 'k6/ws';
import { check, sleep } from 'k6';

const results = [];
const url = 'ws://127.0.0.1:8088/hubs/platform';
const headers = {
  Authorization: `Bearer ${__ENV.MASSAR_WS_ACCESS_TOKEN}`,
  Host: 'ws.massar-academy.net',
};
if (__ENV.MASSAR_SIGNALR_PINNED_NODE) {
  headers.Cookie = `MASSAR_SIGNALR_NODE=${__ENV.MASSAR_SIGNALR_PINNED_NODE}`;
}

function connect(label, holdMs) {
  let handshake = false;
  const response = ws.connect(url, { headers }, (socket) => {
    socket.on('open', () => socket.send('{"protocol":"json","version":1}\u001e'));
    socket.on('message', (message) => {
      if (String(message).startsWith('{}\u001e')) handshake = true;
    });
    socket.setTimeout(() => socket.close(), holdMs);
  });
  results.push({
    label,
    status: response && response.status,
    node: response && (response.headers['X-Massar-Node'] || response.headers['x-massar-node'] || null),
    handshake,
  });
  console.log(`MASSAR_SIGNALR_PROBE ${JSON.stringify(results[results.length - 1])}`);
  check(response, { [`${label} upgraded`]: (value) => value && value.status === 101 });
}

export default function () {
  connect('before-node-loss', 20000);
  sleep(8);
  connect('after-node-loss', 5000);
}
