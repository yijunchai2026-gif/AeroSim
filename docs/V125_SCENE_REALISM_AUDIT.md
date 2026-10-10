# AeroSim 1.25：场景真实感与材质技术调研

日期：2026-10-10（America/Los_Angeles）。范围：当前工作区的 Unity 环境生成源码、MaterialsV2 资源/导入设置、三张原始概念目标和三张已保存的候选05原生飞行截图。这里只新增本报告；没有改运行代码、生成/下载新素材、操作 Unity 构建或替用户批准机型。

## 1. 结论与证据边界

真实感不足主要不是“PNG不够大”，而是**材质类别与米尺度不匹配、颜色细节没有对应的法线/粗糙度响应、近景轮廓和结构过于规则、水面与环境的光照关联不足**。优先在现有 Built-in 管线上做一条可复测的海岸/桥梁/工业近景样板，再扩展场景；URP不是贴图优化必需前置，也没有已接入。

这是源码与图像的专家审查，不是新运行测试、用户实验或移动真机验收。源码存在尚未提交的其他成员改动；历史PNG不证明当前源码或候选08具有相同行为。没有测得GPU帧时、纹理驻留、总内存、热降频或三端画质上限；第7节全部为 **proposal**。最终视觉与用户验收仍待执行。

本报告与[根负责人综合调研](V125_UX_UI_OPTIMIZATION_RESEARCH.md)互补。保留[PRD](../飞行模拟器产品设计文档.md)的23张原始概念图，不把实际截图覆盖为目标；材质/地形真实性也不能被菜单静态照片替代。当前程序化训练环境不等于已接入美国真实地图或可验证实景。

## 2. 实际检查的图像

以下6张PNG均已实际打开观察。三张概念图为1672×941；三张实际图的[gallery](../assets/ui-v1/candidate-20261009-05/gallery.md)和[manifest](../assets/ui-v1/candidate-20261009-05/manifest.json)标明：1.0.0开发候选05、2026-10-09捕获、1920×1080、UI/HUD/Text100%、隔离QA数据、视觉/用户验收PENDING。它们是已保存的原生运行证据，不是本轮新候选截图；目录中的08精选图主要是首页、暂停和回放，未用于宣称三种飞行场景已更新。

| 场景 | 原始概念目标 / 已保存实际图 | 可见差异与限制 |
| --- | --- | --- |
| 航拍海岸 | [概念航拍HUD](../assets/aerosim-us-flight-training-ui-v1.png) / [候选05 Aerial-Flight](../assets/ui-v1/candidate-20261009-05/Aerial-Flight.png) | 目标有破碎岩岸、沙滩/裸土/草丛多层过渡、体积化树群和白浪；实际可见大片均匀草地、平滑远山及较稀重复树冠。相机、地形构图和状态不同，不能把全图差异只归因于贴图或作像素一致性判定。 |
| FPV工业 | [概念FPV HUD](../assets/aerosim-us-09-fpv-race-osd.png) / [候选05 FPV-Flight](../assets/ui-v1/candidate-20261009-05/FPV-Flight.png) | 目标混凝土有接缝、边损、污迹和不规则杂物，低角度夕照强调体积；实际矩形墙/柱/桁架较规则、橙色箱体大面积单色、地坪均匀。目标本身是开放/破损工业骨架，不能简单判为“缺屋顶”；白天与黄昏不一致也不是单一材质缺陷。 |
| 专业桥梁 | [概念专业任务](../assets/aerosim-us-12-professional-mission.png) / [候选05 Professional-Flight](../assets/ui-v1/candidate-20261009-05/Professional-Flight.png) | 目标近景可辨模板孔/接缝、潮痕、支座、林岸及桥下水面关联；实际桥体大块规则几何、细线接缝、水面偏固定蓝色波纹。距离/视点不同，需同相机重拍才能量化改善；目标精细结构不能全靠法线替代。 |

MaterialsV2中的[草地](../AeroSim/Assets/AeroSim/Resources/Art/MaterialsV2/meadow-grass-v2.png)、[岩石](../AeroSim/Assets/AeroSim/Resources/Art/MaterialsV2/coastal-rock-v2.png)、[针叶](../AeroSim/Assets/AeroSim/Resources/Art/MaterialsV2/evergreen-needles-v2.png)也已实际打开。原图确有细部，但岩石颜色纹不能代表树皮/沥青，针叶扇状贴片也不自动形成自然整树。未做平铺边缘数值检验或动态渲染检验，不能声称无缝已通过。

## 3. 当前技术事实与定位

| 本地证据（源码行号为本轮观察位置） | 已有事实 | 对真实感/成本的影响（静态判断，待运行验证） |
| --- | --- | --- |
| [ProjectVersion](../AeroSim/ProjectSettings/ProjectVersion.txt)、[manifest](../AeroSim/Packages/manifest.json)、[GraphicsSettings](../AeroSim/ProjectSettings/GraphicsSettings.asset) L40、[QualitySettings](../AeroSim/ProjectSettings/QualitySettings.asset)各档 | Unity6000.3.25f1；包清单无URP依赖；CustomRenderPipeline为空，六档无SRP资产。 | 按Built-in审查，不能引用URP Renderer Feature或SRP Batcher为已实现优化。 |
| [ProjectSettings](../AeroSim/ProjectSettings/ProjectSettings.asset) L50；[LandscapeSurface](../AeroSim/Assets/AeroSim/Resources/LandscapeSurface.shader) L15、[TrainingSurface](../AeroSim/Assets/AeroSim/Resources/TrainingSurface.shader) L13、[IndustrialSurfaceV1](../AeroSim/Assets/AeroSim/Resources/IndustrialSurfaceV1.shader) L18 | Linear色彩空间；主表面采用Built-in `surface ... Standard`，不是完全无PBR光照。 | 问题是缺少逐材质物理输入和环境响应，不宜笼统说“未用PBR”。 |
| [Visuals](../AeroSim/Assets/AeroSim/Runtime/Simulation/FlightSimulator.Visuals.cs) L79–87、L310；[PROMPTS](../AeroSim/Assets/AeroSim/Resources/Art/MaterialsV2/PROMPTS.md) L9/L16 | 草纹作者意图约2m一砖，地面用5m；岩石4m、树皮也用岩石1.7m、桥面沥青也用岩石4m，沙漠用岩石9m。 | 草纹图案相对原意放大约2.5倍；一张岩石变色不等于树皮、沥青、砂地。先纠正语义/尺度，再谈分辨率。 |
| LandscapeSurface L24–30；IndustrialSurfaceV1 L35–48；TrainingSurface L21–22 | 地表颜色三向投射，但岩石坡度层仅worldXZ；无法线贴图输入。地表Smoothness常量、AO=.96；工业有程序化颜色/锈/光滑度微变，无Normal/AO贴图；训练表面只有颜色/Metallic/Smoothness。 | 颜色里的裂纹不会随光向正确凸凹；陡面坡度岩纹有投射拉伸风险。先加规范通道，而不是把高光/阴影画进BaseColor。 |
| [EnvironmentV1](../AeroSim/Assets/AeroSim/Runtime/Simulation/FlightSimulator.EnvironmentV1.cs) L10–24及工厂调用 | 混凝土/钢铁/木色栈道的调用多为texture=null，`_UseTexture=0`；钢铁kind默认Metallic=.52，混凝土Smoothness=.18。 | 程序化噪声有变化，但缺少材料特有结构与定向磨损；“木色混凝土”不是木板，涂漆/锈层不应全按同一金属值。 |
| Visuals L38–39/L88/L165–174/L187–199/L220–245；EnvironmentV1盒子批处理 | 720m地面120×120格=6m网格；规则有机形岩石、交叉针叶卡、硬边盒体；KeepMesh重算法线/包围盒，未重算切线。 | 近地碎石/悬崖破碎和桥梁边缘要几何补足。未来UV法线需切线；三向法线需正确坐标重定向/混合，不能直接把三份RGB相加。 |
| Visuals L57–76/L139–151 | 三色环境光＋一盏方向光＋程序化天空/雾；时间以>18分支，Overcast二分；HDR关闭。移动阴影65m/2级/2×MSAA，桌面95或150m/2或4级/4×MSAA；两端低档MSAA均为0。 | 天空/太阳已有方向关联，非“没有灯”；间接光、接触感、曝光与天气过渡仍需校准。HDR开启不自动变真实，先保持训练画面清晰。 |
| [OceanSurface](../AeroSim/Assets/AeroSim/Resources/OceanSurface.shader) L21–38；[RiverSurfaceV1](../AeroSim/Assets/AeroSim/Resources/RiverSurfaceV1.shader) L25–43 | 海面/河面有程序化波法线、Fresnel和太阳高光，但混合固定天空颜色；海岸泡沫按世界X阈值，未采样场景深度/桥梁反射。河面是视觉层，未替换地面物理。 | 光斑已有，不应称全无水面细节；岩岸/桥墩局部关联弱。若改变真实水体/地形碰撞语义，另需飞行核心验证。 |
| Visuals L96/L268；[NeedleFoliage](../AeroSim/Assets/AeroSim/Resources/NeedleFoliage.shader) L9–16；Runtime全文搜索 | 树/石尝试数量移动90、桌面165（跳过安全区后实际更少）；灌木移动45/桌面120；双面AlphaTest针叶投影。Runtime未找到LODGroup、LightProbe或ReflectionProbe接线。 | iPad/iPhone共用泛移动分支，不是独立实测档位。卡片遮挡/阴影可能比三角形先成为瓶颈，必须用GPU/Frame Debugger确认。 |

源码可推导地面28,800三角形、远山20,000、海面9,800、湖面4,050；这是单个生成网格的索引计数，不是实际可见总三角形、draw call或GPU成本。对象批处理/共享网格已有价值，不应为精细化回退为每颗碎石一个独立材质实例。

## 4. 材质清单与真实米尺度（全部待实施建议）

P0为先冻结合同/证据，P1为首轮样板高收益，P2为精修。以下覆盖尺寸及roughness是美术起始候选，不是实测材料参数。Unity现有shader输出的是Smoothness；若输入是同定义的感知roughness，可先用`s=1-r`转换并用球/平面受光校准，不能把roughness直接当Smoothness。金属/非金属、AO、Alpha职责分别记录。[Unity Standard材质通道](https://docs.unity.com/en-us/engine/6000.3/manual/materials-and-shaders/built-in/shader-built-in-birp/shader-standard-shader/standard-shader-material-parameters)

| 优先级 / 材质 | 米尺度与层次建议 | 需要的响应/几何 |
| --- | --- | --- |
| P0 / 所有材质合同 | 每组记录覆盖宽高（米）、作者/来源/许可、版本/SHA、BaseColor/Normal/Mask通道、法线Y方向、纹理导入及设备覆盖。 | 同一灰卡/球/平面、1m比例尺、两种光角建立基准；不可凭生成提示宣称PBR正确。 |
| P1 / 草地＋裸土＋干湿沙 | 草地先按原意2m砖试样；土/沙1–2m；近景detail0.25–0.5m，宏观分区20–60m；3–10m斑块控制草土比例。 | 非金属；干燥r约.75–.95、湿区.35–.6候选。独立normal和混合mask，低饱和色差；近地少量体积草/石，保留安全训练走廊。 |
| P1 / 海岸岩石 | 2–4m基底；裂隙/层理按岩型方向，5–15m宏观地质块；近景detail0.3–0.5m。 | r约.6–.9；干湿线分区不全靠蓝色。normal改善矿物/小裂缝，轮廓破碎/悬挑/洞口用少量变体几何。坡面颜色和法线须使用一致投射。 |
| P1 / 桥梁与工业混凝土 | 2×2m基底；1–2m模板/结构节距，0.25–0.5m微孔detail；接缝与孔洞按结构放置。 | 非金属，干r约.65–.9，湿r约.2–.45；normal＋Smoothness/AO，模板孔与倒角只做近景必要几何。竖向渗水、边缘损伤、潮痕不是全表面均匀随机噪声。 |
| P1 / 树皮＋针叶 | 树皮约0.5×1m定向砖，纵轴沿树干；针叶/枝卡按真实枝段尺寸组织，不把一扇叶纹铺成整棵树。 | 树皮normal、r约.75–.95；分树龄/树冠形状/枝密度，叶卡紧裁减少空Alpha。远树保轮廓和颜色群，不强求每根针叶。 |
| P1 / 水/岸线 | 微波normal约1–3m、宏观波10–25m候选；深浅、泡沫和湿边按岸线/深度mask，而非固定世界X带。 | 先让天空、太阳、桥下阴影和局部反射一致。移动优先低频cubemap/局部预烘反射；桌面平面反射只作独立成本试验，不默认每帧多相机。波几何不是碰撞水体。 |
| P2 / 沥青、道路标线 | 沥青1–2m基底＋0.25m颗粒detail；标线按实际宽度和交通语义。 | 不再借用岩石；非金属，r约.75–.95；裂缝/修补/磨损控制到安全走廊外，不让装饰误似任务引导。 |
| P2 / 涂漆钢、裸钢、锈 | 约1m基底，接缝/铆钉按构件；锈和漆分mask。 | 完整漆/锈通常非金属，露出金属区域再用金属响应；r依漆/氧化层变化，不用整根钢管一个.52。容器波纹/支座轮廓由几何体现。 |
| P2 / 栈道木板 | 0.15–0.25m板宽、1–2m长度候选；板色少量离散变体。 | 木纹normal方向沿板长；非金属、干r约.65–.9；板缝与轮廓分开，不能仅把混凝土染棕。 |

只把microdetail放在真正近景，按距离渐淡以免高速飞行闪烁；宏观色/湿度mask控制重复，不能只给每个对象随机旋转会有方向性的树皮/木纹。Detail地图可让近景锐利而不需要单张超大纹理，但会增加采样；现有自定义shader没有自动获得Standard Inspector的Detail输入，需专门接线和测量。[Unity Detail Maps说明](https://docs.unity.com/en-us/engine/6000.3/manual/materials-and-shaders/built-in/shader-built-in-configure-properties/standard-shader-texture-maps/material-parameter-detail)

BaseColor应去掉固定太阳方向的阴影/高光；法线优先来自有权使用的高模/高度数据烘焙，不把颜色照片中的阴影盲目变成高度。Normal按Normal Map类型导入并核对绿色通道，mask/roughness/smoothness/AO是数值数据，不按颜色sRGB采样。法线只改变细部受光，不能补悬崖或桥体轮廓。[Unity Normal导入](https://docs.unity.com/en-us/engine/6000.3/manual/materials-and-shaders/textures/textures-reference/texture-type-normal-map)、[Unity法线与几何边界](https://docs.unity3d.com/6000.1/Documentation/Manual/StandardShaderMaterialParameterNormalMap.html)

## 5. 导入、decals、光照与LOD方案

### 已有导入设置，不重复当成缺失

MaterialsV2源文件1254×1254；前三张RGB，针叶RGBA。[草地meta](../AeroSim/Assets/AeroSim/Resources/Art/MaterialsV2/meadow-grass-v2.png.meta)、[岩石meta](../AeroSim/Assets/AeroSim/Resources/Art/MaterialsV2/coastal-rock-v2.png.meta)、[针叶meta](../AeroSim/Assets/AeroSim/Resources/Art/MaterialsV2/evergreen-needles-v2.png.meta)显示：mipmap开启，颜色sRGB开启，Read/Write关闭，Trilinear、aniso8；草/岩Repeat、针叶Clamp；针叶已做Alpha coverage并配.42 cutoff。iOS override已启用、max1024、format50；ASTC6×6枚举50的官方定义可核对，但序列化配置不证明实际设备导入/解压结果。Standalone override未启用、自动格式。所有质量档streamingMipmapsActive=0，素材streamingMipmaps=0；512预算字段不等于已使用流送。

NPOT转换设置为1，源1254不等于运行驻留1254；下一轮应读取Importer实际宽高/格式和Profiler内存。不要把磁盘PNG大小当GPU占用。已有mips继续保留，斜视地面aniso可测试Desktop8、iPad4–8、iPhone4档；不是“越高越真实”。Unity将sRGB用于颜色，数值纹理需关闭；Read/Write会保留CPU副本，只有确需访问像素才开。[Unity6000.3 Texture Import设置](https://docs.unity.com/en-us/engine/6000.3/manual/materials-and-shaders/textures/textures-reference/texture-type-default)

移动首轮仍以ASTC6×6为普通地表起点；近景normal/针叶Alpha可A/B 4×4或6×6，远景色图8×8；Desktop按实际GPU/API选择BC1/BC7、normal等格式，不假设macOS与DirectX设备支持完全相同。官方建议A8及以上iOS用ASTC；不支持格式可能解压造成内存变化，须核对支持设备与运行格式。[Unity平台压缩选择](https://docs.unity.com/en-us/engine/6000.3/manual/materials-and-shaders/textures/textures-getting-started/texture-compression-formats/texture-choose-format-by-platform)、[ASTC6×6定义](https://docs.unity.com/ja-jp/engine/6000.3/script-reference/unityengine/textureformat/astc-6x6)

### 表面局部细节与训练清晰度

Built-in先试共享atlas的少量mesh decals或材质mask：混凝土接缝、流水污迹、潮线、路面修补；控制重叠、透明层数和深度偏移，不能与任务门/安全标记混淆。大范围磨损先进mask，近景才加贴花或倒角，保持任务语义、碰撞和导航标志不变。当前无URP Decal Renderer Feature，不能把它写成可直接开启的现有配置。

### 光照

先固定昼间晴天的太阳角、环境亮度、雾、相机FOV与曝光作基准；再分测黄昏/阴天。校准混凝土、岩石、草地的相对明暗和接触阴影，避免环境光过强掩盖normal，或压黑桥下妨碍巡检。霓虹/门条Emission本身不证明有周围照明。

若样板改为可编辑固定环境，再做非重叠lightmap UV、静态GI烘焙、飞行空间Light Probes和少量预烘Reflection Probes；当前运行时生成网格不能仅加组件就获得已烘GI。移动优先烘焙/自定义cubemap，不每帧更新所有探针。动态天气可用预先校准的光照组或低频更新，另验证切换；水shader还需实际采样反射，不能“加probe即修水”。烘焙探针只记录静态环境，实时捕获有额外成本。[Unity Reflection Probe类型](https://docs.unity.com/en-us/engine/6000.3/manual/lighting-overview/reflections/ref-probe-types)

### 近/中/远景与LOD

近景约0–15m：桥墩/支座/岩岸保倒角、接缝、轮廓碎损及一层detail normal；中景15–80m：简化细件、保大材质分区与树群体积；远景>80m：轮廓、树群色块、雾和宏观材质优先，取消微detail/细贴花/无意义小阴影。这是摄像距离试验分组，不是固定LOD算法阈值，FPV95°与普通66°FOV应分别检验。

树/石/非任务建筑采用独立LOD0/1/2与远景轮廓；按屏占高度初试12%/4%/1%切换，调LOD Bias后观察高速侧飞爆跳、阴影跳变和Alpha shimmering。移动先不加昂贵跨淡化；不会以视觉LOD删除碰撞、桥梁巡检目标、门或训练地标。地面/水体/远山可分块增加剔除粒度，但须衡量额外draw call；不要无限增细整张720m网格。LODGroup需要真实各级renderer/mesh，Quality设置不自动生成它们。[Unity6000.3 LOD配置](https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/lod/lod-group/lod-group-configure)

### URP仅为另案

如果后续决定迁移，先在隔离样板验证全部自定义天空/水/地表/针叶shader、深度/透明/阴影以及三端Metal/实际API。官方兼容表明确Surface Shader不支持URP，不能只安装包。URP优化文档可帮助规划阴影距离/级联、Render Scale、额外灯和Decal成本，但这些是迁移后的配置，不是本项目已测成果；不引用“URP一定更快”。[Surface Shader管线兼容](https://docs.unity.com/en-us/engine/6000.3/manual/materials-and-shaders/shaders/writing-shaders-birp/writing-surface/sl-surface-shader-lighting)、[Unity6000.3 URP性能调节](https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/in-urp/optimize-for-better-performance)

## 6. 真实性与回归验证方法（本轮未执行）

1. 冻结构建SHA、环境种子、机型/批准状态、相机位置/朝向/FOV、昼间/天气、画质、渲染分辨率、UI/HUD/Text比例；给历史05和新样板不同批次ID。先相同视点A/B，再另比原始概念的构图/题材，不用不同时间/距离冒充单材质改善。
2. 三组固定观察：起飞地面/岩壁1、5、15m；桥墩/容器/树冠15、40、80m；海岸轮廓/林群/远山100、300m。使用1m尺度参照；检查草叶/矿物/模板孔比例、坡面拉伸、4×4平铺重复、接缝、normal Y方向、干湿Smoothness和Alpha压缩。法线/纹理通过不等于结构比例通过。
3. 原生运行慢飞、快速侧飞与转头；录检查清单观察纹理游动、mipmap闪烁、叶缘、LOD/阴影切换、岸线泡沫与桥下反射。Profiler/Frame Debugger分别记录本帧CPU、GPU、draw call/SetPass、可见tri、纹理驻留和材质实例，不能从静态PNG读FPS。
4. 同设备/同设置重复至少3次；完成冷启动、20–30分钟持续飞行、连续10次进出环境/机库/回放的内存回收检查。记录机型/芯片、OS、屏幕像素与内部渲染尺寸、温度/系统热状态、供电、采样窗口和p50/p95/p99；有Profiler开销的Development测量另与非Development试用版本交叉核对。
5. macOS、最低支持iPad、最低支持iPhone及一个高档移动设备分别留证。支持型号/OS下限尚未冻结，不能替用户指定“所有iPhone/iPad”；桌面逻辑窗口不算移动GPU/触点/安全区验收。横竖屏、Text200和双杆场景也检查地标可辨、告警对比及UI遮挡。
6. 独立QA标PASS/FAIL/NOT_RUN；模型批准单独查，不把材质样板、自动几何检查、截图好看或本报告当用户确认。仍待用户逐机型绑定稿件ID、版本/SHA与确认记录。

## 7. 三端可测预算候选（proposal，不是实测或系统限额）

这些是首轮压力测试的报警线，用来发现瓶颈后调整；不是Unity官方通用上限，也不是承诺所有设备60FPS。CPU与GPU可并行，不能相加当总帧时。采样指标排除主动加载并另报加载峰值；纹理预算仅为场景驻留，UI/渲染目标/网格/引擎总内存另测。

| 项目 | Desktop首轮样板 | iPad首轮样板 | iPhone首轮样板 |
| --- | --- | --- | --- |
| 测试基线 | 已有macOS主机另记配置；新增Windows支持须另测 | 最低支持机型待确定＋高档对照，横/竖与窄窗 | 最低支持机型待确定＋高档对照，横/竖 |
| 帧预算 | 60FPS目标16.67ms；CPU main p95≤10ms、GPU p95≤12ms候选 | 60FPS目标；CPU main p95≤10ms、GPU p95≤13ms候选 | 首试60FPS，CPU main p95≤10ms、GPU p95≤13ms；必要时30FPS档33.33ms另评，不自动认为降档合格 |
| 内部3D像素 | 首测1920×1080；高分屏另测，UI保持独立清晰 | 首测1600×1200量级；2048×1536作高档压力点，比例按窗口变化 | 首测1600×900量级；1280×720低档候选，比例随横竖屏变化 |
| 场景纹理驻留报警线 | 256MiB | 160MiB | 96MiB |
| 普通单组色/normal/mask | 1K–2K，仅关键近景按投射像素评估2K | 通常1K，重点近景2K须预算核算 | 通常512–1K，重点近景1K；不一刀切破坏地标 |
| 可见tri / draw call软报警 | 300k / 400 | 200k / 220 | 120k / 150 |
| 初试实时阴影 | 一方向光，距离120m，2级与4级A/B | 一方向光，50–65m、2级 | 一方向光，40–50m、1/2级A/B |
| Detail/反射起点 | 近景1层detail，少量烘焙probe；平面反射独立试验 | 近景1层可选、远景关闭；预烘反射，避免每帧捕获 | 近景按成本选择，远景关闭；低频/预烘反射 |
| 验证状态 | NOT_RUN（本轮） | NOT_RUN（本轮） | NOT_RUN（本轮） |

draw call数不是成功标准：一张巨大双面针叶卡或多层三向材质也可能拖慢GPU；少量复杂shader不必优于更多简单对象。顺序应是先量CPU/GPU瓶颈，再裁近景以外的反射、影距、Alpha空白与微detail，再讨论渲染像素；任务目标/碰撞/安全提示不随画质档删减。[Unity Built-in性能资料](https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/birp)

## 8. 素材来源与交付门禁

现有MaterialsV2的[PROMPTS](../AeroSim/Assets/AeroSim/Resources/Art/MaterialsV2/PROMPTS.md)说明为独立AI生成颜色素材，不是扫描测量PBR，也未保证数学无缝。本轮未生成、下载或引入任何新环境资产。

后续环境素材先用原创/自摄、拥有烘焙权的自制高模，或许可明确的素材；逐项保存原页面URL、作者、许可原文/版本/获取日、可否修改/商业分发、署名要求、文件SHA和派生关系。来源与许可未核实不得进交付资源；“官方图片公开可见”不等于可提取作贴图，不能抓取DJI产品纹理、Google地图影像或第三方商用扫描素材拼接离线包。真实地图/摄影测量需另核地理数据与素材许可，不把本报告当法律许可结论。

环境与机型审批独立。Mavic 4 Pro等新机型外观仍为`pending_user`，没有借场景优化加入正式选机飞行、替换已确认机型或变更批准记录。用户逐型批准不阻止不依赖该机型的材质研究；批准前也不能把二维稿称为最终网格/材质。

建议接力：美术冻结材质合同与样板清单 → 程序员在独立候选接线并保存实际导入格式 → 三条设备线测原生运行 → 独立QA核对视觉/性能/安全语义 → 根负责人显式选文件、验证、提交、推送及远端SHA核对。本轮只交调研报告，不能据此宣称贴图已升级、移动已适配或1.25验收完成。
