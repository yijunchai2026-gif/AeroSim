# Candidate 02 独立复核：NOT ACCEPTED

真实版本1.0.0，1920×1080，UI/HUD/Text100%，原生开始UTC `2026-10-09T04:39:17.4189090Z`。冻结契约 `v1-candidate02-34states`。

原始报告 `results/candidate-02/native/desktop-1920x1080/report.json`，SHA256 `dfa81fc511224946ae95cf2a5b439ec57b20d676bbd739e8de10b9ba52b986a0`，未回写。37cases、3949checks、83fails、0runtime；83项为54旧64/240冲突、19文字候选、4水平越界、6键盘失败。原图比较 `results/candidate-02/visual/summary.json` 仍 **23/23 VISUAL_DIFFERENT**（无遮罩）。本轮不是全缩放矩阵，不是用户验收；新源码修改不得倒算为本构建已通过。

## 已实证改善

- 全23页人工并排浏览，Academy/Tuning/Pro Home/Preflight/Brief/Report额外全尺寸复核。原6页约46–62px主列坍缩已修：Academy608px、Tuning902px、Pro Home1178px、Preflight1178px、Brief1178px、Report902px。原生19个主列断言均PASS；达到可读最低门禁，不等于原图精确比例。
- 暂停12次Tab全部留在modal；Objective Esc、嵌套确认取消仍暂停、OSD保存/位置改变、跨工作台Replay上下文探针通过。
- FPV捕获改为真实Warehouse Manual竞速，Aerial Flight为真实a10 RTH配置，未用背景照片冒充飞行渲染。通用自由飞行两页与失败结果页仍保留独立回归。
- 成功结果/回放/报告的41帧、5事件合成QA数据已显示，明确NOT a pilot achievement；没有添加虚构照片。

## 剩余真实缺陷

| 优先级 | 证据 | 结论 |
|---|---|---|
| P0 交互 | 4次Toggle Space：focus=Toggle、focusable=true；keyDown始终0、submit=3不增加 | 缺UI Submit Space binding，不能靠KeyDown翻值修复；后继launch disabled导致2次失败 |
| P1 几何 | Aerial/Pro Preflight第3/4Toggle `(1559,883,354,44)` / `(1559,983,357,51)`；clip右边1872而控件右边1913/1916 | 确定越界；截图第3确认长句向右超出、checkbox不可见，不是文字测量误报 |
| P1 文本 | HUD Return Home / Finish Session：可用61px，需100/108px | 窄工具栏标签超出容器；全尺寸截图确见文字伸出右侧栏 |
| P1 文本 | Replay事件：可用265px，3项需295/283/377px | 全尺寸截图事件标题跨卡片与图标相互覆盖；需要wrap/宽度/可滚动的真实布局修复 |
| P1 文本/视觉 | Tuning右栏Slider数值框全尺寸截图 | 数字上下被裁切；不在Label/Button text-fits覆盖范围内，不能以19项外没有失败认为无问题 |
| 待核验 | Home数字/箭头差4px、Pro Hero副标题20px vs39px、Catalog150px vs132px | 保留文字候选，逐项截图/原生输入测量验证；不可全部豁免 |

## 23页原图差异仍在

| 页 | 当前主要差异 |
|---|---|
| 01 Onboarding | 无全屏摄影背景、标题左对齐、卡片/步骤/action位置不同 |
| 02 Controller | 步骤/底部动作位置不同，live axis灰条不等于参考细彩条；无真实USB不能伪造Connected |
| 03 Aerial Home | Hero/控制器/路径卡比例、文案密度与参考不同，部分图像低清/裁切 |
| 04 Academy | 主列已可读，但课程路径卡很高/长滚动，详情照片明显低清，未达到参考首屏密度 |
| 05 FreeFlight | 新hero清晰，但天气/模式/启动表单仍不是参考紧凑chips与底栏结构 |
| 06 FPV Home | 新仓库美术自然，但摄影构图、卡片与按钮间距不等于原图 |
| 07 Challenges | 4张2×2不是6张3×2；Ghost/Best/scene内容仍不齐 |
| 08 Tuning | 主列恢复，但单共享rate/单曲线与参考多轴彩色曲线不同；mini4不是竞速build；数字input裁切 |
| 09 FPV Flight | 真实仓库竞速已捕获，但未完成摄影级仓库/OSD构图，raw差异98.68% |
| 10 Pro Home | 主列恢复；hero使用低清海岸而非桥梁、按钮全宽、Training Plan/Recent报告布局和密度不同 |
| 11 Preflight | 示意网格地图不是参考桥梁摄影/3D；2个Toggle越界；状态检查内容/底部CTA布局不同 |
| 12 Pro Flight | 真实桥梁场景/HUD存在，但近柱摄影、8目标与当前3目标内容不同，raw差异94.61% |
| 13 Instructor | 本地单记录复核，不等于四路实时教员与cohort控制 |
| 14 Results | 正确明示合成成功数据，但全宽左对齐卡片/大区块仍不同于居中参考 |
| 15 Replay | 真实场景/曲线正常；事件文字越界；摄影级海岸和参考timeline/控件不同 |
| 16 Readiness | 基本5项结构具备，tabs/右栏图片/控件视觉不同；非审批功能 |
| 17 Report | 主列恢复，合成航迹/事件可显示；示意地图/评分结构/首屏密度与原图不同，0真实照片需空态 |
| 18 OSD Editor | 仓库概念图用于明确预览，不是实飞；组件树/selected-properties/拖拽能力仍不同 |
| 19 Brief | 示意网格路线替代参考桥梁地图，风险/条件/action布局不同 |
| 20 Catalog | 一条本地任务而非11项课程/filters，右侧hero错误海岸摄影 |
| 21 Editor | 3点示意图+数值表单不等于真实3D对象放置/任务时间线 |
| 22 Settings | 已紧凑改善，分区/控件视觉/预览仍不同；raw差异25.32% |
| 23 Aerial Flight | 配置语义已为RTH，但摄影级海岸/HUD/radar不同；raw差异87.53% |

以上像素比例来自无遮罩比较器，不用于把真实数据/光照时变免责为完全一致。即使后续自动几何全通过，23页仍需逐页视觉与最终用户验收。

## Space方案独立源码审查及新增风险探针

本机InputSystem1.16 `InputSystemProvider` 将 `UI/Submit` performed转为Navigation Submit（不是KeyDown）；选择persisted project-wide `UI` map并Enable。`ProjectWideActionsBuildProvider`把Edit模式指定的asset预加载到Player。Root新`UIInput.inputactions`新增Space→UI/Submit并在Editor构建赋值，删除人工KeyDown翻值，方向正确；需要候选03原生证明构建确实加载asset。

更正早先风险推断：当前 `FlightInput` 升降键为R/F及PageUp/Down，**Space不是飞行上升键**。真正风险是Pause恢复只隐藏overlay时Resume按钮可能留焦点；Space Submit可能重新TogglePause。新Root源码Blur焦点是针对该问题的修复，未在candidate02中实测。Objective/Confirm移除overlay和方向导航是否窃取焦点也需实测。

新增独立补充 `SpaceSubmitProbe.cs` / `build-space-submit-probe.sh`，不改变冻结34状态聚合契约。覆盖：

- 实际Player加载UI/Submit Space及保留leftStick/Joystick导航bindings。
- 真实Toggle两次Space各翻一次，事件count精确增加、业务callback启用launch，Enter真实启动。
- 新飞行、真实飞前启动、暂停Enter/Esc/Gamepad恢复、Objective关闭、Exit确认取消恢复后Space不得误触Pause/RTH/Finish/Photo，焦点不得留隐藏控件。
- 非暂停飞行Arrow/leftStick不能窃取UI焦点/改变页面或RTH；菜单与暂停中leftStick真实Navigation事件和焦点移动正常，Gamepad South真实恢复。

用的是隔离副本中的InputSystem合成键盘/鼠标/手柄，不等于物理遥控器/移动真机。C# Roslyn编译和shell语法已通过；补充探针原生结果 **NOT_RUN**。

```bash
# 仅根负责人构建副本；下面build脚本不启动Unity或native app。
bash verification/v1-qa/build-space-submit-probe.sh /absolute/uninstrumented/candidate03/AeroSim.app
# 将脚本最后输出的明确路径填入；不要使用已注入旧矩阵的app。
SPACE_QA_APP=/private/tmp/aerosim-space-submit.XXXXXX/AeroSim-UIQA.app
SPACE_QA_PROFILE=$(mktemp -d /private/tmp/aerosim-space-profile.XXXXXX)
SPACE_QA_OUTPUT=/Users/bytedance/Documents/ChatGPT/飞行模拟器/verification/v1-qa/results/candidate-03/space-submit
"$SPACE_QA_APP/Contents/MacOS/AeroSim" --space-submit-qa "$SPACE_QA_OUTPUT" \
  --data-dir "$SPACE_QA_PROFILE" -screen-width 1920 -screen-height 1080 \
  -screen-fullscreen 0 -logFile /private/tmp/aerosim-space-submit-player.log
```

输出独立 `space-submit-report.json`；退出0仅PASS_AUTOMATED_ONLY，失败返回2。没有报告为运行失败，不借用旧矩阵PASS。补充探针不允许替代候选03冻结矩阵或视觉验收。
