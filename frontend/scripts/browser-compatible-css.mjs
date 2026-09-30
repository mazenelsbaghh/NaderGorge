import { readFile, readdir, mkdir, writeFile } from 'node:fs/promises';
import path from 'node:path';
import vm from 'node:vm';
import postcss from 'postcss';
import presetEnv from 'postcss-preset-env';
import valueParser from 'postcss-value-parser';

function rebasedStylesheet({ href, css }) {
  const sheet = postcss.parse(css, { from: href });
  sheet.walkDecls(declaration => {
    const parsed = valueParser(declaration.value);
    parsed.walk(token => {
      if (token.type !== 'function' || token.value !== 'url') return;
      const resource = token.nodes[0];
      if (!resource || /^(?:[a-z]+:|\/|#)/i.test(resource.value)) return;
      const absolute = new URL(resource.value, `https://build.invalid${href}`);
      resource.value = absolute.pathname + absolute.search + absolute.hash;
    });
    declaration.value = parsed.toString();
  });
  return sheet;
}

// Process the complete bundle: translating layers per chunk changes precedence.
export async function compatibleStylesheet(stylesheets) {
  const bundle = postcss.root();
  for (const stylesheet of stylesheets) bundle.append(rebasedStylesheet(stylesheet).nodes);
  const compiled = await postcss([presetEnv({
    browsers: ['Chrome 90', 'Safari 14.1', 'Firefox 90'],
    stage: 2,
    features: {
      'cascade-layers': true,
      'nesting-rules': true,
      'custom-properties': false,
      'logical-properties-and-values': false,
    },
  })]).process(bundle, { from: undefined, map: false });
  const layerWarnings = compiled.warnings().filter(warning => warning.plugin === 'postcss-cascade-layers');
  if (layerWarnings.length) throw new Error(layerWarnings.join('\n'));
  return compiled.css;
}

export function stylesheetOrder(sequences) {
  const dependencies = new Map();
  for (const sequence of sequences) {
    sequence.forEach((file, index) => {
      if (!dependencies.has(file)) dependencies.set(file, new Set());
      if (index) dependencies.get(file).add(sequence[index - 1]);
    });
  }
  const ordered = [];
  while (dependencies.size) {
    const ready = [...dependencies].filter(([, predecessors]) => predecessors.size === 0);
    if (!ready.length) throw new Error('Conflicting stylesheet order in Next.js manifests');
    for (const [file] of ready) {
      ordered.push(file);
      dependencies.delete(file);
      for (const predecessors of dependencies.values()) predecessors.delete(file);
    }
  }
  return ordered;
}

async function stylesheetSequences(buildDirectory) {
  const appDirectory = path.join(buildDirectory, 'server/app');
  const files = (await readdir(appDirectory, { recursive: true })).sort();
  const sequences = [];
  for (const file of files.filter(file => file.endsWith('_client-reference-manifest.js'))) {
    const context = vm.createContext({});
    vm.runInContext(await readFile(path.join(appDirectory, file), 'utf8'), context, { timeout: 1000 });
    for (const manifest of Object.values(context.__RSC_MANIFEST)) {
      sequences.push(...Object.values(manifest.entryCSSFiles).map(files => files.map(file => file.path)));
    }
  }
  return sequences;
}

export async function writeCompatibleStylesheet(buildDirectory, buildId) {
  const files = stylesheetOrder(await stylesheetSequences(buildDirectory));
  if (!files.length) throw new Error('No compiled stylesheets found for browser compatibility');
  const stylesheets = await Promise.all(files.map(async file => ({
    href: `/_next/${file}`,
    css: await readFile(path.join(buildDirectory, file), 'utf8'),
  })));
  const css = await compatibleStylesheet(stylesheets);
  const outputDirectory = path.join(buildDirectory, 'static/browser-compat');
  await mkdir(outputDirectory, { recursive: true });
  await writeFile(path.join(outputDirectory, `${buildId}.css`), css);
  console.log(`Browser compatibility: ${files.length} stylesheets, ${Buffer.byteLength(css)} bytes`);
}
