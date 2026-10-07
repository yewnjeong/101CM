
def cardboard_box(mb, c, size, label=None, yaw=0.0, tape_w=0.13, faces=('front','back')):
    # kraft box: corrugated sides, tape over the top seam running down front/back, optional printed stamp on chosen sides
    x,y,z = c; sx,sy,sz = size
    mb.push((x,y,z), yaw)
    mb.box((0,0,sz/2),(sx,sy,sz),{'top':('kraft',(0,0,1,1),1),'all':'kraft'},skip=('bottom',))
    mb.box((0,0,sz+0.001),(0.012,sy,0.002),'sw_woodD',skip=('bottom','left','right','front','back'))
    mb.box((0,0,sz+0.003),(tape_w,sy+0.006,0.004),'sw_woodL',skip=('bottom',))
    for s in (-1,1):
        mb.box((0,s*(sy/2+0.002),sz-0.09),(tape_w,0.004,0.18),'sw_woodL',skip=('top','bottom'))
    if label:
        lh=min(0.26, sz-0.22); lw=lh*2.0; zc=min(sz*0.45, sz-0.2-lh/2)
        for f in faces:
            if f=='front': mb.quad(( sx*0.25,-sy/2-0.003,zc),( 1,0,0),(0,0,1),lw/2,lh/2,label)
            if f=='back':  mb.quad((-sx*0.25, sy/2+0.003,zc),(-1,0,0),(0,0,1),lw/2,lh/2,label)
            if f=='right': mb.quad(( sx/2+0.003,0,zc),(0, 1,0),(0,0,1),lw/2,lh/2,label)
            if f=='left':  mb.quad((-sx/2-0.003,0,zc),(0,-1,0),(0,0,1),lw/2,lh/2,label)
    mb.pop()

def box_step(name="Prop_BoxStep"):
    # Unity: low step x -1.4..0 (0.5 high), high step x 0..1.4 (1.0 high), z +-0.7. Blender x = -Unity x
    # stamp = our jelly bear (atlas 'bear_print'); low box also on its room-side (Blender +x = Unity -x)
    c=get_coll(name); mb=MB()
    cardboard_box(mb,( 0.70,0,0.0),(1.40,1.40,0.50),None)
    cardboard_box(mb,(-0.70,0,0.0),(1.40,1.40,0.55),None)
    cardboard_box(mb,(-0.71,0.01,0.55),(1.32,1.30,0.45),None,math.radians(4))
    ob=mb.build(name,c,MAT); stats[ob.name]=mb.tris(); return ob
