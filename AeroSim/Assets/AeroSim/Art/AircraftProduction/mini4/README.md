# Mini 4 Pro Production Intake（RD-06）

本目录只为**未来正式交付资产**提供接入合同；当前不包含、也不生成 FBX/GLB、高模、贴图、材质或预制体。当前唯一合法状态是 `CONTENT_NOT_DELIVERED`。运行时继续使用已披露的 programmatic prototype，身份保持 `Unverified`。

## 接入边界

- 运行时 ID：`mini4`
- 参考型号：`DJI Mini 4 Pro`
- 坐标：Unity，Y-up、+Z forward、+X right，单位为米。
- 正式源文件仅允许放在本目录下，并由 RD-02 主 manifest 以路径和 SHA-256 明确寻址。
- 不得用占位网格、程序生成模型、重命名的其他机型或空白贴图冒充正式交付。
- 在模型、完整贴图集、来源哈希和验收证据全部到齐前，不得把 `productionContentStatus` 改为 `DELIVERED`，也不得把身份/质量提升为 `ProductionApproved` / `ProductionReady`。

## 导入命名约定

交付后按以下结构放置；大小写必须一致：

```text
mini4/
  Models/AS_Mini4Pro_SRC.fbx
  Textures/T_AS_Mini4Pro_<Material>_BaseColor.png
  Textures/T_AS_Mini4Pro_<Material>_Normal.png
  Textures/T_AS_Mini4Pro_<Material>_MetallicAO.png
  Textures/T_AS_Mini4Pro_<Material>_Emission.png   # 仅确实存在自发光时
```

- 模型只接受 `.fbx` 或 `.glb`；正式文件名必须分别为 `AS_Mini4Pro_SRC.fbx` 或 `AS_Mini4Pro_SRC.glb`。
- `<Material>` 使用 ASCII PascalCase 语义名，例如 `Body`、`Arm`、`Gimbal`；禁止空格、版本号、`final`、`new` 等非确定后缀。
- DCC 根节点：`AS_Mini4Pro`。网格：`GEO_<Part>_LOD0..3`。材质：`M_AS_Mini4Pro_<Material>`。
- 相机 socket：`CAM_Flight`、`CAM_PreviewFocus`。
- 碰撞代理：`COL_BOX_<Part>`、`COL_SPHERE_<Part>` 或 `COL_CAPSULE_<Part>`，总数 6–16；禁止 MeshCollider，代理 Renderer 必须隐藏。

## 贴图通道约定

项目使用 Built-in `Standard` / Metallic workflow：

| 后缀 | 色彩空间 | 通道合同 |
|---|---|---|
| `_BaseColor` | sRGB | RGB=基础色，A=透明度（仅在材质确需透明时） |
| `_Normal` | Linear / Normal Map | RGB=切线空间法线；Unity Texture Type 必须为 Normal map |
| `_MetallicAO` | Linear | R=Metallic，G=Ambient Occlusion，B/A 保留并填白（1.0） |
| `_Emission` | sRGB | RGB=自发光颜色；无自发光材质不得提交伪黑图凑数 |

不得提交纯色占位贴图。贴图必须与模型 UV、材质槽及来源记录一致。

## 预制体装配模板（说明，不创建假 Prefab）

正式内容通过导入门后，人工建立 `Assets/AeroSim/Resources/AircraftProduction/mini4/Aircraft.prefab`：

```text
Aircraft                         # local TRS = (0,0,0) / identity / (1,1,1)
├─ AS_Mini4Pro                   # 导入模型实例
├─ CAM_Flight                    # 与 RD-02 socket 合同一致
├─ CAM_PreviewFocus              # 与 RD-02 socket 合同一致
├─ LODs                          # 一个 LODGroup：LOD0/1/2/3/Cull
└─ Collision                     # 6–16 个隐藏 primitive collider
```

- `LODGroup` 必须有 5 档；Cull 档无 Renderer；LOD0–3 不得超过主 manifest 三角面预算。
- 每个可见 Renderer 的每个材质槽都必须绑定 `Standard` 材质。
- prefab 通过 `ProductionAssetAssemblyValidator` 后才允许在主 manifest 声明 resource/address。
- 禁止在 loader 中加入程序几何或 fallback prefab。

## 自动检查

在仓库根目录执行：

```bash
python3 verification/rd06/validate_mini4_pro_intake.py
```

当前阶段该脚本应确认：placeholder 与 RD-02 主 manifest 均为 `CONTENT_NOT_DELIVERED`、正式模型/贴图不存在、UI 身份仍是 `ProgrammaticPrototype / Unverified`，且 loader 有显式 fail-closed 状态门。
