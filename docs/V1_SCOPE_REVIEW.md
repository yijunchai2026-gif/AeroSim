# AeroSim 1.0 第二波技术范围审查

审查日期：2026-10-08。只读源码审查；不代表构建、视觉、真机或服务验收通过。本文件不修改完成标准，也不批准缩减用户要求。基于 [技术方案](V1_TECHNICAL_PLAN.md)、[23 图清单](V1_SCREEN_SPEC.md) 和本轮 Unity 源码；共享目录仍在整合，后续提交须重新核对。

## 结论

当前实现是 1.0 开发版，不能仅凭版本号、菜单布局重做或 smoke 通过称完整 1.0。大部分缺口可以继续在本机、无新增付费账号的条件下补齐；其中引导、圈赛、逐轴调参、设置草稿、任务编辑、照片证据、结果分项和 OSD 编辑，不应归因于外部服务而跳过。23 图仍逐张对照，不以运行截图替换参考。

图 13 的真正实时教员会话也不天然要求第三方付费账号：可先实现自托管/LAN 的真实独立客户端，但现有本地记录审阅完全不是 Live。若只提供本地审阅，13 继续未完成；若实现 LAN Live，应明确它尚非公网部署版。Google Maps 真实接入、最新全美空域服务以及 iOS 实机/发布则另有账户、服务或硬件边界，不能以静态离线地图或桌面手机视口代替。

主集成人报告初候选原生 smoke 已通过、无异常；本审查没有独立运行该候选，未将报告转换成视觉或全部交互通过结论。

## 23 图逐项真实能力差距

“本地”表示可本机实现并测试，不表示已完成。“外部”不一定付费，需真实服务/其他客户端/硬件，不能伪造。

| 目标 | 现源码可确认的能力 | 仍需实现或验证 | 边界 |
|---|---|---|---|
| 01 引导 | 三训练线图卡、偏好、控制器说明、档案保存 | 仍为 **3 步**；参考为 5 节点。需真实五步状态、草稿、必选门禁、返回保留、完成/再次引导 | 本地 |
| 02 校准 | 实时四轴值、轴条、已有校准动作、三分区设备图 | 参考五阶段与当前四阶段冲突待按图补齐；摇杆箭头/位置需真实输入驱动；断连、范围异常、保存前验证 | 本地；特定 DJI 发射器兼容需真实设备 |
| 03 航拍首页 | 独立壳、课程/机库/准备入口，使用本地记录 | 逐图卡片、海岸构图、字体/尺寸/图标精调；不能将插画写成当前 3D 场景或虚构控制器连接 | 本地 |
| 04 学院 | 三栏分类/课程/详情，前置与进度读取 | 连接学习节点、图稿状态、真实演示动作和课程链；现 “Lesson Brief” 并非 “Watch Demo” | 本地 |
| 05 自由飞 | 四环境、真实 config、选择机型、天气/风/时间、启动 | 设置参数对应可视预览；选择机型后右栏 RT 需即时更新；进入机库返回不应丢 config；RTH/风等单位一致、底栏比例 | 本地 |
| 06 FPV 首页 | 无侧栏工业插画、四入口、机体 RT、快捷飞行、窄屏滚动 | 按参考精调构图/层次；真实模式/机型状态同步；快捷飞行与控制要求、所有按钮覆盖 | 本地 |
| 07 挑战 | 四环境、同一六门路线、个人最佳记录与回放 | 参考 **六赛道、筛选/排序、圈数、Timed/Practice/Ghost** 未全覆盖；Practice 目前改为 Normal，不等价保留 Manual 的无计分练习 | 本地；联网榜单另需服务 |
| 08 调参 | 一组共用 rate/expo/sensitivity、机体 RT、一条响应曲线 | 每轴模型、三曲线、build 列表与版本、draft/discard/save-new、真实 A/B 比较；不能把同一 rate 三次展示称三轴模型 | 本地 |
| 09 竞速 HUD | 真物理场景、真实计时、遥测及暂停 | 当前计时仍为会话 elapsed，尚非 lap/best/delta；无圈赛 state。电压/链路只能明确模拟模型，不伪装实机 | 本地 |
| 10 专业首页 | 本地任务、记录、状态、技能入口 | 任务分配与实际进度的表格密度/布局；资格字段只能用真实用户记录或明确未验证，不假 FAA 资格 | 本地；真实执照验证另有边界 |
| 11 飞检 | 实际输入/机型信息，四个阅读 ack，Start 门禁 | 参考五检查项；自动检测与人工 ack 分离、warning/action/pass、RTH 范围/障碍验证、展开修复与统计 | 本地 |
| 12 专业 HUD | 真桥梁场景、目标顺序/距离、CapturePhoto 和 PNG、真实完成判断 | 默认 **3 目标**，参考 8；目标实体/相机朝向/安全距离、右栏详情、照片缩略图、完成证据；图像质量未分析不可 Good | 本地 |
| 13 教员 Live | 四条历史记录卡、打开报告/回放、本地分配 | 无独立远程飞行数据/视频/网络延迟/同意接管/会话审计；必须保持当前 “Instructor Review” 诚实文案至真实实现 | LAN 可本地自托管；公网需部署/安全验证 |
| 14 结果 | 真分数/时间/照片/碰撞，事件、重试/回放/报告 | 背景当前是环境插画，不是最后帧；原图分项表现/正负反馈/下一课未齐；需要评分证据，不硬写图中示例成绩 | 本地 |
| 15 回放 | Camera→RT，真实 frames、三图、摇杆图、seek/速度/视角、事件跳转/CSV | Practice Again 从 home 重来，不是 Practice From Here；需快照练习与原记录只读；任意跳转/结束/资源释放与像素匹配待测 | 本地 |
| 16 美国准备 | 三用途 tab、官方链接、说明和边界 | 条目与独立官方目标地址、已读/复习状态、最后内容审阅时间；浏览链接不等于资格/空域授权 | 本地教育内容；最新真实授权外部 |
| 17 报告 | 真分数/轨迹/事件/本地批注、CSV、可打印 HTML | 分项评分、目标与照片关联、事件定位回放、独立 PDF、姓名/日期/批注语义和图稿布局 | 本地 PDF 可实现，无需付费服务 |
| 18 OSD 编辑 | 四 telemetry toggle、拖动 token、归一位置、保存、飞行安全区回退 | 组件选择/handle、预设、undo/redo、逐组件样式/实时字段预览、问题列表、真实 16:9 飞行预览和最终 HUD 一致性 | 本地 |
| 19 任务简报 | 任务真实列表/目标、示意路线、风险阅读门禁 | 场景正交地图、区域/步骤/风险/标准完整分区；当前 grid schematic 不是图中真实桥梁俯视地图 | 本地场景地图；不是 Google Maps |
| 20 目录 | 本地任务查询、列表、选中预览、开始/编辑/创建 | 技能路径栏、filter/sort、前置/状态/进度/多列，足量真实内容而非图示数量假数据 | 本地 |
| 21 编辑器 | 目标列表/增加删除、坐标/参数、真实保存、示意图 | 可选/可拖地图对象、限制体积/起点/触发/时间线、draft/cancel/undo/validation/preview；当前直接改现有 mission 引用，不是独立草稿 | 本地完整编辑；服务审查 Submit 外部 |
| 22 设置 | 分类、比例/单位/图形/音量等真实字段、预览、保存 | 当前直接修改 Profile，部分立即写盘，无 Cancel/Restore transactional draft；三种单位含 Aviation 未齐；音量变更即时效果需测 | 本地；内容更新/云同步外部 |
| 航拍 HUD | 实时遥测、地图、RTH/高度、Photo、实际暂停/降落 | RTH 当前即时执行，原图 **800ms hold/撤销/接管** 没有完整交互；地图放大；视频若无真实录制保持不可用/缺项 | 本地 |

源码入口集中在 `AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.DesignV1.cs`、`AeroSimApplication.FlightDesignV1.cs`、`AeroSimApplication.Flight.cs`、`Simulation/FlightSimulator.cs` 和 `Domain/TrainingData.cs`。视觉是否达到原图另以独立原生截图报告为准，本表不因控件存在就标视觉通过。

## 照片链路：已存在能力和必须加固部分

`FlightSimulator.CapturePhoto()` 确实不是完整链路终点。成功时发出 Notice；`EnsureSimulator()` 监听 “Capture saved”，启动 `SavePhoto()`；后者以真实模拟相机生成 1280×720 PNG。现有 smoke 也检查 inspection 顺序完成和 PNG 数量。不能误报成“只有计数、没有照片”。

仍需加固：`SavePhoto()` 等到帧尾才读取当前 `_sim.Record.id` 和 `photos`，快速连续拍摄、结束或切换会话时存在文件索引/会话错绑风险；异常时缺 `try/finally` 归还相机 RT；业务记录目前没有 capture 的目标、时间、文件、相机姿态元数据。建议 CaptureAccepted 产生不可变请求（sessionId/captureId/targetId/time/pose），图像写成功后追加证据，失败记录明确错误而非完成目标；Report/目标详情读取同一证据。测试至少覆盖同帧多拍、立即 Finish、立即返回、目录不可写、回放禁拍，校验每个文件对应记录而非只数文件。

相机朝向/距离/顺序可做确定性本地检查；“图像质量”若没有实际算法和门禁必须继续标未评估。PNG 存在不能自动证明质量合格。

## 不需外部账号的完整桌面技术方案

这是继续实现方案，不是获准删减 23 图后的新范围。继续 Unity UI Toolkit、真实 3D、当前输入/记录体系；只拆共享 view/state/data，不整体重写框架。

1. **领域模型补齐与迁移**：将 Profile schema 从 2 平稳迁移；新增 ChallengeDefinition（六真实路线、lapCount、模式）、LapRecord（过门时刻/圈时/最佳）、AxisRates、BuildProfile、OsdLayout、MissionDraft/对象/触发、CaptureEvidence、ScoreBreakdown、ReadinessState。为旧档案提供默认值，保存失败不破坏原文件。禁止 UI 私有字段成为唯一业务真值。
2. **草稿与命令**：Settings/Tuning/OSD/MissionEditor 使用深拷贝 draft、dirty 状态、Cancel/Discard、Restore Defaults、Undo/Redo；Preview 是临时应用，Cancel 恢复原 runtime 值，Save 原子写盘。离页提示只在有实际未保存变化时出现。
3. **真实飞行闭环**：圈赛是按门有向平面穿越、顺序/方向/重复去抖、每圈重置，不用距离进入球体当完美过门；Practice 不写排名，Timed 记录 laps；Ghost 读取本地 best frames 单独无碰撞视觉体，不驱动本机物理。三轴 rates 绑定真实 Manual angularVelocity。专业八目标配置与实体/安全距离/朝向同源。
4. **视口与证据**：共享 Camera→RT 视口组件服务任务地图、预览、回放、OSD；世界坐标/场景对象映射一致。结果冻结真实最后帧；照片独立元数据/缩略图/导出关联。PDF 可用经审阅的可再发行库或直接生成规范 PDF，必须在桌面阅读器实际打开；HTML 的 Print/Save as PDF 先保持诚实标签，不能改名 PDF。
5. **四独立客户端 LAN Live**：另建自托管 session host 和 learner/instructor 客户端协议，分别隔离档案/输入/模拟器。会话 join PIN/身份、序列号/时间戳、遥测消息、低分辨率视频帧、真实 ping、Hint、Request Control→显式同意/拒绝→短期授权→撤销/掉线归还控制、审计由服务端记录。先同机四进程验证，再真实 LAN 两设备验证。四张同一 camera 或四历史截图不满足；单机多进程测试也必须标本机，不伪称真实四远程学员。是否支持公网单列，不为 LAN 验证擅自部署公网或开放端口。
6. **视觉/响应式最终门禁**：基准 1920×1080 用 23 锁定源图等比 contain，原生控件、图标、字体、ROI 和整屏差异逐页检查。动态场景差异单报，不大范围 mask。再做 1280/超宽/4K/Text200/缩放全部流程；窄屏重排不要求拉伸静态图，但动作不可丢失。每次整合构建由根独占，QA 独立报告。

LAN Live 的新协议/host 仅是技术建议，当前仓库不存在上述实现；因此不能在本轮报告中标 13 已完成。

## P0 实施顺序与验收动作

| 优先顺序 | 具体文件/新增模块 | 验收动作 |
|---|---|---|
| P0-1 记录/照片可靠性 | `Domain/TrainingData.cs`、`Simulation/FlightSimulator.cs`、`AeroSimApplication.Flight.cs`；建议独立 `CaptureEvidenceService.cs` | 连拍/结束/切页/写失败；PNG 与目标和时间一一对应；原成绩不误改；相机 targetTexture 始终恢复 |
| P0-2 真实五步与设置草稿 | `AeroSimApplication.DesignV1.cs` 拆出 Onboarding/Settings state；`Domain/TrainingData.cs` | 全新档案完整五步，Back 保留；取消不落盘，重启原值；Preview→Cancel 恢复比例/声音/图形；所有单位确实换算 |
| P0-3 六赛道圈赛/HUD | 新 `Domain/ChallengeCatalog.cs`、`Simulation/RaceSession.cs`；`FlightSimulator.cs`、`FlightDesignV1.cs` | 六路线分别飞通；逆向/跳门不计；2 圈后正确结束；best/delta 来自真记录；Practice 无榜成绩；Ghost 与真实遥测对齐 |
| P0-4 八目标专业闭环 | `MissionDefinition`、`FlightSimulator.cs`、`EnvironmentV1.cs`、Brief/Preflight/HUD/Report | 八目标位置/实体/地图同源；过近/过远/背向拒绝且原因可见；飞检 warning 可修复；全程回放照片证据关联 |
| P0-5 图 13 真实 Live | 新 session host、transport、learner/instructor protocol/UI；不要伪装当前 `DesignInstructor()` | 四独立 flight 状态和画面；发 Hint 到正确 learner；拒绝接管无控制变更；同意后可撤销；断线归还/审计完整；明确 LAN 与公网验证边界 |
| P0-6 冻结视觉门禁 | `Product.uss`、`Responsive.uss`、`FlightDesignV1.uss` 与各页 view；`verification/v1-qa` | 23 页对照逐图人工审阅，原图/实际/叠加/差异；30 流程全部真实操作；缩放布局与文字候选逐项处置；不只检查壳 88/248 |

上述“P0”是防止无法诚实完成 1.0 的优先级，不会把原技术清单 P1 的 Undo/逐轴调参/编辑器/PDF 永久豁免。它们仍须在最终全量交付前完成并验收。

## 真正外部边界及不可替代证据

- **Google Maps / 真实美国任意地点**：Google Map Tiles API 要启用项目 billing，请求需要 API key 或 OAuth；不得擅自开通收费或提交 key。地图底图/3D tiles 不是飞行空域授权，且覆盖范围、归属标识、缓存/再分发条件需按具体 API 核对。当前四模拟场景与 schematic map 不满足原先“美国任意地点”。[Google Map Tiles API 使用与计费](https://developers.google.com/maps/documentation/tile/usage-and-billing)。
- **全美限制区与最新状态**：公共 FAA 数据可研究/做标明日期的离线训练层，并不天然需要付费账号；“完整当前禁飞区”则需要多种权威数据、时间范围/高度/更新时间、失败/陈旧态和持续服务。UAS Facility Maps 不是授权也不是所有禁飞区；TFR 单独发布，不能用一个 grid layer 叫“完全复制美国禁飞地图”。[FAA UAS Facility Maps](https://www.faa.gov/uas/commercial_operators/uas_facility_maps)、[FAA TFR](https://www.faa.gov/uas/getting_started/temporary_flight_restrictions)。真实法律授权与现场处置不能由模拟器承诺；脚本警告/拦截只能标训练情景。
- **公网教员课堂**：自托管设计不强制商业 SaaS 账号；真实跨网部署、NAT/转发、TLS/身份/隐私/审计/访问控制、用户设备与网络需要额外部署和验证。仅 localhost 或单机四进程不可宣称公网实测通过。
- **iPad/iOS**：本机布局验证与 Xcode 导出不等于真机验证。设备输入、触控、安全区、温度/性能/纹理、生命周期要实机测；发布签名与分发需要相应 Apple 配置和权限。桌面 1.0 可交兼容源代码，但 iOS 真机/发布状态必须独立列出。[Unity iOS 构建流程](https://docs.unity3d.com/6000.3/Documentation/Manual/iphone-BuildProcess.html)。
- **真实 DJI 发射器/飞行模型认证**：六机型视觉区分/参数不同可本地完成；特定设备兼容、与 DJI 实机物理一比一、制造商认证必须实际硬件/可追溯测量。没有证据不使用 DJI 官方或认证文案。

外部边界不会阻止本地可做的页面与真实能力继续开发；但也不能把这些需求删掉后给“全部完成”标签。若最终这些条件仍缺，交付必须明确为开发版及待补项，用户仍决定是否验收该范围。
