# AeroSim 1.0 技术实施与验收方案

日期：2026-10-08。状态：实施方案，不是已完成声明。目标分支：`codex/v1-design-parity`；保留 `codex/v04-ui-proportions` 和 0.4 构建不变。

## 1. 目标与证据

用户要求将三条产品线做到 1.0，并按原产品效果图验收。目标是 `assets/aerosim-us-01…22` 加 `aerosim-us-flight-training-ui-v1.png` 的 **23 张概念效果图**，不是把 `assets/ui-v04/` 的现状截图改名为 1.0 设计稿。产品文档第 18、19 章给出交互和美国本地化，第 23 章用于对照当前差距。

本方案已逐张查看全部 23 张概念图，并读过现有 Unity 页面、HUD、响应式、机库、存档和模拟器源码。概念图的拓扑差距很大，不可能只修改颜色、边距和圆角就宣称完成。源图片实测 1672×941；参照页映射和逐页缺口见 [V1_SCREEN_SPEC.md](V1_SCREEN_SPEC.md)。

项目实际配置是 Unity **6000.3.25f1**，Input System 1.16.0。继续 Unity 原生实现；HTML 图册仅作产品文档和验收工具，不能用网页截图代替真实可飞行应用。

## 2. 平台与关键实现选择

### 2.1 继续 UI Toolkit，不整体切换框架

现有 `AeroSimApplication` partial、Domain、FlightSimulator 和 Input 适配保留。已有选机、存档、回放、校准和训练闭环不能因重做界面而丢失。业务状态与新视图绑定；不让每个屏幕直接复制存档文件处理代码。

UI Toolkit 使用 Flex 布局及其受支持的 USS 属性。不能把网页 CSS Grid、浏览器滤镜等代码直接搬入 USS 后当作有效样式；先在当前编辑器编译和截图验证。[Unity 6.3 布局引擎](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-LayoutEngine.html)、[USS 属性表](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-USS-Properties-Reference.html)。

### 2.2 3D 与 UI 一起呈现

航拍和 FPV 飞行仍由真实 Unity Camera、Rigidbody、Input 和碰撞生成画面；专业 HUD 必须有任务目标与实际照片动作，不能以整张桥梁图假装动态巡检。回放和任务编辑器的中间视口可用 Camera→RenderTexture→UI Toolkit 背景/Image；现有 Hangar 已有可参考的 RenderTexture 实现。必须释放 RenderTexture、恢复相机 targetTexture、避免每帧重新创建资源。[Unity 6.3 RenderTexture 背景 API](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/UIElements.Background.FromRenderTexture.html)。

地图面板首选当前场景的正交俯视相机与坐标投影，在其上画路径、目标和限制区。这是模拟场景地图，不是 Google Maps。若桥梁概念图只做为简报插画，标注“Training environment preview”，不要让点选插画产生错误的世界坐标。

遥测折线、学习路径连线、校准轴与事件时间线用 `generateVisualContent` / `Painter2D` 自绘，数据源取现有 records/frames/events 和真实输入。不要烘焙曲线和数值进背景图；颜色、选择和焦点状态仍由可交互元素表达。[Unity 6.3 自绘二维内容](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-generate-2d-visual-content.html)。

### 2.3 响应式与像素基准并存

建立两层：1920×1080 基准下源图归一后的严格视觉规范；其他比例下保留阅读、动作可达和 HUD 安全区的重排规范。UI Scale、HUD Scale 和 Text Size 独立保持；勿用一张整屏位图做全局拉伸来满足像素截图。

`PanelSettings` 支持像素、物理尺寸和参照分辨率等模式；项目选用的模式与用户 scale 要显式记录，避免既做全屏参照缩放又按 DPI 再乘一次。文本比例不能再次放大图标。Auto 在运行时尺寸变化后重新计算。[Unity 6.3 PanelSettings](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-Runtime-Panel-Settings.html)。

图稿和旧文字规范冲突：概念顶栏约 76–82 源图像素，归一后约 87–94；旧 PRD 指定 64 逻辑像素。以用户最新“跟效果图一模一样”作为本次视觉目标，给每张图记录实际边界；旧 64 数字不应成为 QA 的唯一视觉真值。无障碍/安全和真实数据语义仍是硬约束；不能为图稿复制错误数据。

集成决定：`design-v1` 共享菜单壳先采用 **顶部88、侧导航248** 的1920基准，按每张源图需要覆写区域值（FPV菜单侧栏更宽，FPV首页无侧栏、竞速HUD无菜单壳）。这是源图视觉测量优先，不是把图片改成现状。88/248仅是实施起点，不能替代每张图的边界QA；偏离原图细节仍记FAIL，不因为符合新常量就算通过。

### 2.4 保留 iPad/iOS 共享路线

布局按可用逻辑窗口而非设备营销型号判断。安全区取 `Screen.safeArea`，从屏幕底左原点转换到 UI 顶左坐标时顶部间距应使用 `Screen.height - safeArea.yMax`，再除面板缩放；不要把 yMin 直接当顶部 inset。触控按钮不依赖 hover，最小触控目标按移动端规范单独验收。[Unity 6.3 safeArea](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Screen-safeArea.html)。

Unity 导出 Xcode 工程后，仍需 Xcode 构建、签名和实机验收。导出成功不能称 iOS 发布成功，桌面模拟手机视口不能称 iPhone/iPad 真机通过。[Unity 6.3 iOS 构建流程](https://docs.unity3d.com/6000.3/Documentation/Manual/iphone-BuildProcess.html)。这次桌面 1.0 保留代码兼容路线；缺失真实移动端硬件/账号条件记录为独立验收项目，不虚构证据。

## 3. 必须实现的结构升级

| 模块 | 0.4 证据 | 1.0 实施方向 |
|---|---|---|
| 应用壳 | 字母/三角图标、同一顶部状态、工作区切换在侧栏 | 原创可缩放图标与品牌；按图稿工作区顶栏、侧栏与无侧栏屏分模板；真实设备状态 |
| 航拍 | 首页接近层次，其他多为通用长表单 | 五步引导、校准三列、学院分类/路径/详情、自由飞大图/场景条/参数栏/底栏 |
| FPV | 首页与专业共用 WorkspaceHome；挑战四张卡；单参数长表单 | 全幅工业首页，3×2挑战+详情，三栏调参和 OSD 编辑，极简竞速 HUD |
| 专业 | 卡片任务目录，坐标文本编辑，三目标任务 | 仪表盘、可筛选目录表、双栏简报/飞检、对象/地图/属性/时间线编辑器、目标右栏 HUD |
| 结果与回放 | 长结果表，回放复用飞行 HUD | 静帧结果+真实评分，3D视口/遥测图/输入图/时间线/事件跳转/导出 |
| 美术 | 部分低分辨率概念图裁切被拉大 | 无字高分辨率场景插画、控制器透明图、机型材质和实际场景物体；素材不承担真实控件 |

## 4. 分波并行与文件所有权

工具并发总共 4 槽（含主集成人）。采用短波次并交叉角色，不把“几个程序员、美工”解释成无限同时运行，也不假称有实体雇员。

| 角色/波次 | 独占写文件 | 禁止写的共享处 |
|---|---|---|
| 技术负责人 | 本方案、`V1_SCREEN_SPEC.md` | 不改代码、不构建、不推送 |
| 主页面集成人 | `AeroSimApplication.cs`、Pages路由/公共 helpers、Responsive.cs、Domain迁移、统一加载新样式 | 不覆盖其他人的 partial 文件 |
| 航拍/公共程序员 | 新 `AeroSimApplication.DesignAerial.cs`、`DesignAerial.uss` | 不直接改 FPV/Professional/HUD 方法 |
| FPV 程序员 | 新 `AeroSimApplication.DesignFpv.cs`、`DesignFpv.uss` | 不直接改公共路由/Domain，需求用接口通知集成人 |
| 专业程序员 | 新 `AeroSimApplication.DesignProfessional.cs`、`DesignProfessional.uss` | 同上 |
| HUD/回放程序员 | Flight.cs 或独立 DesignFlight.cs、`DesignFlight.uss` | 响应式全局逻辑由集成人统一 |
| QA | `verification/v1-*` harness、摘要和差异报告、被允许的QA诊断接口 | 不改目标概念图/样式来使结果通过 |
| 场景美术 | `assets/v1-art/` 场景无字素材、素材清单 | 不改原23图、页面布局和测试真值 |
| 机型/设备美术 | `assets/v1-art/` 透明设备图、材质贴图、素材清单 | 不生成用于冒充真实3D模型的整屏画面 |

美术后续可在空槽生产，QA 在程序员交付后独立执行，不只由实现者给自己打分。共享方法要先拆分再并行；多个角色同改 Pages.cs 会产生冲突。Unity Editor/构建操作只由主集成人执行，避免同工程多实例锁。

## 5. 实施批次与门禁

1. **R1 参照冻结与共同组件**：23张图和30状态映射；测量源图边界；图标/字体/颜色；页模板拆分。逐图原文与语义更正表，不重画一套更容易实现的目标图。
2. **R2 航拍/公共**：引导、校准、首页、学院、自由飞、美国准备、设置、结果。既有任务链路与六机型继续工作。
3. **R3 FPV + 美术**：首页/挑战/调参/OSD编辑四屏；挑战路线与圈数等数据模型真实补齐，不装饰成已实现。高分辨率无字美术与程序接口一起集成。
4. **R4 专业 + HUD/回放**：密集目录、简报、飞检、任务编辑、报告、3种HUD与回放。目标数据/照片/时间线要共用实际会话。
5. **R5 独立验收与候选**：30流程回归、23视觉对照、缩放/输入/存档/内存/性能；生成运行图册与缺陷记录。仅达到门禁后标 1.0 验收候选。用户完成体验前状态为“等待人工验收”，不是已验收。

每个批次验证后由主集成人只提交相关文件并 push，核对远端 SHA，提供仓库、分支与 commit 链接。不得自行合并 main、force-push 或替用户确认 milestone。

## 6. 不可通过假数据获得一致

- 菜单中的 GPS 18、Link 96%、Battery 82% 是概念示例，当前无飞机连接。普通菜单用本地训练/输入就绪；飞行中若显示定位/链路，明确为模拟值并建立模型，不称真实设备遥测。
- 成绩、期限、名字、资格、课程解锁和个人最佳来自档案/规则，不把图中 Alex、88、2024 到期等装成用户数据。视觉测试可用隔离 fixture，图册永久标明 fixture，正常启动不自动种入测试成绩。
- 13 的四远程学员实时视频、延迟和请求接管是服务功能，不是四幅随机录像。需要真实传输、身份/同意、会话状态和审计；若当前批次只完成本地回放复核，应标明缺项，**不能减少该要求再声称完整1.0**。
- 图 17 的 PDF 不是把 HTML 文件扩展名改 PDF；可先保留“Print / Save as PDF”真实功能，但此差异留在缺陷表。Image Quality 若仅按距离/顺序判定，不显示有图像分析得出的“Good”。
- 图 18 属性若没有实际效果就禁用并解释，不挂空按钮；撤销/重做、预设、像素/归一坐标要一致。图 22 内容更新若没有服务必须明确离线，不能虚构更新成功。
- Google Maps、全美禁区和 FAA 授权不是此次布局改造自动实现的功能。真实服务需要用户账号/API/费用/使用条件和权威数据来源；无需为本次视觉实施开启付费服务。模拟边界始终标“SIMULATION — NOT FOR NAVIGATION”。

## 7. 视觉与功能验收不能互相替代

23张图逐页记录参照、实际、透明叠加、差异热图、主结构边界、字级/图标/色彩和各差异。图稿源像素、归一规则、字体、视口、scale、seed、fixture、时间点和构建SHA固定。动态3D像素必须独立报告；不能把整个场景掩码后给全屏“100%一致”。

建议像素指标为候选检测：平色UI区域每通道平均差、SSIM、边缘位置偏差和文字框偏差；图标/边界可用≤2px的目标，但这只是待执行验收阈值，非已测成绩。多指标通过仍需逐页人工视觉检查。“几何0失败”不能等价效果图一致，0.4 的8551文字候选不得隐去。

30状态需测试每个主操作、Back、工作区切换、选择/过滤、保存/取消、断连、安全暂停、RTH、结果/回放/报告、六机型持续选择；拒绝空按钮。独立QA覆盖1280×720、1920×1080、超宽、4K缩放、iPad逻辑视口、手机横/竖和Text200%；移动实机单列。

验收交付必须列出：23图匹配状态、所有真实流程结果、剩余差异与阻塞、运行应用路径/版本/哈希、图文产品文档、GitHub提交。未达到原图目标和功能门禁时保持候选/开发状态，不仅改版本号宣布完成。
