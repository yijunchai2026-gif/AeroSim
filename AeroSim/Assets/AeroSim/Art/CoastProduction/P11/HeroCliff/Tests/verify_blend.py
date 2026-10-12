import bpy, json, os, sys
from mathutils import Vector

root=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
bpy.ops.wm.open_mainfile(filepath=os.path.join(root,"Models","HeroCliff_P11_Source.blend"))
errors=[]
def tris(o): return sum(len(p.vertices)-2 for p in o.data.polygons)
def need(name):
    o=bpy.data.objects.get(name)
    if o is None: errors.append("missing "+name)
    return o
def uv_zero_ratio(o):
    layer=o.data.uv_layers.active
    if not layer: return 1.0
    zero=total=0
    for p in o.data.polygons:
        if len(p.loop_indices)!=3: continue
        a,b,c=[layer.data[i].uv for i in p.loop_indices]
        area=abs((b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x))*.5
        total+=1; zero+=area<1e-12
    return zero/max(1,total)
def topology_stats(obj):
    mesh=obj.data; edge_faces={}; neighbors=[set() for _ in mesh.polygons]; zero=front=0
    for p in mesh.polygons:
        verts=list(p.vertices); a,b,c=(mesh.vertices[i].co for i in verts)
        cross=(b-a).cross(c-a); zero+=cross.length_squared<1e-16; front+=cross.x<0
        for edge in ((verts[0],verts[1]),(verts[1],verts[2]),(verts[2],verts[0])):
            edge_faces.setdefault(tuple(sorted(edge)),[]).append(p.index)
    for linked in edge_faces.values():
        if len(linked)==2: neighbors[linked[0]].add(linked[1]); neighbors[linked[1]].add(linked[0])
    remaining=set(range(len(mesh.polygons))); components=[]
    while remaining:
        stack=[remaining.pop()]; count=0
        while stack:
            face=stack.pop(); count+=1; unseen=neighbors[face].intersection(remaining); remaining.difference_update(unseen); stack.extend(unseen)
        components.append(count)
    boundary=[edge for edge,linked in edge_faces.items() if len(linked)==1]; adjacency={}
    for a,b in boundary: adjacency.setdefault(a,set()).add(b); adjacency.setdefault(b,set()).add(a)
    seen=set(); boundary_components=0
    for start in adjacency:
        if start in seen: continue
        boundary_components+=1; stack=[start]; seen.add(start)
        while stack:
            for nxt in adjacency[stack.pop()]:
                if nxt not in seen: seen.add(nxt); stack.append(nxt)
    total=max(1,len(mesh.polygons))
    return {"mainComponentRatio":max(components or [0])/total,"componentCount":len(components),"zeroAreaFaces":zero,
            "nonManifoldEdges":sum(len(v)>2 for v in edge_faces.values()),"boundaryEdges":len(boundary),
            "boundaryComponents":boundary_components,"boundaryDegreeErrors":sum(len(v)!=2 for v in adjacency.values()),"frontFacingRatio":front/total}
def render_masks(objects):
    scene=bpy.context.scene; engine=scene.render.engine; resolution=(scene.render.resolution_x,scene.render.resolution_y,scene.render.resolution_percentage); transparent=scene.render.film_transparent
    states={o:o.hide_render for o in scene.objects}; camera_data=bpy.data.cameras.new("QA_Camera_Data"); camera=bpy.data.objects.new("QA_Camera",camera_data); scene.collection.objects.link(camera)
    camera.location=(-50,21,0); camera.rotation_euler=(Vector((5,21,0))-camera.location).to_track_quat('-Z','Y').to_euler(); camera_data.type='ORTHO'; camera_data.clip_end=500; camera_data.ortho_scale=60.48; scene.camera=camera
    scene.render.engine='BLENDER_WORKBENCH'; scene.display.shading.light='FLAT'; scene.display.shading.color_type='SINGLE'; scene.display.shading.single_color=(1,1,1); scene.display.shading.show_shadows=False; scene.display.shading.show_cavity=False; scene.render.resolution_x=512; scene.render.resolution_y=256; scene.render.resolution_percentage=100; scene.render.film_transparent=True; masks=[]
    try:
        for item in scene.objects:
            if item.type=='MESH': item.hide_render=True
        for obj in objects:
            obj.hide_render=False; bpy.context.view_layer.update(); bpy.ops.render.render(); tmp=os.path.join(root,"Reports",".verify_"+obj.name+".png"); bpy.data.images['Render Result'].save_render(tmp,scene=scene); rendered=bpy.data.images.load(tmp,check_existing=False); pixels=list(rendered.pixels[:]); bpy.data.images.remove(rendered); os.remove(tmp); masks.append([pixels[i+3]>.5 for i in range(0,len(pixels),4)]); obj.hide_render=True
    finally:
        scene.render.engine=engine; scene.render.resolution_x,scene.render.resolution_y,scene.render.resolution_percentage=resolution; scene.render.film_transparent=transparent
        for item,state in states.items(): item.hide_render=state
        bpy.data.objects.remove(camera,do_unlink=True); bpy.data.cameras.remove(camera_data)
    base=masks[0]; return [{"occupancy":sum(mask)/len(mask),"silhouetteIoUToLOD0":sum(a and b for a,b in zip(base,mask))/max(1,sum(a or b for a,b in zip(base,mask)))} for mask in masks]

objs=[need("HC_LOD"+str(i)) for i in range(4)]; high=need("HC_High"); cage=need("HC_BakeCage")
cols=[o for o in bpy.data.objects if o.name.startswith("HC_COL_BOX_")]
if not (0<len(cols)<=16): errors.append("collision proxy count")
t=[]; qa={}
if high and all(objs):
    t=[tris(high)]+[tris(o) for o in objs]
    if t[0]<400000 or t[1]>120000 or not (t[0]>t[1]>t[2]>t[3]>t[4]): errors.append("high/LOD triangles")
    if 1-t[1]/t[0]<.65 or not (.50<=t[2]/t[1]<=.55 and .20<=t[3]/t[1]<=.25 and .075<=t[4]/t[1]<=.095): errors.append("LOD reduction ratios")
    if uv_zero_ratio(objs[0])>=.005: errors.append("LOD0 degenerate UV")
    renders=render_masks(objs)
    for obj,render in zip(objs,renders):
        stats=topology_stats(obj); stats.update(render); qa[obj.name]=stats
        if stats["mainComponentRatio"]<=.99 or stats["componentCount"]!=1: errors.append(obj.name+" main connected component")
        if stats["zeroAreaFaces"] or stats["nonManifoldEdges"] or stats["boundaryComponents"]!=1 or stats["boundaryDegreeErrors"]: errors.append(obj.name+" face/boundary topology")
        if stats["frontFacingRatio"]<.999: errors.append(obj.name+" face orientation")
        if stats["occupancy"]<.25 or stats["silhouetteIoUToLOD0"]<.97: errors.append(obj.name+" offscreen silhouette")
if high and not all(k in high.get("geology","") for k in ("bedding","fault","fractures","cavities","talus")): errors.append("feature metadata")
if cage is None or not cage.hide_render: errors.append("bake cage configuration")
mat=bpy.data.materials.get("M_HeroCliff_P11_Baked"); expected={"BAKED_BASECOLOR","BAKED_NORMAL","BAKED_ROUGHNESS","BAKED_AO","BAKED_HEIGHT","BAKED_CURVATURE","BAKED_WETNESSCAVITYMASK"}; actual={n.name for n in mat.node_tree.nodes} if mat and mat.use_nodes else set()
if not expected.issubset(actual): errors.append("baked material texture connections")
if bpy.context.scene.render.engine!='CYCLES' or not bpy.context.scene.render.bake.use_selected_to_active or not bpy.context.scene.render.bake.use_cage: errors.append("Cycles selected-to-active bake state")
print("P11_BLEND_VALIDATION="+json.dumps({"gate":"PASS" if not errors else "FAIL","errors":errors,"triangles":t,"lodValidation":qa,"lod0UvZeroAreaRatio":uv_zero_ratio(objs[0]) if objs[0] else None,"colliders":len(cols),"cage":cage.name if cage else None},sort_keys=True))
sys.exit(1 if errors else 0)
