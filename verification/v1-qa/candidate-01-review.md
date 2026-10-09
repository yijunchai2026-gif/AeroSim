# Candidate 01 独立复核：NOT ACCEPTED

实测版本 **1.0.0**，1920×1080，UI/HUD/Text100%，原生报告开始时间 `2026-10-09T04:22:50.3725960Z`。来源 `results/candidate-01/native/desktop-1920x1080/report.json`；像素比较 `results/candidate-01/visual/summary.json`。本文件是紧凑审查证据，不替代保留在本机的原始 PNG / JSON。

34状态、3580断言、87失败、0运行时异常；52项旧64/240数字冲突保留，21项文字测量候选仍待核验，14项键盘交互失败仍是真失败记录。23张原图逐页人工浏览、对6张严重坍缩页额外全尺寸查看；比较器无遮罩得 **23/23 VISUAL_DIFFERENT**。Smoke51项无失败不等于设计或用户验收通过。完整缩放矩阵、本轮后续修复、移动真机均尚未验收。

根负责人在发现6页P0后中止Candidate01后续全矩阵（exit143）：另一组完整1920矩阵保留720张原始截图及报告，1280仅部分截图保留。七组全矩阵并未完成，禁止声称完整缩放通过；中止不是已通过。后续Candidate02工具新增状态和场景语义，不能用其34状态新期望倒推旧31状态批次成功或覆盖原报告。必须在候选02构建/运行前冻结工具，复测后生成独立新批次。

## P0：真实页面缺陷，不能当文字测量误报

共同根因：`.design-column` 需要 `flex-grow:1;flex-basis:0`，但 `Responsive.uss` 的 `.product-panel {flex-grow:0}` 优先级/加载顺序覆盖了同级主列样式；Responsive 非stack设置Null移除inline后并未恢复预期主列grow。应修生产布局，不放宽QA或改原图。

| 页面 | 原生截图证据 | 肉眼确定缺陷 |
|---|---|---|
| 04 Academy | `100-text100-Aerial-Academy.png` | 中间课程列表约46px，逐字竖排；大量右空白，课程内容难读 |
| 08 Tuning | `100-text100-FPV-Tuning.png` | 中央机型/曲线约46px，文字竖排，曲线不可正常使用 |
| 10 Professional Home | `100-text100-Professional-Home.png` | 主训练hero约62px，课程标题竖排；主体布局损坏 |
| 11 Preflight | `100-text100-Professional-Preflight.png` | Mission Preview约46px，地图被压没，主动作首屏不可见 |
| 17 Report | `100-text100-Aerial-Report.png` | Flight Route & Events约46px，航迹/事件不可读；右侧大片空白 |
| 19 Brief | `100-text100-Professional-Brief.png` | Mission Map & Route约46px，任务地图和路线不可读 |

新工具增加 `layout.design-main-column-readable`：桌面逻辑宽≥1280/高≥500、Text100%下，真实 `.design-columns` 直接主列不得小于160px。这仅捕捉灾难性坍缩，不代表达到原图精确几何。

## 键盘失败：先保留，再区分事件与业务

- 4个飞前Toggle Space未改变值，之后启动按钮仍禁用，主动作不存在/飞行未启动为后继失败。测试使用真实InputSystem Keyboard、按住两帧并释放；Home/Academy/Brief Enter和Tab已成功，不能简单说整个键盘注入无效。
- 暂停12次Tab中，第4至11次逃出modal。候选01只用KeyDown处理Tab，Unity还可能派发NavigationMove；需生产层处理真实焦点循环，不能测试直接改焦点假装通过。
- 候选01报告没有事件类型和Toggle焦点细节，因此尚不能严格断言Space事件是否到达。下一轮工具新增只读KeyDown/NavigationMove/Submit计数、最后键值、实际焦点路径、Toggle.focusable；不消费事件、不更改值、不删除原断言。
- Objective Esc、退出确认Esc仍暂停、OSD保存/位置改变、跨工作台Replay上下文探针本轮通过；这不撤销上面焦点失败。

## 全23页视觉审查

下面每行都仍为VISUAL_DIFFERENT。`内容/能力差异`不等于允许忽略；`fixture差异`也不能掩盖真实3D/布局差异。修复后必须新批次复测。

| 图 | 页面 | 主要不一致/后续验证 |
|---|---|---|
| 01 | Onboarding | 无摄影背景；标题偏左；三卡片/步骤/底部动作高度和位置不同，摄影素材不对应 |
| 02 | Controller | 校准步骤、图片比例/位置、底部导航不同；实测无USB设备，不能伪造Connected；live bars为默认灰宽条而非参考细彩条 |
| 03 | Aerial Home | Hero照片低清/裁切不同，CTA/学习路径/Controller卡尺寸不同；状态与成绩不得伪造 |
| 04 | Academy | **P0主列坍缩**；课程路径、详情动作/摄影内容还需独立对照 |
| 05 | FreeFlight | 右侧表单过长、启动动作不在原图底栏；模式/天气控件不是参考compact chips；hero低清 |
| 06 | FPV Home | 新美术仓库自然但不等于原图；快捷飞行/机型卡/footer位置、宽高仍不同 |
| 07 | FPV Challenges | 4张2×2而非6张3×2；竞速场景、Ghost/Best/右侧信息内容不齐 |
| 08 | Tuning | **P0主列坍缩**；单机型/曲线、三轴彩色曲线/多配置视觉仍不同 |
| 09 | FPV Flight | **捕获语义错误**：工具默认Pacific Coast/race=false，参考Warehouse Race；仍须实测真正竞速场景与OSD，不能照片替代3D |
| 10 | Pro Home | **P0主列坍缩**；训练表/资格状态/Recent Reports分区缺少参考密度 |
| 11 | Pro Preflight | **P0主列坍缩**；参考状态检查与当前人工4项确认不同，不冒充硬件/实时空域状态 |
| 12 | Pro Flight | 真实桥梁3D与参考摄影级近柱场景不同；HUD/8目标参考与当前3目标内容不同 |
| 13 | Instructor | 本地单条记录复核不等于四路实时教员监看、cohort控制与时间线；能力范围缺口 |
| 14 | Results | 面板全宽左对齐而非参考居中；fixture默认passed=false、0事件/指标，需独立明示成功与失败状态数据 |
| 15 | Replay | 真实RenderTexture正常但场景不摄影级；fixture2帧/0事件使图表平/时间线空，不代表参考成功会话 |
| 16 | Readiness | 基本5项结构具备；顶部tabs、右栏素材、间距/按钮尺寸不同；不等于飞行审批 |
| 17 | Report | **P0主列坍缩**；fixture0事件/0照片不能代表任务报告；评分/曲线仍需真实数据验证 |
| 18 | OSD Editor | 背景不是仓库竞速；全4项数值表单替代参考selected-properties/组件树；拖拽/撤销等未全面验证 |
| 19 | Pro Brief | **P0主列坍缩**；示意地图不是参考3D/摄影路线；passing criteria/CTA布局仍不同 |
| 20 | Pro Catalog | 一条本地任务而非参考11项训练列表；分类/右图/课程状态不同 |
| 21 | Pro Editor | 示意3点图+数值表单不等于参考真实3D对象放置、选中target、任务时间线；能力差异 |
| 22 | Settings | 分区/预览及控件间距不同、表单过长；原生默认input外观与参考深色紧凑控件不同 |
| 23 | Aerial Flight | **捕获语义错误**：当前FreeFlight explore，参考Return-to-Home课程；场景真实3D质量/HUD/radar仍不同 |

## 证据边界

不能将期望成功会话缺少fixture数据算成产品崩溃；也不能用丰富fixture冒充用户成就或改变参考图。建议在保留通用自由飞行/失败状态回归后，另捕获真实可运行Warehouse竞速、Return-to-Home课程和明确标注的合成成功会话。产品源、原图和旧历史合同本次复核均未修改。
