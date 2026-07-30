import http from 'k6/http';
import ws from 'k6/ws';
import { check, sleep } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';

if (__ENV.MASSAR_LOAD_AUTHORIZED !== '1') {
  throw new Error('Refusing load test without MASSAR_LOAD_AUTHORIZED=1.');
}
for (const required of [
  'MASSAR_PUBLIC_ORIGIN',
  'MASSAR_API_ORIGIN',
  'MASSAR_RELEASE_ID',
  'MASSAR_LOAD_RUN_ID',
  'MASSAR_LOAD_EVIDENCE_PATH',
  'MASSAR_EXPECTED_NODES',
  'MASSAR_BASELINE_RPS',
]) {
  if (!__ENV[required]) {
    throw new Error(`${required} is required; Production domains are never implicit defaults.`);
  }
}

const configuredRate = Number(__ENV.MASSAR_LOAD_RATE || 1);
const baselineRate = Number(__ENV.MASSAR_BASELINE_RPS);
const duration = __ENV.MASSAR_LOAD_DURATION || '2m';
const profile = __ENV.MASSAR_LOAD_PROFILE || 'steady';
const websocketVus = Number(__ENV.MASSAR_WS_VUS || 0);
const websocketHoldMs = Number(__ENV.MASSAR_WS_HOLD_MS || 10_000);
const workflowRps = Number(__ENV.MASSAR_WORKFLOW_RPS || 0);
const nodeBalanceTolerance = Number(__ENV.MASSAR_NODE_BALANCE_TOLERANCE || 0.20);
const expectedNodes = [...new Set(__ENV.MASSAR_EXPECTED_NODES.split(',').map((value) => value.trim()).filter(Boolean))];
const allowedNodes = new Set(['node-1', 'node-2', 'node-3']);
const allowedProfiles = new Set(['steady', 'n-minus-one', 'rolling-deploy', 'failover']);
const excludedNode = __ENV.MASSAR_EXCLUDED_NODE || null;
const evidencePath = __ENV.MASSAR_LOAD_EVIDENCE_PATH;
const capacityStages = [];
const requestedRate = configuredRate;

for (const [name, value] of Object.entries({
  MASSAR_LOAD_RATE: configuredRate,
  MASSAR_BASELINE_RPS: baselineRate,
  MASSAR_WS_VUS: websocketVus,
  MASSAR_WS_HOLD_MS: websocketHoldMs,
  MASSAR_WORKFLOW_RPS: workflowRps,
  MASSAR_NODE_BALANCE_TOLERANCE: nodeBalanceTolerance,
})) {
  const zeroAllowed = name === 'MASSAR_WS_VUS' || name === 'MASSAR_WORKFLOW_RPS';
  if (!Number.isFinite(value) || value < 0 || (!zeroAllowed && value === 0)) {
    throw new Error(`${name} must be a finite positive number.`);
  }
}
if (!allowedProfiles.has(profile)) {
  throw new Error('MASSAR_LOAD_PROFILE is invalid.');
}
if (expectedNodes.length === 0 || expectedNodes.some((node) => !allowedNodes.has(node))) {
  throw new Error('MASSAR_EXPECTED_NODES must contain node-1, node-2, or node-3.');
}
if (profile === 'n-minus-one') {
  if (!allowedNodes.has(excludedNode) || expectedNodes.length !== 2 || expectedNodes.includes(excludedNode)) {
    throw new Error('n-minus-one requires one excluded node and exactly the other two expected nodes.');
  }
}
if (__ENV.MASSAR_LOAD_STAGES_JSON) {
  throw new Error(
    'MASSAR_LOAD_STAGES_JSON is forbidden; capacity stages must be separate constant-rate runs with separate evidence.',
  );
}
if (!/^(?:git-[0-9a-f]{7,40}|src-[0-9a-f]{40})$/.test(__ENV.MASSAR_RELEASE_ID)) {
  throw new Error('MASSAR_RELEASE_ID must be an immutable git-* or src-* release identifier.');
}
if (!evidencePath.endsWith('.json') || evidencePath.includes('\n') || evidencePath.includes('\r')) {
  throw new Error('MASSAR_LOAD_EVIDENCE_PATH must be a JSON file path.');
}
if (websocketVus > 0 && (!__ENV.MASSAR_WS_ORIGIN || !__ENV.MASSAR_WS_ACCESS_TOKEN)) {
  throw new Error('MASSAR_WS_ORIGIN and MASSAR_WS_ACCESS_TOKEN are required for SignalR load.');
}
if (workflowRps > 0 && (
  !__ENV.MASSAR_PUBLIC_ASSET_URL
  || !__ENV.MASSAR_PROTECTED_ASSET_URL
  || !__ENV.MASSAR_UPLOAD_PROBE_URL
  || !__ENV.MASSAR_WORKFLOW_ACCESS_TOKEN
)) {
  throw new Error('Asset URLs, upload probe URL, and workflow token are required for workflow probes.');
}

const nodeHits = new Counter('massar_node_hits');
const unexpectedNode = new Rate('massar_unexpected_node');
const releaseMismatch = new Rate('massar_release_mismatch');
const routeDuration = new Trend('massar_route_duration', true);
const signalrUpgradeSuccess = new Rate('massar_signalr_upgrade_success');
const signalrHandshakeSuccess = new Rate('massar_signalr_handshake_success');
const signalrHoldSuccess = new Rate('massar_signalr_hold_success');
const workflowSuccess = new Rate('massar_workflow_success');

const scenarios = {
  http_capacity: {
    executor: 'constant-arrival-rate',
    rate: requestedRate,
    timeUnit: '1s',
    duration,
    preAllocatedVUs: Number(__ENV.MASSAR_LOAD_VUS || Math.max(10, requestedRate * 2)),
    maxVUs: Number(__ENV.MASSAR_LOAD_MAX_VUS || Math.max(30, requestedRate * 6)),
    exec: 'httpCapacity',
  },
};
if (websocketVus > 0) {
  scenarios.signalr_connections = {
    executor: 'constant-vus',
    vus: websocketVus,
    duration,
    exec: 'signalrConnections',
  };
}
if (workflowRps > 0) {
  scenarios.workflow_probes = {
    executor: 'constant-arrival-rate',
    rate: workflowRps,
    timeUnit: '1s',
    duration,
    preAllocatedVUs: Math.max(3, workflowRps * 2),
    maxVUs: Math.max(9, workflowRps * 6),
    exec: 'workflowProbes',
  };
}

const thresholds = {
  http_req_failed: ['rate<0.01'],
  http_req_duration: ['p(95)<1000', 'p(99)<2000'],
  'http_req_duration{surface:landing}': ['p(95)<1000', 'p(99)<2000'],
  'http_req_duration{surface:api-live}': ['p(95)<500', 'p(99)<1000'],
  checks: ['rate>0.99'],
  dropped_iterations: ['count==0'],
  massar_release_mismatch: ['rate==0'],
  massar_unexpected_node: ['rate==0'],
};
for (const node of expectedNodes) {
  thresholds[`massar_node_hits{node:${node}}`] = ['count>0'];
}
if (websocketVus > 0) {
  thresholds.massar_signalr_upgrade_success = ['rate>0.99'];
  thresholds.massar_signalr_handshake_success = ['rate>0.99'];
  thresholds.massar_signalr_hold_success = ['rate>0.99'];
}
if (workflowRps > 0) {
  thresholds.massar_workflow_success = ['rate>0.99'];
}

export const options = {
  scenarios,
  thresholds,
};

const root = __ENV.MASSAR_PUBLIC_ORIGIN;
const api = __ENV.MASSAR_API_ORIGIN;
const websocket = __ENV.MASSAR_WS_ORIGIN || '';
const hostHeaders = {
  landing: __ENV.MASSAR_PUBLIC_HOST || '',
  api: __ENV.MASSAR_API_HOST || '',
  websocket: __ENV.MASSAR_WS_HOST || '',
};
const workflowProbeNames = workflowRps > 0
  ? ['public-asset-read', 'protected-asset-read', 'invalid-upload-validation']
  : [];

function requestParams(surface, host) {
  const headers = {};
  if (host) headers.Host = host;
  return {
    headers,
    redirects: 0,
    tags: {
      surface,
      release: __ENV.MASSAR_RELEASE_ID,
      run: __ENV.MASSAR_LOAD_RUN_ID,
    },
    timeout: __ENV.MASSAR_HTTP_TIMEOUT || '5s',
  };
}

export function httpCapacity() {
  const landing = __ITER % 4 !== 0;
  const surface = landing ? 'landing' : 'api-live';
  const response = landing
    ? http.get(root, requestParams(surface, hostHeaders.landing))
    : http.get(`${api}/api/health/live`, requestParams(surface, hostHeaders.api));
  const { node, release } = recordIdentity(response, surface);
  routeDuration.add(response.timings.duration, { node, surface });
  check(response, {
    'request succeeded': (value) => value.status >= 200 && value.status < 400,
    'node evidence exists': () => node !== 'missing',
    'release matches target': () => release === __ENV.MASSAR_RELEASE_ID,
  }, { node, surface });
  sleep(0.05);
}

function recordIdentity(response, surface) {
  const node = response.headers['X-Massar-Node'] || response.headers['x-massar-node'] || 'missing';
  const release = response.headers['X-Massar-Release'] || response.headers['x-massar-release'] || 'missing';
  nodeHits.add(1, { node, surface });
  unexpectedNode.add(!expectedNodes.includes(node), { node, surface });
  releaseMismatch.add(release !== __ENV.MASSAR_RELEASE_ID, { node, surface });
  return { node, release };
}

export function signalrConnections() {
  const headers = {
    Authorization: `Bearer ${__ENV.MASSAR_WS_ACCESS_TOKEN}`,
  };
  if (hostHeaders.websocket) headers.Host = hostHeaders.websocket;
  let handshakeSeen = false;
  let openedAt = 0;
  const response = ws.connect(`${websocket}/hubs/platform`, {
    headers,
    tags: { surface: 'signalr', release: __ENV.MASSAR_RELEASE_ID },
  }, (socket) => {
    socket.on('open', () => {
      openedAt = Date.now();
      socket.send('{"protocol":"json","version":1}\u001e');
    });
    socket.on('message', (message) => {
      if (String(message).startsWith('{}\u001e')) {
        handshakeSeen = true;
      }
    });
    socket.on('close', () => {
      signalrHandshakeSuccess.add(handshakeSeen);
      signalrHoldSuccess.add(openedAt > 0 && Date.now() - openedAt >= websocketHoldMs * 0.90);
    });
    socket.setTimeout(() => socket.close(), websocketHoldMs);
  });
  signalrUpgradeSuccess.add(Boolean(response && response.status === 101));
  check(response, {
    'SignalR WebSocket upgraded': (value) => value && value.status === 101,
  });
}

export function workflowProbes() {
  const probe = __ITER % 3;
  let response;
  let expected;
  let surface;
  if (probe === 0) {
    surface = 'public-asset';
    response = http.get(__ENV.MASSAR_PUBLIC_ASSET_URL, requestParams(surface, __ENV.MASSAR_ASSET_HOST || ''));
    expected = response.status >= 200 && response.status < 400;
  } else if (probe === 1) {
    surface = 'protected-asset';
    const params = requestParams(surface, __ENV.MASSAR_ASSET_HOST || '');
    params.headers.Authorization = `Bearer ${__ENV.MASSAR_WORKFLOW_ACCESS_TOKEN}`;
    response = http.get(__ENV.MASSAR_PROTECTED_ASSET_URL, params);
    expected = response.status >= 200 && response.status < 400;
  } else {
    surface = 'upload-validation';
    const params = requestParams(surface, hostHeaders.api);
    params.headers.Authorization = `Bearer ${__ENV.MASSAR_WORKFLOW_ACCESS_TOKEN}`;
    response = http.post(__ENV.MASSAR_UPLOAD_PROBE_URL, {
      image: http.file('not-an-image', 'load-probe-invalid.png', 'image/png'),
    }, params);
    expected = response.status === 400;
  }
  const { node, release } = recordIdentity(response, surface);
  workflowSuccess.add(expected, { surface, node });
  check(response, {
    'workflow probe reached expected boundary': () => expected,
    'workflow release matches target': () => release === __ENV.MASSAR_RELEASE_ID,
  }, { surface, node });
}

function metricValue(data, name, value, fallback = 0) {
  return data.metrics[name] && data.metrics[name].values[value] !== undefined
    ? data.metrics[name].values[value]
    : fallback;
}

function thresholdFailures(data) {
  const failures = [];
  for (const [metricName, metric] of Object.entries(data.metrics)) {
    for (const [expression, result] of Object.entries(metric.thresholds || {})) {
      if (!result.ok) failures.push(`${metricName}:${expression}`);
    }
  }
  return failures.sort();
}

export function handleSummary(data) {
  const failures = thresholdFailures(data);
  const completed = new Date();
  const testRunDurationMs = Number(
    data.state && data.state.testRunDurationMs ? data.state.testRunDurationMs : 0,
  );
  const completedAt = completed.toISOString();
  // k6 evaluates module globals in the summary runtime too, so a module-level
  // timestamp is not the test start time. Bind resource samples to the real
  // execution window derived from k6's measured duration instead.
  const startedAt = new Date(
    completed.getTime() - Math.max(0, testRunDurationMs),
  ).toISOString();
  const observedNodes = expectedNodes.filter(
    (node) => metricValue(data, `massar_node_hits{node:${node}}`, 'count', 0) > 0,
  );
  const nodeRequestCounts = Object.fromEntries(
    expectedNodes.map((node) => [
      node,
      metricValue(data, `massar_node_hits{node:${node}}`, 'count', 0),
    ]),
  );
  const totalNodeRequests = Object.values(nodeRequestCounts).reduce((sum, value) => sum + value, 0);
  const nodeTrafficShares = Object.fromEntries(
    expectedNodes.map((node) => [
      node,
      totalNodeRequests > 0 ? nodeRequestCounts[node] / totalNodeRequests : 0,
    ]),
  );
  const expectedNodeRequests = expectedNodes.length > 0 ? totalNodeRequests / expectedNodes.length : 0;
  const nodeImbalanceRatio = expectedNodeRequests > 0
    ? Math.max(...Object.values(nodeRequestCounts).map(
      (count) => Math.abs(count - expectedNodeRequests) / expectedNodeRequests,
    ))
    : 1;
  if (nodeImbalanceRatio > nodeBalanceTolerance) {
    failures.push(`node-balance:${nodeImbalanceRatio.toFixed(4)}>${nodeBalanceTolerance}`);
  }
  const iterations = metricValue(data, 'iterations', 'count', 0);
  const droppedIterations = metricValue(data, 'dropped_iterations', 'count', 0);
  const attemptedIterations = iterations + droppedIterations;
  const actualDurationSeconds = Math.floor(
    testRunDurationMs / 1000,
  );
  const output = {
    schemaVersion: 1,
    status: failures.length === 0 ? 'success' : 'failed',
    runId: __ENV.MASSAR_LOAD_RUN_ID,
    releaseId: __ENV.MASSAR_RELEASE_ID,
    startedAt,
    completedAt,
    capturedAt: completedAt,
    requestedRps: requestedRate,
    achievedRps: actualDurationSeconds > 0
      ? metricValue(data, 'http_reqs', 'count', 0) / actualDurationSeconds
      : 0,
    durationSeconds: actualDurationSeconds,
    requestedDuration: duration,
    baselineRps: baselineRate,
    baselineMultiplier: requestedRate / baselineRate,
    profile,
    excludedNode,
    capacityStages,
    websocketVus,
    websocketHoldSuccessRate: metricValue(data, 'massar_signalr_hold_success', 'rate', websocketVus > 0 ? 0 : 1),
    workflowRps,
    workflowProbes: workflowProbeNames,
    workflowSuccessRate: metricValue(data, 'massar_workflow_success', 'rate', workflowRps > 0 ? 0 : 1),
    expectedNodes,
    observedNodes,
    nodeRequestCounts,
    nodeTrafficShares,
    nodeImbalanceRatio,
    unexpectedNodeRate: metricValue(data, 'massar_unexpected_node', 'rate', 1),
    healthyNodeCount: observedNodes.length,
    errorRate: metricValue(data, 'http_req_failed', 'rate', 1),
    checkRate: metricValue(data, 'checks', 'rate', 0),
    p95Milliseconds: metricValue(data, 'http_req_duration', 'p(95)', 0),
    p99Milliseconds: metricValue(data, 'http_req_duration', 'p(99)', 0),
    surfaceP95Milliseconds: {
      landing: metricValue(data, 'http_req_duration{surface:landing}', 'p(95)', 0),
      apiLive: metricValue(data, 'http_req_duration{surface:api-live}', 'p(95)', 0),
    },
    surfaceP99Milliseconds: {
      landing: metricValue(data, 'http_req_duration{surface:landing}', 'p(99)', 0),
      apiLive: metricValue(data, 'http_req_duration{surface:api-live}', 'p(99)', 0),
    },
    droppedIterations,
    droppedIterationRate: attemptedIterations > 0 ? droppedIterations / attemptedIterations : 0,
    thresholdFailures: failures,
  };
  return {
    stdout: `${JSON.stringify({
      runId: output.runId,
      releaseId: output.releaseId,
      requestedRps: output.requestedRps,
      status: output.status,
    })}\n`,
    [evidencePath]: `${JSON.stringify(output, null, 2)}\n`,
  };
}
