OUT=r'C:\Users\mbc\Documents\YW_Project\101CM\101CM 3D\props\fresh_kit'

from PIL import Image, ImageDraw, ImageFont
import random, math, json
random.seed(101)
W=1024
img=Image.new("RGB",(W,W),"#F5E8CD")
d=ImageDraw.Draw(img)
KR=r"C:\Windows\Fonts\malgunbd.ttf"
KRR=r"C:\Windows\Fonts\malgun.ttf"
def F(sz,bold=True): return ImageFont.truetype(KR if bold else KRR, sz)
regions={}
def reg(name,x,y,w,h): regions[name]=[x,y,w,h]
sw=["#D9B07A","#B9864F","#7A4E2D","#C9CDD3","#6E7680","#472F26","#F5E8CD","#FEF0B6",
    "#BBD3E0","#7FB069","#3F7D4E","#D9534F","#4A90C8","#F4F4F0","#2A2A2E","#C8A577"]
names=["woodL","woodM","woodD","metalL","metalD","brown","cream","yellow","azure","felt","greenD","red","blue","white","black","kraft"]
for i,c in enumerate(sw):
    x=(i%8)*64; y=(i//8)*64
    d.rectangle([x,y,x+63,y+63],fill=c); reg("sw_"+names[i],x,y,64,64)
fr=["#D93A3A","#F39A2B","#F5D63D","#93C24A","#F7A98B","#6B3E8E","#F2D35B","#E84A3A",
    "#4E9A3E","#B8D88A","#EE7F2D","#C9A26B","#D9B48F","#5B3A6E","#F2EFE6","#4C8C3A"]
fn=["apple","orange","lemon","gapple","peach","grape","banana","tomato","leaf","cabbage","carrot","potato","onion","eggplant","radish","broccoli"]
for i,c in enumerate(fr):
    x=512+(i%8)*64; y=128+(i//8)*64
    d.rectangle([x,y,x+63,y+63],fill=c)
    dk=tuple(int(int(c[q:q+2],16)*0.62) for q in (1,3,5))
    lt=tuple(min(255,int(int(c[q:q+2],16)*1.0+45)) for q in (1,3,5))
    tile=Image.new("RGB",(64,64),dk); td=ImageDraw.Draw(tile)
    for (ox,oy) in [(0,0),(32,0),(16,26),(48,26),(-16,26),(0,52),(32,52),(64,0),(64,52),(-32,0),(-32,52)]:
        cx=16+ox; cy=12+oy
        td.ellipse([cx-15,cy-14,cx+15,cy+14],fill=c)
        td.ellipse([cx-9,cy-9,cx-3,cy-4],fill=lt)
    img.paste(tile,(x,y))
    reg("fr_"+fn[i],x,y,64,64); reg("fill_"+fn[i],x,y,64,64)
for p in range(4):
    base=[(217,176,122),(205,160,105),(222,183,130),(210,168,112)][p]
    y0=p*32
    d.rectangle([512,y0,1023,y0+31],fill=base)
    for k in range(10):
        yy=y0+random.randint(3,28); amp=random.uniform(0.5,2.0); ph=random.uniform(0,6)
        col=tuple(int(v*random.uniform(0.82,0.92)) for v in base)
        pts=[(512+x, yy+amp*math.sin(x/random.uniform(30,70)+ph)) for x in range(0,512,4)]
        d.line(pts,fill=col,width=1)
    d.line([(512,y0),(1023,y0)],fill=(150,105,62),width=2)
    d.line([(512,y0+31),(1023,y0+31)],fill=(170,122,75),width=1)
reg("wood",512,0,512,128)
d.rectangle([0,128,255,255],fill="#7FB069")
for k in range(1400):
    x=random.randint(0,255); y=random.randint(128,255)
    c=random.choice([(104,160,90),(140,190,112),(118,172,98)])
    d.line([(x,y),(x+random.randint(-1,1),y-2)],fill=c)
reg("turf",0,128,256,128)
d.rectangle([256,128,511,255],fill="#C8A577")
for k in range(0,128,6):
    d.line([(256,128+k),(511,128+k)],fill=(190,154,105),width=1)
d.rectangle([256,128,511,255],outline=(160,124,80),width=3)
reg("kraft",256,128,256,128)
def ctext(box,txt,font,fill,stroke=0,sf=None):
    x0,y0,x1,y1=box
    b=d.textbbox((0,0),txt,font=font,stroke_width=stroke)
    w=b[2]-b[0]; h=b[3]-b[1]
    d.text(((x0+x1-w)/2-b[0],(y0+y1-h)/2-b[1]),txt,font=font,fill=fill,stroke_width=stroke,stroke_fill=sf)
def fruitIcon(cx,cy,r,col,kind="round"):
    if kind=="banana":
        d.arc([cx-r,cy-r*1.2,cx+r,cy+r*0.8],20,160,fill=col,width=int(r*0.55))
    elif kind=="grape":
        for i,(dx,dy) in enumerate([(-1,-1),(0,-1),(1,-1),(-.5,0),(.5,0),(0,1)]):
            rr=r*0.38; x=cx+dx*rr*1.6; y=cy+dy*rr*1.6
            d.ellipse([x-rr,y-rr,x+rr,y+rr],fill=col,outline=(60,30,80),width=2)
    else:
        d.ellipse([cx-r,cy-r,cx+r,cy+r],fill=col,outline=(70,40,30),width=3)
        d.ellipse([cx-r*0.55,cy-r*0.6,cx-r*0.15,cy-r*0.25],fill=(255,255,255))
    d.polygon([(cx,cy-r*1.0),(cx+r*0.6,cy-r*1.45),(cx+r*0.15,cy-r*0.9)],fill="#4E9A3E")
boxes=[("사과","APPLE","#D93A3A","round"),("오렌지","ORANGE","#F39A2B","round"),("바나나","BANANA","#F2D35B","banana"),("포도","GRAPE","#6B3E8E","grape")]
for i,(k,e,c,kind) in enumerate(boxes):
    x=i*256; y=256
    d.rectangle([x,y,x+255,y+127],fill="#C8A577")
    for kk in range(0,128,7): d.line([(x,y+kk),(x+255,y+kk)],fill=(190,154,105))
    d.rounded_rectangle([x+10,y+12,x+245,y+115],radius=14,outline=c,width=5)
    fruitIcon(x+60,y+68,30,c,kind)
    ctext((x+100,y+20,x+240,y+80),k,F(42),c)
    ctext((x+100,y+78,x+240,y+108),"햇살농장 · "+e,F(15,False),(90,60,35))
    reg("box_"+e.lower(),x,y,256,128)
packs=[("딸기","#E84A5F","#FFE3E6"),("블루베리","#3E4E9E","#E3E8FF"),("방울토마토","#E84A3A","#FFF0D6"),("체리","#A3203A","#FFE6EC"),
       ("키위","#7A9A2E","#F1F7DC"),("청포도","#7DB04A","#EEF8E0"),("버섯","#8A6A4A","#F4ECE0"),("깻잎","#3F7D4E","#E6F2E6")]
for i,(k,c,bg) in enumerate(packs):
    x=i*128; y=384
    d.rectangle([x,y,x+127,y+127],fill=bg)
    d.rectangle([x+4,y+4,x+123,y+123],outline=c,width=4)
    for gx in range(x+4,x+124,12):
        d.rectangle([gx,y+86,gx+5,y+119],fill=c)
    for gy in range(y+86,y+120,12):
        d.rectangle([x+4,gy,x+123,gy+5],fill=c)
    d.ellipse([x+40,y+14,x+88,y+58],fill=c)
    ctext((x,y+56,x+128,y+86),k,F(22 if len(k)<4 else 16),c)
    reg("pack_%d"%i,x,y,128,128)
juices=[("오렌지 주스","#F39A2B","#FFF4E0"),("사과 주스","#D93A3A","#FFF0E8"),("포도 주스","#6B3E8E","#F2EAF8"),("토마토 주스","#E84A3A","#FFEDE6")]
for i,(k,c,bg) in enumerate(juices):
    x=i*256; y=512
    d.rectangle([x,y,x+255,y+127],fill=bg)
    d.rectangle([x,y+8,x+255,y+22],fill=c); d.rectangle([x,y+106,x+255,y+120],fill=c)
    d.ellipse([x+18,y+36,x+78,y+96],fill=c,outline="#472F26",width=3)
    d.ellipse([x+30,y+46,x+44,y+58],fill="white")
    ctext((x+84,y+30,x+250,y+78),k,F(30),"#472F26")
    ctext((x+84,y+76,x+250,y+100),"100% FRESH · 1L",F(14,False),c)
    reg("bottle_%d"%i,x,y,256,128)
cans=[("황도","PEACH","#F7A98B","#FEF0B6"),("파인애플","PINEAPPLE","#F5D63D","#3F7D4E"),("옥수수","CORN","#F2D35B","#4A90C8"),("귤","MANDARIN","#F39A2B","#BBD3E0")]
for i,(k,e,c,bg) in enumerate(cans):
    x=i*256; y=640
    d.rectangle([x,y,x+255,y+127],fill=bg)
    for sx in range(x,x+256,32): d.rectangle([sx,y,sx+15,y+14],fill="#F5E8CD")
    d.ellipse([x+160,y+30,x+236,y+106],fill=c,outline="#472F26",width=3)
    ctext((x+10,y+26,x+160,y+80),k,F(36),"#472F26" if bg in("#FEF0B6","#BBD3E0") else "#FEF0B6")
    ctext((x+10,y+80,x+160,y+108),e,F(16,False),"#472F26" if bg in("#FEF0B6","#BBD3E0") else "#FEF0B6")
    reg("can_%d"%i,x,y,256,128)
bags=[("양상추","#9BCB6A"),("시금치","#3F7D4E"),("샐러드","#7FB069"),("양파","#D9B48F")]
for i,(k,c) in enumerate(bags):
    x=i*128; y=768
    d.rectangle([x,y,x+127,y+191],fill="#EAF3E2")
    d.rectangle([x,y,x+127,y+30],fill=c)
    for j in range(7):
        cx=x+30+random.randint(0,68); cy=y+70+random.randint(0,60); r=random.randint(14,22)
        d.ellipse([cx-r,cy-r,cx+r,cy+r],fill=c,outline=tuple(int(int(c[q:q+2],16)*0.75) for q in (1,3,5)),width=2)
    d.rounded_rectangle([x+10,y+140,x+117,y+180],radius=8,fill="#FEF0B6",outline="#472F26",width=2)
    ctext((x+10,y+140,x+117,y+180),k,F(20),"#472F26")
    reg("bag_%d"%i,x,y,128,192)
prices=["1,980","2,500","990","3,900","4,500","1,200","5,900","2,980"]
pcols=[("#FEF0B6","#D9534F"),("#BBD3E0","#472F26"),("#D9534F","#FEF0B6"),("#FEF0B6","#3F7D4E")]
for i,p in enumerate(prices):
    x=512+(i%4)*128; y=768+(i//4)*96
    bg,fg=pcols[i%4]
    d.rectangle([x,y,x+127,y+95],fill="#F5E8CD")
    d.rounded_rectangle([x+4,y+4,x+123,y+91],radius=10,fill=bg,outline="#472F26",width=3)
    d.ellipse([x-8,y+40,x+12,y+56],fill="#F5E8CD"); d.ellipse([x+116,y+40,x+136,y+56],fill="#F5E8CD")
    ctext((x+10,y+8,x+118,y+34),"오늘의 특가" if i%2==0 else "SALE!",F(14),fg)
    ctext((x+10,y+34,x+118,y+84),"₩"+p,F(30),fg)
    reg("price_%d"%i,x,y,128,96)
d.rectangle([0,960,511,1023],fill="#3F7D4E")
for sx in range(0,512,24): d.polygon([(sx,1023),(sx+12,1010),(sx+24,1023)],fill="#FEF0B6")
ctext((0,962,512,1012),"FRESH  과일 · 야채",F(34),"#FEF0B6")
reg("banner",0,960,512,64)
d.rectangle([512,960,767,1023],fill="#F5E8CD")
for gx in range(512,768,16): d.rectangle([gx,960,gx+7,1023],fill=(127,176,105))
for gy in range(960,1024,16): d.rectangle([512,gy,767,gy+7],fill=(127,176,105))
for gx in range(512,768,16):
    for gy in range(960,1024,16): d.rectangle([gx,gy,gx+7,gy+7],fill=(63,125,78))
reg("gingham",512,960,256,64)
for sx in range(768,1024,32):
    d.rectangle([sx,960,sx+15,1023],fill="#3F7D4E"); d.rectangle([sx+16,960,sx+31,1023],fill="#F5E8CD")
reg("stripe",768,960,256,64)


# --- pale blue plastic basket (open grid bars), 128x64 at (704,192) - flat colours sampled by bar tubes
bx,by=704,192
d.rectangle([bx,by,bx+127,by+63],fill=(124,164,212))      # bars (right half)
d.rectangle([bx,by,bx+31,by+63],fill=(166,198,234))       # rim highlight (left quarter)
d.rectangle([bx+32,by,bx+63,by+63],fill=(102,142,194))    # shadowed bars
reg("basket_slots",bx,by,128,64)
# --- pastel swatches (mart palette) in the basket region's unused columns; basket only samples x=16/48/96
for (n,c),(ox,oy) in zip([('pst_pink', (236, 168, 178)), ('pst_mint', (160, 210, 184)), ('pst_peach', (242, 186, 140)), ('pst_lavender', (190, 172, 224)), ('pst_coral', (234, 146, 132)), ('pst_sage', (180, 200, 146)), ('pst_rose', (222, 150, 176)), ('pst_lemon', (240, 214, 128))],[(64, 0), (64, 32), (76, 0), (76, 32), (108, 0), (108, 32), (118, 0), (118, 32)]):
    w=10 if ox>=108 else 12
    d.rectangle([bx+ox,by+oy,bx+ox+w-1,by+oy+31],fill=c); reg(n,bx+ox,by+oy,w,32)

img.save(OUT+r"\T_Kit_Fresh.png")
json.dump(regions,open(OUT+r"\regions.json","w"))
