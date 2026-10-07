
def slat_table(name, L, W, ztop, legx, legy, n=None):
    # flat pine slat table (same look as Prop_ProduceTable), parametric size
    c=get_coll(name); mb=MB()
    sw=0.15; n = n or max(4, int(round((W+0.03)/(sw+0.035)))); g=(W-n*sw)/(n-1); t=0.04
    for i in range(n):
        y=-W/2+sw/2+i*(sw+g); k=i%4
        mb.box((0,y,ztop-t/2),(L,sw,t),{'top':('wood',(0.02*k,0.25*k+0.03,0.55+0.1*k,0.25*k+0.22)),'all':'sw_woodM'})
    for x in sorted(set([-legx[-1]+0.07, 0.0, legx[-1]-0.07])):
        mb.box((x,0,ztop-t-0.035),(0.07,W-0.12,0.07),{'all':'sw_woodL','left':('wood',(0,0.5,0.3,0.72)),'right':('wood',(0,0.5,0.3,0.72))},skip=('top',))
    for x in legx:
        for y in legy:
            mb.box((x,y,(ztop-t)/2),(0.16,0.16,ztop-t),{'all':('wood',(0.6,0.0,0.75,0.25),1)},skip=('bottom','top'))
    span=legx[-1]-legx[0]
    for y in legy:
        mb.box(((legx[0]+legx[-1])/2,y,0.80),(span-0.16,0.06,0.08),{'all':'sw_woodL','front':('wood',(0.1,0.75,0.9,0.95)),'back':('wood',(0.1,0.75,0.9,0.95))})
    for x in legx:
        mb.box((x,0,0.80),(0.06,legy[-1]-legy[0]-0.16,0.08),{'all':'sw_woodL','left':('wood',(0.1,0.5,0.75,0.7)),'right':('wood',(0.1,0.5,0.75,0.7))})
    for sx in (-1,1):
        for y in legy:
            mb.push((sx*(abs(legx[-1])-0.13),y,ztop-t-0.15),0,0,-sx*math.pi/4)
            mb.box((0,0,0),(0.05,0.045,0.17),'sw_woodM')
            mb.pop()
    ob=mb.build(name,c,MAT); stats[ob.name]=mb.tris(); return ob

# slat_table('Prop_WarehouseTable', 2.5, 2.4, 0.97, [-1.10,0.0,1.10], [-1.05,1.05])
