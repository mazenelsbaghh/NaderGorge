import fs from 'node:fs';
import path from 'node:path';

const root = path.resolve(import.meta.dirname, '..');
const navigationPath = path.join(root, 'src/packages/admin/navigation.tsx');
const policyPath = path.join(root, 'src/packages/admin/route-permissions.ts');
const layoutPath = path.join(root, 'src/app/admin/layout.tsx');
const shellPath = path.join(
  root,
  'src/components/admin/AdminShellChrome.tsx'
);
const assistantShellPath = path.join(
  root,
  'src/components/assistant/AssistantShellChrome.tsx'
);
const settingsPath = path.join(
  root,
  'src/app/admin/settings/AdminSettingsPageClient.tsx'
);

const navigation = fs.readFileSync(navigationPath, 'utf8');
const policy = fs.readFileSync(policyPath, 'utf8');
const layout = fs.readFileSync(layoutPath, 'utf8');
const shell = fs.readFileSync(shellPath, 'utf8');
const assistantShell = fs.readFileSync(assistantShellPath, 'utf8');
const settings = fs.readFileSync(settingsPath, 'utf8');

const navigationRoutes = [
  ...navigation.matchAll(/href:\s*['"]([^'"]+)['"]/g),
].map((match) => match[1]);
const uniqueRoutes = [...new Set(navigationRoutes)];
const missing = uniqueRoutes.filter(
  (route) =>
    !policy.includes('adminNavigationRoutePermissions') &&
    !policy.includes(`pattern: '${route}'`)
);

if (missing.length > 0) {
  throw new Error(
    `Admin navigation routes are missing from the route policy: ${missing.join(', ')}`
  );
}
if (layout.includes('ROUTE_PERMISSIONS')) {
  throw new Error('Admin layout must not define a second route-permission matrix.');
}
if (!layout.includes('canAccessAdminRoute')) {
  throw new Error('Admin layout must consume the canonical route policy.');
}
if (!shell.includes('canAccessAdminRoute(item.href, user)')) {
  throw new Error('Admin menu visibility must consume the canonical route policy.');
}

const assistantNavigationRoutes = [
  ...assistantShell.matchAll(/href:\s*['"](\/assistant\/[^'"]+)['"]/g),
].map((match) => match[1]);
const uniqueAssistantRoutes = [...new Set(assistantNavigationRoutes)];
const missingAssistantSettings = uniqueAssistantRoutes.filter(
  (route) => route !== '/assistant/dashboard' && !settings.includes(`key: '${route}'`)
);

if (missingAssistantSettings.length > 0) {
  throw new Error(
    `Assistant navigation routes are missing from role settings: ${missingAssistantSettings.join(', ')}`
  );
}

if (!settings.match(/'users\.manage':\s*\[[\s\S]*?'\/assistant\/students'[\s\S]*?\]/)) {
  throw new Error(
    'The users.manage permission must automatically grant the assistant student-management route.'
  );
}

console.log(
  `route permission contracts passed (${uniqueRoutes.length} admin routes, ${uniqueAssistantRoutes.length} assistant routes)`
);
