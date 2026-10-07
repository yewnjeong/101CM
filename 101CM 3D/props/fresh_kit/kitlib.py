
# fresh_kit builder library (Blender Z-up, front = -Y  ->  Unity +Z)
import bpy, bmesh, math, json, os, random
from mathutils import Vector, Matrix
OUT = r"C:\Users\mbc\Documents\YW_Project\101CM\101CM 3D\props\fresh_kit"
REG = json.load(open(os.path.join(OUT, "regions.json")))
A = 1024.0

def reg_uv(name, sub=(0,0,1,1)):
    x,y,w,h = REG[name]
    if name.startswith("sw_") or name.startswith("fr_"):
        cx=x+w/2; cy=y+h/2; x,y,w,h = cx-w*0.25, cy-h*0.25, w*0.5, h*0.5
    if name in ("wood","turf","kraft","gingham","stripe","banner"):
        x,y,w,h = x+1.5, y+1.5, w-3, h-3
    u0=(x+w*sub[0])/A; u1=(x+w*sub[2])/A
    vb=1-(y+h)/A; vt=1-y/A
    return u0, vb+(vt-vb)*sub[1], u1, vb+(vt-vb)*sub[3]

def _spec(val):
    if isinstance(val, tuple):
        reg = val[0]; sub = val[1] if len(val)>1 else (0,0,1,1); rot = val[2] if len(val)>2 else 0
        return reg, sub, rot
    return val, (0,0,1,1), 0

class MB:
    def __init__(s):
        s.v=[]; s.f=[]; s.uv=[]; s.sm=[]; s.M=Matrix.Identity(4); s.stack=[]
    def push(s, loc=(0,0,0), yaw=0.0, pitch=0.0, roll=0.0, scale=(1,1,1)):
        s.stack.append(s.M.copy())
        S = Matrix.Diagonal((scale[0],scale[1],scale[2],1))
        s.M = s.M @ Matrix.Translation(loc) @ Matrix.Rotation(yaw,4,'Z') @ Matrix.Rotation(pitch,4,'X') @ Matrix.Rotation(roll,4,'Y') @ S
    def pop(s): s.M = s.stack.pop()
    def face(s, pts, uvs, smooth=False):
        b=len(s.v)
        s.v += [(s.M @ Vector(p).to_4d()).to_3d() for p in pts]
        s.f.append(list(range(b, b+len(pts)))); s.uv.append(list(uvs)); s.sm.append(smooth)
    def quad(s, c, r, u, hr, hu, val):
        reg, sub, rot = _spec(val)
        c=Vector(c); r=Vector(r); u=Vector(u)
        pts=[c-r*hr-u*hu, c+r*hr-u*hu, c+r*hr+u*hu, c-r*hr+u*hu]
        u0,v0,u1,v1 = reg_uv(reg, sub)
        uvs=[(u0,v0),(u1,v0),(u1,v1),(u0,v1)]
        uvs = uvs[rot:]+uvs[:rot]
        s.face(pts, uvs)
    def box(s, center, size, faces, skip=()):
        cx,cy,cz = center; sx,sy,sz = [a/2 for a in size]
        spec = {'front':((cx,cy-sy,cz),(1,0,0),(0,0,1),sx,sz), 'back':((cx,cy+sy,cz),(-1,0,0),(0,0,1),sx,sz),
                'right':((cx+sx,cy,cz),(0,1,0),(0,0,1),sy,sz), 'left':((cx-sx,cy,cz),(0,-1,0),(0,0,1),sy,sz),
                'top':((cx,cy,cz+sz),(1,0,0),(0,1,0),sx,sy), 'bottom':((cx,cy,cz-sz),(1,0,0),(0,-1,0),sx,sy)}
        for k,(c,r,u,hr,hu) in spec.items():
            if k in skip: continue
            val = faces.get(k, faces.get('all')) if isinstance(faces, dict) else faces
            if val is None: continue
            s.quad(c,r,u,hr,hu,val)
    def segwall(s, c, r, u, length, height, regions, plinth=0.0, plinth_reg="sw_brown"):
        # a vertical face split into label segments (c = centre of face, r = right dir, u = up)
        c=Vector(c); r=Vector(r); u=Vector(u)
        n=len(regions); w=length/n
        h=height-plinth
        for i,rg in enumerate(regions):
            cc = c + r*(-length/2 + w*(i+0.5)) + u*(plinth/2)
            s.quad(cc, r, u, w/2, h/2, rg)
        if plinth>0:
            s.quad(c + u*(-height/2+plinth/2), r, u, length/2, plinth/2, plinth_reg)
    def cyl(s, base, radius, height, n, side, top=None, bottom=None, r_top=None, start=math.pi/2):
        rt = radius if r_top is None else r_top
        bx,by,bz = base
        reg, sub, rot = _spec(side)
        u0,v0,u1,v1 = reg_uv(reg, sub)
        ang=[start + 2*math.pi*i/n for i in range(n+1)]
        for i in range(n):
            a0,a1=ang[i],ang[i+1]
            p=[(bx+radius*math.cos(a0),by+radius*math.sin(a0),bz),(bx+radius*math.cos(a1),by+radius*math.sin(a1),bz),
               (bx+rt*math.cos(a1),by+rt*math.sin(a1),bz+height),(bx+rt*math.cos(a0),by+rt*math.sin(a0),bz+height)]
            ua=u0+(u1-u0)*i/n; ub=u0+(u1-u0)*(i+1)/n
            s.face(p,[(ua,v0),(ub,v0),(ub,v1),(ua,v1)], smooth=True)
        if top:
            t0,tv0,t1,tv1=reg_uv(top); cu=((t0+t1)/2,(tv0+tv1)/2)
            s.face([(bx+rt*math.cos(a),by+rt*math.sin(a),bz+height) for a in ang[:n]],[cu]*n)
        if bottom:
            t0,tv0,t1,tv1=reg_uv(bottom); cu=((t0+t1)/2,(tv0+tv1)/2)
            s.face([(bx+radius*math.cos(a),by+radius*math.sin(a),bz) for a in reversed(ang[:n])],[cu]*n)
    def sphere(s, c, r, reg, seg=6, rings=4, sz=1.0):
        t0,tv0,t1,tv1=reg_uv(reg); cu=((t0+t1)/2,(tv0+tv1)/2)
        cx,cy,cz=c
        def P(i,j):
            th=math.pi*j/rings; ph=2*math.pi*i/seg
            return (cx+r*math.sin(th)*math.cos(ph), cy+r*math.sin(th)*math.sin(ph), cz+r*sz*math.cos(th))
        for i in range(seg):
            s.face([P(i,0),P(i+1,1),P(i,1)][::-1],[cu]*3, smooth=True) if False else None
        for i in range(seg):
            i1=(i+1)
            # top cap (CCW seen from outside)
            s.face([P(i,1),P(i1,1),(cx,cy,cz+r*sz)],[cu]*3, smooth=True)
            for j in range(1,rings-1):
                s.face([P(i,j+1),P(i1,j+1),P(i1,j),P(i,j)],[cu]*4, smooth=True)
            s.face([(cx,cy,cz-r*sz),P(i1,rings-1),P(i,rings-1)],[cu]*3, smooth=True)
    def tris(s):
        return sum(len(f)-2 for f in s.f)
    def build(s, name, coll, mat):
        me=bpy.data.meshes.new(name)
        me.from_pydata([tuple(v) for v in s.v], [], s.f)
        uvl=me.uv_layers.new(name="UVMap")
        li=0
        for pi,poly in enumerate(me.polygons):
            for k,loop in enumerate(poly.loop_indices):
                uvl.data[loop].uv = s.uv[pi][k]
            poly.use_smooth = s.sm[pi]
        me.validate(); me.update()
        bm=bmesh.new(); bm.from_mesh(me)
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0005)
        bm.normal_update(); bm.to_mesh(me); bm.free()
        me.materials.append(mat)
        ob=bpy.data.objects.new(name, me)
        coll.objects.link(ob)
        return ob

def get_mat():
    m=bpy.data.materials.get("M_Kit_Fresh")
    if m: return m
    m=bpy.data.materials.new("M_Kit_Fresh"); m.use_nodes=True
    nt=m.node_tree; bsdf=nt.nodes.get("Principled BSDF")
    img=bpy.data.images.load(os.path.join(OUT,"T_Kit_Fresh.png"), check_existing=True)
    tex=nt.nodes.new("ShaderNodeTexImage"); tex.image=img; tex.interpolation='Closest'
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value=0.8
    return m

def get_coll(name):
    c=bpy.data.collections.get(name)
    if c is None:
        c=bpy.data.collections.new(name); bpy.context.scene.collection.children.link(c)
    for o in list(c.objects): bpy.data.objects.remove(o, do_unlink=True)
    return c
