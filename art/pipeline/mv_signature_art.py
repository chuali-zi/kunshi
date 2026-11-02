#!/usr/bin/env python3
"""Rebuild ConGame's own vector-drawn MV overlays (Pillow; no MV pixels reused).
Title uses the installed Noto Serif CJK Bold font (SIL OFL), rasterised only.
"""
from pathlib import Path
import math
import random
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Game/Art/MvSignature'
OUT.mkdir(parents=True, exist_ok=True)
S = 2
W, H = 1920, 1080

class Drawing:
    def __init__(self):
        self.im = Image.new('RGBA', (W*S, H*S))
        self.d = ImageDraw.Draw(self.im)
        self.svg = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">']
    def line(self, pts, color='#15161a', width=3):
        self.d.line([(int(x*S), int(y*S)) for x,y in pts], fill=color, width=max(1,int(width*S)), joint='curve')
        self.svg.append('<polyline points="'+' '.join(f'{x:.1f},{y:.1f}' for x,y in pts)+f'" fill="none" stroke="{color}" stroke-width="{width}" stroke-linejoin="round"/>')
    def poly(self, pts, fill='#faf9f2', stroke='#15161a', width=4):
        self.d.polygon([(int(x*S),int(y*S)) for x,y in pts],fill=fill)
        self.svg.append('<polygon points="'+' '.join(f'{x},{y}' for x,y in pts)+f'" fill="{fill}"/>')
        if stroke: self.line(pts+[pts[0]],stroke,width)
    def ellipse(self, box, fill=None, stroke='#15161a', width=4):
        self.d.ellipse(tuple(int(v*S) for v in box),fill=fill,outline=stroke,width=int(width*S))
        x,y,xx,yy=box
        self.svg.append(f'<ellipse cx="{(x+xx)/2}" cy="{(y+yy)/2}" rx="{(xx-x)/2}" ry="{(yy-y)/2}" fill="{fill or "none"}" stroke="{stroke}" stroke-width="{width}"/>')
    def curve(self, p, color='#15161a', width=4):
        # Cubic Bezier, code-native line art.
        pts=[]
        for i in range(65):
            t=i/64;u=1-t
            pts.append((u**3*p[0][0]+3*u*u*t*p[1][0]+3*u*t*t*p[2][0]+t**3*p[3][0],u**3*p[0][1]+3*u*u*t*p[1][1]+3*u*t*t*p[2][1]+t**3*p[3][1]))
        self.line(pts,color,width)
    def save(self, name):
        self.im.resize((W,H),Image.Resampling.LANCZOS).save(OUT/(name+'.png'))
        (OUT/(name+'.svg')).write_text('\n'.join(self.svg+['</svg>'])+'\n')

rng=random.Random(1101)
a=Drawing()
a.poly([(0,235),(1920,355),(1920,835),(0,715)],'#faf9f2',width=12)
# Converging manga speed lines within the slanted panel.
for i in range(82):
    x=rng.uniform(-200,1960); y=rng.choice([0,1080])
    dx=x-1240;dy=y-550
    if abs(dy)<1: continue
    t=rng.uniform(.24,.48)
    start=(1240+dx*t,550+dy*t)
    end=(1240+dx*.78,550+dy*.78)
    # Clamp strokes to the panel's parallel diagonal borders.
    def clip(p):
        xx,yy=p;xx=max(0,min(W,xx));return xx,max(254+xx*.0625,min(696+xx*.0625,yy))
    a.line([clip(start),clip(end)],width=rng.choice([1,2,3]))
# Tan-suited protagonist, own original ink portrait. Close-up, round glasses and sharp eyes.
a.poly([(800,750),(920,627),(1040,583),(1390,593),(1525,674),(1610,803)],'#25252a',width=5)
a.poly([(1010,606),(1142,690),(1220,637),(1329,705),(1430,625),(1334,783),(1138,776)],'#eeeeea',width=4)
a.poly([(1198,669),(1245,672),(1268,710),(1237,794),(1201,791),(1215,714)],'#24252b',width=5)
a.poly([(1062,576),(1074,637),(1208,697),(1335,633),(1339,569)],'#f7f6ef',width=4)
a.poly([(1004,357),(1070,310),(1262,311),(1390,360),(1380,530),(1325,603),(1211,642),(1102,602),(1033,522)],'#faf9f2',width=6)
a.poly([(990,428),(983,361),(1018,291),(1152,276),(1325,309),(1395,353),(1409,435),(1340,397),(1291,348),(1260,406),(1190,357),(1109,395),(1083,357),(1024,449)],'#17181c',width=7)
a.ellipse((995,454,1049,535),'#f5f4ee',width=4)
a.ellipse((1366,455,1412,537),'#f5f4ee',width=4)
a.curve([(1039,455),(1080,412),(1139,413),(1171,444)],width=10)
a.curve([(1244,444),(1280,415),(1331,420),(1375,460)],width=10)
a.ellipse((1040,438,1190,550),width=8)
a.ellipse((1225,442,1374,554),width=8)
a.curve([(1187,476),(1200,468),(1215,469),(1227,479)],width=7)
a.line([(1008,456),(1042,470)],width=7);a.line([(1374,477),(1400,462)],width=7)
a.curve([(1070,490),(1097,461),(1148,466),(1167,491)],width=5)
a.curve([(1249,493),(1280,468),(1323,468),(1346,497)],width=5)
a.ellipse((1116,477,1133,510),'#17181c',width=2)
a.ellipse((1273,479,1290,512),'#17181c',width=2)
a.line([(1207,488),(1191,548),(1214,557),(1229,551)],width=4)
a.curve([(1168,590),(1194,575),(1236,578),(1261,589)],width=5)
a.line([(1189,604),(1239,606)],width=2)
# Hatching instead of colour shading.
for i in range(16):
    x=1054+i*6;a.line([(x,552),(x+15,576)],width=1)
for i in range(19):
    x=1270+i*5;a.line([(x,564),(x-12,590)],width=1)
a.line([(973,663),(1090,696),(1053,723),(1130,775)],width=6)
a.line([(1441,679),(1347,712),(1395,743),(1339,784)],width=6)
# Thin lens glints retain the recognisable circular glasses.
a.line([(1078,458),(1100,469)],'#ffffff',6);a.line([(1254,462),(1278,475)],'#ffffff',6)
a.save('Realisation')

# Transparent, stable radial fracture. Black seams under white chipped edges.
g=Drawing();cx,cy=440,725
spokes=[]
for i in range(17):
    angle=i*math.tau/17+rng.uniform(-.08,.08)
    pts=[(cx,cy)]
    for dist in [26,64,140,260,490,800,1300,2100]:
        theta=angle+rng.uniform(-.07,.07)
        pts.append((cx+math.cos(theta)*dist,cy+math.sin(theta)*dist))
    spokes.append(pts)
for pts in spokes:
    g.line(pts,'#10141e',5)
    g.line([(x-1.7,y-1.3) for x,y in pts],'#e9f0f5',2)
    for j in [3,5]:
        x,y=pts[j];xx,yy=pts[j+1]
        branch=[(x,y),(x+(xx-x)*.5+(yy-y)*.18,y+(yy-y)*.5-(xx-x)*.18)]
        g.line(branch,'#cad7e0',1)
for radius in [35,80,165,310,575]:
    for i in range(17):
        if rng.random()<.30:continue
        a1=i*math.tau/17;a2=(i+1)*math.tau/17
        pts=[(cx+math.cos(a1)*radius,cy+math.sin(a1)*radius),(cx+math.cos((a1+a2)/2)*radius*rng.uniform(.76,.93),cy+math.sin((a1+a2)/2)*radius*.89),(cx+math.cos(a2)*radius,cy+math.sin(a2)*radius)]
        g.line(pts,'#131822',3);g.line([(x-1,y-1) for x,y in pts],'#edf3f7',1)
g.save('ScreenGlass')

font=ImageFont.truetype('/usr/share/fonts/opentype/noto/NotoSerifCJK-Bold.ttc',310)
title=Image.new('RGBA',(880,420));d=ImageDraw.Draw(title)
d.text((100,-3),'困室',font=font,fill=(246,245,235,255),stroke_width=1)
# Small dry-ink gaps; retain legibility of the two original game-title glyphs.
mask=title.getchannel('A');md=ImageDraw.Draw(mask)
for i in range(210):
    x=rng.randrange(110,750);y=rng.randrange(65,357)
    md.line((x,y,x+rng.randrange(2,9),y-1),fill=0,width=1)
title.putalpha(mask)
title=title.transform(title.size,Image.Transform.AFFINE,(1,.10,-20,0,1,0),Image.Resampling.BICUBIC)
title.save(OUT/'TitleInk.png')
print('Wrote original manga panel, fracture art and raster title to',OUT)
