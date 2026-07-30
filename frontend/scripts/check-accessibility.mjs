import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { spawnSync } from 'node:child_process';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const runBrowserMatrix =
  process.argv.includes('--browser') ||
  process.env.ACCESSIBILITY_BROWSER_MATRIX === '1';

const components = [
  {
    name: 'ParticipantConversation',
    path: '../src/components/live-support/participant/ParticipantConversation.tsx',
    rules: [
      { test: /aria-live="polite"/, message: 'Must have aria-live="polite" for dynamic message updates' },
      { test: /role="log"/, message: 'Must have role="log" for message transcripts' }
    ]
  },
  {
    name: 'AIPendingActionCard',
    path: '../src/components/live-support/participant/AIPendingActionCard.tsx',
    rules: [
      { test: /aria-label|aria-describedby|label/, message: 'Must have aria-label or aria-describedby or label for action details' },
      { test: /disabled/, message: 'Must handle disabled state for actions' }
    ]
  },
  {
    name: 'AIHandoffConfirmation',
    path: '../src/components/live-support/participant/AIHandoffConfirmation.tsx',
    rules: [
      { test: /aria-label|aria-describedby|label/, message: 'Must have descriptive labels for handoff' }
    ]
  },
  {
    name: 'AIGuestVerification',
    path: '../src/components/live-support/participant/AIGuestVerification.tsx',
    rules: [
      { test: /aria-live|role="status"|role="alert"/, message: 'Must have aria-live or status/alert role for verification updates' },
      { test: /label|htmlFor|aria-label/, message: 'Must have labels for verification input fields' }
    ]
  },
  {
    name: 'AISecureRegistrationForm',
    path: '../src/components/live-support/participant/AISecureRegistrationForm.tsx',
    rules: [
      { test: /htmlFor|aria-label|label/, message: 'Must have inputs bound to labels for secure registration fields' }
    ]
  },
  {
    name: 'StaffConversationWorkspace',
    path: '../src/components/live-support/staff/StaffConversationWorkspace.tsx',
    rules: [
      { test: /aria-label|role=|label|aria-live/, message: 'Must have descriptive labels, roles, or live regions in staff workspace' }
    ]
  },
  {
    name: 'AIHandoffSummary',
    path: '../src/components/live-support/staff/AIHandoffSummary.tsx',
    rules: [
      { test: /aria-label|aria-describedby|title|label/, message: 'Must have descriptive accessibility tags in handoff summary' }
    ]
  }
];

const browserMatrix = [
  {
    path: '../tests/e2e/platform-accessibility.spec.ts',
    rules: [/axe\.source/, /publicRoutes/, /authenticatedRoutes/],
  },
  {
    path: '../tests/e2e/accessible-overlays.spec.ts',
    rules: [/Escape/, /inert/, /toBeFocused/],
  },
  {
    path: '../tests/e2e/accessible-carousels.spec.ts',
    rules: [/reducedMotion/, /ArrowRight|ArrowLeft/, /pause|إيقاف/],
  },
  {
    path: '../tests/e2e/resilient-ui-states.spec.ts',
    rules: [/320/, /200%|zoom/, /SENSITIVE_INTERNAL_ERROR_MESSAGE/],
  },
];

let failed = false;

for (const comp of components) {
  const fullPath = path.resolve(__dirname, comp.path);
  if (!fs.existsSync(fullPath)) {
    console.error(`Component path not found: ${fullPath}`);
    failed = true;
    continue;
  }
  const content = fs.readFileSync(fullPath, 'utf8');
  for (const rule of comp.rules) {
    if (!rule.test.test(content)) {
      console.error(`Accessibility failure in ${comp.name}: ${rule.message}`);
      failed = true;
    }
  }
}

for (const entry of browserMatrix) {
  const fullPath = path.resolve(__dirname, entry.path);
  if (!fs.existsSync(fullPath)) {
    console.error(`Accessibility browser matrix is missing: ${fullPath}`);
    failed = true;
    continue;
  }
  const content = fs.readFileSync(fullPath, 'utf8');
  for (const rule of entry.rules) {
    if (!rule.test(content)) {
      console.error(
        `Accessibility browser matrix contract failed in ${path.basename(fullPath)}: ${rule}`,
      );
      failed = true;
    }
  }
}

if (!failed && runBrowserMatrix) {
  const result = spawnSync(
    path.resolve(__dirname, '../node_modules/.bin/playwright'),
    [
      'test',
      'tests/e2e/platform-accessibility.spec.ts',
      'tests/e2e/accessible-overlays.spec.ts',
      'tests/e2e/accessible-carousels.spec.ts',
      'tests/e2e/resilient-ui-states.spec.ts',
    ],
    { cwd: path.resolve(__dirname, '..'), stdio: 'inherit' },
  );
  if (result.status !== 0) failed = true;
}

if (failed) {
  process.exit(1);
} else {
  console.log(
    runBrowserMatrix
      ? 'Static accessibility contracts and browser matrix passed.'
      : 'Static accessibility contracts and browser-matrix definition passed; browser execution is a separate release gate.',
  );
}
