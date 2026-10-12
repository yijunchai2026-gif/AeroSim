# Mini 4 Pro 正式资产接入 Checklist

> 当前状态：**CONTENT_NOT_DELIVERED**。本清单是未来交付验收表，不代表任何资产已到货或已通过验证。

## A. 交付与溯源（未完成前禁止晋级）

- [ ] 已收到正式 `AS_Mini4Pro_SRC.fbx` 或 `.glb`，不是占位/程序生成模型。
- [ ] 已记录供应来源、交付批次与文件 SHA-256。
- [ ] 已收到模型实际使用的完整贴图，不含纯色占位图。
- [ ] `intake-manifest.placeholder.json` 已由负责人依据实物更新并复核。

## B. DCC 与命名

- [ ] 根节点 `AS_Mini4Pro`；单位米；Y-up、+Z forward、+X right；根 TRS 为 identity。
- [ ] 网格按 `GEO_<Part>_LOD0..3` 命名，无 `final/new/copy` 等漂移后缀。
- [ ] 存在 `CAM_Flight` 与 `CAM_PreviewFocus`。
- [ ] LOD0/1/2/3 均在 RD-02 三角面预算内，Cull 档无 Renderer。
- [ ] 6–16 个 `COL_BOX_` / `COL_SPHERE_` / `COL_CAPSULE_` 隐藏碰撞代理；无 MeshCollider。

## C. 材质与贴图

- [ ] 材质命名为 `M_AS_Mini4Pro_<Material>`，shader 为 Built-in `Standard`，workflow 为 Metallic。
- [ ] `_BaseColor` 与 `_Emission` 使用 sRGB；`_Normal` 与 `_MetallicAO` 使用 Linear。
- [ ] `_MetallicAO`：R=Metallic、G=AO、B/A=1；没有伪造 `_Emission` 黑图。
- [ ] 每个 Renderer 材质槽均已绑定；贴图命名和材质语义一致。

## D. Manifest、Prefab 与门禁

- [ ] 主 manifest 的 `sourcePath/sourceFormat/sourceSha256` 与实际文件一致。
- [ ] 先走 `DccCandidate → ImportValidated`，不得跨级晋升。
- [ ] `Aircraft.prefab` 按 README 模板装配并通过 assembly validator。
- [ ] 自动脚本与 Unity 菜单 `AeroSim/RD-02/Validate Asset Manifest and Production Imports` 均通过。
- [ ] 12 角度证据与盲测通过后，才允许设置 `ProductionApproved + ProductionReady`。
- [ ] 最后才把 `productionContentStatus` 从 `CONTENT_NOT_DELIVERED` 改为 `DELIVERED`。

## 当前阶段验收

- [ ] 不存在正式 FBX/GLB/贴图/Prefab。
- [ ] loader 返回 `CONTENT_NOT_DELIVERED`，不创建 fallback。
- [ ] UI 显示 prototype / unverified，不宣称正式 Mini 4 Pro 身份。
