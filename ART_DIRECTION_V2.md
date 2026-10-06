# AeroSim 0.3 — 场景与无人机美术升级

本轮把第一版纯色几何体的占位画面，升级为可在 Unity 实际飞行的原创美术资产。目标是向高质量无人机训练产品靠近；不是 DJI 官方模型、贴图或商业级画质的等效复刻。没有改动飞行动力学、训练目标或旧记录格式。

## 已接入的资产与效果

- 草地、风化岩石、哑光无人机外壳：3 张原创 AI albedo，加一张 RGBA 针叶卡片，共 4 张，原图 1254 × 1254；真实用于场景，不是仅放在设计文档的概念效果图。
- 地面：起伏网格、三平面贴图、坡面岩石混合、低频色彩变化；原训练走廊与 Home 附近保持平坦。
- 海岸：侵蚀岸石、水面波纹、视角反射近似、日光闪点与岸边泡沫。
- 植被：有树干、带真实 alpha 的分层针叶卡片与随机旋转/尺度的松树，代替椭球“棒棒糖”；近地低灌木和小石子。
- 天空：渐变大气、程序云、太阳方向，配合距离雾、暖色太阳与实时软阴影；Clear / Overcast / 傍晚分开处理。
- 无人机：圆滑机身、底盘与电池接缝、折叠臂、铰链、电机、导航灯、通风口、避障镜头、云台与相机玻璃、桨叶与旋转模糊盘。模型原创，无 DJI 商标。
- 城市/仓库：窗口带、楼顶设备、桥墩顶盖、桥面道路与栏杆、集装箱波纹和地面标线。
- 采用现有 Built-in Unity 渲染器与 Standard 光照；没有要求升级 URP、购买素材包或安装外部软件。

## 真实渲染图册

下列画面来自新 macOS 程序中的实际 Unity 场景。带 HUD 的图是飞行页面截图；无 HUD 的图由验收相机换角度拍摄，使用相同几何、材质和灯光，不是 AI 合成场景。

### 海岸飞行

![实际飞行 HUD](verification/graphics-v2/pacific-coast-flight.png)

### 无人机细节

![无人机实际模型近景](verification/graphics-v2/drone-detail.png)

### 海岸、峡谷、城市巡检与仓库

![海岸](verification/graphics-v2/coast-panorama.png)
![峡谷](verification/graphics-v2/desert-range-scene.png)
![城市巡检](verification/graphics-v2/city-park-scene.png)
![仓库](verification/graphics-v2/warehouse-scene.png)

### 傍晚与阴天

![傍晚](verification/graphics-v2/coast-evening.png)
![阴天](verification/graphics-v2/coast-overcast.png)

## 可复用的美术源文件

资产目录：`AeroSim/Assets/AeroSim/Resources/Art/MaterialsV2/`。

完整生成提示词、工具来源和素材限制：`PROMPTS.md`，使用内置 imagegen，没有 API / CLI fallback。三个表面贴图要求正投影、均匀去光照、可平铺；针叶要求透明背景。均无文字、商标和水印。

场景/无人机模型由 `FlightSimulator.Visuals.cs` 可复现地生成，所有网格共享/追踪并在结束飞行时释放；新材质 shader 放在 Resources，避免构建时被剥离。主飞行逻辑仍在原 `FlightSimulator.cs`。

## iPad / iPhone 考虑

素材导入器启用 mipmaps、表面 Repeat / 针叶 Clamp、Trilinear 和 anisotropic filtering；针叶保留 mipmap alpha 覆盖。iOS 采用 1024 上限、ASTC 6×6 压缩；移动平台降低树木与近景细节数量，阴影距离 65 米、2 层级联、2× MSAA。桌面 High 为 150 米阴影、4 层级联、4× MSAA。使用原有 Unity / Input System / UI Toolkit 跨平台架构。

这只是移动画质预算起点，不是已完成 iPhone/iPad 真机性能验收。需要完整 Xcode、用户 Apple Development Team 和实际设备测量。

## 与商业级目标的差距

当前贴图只有 albedo，没有摄影测量的 normal / roughness / AO / displacement 全套贴图。AI 请求了可平铺，但并不保证数学无缝；三平面映射与宏观颜色变化减少突兀重复。水面是低成本实时近似，没有真实海洋动力学、屏幕空间反射或波浪碰撞。

树木和无人机仍是程序化原型模型，不是经过 DCC 艺术家拓扑、UV、LOD 与风动画制作的资产。进入商业交付还需专业精修无人机模型、扫描地形/植被、完整 PBR 套图、LOD/实例化与移动机型分档。不能把本轮提升等同于“已经达到 DJI 商用水平”。

## 运行和回退

验收：最终 macOS 构建 48 项通过、0 失败，四环境/无人机/天气共 12 张真实渲染图已实看，图册包含 17 张图片（含原素材与旧版对照）。桌面海岸 120 帧短测约 60 FPS，不替代移动性能测试；iOS 工程成功导出，未签名或真机运行。详细结果见 `verification/GRAPHICS_V2.md`。

新版：`AeroSim/Builds/macOS-ArtV2/AeroSim.app`。旧版：`AeroSim/Builds/macOS/AeroSim.app`，未被覆盖。

两版共用正常用户档案；测试则使用独立的 `--data-dir`，不预填或修改正常记录。此任务未增加网络上传、账号、素材订阅或第三方依赖。

编辑器菜单：AeroSim → Build Art V2 Preview。命令行方法：`AeroSim.Editor.ProjectSetup.BuildArtPreview`。新诊断参数 `--capture-graphics <目录>` 可复现美术验收图册。
