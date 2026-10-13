# 1.25 远端海岸网格与近景美术独立审查

审查基线：`c1916a0f63a4a17d2af8767e4fd2dcdcab79ee2f`，分支 `codex/v125-raw-mapping`。结论：**存在可核对的程序化网格与 P11 DCC/烘焙候选，但近景视觉、导入一致性、米尺度、碰撞贴合和移动设备交付均未闭环；不是 ProductionReady 或 3A PASS。**

## 实际审查范围与边界

- 完整阅读 `AGENTS.md`、`docs/RD08_COAST_GOLDEN_SCENE.md`、`docs/RD02_ASSET_GATE.md` 及 P01/P04/P05/P11 README；查看包索引、P01 macro-cliff/sea-stack-a/collision-proxy/distant-silhouette、P04 平台与三个合同、P05 standards/guardrail/metal_collision_proxy，以及 P11 生产/截图清单、生成器、导入器、shader、loader/catalog 和验证脚本。子资产采样不是对各包全部型号逐项验收。
- 本轮只运行文件读取、目录枚举、`file` 文件头识别和 `shasum -a 256`；通过 `view_image` 实际看图。**未执行** P01/P04/P05 校验器、P11 `verify_products.py`/`verify_blend.py`、Unity 导入/构建/运行、Blender 重建/烘焙、原生应用、碰撞回放、性能测试或 iPad/iPhone 真机测试。
- 没有生成、修改、提亮、裁剪或重采样图稿；没有改变 23 张原始参考。工具显示 BaseColor/Normal 时从 2048² 缩至 1600²，文件本身未变，因此微纹理判断不是 1:1 像素 QA。
- RD-02 是无人机身份/生产门禁，不是海岸视觉合格证；本轮不触及正式无人机资产或逐机型 `pending_user` 确认。用户验收和外观确认均未代行。本报告不决定产品文档版本或范围迁移。

以下简写只表示仓库内路径：`A/` = `AeroSim/Assets/AeroSim/Art/CoastProduction/`；`E/` = `AeroSim/Assets/AeroSim/Editor/`；`R/` = `AeroSim/Assets/AeroSim/Runtime/`；`S/` = `AeroSim/Assets/AeroSim/Resources/CoastProduction/`。

## 实际看图记录

| 文件（均为原字节） | 实际可见事实 | 本轮 SHA-256 |
| --- | --- | --- |
| `A/P11/HeroCliff/Reports/CaptureFrames/P11_001m_FinalColor.png` | 640×360，画面几乎全黑；只见极弱暗纹，不能识别岩层、裂隙或材质质量。 | `e14b163c60aafab6e40b0ee9a61000bf1c83ee87b194eb7f5c5e994360a8cc21` |
| `A/P11/HeroCliff/Reports/CaptureFrames/P11_015m_FinalColor.png` | 仍近乎全黑，右下灰色背景/边界；没有可读水线和冠部。 | `90eb5d61eeccb768d36758125d2e6b9298f93da5692d68253abad010dbd48f96` |
| `A/P11/HeroCliff/Reports/CaptureFrames/P11_040m_FinalColor.png` | 暗岩面占大部分画面，右侧灰色背景；弱竖向表面节奏，水线/地景关系无法判断。 | `268583b58b263dc9ecef9b5978ec51698b28887c4c4c2031986919a0d9c0348d` |
| `A/P11/HeroCliff/Reports/CaptureFrames/P11_080m_FinalColor.png` | 画面中央是一块暗竖面，两侧灰背景；斜截边界明显，没有可辨海岸、水线或整体冠部上下文。 | `92c934985a921c413f4070be75a25b993ac471e8d53efb18662f689ea3d794a7` |
| `A/P11/HeroCliff/Textures/T_HeroCliff_P11_BaseColor.png` | 灰褐、低对比细噪声为主，四角/边缘有黑色外域；单看贴图不能证明成景层理、真实岩性或 1 m 可信度。 | `0400bd104b639a5f2180f812bc5428585124b5928bffbe0e339e65b46559b4b7` |
| `A/P11/HeroCliff/Textures/T_HeroCliff_P11_Normal.png` | 蓝紫法线色，明显规则斜向/纵向条带，局部有连贯凹凸变化；规律性是人工复核候选，不凭预览断言烘焙错误。 | `dd078e7ff24bba92cc7683cff4c2c9e9e17abf1fea566fbfc1caa513778371e4` |
| `assets/aerosim-us-12-professional-mission.png` | 原 23 图中第 12 图（专业桥梁巡检概念图）；桥柱明暗面、拼缝、孔洞、湿水接触和近远层次可读。仅作为“专业近景须可读”的局部 LookDev 对照，不是海岸实测或同视角对比。 | `235ce331d4632b4a1186ac3d746a575e3a35b7a89fa1bb9b4d11e1ff234e9914` |

第 12 图身份/锁定散列见 `verification/v1-qa/reference-manifest.json:519–524`，本轮散列一致。没有检查全部 23 图，也没有检查 P11 5 m 图。P11 截图清单 `A/P11/HeroCliff/Reports/CaptureFrames/manifest.json:3–15` 明确为 `Blender EEVEE source review (not Unity device)`、`NOT_REVIEWED`、`GATE_NOT_RUN`、`visualPass:false`；距离标签不是设备捕获证据。

## 包级真实性与具体缺口

| 包 | 文件证据，而非状态标签推断 | 当前不能据此宣称 |
| --- | --- | --- |
| P01 | 目录实有 9 个 Unity `.asset` 网格和 10 个 `.prefab`（第 10 个为无渲染碰撞代理）；`Meshes/macro-cliff.asset:14,18,38–62,99–100` 有 672 索引、114 顶点、UV0 与实际缓冲，非空占位。Prefab `macro-cliff.prefab:30–44` 根 TRS 为 identity 并引用网格，材质 `Materials/CoastRockP01.mat:25–38` 绑定生成源图及常量参数。生成器 `Editor/CoastP01Generator.cs:94–132` 是 8 边环状程序网格，重新计算法线/切线。 | 扫描岩石、正式高模烘焙材质、近景真实岩性、DCC 精修或已过设备 QA。`GENERATED_READY`（各 manifest:41）不是 ProductionReady。 |
| P04 | README:3 明确仅源图/10 个 manifest/合同/静态校验；枚举没有 `.fbx/.blend/.obj/.glb/.asset/.prefab/.mat` 交付。`package.manifest.json:9–10` 是两张生成式源图锁定，非完整 PBR texture set。`contracts/material.contract.json:9–21` 是 3 m 世界/盒投影、防滑微法线等规格，`contracts/decal.contract.json:12–18` 要求另生成 alpha 和程序/矢量标线。 | 完成工程级平台、系留件、排水与贴花网格、UV、LOD 或 Unity 导入。已有报告 `validation/static_validation_report.md:3–6` 的 PASS 仅是合同/清单等静态范围，本轮未重跑。 |
| P05 | README:3,31,45 明确规格/参考而非引擎产物；枚举没有网格、LOD、prefab、材质或实际 `T_P05_*` texture set。`standards.json:4–10` 是厘米、Z-up、+X-forward；`manifests/guardrail.json:18–23` 的纹理、四档预算和胶囊/盒体仍是待制作交付项。 | 已有生产工业模块、正确米制轴转换、实际 1024/512 px/m、四档 LOD 或碰撞贴合。`validation_report.md:11–21` 是规格合法性声明，不是实物验证。 |
| P11 | `file` 识别 `Models/HeroCliff_P11.fbx` 为 FBX 7400、`HeroCliff_P11_Source.blend` 为 Blender 3.04 源；两者本轮散列分别为 `1d5ec1fd1fd33eea0f7921680424abb99b261163a3a6f43f003202519e7a7e66`、`36dcc73e7aa45cb9e31d7fada273c17837becbe6a3b198de37cfa4f3721eaa56`，与生产清单:134–135 一致。七张 PNG、shader、材质和导入代码确实存在；真实文件存在与散列一致不等于本轮解析了 FBX 网格或重跑了 bake。 | Unity/真机已导入、LOD/碰撞工作正常、3A 视觉过关。生产清单:239–242 自身仍是 `productionReady:false`、`GENERATED_NOT_REVIEWED`。 |

## 优先处理的发现

1. **P1 — 现有 P11 捕获不可用于近景/构图验收。** 实际看过的 1/15/40/80 m 图过暗，15/40/80 m 不具备可读冠部/水线，不能证明清单:15 的 compositionProtection 已落实。`Source/render_capture_route.py:15–25` 只渲染 LOD0 和一个 FinalColor 通道；不是 Unity 5×8 诊断捕获，也未覆盖任何跨 LOD 转换。应先取得曝光/材质可读、保持原相机元数据的独立新证据；不能改旧图或把旧图改称 PASS。黑图原因本轮未定位，不能归因于 Unity `.meta`，因为它们是 Blender 源渲染。

2. **P1 — Unity 导入执行未验证，当前静态缺件与 metadata drift。** 目录枚举中 `S/P11/` 只有 `HeroCliffP11.mat`/meta，没有 `HeroCliff.prefab`/meta；模型目录没有 FBX `.meta`。但 `S/CoastProductionRuntimeCatalog.asset:32` 存在 prefab GUID `c38cfe2690b6b44c6852551ee2555928`，本轮 `Assets/**/*.meta` 搜索无对应 GUID。`E/HeroCliffP11Importer.cs:58–64,102` 明确有自动/菜单构建 prefab 的路径；**仓库静态 missing 不等于已证明运行时失败**，本轮导入执行为 NOT_RUN。loader `R/Simulation/HeroCliffProductionAssetLoader.cs:23–24` 仅说明若导入后仍缺 prefab 可报告 AssetMissing，不是本轮运行结果。六张数据图 `.png.meta:10` 均为 `sRGBTexture:1`，Normal `.meta:57` 是 `textureType:0`，与 README:25、导入器:22–23 的 linear/NormalMap 合同冲突。导入器可能在重导入时修复，但本轮未运行，当前磁盘证据不能称导入已验证。

3. **P2 — P01 米尺度合同与已生成网格不一致，LOD 距离只是规格。** `Manifests/macro-cliff.manifest.json:21–33` 宣称 96×54×30 m，实际 `Meshes/macro-cliff.asset:153–155` AABB extent 为 `(59.477432,27,15.581018)`，即约 **118.954864×54×31.162036 m**；identity prefab 不是缩回合同尺寸。该 mesh 672 索引对应 224 tri，42,000 是上限而非实际面数。生成器:67–89 只构建一个 MeshRenderer，没有 LODGroup；manifest 的 0/80/250 m 不证明三档已实现。三盒代理只在 `Prefabs/collision-proxy.prefab:39,60,81` 存在；视觉岩石 prefab 默认没有碰撞符合 README:32，但未证明装配时代理与岩柱/悬崖的可见轮廓一一贴合。本轮 mesh/manifest 散列：`63739f85c3c99803c05bb10c26d8b44a6f3c3a33185f76ba63e9a2a15cca2e65` / `0fc2f9b4c9d962986b988a111f29ee536c38981df07d009d6803add39833f47f`。

4. **P2 — P11 网格与 UV 有可重建契约，但不是闭合立体岩体或完整 LOD 连续性证明。** `Source/build_hero_cliff.py:14,48–73` 是 `x=f(y,z)` 的 42 高、112 长开放前表面网格，只在矩形域产生三角面与 unique UV；没有闭合背面/侧面/顶面生成。清单:163–173 允许 872 条边界边、一个 boundary component，因此 `nonManifoldEdges:0` 不意味着 watertight。近岩后方、冠部、侧方飞行须明确闭合或场景遮挡策略。2K atlas 加 16 px gutter 的源参数估算约 **18 px/源单位（112 长方向）、48 px/源单位（42 高方向）**，不是已测实际 texel density；Unity 米尺度与 UV 拉伸仍须验证。

5. **P2 — 跨 LOD 声明/测试范围不一致。** P11 README:18 仍称 LOD1–3“由 LOD0 轮廓感知减面”，但生产清单:155 和生成器:211–215 是独立低分辨 heightfield，面数声明为 92,160/49,536/21,868/7,728。正面正交掩膜 `build_hero_cliff.py:128–156` 从 −X 侧沿 +X 看，固定 Y/Z 域，IoU:1.0 对 X 深度差异、斜视边界、内部岩檐与法线高光跳变不敏感。所有 LOD 使用相同参数 UV 布局不代表切线/normal 的跨 LOD 外观已过；当前 source capture 仅 LOD0。Importer:99–100 配 CrossFade，主 shader:23–25 没有 dither/crossfade variant 声明；只记录为需要实际运行确认的连续性风险，不把代码设置当成功证据。

6. **P2 — 坐标/碰撞与平台合同需要实物核对。** P11 source 用 Y 为高度、Z 为长度，而 `build_hero_cliff.py:295` 使用 FBX `axis_up='Y'` 和 `apply_unit_scale=True`，没有显式 `scene.unit_settings`；Unity 集成 `R/Simulation/CoastProductionIntegration.cs:148–150` 保持 identity rotation/scale。本轮没解析 FBX 轴/单位或导入后的 bounds，故只标坐标与米尺度风险，不断言实际倒置。15 个 P11 代理盒在 source:275–279 明确给出，但 loader:45–50 只验数量/类型/renderer/material，不测对表面误差、凹腔可进入性或碰撞早触发。P04 平台 `manifests/01_anti_slip_platform.manifest.json:12–16` 声明 24 m、1.5% 坡度（全宽高差 0.36 m）却指定水平 box 碰撞；实际坡面与盒顶的吻合/接触高度没资产或实测。P05 cm/Z-up 到 Unity m/Y-up 的转换也未见实物导入证据。RD-08:15 的 MeshCollider 是原程序切片描述，不能覆盖 P04:32/P11:130 的“不用 MeshCollider”生产合同，需分对象记录新旧策略。

7. **P2 — 静态验证器与当前 shader 有漂移。** `A/P11/HeroCliff/Tests/verify_products.py:71` 要求 `tri(_MacroDetail` 和 `triNormal(_MicroDetailNormal`，当前 shader 已换为 `RockNoise`（shader:46–52），这两个字符串不存在；验证器:81–82 也检查 linear/NormalMap metadata，与当前 `.meta` 相矛盾。以上是文件逐项比对，不是本轮运行所得 FAIL/PASS；应由独立 QA 跑实际 gate 并保存原始结果，不能引用 README 的“自动门禁验证”作为本次 PASS。

## 可诚实推进的下一步

- 保留当前候选与全部原图散列，先对 P11 做冻结 Unity 版本的重新导入、生成 prefab、引用/轴/米尺度/切线/贴图色空间检查；记录导入前后散列和真实导入结果。
- 在可读灯光下采集 1/5/15/40/80 m 的 Unity FinalColor、Albedo、Normal、Roughness、AO、Wetness、LOD、Collision；加入侧/背/冠部视角与连续跨 LOD 飞行，核近景重复节奏、湿水关系、轮廓/高光 popping 和碰撞 signed distance/手感。
- P04/P05 继续按合同产出真正模块、UV、LOD、独立 data maps 和碰撞体；不要把参考源 RGB 贴图绑定或 manifest PASS 当工程资产完成。
- Desktop/iPad/iPhone 各自记录真实硬件、内部渲染分辨率、材质/贴图压缩、High 与固定回放性能；RD-08:19–27 只是候选报警线。本轮三端状态均为 **NOT_RUN**，最终人工视觉/产品验收仅由用户完成。

本报告只新增这一文档；没有修改任何图稿、源码、资产、用户确认状态或 Git 历史。根负责人负责整合、验证及 GitHub 交付。
