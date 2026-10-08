
TRAY_COLS=['sw_greenD','sw_yellow','sw_blue','sw_black','sw_felt','sw_red','sw_azure','sw_black']
def _tq(mb, pts, reg, want_n):
    from mathutils import Vector
    P=[Vector(p) for p in pts]
    u0,v0,u1,v1=reg_uv(reg,(0.3,0.3,0.7,0.7)); cu=((u0+u1)/2,(v0+v1)/2)
    n=(P[1]-P[0]).cross(P[3]-P[0])
    if n.dot(Vector(want_n))<0: P=P[::-1]
    mb.face(P,[cu]*4)

def produce_tray(mb, W, Dp, H, col, inside='sw_kraft', ground=False, t=0.03, post=0.03):
    # open cardboard produce tray: coloured printed outside, kraft inside, hand holes on the short ends,
    # kraft stacking posts in the 4 inner corners standing 'post' above the rim (next tray sits on them)
    from mathutils import Vector
    up=Vector((0,0,1))
    def wall(c, ud, nd, L, hole=None):
        c=Vector(c); ud=Vector(ud); nd=Vector(nd)
        def pt(u,v,s): return c+ud*u+up*v+nd*(t/2*s)
        if hole:
            hu0,hu1,hv0,hv1=hole
            rects=[(-L/2,L/2,0,hv0),(-L/2,L/2,hv1,H),(-L/2,hu0,hv0,hv1),(hu1,L/2,hv0,hv1)]
        else:
            rects=[(-L/2,L/2,0,H)]
        for s,reg in ((1,col),(-1,inside)):
            for (a,b,v0,v1) in rects:
                _tq(mb,[pt(a,v0,s),pt(b,v0,s),pt(b,v1,s),pt(a,v1,s)],reg,nd*s)
        if hole:
            _tq(mb,[pt(hu0,hv0,1),pt(hu1,hv0,1),pt(hu1,hv0,-1),pt(hu0,hv0,-1)],inside,up)
            _tq(mb,[pt(hu0,hv1,1),pt(hu1,hv1,1),pt(hu1,hv1,-1),pt(hu0,hv1,-1)],inside,-up)
            _tq(mb,[pt(hu0,hv0,1),pt(hu0,hv1,1),pt(hu0,hv1,-1),pt(hu0,hv0,-1)],inside,ud)
            _tq(mb,[pt(hu1,hv0,1),pt(hu1,hv1,1),pt(hu1,hv1,-1),pt(hu1,hv0,-1)],inside,-ud)
        _tq(mb,[pt(-L/2,H,1),pt(L/2,H,1),pt(L/2,H,-1),pt(-L/2,H,-1)],inside,up)   # cut edge = kraft
        return pt
    # long walls (along X) with end caps
    for s in (1,-1):
        pt=wall((0,s*(Dp/2-t/2),0),(1,0,0),(0,s,0),W)
        for e in (1,-1):
            _tq(mb,[pt(e*W/2,0,1),pt(e*W/2,H,1),pt(e*W/2,H,-1),pt(e*W/2,0,-1)],inside,(e,0,0))
    Ls=Dp-2*t
    for s in (1,-1):   # short ends with hand holes
        wall((s*(W/2-t/2),0,0),(0,1,0),(s,0,0),Ls,(-Ls*0.17,Ls*0.17,H*0.52,H*0.80))
    _tq(mb,[(-W/2,-Dp/2,t*0.5),(W/2,-Dp/2,t*0.5),(W/2,Dp/2,t*0.5),(-W/2,Dp/2,t*0.5)],inside,(0,0,1))
    if ground: _tq(mb,[(-W/2,-Dp/2,0),(W/2,-Dp/2,0),(W/2,Dp/2,0),(-W/2,Dp/2,0)],col,(0,0,-1))
    # triangular-ish corner posts (square posts tucked in the corners)
    ps=0.07
    for sx in (1,-1):
        for sy in (1,-1):
            mb.box((sx*(W/2-t-ps/2),sy*(Dp/2-t-ps/2),(H+post)/2),(ps,ps,H+post),inside,skip=('bottom',))

def tray_stack(name="Prop_TrayStack", n=8, W=1.2, Dp=0.8, H=0.26, seed=5):
    import random, math
    rnd=random.Random(seed)
    c=get_coll(name); mb=MB(); z=0.0; post=0.03
    for i in range(n):
        w=W*rnd.uniform(0.96,1.06); d=Dp*rnd.uniform(0.97,1.04); h=H*rnd.uniform(0.85,1.1)
        ox=rnd.uniform(-0.06,0.06) if i else 0.0; oy=rnd.uniform(-0.04,0.04) if i else 0.0
        yaw=math.radians(rnd.uniform(-5,5)) if i else 0.0
        mb.push((ox,oy,z),yaw)
        produce_tray(mb,w,d,h,TRAY_COLS[i%len(TRAY_COLS)],ground=(i==0),post=post)
        mb.pop()
        z+=h+post
    ob=mb.build(name,c,MAT); stats[ob.name]=mb.tris(); return ob, z


def tray_wall(name="Prop_TrayWall_South", length=19.5, W=1.6, Dp=0.95, H=0.54, depth_center=0.56, seed=11, nmin=1, nmax=4, cols=None, inside="sw_kraft", min_diff=1):
    # a row of produce-tray stacks along a wall. Blender X = along the wall, front (room side) = -Y.
    # every stack gets a random tray count (nmin..nmax), so the top line is uneven.
    import random, math
    rnd=random.Random(seed)
    c=get_coll(name); mb=MB(); post=0.03
    stacks=[]; x=length/2
    cols=cols or ['pst_pink','pst_mint','pst_peach','pst_lavender','pst_coral','pst_sage','pst_rose','pst_lemon','sw_azure','sw_yellow']
    prev_n=None
    while True:
        w0=W*rnd.uniform(0.92,1.08)
        gap=rnd.uniform(0.03,0.14)
        if x-gap-w0 < -length/2: break
        cx=x-gap-w0/2; x=cx-w0/2
        n=rnd.randint(nmin,nmax)
        while prev_n is not None and abs(n-prev_n)<min_diff: n=rnd.randint(nmin,nmax)   # keep neighbours visibly different
        prev_n=n
        z=0.0; last=None; colprev=None
        for i in range(n):
            w=w0*rnd.uniform(0.97,1.03); d=Dp*rnd.uniform(0.96,1.04); h=H*rnd.uniform(0.85,1.12)
            ox=rnd.uniform(-0.05,0.05) if i else 0.0; oy=rnd.uniform(-0.04,0.04) if i else 0.0
            yaw=math.radians(rnd.uniform(-4,4)) if i else 0.0
            col=rnd.choice([k for k in cols if k!=colprev]); colprev=col
            mb.push((cx+ox,-depth_center+oy,z),yaw)
            produce_tray(mb,w,d,h,col,inside=inside,ground=(i==0),post=post)
            mb.pop()
            last=(z,w,d,h,cx+ox,-depth_center+oy); z+=h+post
        stacks.append({"cx":cx,"n":n,"w":w0,"top_base":last[0],"top_w":last[1],"top_d":last[2],"top_h":last[3],"top_x":last[4],"top_y":last[5],"height":z})
    ob=mb.build(name,c,MAT); stats[ob.name]=mb.tris(); return ob, stacks


def remap_uv_region(ob, mapping):
    # move face UVs that sit inside region A onto the centre of region B (keeps geometry/layout identical)
    me=ob.data; uvl=me.uv_layers.active.data
    boxes={a:(reg_uv(a,(0,0,1,1)), reg_uv(b,(0.3,0.3,0.7,0.7))) for a,b in mapping.items()}
    n=0
    for poly in me.polygons:
        us=[uvl[i].uv for i in poly.loop_indices]; cu=sum(u.x for u in us)/len(us); cv=sum(u.y for u in us)/len(us)
        for a,((u0,v0,u1,v1),(t0,tv0,t1,tv1)) in boxes.items():
            if min(u0,u1)<=cu<=max(u0,u1) and min(v0,v1)<=cv<=max(v0,v1):
                c=((t0+t1)/2,(tv0+tv1)/2)
                for i in poly.loop_indices: uvl[i].uv=c
                n+=1; break
    return n
