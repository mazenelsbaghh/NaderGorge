from pathlib import Path
from io import BytesIO
import uharfbuzz as hb
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.recordingPen import DecomposingRecordingPen, replayRecording
from fontTools.pens.boundsPen import BoundsPen
from fontTools.pens.transformPen import TransformPen

ROOT = Path(__file__).parent
OUT = ROOT / 'integrated'
OUT.mkdir(exist_ok=True)
BLUE, INK = '#0759C7', '#102E52'
NAMES = ['صحّ في الكاف', 'الألف قلم', 'حروف الاختيار']
DESCS = ['علامة الصح بتحلّ مكان الجزء الداخلي من الكاف، وتبقى من تكوين الحرف نفسه.', 'أول ألف في الاسم بيتحوّل لقلم، وسنّه يوصل لنفس خطّ الحروف.', 'نقط التاء والنون بتتحوّل لدوائر اختيار، وإجابة واحدة متحددة بعلامة صح.']

def build(version, mono=False, inverse=False):
    weight = [780, 670, 600][version-1]
    font = instantiateVariableFont(TTFont(ROOT/'Alexandria.ttf'), {'wght':weight})
    data = BytesIO()
    font.save(data)
    hf = hb.Font(hb.Face(data.getvalue()))
    hf.scale = (font['head'].unitsPerEm,)*2
    buffer = hb.Buffer()
    buffer.add_str('امتحاناتك')
    buffer.guess_segment_properties()
    hb.shape(hf,buffer)
    glyphs, order = font.getGlyphSet(), font.getGlyphOrder()
    color = '#FFFFFF' if inverse else INK
    accent = color if mono or inverse else BLUE
    outlines, additions = [], []
    advance = 0
    for info,pos in zip(buffer.glyph_infos,buffer.glyph_positions):
        glyph = glyphs[order[info.codepoint]]
        rec = DecomposingRecordingPen(glyphs)
        glyph.draw(rec)
        contours, contour = [], []
        for op,args in rec.value:
            contour.append((op,args))
            if op in ('closePath','endPath'):
                bounds = BoundsPen(glyphs)
                replayRecording(contour,bounds)
                contours.append((contour,bounds.bounds))
                contour = []
        for contour,bounds in contours:
            x0,y0,x1,y1 = bounds
            # Keep the letter's connected body while replacing only its inset.
            if version == 1 and info.cluster == 8 and y0 > 300:
                additions.append(f'<path d="M350 -570L460 -455L700 -735" fill="none" stroke="{accent}" stroke-width="95" stroke-linecap="round" stroke-linejoin="round"/>')
                continue
            # The freestanding first alef becomes the writing instrument itself.
            if version == 2 and info.cluster == 0:
                left,right = advance+x0,advance+x1
                mid=(left+right)/2
                additions.append(f'<path fill="{accent}" d="M{left} -790H{right}V-215L{mid} 0L{left} -215Z"/>')
                additions.append(f'<path fill="{accent}" d="M{left} -825V-855Q{left} -900 {mid} -900Q{right} -900 {right} -855V-825Z"/>')
                # A slit and small nib hole are genuinely transparent geometry.
                nib=f'M{mid-13} -150H{mid+13}V-50L{mid} 0L{mid-13} -50Z'
                body=additions[-2]
                additions[-2]=body.replace('fill="', 'fill-rule="evenodd" fill="',1).replace('Z"/>', 'Z'+nib+'"/>')
                continue
            # The five original dots retain their places and linguistic role.
            if version == 3 and info.cluster in (2,5,7) and y0>550:
                cx=advance+(x0+x1)/2
                cy=-(y0+y1)/2
                radius=58 if info.cluster!=5 else 85
                additions.append(f'<circle cx="{cx}" cy="{cy}" r="{radius}" fill="none" stroke="{accent}" stroke-width="25"/>')
                if info.cluster==5:
                    additions.append(f'<path d="M{cx-38} {cy}l27 27 49-55" fill="none" stroke="{accent}" stroke-width="23" stroke-linecap="round" stroke-linejoin="round"/>')
                continue
            pen=SVGPathPen(glyphs)
            replayRecording(contour,TransformPen(pen,(1,0,0,-1,advance+pos.x_offset,-pos.y_offset)))
            outlines.append(pen.getCommands())
        advance+=pos.x_advance
    scale=490/advance
    content=f'<g transform="translate(45 122) scale({scale})"><path fill="{color}" d="'+''.join(outlines)+'"/>'+''.join(additions)+'</g>'
    return f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 580 160" role="img" aria-label="امتحاناتك: {NAMES[version-1]}"><title>امتحاناتك: {NAMES[version-1]}</title>{content}</svg>'

for i in range(1,4):
    for suffix,mono,inverse in [('',False,False),('-mono',True,False),('-white',False,True)]:
        (OUT/f'concept-{i}{suffix}.svg').write_text(build(i,mono,inverse))

sections=''
for i in range(1,4):
    sections+=f'''<section><div class="label"><span>0{i}</span><h2>{NAMES[i-1]}</h2><p>{DESCS[i-1]}</p><a href="concept-{i}.svg" download>تحميل SVG ↙</a></div><div class="logo"><img src="concept-{i}.svg" alt="{NAMES[i-1]}"><div class="mini"><img src="concept-{i}-mono.svg" alt="نسخة أحادية اللون"><div><img src="concept-{i}-white.svg" alt="نسخة بيضاء"></div></div></div></section>'''
html='''<!doctype html><html lang="ar" dir="rtl"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>امتحاناتك • ثلاثة شعارات مدمجة</title><style>
@font-face{font-family:Alexandria;src:url(../Alexandria.ttf)}*{box-sizing:border-box}body{margin:0;background:#F3F6FA;color:#102E52;font:14px/1.9 Alexandria,sans-serif}main{max-width:1160px;margin:auto;padding:38px}header{display:flex;align-items:center;justify-content:space-between;margin-bottom:24px}h1{font-size:22px;margin:0}header p{margin:0;color:#50657E;font-size:12px}section{display:grid;grid-template-columns:230px 1fr;background:white;margin-bottom:16px;min-height:250px}.label{padding:28px 28px 20px;border-inline-end:1px solid #E7ECF3}.label span{font-size:12px;color:#0759C7;font-weight:600}.label h2{margin:6px 0 8px;font-size:18px}.label p{margin:0 0 14px;color:#50657E;font-size:12px}.label a{color:#0759C7;font-size:12px;text-underline-offset:4px}.logo{min-width:0;padding:20px 30px 13px;display:flex;flex-direction:column;align-items:center;justify-content:center;gap:8px}.logo>img{width:100%;max-width:600px;height:160px}.mini{width:100%;display:flex;justify-content:center;align-items:center;gap:24px}.mini>img,.mini div{width:160px}.mini div{background:#102E52;border-radius:5px;display:grid;place-items:center;padding:3px}.mini div img{width:100%}footer{font-size:12px;color:#50657E} @media(max-width:700px){main{padding:20px}header{display:block}header p{margin-top:6px}section{grid-template-columns:1fr}.label{border-inline-end:0;padding:20px}.label p{max-width:50ch}.logo{padding:0 12px 18px}.logo>img{height:auto}.mini{gap:10px}.mini>img,.mini div{width:130px}}
</style><main><header><h1>امتحاناتك، الرمز جوّه الاسم.</h1><p>٣ اتجاهات أصلية قابلة للتطوير · SVG</p></header>'''+sections+'''<footer>كل نسخة مرسومة بمسارات متجهة، بخلفية شفافة، وبدون خطوط مطلوبة للتشغيل.</footer></main></html>'''
(OUT/'index.html').write_text(html)
print('Created three integrated wordmarks and monochrome variants.')
