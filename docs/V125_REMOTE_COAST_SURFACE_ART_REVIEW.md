# 1.25 远端海岸材质 / 水面 / 天气 / 植被独立美术审查

审查日期：2026-10-12。工作区：`/Users/bytedance/.codex/worktrees/v125-remote-review/飞行模拟器`。根负责人提供基线 `c1916a0`、分支 `codex/v125-raw-mapping`；本审查未自行操作 Git。角色：本波第 2 名 3D 美术，只新增本报告，未修改资产、参考图、代码或批准状态。

结论：已有运行时程序化 Shader 和 generated RGB 源图接线，但不能称 P02/P03/P06/P07/P08 生产 PBR 材质完成、10 项子资产逐项实现、23 原图视觉一致或三端性能通过。尤其有“贴图已绑定但当前输出不使用”的 P02 路径。包内 `NOT_CONNECTED` 仍需和后来新增消费者分开解释，不能只引用其中一方。

## 精确检查范围与方法

- 完整读取 `AGENTS.md`、`docs/RD08_COAST_GOLDEN_SCENE.md`；P02/P03/P06/P07/P08/P09 的 README、顶层 manifest 与材质/天气/LookDev 合同；P02 湿润参数、P08 adapter JSON/C#、P07 static-validation、P09 黄金镜头说明；P10 `P10_STATIC_AUDIT.md` 与完整 `coast-art-budget-v1.json`（本目录未发现 README/顶层 manifest）。
- 子资产只抽查：P02 `01-wet-rock`、`03-tidepool-bed`；P03 `01-coarse-sand`、`09-wheel-track-disturbance`；P06 `01-salt-grass-a`、`05-low-shrub`、`10-distant-community`；P07 `02-shore-break-strip`、`07-raindrop-ripple`、`09-wet-edge-mask`；P08 `03-lens-drops`、`08-cloud-shadows`；P09 `03-rain-exposure`、`06-vegetation-gamut`。不声称逐份审完其余子资产。
- 完整读取 `Runtime/Simulation/CoastProductionIntegration.cs`、Resources catalog、`HeroSurfaceV1.shader`、`CoastalWaterDetail.shader`、`RainStreak.shader`、P07 `FoamAtlas.shader`；只读取 `FlightSimulator.Visuals.cs` 的相关质量/材质/雨段与 `FlightSimulator.EnvironmentVerticalSlice.cs` 的材质绑定、植被批次段，及 P10 validator 的纹理/Shader 门禁段。以下路径均相对仓库，`Runtime`/`Resources` 位于 `AeroSim/Assets/AeroSim/`。
- 实际 `view_image` 看了 P02 湿岩、P06 植被、P07 泡沫、P08 天气 4 张源图（2880²，工具显示为 1600²）；`file`/SHA-256 核对 7 张 PNG。其余源图仅查头信息/散列，没有冒称实看。读取了 6 张 runtime-bound 源图的 `.meta`（P02 两张、P03、P06、P07、P08）及项目色空间/quality 序列化字段。
- 未启动 Unity/Blender/原生应用，未重新执行包 validator/P10，未打开真实存档，未采集候选画面、iPhone/iPad 真机、GPU/内存/热稳态数据；没有检查全部仓库资产、P11 材质或原 23 概念图，因此本报告不是全场景/全图验收。

## 已有交付物与真实消费者

| 包 | 已存在物料 | 当前消费者（源码静态核对） | 尚不能证明的内容 |
|---|---|---|---|
| P02 潮间带 | 两张 generated RGB 参考、10 份规格；专用 Shader 合同写 `NOT_AUTHORED` | 湿岩映射到 HeroSurfaceV1 `_BaseMap`；潮池底/湿边映射到 CoastalWaterDetail `_DetailAtlas` | 独立 normal/AO/roughness/height/waterMask 未发现于该包；不是去光照/标定 PBR；后两模式的贴图对输出无效，见 F1 |
| P03 地表 | 一张 generated aggregate RGB 参考与规格 | `soil` 把整张图传到 HeroSurfaceV1 | 粗/细砂、砾石、贝壳、轮迹等不是已生成的独立材质；无烘焙 normal/height/track mask；米尺度与铺贴合同未落实，见 F3 |
| P06 植被 | 黑底 RGB atlas，合同明确无 alpha、无 card/billboard | whole atlas 到 Opaque HeroSurfaceV1；BuildHeroVegetation 构造 box 批次 | cutout/matte、物种网格、风权重、8 视图 billboard、生态范围过滤均未由该消费者落实，见 F2 |
| P07 水体 FX | 黑底 RGB atlas；独立 FoamAtlas Shader 代码确实存在（premultiplied、depth fade、裁切 rect、质量变体） | 当前 shore-breaker 用 Opaque CoastalWaterDetail，未找到 Runtime/Resources 调用 FoamAtlas / `_AtlasRect` | 独立 Shader 有源码不等于本次已编译或接入；10 子资产、岸浪 ribbon、单涟漪/粒子、湿边线性控制图没有因此自动完成 |
| P08 天气 | 多板块 RGB atlas、连续参数合同、只读参考 adapter C# | RainStreak 绑定 whole weather atlas 作雨条透明度调制；Runtime/Resources 未查到 WeatherVisualAdapter 调用 | 镜头滴、折射 normal、流痕、涟漪、雾/云 mask 切片、适配器连续权重/关闭/泄漏合同仍未由此完成 |
| P09 LookDev | RGB 构图参考、10 JSON profiles、比较方法 | `CoastLookDevProfile.ForWeather` 硬编码 3 套灯光/环境色；EnvironmentState 提供 fogDensity | catalog 有 3 TextAsset 引用，但已读消费者未解析 `lookDevProfiles`；未实现合同的完整 colorGrading/色域/平滑转场；没有本次黄金画面或人工验收 |

源图是视觉词汇/图形来源，不是扫描数据。使用 Unity Standard 输出的程序化金属度/光滑度/AO 是“有 PBR 光照模型”的运行时材质，不等于拥有真实采集或高模烘焙的 PBR 通道。在检查的 P02/P03/P06/P07/P08/P09 文件清单中，位图只有 source PNG；P07 另有一份 Shader，未发现该范围的生产 `.mat`/网格/prefab 或独立 normal/mask/height 图。

## 优先缺口（事实、影响与关闭证据）

### F1 / P1：P02 潮池底与湿边只是计数绑定，当前 Shader 输出不依赖源图

`FlightSimulator.EnvironmentVerticalSlice.cs:155` 将 mode 0/1 的 P02 图送到 `_DetailAtlas`。`Resources/CoastalWaterDetail.shader:49` 起定义 `tidePool/wetBand/breaker`；53–58 行的 atlas 只改变 `foamLine`，所有进入颜色的 foamLine 路径均乘 `breaker`。在整数 mode 0 或 1，`breaker=0`，Albedo/Smoothness/Occlusion 不依赖 atlas。此为源码数据流推导，不是实看 GPU 抓帧；该无效采样是否由平台编译器消除也未测。

`CoastProductionIntegration.cs:123` 的 `boundTextures++` 是每次成功 lookup 计数，湿岩重复使用也加计数；185 行的 `>=9` 不是唯一纹理数、PBR 通道数或像素贡献证明。应分别记录“资源存在/绑定/实际输出贡献”，再用同机位替换诊断图 A/B 证明潮池与湿边真正受控。

### F2 / P1：P06 atlas 被当不透明世界色图，未实现 cutout 物种

实看图为黑底分离植株，带方向明暗/边缘混色。`HeroSurfaceV1.shader:55` 按 world XZ 采样整图；58 行将暗 RGB 混回语义色，70 行恒 `Alpha=1`，没有 alpha clip、card UV、叶片 normal/translucency 或风顶点位移。`FlightSimulator.EnvironmentVerticalSlice.cs:358` 起构造 grass/scrub box，372–376 行不是合同的 7/12 张植物 cards。不能称真实植物 alpha、物种/季节、风权重、billboard 已实现。

P06 的 `salt-grass-a` atlas rect 为左上 `[0,.57,.1667,.21]`；该源图底部还包含多组地被/灌木，裁切必须按单株逐项复核，不能只检查 rect 在界内。当前不使用这些 rect，故不存在“所有物种裁切正确”的证据。关闭需 de-light、matte/去黑边/扩色、mip coverage、根部锚点和 LOD 逐株画面，再查生态 placement 与真机 shimmer/overdraw。

### F3 / P1：彩色源图的尺度、映射和物理通道与合同脱节

实看湿岩的沟缝暗部和表面亮斑已包含照明；source import `sRGBTexture=1` 适合可见颜色，但不提供 linear normal/roughness/height。HeroSurfaceV1 只采 RGB，其 Standard smoothness/AO 来自常量/程序噪声，没有 normal map。`candidateUv=worldXZ/_Scale*.18` 意味整图名义周期为 `_Scale/.18`：岩石 scale 3.6 → 20 m（P02 湿岩合同 3.2 m）；soil scale 2.2 → 12.22 m（抽查 P03 粗砂合同 2 m）。这不等同颗粒毫米尺寸，而且垂直岩面采样不随 Y 变化；噪声有方向加权不等于彩色贴图已 triplanar。需 1 m 中性标尺平面/垂直面、近/掠角、高光关闭 A/B 与连续 1/5/15/40/80 m 路径核对。

### F4 / P1：水体/天气图集消费者未遵守单元裁切与透明/数据图合同

实看 foam 含多条破浪、散点、多个圆形涟漪；`CoastalWaterDetail.shader:53` 在整幅 `frac(worldXZ*.035 + time)` 上采样，再取 max RGB。当作一张连续岸浪图会混用不同 FX 单元，且当前 `RenderType=Opaque` / `Alpha=1`，不是 P07 的 transparent premultiplied overlay。独立 FoamAtlas 在 62 行支持 `_AtlasRect`，但其运行接线、边缘 padding/mip bleed、深度纹理启用/排序尚未验证；不能用该独立代码存在替实际消费者验收。

实看 weather 含雨线、镜头滴、流痕、涟漪、底部雾噪声板块；`RainStreak.shader:24` 用 `frac(uv*(.25,1)+timeOffset)` 跨全高/横向滚动取 RGB 灰度，只调雨条 alpha，黑色输入仍保留 `.62` 权重。它不是按板块提取的雨线/镜头 alpha 或折射 normal。已读 P08 adapter 返回权重但未被查到调用，且没有其合同的资源句柄 Dispose/释放实现。源 atlas 以 sRGB 导入，当前 RGB 取灰度只证明颜色解码后的艺术调制，不等于经校准的 linear coverage/wetness/noise。独立数据 mask 应另导为 linear 并核对 alpha、UV 原点、padding、mip、精度与 A/B。

### F5 / P2：声明与消费者版本不同，P09 也仅局部接入

P02/P03/P06/P07/P09 README/manifest 的 `NONE/NOT_CONNECTED` 描述的是隔离包交付；catalog GUID 现在已接入 source 图，不能说完全未引用，也不能因此升级包的 `NOT_DERIVED/NOT_RUN`。P09 `ForWeather`（68 行起）只按 weatherId 选择硬编码值，`ApplyCoastLookDev`（126 行起）未解析 profiles 的 colorGrading；雨 profile 声称的 exposureEv/contrast/saturation/lift/gamma/gain 不由此实现。后续应记录绑定版本/适配器版本/未落实字段，再采集原生 High 黄金画面；不要更新参考或套用单一数值通过视觉验收。

## 手机版预算真实性

已经存在可检查的候选门禁，不是设备 PASS：

- RD08 明确标注 iPhone Low 120k tris / 150 draw / GPU p95 13 ms、Desktop High 300k / 400 draw / 12 ms、iPad High 200k / 220 draw 为软报警线；要求冻结硬件、非 Development Player、120 秒预热、30 分钟固定回放、3 次冷启动与 P50/P95/P99。本次全部未运行。
- P03 96/48/32 MB desktop/tablet/phone streaming 是该包规格；P10 每资产静态纹理 64 MiB、source/Standalone 4K/mobile 2K 与 draw/LOD/keyword 限额也是合同。`FlightSimulator.Visuals.cs:249` 实际全局 streaming 预算为 Low/Medium/High/Ultra 256/384/512/768 MB，未分别区分 iPhone/iPad。两者量纲/范围不同，不能视为实际服从 P03 包预算。
- 6 份已读 `.meta` 已设置 mip/streaming、压缩目标、readWrite=0；Standalone max4096，序列化 `iOS` override max2048/format50。P06/P07/P08 Clamp，P02/P03 Repeat；这些是导入设置而非实际设备格式、驻留内存或热稳定证明。没有解码/宣称 format50 的设备实际结果。
- P10 validator 已增加 runtime catalog dependency 检查，不再豁免被运行时引用的 generated source；这是合理静态防漏。但是 validator 查询 `GetPlatformTextureSettings("iPhone")`（`Editor/CoastArtBudgetValidator.cs:146`），meta 序列化 target 为 `iOS`；应在目标 Unity Editor 确认别名及实际 override getter 结果，本次不自行判定有效/失效。
- P10 `P10_STATIC_AUDIT.md` 声称既有 Unity 编译/执行以及三张旧 MaterialsV2 streaming 缺口；本次只读取，未复跑。该文档的 generated-source 豁免摘要和 JSON 的“runtime-used 不豁免”也须一起阅读。即使静态门禁通过，也未证明运行时程序批次的全部 draw/collider/LOD、transparent overdraw、atlas 有效贡献、GPU 或 iOS 真机通过。

## 资产与源码指纹

以下 PNG 的 SHA-256 由本次 `shasum -a 256` 复核，与顶层 manifest 一致；RGB8 无 alpha 由 PNG 头复核。

| 相对 `AeroSim/Assets/AeroSim/Art/CoastProduction/` | 尺寸 | 本次实看 | SHA-256 |
|---|---|---|---|
| P02/source/wet-rock-reference.png | 2880² | 是 | `ceb7ff9a0b55ef5801e2bc9d4bf78308c51144c752f04cab2d51ff94e0f77b40` |
| P02/source/tidepool-bed-reference.png | 2880² | 否 | `d84db9b1323b596e5166ea08ae1d129cb989401c9598b9877918825c7c2b2f80` |
| P03/source/coastal-aggregate-reference.png | 2880² | 否 | `1ed0eaa6db628cb8835b692e0db4f0f3c7117e8682ee358688474e147ae3c4e6` |
| P06/source/coastal-vegetation-atlas.png | 2880² | 是 | `c82d1f17f49ed33dc359a002332bd39cb0d5759bbf2fe926654352ab87ce6b6f` |
| P07/source/foam-atlas-reference.png | 2880² | 是 | `1afda591f7a75dc2cdcd80934d7626429ca22da5d1310eeb3786b1b20908433f` |
| P08/source/weather-atlas-reference.png | 2880² | 是 | `7db8075a5b0a8558282b15c3276c9c081a298b083633303fc906511a66e176e1` |
| P09/source/coast-lookdev-reference.png | 3840×1648 | 否 | `fd8dea3fd76b9a57274294814f69e701eb4fd545d2d50e7598024675eb217859` |

核心核对对象指纹（相对 `AeroSim/Assets/AeroSim/`）：

| 路径 | SHA-256 |
|---|---|
| Runtime/Simulation/CoastProductionIntegration.cs | `ae3ce29bf204283ff5e7b94f4de23234dcad8f5f5a6210570e049f9a62012b19` |
| Resources/CoastProduction/CoastProductionRuntimeCatalog.asset | `af5985b29face5c7fe000d368e243ca0cbd14eb0bce6e54e3d7d848a018d15a4` |
| Resources/HeroSurfaceV1.shader | `0117378d58f5713f20ee2f3e00b368ed05b1f16d4e65a19229c9289754f73706` |
| Resources/CoastalWaterDetail.shader | `7a374a994e9a0e2586ad418572f92ad46d295f14d2d0ed9bb90a7a3a11e513f3` |
| Resources/RainStreak.shader | `c993438d4602c3394795d1687fa9ee9df06c5ce9d7cf265c1b91639f3a4a0f7b` |
| Art/CoastProduction/P10/coast-art-budget-v1.json | `f7aa0e5f4b94303094226a823267b8f761860a04503c911dd3d0e89d2e1c324d` |

本次审查只提供可复查静态缺口与局部源图读图，不设 ProductionReady、不替用户确认外观、不替最终人工验收。建议优先关闭 F1/F2/F3，再做图集逐项接线和同 build 的真实画面/移动设备验证。
