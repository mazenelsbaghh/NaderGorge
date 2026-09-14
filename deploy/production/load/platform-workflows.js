import http from 'k6/http';
import ws from 'k6/ws';
import { check, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';

if (__ENV.MASSAR_LOAD_AUTHORIZED !== '1') {
  throw new Error('Refusing workflow load test without MASSAR_LOAD_AUTHORIZED=1.');
}

const requiredEnvironment = [
  'MASSAR_API_ORIGIN',
  'MASSAR_WS_ORIGIN',
  'MASSAR_RELEASE_ID',
  'MASSAR_STUDENT_PHONE',
  'MASSAR_STUDENT_PASSWORD',
  'MASSAR_ADMIN_PHONE',
  'MASSAR_ADMIN_PASSWORD',
];
for (const name of requiredEnvironment) {
  if (!__ENV[name]) throw new Error(`${name} is required.`);
}
if (!__ENV.MASSAR_API_ORIGIN.startsWith('https://')) {
  throw new Error('MASSAR_API_ORIGIN must use HTTPS.');
}
if (!/^(?:git-[0-9a-f]{7,40}|src-[0-9a-f]{40})$/.test(__ENV.MASSAR_RELEASE_ID)) {
  throw new Error('MASSAR_RELEASE_ID must be an immutable release identifier.');
}

const api = __ENV.MASSAR_API_ORIGIN.replace(/\/$/, '');
const websocketOrigin = __ENV.MASSAR_WS_ORIGIN
  .replace(/^https:/, 'wss:')
  .replace(/^http:/, 'ws:')
  .replace(/\/$/, '');
const websocketHttpOrigin = websocketOrigin
  .replace(/^wss:/, 'https:')
  .replace(/^ws:/, 'http:');
const duration = __ENV.MASSAR_WORKFLOW_DURATION || '2m';
const rate = Number(__ENV.MASSAR_WORKFLOW_RATE || 1);
const searchTerm = encodeURIComponent(__ENV.MASSAR_ADMIN_SEARCH_TERM || 'طالب');

if (!Number.isFinite(rate) || rate <= 0 || rate > 25) {
  throw new Error('MASSAR_WORKFLOW_RATE must be between 1 and 25 requests per second.');
}

const workflowSuccess = new Rate('massar_workflow_success');
const workflowDuration = new Trend('massar_workflow_duration', true);
const reconnectSuccess = new Rate('massar_reconnect_success');

function scenario(exec, scenarioRate = rate) {
  return {
    executor: 'constant-arrival-rate',
    rate: scenarioRate,
    timeUnit: '1s',
    duration,
    preAllocatedVUs: Math.max(4, scenarioRate * 2),
    maxVUs: Math.max(12, scenarioRate * 6),
    exec,
  };
}

export const options = {
  scenarios: {
    login: scenario('loginJourney', Math.min(rate, 2)),
    student_dashboard: scenario('studentDashboardJourney'),
    student_packages: scenario('studentPackagesJourney'),
    admin_search: scenario('adminSearchJourney'),
    live_support: scenario('liveSupportJourney'),
    signalr_reconnect: {
      executor: 'constant-vus',
      vus: Number(__ENV.MASSAR_RECONNECT_VUS || 1),
      duration,
      exec: 'signalRReconnectJourney',
    },
  },
  thresholds: {
    checks: ['rate>0.99'],
    http_req_failed: ['rate<0.01'],
    'http_req_duration{workflow:login}': ['p(95)<1000'],
    'http_req_duration{workflow:student-dashboard}': ['p(95)<750'],
    'http_req_duration{workflow:student-packages}': ['p(95)<750'],
    'http_req_duration{workflow:admin-search}': ['p(95)<1000'],
    'http_req_duration{workflow:live-support}': ['p(95)<750'],
    massar_workflow_success: ['rate>0.99'],
    massar_reconnect_success: ['rate>0.99'],
  },
};

function requestParams(token, workflow) {
  return {
    headers: {
      Accept: 'application/json',
      Authorization: `Bearer ${token}`,
      'X-App-Release': __ENV.MASSAR_RELEASE_ID,
    },
    tags: {
      release: __ENV.MASSAR_RELEASE_ID,
      workflow,
    },
    timeout: __ENV.MASSAR_WORKFLOW_TIMEOUT || '8s',
  };
}

function login(phoneNumber, password, surface, workflow = 'login') {
  const response = http.post(
    `${api}/api/auth/login`,
    JSON.stringify({
      phoneNumber,
      password,
      deviceFingerprint: `massar-load-${surface}`,
      deviceName: 'Massar authorized workflow probe',
    }),
    {
      headers: {
        Accept: 'application/json',
        'Content-Type': 'application/json',
        'X-App-Surface': surface,
      },
      tags: { release: __ENV.MASSAR_RELEASE_ID, workflow },
      timeout: __ENV.MASSAR_WORKFLOW_TIMEOUT || '8s',
    },
  );
  let token = '';
  try {
    token = response.json('data.accessToken') || '';
  } catch {
    token = '';
  }
  const succeeded = response.status === 200 && token.split('.').length === 3;
  workflowSuccess.add(succeeded, { workflow });
  check(response, {
    [`${workflow} returns a disposable access token`]: () => succeeded,
  }, { workflow });
  return token;
}

export function setup() {
  const studentToken = login(
    __ENV.MASSAR_STUDENT_PHONE,
    __ENV.MASSAR_STUDENT_PASSWORD,
    'student',
    'setup-student-login',
  );
  const adminToken = login(
    __ENV.MASSAR_ADMIN_PHONE,
    __ENV.MASSAR_ADMIN_PASSWORD,
    'admin',
    'setup-admin-login',
  );
  if (!studentToken || !adminToken) {
    throw new Error('Disposable workflow accounts could not authenticate.');
  }
  return { adminToken, studentToken };
}

export function loginJourney() {
  const startedAt = Date.now();
  login(
    __ENV.MASSAR_STUDENT_PHONE,
    __ENV.MASSAR_STUDENT_PASSWORD,
    'student',
  );
  workflowDuration.add(Date.now() - startedAt, { workflow: 'login' });
  sleep(0.2);
}

function recordJsonRead(response, workflow) {
  const succeeded =
    response.status >= 200 &&
    response.status < 300 &&
    String(response.headers['Content-Type'] || '').includes('application/json');
  workflowSuccess.add(succeeded, { workflow });
  check(response, {
    [`${workflow} returns JSON successfully`]: () => succeeded,
    [`${workflow} preserves release identity`]: (value) =>
      !value.headers['X-Massar-Release'] ||
      value.headers['X-Massar-Release'] === __ENV.MASSAR_RELEASE_ID,
  }, { workflow });
}

export function studentDashboardJourney(data) {
  const workflow = 'student-dashboard';
  const startedAt = Date.now();
  const response = http.get(
    `${api}/api/student/dashboard`,
    requestParams(data.studentToken, workflow),
  );
  recordJsonRead(response, workflow);
  workflowDuration.add(Date.now() - startedAt, { workflow });
  sleep(0.1);
}

export function studentPackagesJourney(data) {
  const workflow = 'student-packages';
  const startedAt = Date.now();
  const response = http.get(
    `${api}/api/content/packages`,
    requestParams(data.studentToken, workflow),
  );
  recordJsonRead(response, workflow);
  workflowDuration.add(Date.now() - startedAt, { workflow });
  sleep(0.1);
}

export function adminSearchJourney(data) {
  const workflow = 'admin-search';
  const startedAt = Date.now();
  const response = http.get(
    `${api}/api/admin/users?page=1&pageSize=25&role=Student&search=${searchTerm}`,
    requestParams(data.adminToken, workflow),
  );
  recordJsonRead(response, workflow);
  workflowDuration.add(Date.now() - startedAt, { workflow });
  sleep(0.1);
}

export function liveSupportJourney(data) {
  const workflow = 'live-support';
  const startedAt = Date.now();
  const responses = http.batch([
    ['GET', `${api}/api/live-support/availability`, null, requestParams(data.studentToken, workflow)],
    ['GET', `${api}/api/live-support/participant/conversations`, null, requestParams(data.studentToken, workflow)],
  ]);
  for (const response of responses) recordJsonRead(response, workflow);
  workflowDuration.add(Date.now() - startedAt, { workflow });
  sleep(0.1);
}

function signalRSession(token, attempt) {
  const workflow = 'signalr-reconnect';
  const negotiate = http.post(
    `${websocketHttpOrigin}/hubs/platform/negotiate?negotiateVersion=1`,
    null,
    requestParams(token, workflow),
  );
  let connectionToken = '';
  try {
    connectionToken = negotiate.json('connectionToken') || '';
  } catch {
    connectionToken = '';
  }
  if (negotiate.status !== 200 || !connectionToken) return false;

  let handshakeSeen = false;
  const response = ws.connect(
    `${websocketOrigin}/hubs/platform?id=${encodeURIComponent(connectionToken)}`,
    {
      headers: { Authorization: `Bearer ${token}` },
      tags: { attempt: String(attempt), release: __ENV.MASSAR_RELEASE_ID, workflow },
    },
    (socket) => {
      socket.on('open', () => {
        socket.send('{"protocol":"json","version":1}\u001e');
      });
      socket.on('message', (message) => {
        if (String(message).startsWith('{}\u001e')) {
          handshakeSeen = true;
          socket.close();
        }
      });
      socket.setTimeout(() => socket.close(), 2_000);
    },
  );
  return response?.status === 101 && handshakeSeen;
}

export function signalRReconnectJourney(data) {
  const first = signalRSession(data.studentToken, 1);
  sleep(0.2);
  const second = signalRSession(data.studentToken, 2);
  const succeeded = first && second;
  reconnectSuccess.add(succeeded);
  workflowSuccess.add(succeeded, { workflow: 'signalr-reconnect' });
  check(null, {
    'SignalR reconnect completes two authenticated handshakes': () => succeeded,
  });
  sleep(0.5);
}

export function handleSummary(data) {
  const evidencePath = __ENV.MASSAR_WORKFLOW_EVIDENCE_PATH;
  if (!evidencePath) return {};
  const metric = (name, field, fallback) =>
    data.metrics[name]?.values?.[field] ?? fallback;
  const evidence = {
    schemaVersion: 1,
    releaseId: __ENV.MASSAR_RELEASE_ID,
    capturedAt: new Date().toISOString(),
    checkRate: metric('checks', 'rate', 0),
    errorRate: metric('http_req_failed', 'rate', 1),
    workflowSuccessRate: metric('massar_workflow_success', 'rate', 0),
    reconnectSuccessRate: metric('massar_reconnect_success', 'rate', 0),
  };
  return { [evidencePath]: `${JSON.stringify(evidence, null, 2)}\n` };
}
