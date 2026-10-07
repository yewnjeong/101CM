
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
