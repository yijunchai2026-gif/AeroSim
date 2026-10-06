# AeroSim 0.3 — 离线功能版 / 美术 V2

## 打开应用

双击新版 `Builds/macOS-ArtV2/AeroSim.app`。旧版 `Builds/macOS/AeroSim.app` 保留，方便回退。首次启动有三步引导；也可以先从侧栏进入 Free Flight，滚动到页面下方点 Launch Free Flight。请勿用旧进程判断当前版本。

这是可交互的本地 MVP，不是 DJI 官方应用，也不是商业级 1:1 拟真模拟器。

美术 V2：原创草地、岩石、外壳、针叶素材；连续地形与远山、针叶卡片植被、天空云、水面近似、软阴影；无人机增加圆滑机身、云台镜头、电机/桨叶等结构。详见外层 `ART_DIRECTION_V2.md` 和 `AeroSim-美术升级V2.html`（真实渲染图册）。源贴图与完整生成提示词位于 `Assets/AeroSim/Resources/Art/MaterialsV2/`。

## 操作

| 输入 | 行为 |
|---|---|
| W / S、A / D | 前后、左右；Manual 模式对应 pitch / roll |
| R / F（或 Page Up / Down） | 上升 / 下降油门 |
| Q / E | 航向旋转 |
| H | Return-to-Home，推动摇杆可接管 |
| C | 追踪 / FPV 视角切换 |
| P | 保存训练照片（先起飞） |
| Backspace | 重置到 Home Pad |
| Esc | 暂停 / 继续 |
| 手柄双摇杆 | Mode 2：左杆油门/航向，右杆俯仰/横滚 |
| 手柄 Start / 右肩键 | 暂停 / 拍照 |
| 屏幕双摇杆 | 鼠标或触控拖动；松开回中 |

Manual 模式不会自动扶正，松杆不等于制动。先在 Normal 模式练习。这个 MVP 没有自动避障，障碍物会造成碰撞。

## 三条路线

- 航拍：Home → Academy → Lesson Brief → Preflight Check → Start Session → Finish Session → Results → Replay / Report。
- FPV：左上切工作区 → Tuning / OSD Editor → Free Flight 或 Challenges → 飞行 → 结果 / 回放。
- 专业：切 Professional → Training Catalog / Mission Editor → Mission Brief → Preflight → 有序目标拍照 → 结果 / 本地 Instructor 批注 / 导出。

所有主页面均可操作。专业 Instructor 是单机工作流，不存在远程学员房间、联网账号或假在线状态。

## 存储与导出

正常用户数据：`~/Library/Application Support/AeroSim Labs/AeroSim/AeroSim/`（运行时 Settings 页显示准确路径）。

- `profile.json` 与最近一次备份：设置、课程进度、任务、记录索引。
- `<session-id>.json`：时间、位置、姿态、输入和事件；遥测采样约 10 Hz。
- `<session-id>-photo-N.png`：1280×720 的场景相机图像，不含 HUD。
- `<session-id>-telemetry.csv`：结构化遥测；长度/速度字段统一 SI 单位。
- `<session-id>-report.html`：可打印报告；用浏览器 Print / Save as PDF 输出 PDF。

此版本不上传任何记录。官方 FAA 链接在外部浏览器打开，不代表 AeroSim 对真实飞行资格、空域或法规做了验证。

## 开发与复验

Unity 6000.3.25f1，打开此文件所在的 `AeroSim` 工程目录，不是外层文档目录。编辑器菜单：AeroSim / Configure Project、Build Art V2 Preview。

```sh
/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath "$PWD" \
  -executeMethod AeroSim.Editor.ProjectSetup.BuildArtPreview \
  -logFile /private/tmp/aerosim-build.log

./Builds/macOS-ArtV2/AeroSim.app/Contents/MacOS/AeroSim \
  --smoke-test --data-dir /private/tmp/aerosim-isolated-test \
  --test-results /private/tmp/aerosim-test-results.txt \
  --capture-all /private/tmp/aerosim-screens \
  --capture-graphics /private/tmp/aerosim-art-screens \
  -screen-width 1680 -screen-height 945 -screen-fullscreen 0
```

复验应使用新的独立 `--data-dir`，因为测试会核对新增记录数量。自动化覆盖页面构建、真实指针事件启动、虚拟键盘/手柄、触控输入通道、暂停、门序列、碰撞记分、存盘、回放、RTH、巡检与照片、导出、档案重载。穿门/巡检测试会把飞机置于目标位置；它验证规则，不等于真人完成课程，也不是遥控器真机测试。

## iPad / iPhone

共用 Unity 飞行域、Input System、UI Toolkit 和本地存储，移动平台采用逻辑点缩放、安全区和双虚拟摇杆。菜单主要支持触控/鼠标/键盘，完整手柄焦点导航仍待补齐。

```sh
/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath "$PWD" \
  -executeMethod AeroSim.Editor.ProjectSetup.BuildIosExport \
  -logFile /private/tmp/aerosim-ios.log
```

导出目录 `Builds/iOS/Unity-iPhone.xcodeproj`。目前这台电脑只有 Command Line Tools，没有完整 Xcode：不能在此完成签名、运行 iOS Simulator、安装真机或提交商店。需安装 Xcode、选择用户自己的 Apple Development Team，并在 iPad/iPhone 实测多点触控、MFi、性能和文件分享。桌面小窗口截图只是布局检查，不是 iOS 真机截图。
