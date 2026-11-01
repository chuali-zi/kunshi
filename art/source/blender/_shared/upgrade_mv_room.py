"""Run through Blender MCP. Preserve gameplay names, sockets and local origins."""
import bpy, sys, math, json, shutil
from pathlib import Path
from mathutils import Vector
ROOT=Path('D:/game/con_game')
sys.path.insert(0,str(ROOT/'art/source/blender/_shared'))
from ak import box, cyl, sphere, assign, join_into, export_fbx
from build_b08_apply import rebuild_material, cube_uv, TEXMAP
TEXMAP['MAT_Floor_DarkGreen_Rug']=('TEX_MAT_Fabric_Rug',0,.96)
REPORT=[]

def merge_detail(target, parts):
    if not parts: return
    bpy.ops.object.select_all(action='DESELECT')
    target.select_set(True)
    for o in parts: o.select_set(True)
    bpy.context.view_layer.objects.active=target
    bpy.ops.object.join()

def armchair():
    body=bpy.data.objects['Armchair_Body']
    leather=bpy.data.materials['MAT_Leather_Burgundy']
    # Maintain original envelope and cushion. Sculpt a padded diamond back.
    parts=[]
    nx,nz=32,32
    verts=[]; faces=[]
    for j in range(nz+1):
        z=.58+j/nz*.46
        for i in range(nx+1):
            x=-.30+i/nx*.60
            u=(x+.30)/.15; v=(z-.58)/.115
            diamond=abs(math.sin(math.pi*(u+v)) * math.sin(math.pi*(u-v)))
            yy=.177-.024*diamond+(z-.58)*.105
            verts.append((x,yy,z))
    for j in range(nz):
        for i in range(nx):
            # The chair faces toward -Y.  Keep the quilted surface front-facing
            # when Unity imports with back-face culling enabled.
            a=j*(nx+1)+i; faces.append((a,a+1,a+nx+2,a+nx+1))
    me=bpy.data.meshes.new('Tufted_back');me.from_pydata(verts,[],faces);me.update()
    ob=bpy.data.objects.new('Tufted_back',me);bpy.context.collection.objects.link(ob);assign(ob,leather)
    for p in me.polygons:p.use_smooth=True
    parts.append(ob)
    for row in range(4):
        for col in range(4):
            x=-.225+col*.15+(row%2)*.035
            z=.635+row*.11
            parts.append(sphere('Leather_button',.009,(x,.173+(z-.58)*.105,z),scale=(1,.5,1),seg=12,rings=8,mat=leather))
    for s in (-1,1):
        wing=box('Back_wing',.12,.25,.43,(s*.355,.20,.9),bev=.047,mat=leather)
        wing.rotation_euler.z=s*.14
        parts.append(wing)
        parts.append(cyl('Front_piping',.006,.53,(s*.365,-.337,.365),rot=(0,0,0),verts=10,mat=leather))
    merge_detail(body,parts)

def bookshelf():
    body=bpy.data.objects['Bookcase_Body'];root=body.parent
    wood=bpy.data.materials['MAT_Wood_Walnut'];parts=[]
    for z,w,dep,h in [(2.1,1.06,.39,.045),(2.14,1.1,.41,.035),(.045,1.04,.39,.07)]:
        parts.append(box('Cornice',w,dep,h,(0,0,z),bev=.006,mat=wood))
    for s in (-1,1):
        parts.append(box('Pilaster',.045,.045,2.0,(s*.464,-.173,1.06),bev=.006,mat=wood))
    # Decorative books on shelves other than the interactable second shelf.
    colors=[(.13,.20,.17),(.22,.085,.055),(.30,.24,.14),(.10,.135,.18)]
    from ak import make_mat
    mats=[make_mat('MAT_MV_Book_'+str(i),c,rough=.83) for i,c in enumerate(colors)]
    import random
    rnd=random.Random(73)
    for level in [0,2,3,4]:
        z=.075+.36*level;x=-.40
        while x<.40:
            w=rnd.uniform(.025,.058);h=rnd.uniform(.20,.30)
            parts.append(box('Shelf_book',w,.18,h,(x,-.015,z+h/2),mat=rnd.choice(mats)))
            # restrained spine bands, using existing bedding material for paper
            x+=w+.008
    merge_detail(body,parts)

def clock():
    bodies=[o for o in bpy.data.objects if o.type=='MESH' and 'Clock_Body' in o.name]
    if not bodies:return
    body=bodies[0]
    # Dial and index marks stay attached to the breakable body.
    from ak import make_mat
    ivory=make_mat('MAT_MV_ClockFace',(.77,.74,.65),rough=.85)
    dark=make_mat('MAT_MV_Ink',(.025,.025,.02),rough=.7)
    bounds=[body.matrix_world@Vector(c) for c in body.bound_box]
    cx=(min(v.x for v in bounds)+max(v.x for v in bounds))/2
    cz=(min(v.z for v in bounds)+max(v.z for v in bounds))/2
    rad=min(max(v.x for v in bounds)-min(v.x for v in bounds),max(v.z for v in bounds)-min(v.z for v in bounds))*.43
    yy=min(v.y for v in bounds)-.003
    parts=[cyl('Clock_ivory_dial',rad,.004,(cx,yy,cz),rot=(math.pi/2,0,0),verts=64,mat=ivory)]
    for n in range(60):
        a=n*math.tau/60
        r=rad*.89
        o=box('Dial_tick',.007 if n%5==0 else .003,.003,.022 if n%5==0 else .009,(cx+math.sin(a)*r,yy-.004,cz+math.cos(a)*r),mat=dark)
        o.rotation_euler.y=a;parts.append(o)
    merge_detail(body,parts)

sources=[]
for category in ['environment','props']:
    sources.extend((category,p) for p in sorted((ROOT/'art/source/blender'/category).glob('*_v002.blend')))
for category,path in sources:
    aid=path.stem.removesuffix('_v002')
    from ak import clean_scene
    clean_scene()
    bpy.ops.outliner.orphans_purge(do_recursive=True)
    for img in list(bpy.data.images):
        if img.users==0:bpy.data.images.remove(img)
    with bpy.data.libraries.load(str(path),link=False) as (src,dst):
        dst.objects=list(src.objects)
    for o in dst.objects:
        if o:bpy.context.scene.collection.objects.link(o)
    bpy.context.view_layer.update()
    original_names={o.name for o in bpy.data.objects}
    oldtri=sum(len(p.vertices)-2 for o in bpy.data.objects if o.type=='MESH' for p in o.data.polygons)
    if aid=='FUR_Armchair':armchair()
    if aid=='FUR_Bookcase':bookshelf()
    if aid in ['PRP_WallClock','PRP_WallClock_Broken']:clock()
    if aid in ['FUR_WoodChair','FUR_WoodChair_Broken']:
        body=bpy.data.objects.get('Chair_Back') or bpy.data.objects.get('FUR_WoodChair')
        if body and body.type=='MESH':
            wood=bpy.data.materials['MAT_Wood_Worn']
            merge_detail(body,[cyl('Chair_spindle',.009,.40,(x,.205,.69),verts=12,mat=wood) for x in [-.13,-.065,0,.065,.13]])
    if aid.startswith('PRP_CeilingLamp'):
        from ak import make_mat
        black=make_mat('MAT_MV_EnamelBlack',(.012,.015,.012),rough=.4)
        black.node_tree.nodes.get('Principled BSDF').inputs['Specular IOR Level'].default_value=.18
        shade_outer=make_mat('MAT_MV_LampShadeOuter',(.30,.31,.27),rough=.78)
        shade_inner=make_mat('MAT_LampShade_Inner',(.80,.75,.68),rough=.4)
        for o in bpy.data.objects:
            if o.type!='MESH':continue
            if o.name=='Lamp_Shade':
                # The joined shade keeps slot 0 for its outer shell and slot 1
                # for the flipped inner reflector. Preserve that face split.
                old_indices=[p.material_index for p in o.data.polygons]
                o.data.materials.clear();o.data.materials.append(shade_outer);o.data.materials.append(shade_inner)
                for p,idx in zip(o.data.polygons,old_indices):p.material_index=0 if idx==0 else 1
            else:
                for i,m in enumerate(o.data.materials):
                    if m and m.name.split('.')[0] in ['MAT_Metal_Dark','MAT_LampShade_Inner']:o.data.materials[i]=black
    if aid=='ENV_RoomShell':
        skirt=bpy.data.objects.get('Shell_Skirting')
        if skirt:
            from ak import make_mat
            skirt.data.materials.clear();assign(skirt,make_mat('MAT_MV_Skirting',(.075,.066,.038),rough=.8))
    for mat in list(bpy.data.materials):
        rebuild_material(mat)
        if mat.use_nodes:
            for n in mat.node_tree.nodes:
                if n.type=='NORMAL_MAP':n.inputs['Strength'].default_value=.22
    # Reload image data: an open Blender session may cache old files.
    for img in bpy.data.images:
        if img.source=='FILE':
            try:img.reload()
            except RuntimeError:pass
    for o in bpy.data.objects:
        if o.type!='MESH':continue
        if aid in ['FUR_Armchair','FUR_Bookcase']:cube_uv(o,2)
        if aid=='ENV_RoomShell' and 'Wall' in o.name:
            uv=o.data.uv_layers.active or o.data.uv_layers.new()
            axis=0 if o.dimensions.x>o.dimensions.y else 1
            # 2.4 m repeat / 8 sprigs = 30 cm floral repeat.
            for poly in o.data.polygons:
                for li in poly.loop_indices:
                    v=o.matrix_world@o.data.vertices[o.data.loops[li].vertex_index].co
                    uv.data[li].uv=(v[axis]/2.4,v.z/2.4)
        if aid=='PRP_Rug':
            uv=o.data.uv_layers.active or o.data.uv_layers.new()
            xs=[v.co.x for v in o.data.vertices];ys=[v.co.y for v in o.data.vertices]
            for poly in o.data.polygons:
                for li in poly.loop_indices:
                    v=o.data.vertices[o.data.loops[li].vertex_index].co
                    uv.data[li].uv=((v.x-min(xs))/(max(xs)-min(xs)),(v.y-min(ys))/(max(ys)-min(ys)))
    dst=path.with_name(aid+'_v003.blend')
    # Appended assets may retain library bookkeeping from earlier lookdev sessions.
    for lib in list(bpy.data.libraries):
        bpy.data.libraries.remove(lib)
    bpy.ops.wm.save_as_mainfile(filepath=str(dst))
    fbx=ROOT/'art/exports'/category/(aid+'.fbx')
    backup=ROOT/'art/source/pre_mv_20260911'/category/fbx.name
    backup.parent.mkdir(parents=True,exist_ok=True)
    if fbx.exists() and not backup.exists():shutil.copy2(fbx,backup)
    export_fbx(str(fbx))
    unity=ROOT/'Assets/_Game/Art'/category.capitalize()/fbx.name
    shutil.copy2(fbx,unity)
    names={o.name for o in bpy.data.objects}
    tri=sum(len(p.vertices)-2 for o in bpy.data.objects if o.type=='MESH' for p in o.data.polygons)
    REPORT.append({'asset':aid,'triangles_before':oldtri,'triangles_after':tri,'missing_original_objects':sorted(original_names-names),'source':str(dst.relative_to(ROOT))})
out=ROOT/'art/previews/mv_20260911';out.mkdir(parents=True,exist_ok=True)
(out/'asset_report.json').write_text(json.dumps(REPORT,indent=2),encoding='utf-8')
for p in (ROOT/'art/exports/textures').glob('*.png'):
    shutil.copy2(p,ROOT/'Assets/_Game/Art/Textures'/p.name)
print('MV_UPGRADE_DONE',len(REPORT),'assets')
