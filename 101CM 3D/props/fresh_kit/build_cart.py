
def tube(mb, p0, p1, r, reg, ends=False, sides=4, smooth=False):
    p0=Vector(p0); p1=Vector(p1); d=(p1-p0); L=d.length
    if L<1e-6: return
    d.normalize()
    up=Vector((0,0,1)) if abs(d.z)<0.9 else Vector((1,0,0))
    a=d.cross(up).normalized(); b=d.cross(a).normalized()
    u0,v0,u1,v1=reg_uv(reg); cu=((u0+u1)/2,(v0+v1)/2)
    ang=[2*math.pi*k/sides + (math.pi/sides if sides==4 else 0) for k in range(sides)]
    ring=lambda c:[c+(a*math.cos(t)+b*math.sin(t))*r for t in ang]
    A=ring(p0); B=ring(p1)
    for i in range(sides):
        j=(i+1)%sides
        mb.face([A[i],A[j],B[j],B[i]][::-1],[cu]*4, smooth=smooth)
    if ends:
        mb.face(A,[cu]*sides); mb.face(B[::-1],[cu]*sides)

def lathe(mb, c, axis, perp1, perp2, prof, segs, reg, smooth=True, cap0=False, cap1=False):
    # prof: list of (radius, offset along axis); surface of revolution
    c=Vector(c); ax=Vector(axis); u=Vector(perp1); v=Vector(perp2)
    t0,tv0,t1,tv1=reg_uv(reg); cu=((t0+t1)/2,(tv0+tv1)/2)
    P=lambda i,j: c+ax*prof[j][1]+(u*math.cos(2*math.pi*i/segs)+v*math.sin(2*math.pi*i/segs))*prof[j][0]
    for j in range(len(prof)-1):
        for i in range(segs):
            q=[P(i,j),P(i+1,j),P(i+1,j+1),P(i,j+1)]
            mb.face(q,[cu]*4,smooth=smooth)
    if cap0: mb.face([P(i,0) for i in range(segs)][::-1],[cu]*segs)
    if cap1: mb.face([P(i,len(prof)-1) for i in range(segs)],[cu]*segs)

def caster_wheel(mb, x, y, top_z):
    # red rounded wheel + silver hub + grey caster fork + swivel plate (reference photo)
    R=0.072; W=0.05; cx=x+0.022; cz=R
    ay=Vector((0,1,0)); ux=Vector((1,0,0)); uz=Vector((0,0,1))
    tire=[(0.046,-W/2),(0.064,-W/2+0.004),(R,-0.009),(R,0.009),(0.064,W/2-0.004),(0.046,W/2)]
    lathe(mb,(cx,y,cz),ay,ux,uz,tire,12,'sw_red')
    for s in (-1,1):   # domed silver hubs
        hub=[(0.046,s*W/2),(0.03,s*(W/2+0.006)),(0.0,s*(W/2+0.009))]
        if s<0: hub=hub
        lathe(mb,(cx,y,cz),ay,ux,uz,hub if s>0 else hub[::-1],12,'sw_metalL')
    # fork side plates
    for s in (-1,1):
        mb.box((x+0.012,y+s*(W/2+0.016),(top_z-0.012+cz)/2),(0.05,0.008,top_z-0.012-cz+0.02),'sw_metalD')
    mb.box((x+0.008,y,top_z-0.012),(0.06,W+0.04,0.012),'sw_metalD',skip=('bottom',))   # fork top
    tube(mb,(x,y,top_z-0.006),(x,y,top_z+0.02),0.012,'sw_metalL',sides=8,smooth=True)  # swivel stem
    mb.box((x,y,top_z+0.024),(0.07,0.07,0.01),'sw_metalL')                             # mount plate

def lerp(a,b,t): return tuple(a[k]+(b[k]-a[k])*t for k in range(3))

def shopping_cart(name="Prop_ShoppingCart"):
    # nose = Blender +X, handle at -X
    c=get_coll(name); mb=MB()
    M='sw_metalL'; R=0.009
    tz,bz=0.98,0.52
    T=[(-0.45,-0.31,tz),(0.58,-0.31,tz),(0.58,0.31,tz),(-0.45,0.31,tz)]
    Bm=[(-0.40,-0.25,bz),(0.36,-0.25,bz),(0.36,0.25,bz),(-0.40,0.25,bz)]
    for i in range(4):
        tube(mb,T[i],T[(i+1)%4],0.015,M,sides=8,smooth=True)
        tube(mb,Bm[i],Bm[(i+1)%4],0.012,M)
        tube(mb,T[i],Bm[i],0.012,M)
    for t in (0.33,0.66):
        P=[lerp(Bm[i],T[i],t) for i in range(4)]
        for i in range(4): tube(mb,P[i],P[(i+1)%4],R,M)
    n=9
    for k in range(1,n):
        f=k/n
        tube(mb,lerp(T[0],T[1],f),lerp(Bm[0],Bm[1],f),R,M)
        tube(mb,lerp(T[3],T[2],f),lerp(Bm[3],Bm[2],f),R,M)
    for k in range(1,6):
        f=k/6
        tube(mb,lerp(T[1],T[2],f),lerp(Bm[1],Bm[2],f),R,M)
        tube(mb,lerp(T[0],T[3],f),lerp(Bm[0],Bm[3],f),R,M)
        tube(mb,lerp(Bm[0],Bm[3],f),lerp(Bm[1],Bm[2],f),R,M)
    # chassis (round chrome tubes)
    bz2=0.20
    F=[(-0.46,-0.24,bz2),(0.50,-0.20,bz2),(0.50,0.20,bz2),(-0.46,0.24,bz2)]
    for i in range(4): tube(mb,F[i],F[(i+1)%4],0.017,M,sides=8,smooth=True)
    tube(mb,(-0.40,-0.25,bz),F[0],0.017,M,sides=8,smooth=True); tube(mb,(-0.40,0.25,bz),F[3],0.017,M,sides=8,smooth=True)
    tube(mb,(0.30,-0.22,bz),(0.46,-0.20,bz2),0.015,M,sides=8,smooth=True); tube(mb,(0.30,0.22,bz),(0.46,0.20,bz2),0.015,M,sides=8,smooth=True)
    for k in range(4):
        y=-0.18+k*0.12
        tube(mb,(-0.40,y,bz2+0.03),(0.42,y*0.85,bz2+0.03),R,M)
    # handle + red grip
    for s in (-1,1):
        tube(mb,(-0.45,s*0.31,tz),(-0.60,s*0.29,1.09),0.015,M,sides=8,smooth=True)
    tube(mb,(-0.61,-0.34,1.10),(-0.61,0.34,1.10),0.03,'sw_red',ends=True,sides=12,smooth=True)
    # caster wheels
    for (x,y) in ((-0.43,-0.22),(-0.43,0.22),(0.47,-0.18),(0.47,0.18)):
        caster_wheel(mb,x,y,bz2-0.035)
    ob=mb.build(name,c,MAT); stats[ob.name]=mb.tris(); return ob
