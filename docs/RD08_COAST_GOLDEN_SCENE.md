# RD-08 海岸黄金场景实现说明

> **Procedural Vertical Slice / NOT SCANNED**
> LookDev 参考图只用于构图、色调、层次与题材目标；本实现为可飞行、可碰撞的运行时程序化 3D，不把参考图作为场景背景或扫描资产。

## 本轮落地

- **专业起降区**：抬高式防滑飞行甲板、H 环、禁入斜纹、周边框架、系留/护栏语义、排水槽，并保留实体碰撞。
- **海岸地貌**：分层岩壁、错台、悬挑层、崩积块与两座非规则下切海蚀岩柱；近景不再只依赖规则方块轮廓。
- **潮间带**：新增 `CoastalWaterDetail` Shader，独立表达潮池、暗色湿润带和岸浪白沫；Ocean 继续负责远海波浪、Fresnel、近岸浅水色。
- **植被群落**：同一 EnvironmentState seed 驱动盐生草、石南/低矮灌丛的确定性簇状分布，Low/High/Ultra 改变密度而不改变任务与碰撞。
- **检查设施**：低轮廓仪器房、管架、检查步道、护栏、灯标及排水，形成从海蚀岩柱、悬崖灯标到检查塔的连续可读地标。
- **天气与光照**：Clear/Overcast/Rain 继续由单一 EnvironmentState 驱动；Hero/Landscape 增加随云量变化的低频移动云影，雾、湿润、积水、雨和音频保持同源。
- **分级**：1/5/15/40/80 m 视距写入 GoldenView `reviewBand`；Low/High/Ultra 控制海岸 Shader 细节、岩柱径向段数、岩层/潮池/植被/VFX/音频密度。
- **碰撞与边界**：飞行甲板、岩层、岩柱、步道、工业设施与排水均有 MeshCollider；新增 640×640×180 m 五面实体安全边界。视觉降级不删除安全碰撞。
- **声音**：空间化岸浪、海风、工业低频；High 增加缆索/草丛风和金属共振，Ultra 增加远岸浪尾音。
- **证据合同**：Visual Lab 截图 metadata 增加 hero zone、review band、`Procedural Vertical Slice / NOT SCANNED`；PreviewCapture 校验新 Shader、五级视距、碰撞、地标及 provenance。

## 性能预算（候选报警线，不是实测 PASS）

| 档位 | 内容策略 | 目标软报警线 |
| --- | --- | --- |
| Low | 低段数岩柱、较少岩层/潮池/植被、海岸微细节关闭、VFX Off、3 层环境音 | iPhone 候选：可见 tri 120k、draw 150、GPU p95 13 ms |
| High | ART TARGET；海岸中频细节、10 层岩层、10 个潮池带、完整群落、5 层环境音 | Desktop 候选：可见 tri 300k、draw 400、GPU p95 12 ms；iPad tri 200k/draw 220 |
| Ultra | 15 段岩柱、14 层岩层、额外 Shader 微频、更多 VFX/植被、6 层环境音 | 仅 headroom；不能代替 High 内容正确性 |

运行时真实性能仍必须在冻结硬件、非 Development Player、High、120 秒预热、30 分钟固定回放、3 次冷启动条件下采集 P50/P95/P99；静态检查不把性能门标为 PASS。

## 正式资产缺口

1. 岩石、潮间带、混凝土、镀锌钢仍是程序化/常量 PBR；缺经来源审查的扫描或高模烘焙 albedo/normal/mask。
2. 海蚀岩柱已有非规则运行时网格和碰撞，但缺正式 DCC 拓扑、UV、顶点法线、LOD0/1/2 与烘焙碰撞代理。
3. 起降灯、系留件、护栏节点、格栅、排水口仍为批处理几何，缺工程级模块资产与标识贴花。
4. 植被是确定性程序群落，缺正式物种模型、alpha/法线、风权重、季节与生态区审核。
5. 环境声为确定性合成占位音，缺正式多声道岸浪、阵风、雨打金属及距离/遮挡调音资产。
6. Built-in 下的云影是廉价地表调制，缺正式云层投影、Reflection/Light Probe 烘焙与目标设备校准。
7. 参考图仍只是 LookDev 目标，不是实际 3D、摄影测量或可交付纹理。
