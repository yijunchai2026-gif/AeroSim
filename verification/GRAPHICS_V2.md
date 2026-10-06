# AeroSim 0.3 美术 V2 验证

2026-10-05，Apple M3 Pro / macOS / Unity 6000.3.25f1，Built-in 渲染器，Linear 色彩。

## 构建与素材

- `ProjectSetup.BuildArtPreview` 成功，独立输出 `AeroSim/Builds/macOS-ArtV2/AeroSim.app`，没有覆盖 0.2 应用。
- 4 张内置 imagegen 原创资产已复制到工程 Resources，完整提示词保存于 `Assets/AeroSim/Resources/Art/MaterialsV2/PROMPTS.md`。原图 1254×1254；针叶有 alpha。
- 实际导入参数已核对：表面 Repeat / 针叶 Clamp，Trilinear，aniso 8，mipmaps，针叶 mipmap 覆盖保持，alpha cutoff .42，iPhone override ASTC 6×6、1024 上限。
- Shader 在 Resources 中打包，运行时检查 6 个 shader 均 supported，4 张贴图均可加载。
- 新 iOS 工程成功导出到 `AeroSim/Builds/iOS`，Metal shader / IL2CPP 资源生成阶段无构建错误。没有完整 Xcode，未原生编译、签名、安装或完成真机验收。
- 一项已有非阻塞 C# 提示：UI 背景裁切使用 obsolete `unityBackgroundScaleMode`。本轮没有引入托管异常或 shader 编译错误。

## 功能复验

最终程序执行 `--smoke-test`，48 项通过、0 失败，详见 `graphics-v2-tests.txt`。原有 38 项，加 4 张贴图打包检查与 6 个 shader 支持检查。

测试覆盖鼠标启动、键盘油门、暂停/继续、虚拟 Gamepad、设备断连、触控输入通道、门序列、碰撞记分、遥测保存、结果页面、回放、返航落地、巡检照片、导出与档案重载。路径/目标测试有设置刚体位置的步骤，不等于真人完成所有课程。

隔离最终 QA 目录：`/private/tmp/aerosim-art-v2-release-qa-20261005`。不修改正常用户成绩与照片。

## 实际画面验收

`graphics-v2/` 包含 12 张实际运行图，覆盖四环境飞行 HUD / 无 HUD 场景、无人机近景、海岸全景、傍晚与阴天。无 HUD 场景使用额外 Unity 验收相机，模型/材质/灯光与实际飞行一致，未使用 AI 概念图替换运行结果。

第一轮截图另存 `graphics-v2-pass1/`，可看到针对规则波纹、几何树冠与分块远山的迭代。0.2 原 UI 图册仍保留 `desktop/`。

原生应用实际点击 Free Flight → Launch Free Flight，确认新的纹理、针叶、远山和飞机出现，再点击 Pause，出现 Resume Flight。留在暂停状态供用户继续检查，没有保存示范成绩。

新增可视化文档 `AeroSim-美术升级V2.html` 有 17 张图片，原实现文档增加最新实际飞行和飞机近景；两份 HTML 已检查本地图片路径，无缺失引用。

内置浏览器工具拒绝自动访问 `file://`（只允许 http/https），因此没有通过浏览器工具验证 HTML 整页布局，也没有尝试绕过该限制。文件已保存，可由用户在本机手动打开；图册中的全部 Unity 场景图已直接实看。

## 性能与限制

桌面 High / 1680×946 / 海岸场景的 120 帧短测约 60 FPS（具体数值见测试文件）；60 FPS 是目标帧率限制下的观察，不是最大性能测量、不是真机移动 benchmark，也没有量化电量、显存、发热或最坏帧。

移动预算已设置：较少植被/近景物件、65m 阴影、2 层级联、2× MSAA 与 ASTC。共享树冠/岩石网格材质启用 GPU instancing；还没有专门的树木 LOD / 地形流式加载系统。

画面明显超过纯色占位版，但仍是程序模型与 AI albedo 原型。无完整扫描 PBR 套图、专业 DCC 网格/UV、真实水体或摄影测量场景；不宣称达到 DJI 商用画质。
