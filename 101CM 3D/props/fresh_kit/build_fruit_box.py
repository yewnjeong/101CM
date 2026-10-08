
import json, math, random
from mathutils import Vector
FB_REG=json.load(open(OUT+r"\fruitbox_regions.json"))
FB_W,FB_H=1024,512
def fb_mat():
    m=bpy.data.materials.get("M_FruitBox")
    if m: return m
    m=bpy.data.materials.new("M_FruitBox"); m.use_nodes=True
    nt=m.node_tree; bsdf=nt.nodes.get("Principled BSDF")
    img=bpy.data.images.load(OUT+r"\T_FruitBox.png", check_existing=True)
    tex=nt.nodes.new("ShaderNodeTexImage"); tex.image=img
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"]); bsdf.inputs["Roughness"].default_value=0.85
    return m
def fb_uv(region, u0, v0, u1, v1):
    x,y,w,h=FB_REG[region]
    U=lambda f:(x+f*w)/FB_W; V=lambda f:1-(y+f*h)/FB_H
    return [(U(u0),V(1-v0)),(U(u1),V(1-v0)),(U(u1),V(1-v1)),(U(u0),V(1-v1))]
def fb_face(mb, pts, uvs, want_n):
    P=[Vector(p) for p in pts]
    n=(P[1]-P[0]).cross(P[3]-P[0])
    if n.dot(Vector(want_n))<0: P=P[::-1]; uvs=uvs[::-1]
    mb.face(P,uvs)
def fruit_box(mb, W, Dp, H, kind, rnd, PAN=1.7):
    # closed printed kraft box centred at (0,0), base z=0. Faces sample the fruit panel at a constant print scale.
    def panel_uv(fw,fh):
        su=min(fw/PAN,0.98); sv=min(fh/PAN,0.98); a=rnd.uniform(0,1-su); b=rnd.uniform(0,1-sv)
        return fb_uv(kind,a,b,a+su,b+sv)
    x,y,z=W/2,Dp/2,H
    faces=[ # pts (bottom-left, bottom-right, top-right, top-left seen from outside), size, normal
     ([(-x,-y,0),(x,-y,0),(x,-y,z),(-x,-y,z)],(W,H),(0,-1,0)),
     ([(x,y,0),(-x,y,0),(-x,y,z),(x,y,z)],(W,H),(0,1,0)),
     ([(x,-y,0),(x,y,0),(x,y,z),(x,-y,z)],(Dp,H),(1,0,0)),
     ([(-x,y,0),(-x,-y,0),(-x,-y,z),(-x,y,z)],(Dp,H),(-1,0,0)),
     ([(-x,-y,z),(x,-y,z),(x,y,z),(-x,y,z)],(W,Dp),(0,0,1))]
    for pts,(fw,fh),nrm in faces:
        fb_face(mb,pts,panel_uv(fw,fh),nrm)
    # tape seam along the top (long axis) and the front/back flap edge
    tw=0.10; e=0.004
    fb_face(mb,[(-x-e,-tw/2,z+e),(x+e,-tw/2,z+e),(x+e,tw/2,z+e),(-x-e,tw/2,z+e)],fb_uv("tape",0.2,0.2,0.8,0.8),(0,0,1))
    for s in (1,-1):   # tape running down the short sides a little
        fb_face(mb,[(s*(x+e),-tw/2,z-0.16),(s*(x+e),tw/2,z-0.16),(s*(x+e),tw/2,z+e),(s*(x+e),-tw/2,z+e)],fb_uv("tape",0.2,0.2,0.8,0.8),(s,0,0))
    # hand holes on the short sides (dark slots)
    hw,hh,hz=0.24,0.07,H*0.68
    for s in (1,-1):
        fb_face(mb,[(s*(x+e*2),-hw/2,hz-hh/2),(s*(x+e*2),hw/2,hz-hh/2),(s*(x+e*2),hw/2,hz+hh/2),(s*(x+e*2),-hw/2,hz+hh/2)],fb_uv("hole",0.2,0.2,0.8,0.8),(s,0,0))

def fruit_box_wall(name="Prop_FruitBoxWall_N", length=7.5, W=1.3, Dp=0.9, H=0.62, depth_center=0.5, seed=21, nmin=1, nmax=4):
    rnd=random.Random(seed)
    kinds=["apple","grape","orange","lemon","peach"]
    c=get_coll(name); mb=MB(); stacks=[]; x=length/2; prev_n=None; prev_k=None
    while True:
        w0=W*rnd.uniform(0.94,1.06); gap=rnd.uniform(0.04,0.14)
        if x-gap-w0 < -length/2: break
        cx=x-gap-w0/2; x=cx-w0/2
        n=rnd.randint(nmin,nmax)
        while prev_n is not None and n==prev_n: n=rnd.randint(nmin,nmax)
        prev_n=n
        k0=rnd.choice([k for k in kinds if k!=prev_k]); prev_k=k0
        zc=0.0
        for i in range(n):
            k=k0 if rnd.random()<0.75 else rnd.choice(kinds)
            h=H*rnd.uniform(0.92,1.08); w=w0*rnd.uniform(0.98,1.02); d=Dp*rnd.uniform(0.97,1.03)
            ox=rnd.uniform(-0.04,0.04) if i else 0.0; oy=rnd.uniform(-0.03,0.03) if i else 0.0
            yaw=math.radians(rnd.uniform(-4,4)) if i else 0.0
            mb.push((cx+ox,-depth_center+oy,zc),yaw); fruit_box(mb,w,d,h,k,rnd); mb.pop()
            zc+=h
        stacks.append({"cx":cx,"w":w0,"n":n,"height":zc})
    ob=mb.build(name,c,fb_mat()); stats[ob.name]=mb.tris(); return ob,stacks
