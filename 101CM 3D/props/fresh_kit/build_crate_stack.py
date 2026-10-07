
CRATE_COLS=['sw_yellow','sw_red','sw_azure','sw_blue','sw_cream','sw_white']
def _q(mb, pts, reg, want_n):
    from mathutils import Vector
    P=[Vector(p) for p in pts]
    u0,v0,u1,v1=reg_uv(reg,(0.3,0.3,0.7,0.7)); cu=((u0+u1)/2,(v0+v1)/2)
    n=(P[1]-P[0]).cross(P[3]-P[0])
    if n.dot(Vector(want_n))<0: P=P[::-1]
    mb.face(P,[cu]*4)

def crate(mb, W, Dp, H, reg, top=False, ground=False, t=0.035, exposed=('+x','-x','+y','-y')):
    # open-top plastic bottle crate centred on (0,0), base z=0, with handle windows on all 4 sides (real holes)
    from mathutils import Vector
    def wall(c, ud, nd, L, hole):
        # c = centre of wall mid-plane at z=0, ud = along-wall unit, nd = outward unit
        c=Vector(c); ud=Vector(ud); nd=Vector(nd); up=Vector((0,0,1))
        hu0,hu1,hv0,hv1=hole
        def pt(u,v,s): return c+ud*u+up*v+nd*(t/2*s)
        rects=[(-L/2,L/2,0,hv0),(-L/2,L/2,hv1,H),(-L/2,hu0,hv0,hv1),(hu1,L/2,hv0,hv1)]
        for s in (1,-1):
            for (a,b,v0,v1) in rects:
                _q(mb,[pt(a,v0,s),pt(b,v0,s),pt(b,v1,s),pt(a,v1,s)],reg,nd*s)
        # reveals
        _q(mb,[pt(hu0,hv0,1),pt(hu1,hv0,1),pt(hu1,hv0,-1),pt(hu0,hv0,-1)],reg,up)
        _q(mb,[pt(hu0,hv1,1),pt(hu1,hv1,1),pt(hu1,hv1,-1),pt(hu0,hv1,-1)],reg,-up)
        _q(mb,[pt(hu0,hv0,1),pt(hu0,hv1,1),pt(hu0,hv1,-1),pt(hu0,hv0,-1)],reg,ud)
        _q(mb,[pt(hu1,hv0,1),pt(hu1,hv1,1),pt(hu1,hv1,-1),pt(hu1,hv0,-1)],reg,-ud)
        # rim
        _q(mb,[pt(-L/2,H,1),pt(L/2,H,1),pt(L/2,H,-1),pt(-L/2,H,-1)],reg,up)
        return pt
    hv0,hv1=H*0.50,H*0.80
    # long walls (along X) full length, with end caps
    for s in (1,-1):
        pt=wall((0,s*(Dp/2-t/2),0),(1,0,0),(0,s,0),W,(-W*0.30,W*0.30,hv0,hv1))
        for e in (1,-1):
            _q(mb,[pt(e*W/2,0,1),pt(e*W/2,H,1),pt(e*W/2,H,-1),pt(e*W/2,0,-1)],reg,(e,0,0))
    # short walls (along Y) between long walls
    Ls=Dp-2*t
    for s in (1,-1):
        wall((s*(W/2-t/2),0,0),(0,1,0),(s,0,0),Ls,(-Ls*0.25,Ls*0.25,hv0+H*0.05,hv1))
    # floor (inside) + bottom
    _q(mb,[(-W/2,-Dp/2,t),(W/2,-Dp/2,t),(W/2,Dp/2,t),(-W/2,Dp/2,t)],reg,(0,0,1))
    if ground: _q(mb,[(-W/2,-Dp/2,0),(W/2,-Dp/2,0),(W/2,Dp/2,0),(-W/2,Dp/2,0)],reg,(0,0,-1))

    # ---- relief (raised rim lip, bottom band, ribs beside the windows, corner posts) on exposed sides only
    INW={('y',1):'front',('y',-1):'back',('x',1):'left',('x',-1):'right'}
    def obox(ax,sg,a0,a1,z0,z1,o0,o1):
        E=(Dp/2 if ax=='y' else W/2)
        n0=sg*(E+o0); n1=sg*(E+o1); lo,hi=min(n0,n1),max(n0,n1)
        if ax=='y': mb.box(((a0+a1)/2,(lo+hi)/2,(z0+z1)/2),(a1-a0,hi-lo,z1-z0),reg,skip=(INW[(ax,sg)],))
        else:       mb.box(((lo+hi)/2,(a0+a1)/2,(z0+z1)/2),(hi-lo,a1-a0,z1-z0),reg,skip=(INW[(ax,sg)],))
    RIM_H=0.075; BAND_H=0.055; PL=0.022; PB=0.012; PR=0.016; RW=0.04
    for sd in exposed:
        ax=sd[1]; sg=1 if sd[0]=='+' else -1
        L=(W if ax=='y' else Dp)
        obox(ax,sg,-L/2-PL,L/2+PL,H-RIM_H,H,-t,PL)            # top lip
        obox(ax,sg,-L/2,L/2,0,BAND_H,-t,PB)                   # bottom band
        hh=(W*0.30 if ax=='y' else (Dp-2*t)*0.25)+RW*0.75
        for s2 in (1,-1):                                     # ribs framing the window
            obox(ax,sg,s2*hh-RW/2,s2*hh+RW/2,BAND_H,H-RIM_H,-t,PR)
        obox(ax,sg,-hh,hh,H*0.40,H*0.40+0.03,-t,PB)          # ledge under the window
    for sx in (1,-1):
        for sy in (1,-1):
            if (('+x' if sx>0 else '-x') in exposed) or (('+y' if sy>0 else '-y') in exposed):
                cx=sx*(W/2-0.02+0.008); cy=sy*(Dp/2-0.02+0.008)
                mb.box((cx,cy,H/2),(0.056,0.056,H),reg,skip=('bottom',))
    if top:   # bottle dividers 3x2
        dh=H*0.62
        for k in (-1,0,1):
            if k==0: continue
            x=k*W/6
            for sd in (1,-1): _q(mb,[(x,-Dp/2+t,t),(x,Dp/2-t,t),(x,Dp/2-t,dh),(x,-Dp/2+t,dh)],reg,(sd,0,0))
        for sd in (1,-1): _q(mb,[(-W/2+t,0,t),(W/2-t,0,t),(W/2-t,0,dh),(-W/2+t,0,dh)],reg,(0,sd,0))

def crate_stack(name, nx, ny, nz, cw, cd, ch, seed, heights=None, open_sides=('+x','-x','+y','-y')):
    # heights: optional {(ix,iy): n} per column (overrides nz)
    import random, math
    rnd=random.Random(seed)
    c=get_coll(name); mb=MB()
    for ix in range(nx):
        for iy in range(ny):
            col_base=rnd.randrange(len(CRATE_COLS))
            hz=(heights or {}).get((ix,iy),nz)
            for iz in range(hz):
                reg=CRATE_COLS[(col_base+ (iz if rnd.random()<0.65 else rnd.randrange(6)))%len(CRATE_COLS)]
                cx=(-nx/2+ix+0.5)*cw + rnd.uniform(-0.02,0.02)
                cy=(-ny/2+iy+0.5)*cd + rnd.uniform(-0.02,0.02)
                yaw=math.radians(rnd.uniform(-3,3)) if iz>0 else 0.0
                mb.push((cx,cy,iz*ch),yaw)
                def H_(i,j): return (heights or {}).get((i,j),nz) if (0<=i<nx and 0<=j<ny) else 0
                ex=[]
                for sd,(di,dj) in (('+x',(1,0)),('-x',(-1,0)),('+y',(0,1)),('-y',(0,-1))):
                    nb=H_(ix+di,iy+dj)
                    if nb>iz: continue                      # neighbour crate covers this side
                    if nb==0 and sd not in open_sides: continue   # stack edge against a room wall
                    ex.append(sd)
                crate(mb,cw*0.97,cd*0.97,ch,reg,top=(iz==hz-1),ground=(iz==0),exposed=tuple(ex))
                mb.pop()
    ob=mb.build(name,c,MAT); stats[ob.name]=mb.tris(); return ob
