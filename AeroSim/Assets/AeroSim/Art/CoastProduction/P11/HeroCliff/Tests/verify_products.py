#!/usr/bin/env python3
"""P11 v2 authenticity/static artifact gate; no Unity installation required."""
import json, os, struct, hashlib, sys
ROOT=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
errors=[]
def check(ok,msg):
    if not ok: errors.append(msg)
def text(path):
    with open(path,encoding="utf-8") as f: return f.read()

manifest_path=os.path.join(ROOT,"Reports","hero-cliff-production-manifest.json")
with open(manifest_path) as f: m=json.load(f)
check(m.get("schema")=="aerosim.hero-cliff-p11/v2","manifest is not P11 v2")
check(m.get("productionReady") is False,"must not claim ProductionReady")
g=m.get("geometry",{}); tri=g.get("triangles",{})
check(tri.get("HC_High",0)>=400000,"high mesh <400k")
check(0<tri.get("HC_LOD0",999999)<=120000,"LOD0 >120k")
check(g.get("highToLod0Reduction",0)>=.65,"high/LOD0 reduction is not significant")
ratios=g.get("lodRatios",{})
check(.50<=ratios.get("LOD1",0)<=.55,"LOD1 ratio outside 50-55%")
check(.20<=ratios.get("LOD2",0)<=.25,"LOD2 ratio outside 20-25%")
check(.075<=ratios.get("LOD3",0)<=.095,"LOD3 ratio outside 7.5-9.5%")
check("continuous manifold" in g.get("lodGeneration","") and "no decimate" in g.get("lodGeneration",""),"LOD generation does not record topology-preserving rebuild")
lod_validation=g.get("lodValidation",{})
for lod in ("HC_LOD0","HC_LOD1","HC_LOD2","HC_LOD3"):
    stats=lod_validation.get(lod,{})
    check(stats.get("mainComponentRatio",0)>.99 and stats.get("componentCount") == 1,lod+" main component <=99%")
    check(stats.get("zeroAreaFaces",1)==0 and stats.get("nonManifoldEdges",1)==0,lod+" has degenerate/non-manifold geometry")
    check(stats.get("boundaryComponents") == 1 and stats.get("boundaryDegreeErrors") == 0,lod+" boundary is inconsistent")
    check(stats.get("frontFacingRatio",0)>=.999,lod+" normals/face orientation inconsistent")
    check(stats.get("occupancy",0)>=.25 and stats.get("silhouetteIoUToLOD0",0)>=.97,lod+" offscreen occupancy/silhouette gate")
check(len(g.get("features",[]))>=8,"geological feature declaration incomplete")
detached=g.get("detachedRockTriangles",{})
check(detached.get("LOD0",0)>=3500 and detached.get("LOD3",99999)<=500,"detached talus LOD budget invalid")
uv=m.get("uv",{}); uvs=uv.get("stats",{})
check(uvs.get("zeroAreaRatio",1)<.005,"zero-area UV ratio >=0.5%")
check(uv.get("paddingPixels",0)>=8,"UV padding contract too small")
check(m.get("collision",{}).get("count",99)<=16 and not m.get("collision",{}).get("meshCollider",True),"collision budget/type")

b=m.get("bakes",{}); maps=b.get("maps",[]); names=[x.get("map") for x in maps]
expected=["Normal","AO","Height","Curvature","BaseColor","Roughness","WetnessCavityMask"]
check(names==expected,"bake map set/order incomplete")
check(b.get("engine")=="CYCLES" and b.get("selectedToActive") is True,"Cycles selected-to-active not recorded")
check(bool(b.get("cageObject")) and b.get("cageExtrusion",0)>0 and b.get("marginPixels",0)>=8,"cage/margin not recorded")
for entry in maps:
    check(entry.get("api")=="bpy.ops.object.bake" and entry.get("selectedToActive") is True and entry.get("useCage") is True,"non-authentic bake record "+str(entry.get("map")))
    check(entry.get("resolution")==[2048,2048] and entry.get("seconds",0)>0,"invalid bake dimensions/time "+str(entry.get("map")))
check(next((x.get("bitDepth") for x in maps if x.get("map")=="Height"),0)==16,"Height is not recorded as 16-bit")

source=text(os.path.join(ROOT,"Source","build_hero_cliff.py"))
for token in ("bpy.ops.object.bake", "use_selected_to_active=True", "use_cage=True", "cage_object=cage", "type=bake_type"):
    check(token in source,"bake source contract missing: "+token)
check("high.hide_render=False" in source,"high-poly bake source is render-hidden")

hashes=[]
for name in expected:
    rel="Textures/T_HeroCliff_P11_"+name+".png"; p=os.path.join(ROOT,rel)
    check(os.path.isfile(p),"missing "+rel)
    if not os.path.isfile(p): continue
    data=open(p,"rb").read(); digest=hashlib.sha256(data).hexdigest(); hashes.append(digest)
    check(data[:8]==b'\x89PNG\r\n\x1a\n',name+" not PNG")
    w,h=struct.unpack(">II",data[16:24]); check((w,h)==(2048,2048),name+" not 2K")
    check(len(data)>100000,name+" suspiciously uniform")
    expected_hash=m.get("files",{}).get(rel); check(digest==expected_hash,"manifest hash mismatch "+rel)
    check(data[24]==(16 if name=="Height" else 8),name+" PNG bit depth invalid")
check(len(set(hashes))==len(expected),"distinct texture maps have duplicate hashes")
for rel,digest in m.get("files",{}).items():
    p=os.path.join(ROOT,rel); check(os.path.isfile(p),"missing "+rel)
    if os.path.isfile(p): check(hashlib.sha256(open(p,"rb").read()).hexdigest()==digest,"hash mismatch "+rel)

shader=text(os.path.join(ROOT,"Shaders","HeroCliffP11.shader"))
check("float2 uv_BaseColor" in shader and "tex2D(_BaseColor,uv)" in shader and "tex2D(_NormalMap,uv)" in shader,"unique UV baked-map shader sampling missing")
check("ValueNoise3D(macroPosition)" in shader and "ValueNoise3D(microPosition" in shader and "DetailVisibility" in shader,"world-space non-periodic macro/micro overlay missing")
check("tri(_BaseColor" not in shader and "tri(_NormalMap" not in shader,"unique bake is incorrectly repeated as triplanar")
importer=text(os.path.abspath(os.path.join(ROOT,"../../../../Editor/HeroCliffP11Importer.cs")))
for prop,suffix in (("_BaseColor","BaseColor"),("_NormalMap","Normal"),("_RoughnessMap","Roughness"),("_AOMap","AO"),("_HeightMap","Height"),("_CurvatureMap","Curvature"),("_WetCavityMask","WetnessCavityMask")):
    check(('Bind(material, "'+prop+'", "'+suffix+'")') in importer,"Unity material binding missing "+prop)
check("TextureImporterType.NormalMap" in importer and "importer.sRGBTexture = baseColor" in importer,"Unity NormalMap/linear import policy missing")
check('Set(importer, "Standalone", 4096' in importer and 'Set(importer, "Android", 2048' in importer and 'Set(importer, "iPhone", 2048' in importer,"platform texture size policy missing")
for name in expected:
    meta=text(os.path.join(ROOT,"Textures","T_HeroCliff_P11_"+name+".png.meta"))
    check("enableMipMap: 1" in meta and "streamingMipmaps: 1" in meta,"mipmap/streaming meta missing "+name)
    check(("sRGBTexture: 1" if name=="BaseColor" else "sRGBTexture: 0") in meta,"sRGB/linear meta mismatch "+name)
    if name=="Normal": check("textureType: 1" in meta,"Normal meta is not NormalMap")

capture_path=os.path.join(ROOT,"Reports","CaptureFrames","manifest.json")
check(os.path.isfile(capture_path),"source capture manifest missing")
if os.path.isfile(capture_path):
    capture=json.load(open(capture_path)); check(capture.get("visualPass") is False,"source capture must not claim visual pass")
    check(capture.get("captureStatus")=="CAPTURED" and capture.get("reviewStatus")=="NOT_REVIEWED" and capture.get("gateStatus")=="GATE_NOT_RUN","capture truth states invalid")
    check(capture.get("fixedRoute")==[1,5,15,40,80] and len(capture.get("frames",[]))==5,"fixed source route incomplete")

code_root=os.path.abspath(os.path.join(ROOT,"../../../../"))
preview=text(os.path.join(code_root,"Runtime","Diagnostics","PreviewCapture.cs")); route=text(os.path.join(code_root,"Runtime","Diagnostics","HeroCliffCaptureRoute.cs")); diagnostic=text(os.path.join(ROOT,"Shaders","HeroCliffDiagnostics.shader"))
check('--capture-hero-cliff' in preview and 'CaptureHeroCliff' in preview,"PreviewCapture CLI route missing")
check('RenderWithShader' in route and 'frames.Count != Distances.Length * ChannelNames.Length' in route,"runtime 5x8 capture verification missing")
check(all(value in route for value in ('CAPTURED','NOT_REVIEWED','GATE_NOT_RUN')),"runtime capture truth states missing")
check('_HeroCliffDiagnosticMode' in diagnostic and all(value in diagnostic for value in ('_RoughnessMap','_AOMap','_WetCavityMask','_MaterialId','_LodColor')),"diagnostic shader channels incomplete")
print(json.dumps({"gate":"PASS" if not errors else "FAIL","errors":errors,"triangles":tri,"uvZeroAreaRatio":uvs.get("zeroAreaRatio"),"bakeCount":len(maps)},indent=2))
sys.exit(1 if errors else 0)
