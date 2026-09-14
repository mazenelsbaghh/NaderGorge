export const watermarkDefaults = {
  EnableWatermark: 'true', WatermarkOpacity: '0.15',
  WatermarkShowBrand: 'true', WatermarkShowName: 'true', WatermarkShowPhone: 'true',
  WatermarkShowStudentId: 'false', WatermarkShowCustom: 'false',
  WatermarkBrandText: 'Massar Academy', WatermarkCustomText: '',
  WatermarkBrandColor: '#ffffff', WatermarkNameColor: '#ffffff',
  WatermarkPhoneColor: '#ffffff', WatermarkStudentIdColor: '#ffffff', WatermarkCustomColor: '#ffffff',
  WatermarkFontSize: '18', WatermarkFontWeight: '700', WatermarkFontFamily: 'Tajawal',
  WatermarkPosition: 'top-left', WatermarkMoving: 'true', WatermarkIntervalSeconds: '12',
  WatermarkBackgroundColor: '#000000', WatermarkBackgroundOpacity: '0', WatermarkTextShadow: 'true',
};

export type WatermarkSettings = typeof watermarkDefaults;
export type WatermarkIdentity = { name: string; phone: string; studentId: string };

export function normalizeWatermarkSettings(raw: Record<string, string> = {}): WatermarkSettings {
  const settings = { ...watermarkDefaults };
  for (const key of Object.keys(settings) as (keyof WatermarkSettings)[]) {
    const value = raw[key];
    if (value === undefined) continue;
    if (/Color$/.test(key)) { if (/^#[0-9a-f]{6}$/i.test(value)) settings[key] = value; }
    else if (key === 'EnableWatermark' || /Show|Moving|TextShadow/.test(key)) {
      if (value === 'true' || value === 'false') settings[key] = value;
    } else settings[key] = value.slice(0, 120);
  }
  for (const [key, min, max] of [
    ['WatermarkOpacity', 0.05, 1], ['WatermarkBackgroundOpacity', 0, 1],
    ['WatermarkFontSize', 10, 36], ['WatermarkIntervalSeconds', 3, 120],
  ] as const) {
    const number = Number(settings[key]);
    settings[key] = Number.isFinite(number) ? String(Math.min(max, Math.max(min, number))) : watermarkDefaults[key];
  }
  if (!['400', '700', '900'].includes(settings.WatermarkFontWeight)) settings.WatermarkFontWeight = '700';
  if (!['Tajawal', 'Montserrat', 'system-ui'].includes(settings.WatermarkFontFamily)) settings.WatermarkFontFamily = 'Tajawal';
  if (!['top-left', 'top-right', 'bottom-left', 'bottom-right', 'center'].includes(settings.WatermarkPosition)) settings.WatermarkPosition = 'top-left';
  return settings;
}

export function watermarkLines(settings: WatermarkSettings, identity: WatermarkIdentity) {
  return [
    { show: settings.WatermarkShowBrand, text: settings.WatermarkBrandText, color: settings.WatermarkBrandColor },
    { show: settings.WatermarkShowName, text: identity.name, color: settings.WatermarkNameColor },
    { show: settings.WatermarkShowPhone, text: identity.phone, color: settings.WatermarkPhoneColor },
    { show: settings.WatermarkShowStudentId, text: identity.studentId, color: settings.WatermarkStudentIdColor },
    { show: settings.WatermarkShowCustom, text: settings.WatermarkCustomText, color: settings.WatermarkCustomColor },
  ].filter(line => line.show === 'true' && line.text);
}

export function configureWatermarkHtml(html: string, raw: Record<string, string>, identity: WatermarkIdentity): string {
  const settings = normalizeWatermarkSettings(raw);
  const payload = JSON.stringify({ settings, lines: watermarkLines(settings, identity) }).replace(/</g, '\\u003c');
  const script = `<script>(function(){var config=${payload},s=config.settings;
window.configureMassarWatermark=function(w){if(!w||w.dataset.configured)return;w.dataset.configured='true';w.replaceChildren();
w.style.cssText='position:absolute;z-index:99;pointer-events:none;user-select:none;left:0;top:0;max-width:44%;width:max-content;overflow-wrap:anywhere;text-align:center;line-height:1.35;padding:4px;border-radius:4px;';
if(s.EnableWatermark!=='true'){w.style.display='none';return;}
w.style.opacity=s.WatermarkOpacity;w.style.fontSize=s.WatermarkFontSize+'px';w.style.fontWeight=s.WatermarkFontWeight;w.style.fontFamily=s.WatermarkFontFamily+',sans-serif';w.style.textShadow=s.WatermarkTextShadow==='true'?'0 1px 3px #000':'none';
var hex=s.WatermarkBackgroundColor;w.style.backgroundColor='rgba('+parseInt(hex.slice(1,3),16)+','+parseInt(hex.slice(3,5),16)+','+parseInt(hex.slice(5,7),16)+','+s.WatermarkBackgroundOpacity+')';
config.lines.forEach(function(line){var span=document.createElement('span');span.textContent=line.text;span.style.display='block';span.style.color=line.color;w.appendChild(span);});
function place(random){var xMax=Math.max(8,innerWidth-w.offsetWidth-8),yMax=Math.max(8,innerHeight-w.offsetHeight-8),p=s.WatermarkPosition;var x=p.includes('right')?xMax:8,y=p.includes('bottom')?yMax:8;if(p==='center'){x=xMax/2;y=yMax/2;}if(random){x=8+Math.random()*(xMax-8);y=8+Math.random()*(yMax-8);}w.style.transform='translate3d('+x+'px,'+y+'px,0)';}
requestAnimationFrame(function(){place(false)});addEventListener('resize',function(){place(false)});
if(s.WatermarkMoving==='true'&&!matchMedia('(prefers-reduced-motion: reduce)').matches)setInterval(function(){if(!document.hidden)place(true)},Number(s.WatermarkIntervalSeconds)*1000);
};addEventListener('DOMContentLoaded',function(){window.configureMassarWatermark(document.getElementById('wm')||document.getElementById('video-watermark'));});})();</script>`;
  return html.replace('<head>', '<head>' + script);
}
