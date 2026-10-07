
def produce_table_slat():
    # reference: light pine slatted table (user photo 2026-10-07). Collider kept: top 2.8x2.2 @0.85-0.95, legs @(+-1.31,+-1.01)
    c=get_coll("Prop_ProduceTable"); mb=MB()
    L=2.8; Wd=2.2; n=12; sw=0.15; g=(Wd-n*sw)/(n-1); t=0.04
    for i in range(n):
        y=-Wd/2+sw/2+i*(sw+g)
        k=i%4
        mb.box((0,y,0.95-t/2),(L,sw,t),{'top':('wood',(0.02*k,0.25*k+0.03,0.55+0.1*k,0.25*k+0.22)),'front':'sw_woodM','back':'sw_woodM','left':'sw_woodM','right':'sw_woodM','bottom':'sw_woodM'})
    # rails under slats (along Y): two ends + middle
    for x in (-1.18,0.0,1.18):
        mb.box((x,0,0.875),(0.07,Wd-0.12,0.07),{'all':'sw_woodL','top':None,'left':('wood',(0,0.5,0.3,0.72)),'right':('wood',(0,0.5,0.3,0.72))},skip=('top',))
    # legs
    for sx in (-1,1):
        for sy in (-1,1):
            mb.box((sx*1.31,sy*1.01,0.455),(0.16,0.16,0.91),{'all':('wood',(0.6,0.0,0.75,0.25),1)},skip=('bottom','top'))
    # long stretchers (front/back) + short stretchers (sides)  -- bottom 0.74 keeps jelly clearance
    for sy in (-1,1):
        mb.box((0,sy*1.01,0.78),(2.46,0.06,0.08),{'all':'sw_woodL','front':('wood',(0.1,0.75,0.9,0.95)),'back':('wood',(0.1,0.75,0.9,0.95))})
    for sx in (-1,1):
        mb.box((sx*1.31,0,0.78),(0.06,1.86,0.08),{'all':'sw_woodL','left':('wood',(0.1,0.5,0.75,0.7)),'right':('wood',(0.1,0.5,0.75,0.7))})
    # corner brackets (45deg braces between leg and stretcher)
    for sx in (-1,1):
        for sy in (-1,1):
            mb.push((sx*1.18,sy*1.01,0.70),0,0,-sx*math.pi/4)
            mb.box((0,0,0),(0.05,0.045,0.17),'sw_woodM')
            mb.pop()
            mb.push((sx*1.31,sy*0.88,0.70),0,sy*math.pi/4,0)
            mb.box((0,0,0),(0.045,0.05,0.17),'sw_woodM')
            mb.pop()
    ob=mb.build("Prop_ProduceTable",c,MAT); stats[ob.name]=mb.tris(); return ob
