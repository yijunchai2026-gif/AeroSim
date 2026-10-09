# Candidate 03 独立复核：NOT ACCEPTED

版本1.0.0开发候选，1920×1080、UI/HUD/Text100%；实际应用 `/private/tmp/aerosim-v1-build.v1uVO6/AeroSim.app`。这是一轮单视口snapshot，不代表完整缩放矩阵或人工验收。

| 真实来源 | 原始证据 | 结论 |
|---|---|---|
| 冻结34-state原生 | `results/candidate-03/native/desktop-1920x1080/report.json`，UTC `2026-10-09T20:30:54.6705610Z`，SHA256 `17c1cc3f40cdb1ef29354945ca7149b008acd82b8a5c1ae4dd4c03b5404b40e0` | 37cases / 3926checks / 55fail / 0runtime；54旧PRD数字冲突＋1ProHero文字候选；无该合同硬几何/旧键盘失败 |
| 独立Space/焦点 | `results/candidate-03/space-submit/space-submit-report.json`，UTC `2026-10-09T20:31:45.7136740Z`，SHA256 `2c32e0049af116544fe0e26206209506ff23d3997fb475cb018aeae0074253af` | 54checks，51PASS / 3FAIL，0runtime；未通过 |
| 无遮罩视觉 | `results/candidate-03/visual/summary.json`，SHA256 `7f021db6d8084a8848258a60e43b0b8caa540b8c1e4dbb707226c47d8b898142` | 23/23 VISUAL_DIFFERENT；概念原图未改 |

独立读取 `verification/v1-qa/candidate-03-native-smoke.txt` 确认Smoke66PASS/Failures0，包括真实照片快速连续捕获、失败路径、身份/导出路径隔离与旧数据兼容；这不代表上述视觉/补充输入验收通过。原始报告不回写，不用后续源码修复重算candidate03结果。

随后另读真实1280×720/Text200快照 `results/candidate-03/native/desktop-1280x720-text200/report.json`：37/2978/90/0，其中54legacy、35text候选、**1硬失败OSD锚点实际位置不变**。它仍不是完整七组缩放矩阵。两组snapshot必须分开记，不把1920无该合同hard失败推广到所有比例。

## 真实改善及已通过范围

- 上轮6页主列坍缩未回归。Aerial/Pro Preflight第3/4确认文字已正常换行，checkbox均可见；原4项水平越界已消失。
- 原生Space→UI Submit持久化asset已实际Player加载；4个Toggle每次Space仅1次value change/Submit，第二次正常翻回，业务callback使launch可用，Enter真实飞行启动通过。
- 暂停Enter/Esc/Gamepad South恢复后立即Space未误触Pause/RTH/Finish/Photo，恢复时无隐藏Resume残留焦点。Objective和嵌套退出确认取消后的焦点恢复通过。
- 菜单、暂停中Gamepad leftStick导航与South业务恢复通过；非暂停飞行leftStick没有窃取焦点。合成Gamepad/InputSystem事件不是物理遥控器验证。
- Controller去掉产品主导航，改为校准向导壳，符合原概念02结构方向；真实无USB设备仍明示Keyboard+Mouse，未伪造Connected。新棒位/轴条显示清晰，实际硬件范围/逆向校准仍需设备验证。
- Replay原跨卡片长事件已正常换行，不再互相覆盖；这是正文可读改善，不等于原图时间线控件复刻。

## 仍需修的独立发现

### P1：DownArrow焦点窃取，3次真实失败

在Enter、Escape、Gamepad恢复暂停后，按DownArrow均从 `focus=none` 变为可见 `Button#action-pause`。同一时刻未直接改变暂停/RTH/page，所以 `direction-no-unintended-action`通过，但 `navigation-does-not-steal-focus`失败。这说明原NavigationMove guard挡住Stick却未挡键盘Arrow原生焦点路径。

当前探针顺序是**Space后Arrow**，没有在该窃取之后再按Space。故“后续Space可能触发Pause”是基于已被聚焦的真实Pause按钮的风险推断，不是本轮已复现误暂停；不能扩张证据。后续应独立加Arrow→Space并测四方向；刻意Tab选择按钮再Space仍必须能正常执行。

Root准备的新KeyDown guard应仅针对运行中Flight（非Paused），在UI事件层 `focusController.IgnoreEvent`并停止默认焦点移动，不改InputSystem原始键状态/`FlightInput.Read`，否则会误屏蔽物理飞行方向输入。菜单、暂停/Objective阅读中的方向导航及Tab Next/Previous要继续可用。新修复尚未candidate03原生证明。

### P1：Slider数字仍真实上下裁切，冻门禁未覆盖

全尺寸查看 `100-text100-FPV-Tuning.png`：五个数值input文字裁切。原生report.elements提供精确证据：

| 数字 | 内部TextElement坐标 | 实测字框 | 字号 |
|---|---|---|---|
| 360 | (1794,428) | 40×8px | 16px |
| 0.3 | (1794,520) | 40×8px | 16px |
| 1 | (1794,612) | 40×8px | 16px |
| 2.4 | (1794,704) | 40×8px | 16px |
| 25 | (1794,796) | 40×8px | 16px |

这是 `.unity-text-element--inner-input-field-component` 类型，不是Label/Button。冻结text-fits只检Label/Button，故没有fail统计并不表示数字可读。应修嵌套input的padding/linebox/高度并新截图复核，不能只改计数/豁免。

### 待核验：ProHero文字候选仍保留

`Plan the mission, capture each target and review your recorded evidence.` 可用338×34，文字测量338×39。全尺寸图实际两行可读，但5px测量不足尚未排除；保留候选，不能默认通过。

### 窄视口硬失败：OSD保存锚点不影响实际位置

1280×720的独立OSD探针设两个安全锚点后，speed实际坐标始终 `(16,100,184,60)`，`regression.osd-anchor-affects-flight`失败。紧凑HUD的固定网格可能为了安全覆盖用户位置，但这是实际“保存→飞行展示”行为冲突，需要产品明确披露安全重排/约束并确定交互，不应静默宣称锚点在全部尺寸生效。保留原断言和失败；35项Text200候选仍需逐页截图验证，本轮未全部排除。

## 全23页视觉仍不一致

本轮全23页并排检查；Controller、ProHome、Preflight、Replay、Tuning额外全尺寸复核。上轮 [candidate-02-review.md](candidate-02-review.md) 的逐页差异仍适用，以下是关键变化/仍未解决的大项：

- 01无摄影引导背景，卡片、步骤、底部动作位置不同；03/05/06照片更清晰但原图构图与组件尺寸不相同。
- 02向导壳/Controller箭头改善；步骤/图像/右栏的比例、真实状态和动作条仍不同。不可伪造USB连接使画面“相同”。
- 04课程卡片/滚动与原图路径密度不同；07仍4张2×2，不是6张3×2；08单共享rate/单曲线/mini4与参考多轴竞速build不同，并有数字裁切。
- 09真实Warehouse竞速为开放屋顶/简单灯框3D，原图是摄影级室内竞速；12真实桥梁的材质/视角和3目标不等于原图近柱摄影/8目标；23真实RTH的简化山地海岸不等于参考摄影海岸。
- 10“Bridge Inspection”hero仍使用海岸图，不是桥梁；首屏Training Plan/Recent Reports密度不同，实际图像内容不匹配任务。
- 11/19/21专业任务使用示意网格路线，不是参考摄影/3D地图与真实对象放置/时间线；13本地单记录教员复核，不是四路实时监看；20单本地任务，不是11项课程目录。
- 14成功结果是明确合成QA记录，面板位置/密度仍不同；15曲线/事件可读但实际Replay场景与控件布局仍不同；17示意航迹/0真实照片空态不是参考完整任务报告，不能用假照片补齐。
- 16/18/22结构和图片/控件比例仍不同；OSD预览美术不冒充实飞，Readiness不冒充FAA许可。

结论：常见桌面交互明显改善，但仍有输入焦点和真实数字裁切缺陷、23页视觉差异。只能作为**1.0开发checkpoint**，不能宣布完整1.0、像素一致、缩放全通过或用户milestone已验收。
