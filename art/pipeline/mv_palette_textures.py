"""Deterministic, original material patterns based on the Abuku room palette."""
from pathlib import Path
import math, random, shutil
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'art/exports/textures'
BACKUP = ROOT / 'art/source/pre_mv_20260911/textures'
S = 2048
rng = np.random.default_rng(911)

def save(name, image, rough=.8):
    for suffix in ('BC', 'N', 'Mask'):
        p = OUT / f'TEX_{name}_{suffix}.png'
        if p.exists():
            dest = BACKUP / p.name
            dest.parent.mkdir(parents=True, exist_ok=True)
            if not dest.exists(): shutil.copy2(p, dest)
    image.save(OUT / f'TEX_{name}_BC.png')
    Image.new('RGB', (S,S), (128,128,255)).save(OUT/f'TEX_{name}_N.png')
    Image.new('RGBA',(S,S),(0,255,0,round(255*(1-rough)))).save(OUT/f'TEX_{name}_Mask.png')

def noise_image(base, strength=2):
    noise = rng.normal(0,strength,(S,S,1))
    return Image.fromarray(np.clip(np.array(base)[None,None,:]+noise,0,255).astype('uint8'))

def wallpaper():
    im = noise_image((218,207,173),1.5)
    d = ImageDraw.Draw(im)
    rnd = random.Random(122)
    # Repeating hand-drawn rose sprigs, with three flowers and olive leaves.
    for row in range(8):
        for col in range(8):
            x=col*256+100+(row%2)*70
            y=row*256+122
            for ox,oy in ((0,0),(-2048,0),(0,-2048)):
                cx,cy=x+ox,y+oy
                d.line([(cx-22,cy+45),(cx-6,cy+14),(cx+5,cy-30)], fill=(105,113,68),width=3)
                for dx,dy,a in [(-22,18,-.7),(16,9,.5),(-7,-20,-.6),(11,35,.7)]:
                    px,py=cx+dx,cy+dy
                    pts=[]
                    for t in range(24):
                        z=t*math.tau/24
                        u,v=17*math.cos(z),7*math.sin(z)
                        pts.append((px+u*math.cos(a)-v*math.sin(a),py+u*math.sin(a)+v*math.cos(a)))
                    d.polygon(pts,fill=rnd.choice([(104,118,72),(122,129,80),(137,141,93)]))
                    d.line([(px-9,py+4),(px+10,py-3)],fill=(166,157,104),width=1)
                for dx,dy,sz in [(-14,-4,17),(9,-35,14),(24,22,11)]:
                    px,py=cx+dx,cy+dy
                    for k in range(5):
                        a=k*math.tau/5
                        xx,yy=px+sz*.5*math.cos(a),py+sz*.5*math.sin(a)
                        d.ellipse((xx-sz*.62,yy-sz*.55,xx+sz*.62,yy+sz*.55),fill=(165+k*5,101+k*6,87+k*5))
                    d.arc((px-sz*.65,py-sz*.65,px+sz*.65,py+sz*.65),20,300,fill=(125,78,65),width=3)
                    d.arc((px-5,py-5,px+6,py+5),60,335,fill=(225,162,130),width=3)
    save('MAT_Wallpaper_FloralAged',im.filter(ImageFilter.GaussianBlur(.45)),.92)

def wood(name, base):
    y,x=np.mgrid[0:S,0:S]/S
    grain=np.sin(x*math.tau*72 + 1.6*np.sin(y*math.tau*2)+.6*np.sin(y*math.tau*9))
    fine=np.sin(x*math.tau*330+2*np.sin(y*math.tau*3))
    v=grain*4+fine*1.4+rng.normal(0,.8,(S,S))
    im=Image.fromarray(np.clip(np.array(base)[None,None,:]+v[:,:,None],0,255).astype('uint8'))
    save(name,im,.52)

wallpaper()
wood('MAT_Wood_Walnut',(68,43,28))
wood('MAT_Wood_Worn',(110,78,44))
save('MAT_Leather_Burgundy',noise_image((89,35,37),2),.52)
save('MAT_Floor_DarkGreen',noise_image((51,72,48),1.6),.96)
save('MAT_Fabric_Bedding',noise_image((226,220,199),1),.95)
# Woven rug with quiet border, mapped separately from the green floor.
im=noise_image((117,91,61),2)
d=ImageDraw.Draw(im)
for inset,color,w in [(40,(63,57,40),18),(76,(177,149,101),10),(103,(68,64,45),8)]:
    d.rectangle((inset,inset,S-inset,S-inset),outline=color,width=w)
for x in range(150,S-100,72):
    for y in range(150,S-100,72):
        d.polygon([(x,y-12),(x+8,y),(x,y+12),(x-8,y)],fill=(131,108,74))
save('MAT_Fabric_Rug',im,.96)
print('Generated 7 material sets.')
