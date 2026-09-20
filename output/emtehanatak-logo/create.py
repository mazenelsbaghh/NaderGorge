from pathlib import Path
import uharfbuzz as hb
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.transformPen import TransformPen
from fontTools.pens.boundsPen import BoundsPen
from io import BytesIO

ROOT = Path(__file__).parent
BLUE = '#0759C7'
INK = '#102E52'
OUTLINE = 'M30 8H55L80 33V74C80 81.732 73.732 88 66 88H30C22.268 88 16 81.732 16 74V22C16 14.268 22.268 8 30 8Z'
CHECK = 'M28 49C29.562 47.438 32.094 47.438 33.656 49L43 58.344L64.344 37C65.906 35.438 68.438 35.438 70 37C71.562 38.562 71.562 41.094 70 42.656L45.828 66.828C44.266 68.39 41.734 68.39 40.172 66.828L28 54.656C26.438 53.094 26.438 50.562 28 49Z'

def mark(color=BLUE, fold=True):
    shape = f'<path fill="{color}" fill-rule="evenodd" d="{OUTLINE}{CHECK}"/>'
    if fold:
        shape += '<path fill="#91BDFA" d="M55 8V23C55 28.523 59.477 33 65 33H80L55 8Z"/>'
    return shape

def svg(viewbox, content, title='امتحاناتك'):
    return f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="{viewbox}" role="img" aria-label="{title}"><title>{title}</title>{content}</svg>\n'

(ROOT/'symbol.svg').write_text(svg('0 0 96 96', mark()))
(ROOT/'symbol-mono.svg').write_text(svg('0 0 96 96', mark(BLUE, False)))
(ROOT/'symbol-white.svg').write_text(svg('0 0 96 96', mark('#FFFFFF', False)))
(ROOT/'app-icon.svg').write_text(svg('0 0 128 128', '<rect width="128" height="128" rx="28" fill="#0759C7"/><g transform="translate(16 16)">'+mark('#FFFFFF', False)+'</g>'))

# Shape Arabic first, then export the positioned glyphs as vector outlines.
font = instantiateVariableFont(TTFont(ROOT/'Alexandria.ttf'), {'wght':700}, inplace=False)
data = BytesIO()
font.save(data)
hbfont = hb.Font(hb.Face(data.getvalue()))
hbfont.scale = (font['head'].unitsPerEm, font['head'].unitsPerEm)
buf = hb.Buffer()
buf.add_str('امتحاناتك')
buf.guess_segment_properties()
hb.shape(hbfont, buf)
glyphs = font.getGlyphSet()
order = font.getGlyphOrder()
pen = SVGPathPen(glyphs)
advance = 0
for info, position in zip(buf.glyph_infos, buf.glyph_positions):
    glyphs[order[info.codepoint]].draw(TransformPen(pen, (1,0,0,-1,advance+position.x_offset,-position.y_offset)))
    advance += position.x_advance
path = pen.getCommands()
scale = 280 / advance
word = f'<g transform="translate(22 78) scale({scale})"><path fill="{INK}" d="{path}"/></g>'
(ROOT/'logo.svg').write_text(svg('0 0 430 128', word+'<g transform="translate(319 16)">'+mark()+'</g>'))
(ROOT/'logo-white.svg').write_text(svg('0 0 430 128', word.replace(INK,'#FFFFFF')+'<g transform="translate(319 16)">'+mark('#FFFFFF',False)+'</g>'))

preview='''<!doctype html><html lang="ar" dir="rtl"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>امتحاناتك • الهوية</title><style>
@font-face{font-family:Alexandria;src:url(Alexandria.ttf)}*{box-sizing:border-box}body{margin:0;background:#F0F5FC;color:#102E52;font:14px/1.8 Alexandria,sans-serif}main{max-width:1120px;margin:auto;padding:40px}header{display:flex;justify-content:space-between;align-items:center;margin-bottom:34px}header p{margin:0;color:#425B78}h1{font-size:18px;margin:0}h2{font-size:14px;font-weight:500;margin:0}.main-logo{display:grid;place-items:center;background:white;min-height:285px;padding:35px}.main-logo img{width:min(550px,100%)}.versions{display:grid;grid-template-columns:1fr 1fr 1fr;margin-top:18px;gap:18px}.version{display:flex;min-height:200px;flex-direction:column;align-items:center;justify-content:center;gap:22px;background:white}.version img{width:80px;height:80px}.version.dark{background:#0759C7;color:white}.version.ink{background:#102E52;color:white}.version small{font-size:12px}.sizes{display:flex;gap:25px;align-items:center}.sizes img{height:auto}.footer{display:flex;gap:25px;justify-content:space-between;align-items:center;margin-top:30px}.footer p{max-width:62ch}.colors{display:flex;gap:8px;direction:ltr}.colors span{padding:8px 13px;border-radius:5px;font-size:12px}.downloads{display:flex;gap:18px;margin-top:18px;flex-wrap:wrap}a{color:#0759C7;text-underline-offset:5px}@media(max-width:650px){main{padding:22px}.versions{grid-template-columns:1fr}.footer,header{align-items:start;flex-direction:column;gap:14px}.main-logo{min-height:180px;padding:20px}.version{min-height:170px}}</style><main><header><h1>امتحاناتك</h1><p>رمز بسيط لفكرة واضحة</p></header><section class="main-logo"><img src="logo.svg" alt="شعار امتحاناتك"></section><section class="versions"><div class="version"><img src="symbol.svg" alt="الرمز الأزرق"><small>ورقة امتحان + علامة صح</small></div><div class="version dark"><img src="symbol-white.svg" alt="النسخة البيضاء"><small>نسخة بلون واحد</small></div><div class="version ink"><div class="sizes"><img src="app-icon.svg" width="72" style="width:72px" alt="أيقونة التطبيق"><img src="symbol-white.svg" width="32" style="width:32px" alt="الرمز بحجم ٣٢ بكسل"><img src="symbol-white.svg" width="20" style="width:20px" alt="الرمز بحجم ٢٠ بكسل"></div><small>واضح حتى في المقاسات الصغيرة</small></div></section><div class="footer"><p>علامة الصح جزء مفرّغ من الورقة، وثنية الركن بتكمّل شكلها. الاسم والرمز متجهات بالكامل، من غير اعتماد على خطوط خارجية.</p><div class="colors"><span style="background:#0759C7;color:white">#0759C7</span><span style="background:#102E52;color:white">#102E52</span></div></div><nav class="downloads"><a href="symbol.svg" download>تحميل الرمز SVG</a><a href="logo.svg" download>تحميل اللوجو بالاسم</a><a href="app-icon.svg" download>تحميل أيقونة التطبيق</a><a href="logo-white.svg" download>تحميل النسخة البيضاء</a></nav></main></html>'''
(ROOT/'index.html').write_text(preview)
print('Created standalone SVG symbol, outlined Arabic wordmark, app icon, and preview.')
