export function browserCompatibleCssScript(buildId: string) {
  if (!/^[a-f0-9-]{36}$/.test(buildId)) throw new Error('Invalid browser compatibility build ID');
  // Parser-inserted CSS blocks first paint, so an older WebView never flashes
  // the unstyled page. Detection depends on capabilities, not the host app name.
  return `(function () {
    var css = window.CSS;
    if (window.CSSLayerBlockRule && css && css.supports &&
        css.supports('color', 'oklch(0.5 0.1 30)') &&
        css.supports('color', 'color-mix(in srgb, red, blue)')) return;
    document.write('<link rel="stylesheet" data-browser-compat href="/_next/static/browser-compat/${buildId}.css">');
  })();`;
}
