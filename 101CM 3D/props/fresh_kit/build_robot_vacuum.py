
def robot_vacuum(name="Prop_RobotVacuum"):
    # round robot vacuum (no mop pad). Matches hazard graybox: body dia 1.40, height ~0.40. Front = Blender -Y (= Unity +Z)
    from mathutils import Vector
    c=get_coll(name); mb=MB()
    Z=(0,0,1); X=(1,0,0); Y=(0,1,0)
    BODY='sw_metalD'; TOP='sw_white'; DARK='sw_black'; BLUE='sw_blue'
    # body shell (rounded bottom + rounded top edge)
    lathe(mb,(0,0,0),Z,X,Y,[(0.0,0.018),(0.60,0.018),(0.665,0.035),(0.70,0.085),(0.70,0.30),(0.69,0.345),(0.665,0.365)],32,BODY,smooth=True)
    # cream/white top plate, slightly raised with a soft edge
    lathe(mb,(0,0,0),Z,X,Y,[(0.665,0.365),(0.645,0.385),(0.60,0.392),(0.0,0.392)],32,TOP,smooth=True)
    # front bumper sensor strip (dark band on the front arc)
    segs=10; a0=math.radians(-150); a1=math.radians(-30); r=0.705
    for i in range(segs):
        t0=a0+(a1-a0)*i/segs; t1=a0+(a1-a0)*(i+1)/segs
        p=lambda t,z:(r*math.cos(t),r*math.sin(t),z)
        u0,v0,u1,v1=reg_uv(DARK,(0.3,0.3,0.7,0.7)); cu=((u0+u1)/2,(v0+v1)/2)
        mb.face([p(t0,0.17),p(t1,0.17),p(t1,0.215),p(t0,0.215)],[cu]*4,smooth=True)
    # LiDAR turret (blue band + white cap), set a little toward the back
    ty=0.10
    lathe(mb,(0,ty,0),Z,X,Y,[(0.20,0.392),(0.205,0.40),(0.205,0.465)],20,BODY,smooth=True)
    lathe(mb,(0,ty,0),Z,X,Y,[(0.205,0.465),(0.205,0.505)],20,BLUE,smooth=True)
    lathe(mb,(0,ty,0),Z,X,Y,[(0.205,0.505),(0.20,0.53),(0.185,0.548),(0.12,0.556),(0.0,0.556)],20,TOP,smooth=True)
    # control button: dark pill with a blue light, toward the front
    mb.push((0,-0.42,0.392))
    lathe(mb,(0,0,0),Z,X,Y,[(0.075,0.0),(0.075,0.012),(0.07,0.018),(0.0,0.018)],14,DARK,smooth=True)
    lathe(mb,(0,0,0),Z,X,Y,[(0.032,0.018),(0.032,0.024),(0.0,0.026)],10,BLUE,smooth=True)
    mb.pop()
    ob=mb.build(name,c,MAT); stats[ob.name]=mb.tris(); return ob
