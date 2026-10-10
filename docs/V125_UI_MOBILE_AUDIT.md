# AeroSim 1.25 UI / 移动布局审查

2026-10-10（America/Los_Angeles）。状态：`RESEARCH_RECOMMENDATION_NOT_IMPLEMENTED`。岗位为 UI 视觉美术与移动页面程序审查，不是本批最终独立 QA。只新增本文；未改23原图、PRD、USS/业务源码、批准记录或旧QA结果，未启动Unity/native构建，未验证iPad/iPhone真机。本文方案是供根负责人整合的建议，不自动覆盖现行合同。

冻结修订记录：首次稿SHA-256为`081bace55fa485b1e8e13942d4cd84d6dd588ea255bcb6d28da433684a127138`。后续独立QA指出首次稿M08、5.1和持久化矩阵的“统一draft”措辞与现PRD冲突；本次仅修正本文的保存策略，保留UI Scale、HUD Scale、Text Size即时预览、分别本地保存，其余编辑组独立draft＋Apply/Save/Cancel。此修订没有改变PRD、实现或测试结果。

根整合修订：保存策略修订稿SHA为`de8f7669737f47d9519ec7f1760772549381e7c8c4db3d98e2368455bf1cc4ed`。作者停写后，独立QA再指出矩阵不宜用旧1.0映射的30作为当前全量状态数；根仅将该项改为按冻结实现清单覆盖全部实际页面与上下文。旧映射/PRD状态和课程计数各有口径，不在本轮重写历史或固定新基数。

## 1. 结论：需要优化，优先解决“看得懂、点得到、控制安全”

原图有清楚的桌面层级，现候选08的局部叠字/重复单位修复也确实改善了阅读。但“原图桌面一致”“移动好用”“纹理真实”是三个不同目标，目前都不能用局部自动通过替代验收。

建议顺序：

1. 先接安全输入生命周期和实际触点单位：暂停、后台、转屏、第三指、失去capture不能留杆量；44个Unity逻辑像素不能冒称44pt。
2. 再统一页面逻辑：工作台不等于设备；输入来源不等于真实飞机连接；设置各组的即时保存或草稿提交规则必须明确；审核机型不等于正式可飞机型。
3. iPad、iPhone分别做内容预算和导航，不继续仅隐藏标签、缩窄侧栏、把所有桌面卡堆成长页。
4. 最后并行精修桌面原图结构与真实3D材质；清晰菜单照片不能充当可飞场景，也不能让照片化背景遮住真实渲染缺口。

现行[PRD第0章](../飞行模拟器产品设计文档.md#0-当前执行合同与阅读索引)已经明确这三个层次和三设备门禁。本次不缩减继承的1.0功能/23图要求，只给可实施的移动派生规格。

## 2. 实际看图范围与视觉判断

本轮通过`view_image(detail="original")`实际打开下面8张原图与全部6张候选08精选图；不是只读文件名或提示词。23原图的SHA另按[参考清单](../verification/v1-qa/reference-manifest.json)重算，23/23吻合；“字节未变”不等于人眼复核了全部23张。原图1672×941，仅作桌面目标；本轮没有生成替代图。

| 实际打开的原图 | 观察及移动含义 |
|---|---|
| [03 航拍首页](../assets/aerosim-us-03-aerial-home.png) | 单一Continue主动作明确；Hero、Controller、学习路径、Readiness、挑战在宽屏同时出现。手机不应保留大控制器插画和完整桌面两栏占满首屏，应先显示下一课/输入摘要。 |
| [05 自由飞/选机](../assets/aerosim-us-05-free-flight-setup.png) | 大场景、4场景卡、右配置、底启动并列。手机需分成“场景→机型/输入→检查启动”的连续流；展示所选状态和返回保留，不要缩成无法辨认的小卡。图中机型是通用示例，不是全DJI目录已实现。 |
| [航拍飞行HUD](../assets/aerosim-us-flight-training-ui-v1.png) | 大圆雷达、左教学卡、右相机栏和底遥测占四周；没有绘制手机双杆。移动必须为双手和安全区腾位置，但不能借移动派生修改桌面原图。永久模拟标签要补在实现，不改历史PNG。 |
| [09 FPV竞速](../assets/aerosim-us-09-fpv-race-osd.png) | OSD轻、中央通道清晰。手机双杆需加入下角独立区；计时/圈数/关键告警保留，背景动态模糊/太阳眩光不能降低障碍可见性。 |
| [10 专业首页](../assets/aerosim-us-10-professional-dashboard.png) | 桥梁任务Hero、窄CTA、三行计划及技能/报告形成紧凑层级。候选08的海岸图、宽CTA、大Hero不是同一视觉结构。 |
| [12 专业飞行](../assets/aerosim-us-12-professional-mission.png) | 左导航/地图、中央桥柱、右8目标详情，属于桌面高密度任务。手机只常驻当前目标＋进度，完整目标列表经暂停任务抽屉可达；不把8行压缩到拇指区域。 |
| [15 回放](../assets/aerosim-us-15-replay-analysis.png) | 视口、三曲线/双杆、四事件节点、详情和播放条同屏。手机采用“回放/遥测/事件”详情切换，但播放/seek/当前时间仍固定可用；不能把关键播放操作藏在长页底部。 |
| [22 设置](../assets/aerosim-us-22-global-settings.png) | 分类带文字，左设置＋右预览，底Restore/Cancel/Save。手机分类列表→详情，预览成为可打开卡；所有类别不能只靠同一齿轮图标辨认。 |

候选08实际打开：[专业首页1440修复前](../assets/ui-v125/candidate-20261010-08/home-1440-text100-before07c.png)、[同视口修复后](../assets/ui-v125/candidate-20261010-08/home-1440-text100-after08.png)、[首页1920](../assets/ui-v125/candidate-20261010-08/home-1920-text100-after08.png)、[首次Tab/Space暂停](../assets/ui-v125/candidate-20261010-08/pause-1440-first-tab-space.png)、[回放1920/Text100](../assets/ui-v125/candidate-20261010-08/replay-1920-text100.png)、[回放1440/Text200](../assets/ui-v125/candidate-20261010-08/replay-1440-text200.png)。这些是macOS Unity 1.0.0开发候选的历史实图，UI/HUD100%，尺寸/文字倍率按文件名；隔离合成QA数据不是用户成绩，不是本轮新运行结果。

人眼判断：1440修复后标题不再侵入副标题；1920首页首屏仅露部分计划，整体密度仍不同。Text200回放首屏只有标题/视口及视口播放按钮，遥测、事件和其他操作在下方，未在本图目视验证。暂停图底部Exit项不完整，需滚动；不能只凭modal树存在签可达。回放地面重复纹理、山体形面和树群与菜单海岸照片的真实感有明显落差；本轮没有测光照/材质性能。既有[Dashboard差异记录](V125_DESKTOP08_DASHBOARD_VISUAL.md)和[Replay差异记录](V125_DESKTOP08_REPLAY_VISUAL.md)保持其原边界。

## 3. 源码证据：已经有什么，尚缺什么

读取的是当前共享工作区，不等同候选08可执行包的冻结源码：开始时Git已有未提交Flight/FlightDesignV1/Pages与两USS改动，本成员不覆盖。项目版本文件为Unity`6000.3.25f1`。以下均为源码静态证据/风险推断，未声称真机复现。

| ID / 优先级 | 具体证据 | 风险与建议 |
|---|---|---|
| M01 / P1 导航可发现性 | [Responsive.uss:122](../AeroSim/Assets/AeroSim/Resources/UI/Responsive.uss#L122)保留72逻辑px侧栏并隐藏phone/tablet nav-label；[DesignV1:317](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.DesignV1.cs#L317)6个设置分类中General/Graphics/Audio/Accessibility共用settings图标。 | 无文字的重复图标无法解释目的；tooltip不是手机主要入口。iPhone用带文字主导航＋设置分类列表，iPad保留可展开文字侧栏。 |
| M02 / P0 单位 | [AppBootstrap:19](../AeroSim/Assets/AeroSim/Runtime/AppBootstrap.cs#L19)用ConstantPixelSize＋`dpi/163`；[Responsive:39](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.Responsive.cs#L39)再乘UI/HUD百分比。 | 自定义密度估算不是UIKit pt桥接；80%可进一步减小真实点按范围。先建立pt↔panel测量和独立最小hit rect，不能只检查USS min-height44。 |
| M03 / P0 安全区 | [Application:421](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.cs#L421)先按全屏/scale分类布局，后加root padding及flight-hud inset；[FlightDesignV1:453](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.FlightDesignV1.cs#L453)OSD再扣safe宽高。 | 需要核对各容器坐标，确认不是二次扣边/错原点；当前未实测，不能直接断言已双扣。断点、双杆尺寸、OSD占用应统一基于同一个safeContentRect。 |
| M04 / P0 双杆接线 | [Flight:218](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.Flight.cs#L218)旧TouchStick每次Down直接覆盖`_pointer`；Move直接写FlightInput静态轴；CaptureOut不先核对owner。 | 第三指/重复Down/迟到事件存在owner替换或清错风险。UI必须接[TouchInputState](../AeroSim/Assets/AeroSim/Runtime/Input/TouchInputState.cs)，加会话代际；纯核心并不自动替换旧UI。 |
| M05 / P0 中断恢复 | [Flight:120](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.Flight.cs#L120)暂停清轴，但没有在此清TouchStick owner/capture；[Update:173](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.Flight.cs#L173)主要监视屏幕宽高。Runtime检索未找到PointerCancelEvent、OnApplicationPause、OnApplicationFocus/focusChanged协调器。 | 清轴不等于旧指针失效；旧Move可重新写轴。Pause/Detach/禁用/转屏/后台须先Suspend和清owner，再释放capture；恢复仍暂停，确认中立、新Down后才能操纵。未接完整生命周期前不能签三端输入安全。 |
| M06 / P1 大字内容预算 | [Responsive:165](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.Responsive.cs#L165)width<1280、height<500或Text>1.1一律堆叠design-columns；[Responsive.uss:173](../AeroSim/Assets/AeroSim/Resources/UI/Responsive.uss#L173)移动/大字FPV stage min-height1150。 | 125%起全部列消失、固定高Hero/插画拉长流程，未按页面任务预算。按所需最小列宽/文字实际测量收拢辅助列，主动作置于稳定位置；大字允许长页，不允许关键操作不可到达。 |
| M07 / P1 横竖屏 | [ProjectSetup:37](../AeroSim/Assets/AeroSim/Editor/ProjectSetup.cs#L37)默认LandscapeLeft；ProjectSettings defaultScreenOrientation=3且保留4方向autorotate flags。 | 默认方向/允许标志不是本版本横竖屏交付证据；是否启用Autorotation、实际原生window变化均待核。不能仅靠宽度标签推断设备或方向。 |
| M08 / P1 保存逻辑 | [DesignSettings:287](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.DesignV1.cs#L287)Pilot name先改内存；Units立即Save；UI/HUD/Text通过SaveUiPreferences保存；底部又有Save Settings，未见Cancel草稿恢复。 | 混合策略可能把其他未确认修改提前保存；未做实盘测试。按现PRD保留UI Scale、HUD Scale、Text Size即时预览、分别本地保存，并给真实成功/失败反馈；这三项不进入待Save草稿，底部Save不再替它们确认。其余飞参、校准、任务、OSD等编辑组独立draft＋Apply/Save/Cancel；Cancel/Back只处理该组未提交草稿，不回滚已保存的三项倍率，不静默丢/存或顺带提交其他组。 |
| M09 / P0 选机门禁 | [Hangar:14](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.Hangar.cs#L14)仍旧6简化profiles；[DesignFreeFlight:231](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.DesignV1.cs#L231)读AircraftCatalog.Names；[StartFlight:24](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.Flight.cs#L24)Resolve后开始，无新目录批准校验。 | 旧机库与141项审核目录不是同一接线；不把pending新Mavic稿接到这个直接飞行入口。新正式库展示ID/版本与批准状态，无稿/待审/来源hold各说明原因；既存旧候选作为历史边界记录，不假称已符合新正式门禁。 |
| M10 / P1 语义无障碍 | Runtime未找到AccessibilityHierarchy/AccessibilityNode/AssistiveSupport；字体有App内100–200%，没有找到OS文字大小桥接。 | 不能因有Text200、键盘Focus或图标就声称VoiceOver/Dynamic Type可用。Unity语义树、节点frame/操作、读序/模态焦点与系统文字偏好另做原生验证。 |

[纯触点合同](V125_TOUCH_CONTRACT.md)和[既有纯核心QA](V125_TOUCH_QA.md)已明确不是Unity pointer capture、设备safeArea、三端操纵交付；本轮没有重跑或改写其旧计数。`pointerId`会复用，adapter需在事件进核心前检验会话/捕获代际，先清owner再ReleasePointer防重入。

## 4. Apple pt、Unity逻辑像素与触点门槛

Apple以pt描述逻辑界面；UIKit scale将点转换成设备像素。Unity ConstantPixelSize的USS px经panel scale后形成渲染像素，两者没有天然1:1关系；`Screen.dpi`是每英寸点数且可能返回0，不是UIKit scale。必须采集当前window点尺寸、Unity Player/渲染尺寸、panel实际`scaledPixelsPerPoint`、Display Zoom及safeArea，统一换算；3D动态分辨率不应偷偷改变UI hit area。[Apple UIScreen.scale](https://developer.apple.com/documentation/uikit/uiscreen/scale)、[Unity Panel Settings](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-Runtime-Panel-Settings.html)、[Unity Screen.dpi](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Screen-dpi.html)

简化、无额外合成变换时：`hitPt = hitPanelUnits × renderPixelsPerPanelUnit ÷ renderPixelsPerUIKitPoint`。例如**假设**UIKit每pt对应2渲染px、代码采用264/163≈1.62，则44panel单位只有约35.6pt；再乘UI80%约28.5pt。这是公式反例，不是本轮测到某台iPad；不能拿Screen.dpi/163当pt合格证明。

当前Apple HIG表把iOS/iPadOS默认控件尺寸列为44×44pt、最低列为28×28pt；Apple Design Tips另建议手指目标至少44×44pt。因此本文选**AeroSim项目更严格的44×44pt有效hit区**，不把28pt作为飞行控件目标，也不谎称HIG表所有控件硬下限均44。控制间留足间隔，尤其不能重叠扩大后的hit rect。[Apple Accessibility HIG](https://developer.apple.com/design/human-interface-guidelines/accessibility)、[Apple UI Design Tips](https://developer.apple.com/design/tips/)

| 项目建议值（pt，待真机验证） | 起点与规则 |
|---|---|
| 通用返回、导航、下拉、checkbox、图标按钮 | hit rect至少44×44；整行可点。图标视觉可20–24，但不能只给图标本身点按范围。 |
| 训练/启动/保存主CTA | 默认高度52，最小宽按文案＋两侧内边距；Text200自然增高、换行，不缩字。边缘留safe区内16，不贴硬件边。 |
| Pause | 至少56×56，单次点击立即暂停；始终可见/可达，不用800ms长按。与摇杆拖动/结束危险动作分隔。 |
| RTH / 拍照 / 任务动作 | 至少48×48；RTH长按800ms→确认，提供无需长按的等效确认入口；短按、Cancel/转屏/中断不能触发。 |
| 电话双杆 | 直径112–144起点，独立有效区域；不能随UI80%跌到44或更小。最小窗无法容纳两杆/告警时进入暂停布局，不裁杆强行续飞。 |
| 平板双杆 | 直径128–160起点，可在范围内调整位置；位置按设备/方向保存，不能覆盖另一杆或系统手势区。 |
| 间隔与边距 | 独立图标有效区建议至少8–12间隔，危险/安全动作分组至少16；safe区内菜单边距16，平板24起点。数值是项目建议，不是Apple逐项规定。 |
| 正文与数字 | 手机正文17pt起点，标题28–32pt、遥测20–24pt起点；辅助说明建议14pt以上。OS/App放大后允许自然行高；不要把Apple自定义文字最低11pt当正文目标。 |

safeArea以Unity Player窗口像素给出，原点在左下；UI Toolkit原点左上。建议对safe矩形的两角正确翻Y后用`RuntimePanelUtils.ScreenToPanel`转换，在一个容器中应用一次，其他控件仅用本地safe坐标。无刘海、刘海/岛、圆角、home indicator、状态栏、键盘和iPad可调整窗口分别验证；同尺寸左右转屏也可能改变不对称inset，不能只等Screen.width/height变化。[Unity Screen.safeArea](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Screen-safeArea.html)、[Unity ScreenToPanel](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/UIElements.RuntimePanelUtils.ScreenToPanel.html)

## 5. 独立设备布局：同任务模型，不同页面壳

所有端保持“选择训练工作台→课程/任务/自由飞→场景/机型/输入来源→飞检→飞行→结果→回放/记录”。工作台切换不清记录；设备实例/输入来源变了不能复用其他机型控制器校准。没有实体遥控器仍可选屏幕双杆；电脑另有键盘。状态文案“屏幕双杆可用”“受支持控制器已连接”“离线模拟”各自真实，不把默认图中GPS/Link当真实连接。

### 5.1 菜单、选机、设置、回放

| 页面 | iPad建议 | iPhone建议 |
|---|---|---|
| 导航/首页 | 足够空间时文字侧栏＋主任务＋一列输入/资格摘要；窄window先收起辅助摘要，再把侧栏转可打开导航。Hero只负责当前任务，不铺过高。 | 带文字主导航：训练、自由飞、机库、记录；设置/工作台从明确顶部入口进入。首屏优先下一课、主CTA、当前输入摘要；资格/挑战/FAA学习入口仍可达，不把大控制器图置于主CTA前。 |
| 选机 | 左搜索/家族筛选＋机型列表，右详情/四视图与来源/待审状态；窄window变列表→详情，并保留筛选/滚动。 | 单列卡＋搜索/筛选sheet→详情。选机与场景都保留当前选择。审核页只“查看/反馈”，正式已批准库才“选择飞行”；不以颜色区分pending/approved的唯一标记。 |
| 飞行设置 | 场景预览＋配置侧栏；启动CTA固定在safe区上方；高级风/RTH参数按组折叠。 | 选场景→选机型/输入→配置/摘要；底部safe区内主CTA与错误摘要，内容scroll底部留CTA高度padding。Start受阻就地指向原因，错误不必回最上面找。 |
| 设置 | 类别列表＋详情，预览为辅助列；窗口不足时预览先折叠。UI Scale、HUD Scale、Text Size即时预览、分别本地保存，显示真实成功/失败；其余飞参/校准/任务/OSD等各自draft＋Apply/Save/Cancel，不以总Save再次确认三项倍率。 | 分类列表→详情；标签在控件上方，避免窄横排。三项倍率有区别解释和同页样例，改变后即时预览且分别本地保存、真实反馈；Back只询问其他编辑组未提交草稿，不撤销三项已保存值。键盘出现时字段和该组Apply/Save可到达。 |
| 回放/报告 | 宽屏视口＋遥测，事件区下方；窄window或大字先把遥测/事件转详情页签，不缩小播放/时间。 | 主视口＋固定播放/seek/当前时间；下方“遥测/事件/输入”带文字切换，详细曲线全屏可看。所有折叠信息必须有入口，关键操作不能只在滚动最底部。 |

断点按**safe区域可用pt宽高、文字所需最小宽和任务优先级**选择，不按“它叫iPad”或仅横/竖标签。建议内容列最小宽320–360pt、辅助列280–320pt，加实际边距/导航预算；若总和放不下，先收第三列再收第二列，保持主动作与返回位置稳定。它们是起点，不是硬写760/1280替代实际内容测量。Apple当前Layout建议随窗口、文字和安全区调整；iPad窗口能变化，不能只测全屏。[Apple Layout HIG](https://developer.apple.com/design/human-interface-guidelines/layout)

### 5.2 飞行HUD与横竖屏

| 模式 | 布局建议与安全状态 |
|---|---|
| iPhone横屏 | 相机全幅真实3D；左右下角双杆；上方safe区内高度/速度/电量/主要告警及Pause。原大雷达变可展开入口，任务仅显示当前步骤，RTH/拍照不放在摇杆拖动通道。隐藏辅助信息不隐藏故障/低电量/暂停状态。 |
| iPhone竖屏 | 上部相机与短任务条，下部约180–216pt独立操纵带起点，左右双杆；Pause始终可见。教学全文/完整地图/目标表经暂停详情打开。不是把横屏矩形非等比挤入竖屏。 |
| iPad横屏 | 两角双杆；中央保留障碍/航向视野；窄任务/教学侧面板可折叠，地图与操纵区不重叠。宽屏可以多信息，但不用桌面8目标列表强占双手操作区。 |
| iPad竖屏/窄window | 相机＋当前目标，操纵区下移并单独预算；任务详情用sheet/侧面板替代永久第三列。窗口过窄或大字时仍保证暂停/告警/操纵布局，不静默缩杆。 |
| Text200/辅助文字模式 | 遥测换成两行或详情页；安全告警与Pause不能省略。飞行操纵期间不要求长页滚动找暂停；阅读任务详情先安全暂停。UI/HUD缩放不能缩小pt最小hit区。 |

两方向、窗口缩放和Display Zoom变化先使输入协调器暂停/清触点，再重排/重算有效rect；明确显示“布局已调整，确认后继续”，不因同pointer仍按住或重新获得focus自动续飞。这个安全流程继承PRD目标，尚未接线。禁止让HUD移动时旧capture继续给新布局写杆量。

文字策略：保留App内100/125/150/200%，并调研接OS文字偏好；若两者共存，需要明确合成规则，避免未经说明相乘。建议默认采用较大的有效需求并允许App主动覆写，最终规则由根负责人冻结。Apple Larger Text评价不仅看设置存在，而看常见任务在大字下仍可用；系统最大AX尺寸可能超过200%，必须记录真实测试，不可仅靠桌面Text200宣称全部系统可访问性。[Apple Larger Text criteria](https://developer.apple.com/help/app-store-connect/manage-app-accessibility/larger-text-evaluation-criteria/)

语义节点需有名称、角色、选中/禁用状态、操作和实际screen frame；模态打开时读序限定在模态，关闭后返回来源，不让UI读屏操作同时成为飞行输入。Unity6.3的screen reader API不依赖某一种UI，但需要明确建立AccessibilityHierarchy/Node并对目标平台验证，不能从Button存在自动推出VoiceOver可用。[Unity6.3 screen reader guide](https://docs.unity.com/en-us/engine/6000.3/manual/accessibility/screen-readers-get-started)

## 6. “纹理真实”不应靠把UI做复杂

UI继续AeroSim原图的深色层级、单一主蓝、清楚分组；菜单材料用克制的实体暗色面板，最多极弱颗粒，不给正文铺高频纹理。Flight/FPV背景不断变化，关键字和危险状态用稳定底板/描边及文字/图标，不只靠颜色；高对比/减透明模式关闭装饰性透明层。正文建议4.5:1、关键非文本状态建议3:1，检查明/暗/眩光场景而不是只计算USS token。[Apple Contrast criteria](https://developer.apple.com/help/app-store-connect/manage-app-accessibility/sufficient-contrast-evaluation-criteria/)

原图桌面任务主题仍需精修：图10桥梁不能被海岸替代；图12桥柱任务要有真实可飞几何和对应目标。当前[DesignV1:390](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.DesignV1.cs#L390)专业Hero使用CoastHeroV1，这解释了实图主题差异，不能以文字修正代替美术一致。

可见3D改进项：地面tile重复、山体大平面/植被轮廓、近景材质尺度和物体接触阴影，先设同相机/同光照/同距离的可运行对照。源码[Visuals:20](../AeroSim/Assets/AeroSim/Runtime/Simulation/FlightSimulator.Visuals.cs#L20)已经有albedo、smoothness/metallic与Landscape macro/rock混合；[EnvironmentV1:10](../AeroSim/Assets/AeroSim/Runtime/Simulation/FlightSimulator.EnvironmentV1.cs#L10)支持独立tileable albedo。这不是“完全没有材质”，也不证明PBR真实性：法线/粗糙度/接缝、真实尺度、近中远LOD与移动压缩/照明另由美术/技术线量测。

手机不采用更低分辨率大菜单照片冒充优化，也不把高分辨率插画盖在Flight/Replay相机上。原生字体/图标与文字区独立，插画保持比例；只在菜单标明“场景示意”，性能不足可降3D细节而不降触点/字号。纹理增加必须同时量测GPU/内存/温升和触控响应，静态摄影感不是设备实时画质通过证据。

## 7. 可测验收矩阵（本轮全部待执行）

下面是后续独立QA的计划，不是测试结果。尺寸是**点/逻辑布局预检样本**，不代表已支持某具体型号；实际设备另记录型号、OS、window pt、render px、panel scale、safe inset、方向、输入固件、构建SHA。几何/截屏/操作/真机四类结果分开。

| 维度 | 至少覆盖 | 可测通过条件 |
|---|---|---|
| 电脑 | 1280×720、1440×900、1920×1080、3840×2160及21:9逻辑窗口；macOS/Windows分别构建 | 原23图桌面等比对照，未匹配继续列差异；全部实际页面与交互上下文动作可达（按冻结实现清单核对）。不能由macOS代Windows。 |
| iPhone布局预检 | 320×568、375×667、390×844、430×932pt样本及左右横屏转置 | 文字导航、主CTA、Pause/告警在safe区；无横向读文滚动；双杆完整、有效hit区不重叠。窄边界不足时有暂停/恢复布局。 |
| iPad布局预检 | 768×1024、820×1180、1024×1366pt样本、两方向；窄window 375/600/800pt宽并连续resize | 收栏顺序稳定、选中/滚动/草稿不丢，详情可达；不凭设备名锁为三栏；任意window状态输入安全。 |
| 倍率 | 菜单7个UI选项×4个Text=28组合/页面；飞行7个HUD×4个Text=28组合/工作台；菜单↔飞行保存独立性另测7×7×4 | 保留Auto/80/90/100/110/125/150，Text100/125/150/200；字号不漂移、自然行高、滚动到底可点击；hitPt不随80%跌破项目门槛。OS正常/最大AX及Display Zoom另测，不混同App倍率。 |
| 内容/状态 | 首页、选场景/机型、飞检、3种Flight、设置6分类、暂停/确认、结果/回放；长英文名/中文目标文案、空/锁定/加载/错误/未保存 | 名称/数字/单位不烧进图片；禁止重复/无单位；不可用功能解释具体原因；所有折叠信息有可到达入口。当前英文单语不冒称中文实现。 |
| pt与safe坐标 | 2x/3x映射、dpi=0/估算、UI/HUD80–150、左右不对称cutout、键盘出现 | 采样实际panel→screen→pt有效hit rect≥建议值；边界tap确实触发对应控件，外边不串到相邻控件；safe只应用一次。 |
| 触点所有权 | 双Down/Move、第三指、重复Down、同指跨杆、单杆Cancel/CaptureOut、pointerId复用迟到事件 | 只owner改对应轴，第三指不能抢杆；一杆结束不清另一杆；中断后轴0/owner空，新Down之前不能恢复输入。真实UI事件和纯核心测试分开。 |
| 生命周期 | Pause/模态、转屏、window resize、后台/前台、锁屏/来电、失焦、切页、禁用/Detach、外接断连/重连 | 状态清零并保持暂停；不能因旧Move/自动Focus/重连续飞；中立＋明确恢复后才操纵。录像/成绩标技术中断。 |
| 输入组合 | 三端屏幕双杆、受支持外接手柄/摇杆；电脑键盘；touch→controller来源切换 | 不串校准/来源，不双重滤波/相加；不显示移动不可用键为唯一入口；iOS任意generic USB/HID不作承诺。 |
| 三项倍率持久化 | 分别修改UI Scale、HUD Scale、Text Size→即时预览→本地保存→Back/退出重开；逐项模拟写盘失败，并同时保留其他组未提交草稿 | 三项分别保存且不串值；成功/失败反馈符合实际写盘结果，失败保留可恢复状态并可重试，不能假称已保存。不等底部Save，Save不替三项再次确认；Cancel/Back不回滚已保存值，也不顺带提交其他组草稿。 |
| 其他设置持久化 | 飞参、校准、任务、OSD等独立编辑组修改→预览→Apply/Save/Cancel/Back/退出重开；逐组模拟写盘失败 | 每组独立draft；Apply/Save仅提交该组，Cancel仅恢复该组未提交修改，Back说明未提交处理选择。成功才声称已保存，失败可见/可重试；不得把这些组改成全部即时保存，也不能影响三项倍率的独立保存。 |
| 无障碍 | VoiceOver节点名称/角色/禁用/模态读序、键盘/控制器焦点、字体/高对比/减透明、非颜色状态 | 常见任务可完成，icon-only有读屏名；保持读屏focus与飞行输入隔离；OS支持事实按原生结果报告。 |
| 真实纹理/性能 | 固定近/中/远相机、明/暗/逆光、海岸/桥梁/工业、同配置移动低/中/高画质；至少热稳定长会话 | 有视差/真实遮挡/碰撞，不以菜单图替相机；纹理重复/形面/接缝由美术目检；记录帧时/内存/温升/输入延迟及实际硬件，不凭“Estimated FPS”通过。 |

按页面/倍率输出尺寸与语义失败，再用人工实际点击/滚动/双手操作确认。发布只选必要图和紧凑结果，不上传全部矩阵原档。此处未执行任何矩阵，`NOT_RUN`不能改写为PASS；由下一位独立QA冻结整合文档及实现后复核。

## 8. 审查字节快照与来源边界

当前PRD SHA-256：`cc72557d8f3fa0bb899495516418908fba811d63d80fa553631a3c87bdbbf1a6`。23原图字节复核0差异；图表观察仅为本轮实际打开的8张，不扩写成人眼验收23张。官方资料于本轮使用web工具核对，仅采用Apple/Unity primary sources；Accessibility HIG普通open要求JS，采用官方页面搜索返回正文/官方参数页和Apple Design Tips交叉核对，不伪称浏览器实拍了HIG。没有读取或下载第三方UI样式包。

工作区关键源码/USS审查时的SHA（不是运行包证明）：

| 文件 | SHA-256 |
|---|---|
| AppBootstrap.cs | `e7d7e22de7bc31bb2b3d82af8c1fd4cdc0fe9df524ddcfd694ed4e94d00c5991` |
| AeroSimApplication.cs | `749cd8f01ccbc44227f75829fa1929db39cdd7960f9c4555c604d4f6b5d19644` |
| AeroSimApplication.Responsive.cs | `ae7b80aeecc5d8c49f81907236022861d7fd59ea4d8073353a54b38bfdbc9a25` |
| AeroSimApplication.Flight.cs | `8c3482128bad17361bfe10cc1540d24c153ffd21c350bda2af2199c43d78458a` |
| AeroSimApplication.FlightDesignV1.cs | `987e6fd81586443e7e20107c3a448527ab3975c94e1223eb3fa729257ddb02f5` |
| AeroSimApplication.DesignV1.cs | `197bd9fc9f4945d41a3779cb88ace2304fcb6efda23d4603311e5f48d6936f96` |
| AeroSimApplication.Hangar.cs | `21bb883300f052d421d4b638ea97f8744476f48d25bacbcfcd06ad6fe99e4240` |
| Product.uss | `0b0f3f76696d24d8f6f16ddca6c3e36281b08df60b1f021e8b51cd7637504d8e` |
| Responsive.uss | `2cd2ce8fa9abb26248e99188c623e11ae11ba175142b27acffb643b07d793afd` |

官方规范只支持设计依据，不支持“AeroSim已满足HIG”结论。最终视觉验收、每型外观确认及产品验收仍由用户完成；本成员不提交Git，由根负责人审查、独立QA复核后整合与交付。
