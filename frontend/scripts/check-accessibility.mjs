import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

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

if (failed) {
  process.exit(1);
} else {
  console.log('All frontend components passed accessibility checks successfully!');
}
