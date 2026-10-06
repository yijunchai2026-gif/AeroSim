# AeroSim 开发路线与交付状态

> 2026-10-05：根据用户“直接完成最后一个 milestone”的最新指令，取消阶段间人工等待，继续完成 M2–M6 的本地可运行 MVP。此前 M1 的只读占位入口已替换。这里的完成范围是离线功能版，不表示原 PRD 的商业级拟真、远程多人和商店发布全部实现。

## 平台决策

- 引擎：Unity 6.3 LTS（Apple Silicon 编辑器已安装）。
- UI：UI Toolkit，共享桌面、iPadOS、iOS 的组件与设计变量。
- 输入：Unity Input System；业务层只接收归一化飞行指令，桌面 HID/遥控器、键鼠、手柄、iOS 触控与 MFi 手柄分别由适配器处理。
- 首发目标：macOS 桌面可运行原型；工程同时保留 Windows、iPadOS、iOS 输出能力。
- iOS 限制：首版承诺触控和 Apple Game Controller/MFi；第三方无人机遥控器是否可用取决于厂商对 iOS 的协议、SDK 与认证，不作为默认承诺。

## 架构边界

```text
UI Toolkit Views
      ↓
Application / Navigation / Session
      ↓
Flight Domain (platform independent)
      ↓
Input Adapters ─ Simulation Adapter ─ Persistence Adapter
   Desktop HID       Unity Physics       Local profile
   Gamepad/MFi
   iOS Touch
```

飞行物理、课程判定、成绩计算和任务状态不得直接依赖桌面 API，确保移动端只更换输入与显示适配层。

## Milestones

M1–M6 保留为功能分组，当前不再作为继续开发的人工审批门。

1. **M1 — Foundation + Aerial Home**
   - Unity 工程骨架、设计变量、响应式应用壳
   - Aerial / FPV / Professional 工作区切换
   - 航拍训练首页与主要微交互
   - macOS 可运行预览
2. **M2 — Onboarding + Controller**
   - 首次启动、学习路线选择、设备检测
   - 遥控器校准、摇杆实时输入、错误与重试状态
3. **M3 — Aerial Flight Loop**
   - Academy、Free Flight、HUD、暂停/RTH、结果页
   - 基础飞行物理、碰撞、课程判定
4. **M4 — FPV Workspace**
   - FPV 首页、挑战、调参、OSD 与飞行页面
5. **M5 — Professional Workspace**
   - 训练目录、任务编辑、任务简报、飞前检查、教员与报告
6. **M6 — Productization + Mobile Readiness**
   - 回放分析、设置、无障碍、完整状态覆盖
   - iPad/iPhone 响应式适配、触控飞行界面、iOS Xcode 工程导出验证；签名与真机测试另行验收
   - 性能、存档迁移、桌面打包

## 当前离线交付验收清单

- 启动后首页视觉方向与 `assets/aerosim-us-03-aerial-home.png` 一致。
- 顶栏、侧边栏、核心卡片在 16:9 桌面窗口中无溢出或遮挡。
- 工作区菜单可打开；Aerial、FPV、Professional 均有可操作页面。
- 侧边导航、课程、调参、OSD、任务、报告与设置均有实际行为，不再显示“后续里程碑”占位提示。
- 窗口缩窄至 iPad 横屏比例时，内容仍可阅读与滚动，不依赖鼠标 hover 才能操作。
- Controller 卡片能根据实际输入设备展示键鼠、手柄、遥控杆或触控。

## 实现范围

| Milestone | 本地已实现 | 限制 |
|---|---|---|
| M1 | 应用壳、三工作区、响应式首页 | 保留早期概念图裁切的卡片插画 |
| M2 | 三步引导、实时轴、Joystick 轴映射、死区/反向、中心/范围校准 | 无实物 DJI 遥控器在场；虚拟设备测试不能替代真机兼容性 |
| M3 | 课程简报/检查单、四环境、辅助/Manual 飞行、碰撞、RTH、着陆、照片、结果 | 简化刚体控制，不是商业级四旋翼动力学；课程复用通用判定 |
| M4 | FPV 首页、12 个课程入口、四地图竞速、rate/expo/推重比/相机角度、拖拽 OSD | 四挑战共享六门路线；无在线排行 |
| M5 | 任务参数/目标编辑、有序拍照、时间限制、本地分配与教员批注、报告 | 无远程教室/联网多人；照片判定仅距离/顺序，未分析图像质量 |
| M6 | 本地保存、10 Hz 遥测、回放/拖动/速度/视角、CSV/可打印 HTML、设置、触控、安全区、桌面打包、iOS 工程导出 | HTML 可通过浏览器保存 PDF；尚未完成原生分享、VoiceOver、全手柄菜单、签名与真机验收 |

自动化结果与真实页面截图见 `verification/`。诊断使用独立临时档案，不会给正常用户档案写入测试成绩。后续商业版仍需地图/机型专属资产、精细课程、真实控制器标定、低端设备性能测试，以及后端与发布流程。
