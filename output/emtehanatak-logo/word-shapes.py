from pathlib import Path
from io import BytesIO
import uharfbuzz as hb
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.basePen import BasePen

ROOT=Path(__file__).parent
OUT=ROOT/'word-shapes'
OUT.mkdir(exist_ok=True)

class MappedPen(BasePen):
    def __init__(self, glyphs, output, mapping):
        super().__init__(glyphs)
        self.output=output
        self.mapping=mapping
    def _moveTo(self,p): self.output.moveTo(self.mapping(*p))
    def _lineTo(self,p): self.output.lineTo(self.mapping(*p))
    def _curveToOne(self,p1,p2,p3): self.output.curveTo(*[self.mapping(*p) for p in (p1,p2,p3)])
    def _qCurveToOne(self,p1,p2): self.output.qCurveTo(self.mapping(*p1),self.mapping(*p2))
    def _closePath(self): self.output.closePath()
    def _endPath(self): self.output.endPath()

def lettering(weight=800, mode='plain'):
    font=instantiateVariableFont(TTFont(ROOT/'Alexandria.ttf'),{'wght':weight})
    binary=BytesIO();font.save(binary)
    hf=hb.Font(hb.Face(binary.getvalue()));hf.scale=(font['head'].unitsPerEm,)*2
    buf=hb.Buffer();buf.add_str('امتحاناتك');buf.guess_segment_properties();hb.shape(hf,buf)
    glyphs=font.getGlyphSet();order=font.getGlyphOrder()
    width=sum(p.x_advance for p in buf.glyph_positions)
    x=0;pen=SVGPathPen(glyphs)
    for info,p in zip(buf.glyph_infos,buf.glyph_positions):
        shift=0
        if mode=='steps': shift=-420 if info.cluster>=7 else -210 if info.cluster>=5 else 0
        def mapping(a,b):
            gx=x+a+p.x_offset
            gy=-b-p.y_offset
            if mode=='book':
                # The baseline of the actual lettering forms the open spread.
                gy-=abs(gx-width*.51)*.135
            if mode=='steps': gy+=shift
            return gx,gy
        glyphs[order[info.codepoint]].draw(MappedPen(glyphs,pen,mapping))
        x+=p.x_advance
    return pen.getCommands(),width

def svg(body,viewbox='0 0 640 200'):
    return '<svg xmlns="http://www.w3.org/2000/svg" viewBox="'+viewbox+'" role="img" aria-label="امتحاناتك"><title>امتحاناتك</title>'+body+'</svg>'

for color,suffix in [('#0759C7',''),('#142B45','-mono'),('#FFFFFF','-white')]:
    path,width=lettering(760)
    # The word is a transparent cut in the entire pencil silhouette.
    cut=f'<path transform="translate(112 127) scale({432/width})" d="{path}" fill="black"/>'
    mask='<defs><mask id="name-cut" maskUnits="userSpaceOnUse" x="0" y="0" width="640" height="200"><rect width="640" height="200" fill="white"/>'+cut+'<path d="M18 93H78M571 40V145" stroke="black" stroke-width="7"/></mask></defs>'
    pencil=f'<path fill="{color}" mask="url(#name-cut)" d="M102 42H587Q608 42 608 63V124Q608 145 587 145H102L20 93.5Z"/>'
    (OUT/f'01-pencil{suffix}.svg').write_text(svg(mask+pencil))
    path,width=lettering(820,'book');s=535/width
    # Both page edges grow out of the low baseline rather than a surrounding box.
    book=f'<g transform="translate(52 157) scale({s})"><path fill="{color}" d="{path}"/><path fill="none" stroke="{color}" stroke-width="53" stroke-linecap="square" d="M65 -135L{width*.51} 187L{width-40} -145M110 -4L{width*.51} 317L{width-85} -14"/></g>'
    (OUT/f'02-book{suffix}.svg').write_text(svg(book,'0 0 640 235'))
    path,width=lettering(870,'steps');s=525/width
    stairs=f'<g transform="translate(57 185) scale({s})"><path fill="{color}" d="{path}"/></g>'
    # A progression through the word itself; no separate staircase icon.
    (OUT/f'03-progress{suffix}.svg').write_text(svg(stairs,'0 0 640 240'))

names=['الكلمة قلم','الكلمة كتاب','الكلمة بتتقدّم']
descs=['الاسم مفرّغ في جسم قلم كامل، والفراغات هي اللي بتكتب الكلمة.','الحروف منحنية على فتحتَي كتاب، وخطّها السفلي بيكمل الصفحات.','مقاطع الاسم بتطلع من اليمين للشمال، فتقرأ الاسم وتشوف التقدّم في نفس اللحظة.']
files=['01-pencil','02-book','03-progress']
rows=''
for i,(name,desc,file) in enumerate(zip(names,descs,files),1):
    rows+=f'<section><aside><b>0{i}</b><h2>{name}</h2><p>{desc}</p><a href="{file}.svg" download>تحميل SVG</a></aside><div class="art"><img class="large" src="{file}.svg" alt="{name}"><div class="sizes"><img src="{file}-mono.svg" alt="لون واحد"><span><img src="{file}-white.svg" alt="على خلفية داكنة"></span></div></div></section>'
(OUT/'index.html').write_text('''<!doctype html><html lang="ar" dir="rtl"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>امتحاناتك • الكلمة هي الرمز</title><style>@font-face{font-family:Alexandria;src:url(../Alexandria.ttf)}*{box-sizing:border-box}body{margin:0;background:#F1F4F8;color:#142B45;font:14px/1.9 Alexandria,sans-serif}main{max-width:1150px;margin:auto;padding:34px}header{display:flex;justify-content:space-between;align-items:center;margin-bottom:24px}h1{font-size:23px;margin:0}header p{font-size:12px;color:#53647B}section{display:grid;grid-template-columns:220px 1fr;margin-bottom:18px;background:white;min-height:250px}aside{padding:27px;border-inline-end:1px solid #E8EDF3}aside b{color:#0759C7;font-size:12px}h2{font-size:18px;margin:7px 0}aside p{font-size:12px;color:#53647B;margin:0 0 15px}a{color:#0759C7;font-size:12px;text-underline-offset:5px}.art{padding:16px 20px 19px;display:grid;justify-items:center;align-content:center}.large{width:100%;max-width:640px;height:185px}.sizes{display:flex;align-items:center;justify-content:center;gap:24px;width:100%}.sizes>img,.sizes>span{width:155px}.sizes span{background:#142B45;border-radius:4px;display:grid;place-items:center}.sizes span img{width:100%}footer{font-size:12px;color:#53647B}@media(max-width:680px){main{padding:18px}header{display:block}section{grid-template-columns:1fr}aside{border:0;padding:20px}aside p{max-width:48ch}.art{padding:0 8px 18px}.large{height:auto}.sizes{gap:12px}.sizes>img,.sizes>span{width:130px}}</style><main><header><h1>الكلمة نفسها هي الرمز.</h1><p>امتحاناتك · ٣ تكوينات جديدة</p></header>'''+rows+'<footer>SVG متجهي بالكامل · فراغات شفافة · نسخ بالأزرق، بلون واحد، وبالأبيض.</footer></main></html>')
print('Created three word-shape directions.')
