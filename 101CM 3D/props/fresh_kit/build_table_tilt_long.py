
def produce_table_tilt_long(name="Prop_ProduceTable_TiltL", L=4.2, deg=12.0, zf=0.90, W=2.2):
    # long version: 3 crates across. legs at both ends + a middle pair
    c=get_coll(name); mb=MB()
    a=math.radians(deg); ta=math.tan(a)
    D=W/math.cos(a); n=max(12,round(12*W/2.2)); sw=0.15; g=(D-n*sw)/(n-1); t=0.04
    _unused= sw=0.15; g=(D-n*sw)/(n-1); t=0.04
    top=lambda y: zf+(y+W/2)*ta
    lx=L/2-0.09
    mb.push((0,-W/2,zf),0,a)
    for i in range(n):
        y=sw/2+i*(sw+g); k=i%4
        mb.box((0,y,-t/2),(L,sw,t),{'top':('wood',(0.02*k,0.25*k+0.03,0.55+0.1*k,0.25*k+0.22)),'all':'sw_woodM'})
    for x in (-lx+0.13,-0.7,0.7,lx-0.13):
        mb.box((x,D/2,-t-0.035),(0.07,D-0.1,0.07),{'all':'sw_woodL','left':('wood',(0,0.5,0.3,0.72)),'right':('wood',(0,0.5,0.3,0.72))},skip=('top',))
    for sx in (-1,1):
        mb.box((sx*lx,D/2,-t-0.06),(0.06,D-0.06,0.10),{'all':'sw_woodL','left':('wood',(0.1,0.5,0.75,0.7)),'right':('wood',(0.1,0.5,0.75,0.7))},skip=('top',))
    mb.pop()
    for x in (-lx,0.0,lx):
        for sy in (-1,1):
            y=sy*(W/2-0.09); h=top(y)-t-0.02
            mb.box((x,y,h/2),(0.16,0.16,h),{'all':('wood',(0.6,0.0,0.75,0.25),1)},skip=('bottom',))
    for sy in (-1,1):
        y=sy*(W/2-0.09); z=top(y)-t-0.06
        mb.box((0,y,z),(L-0.34,0.06,0.08),{'all':'sw_woodL','front':('wood',(0.0,0.75,1.0,0.95)),'back':('wood',(0.0,0.75,1.0,0.95))})
    mb.box((0,W/2-0.09,0.78),(L-0.34,0.06,0.08),{'all':'sw_woodL','front':('wood',(0.0,0.75,1.0,0.95)),'back':('wood',(0.0,0.75,1.0,0.95))})
    for x in (-lx,0.0,lx):
        mb.box((x,0,0.78),(0.06,W-0.34,0.08),{'all':'sw_woodL','left':('wood',(0.1,0.5,0.75,0.7)),'right':('wood',(0.1,0.5,0.75,0.7))})
    for sx in (-1,1):
        mb.push((sx*(lx-0.13),-(W/2-0.09),top(-(W/2-0.09))-t-0.17),0,0,-sx*math.pi/4)
        mb.box((0,0,0),(0.05,0.045,0.17),'sw_woodM')
        mb.pop()
    ob=mb.build(name,c,MAT); stats[ob.name]=mb.tris(); return ob
