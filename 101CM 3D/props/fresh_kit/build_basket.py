
RIM=('basket_slots',(0,0,0.25,1)); BAR=('basket_slots',(0.5,0,1,1)); BARD=('basket_slots',(0.25,0,0.5,1))
def bar(mb, p0, p1, r, val, sides=4):
    p0=Vector(p0); p1=Vector(p1); d=(p1-p0); L=d.length
    if L<1e-6: return
    d.normalize()
    up=Vector((0,0,1)) if abs(d.z)<0.9 else Vector((1,0,0))
    a=d.cross(up).normalized(); b=d.cross(a).normalized()
    reg,sub=val; u0,v0,u1,v1=reg_uv(reg,sub); cu=((u0+u1)/2,(v0+v1)/2)
    ang=[2*math.pi*k/sides + math.pi/sides for k in range(sides)]
    A=[p0+(a*math.cos(t)+b*math.sin(t))*r for t in ang]; B=[p1+(a*math.cos(t)+b*math.sin(t))*r for t in ang]
    for i in range(sides):
        j=(i+1)%sides
        mb.face([A[i],A[j],B[j],B[i]][::-1],[cu]*4, smooth=(sides>4))

def plastic_basket(name="Prop_ShopBasket"):
    # big pale-blue plastic shopping basket with an open coarse grid (real holes), trapezoid sides (wider top), no doorway.
    # Unity x 0.7..2.6, z 8.8..10.6. Blender: x = -(Ux-1.65), y = -(Uz-9.70). Colliders: vertical walls x +-0.90, y +-0.85
    c=get_coll(name); mb=MB()
    H=0.56; BX,BY=0.86,0.81; TX,TY=0.99,0.94; R=0.02
    P=lambda sx,sy,h: Vector((sx*(BX+(TX-BX)*h/H), sy*(BY+(TY-BY)*h/H), h))
    # horizontal rings (incl. rim)
    for h in (0.035,0.19,0.37):
        for (a,b) in (((-1,-1),(1,-1)),((1,-1),(1,1)),((1,1),(-1,1)),((-1,1),(-1,-1))):
            bar(mb,P(a[0],a[1],h),P(b[0],b[1],h),R,BAR)
    for (a,b) in (((-1,-1),(1,-1)),((1,-1),(1,1)),((1,1),(-1,1)),((-1,1),(-1,-1))):
        bar(mb,P(a[0],a[1],H),P(b[0],b[1],H),0.042,RIM)              # thick rim
        bar(mb,P(a[0],a[1],H-0.06),P(b[0],b[1],H-0.06),0.03,BAR)     # rim lip
    # vertical bars, ~0.2 apart (coarse)
    def verts(axis, n):
        for k in range(n+1):
            t=-1+2*k/n
            for s in (1,-1):
                if axis=='x': b0=P(t,s,0); b1=P(t,s,H)
                else:         b0=P(s,t,0); b1=P(s,t,H)
                rr=0.032 if k in (0,n) else R
                bar(mb,b0,b1,rr,RIM if k in (0,n) else BAR)
    verts('x',9); verts('y',8)
    # floor grid
    for k in range(10):
        x=-BX+2*BX*k/9; bar(mb,(x,-BY,0.02),(x,BY,0.02),R,BARD)
    for k in range(9):
        y=-BY+2*BY*k/8; bar(mb,(-BX,y,0.02),(BX,y,0.02),R,BARD)
    # handles: north one up over the middle, south one folded down outside (bears lean over that rim)
    for s in (1,-1):
        Y=s*TY
        if s>0: pts=[(-0.50,Y,H),(-0.44,s*1.00,0.44),(-0.30,s*1.02,0.29),(0.30,s*1.02,0.29),(0.44,s*1.00,0.44),(0.50,Y,H)]
        else:   pts=[(-0.50,Y,H),(-0.42,s*0.62,0.88),(-0.28,s*0.18,1.04),(0.28,s*0.18,1.04),(0.42,s*0.62,0.88),(0.50,Y,H)]
        for i in range(len(pts)-1):
            bar(mb,pts[i],pts[i+1],0.03,RIM,sides=6)
    ob=mb.build(name,c,MAT); stats[ob.name]=mb.tris(); return ob


def _grid_basket(mb, z0, H, BX, BY, TX, TY, full=True, handles=True, R=0.022):
    # pale-blue grid basket (same look as Prop_ShopBasket) with parameters; full=False -> only the band above the
    # basket below (rim + short bars), used for the hidden middle baskets of a nested stack
    from mathutils import Vector
    P=lambda sx,sy,h: Vector((sx*(BX+(TX-BX)*h/H), sy*(BY+(TY-BY)*h/H), z0+h))
    sides=(((-1,-1),(1,-1)),((1,-1),(1,1)),((1,1),(-1,1)),((-1,1),(-1,-1)))
    hb=0.0 if full else H-0.30
    rings=[0.035,0.36*H,0.68*H] if full else [H-0.22]
    for h in rings:
        for (a,b) in sides: bar(mb,P(a[0],a[1],h),P(b[0],b[1],h),R,BAR)
    for (a,b) in sides:
        bar(mb,P(a[0],a[1],H),P(b[0],b[1],H),0.046,RIM)
        bar(mb,P(a[0],a[1],H-0.07),P(b[0],b[1],H-0.07),0.032,BAR)
    def verts(axis,n):
        for k in range(n+1):
            t=-1+2*k/n
            for s in (1,-1):
                b0=P(t,s,hb) if axis=='x' else P(s,t,hb); b1=P(t,s,H) if axis=='x' else P(s,t,H)
                bar(mb,b0,b1,0.034 if k in (0,n) else R, RIM if k in (0,n) else BAR)
    verts('x',9); verts('y',8)
    if full:
        for k in range(10):
            x=-BX+2*BX*k/9; bar(mb,(x,-BY,z0+0.02),(x,BY,z0+0.02),R,BARD)
        for k in range(9):
            y=-BY+2*BY*k/8; bar(mb,(-BX,y,z0+0.02),(BX,y,z0+0.02),R,BARD)
    if handles:   # both handles folded down outside the long sides
        for s in (1,-1):
            Y=s*TY
            pts=[(-0.50,Y,z0+H),(-0.44,s*(TY+0.06),z0+H-0.12),(-0.30,s*(TY+0.08),z0+H-0.27),(0.30,s*(TY+0.08),z0+H-0.27),(0.44,s*(TY+0.06),z0+H-0.12),(0.50,Y,z0+H)]
            for i in range(len(pts)-1): bar(mb,pts[i],pts[i+1],0.03,RIM,sides=6)

def basket_nest_stack(name="Prop_BasketNest", n=5, H=1.12, step=0.20, BX=0.76, BY=0.72, TX=0.99, TY=0.94):
    # nested shopping baskets (each one sits inside the one below, rims stepping up), baskets 2x the old height
    c=get_coll(name); mb=MB()
    for i in range(n):
        z0=i*step
        _grid_basket(mb,z0,H,BX,BY,TX,TY,full=(i==0 or i==n-1),handles=(i==0 or i==n-1))
    ob=mb.build(name,c,MAT); stats[ob.name]=mb.tris(); return ob, (n-1)*step, (n-1)*step+H


def basket_nest_stack2(name, n, sx=1.18, sy=1.33, sz=1.62, false_floor=None):
    # user-scaled nested basket stack (user edit 2026-10-08: x1.18, y1.33, z1.62); bar thickness NOT scaled.
    # false_floor = depth below the top rim of an extra grid floor inside the top basket (so bears can stand there)
    BX,BY,TX,TY=0.76*sx,0.72*sy,0.99*sx,0.94*sy; H=1.12*sz; step=0.20*sz
    c=get_coll(name); mb=MB()
    for i in range(n):
        _grid_basket(mb,i*step,H,BX,BY,TX,TY,full=(i==0 or i==n-1),handles=(i==0 or i==n-1))
    top=(n-1)*step+H
    if false_floor:
        z=top-false_floor; h=H-false_floor
        bx=BX+(TX-BX)*h/H; by=BY+(TY-BY)*h/H
        for k in range(10):
            x=-bx+2*bx*k/9; bar(mb,(x,-by,z),(x,by,z),0.022,BARD)
        for k in range(9):
            y=-by+2*by*k/8; bar(mb,(-bx,y,z),(bx,y,z),0.022,BARD)
    ob=mb.build(name,c,MAT); stats[ob.name]=mb.tris()
    return ob, dict(BX=BX,BY=BY,TX=TX,TY=TY,H=H,step=step,top=top,top_base=(n-1)*step,floor=(top-false_floor) if false_floor else (n-1)*step)
