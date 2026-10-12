# P11 Hero Cliff v2

确定性生产切片，状态为 **GENERATED_NOT_REVIEWED / NOT 3A QA**；不会自动声明 `ProductionReady`。

## 重建与验证

```bash
blender -b --factory-startup --python Assets/AeroSim/Art/CoastProduction/P11/HeroCliff/Source/build_hero_cliff.py
python3 Assets/AeroSim/Art/CoastProduction/P11/HeroCliff/Tests/verify_products.py
blender -b --factory-startup --python Assets/AeroSim/Art/CoastProduction/P11/HeroCliff/Tests/verify_blend.py
```

Unity 打开工程后由 `HeroCliffP11Importer` 配置模型与纹理并构建 Resources prefab；也可执行菜单 `AeroSim/Coast Production/Rebuild P11 Hero Cliff`。

## v2 几何与烘焙来源

- `HC_High` 是 480,000 三角形连续高模表面；层理台阶、断层错移、裂隙沟槽、侵蚀凹腔/岩檐和崩积均进入几何位移。
- `HC_LOD0` 为 92,160 三角形，拥有非重叠 UV0 图集；High 到 LOD0 减面 80.8%。LOD1/2/3 由 LOD0 轮廓感知减面产生。
- 七张 2048² PNG 均通过 Blender Cycles `bpy.ops.object.bake`、selected-to-active 和显式 `HC_BakeCage` 实际烘焙。Normal/AO 直接由高模投射；Height/Curvature/BaseColor/Roughness/WetnessCavityMask 由高模位置、点锐度及三维几何着色节点以 Emit 投射。
- Height 为 16-bit PNG；其他图为 8-bit。详细 cage、margin、耗时、位深和 SHA-256 记录在 `Reports/hero-cliff-production-manifest.json`。
- Unity shader 只以唯一 UV0 采样上述烘焙图；世界空间三平面仅用于独立、可无缝的宏观和微观补充层，不重复平铺唯一纹理。

## 通道

BaseColor 为 sRGB；Normal、Roughness、AO、Height、Curvature、WetnessCavityMask 为 linear。WetnessCavityMask：R=低位湿润，G=高模 pointiness/cavity，B=三维几何 breakup。

## 真实性边界

自动门禁验证 bake API/selected-to-active/cage 记录、高低模差异、UV 退化率、PNG 分辨率/位深/哈希唯一性、Blender 材质连接、Unity shader 采样策略及导入合同。Unity 6000.3 本机重新导入后的画面、设备性能、最终灯光、运动中 LOD popping、碰撞手感与人工 3A 视觉评审仍需执行；技术门禁通过不等于 3A 通过。
