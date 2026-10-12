"""Render deterministic source-review PNG route; never marks visual PASS.
Run: blender -b --factory-startup --python render_capture_route.py
"""
import bpy, os, json, math
from mathutils import Vector
HERE=os.path.dirname(os.path.abspath(__file__)); ROOT=os.path.dirname(HERE)
bpy.ops.wm.open_mainfile(filepath=os.path.join(ROOT,"Models","HeroCliff_P11_Source.blend"))
out=os.path.join(ROOT,"Reports","CaptureFrames"); os.makedirs(out,exist_ok=True)
scene=bpy.context.scene; scene.render.engine='BLENDER_EEVEE'; scene.eevee.use_gtao=True; scene.eevee.gtao_distance=4; scene.eevee.gtao_factor=1.35
scene.render.resolution_x=640; scene.render.resolution_y=360; scene.render.resolution_percentage=100; scene.render.image_settings.file_format='PNG'
scene.world.color=(.07,.09,.11)
bpy.ops.object.light_add(type='SUN', location=(10,30,-20)); sun=bpy.context.object; sun.rotation_euler=(math.radians(38),math.radians(-28),math.radians(-24)); sun.data.energy=3.2
bpy.ops.object.light_add(type='AREA', location=(-8,24,-20)); bpy.context.object.data.energy=800; bpy.context.object.data.size=30
bpy.ops.object.camera_add(); cam=bpy.context.object; scene.camera=cam
# Hide alternate LODs and proxies: source frames inspect LOD0.
for o in bpy.data.objects:
    if o.name in ('HC_High','HC_LOD1','HC_LOD2','HC_LOD3') or o.name.startswith('HC_COL_'): o.hide_render=True
lod0=bpy.data.objects.get('HC_LOD0'); lod0.hide_render=False
target=Vector((5,18,0)); direction=Vector((.68,-.34,-.65)).normalized()
frames=[]
for d in (1,5,15,40,80):
    cam.location=target-direction*d; cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler(); cam.data.lens=32 if d>=40 else 38
    file='P11_%03dm_FinalColor.png'%d; scene.render.filepath=os.path.join(out,file); bpy.ops.render.render(write_still=True)
    frames.append({'file':file,'distanceMeters':d,'camera':list(cam.location),'target':list(target),'captureStatus':'CAPTURED','reviewStatus':'NOT_REVIEWED'})
manifest={'schema':'aerosim.hero-cliff-source-capture/v1','captureStatus':'CAPTURED','reviewStatus':'NOT_REVIEWED','gateStatus':'GATE_NOT_RUN','visualPass':False,'renderer':'Blender EEVEE source review (not Unity device)','fixedRoute':[1,5,15,40,80],'compositionProtection':'target in central 70%; crown and waterline required at 15/40/80m','frames':frames}
with open(os.path.join(out,'manifest.json'),'w') as f: json.dump(manifest,f,indent=2)
print('P11_CAPTURE='+json.dumps(manifest))
