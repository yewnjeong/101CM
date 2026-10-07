
TILT_DEG = 12.0
def produce_table_tilt(name="Prop_ProduceTable_Tilt", deg=TILT_DEG, zf=0.90):
    # slatted pine table whose top rises toward the back (+Y in Blender = Unity -Z). front edge top = zf
    c=get_coll(name); mb=MB()
    a=math.radians(deg); ta=math.tan(a)
    L=2.8; W=2.2; D=W/math.cos(a); n=12; sw=0.15; g=(D-n*sw)/(n-1); t=0.04
    top=lambda y: zf+(y+W/2)*ta          # surface height at horizontal y
    mb.push((0,-W/2,zf),0,a)            # local frame: y along slope, z normal
    for i in range(n):
        y=sw/2+i*(sw+g); k=i%4
        mb.box((0,y,-t/2),(L,sw,t),{'top':('wood',(0.02*k,0.25*k+0.03,0.55+0.1*k,0.25*k+0.22)),'all':'sw_woodM'})
    for x in (-1.18,0.0,1.18):
        mb.box((x,D/2,-t-0.035),(0.07,D-0.1,0.07),{'all':'sw_woodL','left':('wood',(0,0.5,0.3,0.72)),'right':('wood',(0,0.5,0.3,0.72))},skip=('top',))
    for sx in (-1,1):   # sloped side aprons
        mb.box((sx*1.31,D/2,-t-0.06),(0.06,D-0.06,0.10),{'all':'sw_woodL','left':('wood',(0.1,0.5,0.75,0.7)),'right':('wood',(0.1,0.5,0.75,0.7))},skip=('top',))
    mb.pop()
    # legs: front short, back tall (top meets the sloped apron)
    for sx in (-1,1):
        for sy in (-1,1):
            y=sy*1.01; h=top(y)-t-0.02
            mb.box((sx*1.31,y,h/2),(0.16,0.16,h),{'all':('wood',(0.6,0.0,0.75,0.25),1)},skip=('bottom',))
    # front & back rails just under the top edge
    for sy in (-1,1):
        y=sy*1.01; z=top(y)-t-0.06
        mb.box((0,y,z),(2.46,0.06,0.08),{'all':'sw_woodL','front':('wood',(0.1,0.75,0.9,0.95)),'back':('wood',(0.1,0.75,0.9,0.95))})
    # low stretchers (bottom 0.74 -> jelly passes under)
    mb.box((0,1.01,0.78),(2.46,0.06,0.08),{'all':'sw_woodL','front':('wood',(0.1,0.75,0.9,0.95)),'back':('wood',(0.1,0.75,0.9,0.95))})
    for sx in (-1,1):
        mb.box((sx*1.31,0,0.78),(0.06,1.86,0.08),{'all':'sw_woodL','left':('wood',(0.1,0.5,0.75,0.7)),'right':('wood',(0.1,0.5,0.75,0.7))})
    # corner brackets under the front rail
    for sx in (-1,1):
        mb.push((sx*1.18,-1.01,top(-1.01)-t-0.17),0,0,-sx*math.pi/4)
        mb.box((0,0,0),(0.05,0.045,0.17),'sw_woodM')
        mb.pop()
    ob=mb.build(name,c,MAT); stats[ob.name]=mb.tris(); return ob
