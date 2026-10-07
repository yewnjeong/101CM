
exec(open(os.path.join(OUT,"kitlib.py"),encoding="utf-8").read())
random.seed(7)
MAT=get_mat()
R90=math.pi/2
stats={}

# ---------- products (front = -Y, base at z=0) ----------
def p_fruitbox(mb, x,y,z, label, fruit, yaw=0.0, w=0.40, d=0.30, h=0.24):
    mb.push((x,y,z),yaw)
    mb.box((0,0,h/2),(w,d,h),{'front':label,'back':label,'left':'kraft','right':'kraft','top':fruit.replace('fr_','fill_')},skip=('bottom',))
    mb.pop()
def p_pack(mb,x,y,z,label,yaw=0.0,w=0.17,d=0.13,h=0.07):
    mb.push((x,y,z),yaw)
    mb.box((0,0,h/2),(w,d,h),{'top':(label,(0,0,1,1),0),'front':(label,(0,0.03,1,0.28)),'all':'sw_white'},skip=('bottom','back'))
    mb.pop()
def p_bag(mb,x,y,z,label,tilt=0.25,yaw=0.0):
    mb.push((x,y,z),yaw,tilt)
    mb.box((0,0,0.13),(0.18,0.05,0.26),{'front':label,'back':'sw_white','top':'sw_white','left':'sw_white','right':'sw_white'},skip=('bottom',))
    mb.pop()
def p_bottle(mb,x,y,z,label):
    mb.cyl((x,y,z),0.045,0.19,8,label)
    mb.cyl((x,y,z+0.19),0.045,0.05,8,'sw_white',top='sw_white',r_top=0.02)
def p_can(mb,x,y,z,label):
    mb.cyl((x,y,z),0.05,0.11,8,label,top='sw_metalL')
def p_price(mb,x,y,z,label,w=0.13,h=0.095):
    mb.quad((x,y,z),(1,0,0),(0,0,1),w/2,h/2,label)

# ---------- 1. produce table ----------
def produce_table():
    c=get_coll("Prop_ProduceTable"); mb=MB()
    mb.box((0,0,0.90),(2.8,2.2,0.10),{'top':'wood','bottom':'sw_woodD','all':'sw_woodM'})
    for sy in (-1,1):
        mb.box((0,sy*1.07,0.80),(2.70,0.03,0.10),{'front':('gingham',(0,0,0.9,1)),'back':('gingham',(0,0,0.9,1)),'all':'sw_greenD'},skip=('top',))
    for sx in (-1,1):
        mb.box((sx*1.37,0,0.80),(0.03,2.08,0.10),{'left':('gingham',(0,0,0.8,1)),'right':('gingham',(0,0,0.8,1)),'all':'sw_greenD'},skip=('top',))
    for sx in (-1,1):
        for sy in (-1,1):
            mb.box((sx*1.31,sy*1.01,0.40),(0.14,0.14,0.80),{'all':'sw_metalD','bottom':None},skip=('top','bottom'))
    # hanging price cards on both long sides
    p_price(mb,0.55,-1.090,0.76,'price_0',0.22,0.165)
    mb.push((0,0,0),math.pi); p_price(mb,-0.55,-1.090,0.76,'price_3',0.22,0.165); mb.pop()
    ob=mb.build("Prop_ProduceTable",c,MAT); stats[ob.name]=mb.tris(); return ob

# ---------- 2. fruit hill (two tiers) ----------
def fruit_hill():
    c=get_coll("Prop_FruitHill"); mb=MB()
    BX=["box_apple","box_orange","box_banana","box_grape"]
    def tier(cx,cy,z0,sx,sy,h,nx,ny,k0):
        mb.box((cx,cy,z0+h-0.005),(sx,sy,0.01),{'top':'turf'},skip=('bottom','front','back','left','right'))
        regs=lambda n,k:[BX[(k+i)%4] for i in range(n)]
        mb.segwall((cx,cy-sy/2,z0+h/2),(1,0,0),(0,0,1),sx,h,regs(nx,k0),0.05)
        mb.segwall((cx,cy+sy/2,z0+h/2),(-1,0,0),(0,0,1),sx,h,regs(nx,k0+1),0.05)
        mb.segwall((cx+sx/2,cy,z0+h/2),(0,1,0),(0,0,1),sy,h,regs(ny,k0+2),0.05)
        mb.segwall((cx-sx/2,cy,z0+h/2),(0,-1,0),(0,0,1),sy,h,regs(ny,k0+3),0.05)
        # wood rim
        t=0.05
        for sgn in (-1,1):
            mb.box((cx,cy+sgn*(sy/2-t/2),z0+h+0.015),(sx+0.02,t,0.03),'sw_woodD',skip=('bottom',))
            mb.box((cx+sgn*(sx/2-t/2),cy,z0+h+0.015),(t,sy-2*t,0.03),'sw_woodD',skip=('bottom',))
    tier(0,0,0,3.5,5.7,0.5,4,6,0)
    tier(-0.05,0,0.5,1.4,2.9,0.5,2,3,1)
    # price stakes at two front corners of lower tier
    for sx,lab in ((-1.45,'price_1'),(1.45,'price_6')):
        mb.box((sx,-2.6,0.6),(0.02,0.02,0.2),'sw_metalD',skip=('bottom','top'))
        p_price(mb,sx,-2.611,0.74,lab,0.2,0.15)
        mb.push((sx,-2.6,0),math.pi); p_price(mb,0,-0.011,0.74,lab,0.2,0.15); mb.pop()
    ob=mb.build("Prop_FruitHill",c,MAT); stats[ob.name]=mb.tris(); return ob

# ---------- 3. volley fruit stand (front = roll-out side) ----------
def fruit_stand():
    c=get_coll("Prop_FruitStand"); mb=MB()
    L=2.9; D=1.6
    # body to 1.0
    mb.box((0,0,0.5),(L,D,1.0),{'top':'sw_woodD','back':'sw_woodM','all':'sw_woodM'},skip=('bottom',))
    mb.segwall((0,-D/2-0.004,0.5),(1,0,0),(0,0,1),L,1.0,['box_orange','box_apple','box_orange'],0.08)
    mb.segwall((L/2+0.004,0,0.5),(0,1,0),(0,0,1),D,1.0,['box_grape','box_banana'],0.08)
    mb.segwall((-L/2-0.004,0,0.5),(0,-1,0),(0,0,1),D,1.0,['box_banana','box_grape'],0.08)
    # bin rails
    mb.box((0,-D/2+0.03,1.07),(L,0.06,0.14),{'front':('wood',(0,0,0.6,0.25)),'all':'sw_woodD'})
    mb.box((0,D/2-0.03,1.25),(L,0.06,0.50),{'all':'sw_woodD','front':('wood',(0,0.25,0.6,0.75))})
    for sx in (-1,1):
        mb.box((sx*(L/2-0.03),0,1.15),(0.06,D-0.12,0.30),{'all':'sw_woodD','left':('wood',(0,0,0.3,0.5)),'right':('wood',(0,0,0.3,0.5))})
    # fruit mound (rising toward the back)
    cols=['fr_orange','fr_peach','fr_apple','fr_orange','fr_tomato']
    rows=[(-0.55,1.12,0.15),(-0.2,1.20,0.16),(0.15,1.30,0.16),(0.48,1.40,0.15)]
    for ry,rz,rr in rows:
        n=6
        for i in range(n):
            x=-L/2+0.25+(L-0.5)*(i+random.uniform(0.25,0.75))/n
            mb.sphere((x,ry+random.uniform(-0.04,0.04),rz),rr*random.uniform(0.85,1.05),random.choice(cols),6,4)
    # awning on the wall side
    mb.push((0,D/2-0.05,1.85),0,0.45)
    mb.box((0,-0.2,0),(L+0.1,0.42,0.02),{'top':'stripe','bottom':'sw_cream','front':'sw_greenD'},skip=('back','left','right'))
    mb.pop()
    for sx in (-1,1):
        mb.box((sx*(L/2-0.02),D/2-0.06,1.6),(0.04,0.04,0.5),'sw_metalD',skip=('bottom','top'))
    p_price(mb,0.0,-D/2-0.012,0.88,'price_2',0.26,0.19)
    ob=mb.build("Prop_FruitStand",c,MAT); stats[ob.name]=mb.tris(); return ob

# ---------- 4. veg case (hiding spot, open front) ----------
def veg_case():
    c=get_coll("Prop_VegCase"); mb=MB()
    W=1.8; L=4.3
    # side panels (x = +-0.825, thickness 0.15, height 0.9)
    for sx in (-1,1):
        mb.box((sx*0.825,0,0.45),(0.15,L,0.9),{'all':'sw_greenD','top':'sw_cream'},skip=('bottom',))
    mb.box((0,L/2-0.075,0.45),(W-0.3,0.15,0.9),{'all':'sw_greenD'},skip=('bottom','top','left','right'))
    # inner darkness
    mb.quad((0,L/2-0.151,0.45),(1,0,0),(0,0,1),(W-0.3)/2,0.45,'sw_brown')
    # outer banners
    mb.quad((W/2+0.004,0,0.52),(0,1,0),(0,0,1),1.9,0.24,'banner')
    mb.quad((-W/2-0.004,0,0.52),(0,-1,0),(0,0,1),1.9,0.24,'banner')
    mb.quad((0,L/2+0.004,0.52),(-1,0,0),(0,0,1),0.8,0.30,('gingham',(0,0,0.9,1)))
    for sx in (-1,1):
        mb.quad((sx*(W/2+0.002),0,0.04),(0,sx,0),(0,0,1),L/2,0.04,'sw_brown')
    # lid
    mb.box((0,0,0.95),(W,L,0.10),{'top':'turf','all':'sw_woodM','bottom':'sw_brown'})
    # front fascia with price card
    mb.box((0,-L/2+0.02,0.84),(W,0.04,0.12),{'front':('wood',(0,0,0.5,0.25)),'all':'sw_woodD'},skip=('top',))
    p_price(mb,0.0,-L/2-0.002,0.84,'price_5',0.16,0.12)
    # veg trays on lid
    vegs=[('fr_cabbage',0.14,0.75),('fr_broccoli',0.12,0.9),('fr_eggplant',0.09,0.8),('fr_carrot',0.07,0.9),('fr_radish',0.08,0.8)]
    for ti,ty in enumerate((-1.45,0.0,1.45)):
        mb.box((0,ty,1.02),(1.5,1.2,0.04),{'all':'sw_woodD','top':'sw_woodL'},skip=('bottom',))
        reg,r0,sz=vegs[ti % len(vegs)]
        reg2=vegs[(ti+3)%len(vegs)]
        for i in range(4):
            for j in range(2):
                v = (reg,r0,sz) if (i+j)%2==0 else reg2
                mb.sphere((-0.52+i*0.35, ty-0.28+j*0.56, 1.04+v[1]*v[2]*0.6), v[1], v[0], 6, 4, v[2])
    ob=mb.build("Prop_VegCase",c,MAT); stats[ob.name]=mb.tris(); return ob

# ---------- 5. wall shelf modules ----------
def shelf_frame(mb):
    mb.box((0,0.235,0.9),(1.5,0.03,1.8),{'front':'sw_cream','top':'sw_cream'},skip=('back','bottom','left','right'))
    for sx in (-1,1):
        mb.box((sx*0.74,0,0.9),(0.02,0.5,1.8),'sw_metalD',skip=('bottom','back'))
    mb.box((0,0.01,0.06),(1.46,0.46,0.12),{'front':'sw_brown','top':'sw_woodM'},skip=('bottom','back','left','right'))
    mb.box((0,-0.215,0.17),(1.46,0.03,0.10),{'front':('wood',(0,0,0.5,0.25)),'top':'sw_woodD','back':'sw_woodD'},skip=('bottom','left','right'))
    for z,dep,yc in ((0.72,0.40,0.03),(1.24,0.34,0.06)):
        mb.box((0,yc,z),(1.46,dep,0.03),{'top':'sw_woodL','front':'sw_woodD','bottom':'sw_metalL'},skip=('back','left','right'))
        mb.box((0,yc-dep/2-0.006,z-0.03),(1.46,0.012,0.05),{'front':'sw_yellow','all':'sw_metalD'},skip=('back','left','right','bottom'))
    mb.push((0,0.235,1.78),0,0.35)
    mb.box((0,-0.17,0),(1.5,0.34,0.02),{'top':'stripe','bottom':'sw_cream','front':'sw_greenD'},skip=('back','left','right'))
    mb.pop()

def shelf_A():
    c=get_coll("Prop_ProduceShelf_A"); mb=MB(); shelf_frame(mb)
    for i,x in enumerate((-0.35,0.15,0.55)):
        p_fruitbox(mb,x-0.05,0.08,0.12,['box_apple','box_banana','box_orange'][i],['fr_apple','fr_banana','fr_orange'][i])
    for i in range(6):
        p_bag(mb,-0.6+i*0.24,-0.12,0.12,'bag_%d'%(i%4),0.28)
    for row,(yy,zz) in enumerate(((-0.10,0.735),(0.08,0.735))):
        for i in range(7):
            p_pack(mb,-0.6+i*0.2,yy,zz,'pack_%d'%((i+row*3)%6))
    for i in range(11):
        p_bottle(mb,-0.65+i*0.13,-0.06,1.255,'bottle_%d'%(i//3%4))
    for i in range(10):
        p_can(mb,-0.6+i*0.135,0.11,1.255,'can_%d'%(i//3%4))
    p_price(mb,-0.45,-0.23,0.69,'price_4'); p_price(mb,0.4,-0.23,0.69,'price_1')
    p_price(mb,-0.3,-0.17,1.21,'price_6'); p_price(mb,0.5,-0.17,1.21,'price_3')
    ob=mb.build("Prop_ProduceShelf_A",c,MAT); stats[ob.name]=mb.tris(); return ob

def shelf_B():
    c=get_coll("Prop_ProduceShelf_B"); mb=MB(); shelf_frame(mb)
    lab=['box_orange','box_grape','box_apple']; fr=['fr_orange','fr_grape','fr_gapple']
    for i,x in enumerate((-0.47,0.0,0.47)):
        p_fruitbox(mb,x,-0.05,0.12,lab[i],fr[i],0.0,0.42,0.32,0.22)
    for row,yy in enumerate((-0.09,0.08)):
        for i in range(11):
            p_can(mb,-0.66+i*0.132,yy,0.735,'can_%d'%((i//3+row)%4))
    for row,(yy,zz) in enumerate(((-0.06,1.255),(0.10,1.255))):
        for i in range(7):
            p_pack(mb,-0.6+i*0.2,yy,zz,'pack_%d'%((i+row*2+2)%8))
    p_price(mb,-0.5,-0.23,0.69,'price_2'); p_price(mb,0.35,-0.23,0.69,'price_7')
    p_price(mb,0.0,-0.17,1.21,'price_0')
    ob=mb.build("Prop_ProduceShelf_B",c,MAT); stats[ob.name]=mb.tris(); return ob

def shelf_end():
    c=get_coll("Prop_ProduceShelf_End"); mb=MB()
    mb.box((0,0,0.95),(0.04,0.52,1.9),{'all':'sw_greenD','right':('gingham',(0,0,1,1),1),'left':('gingham',(0,0,1,1),1),'top':'sw_cream'},skip=('bottom','back'))
    ob=mb.build("Prop_ProduceShelf_End",c,MAT); stats[ob.name]=mb.tris(); return ob

# ---------- 6. basket stack ----------
def basket_stack():
    c=get_coll("Prop_BasketStack"); mb=MB()
    def basket(z, top=False):
        bw,bd,tw,td,h=0.40,0.28,0.48,0.34,0.24
        # 4 tapered sides (outer)
        for k in range(4):
            mb.push((0,0,z),k*R90)
            ww,dd,tww,tdd = (bw,bd,tw,td) if k%2==0 else (bd,bw,td,tw)
            p=[(-ww/2,-dd/2,0),(ww/2,-dd/2,0),(tww/2,-tdd/2,h),(-tww/2,-tdd/2,h)]
            u0,v0,u1,v1=reg_uv('sw_red'); cu=((u0+u1)/2,(v0+v1)/2)
            mb.face(p,[cu]*4)
            if top:
                mb.face(p[::-1],[reg_uv('sw_brown')[:2]]*4)
            mb.pop()
        mb.box((0,0,z+h-0.01),(tw+0.01,td+0.01,0.02),'sw_red',skip=('bottom','top'))
    for i in range(6):
        basket(i*0.05, top=(i==5))
    h=0.24+5*0.05
    for sx in (-1,1):
        mb.box((sx*0.12,0,h+0.012),(0.03,0.36,0.02),'sw_black')
    ob=mb.build("Prop_BasketStack",c,MAT); stats[ob.name]=mb.tris(); return ob

objs=[produce_table(),fruit_hill(),fruit_stand(),veg_case(),shelf_A(),shelf_B(),shelf_end(),basket_stack()]
# lay out for preview
for i,o in enumerate(objs):
    o.location=((i%4)*6.0-9.0, (i//4)*7.0, 0)

def contact(names_views, path, size=480):
    import sys, tempfile
    sys.path.insert(0, os.path.join(tempfile.gettempdir(),"claude_pylib"))
    from PIL import Image
    sc=bpy.context.scene
    w=bpy.data.worlds.get("W") or bpy.data.worlds.new("W"); sc.world=w; w.color=(0.55,0.55,0.58)
    cam=bpy.data.objects.get("PreviewCam")
    if cam is None:
        cd=bpy.data.cameras.new("PreviewCam"); cam=bpy.data.objects.new("PreviewCam",cd); sc.collection.objects.link(cam)
    sc.camera=cam; cam.data.lens=35
    sc.render.engine='BLENDER_WORKBENCH'
    sh=sc.display.shading; sh.light='STUDIO'; sh.color_type='TEXTURE'; sh.show_shadows=True; sh.background_type='WORLD'
    sc.render.resolution_x=size; sc.render.resolution_y=int(size*0.75)
    sc.render.image_settings.file_format='PNG'
    bpy.context.view_layer.update()
    tiles=[]
    for name,(az,el,dist) in names_views:
        ob=bpy.data.objects[name]
        for o in bpy.data.objects:
            if o.type=='MESH': o.hide_render = (o!=ob)
        bb=[ob.matrix_world @ Vector(c) for c in ob.bound_box]
        ctr=sum(bb,Vector())/8
        r=max((v-ctr).length for v in bb)
        d=r*dist
        cam.location=ctr+Vector((d*math.cos(el)*math.sin(az), -d*math.cos(el)*math.cos(az), d*math.sin(el)))
        cam.rotation_euler=(ctr-cam.location).to_track_quat('-Z','Y').to_euler()
        tp=os.path.join(OUT,"_tile.png"); sc.render.filepath=tp
        bpy.ops.render.render(write_still=True)
        tiles.append(Image.open(tp).copy())
    for o in bpy.data.objects:
        if o.type=='MESH': o.hide_render=False
    cols=3; rows=(len(tiles)+cols-1)//cols
    tw,th=tiles[0].size
    sheet=Image.new("RGB",(tw*cols,th*rows),(40,40,40))
    for i,t in enumerate(tiles): sheet.paste(t,((i%cols)*tw,(i//cols)*th))
    sheet.save(path,quality=78)
    return path
