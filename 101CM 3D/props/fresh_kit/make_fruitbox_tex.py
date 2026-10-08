
from PIL import Image, ImageDraw
import random, math
KRAFT=(214,166,110); LEAF=(104,166,86); STEM=(120,84,52)
TYPES=[("apple",(228,92,92)),("grape",(150,98,172)),("orange",(244,158,66)),("lemon",(242,208,86)),("peach",(242,166,158))]
def leaf(d,x,y,s,ang):
    pts=[]
    for k in range(12):
        t=2*math.pi*k/12; r=s*(0.5+0.5*abs(math.sin(t)))  # almond-ish
        px=math.cos(t)*s*1.0; py=math.sin(t)*s*0.45
        ca,sa=math.cos(ang),math.sin(ang); pts.append((x+px*ca-py*sa, y+px*sa+py*ca))
    d.polygon(pts,fill=LEAF)
def fruit(d,kind,col,x,y,r,rnd):
    if kind=="grape":
        rr=r*0.22; rows=[4,4,3,2,1]
        for i,n in enumerate(rows):
            for j in range(n):
                cx=x+(j-(n-1)/2)*rr*1.9; cy=y-r*0.45+i*rr*1.65
                d.ellipse([cx-rr,cy-rr,cx+rr,cy+rr],fill=col)
        d.line([x,y-r*0.45-rr,x+r*0.08,y-r*0.75],fill=STEM,width=max(2,int(r*0.06)))
        leaf(d,x+r*0.32,y-r*0.7,r*0.28,-0.5); return
    if kind=="lemon":
        d.ellipse([x-r*1.15,y-r*0.85,x+r*1.15,y+r*0.85],fill=col)
        d.ellipse([x+r*1.0,y-r*0.12,x+r*1.3,y+r*0.12],fill=col)
        leaf(d,x-r*0.6,y-r*0.95,r*0.38,-0.3); return
    d.ellipse([x-r,y-r,x+r,y+r],fill=col)
    if kind=="peach":
        d.arc([x-r*0.55,y-r*0.95,x+r*0.35,y+r*0.9],start=-70,end=70,fill=tuple(max(0,c-40) for c in col),width=max(2,int(r*0.06)))
    if kind=="orange":
        d.ellipse([x-r*0.07,y-r*0.86,x+r*0.07,y-r*0.72],fill=tuple(max(0,c-50) for c in col))
    d.line([x,y-r*0.9,x+r*0.08,y-r*1.18],fill=STEM,width=max(2,int(r*0.07)))
    leaf(d,x+r*0.42,y-r*1.08,r*0.36,-0.45)
def panel(img,ox,oy,S,kind,col,seed):
    d=ImageDraw.Draw(img); rnd=random.Random(seed)
    d.rectangle([ox,oy,ox+S-1,oy+S-1],fill=KRAFT)
    sub=Image.new("RGB",(S,S),KRAFT); sd=ImageDraw.Draw(sub)
    placed=[]
    def ok(x,y,r):
        return all((x-px)**2+(y-py)**2 > (r+pr+0.04*S)**2 for px,py,pr in placed)
    for big,count,lo,hi,m in ((True,4,0.17,0.25,-0.08),(False,4,0.07,0.10,0.08)):
        n=0; tries=0
        while n<count and tries<400:
            tries+=1
            r=rnd.uniform(lo,hi)*S; x=rnd.uniform(m,1-m)*S; y=rnd.uniform(m,1-m)*S
            if ok(x,y,r*(1.25 if kind in ("grape","lemon") else 1.05)):
                placed.append((x,y,r*(1.25 if kind in ("grape","lemon") else 1.05))); fruit(sd,kind,col,x,y,r,rnd); n+=1
    img.paste(sub,(ox,oy))
def make_fruitbox_tex(path):
    S=256; img=Image.new("RGB",(1024,512),KRAFT)
    for i,(k,c) in enumerate(TYPES): panel(img,(i%4)*S,(i//4)*S,S,k,c,100+i)
    d=ImageDraw.Draw(img)
    d.rectangle([256,256,319,319],fill=(60,44,32))        # hand-hole shadow
    d.rectangle([320,256,383,319],fill=(196,150,98))      # tape seam
    img.save(path)
    regions={k:[(i%4)*S,(i//4)*S,S,S] for i,(k,c) in enumerate(TYPES)}
    regions["hole"]=[256,256,64,64]; regions["tape"]=[320,256,64,64]
    return regions
