#!/usr/bin/env python3
"""P11 Hero Cliff v2: deterministic high/low production and real Cycles selected-to-active baking.
Blender 3.4.1: blender -b --factory-startup --python build_hero_cliff.py
"""
import bpy, math, os, json, hashlib, time, random
from mathutils import Vector

SEED=110412
random.seed(SEED)
HERE=os.path.dirname(os.path.abspath(__file__))
ROOT=os.path.dirname(HERE)
MODEL_DIR=os.path.join(ROOT,"Models"); TEX_DIR=os.path.join(ROOT,"Textures"); REPORT_DIR=os.path.join(ROOT,"Reports")
for p in (MODEL_DIR,TEX_DIR,REPORT_DIR): os.makedirs(p,exist_ok=True)
W=H=2048; HEIGHT=42.0; LENGTH=112.0; MARGIN=16; CAGE_EXTRUSION=1.25

# Continuous geological displacement. Camera-facing surface normal is -X.
def n2(a,b): return (math.sin(a*1.73+b*.91+1.7)+.52*math.sin(a*.43-b*2.13+4.1)+.22*math.sin(a*3.27+b*2.71))/1.74

def cliff_x(y,z,high):
    yn=y/HEIGHT; zn=z/LENGTH
    fault_line=z-(23.0+y*.42)
    fault_side=math.tanh(fault_line/1.1)
    # Main silhouette, crown breakup and diagonal fault throw.
    x=5.0+4.5*yn+2.0*math.sin(zn*math.pi*2.05)+.72*math.sin(zn*math.pi*5.5)
    x+=1.15*fault_side
    # Real bedding ledges; fault side offsets the strata phase and position.
    phase=y*1.43+math.sin(z*.105)*1.25+fault_side*1.65
    x-=.72*max(0.0,math.sin(phase))**4
    x+=.18*math.sin(phase*3.0)
    # Recessed erosion cavities and undercut shelves.
    x+=4.8*math.exp(-((y-7.2)/3.0)**2-((z-43.0)/9.2)**2)
    x+=3.1*math.exp(-((y-18.5)/4.0)**2-((z-82.0)/7.0)**2)
    x-=2.2*math.exp(-((y-10.0)/1.0)**2-((z-45.0)/12.0)**2)
    x-=1.55*math.exp(-((y-25.0)/.75)**2-((z-76.0)/15.0)**2)
    # Integrated angular talus/collapse masses at the foot, continuous with cliff.
    for cz,amp,span in ((12,2.1,4.2),(28,1.45,3.4),(56,2.5,5.1),(77,1.8,4.0),(99,2.25,4.6)):
        x-=amp*math.exp(-((y-2.0)/1.75)**2-((z-cz)/span)**2)*(1+.18*math.sin(z*2.1+y*3.4))
    # Fracture channels cut into actual geometry.
    crack=math.exp(-((math.sin(z*.245+y*.49+fault_side*.7))/.105)**2)
    x+=.72*crack*(.2+.8*yn)
    x+=.31*n2(y*.13,z*.075)
    if high:
        # High-only micro fracture and erosion; this is what bakes into LOD0.
        x+=.18*n2(y*.72,z*.46)+.075*math.sin(y*8.3+z*2.7)
        x+=.28*math.exp(-((math.sin(z*.83-y*.36+1.2))/.075)**2)*(.15+.85*yn)
    return x

def make_surface(name,ny,nz,high=False):
    verts=[]; faces=[]; uvs=[]
    for iz in range(nz):
        z=iz*LENGTH/(nz-1)
        for iy in range(ny):
            y=iy*HEIGHT/(ny-1)
            verts.append((cliff_x(y,z,high),y,z-LENGTH*.5))
    for iz in range(nz-1):
        for iy in range(ny-1):
            a=iz*ny+iy; b=a+1; c=a+ny; d=c+1
            faces.extend(((a,c,d),(a,d,b)))
    mesh=bpy.data.meshes.new(name+"_Mesh"); mesh.from_pydata(verts,[],faces); mesh.update()
    obj=bpy.data.objects.new(name,mesh); bpy.context.collection.objects.link(obj)
    # Exact unique atlas UV: no overlap and no zero-area faces, full 0..1 coverage.
    uv=mesh.uv_layers.new(name="UVMap"); li=0
    pad=MARGIN/W; span=1-2*pad
    for iz in range(nz-1):
        u0=pad+span*iz/(nz-1); u1=pad+span*(iz+1)/(nz-1)
        for iy in range(ny-1):
            v0=pad+span*iy/(ny-1); v1=pad+span*(iy+1)/(ny-1)
            for co in ((v0,u0),(v0,u1),(v1,u1),(v0,u0),(v1,u1),(v1,u0)):
                uv.data[li].uv=(co[1],co[0]); li+=1
    for poly in mesh.polygons: poly.use_smooth=True
    obj["geology"]="continuous silhouette,bedding,fault throw,fractures,erosion cavities,undercut ledges,integrated talus"
    obj["seed"]=SEED
    return obj

def tris(obj): return sum(len(p.vertices)-2 for p in obj.data.polygons)

def uv_stats(obj):
    uv=obj.data.uv_layers.active.data; total=zero=0; area=0.0
    for p in obj.data.polygons:
        if len(p.loop_indices)!=3: continue
        a,b,c=[uv[i].uv for i in p.loop_indices]
        signed=abs((b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x))*.5
        total+=1; area+=signed
        if signed<1e-12: zero+=1
    return {"triangles":total,"zeroAreaFaces":zero,"zeroAreaRatio":zero/max(1,total),"summedUvArea":area}

def topology_stats(obj):
    """Reject disconnected shards, degenerates, non-manifold boundaries and flipped faces."""
    mesh=obj.data; edge_faces={}; neighbors=[set() for _ in mesh.polygons]; zero=front=0
    for p in mesh.polygons:
        verts=list(p.vertices)
        if len(verts)!=3: continue
        a,b,c=(mesh.vertices[i].co for i in verts)
        if (b-a).cross(c-a).length_squared<1e-16: zero+=1
        if (b-a).cross(c-a).x<0: front+=1
        for edge in ((verts[0],verts[1]),(verts[1],verts[2]),(verts[2],verts[0])):
            edge_faces.setdefault(tuple(sorted(edge)),[]).append(p.index)
    for linked in edge_faces.values():
        if len(linked)==2:
            neighbors[linked[0]].add(linked[1]); neighbors[linked[1]].add(linked[0])
    remaining=set(range(len(mesh.polygons))); components=[]
    while remaining:
        stack=[remaining.pop()]; size=0
        while stack:
            face=stack.pop(); size+=1
            unseen=neighbors[face].intersection(remaining); remaining.difference_update(unseen); stack.extend(unseen)
        components.append(size)
    boundary=[edge for edge,linked in edge_faces.items() if len(linked)==1]
    boundary_degree={}
    for a,b in boundary:
        boundary_degree[a]=boundary_degree.get(a,0)+1; boundary_degree[b]=boundary_degree.get(b,0)+1
    boundary_vertices=set(boundary_degree); boundary_seen=set(); boundary_components=0
    adjacency={v:set() for v in boundary_vertices}
    for a,b in boundary: adjacency[a].add(b); adjacency[b].add(a)
    for start in boundary_vertices:
        if start in boundary_seen: continue
        boundary_components+=1; stack=[start]; boundary_seen.add(start)
        while stack:
            for nxt in adjacency[stack.pop()]:
                if nxt not in boundary_seen: boundary_seen.add(nxt); stack.append(nxt)
    total=max(1,len(mesh.polygons))
    return {"triangles":len(mesh.polygons),"mainComponentRatio":max(components or [0])/total,
            "componentCount":len(components),"zeroAreaFaces":zero,
            "nonManifoldEdges":sum(len(v)>2 for v in edge_faces.values()),"boundaryEdges":len(boundary),
            "boundaryComponents":boundary_components,"boundaryDegreeErrors":sum(v!=2 for v in boundary_degree.values()),
            "frontFacingRatio":front/total}

def render_silhouette_masks(objects):
    """Front orthographic offscreen render with backface culling enabled."""
    scene=bpy.context.scene; old_engine=scene.render.engine
    old_res=(scene.render.resolution_x,scene.render.resolution_y,scene.render.resolution_percentage)
    old_transparent=scene.render.film_transparent
    states={o:o.hide_render for o in bpy.context.scene.objects}
    camera_data=bpy.data.cameras.new("HC_QA_Camera_Data"); camera=bpy.data.objects.new("HC_QA_Camera",camera_data); scene.collection.objects.link(camera)
    camera.location=(-50,HEIGHT*.5,0); target=Vector((5,HEIGHT*.5,0)); camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
    camera_data.type='ORTHO'; camera_data.clip_end=500; camera_data.ortho_scale=LENGTH*.54; scene.camera=camera
    scene.render.engine='BLENDER_WORKBENCH'; scene.display.shading.light='FLAT'; scene.display.shading.color_type='SINGLE'; scene.display.shading.single_color=(1,1,1); scene.display.shading.show_shadows=False; scene.display.shading.show_cavity=False; scene.render.resolution_x=512; scene.render.resolution_y=256; scene.render.resolution_percentage=100; scene.render.film_transparent=True
    masks=[]
    try:
        for item in bpy.context.scene.objects:
            if item.type=='MESH': item.hide_render=True
        for obj in objects:
            obj.hide_render=False; bpy.context.view_layer.update(); bpy.ops.render.render()
            tmp=os.path.join(REPORT_DIR,".p11_lod_qa_"+obj.name+".png"); bpy.data.images['Render Result'].save_render(tmp,scene=scene)
            rendered=bpy.data.images.load(tmp,check_existing=False); pixels=list(rendered.pixels[:]); bpy.data.images.remove(rendered); os.remove(tmp)
            mask=[pixels[i+3]>.5 for i in range(0,len(pixels),4)]
            masks.append(mask); obj.hide_render=True
    finally:
        scene.render.engine=old_engine
        scene.render.resolution_x,scene.render.resolution_y,scene.render.resolution_percentage=old_res; scene.render.film_transparent=old_transparent
        for item,state in states.items(): item.hide_render=state
        bpy.data.objects.remove(camera,do_unlink=True); bpy.data.cameras.remove(camera_data)
    base=masks[0]; result=[]
    for mask in masks:
        occupied=sum(mask); union=sum(a or b for a,b in zip(base,mask)); intersection=sum(a and b for a,b in zip(base,mask))
        result.append({"occupancy":occupied/max(1,len(mask)),"silhouetteIoUToLOD0":intersection/max(1,union)})
    return result

def image(name,noncolor=True):
    old=bpy.data.images.get(name)
    if old: bpy.data.images.remove(old)
    img=bpy.data.images.new(name,width=W,height=H,alpha=False,float_buffer=False)
    img.colorspace_settings.name='Non-Color' if noncolor else 'sRGB'
    return img

def setup_target_image(material,img):
    nodes=material.node_tree.nodes
    node=nodes.get("BAKE_TARGET") or nodes.new("ShaderNodeTexImage"); node.name="BAKE_TARGET"; node.label="Cycles selected-to-active bake target"
    node.image=img
    for n in nodes: n.select=False
    node.select=True; nodes.active=node

def select_bake(high,low):
    bpy.ops.object.select_all(action='DESELECT'); high.select_set(True); low.select_set(True); bpy.context.view_layer.objects.active=low

def set_high_emit(material,mode):
    nt=material.node_tree; nt.nodes.clear(); out=nt.nodes.new("ShaderNodeOutputMaterial"); emission=nt.nodes.new("ShaderNodeEmission")
    geo=nt.nodes.new("ShaderNodeNewGeometry"); sep=nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(geo.outputs['Position'],sep.inputs[0])
    if mode=="Height":
        ramp=nt.nodes.new("ShaderNodeMapRange"); ramp.inputs['From Min'].default_value=0; ramp.inputs['From Max'].default_value=HEIGHT
        nt.links.new(sep.outputs['Y'],ramp.inputs['Value']); nt.links.new(ramp.outputs['Result'],emission.inputs['Color'])
    elif mode=="Curvature":
        ramp=nt.nodes.new("ShaderNodeValToRGB"); ramp.color_ramp.elements[0].position=.22; ramp.color_ramp.elements[1].position=.78
        nt.links.new(geo.outputs['Pointiness'],ramp.inputs[0]); nt.links.new(ramp.outputs[0],emission.inputs['Color'])
    else:
        noise=nt.nodes.new("ShaderNodeTexNoise"); noise.inputs['Scale'].default_value=3.2; noise.inputs['Detail'].default_value=5; noise.inputs['Roughness'].default_value=.72
        nt.links.new(geo.outputs['Position'],noise.inputs['Vector'])
        ramp=nt.nodes.new("ShaderNodeValToRGB")
        if mode=="BaseColor":
            ramp.color_ramp.elements[0].color=(.055,.045,.035,1); ramp.color_ramp.elements[1].color=(.38,.33,.26,1)
            nt.links.new(noise.outputs['Fac'],ramp.inputs[0]); nt.links.new(ramp.outputs[0],emission.inputs['Color'])
        elif mode=="Roughness":
            ramp.color_ramp.elements[0].color=(.58,.58,.58,1); ramp.color_ramp.elements[1].color=(.96,.96,.96,1)
            nt.links.new(noise.outputs['Fac'],ramp.inputs[0]); nt.links.new(ramp.outputs[0],emission.inputs['Color'])
        else: # Wetness/Cavity is geometry-facing: low height + pointiness + noise.
            height=nt.nodes.new("ShaderNodeMapRange"); height.inputs['From Min'].default_value=0; height.inputs['From Max'].default_value=HEIGHT; height.inputs['To Min'].default_value=1; height.inputs['To Max'].default_value=0
            nt.links.new(sep.outputs['Y'],height.inputs['Value'])
            combine=nt.nodes.new("ShaderNodeCombineRGB"); nt.links.new(height.outputs['Result'],combine.inputs['R']); nt.links.new(geo.outputs['Pointiness'],combine.inputs['G']); nt.links.new(noise.outputs['Fac'],combine.inputs['B'])
            nt.links.new(combine.outputs[0],emission.inputs['Color'])
    emission.inputs['Strength'].default_value=1; nt.links.new(emission.outputs[0],out.inputs[0])

def save_image(img,path,depth):
    img.filepath_raw=path; img.file_format='PNG'; bpy.context.scene.render.image_settings.file_format='PNG'; bpy.context.scene.render.image_settings.color_mode='RGB'; bpy.context.scene.render.image_settings.color_depth=str(depth); bpy.context.scene.render.image_settings.compression=35
    img.save_render(path,scene=bpy.context.scene)

# Clean scene and generate true high/low surfaces.
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
high=make_surface("HC_High",401,601,True)          # 480k actual triangles
lod0=make_surface("HC_LOD0",181,257,False)        # 92,160 continuous triangles
# Lower LODs are rebuilt as lower-resolution instances of the same manifold height field.
# Decimate-collapse is intentionally not used: on this long open grid it produced disconnected shards.
lod1=make_surface("HC_LOD1",130,193,False)        # 49,536 / 53.75%
lod2=make_surface("HC_LOD2",78,143,False)         # 21,868 / 23.73%
lod3=make_surface("HC_LOD3",43,93,False)          # 7,728 / 8.39%
high.hide_render=False # Cycles selected-to-active source must remain renderable during bake.

# Explicit cage, expanded toward camera along low-poly normals.
cage=lod0.copy(); cage.data=lod0.data.copy(); bpy.context.collection.objects.link(cage); cage.name="HC_BakeCage"
for vertex in cage.data.vertices: vertex.co.x-=CAGE_EXTRUSION
cage.hide_render=True; cage.display_type='WIRE'

# Bake source/target materials.
high_mat=bpy.data.materials.new("M_HeroCliff_HighBakeSource"); high_mat.use_nodes=True
low_mat=bpy.data.materials.new("M_HeroCliff_P11_Baked"); low_mat.use_nodes=True
for obj in (high,): obj.data.materials.append(high_mat)
for obj in (lod0,lod1,lod2,lod3): obj.data.materials.append(low_mat)

scene=bpy.context.scene; scene.render.engine='CYCLES'; scene.cycles.samples=8; scene.cycles.use_denoising=False
scene.render.bake.use_selected_to_active=True; scene.render.bake.use_cage=True; scene.render.bake.cage_object=cage; scene.render.bake.cage_extrusion=CAGE_EXTRUSION; scene.render.bake.margin=MARGIN; scene.render.bake.use_clear=True
scene.render.bake.normal_space='TANGENT'; scene.view_settings.view_transform='Standard'; scene.view_settings.look='Medium High Contrast'; scene.view_settings.exposure=0; scene.view_settings.gamma=1

bake_log=[]; images={}; bake_names=("Normal","AO","Height","Curvature","BaseColor","Roughness","WetnessCavityMask")
def bake_map(name,bake_type,depth=8,emit_mode=None,noncolor=True):
    img=image("T_HeroCliff_P11_"+name,noncolor); setup_target_image(low_mat,img)
    if emit_mode: set_high_emit(high_mat,emit_mode)
    select_bake(high,lod0); started=time.time(); bpy.ops.object.bake(type=bake_type)
    elapsed=time.time()-started; path=os.path.join(TEX_DIR,"T_HeroCliff_P11_"+name+".png"); save_image(img,path,depth)
    images[name]=img; bake_log.append({"map":name,"api":"bpy.ops.object.bake","type":bake_type,"selectedToActive":True,"useCage":True,"cageObject":cage.name,"cageExtrusion":CAGE_EXTRUSION,"margin":MARGIN,"resolution":[W,H],"bitDepth":depth,"seconds":round(elapsed,3)})
    print("P11_BAKE",name,round(elapsed,2),"seconds")

# LOD-only rebuilds preserve the accepted LOD0 bakes byte-for-byte. Set P11_REBAKE=1 for a full source rebuild.
texture_paths={name:os.path.join(TEX_DIR,"T_HeroCliff_P11_"+name+".png") for name in bake_names}
preserve_bakes=os.environ.get("P11_REBAKE","0")!="1" and all(os.path.isfile(path) for path in texture_paths.values())
prior_manifest_path=os.path.join(REPORT_DIR,"hero-cliff-production-manifest.json")
if preserve_bakes:
    prior=json.load(open(prior_manifest_path)) if os.path.isfile(prior_manifest_path) else {}
    bake_log=prior.get("bakes",{}).get("maps",[])
    if [entry.get("map") for entry in bake_log]!=list(bake_names): raise RuntimeError("Existing bake manifest is incomplete; use P11_REBAKE=1")
    for name,path in texture_paths.items():
        img=bpy.data.images.load(path,check_existing=False); img.name="T_HeroCliff_P11_"+name; img.colorspace_settings.name='sRGB' if name=="BaseColor" else 'Non-Color'; images[name]=img
    print("P11_BAKES_PRESERVED=TRUE")
else:
    bake_map("Normal","NORMAL",8,None,True)
    bake_map("AO","AO",8,None,True)
    bake_map("Height","EMIT",16,"Height",True)
    bake_map("Curvature","EMIT",8,"Curvature",True)
    bake_map("BaseColor","EMIT",8,"BaseColor",False)
    bake_map("Roughness","EMIT",8,"Roughness",True)
    bake_map("WetnessCavityMask","EMIT",8,"WetnessCavityMask",True)

# Verifiable final Blender material graph: all baked textures connected; unique UV images are not tiled.
nt=low_mat.node_tree; nt.nodes.clear(); out=nt.nodes.new("ShaderNodeOutputMaterial"); bsdf=nt.nodes.new("ShaderNodeBsdfPrincipled")
def tex_node(key,label,noncolor=True):
    n=nt.nodes.new("ShaderNodeTexImage"); n.name="BAKED_"+key.upper(); n.label=label; n.image=images[key]; n.extension='EXTEND'; n.interpolation='Linear'; return n
base=tex_node("BaseColor","Unique UV BaseColor",False); normal=tex_node("Normal","High to low tangent normal"); rough=tex_node("Roughness","Geometry-linked roughness"); ao=tex_node("AO","High-poly Cycles AO"); height=tex_node("Height","16-bit high geometry height"); curv=tex_node("Curvature","High geometry pointiness"); wet=tex_node("WetnessCavityMask","R wetness / G cavity-pointiness / B breakup")
normal_map=nt.nodes.new("ShaderNodeNormalMap"); bump=nt.nodes.new("ShaderNodeBump"); bump.inputs['Strength'].default_value=.18; bump.inputs['Distance'].default_value=.08
mix=nt.nodes.new("ShaderNodeMixRGB"); mix.blend_type='MULTIPLY'; mix.inputs[0].default_value=.32
nt.links.new(base.outputs['Color'],mix.inputs[1]); nt.links.new(ao.outputs['Color'],mix.inputs[2]); nt.links.new(mix.outputs[0],bsdf.inputs['Base Color'])
nt.links.new(normal.outputs['Color'],normal_map.inputs['Color']); nt.links.new(normal_map.outputs['Normal'],bump.inputs['Normal']); nt.links.new(height.outputs['Color'],bump.inputs['Height']); nt.links.new(bump.outputs['Normal'],bsdf.inputs['Normal']); nt.links.new(rough.outputs['Color'],bsdf.inputs['Roughness']); nt.links.new(bsdf.outputs[0],out.inputs[0])
low_mat["uniqueUvBakes"]="BaseColor,Normal,Roughness,AO,Height,Curvature,WetnessCavityMask"; low_mat["wetnessMaskNode"]=wet.name; low_mat["curvatureNode"]=curv.name

# Compound primitive collision proxies; never exported as visible meshes in Unity prefab.
proxies=[]
for i,(x,y,z,sx,sy,sz) in enumerate([
 (5,8,-45,10,16,20),(6,22,-44,12,16,22),(7,35,-43,14,12,22),(4,7,-22,13,14,22),(6,20,-20,14,16,24),
 (8,34,-18,15,13,24),(3,6,2,14,12,22),(6,19,3,15,16,24),(8,33,5,16,14,25),(2,6,27,14,12,22),
 (5,19,29,14,16,22),(8,33,31,16,14,24),(-1,2,-38,10,5,18),(-2,2,-5,11,5,20),(-2,2,28,10,5,20)]):
    bpy.ops.mesh.primitive_cube_add(size=1,location=(x,y,z)); o=bpy.context.object; o.name="HC_COL_BOX_%02d"%i; o.dimensions=(sx,sy,sz); bpy.ops.object.transform_apply(location=False,rotation=False,scale=True); o.hide_render=True; o.display_type='WIRE'; proxies.append(o)

uv=uv_stats(lod0); tri={o.name:tris(o) for o in (high,lod0,lod1,lod2,lod3)}
lod_objects=(lod0,lod1,lod2,lod3); topology={o.name:topology_stats(o) for o in lod_objects}; silhouettes=render_silhouette_masks(lod_objects)
for obj,render_stats in zip(lod_objects,silhouettes):
    stats=topology[obj.name]; stats.update(render_stats)
    if stats["mainComponentRatio"]<=.99 or stats["componentCount"]!=1: raise RuntimeError(obj.name+" disconnected topology: "+json.dumps(stats))
    if stats["zeroAreaFaces"] or stats["nonManifoldEdges"] or stats["boundaryComponents"]!=1 or stats["boundaryDegreeErrors"]: raise RuntimeError(obj.name+" invalid faces/boundary: "+json.dumps(stats))
    if stats["frontFacingRatio"]<.999: raise RuntimeError(obj.name+" inconsistent face orientation: "+json.dumps(stats))
    if stats["occupancy"]<.25 or stats["silhouetteIoUToLOD0"]<.97: raise RuntimeError(obj.name+" offscreen silhouette gate: "+json.dumps(stats))
print("P11_LOD_QA="+json.dumps(topology,sort_keys=True))
# Save reproducible source after all images, bake settings and LOD QA evidence are linked.
blend_path=os.path.join(MODEL_DIR,"HeroCliff_P11_Source.blend"); bpy.ops.wm.save_as_mainfile(filepath=blend_path)
# FBX runtime payload excludes high and cage.
bpy.ops.object.select_all(action='DESELECT')
for o in (lod0,lod1,lod2,lod3,*proxies): o.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(MODEL_DIR,"HeroCliff_P11.fbx"),use_selection=True,apply_unit_scale=True,add_leaf_bones=False,bake_anim=False,mesh_smooth_type='FACE',axis_forward='-Z',axis_up='Y')

manifest={
 "schema":"aerosim.hero-cliff-p11/v2","seed":SEED,"blender":bpy.app.version_string,"status":"GENERATED_NOT_REVIEWED","productionReady":False,
 "geometry":{"features":["continuous silhouette","physical bedding ledges","diagonal fault throw","geometry fracture channels","erosion cavities","undercut rock ledges","integrated talus/collapse"],"lodGeneration":"independent lower-resolution continuous manifold height fields; no decimate collapse","triangles":tri,"highToLod0Reduction":1-tri["HC_LOD0"]/tri["HC_High"],"lodRatios":{"LOD1":tri["HC_LOD1"]/tri["HC_LOD0"],"LOD2":tri["HC_LOD2"]/tri["HC_LOD0"],"LOD3":tri["HC_LOD3"]/tri["HC_LOD0"]},"lodValidation":topology},
 "uv":{"layout":"unique non-overlapping grid atlas","paddingPixels":MARGIN,"stats":uv},
 "bakes":{"engine":"CYCLES","device":"CPU","samples":scene.cycles.samples,"selectedToActive":True,"cageObject":cage.name,"cageExtrusion":CAGE_EXTRUSION,"rayDistance":CAGE_EXTRUSION,"marginPixels":MARGIN,"maps":bake_log,"heightBitDepth":16},
 "material":{"name":low_mat.name,"uniqueUvSampling":"EXTEND","connectedMaps":["BaseColor","Normal","Roughness","AO","Height"],"geometryLinkedAuxiliary":["Curvature","WetnessCavityMask"],"wetnessChannels":"R=low-height wetness,G=high-poly pointiness/cavity,B=geometry noise breakup"},
 "collision":{"type":"compound primitive boxes","count":len(proxies),"meshCollider":False},"files":{}
}
paths=[blend_path,os.path.join(MODEL_DIR,"HeroCliff_P11.fbx")]+[os.path.join(TEX_DIR,"T_HeroCliff_P11_"+n+".png") for n in images]
for path in paths:
    with open(path,'rb') as f: manifest["files"][os.path.relpath(path,ROOT).replace('\\','/')]=hashlib.sha256(f.read()).hexdigest()
with open(os.path.join(REPORT_DIR,"hero-cliff-production-manifest.json"),'w') as f: json.dump(manifest,f,indent=2,sort_keys=True)
print("P11_V2_REPORT="+json.dumps(manifest,sort_keys=True))
