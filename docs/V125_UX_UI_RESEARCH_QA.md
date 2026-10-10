# AeroSim 1.25 UX / UI / 场景调研独立 QA

日期：2026-10-10（America/Los_Angeles）。结论：**PASS_DOCUMENT_REVIEW_ONLY**，仅为冻结调研文档、证据边界和保护性检查通过，不签应用实现、视觉一致性或设备验收。

## 1. 范围与冻结

独立 QA 全文阅读[综合调研](V125_UX_UI_OPTIMIZATION_RESEARCH.md)、[UX 流程](V125_UX_FLOW_AUDIT.md)、[UI / 移动布局](V125_UI_MOBILE_AUDIT.md)、[场景真实感](V125_SCENE_REALISM_AUDIT.md)，检查作者停写后的[5 个冻结指纹](../verification/v125-qa/ux-ui-research-freeze.json)。[31 文件基线](../verification/v125-qa/ux-ui-research-baseline.json)锁定审查开始时的 PRD、当前源码/USS/shader/管线设置、目录和部分图稿；**含原有未提交源码，不是候选08构建冻结或源码已上传证明**。

本成员仅新增本文与[紧凑结果](../verification/v125-qa/ux-ui-research-results.json)，未修改四份报告、PRD、业务源码、图片、批准状态或历史 QA；未启动 Unity、构建应用、跑 native 操作、连接控制器或验证 iPad/iPhone 真机。Git 提交、推送和远端 SHA 由根负责人另收尾，不在本文提前签字。

| 冻结报告 | SHA-256 |
| --- | --- |
| 综合 | `5d2fd6f98fdb3a7f92dac341c33bb65d7356e0a59438b98160e3472765c971f0` |
| UX | `562320981494bc546f6664b18542b3021b4009801e524b3755b38ef0c101b7bf` |
| UI / 移动 | `4ce49b8d70e04c285c9ac84e7cfe2853d9a7f6c4c69ce103622bc55873ccc547` |
| 场景 | `a2d3ed25cbafa80960da5dd9aeb415066b13a8dc1d670018d8281ddc337cfa67` |

## 2. 独立审查发现与修正结果

| 编号 | 初次问题 | 根负责人 / 作者修订后复核 |
| --- | --- | --- |
| Q01 | UI 首稿 M08、5.1 与持久化矩阵写全部设置 draft＋Save/Cancel，和 UX / 当前 PRD 三项倍率独立即时预览与保存冲突。 | CLOSED_DOCUMENT：三报告现统一 UI Scale、HUD Scale、Text Size 各自即时预览 / 本地保存、真实成功或失败反馈；飞参 / 校准 / 任务 / OSD 独立草稿，不被普通 Save 间接提交。并未改实现，事务与写盘失败测试仍待执行。UI 保留首稿及中间稿 SHA 记录。 |
| Q02 | UI 矩阵用“全30实际状态”，将旧映射、PRD 状态与课程目标口径混在一起。 | CLOSED_DOCUMENT：改为全部实际页面与交互上下文，按后续冻结实现清单核对；原23概念图、PRD历史34状态和30课程目标不互相代签，也不改历史计数。 |

最终冻结四报告在本范围内没有发现新的阻断性前后冲突。P0/P1 是产品风险排序，不是已实测严重度或真实失控记录。建议改变原图导航时需先确认新的派生目标，不覆盖23原图来制造通过。

## 3. 关键证据抽查

| 核查项 | 独立观察与可得结论 |
| --- | --- |
| 输入 / 生命周期 | [Flight.cs](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.Flight.cs) L120–157、218–251 确有直接 Pause 翻转、按弹层先前状态恢复、Down 覆盖 pointer、静态轴写入；[FlightInput](../AeroSim/Assets/AeroSim/Runtime/Input/FlightInput.cs) L14–54 确有多源相加与全局校准。仅证实静态路径风险，没有复现真机卡杆。 |
| 多入口语义 | [FlightDesignV1](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.FlightDesignV1.cs) L121、163、167 直接 RTH；L220–226 暂停复位 / 退出路径；[Simulator](../AeroSim/Assets/AeroSim/Runtime/Simulation/FlightSimulator.cs) L203–208 独立键盘轮询。报告没有误把候选08已修 Space / 首次 Tab 重新写成未修失败。 |
| 返回 / 保存 | [DesignV1](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.DesignV1.cs) L145、213–244、279–314 的固定返回、重建配置与混合保存匹配审查；[Pages](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.Pages.cs) L40–51 路由重建及停止逻辑支持草稿风险判断。建议与已实现明确分开。 |
| 点、像素与安全区 | [AppBootstrap](../AeroSim/Assets/AeroSim/Runtime/AppBootstrap.cs) L19–23、[Responsive](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.Responsive.cs) L39–45 的 dpi/163 不是 UIKit pt 映射；[Application](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.cs) L421–446 与 FlightDesignV1 L453–458 需统一坐标验证。报告没有直接宣判重复扣 safeArea。44pt 为项目触点建议，不冒充44个 Unity px 或本机测得值。 |
| 管线 / 材质 | [manifest](../AeroSim/Packages/manifest.json) 无 URP/HDRP；[GraphicsSettings](../AeroSim/ProjectSettings/GraphicsSettings.asset) 自定义管线0，六档[QualitySettings](../AeroSim/ProjectSettings/QualitySettings.asset)各为0。[Landscape](../AeroSim/Assets/AeroSim/Resources/LandscapeSurface.shader) L15、24–30 确为 Standard、颜色三向投射、坡层XZ、常量 Smoothness/AO，非“完全无PBR”。 |
| 场景 / 导入 | [Visuals](../AeroSim/Assets/AeroSim/Runtime/Simulation/FlightSimulator.Visuals.cs) 草地5m / 岩石代树皮及沥青，网格数量推导可核对；[Environment](../AeroSim/Assets/AeroSim/Runtime/Simulation/FlightSimulator.EnvironmentV1.cs) 与海/河 shader 支持“已有程序化表面、水面但响应不足”的表述。MaterialsV2 meta 有 mips / aniso8 / iOS1024、format50 override；设置不等于实测驻留格式。 |
| 预算 / 关闭条件 | 综合优先表逐项有可测关闭条件；UI 的布局、倍率、触点、生命周期、持久化和无障碍矩阵及场景第6–7节有设备、构建、对照、热稳定 / 内存 / 帧时方法。数值均 proposal / NOT_RUN，不冒充测得三端 GPU 预算、60FPS保证或所有设备支持。 |

行号是当前工作区粗定位，不把旧截图绑定到尚未提交源码。独立联网抽查官方 Apple 布局 / 游戏默认触点和 Unity safeArea / Surface Shader兼容 / ASTC6×6定义：方向正确；Apple HIG 普通 open 为 JS 提示时，用官方搜索正文与官方视频交叉核对，未假称在浏览器实拍 HIG。44pt 默认建议可见[Apple 官方游戏讲解](https://developer.apple.com/videos/play/wwdc2024/10085/?time=29)；安全区是 Player 窗口像素且需翻Y见[Unity safeArea](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Screen-safeArea.html)；格式50见[Unity6000.3 TextureImporterFormat](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityeditor/textureimporterformat/astc-6x6)。不是宣称 AeroSim 已满足这些规范。

本 QA 通过 view_image(original) 实看9张：08首页1440、08回放1920；原航拍 / FPV / 专业三个HUD目标；05三种飞行截图；0.3历史海岸。地表重复、粗山体与照片背景、工业/桥体规则细部差异可观察；时间、相机、状态不同，不能逐像素比较或测得材质改善。没有人眼验收全部23页，也没有从PNG推算FPS或触点手感。

## 4. 实际执行的保护性检查

| 检查 | 本轮独立结果 | 限制 |
| --- | --- | --- |
| 冻结报告＋基线指纹 | 5 / 5一致 | 文档冻结，不是构建冻结。 |
| 31源守卫 | 31 / 31一致 | 保留开始时dirty内容，不签实现或上传。 |
| 原概念 / 参考manifest | 23 / 23图与1 / 1清单一致 | 字节保护，不是视觉通过。 |
| 旧 Mavic / 08 精选 / 05三飞行图 | 4 / 4、6 / 6、3 / 3字节一致；2016完整current行未变 | 05/08历史证据各自版本与倍率，不能代新设备结果。 |
| PRD＋四调研报告 --require-tracked | 1075检查 / 0失败；329处本地文件引用；待发布依赖0 | 相对文件路径存在且已跟踪/暂存；不验证Markdown锚点或外部网站可用性，不证明click或视觉。 |
| 目录 / checker隔离测试 | 30＋10＝40 PASS / 0 FAIL / 0 SKIP | 目录门禁及工具反例，不是Unity、浏览器或设备测试。 |
| 新机型门禁 | 132主＋9 variants＝141全部pending；current 5图、0批准；无新production许可 | 不声称已证明目录无遗漏；不代用户逐型批准。 |

本轮设备矩阵、场景样板、真正键盘/手柄/多触点、写盘失败、iOS point转换与系统辅助访问都 **NOT_RUN**。Windows/iPhone/iPad独立构建与安装、真实地图/空域、最终3D机械一致性和1.25完成均不在本 QA 通过范围。根负责人可交付本轮调研，再按冻结合同与用户授权安排实现，不以报告计数代替用户验收。
