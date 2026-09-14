import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import vm from 'node:vm';

import ts from 'typescript';

type WebVitalCallback = (metric: {
  id: string;
  name: string;
  value: number;
  rating: string;
  navigationType: string;
}) => void;

type PostedRequest = {
  path: string;
  payload: Record<string, unknown>;
};

async function loadReporterInBrowserSandbox() {
  const sourceUrl = new URL('./useWebVitalsReporter.ts', import.meta.url);
  const source = await readFile(sourceUrl, 'utf8');
  const compiled = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022,
      esModuleInterop: true,
    },
    fileName: sourceUrl.pathname,
  }).outputText;
  const posted: PostedRequest[] = [];
  let webVitalCallback: WebVitalCallback | undefined;
  const storage = new Map<string, string>([['web_vitals_sampled', '1']]);
  const browserWindow = {
    innerWidth: 390,
    location: {
      hostname: 'app.massar-academy.net',
      pathname: '/student/packages/6f1d4c90-cc0f-46c7-bc85-fba85a938ee7',
      search: '?access_token=PRIVACY_SENTINEL',
      href:
        'https://app.massar-academy.net/student/packages/' +
        '6f1d4c90-cc0f-46c7-bc85-fba85a938ee7?access_token=PRIVACY_SENTINEL',
    },
    addEventListener() {},
    removeEventListener() {},
  };
  const storageApi = {
    getItem(key: string) {
      return storage.get(key) ?? null;
    },
    setItem(key: string, value: string) {
      storage.set(key, value);
    },
  };
  const sandboxProcess = {
    env: {
      NODE_ENV: 'production',
      NEXT_PUBLIC_RELEASE_ID: 'src-0123456789012345678901234567890123456789',
    },
  };
  const moduleRecord = { exports: {} as Record<string, unknown> };
  const context = vm.createContext({
    AbortController,
    console,
    localStorage: storageApi,
    module: moduleRecord,
    exports: moduleRecord.exports,
    navigator: {
      connection: { effectiveType: '3g' },
      onLine: true,
      userAgent: 'PRIVACY_SENTINEL_USER_AGENT',
    },
    process: sandboxProcess,
    require(specifier: string) {
      if (specifier === 'react') {
        return {
          useCallback: <T,>(callback: T) => callback,
          useEffect: (effect: () => void | (() => void)) => effect(),
        };
      }
      if (specifier === 'next/web-vitals') {
        return {
          useReportWebVitals(callback: WebVitalCallback) {
            webVitalCallback = callback;
          },
        };
      }
      if (specifier === '@/services/api-client') {
        return {
          __esModule: true,
          default: {
            post(path: string, payload: Record<string, unknown>) {
              posted.push({ path, payload });
              return Promise.resolve({ status: 202 });
            },
          },
        };
      }
      if (specifier === '@/stores/auth-store') {
        return {
          useAuthStore(
            selector: (state: { isAuthenticated: boolean }) => unknown,
          ) {
            return selector({ isAuthenticated: true });
          },
        };
      }
      throw new Error(`Unexpected reporter dependency: ${specifier}`);
    },
    sessionStorage: storageApi,
    window: browserWindow,
  });
  const wrapper = new vm.Script(
    `(function (exports, require, module) { ${compiled}\n})`,
    { filename: sourceUrl.pathname },
  ).runInContext(context) as (
    exports: Record<string, unknown>,
    require: (specifier: string) => unknown,
    module: typeof moduleRecord,
  ) => void;
  wrapper(
    moduleRecord.exports,
    context.require as (specifier: string) => unknown,
    moduleRecord,
  );
  const useReporter = moduleRecord.exports.useWebVitalsReporter;
  assert.equal(typeof useReporter, 'function');
  (useReporter as () => void)();
  assert.ok(webVitalCallback, 'reporter must register its Web Vitals callback');

  return { posted, report: webVitalCallback };
}

test('browser metric sends normalized low-cardinality dimensions without private browser data', async () => {
  const { posted, report } = await loadReporterInBrowserSandbox();

  report({
    id: 'v4-opaque-metric-id',
    name: 'LCP',
    value: 1834.2,
    rating: 'good',
    navigationType: 'navigate',
  });

  assert.equal(posted.length, 1);
  assert.equal(posted[0]?.path, '/v1/metrics/web-vitals');
  const payload = JSON.parse(
    JSON.stringify(posted[0]?.payload),
  ) as Record<string, unknown>;
  assert.deepEqual(payload, {
    metricId: 'v4-opaque-metric-id',
    metricName: 'LCP',
    value: 1834.2,
    rating: 'good',
    routeTemplate: '/student/packages/[packageId]',
    surface: 'student',
    deviceClass: 'mobile',
    connectionClass: 'moderate',
    navigationType: 'navigate',
    releaseId: 'src-0123456789012345678901234567890123456789',
  });
  const serialized = JSON.stringify(payload);
  assert.doesNotMatch(serialized, /PRIVACY_SENTINEL|userAgent|pageUrl|access_token/i);
});
