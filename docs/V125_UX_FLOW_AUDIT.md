# AeroSim 1.25 UX 流程与输入一致性只读审查

审查日期：2026-10-10。结论：**需要优化，优先解决操作前后状态、任务返回和输入安全，再精修视觉与场景真实感。** 以下是调研建议和待验收条件，不是已实施功能、获准新图稿、用户访谈结果或设备实测结论。

## 1. 范围与证据等级

- 本轮只读检查最新[产品文档](../飞行模拟器产品设计文档.md)第0、7、17、18章、[1.25进度](V125_PROGRESS.md)、[共享输入核心](V125_INPUT_CORE.md)、[触控合同](V125_TOUCH_CONTRACT.md)、[Gate A合同候选](V125_GATE_A_CONTRACT.md)、[接线计划](V125_INPUT_INTEGRATION_PLAN.md)及以下页面/输入源码；只新增本报告，不改变业务代码、图稿、目录、确认状态或构建。
- 源码证据来自审查时工作区，含其他工作已存在的未提交修改；**不能把当前源码推断说成候选08原生复现**。本报告没有启动Unity、构建新应用、执行native操作、连接外设或验证iPad/iPhone真机，也没有访谈用户。
- 最新PRD已明确：1.25是当前目标，已构建1.0.0候选08仍有未通过项；Windows、iOS签名/导出/设备、共享输入接线、实景地图与空域等不能由桌面截图或纯C#测试推导完成。本报告保留该边界。
- 正式运行选择仍是现有6个原型（4个DJI近似外观＋2个generic）；141条审核目录独立，不能由审核图/名称直接接入飞行。Mavic 4 Pro新稿仍为`pending_user`，不替用户确认、不改23张原始桌面概念图。
- P0：输入失控、非预期恢复或误执行关键动作的阻断项；P1：影响主要流程、任务保持和移动可操作性的优先项；P2：可理解性与编辑效率优化。优先级是产品风险排序，不表示已经发生真机故障。

## 2. 官方调研结论与应用边界

1. Apple建议界面随可用空间、窗口、方向和文字尺寸调整，同时保持核心功能和熟悉操作；侧栏在紧凑空间应收起或换成合适导航。AeroSim应保留A/B/C工作台与飞行核心，不另造三套业务语义，也不把固定桌面侧栏硬缩到手机。[Apple Layout](https://developer.apple.com/design/human-interface-guidelines/layout)、[Apple Sidebars](https://developer.apple.com/design/human-interface-guidelines/sidebars)
2. Apple一般按钮建议至少44×44 pt触点；游戏高频控制应方便拇指操作、避开系统手势，并支持平台默认输入。AeroSim关键飞行动作建议统一至少44×44 pt有效触区，外观尺寸可小于触区；**pt不能直接等同于Unity逻辑px或屏幕像素**。Apple游戏指南也区分次要控件，不能把所有桌面图标机械放大为相同视觉尺寸。[Apple Buttons](https://developer.apple.com/design/human-interface-guidelines/buttons)、[Apple Game controls](https://developer.apple.com/design/human-interface-guidelines/game-controls)
3. 游戏在iPhone/iPad默认可触控、Mac默认可键鼠，连接手柄后采用对应提示；不要要求所有人先有外接设备，也不要把手柄菜单操作与游戏轴操作混为一谈。该原则不改变本项目Mode 2的左杆油门/偏航、右杆俯仰/横滚合同。[Apple Game controls](https://developer.apple.com/design/human-interface-guidelines/game-controls)
4. Apple建议支持显著文字放大、足够对比度及非颜色状态表达；游戏的HUD和菜单都要可读。项目自定义Text 200%不是自动获得系统Dynamic Type、VoiceOver或无障碍认证的证明。[Apple Accessibility](https://developer.apple.com/design/human-interface-guidelines/accessibility)、[Apple Designing for games](https://developer.apple.com/design/human-interface-guidelines/designing-for-games/)
5. Unity明确：UI处理输入不会自动阻止同一输入被游戏逻辑读取；需要独立上下文/输入路由。现有UI导航防护不能代替飞行命令门禁。UI Toolkit应按已安装版本接线，不因泛用教程而默认加入uGUI专用InputModule。[Unity Input System 1.16 UI support](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.16/manual/UISupport.html)
6. Unity的`PointerCancelEvent`与捕获丢失、页面销毁是不同清理路径；禁用元素默认也未必收到Cancel。后台/失焦行为随平台和输入设置变化，应用必须显式维护安全中断，而不是假定系统已经归零或自动暂停。[Unity Pointer events](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-Pointer-Events.html)、[Unity PointerCancelEvent](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/UIElements.PointerCancelEvent.html)、[Unity Input Settings](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.16/manual/Settings.html)
7. Unity `Screen.safeArea`是相对Unity Player窗口的像素矩形，不一定是物理屏幕边界。安全区域、panel scale和实际设备触区需要分别核验。[Unity Screen.safeArea](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Screen-safeArea.html)

以上资料于2026-10-10在线核对；只取与本项目有关的交互原则，不建议将原创AeroSim图稿换成Apple原生视觉风格。Unity资料使用项目输入合同对应的Input System 1.16及Unity 6.3文档；不代表已核准升级包或引擎。

## 3. P0：输入与关键动作

### U01 — 暂停、关闭弹层与中断恢复缺少统一状态门禁

**静态证据：** [Flight.cs](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.Flight.cs) `TogglePause`约114–123行直接反转暂停；`ShowConfirm`/`ShowObjective`约124–155行按打开前的`pauseForReading`在Close中直接`Pause(false)`；只把两条静态触控向量置零，没有要求释放按键/摇杆归中。审查到的Application/Simulator文件中未发现`OnApplicationPause`或`OnApplicationFocus`恢复管理。[Application.cs](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.cs)约407–419行仅在“此前至少一个物理设备→现在一个都没有”且正在未暂停飞行时调用暂停。

**影响：** 关闭阅读弹层可能覆盖另一暂停原因；连接回来、按Resume或Reset后可能读取仍按住的键/杆。两台设备中断一台但另一台仍存在，也不会被上述聚合断连条件识别为当前操纵源丢失。它们是源码路径风险，尚未native复现。

**建议：** 独立维护用户暂停、弹层、应用后台/失焦、选定源不可用等原因集合。关闭弹层只移除自己的原因，不负责续飞；所有安全中断都清输入并停留暂停。恢复须明确用户操作＋所有阻断原因解除＋实际选定硬件/键盘/触控的中立检查；不得用已Suspend路由输出的零值替代硬件归中。接线遵守[输入接线计划](V125_INPUT_INTEGRATION_PLAN.md)，不是本轮修改。

**验收：** 两个中断原因先后进入，分别关闭只解除对应原因；后台→回前台、断连→重连、读目标→同时断连后关闭目标，都不得自动续飞。按住R/W或实物杆偏转时Resume不启用，释放/归中后用户再次明确恢复才开始；首个恢复物理帧四轴为中立。桌面自动化与iOS后台/来电/设备测试分开记录。[Unity OnApplicationPause](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.OnApplicationPause.html)、[Unity OnApplicationFocus](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.OnApplicationFocus.html)

### U02 — 旧屏幕杆仍可覆盖指针所有者，生命周期合同未接入

**静态证据：** [Flight.cs](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.Flight.cs) `TouchStick`约218–258行，Down直接覆盖`_pointer`；Move只核pointerId、不检查页面/暂停；CaptureOut不核所有者直接Reset；未见Cancel、Detach或禁用回调。输出写到`FlightInput.TouchLeft/Right`，不是共享`TouchInputState`。[FlightDesignV1.cs](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.FlightDesignV1.cs)约171行仍构建这两个旧杆。[触控合同](V125_TOUCH_CONTRACT.md)已经要求一杆一owner、第三指拒绝、匹配取消以及生命周期清理，但纯合同通过不等于UI接线通过。

**建议：** 用现有纯状态组件实现UI适配，保持左右手轴语义。先清owner/轴，再ReleasePointer；Pause、页退出、旋转重建、Detach、Disable和系统取消均按合同清理。旧捕获释放事件必须不能清除新owner；必要时增加pointer代次标识。

**验收：** 左右分别持有11/12，第三指13、重复Down、非owner Move/Up/Cancel不能抢杆；旧CaptureOut不能清新捕获；拖出边界仍可稳定归零。暂停、旋转、页面重建和OS Cancel后四轴零、owner空，恢复需新的Down。纯组件测试、native UI捕获测试、iPad/iPhone多点触控测试分别给结果，不互相替代。

### U03 — “Input ready”、预飞勾选与实际可用操纵源不相等

**静态证据：** [InputCapabilityService.cs](../AeroSim/Assets/AeroSim/Runtime/Input/InputCapabilityService.cs)约10–25行以`.current`判断并在无设备时返回平台默认文字，约39–43行只监听部分设备事件；[Application.cs](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.cs)约401–405行固定显示`✓ Input ready`。[DesignV1.cs](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.DesignV1.cs)约426–439行预飞启动条件仅为4个人工勾选全部true；自由飞约244行直接StartFlight。[FlightInput.cs](../AeroSim/Assets/AeroSim/Runtime/Input/FlightInput.cs)约14–54行仍叠加Touch/单个`.current`物理设备/键盘；有Joystick校准时对混合后四轴套全局校准，索引越界被clamp。该实现与按来源、身份、可用原因分离的共享合同不同。

**建议：** 设备条显示“检测到/选定/映射有效/已校准/被暂停”等真实状态，只有足够证据才Ready；预飞区分自动硬检查与用户理解勾选。可用键盘或触控是合法体验入口，不因没有物理设备硬阻断；所选外设映射失效、读失败或身份不符则明确修复，不能静默换源或借用另一校准。飞行中是否锁定来源须正式产品/输入合同决策：**现有Auto仍是逐帧优先级，不是已实现操纵权保持**。

**验收：** 无物理外设但键盘/触控真正可用可开始；无可用来源、MappingMissing、Disabled、ReadFailed不可显示Ready或仅靠勾选开始。相反轴向多源同时有值不得混加；更换设备/布局/映射后原校准不被误用；拔掉实际选定控制器但保留另一控制器时仍暂停，恢复源须用户明确选择。实物外设接线与真机兼容性另验，未测iOS generic Joystick不得宣称支持。

### U04 — UI输入消费、飞行命令和RTH确认不是同一层控制

**静态证据：** [FlightSimulator.cs](../AeroSim/Assets/AeroSim/Runtime/Simulation/FlightSimulator.cs)约202–208行在非Replay路径轮询键盘，C/Backspace无`!Paused`条件；H直接StartRth。`CapturePhoto`调用是否最终成功需另核方法与运行状态，不能把调用写成已拍照。[FlightDesignV1.cs](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.FlightDesignV1.cs)约121、163、167行多个布局入口直接`_sim.StartRth`，没有PRD第0/18.8.6章的800ms后确认；约129、164、168行Finish直接执行。现有方向键UI guard约61–88行只拦UI导航事件，不能阻止独立Raw轮询。

**建议：** Flight、PausedMenu、Replay、TextEntry、Modal各自消费一次命令；游戏命令在非Flight上下文禁止。所有RTH入口走同一“800ms触控意图→确认→执行”命令，键盘/控制器给等效确认，不允许小屏入口绕过。Finish与RTH区分：Finish不是本轮认定必须800ms；应明确“结束并保存”，会话放弃则二次确认，避免相邻关键动作误触。

**验收：** 暂停中Backspace不重置、C不切视角；文本/Replay/弹层上下文不执行H/P/重置，Esc只处理当前层且不意外恢复，Space保留已修复的“有意UI焦点/Replay”规则。RTH短按/长按中取消不执行，满800ms只开确认，确认一次才执行；所有宽/窄入口与控制器等效操作一致。Finish只保存一次且结果与退出语义明确。候选08已有Space修复不得被本报告误标为未修。

## 4. P1：简单、明确、操作前后一致

### U05 — 修设备/换机后回不到原配置流程

**静态证据：** [DesignV1.cs](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.DesignV1.cs)约145行Controller Back固定Home；213–218行每次`DesignFreeFlight`重建`FlightConfig`为默认场景/模式；约232/243行转Hangar/Controller，返回重建会失去天气、风、时间、RTH等未持久配置。预飞4勾选同样按每次构建重新置false。另[Pages.cs](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.Pages.cs)约40–51行ShowPage对非Flight/Replay执行Stop并重建根UI，不是上下文抽屉。

**建议：** 用带来源的返回栈/流程上下文保留`workspace、flowId、draft、origin、scroll/focus`；设备中心、Hangar或Settings修复完成后回原流程。新会话才创建默认配置，页面重建不重置草稿。未来飞行设置按PRD17章覆盖抽屉，不用会Stop的普通页面路由。输入/机型变化只作废相关预飞验证，保留仍有效的理解项。

**验收：** FreeFlight设Warehouse/Sport/Overcast/指定风和RTH→Controller→Back，原值和焦点完整；再进Hangar选现有原型→返回，只有机型相关项变更。Onboarding→Controller返回原步骤；Brief→Preflight→修输入回Preflight，任务不变。打开未来飞行设置不销毁场景或丢会话；所有实际路径补native回归。

### U06 — “保存”混合即时生效、即时落盘与待保存，离开行为不明确

**静态证据：** [DesignV1.cs](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.DesignV1.cs)约279–314行Settings直接改全局Profile；Units/UI/HUD/Text即时Save，Pilot/Audio等仅改内存，Graphics部分Apply但未立即Save，仍统一提供Save Settings。约347–360行Tuning写同一全局settings，却称`Save before testing`；[Application.cs](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.cs)约392–398行切工作台执行LocalStore.Save，可能将这些“未保存”修改一并持久化。OSD与Mission编辑也需同类草稿边界核验，不可只改文案伪造事务。

**建议：** 明确两类策略：UI/HUD/Text等按PRD即时预览、分别保存，显示真实本地保存结果；飞行参数/任务/校准/OSD用独立草稿＋应用/保存，离开有保存并离开、放弃、取消。不得让另一页面普通Save替草稿落盘；考试中是否允许应用另按锁定项执行。不在本轮决定新增同步或云存储。

**验收：** 修改后返回/工作台切换/退出重启与显示的保存语义一致；放弃恢复旧值且不被其他Save间接写入；取消保留原流程；磁盘写失败不显示“saved”、不清dirty，支持重试。UI/HUD/Text独立保存测试不能被Tuning草稿策略覆盖。

### U07 — 弹层按钮与真实暂停状态不一致

**静态证据：** [FlightDesignV1.cs](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.FlightDesignV1.cs)约226行从暂停菜单进入Exit Without Saving；[Flight.cs](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.Flight.cs)约132–136行取消按钮仍叫`Keep Flying`，但打开时已暂停，Close不会恢复。Pause中的Reset/SimulateSignalLoss约223–224行调用TogglePause，存在即时续飞语义；与U01共用恢复门禁。

**建议：** 放弃会话弹层用`Cancel`与`Discard Session`，Cancel回暂停菜单，破坏性操作清楚命名并区别视觉；只有`Resume Flight`真正进入恢复检查。重置后先留暂停，是否恢复由用户明确选择。Apple建议描述动作、保留安全取消路径，不能用含糊OK/Confirm代替动作。[Apple Alerts](https://developer.apple.com/design/human-interface-guidelines/alerts)

**验收：** 暂停→退出→Cancel仍暂停且焦点回原按钮；Discard只丢当前未保存会话，不改已有记录；打开目标再关闭回同一状态；Reset结果和按钮名称一致，不能让一个按钮同时承担复位与无门禁续飞。

### U08 — 新手和结果页的主动作未闭合“下一步”

**静态证据：** [DesignV1.cs](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.DesignV1.cs)约75–123行Onboarding工作台按Profile默认值已有选中样式；最后`Start Flying`实际回工作台Home，不进入第一课；Check Controller离开向导后固定回Home。约261行`Quick Flight`进入配置页，不复用已验证配置快速开始；约537行Results最高优先样式是Back Home，Retry为次要，没有Next Lesson。这与PRD7/17/18章的3分钟第一课、快速飞行和结果续练目标有差异。

**建议：** 主CTA准确表达当前实际下一步，不把尚未实现的“立即起飞/快速飞行”当完成；将“第一次体验”串成选工作台→可用默认输入→必要安全检查→入门第一课，已有设备者可跳过不需要的维修。结果以有合法下一课时Continue、否则Retry为主，Home为次；仅保留用户此刻最可能需要的决策，不增加新顶层导航。

**验收：** 新用户无需外接设备能按平台默认输入走入第一课；修设备可返回向导；CTA点击目的地与文案一致；课程通过/未通过/无下一课三种状态均有明确下一步。PRD的3分钟是待用户测试目标，不由静态审查宣布达成；不得用文字修改原图来制造视觉通过。

### U09 — 三端适配要验证空间、触点与安全区域，而非设备名或缩图

**静态证据：** [Application.cs](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.cs)约421–446行按逻辑宽/高划desktop/tablet/phone，移动端换算safeArea并设置根与HUD边距。该基础可复用，但桌面窄窗口也可进入phone样式，尚不说明真机手感；[Responsive.cs](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.Responsive.cs)的Screen.dpi/panel倍率需要与实际设备点/像素对应核验。PRD18.6中40×40、HUD36×36等是桌面起始尺寸，不应变成所有移动有效触点尺寸。

**建议：** 导航按可用空间适配，输入按实际能力识别，二者分开；iPad宽屏保留稳定导航、窄窗口收起，iPhone聚焦单任务并把次要配置收进有界页面/面板。双杆保持可舒适够到的左右下方，不压系统手势；关键按钮有效触区至少44×44 pt，并清楚记录转换。portrait/landscape、iPad多窗口、屏幕键盘和safeArea改变都要保持当前任务，不借旋转重新默认配置。桌面23图比例不受移动布局建议替换。

**验收：** 实际iPhone/iPad横竖屏、iPad窄窗口分别记录screen px、safeArea、panel scale、逻辑视口和触区映射；暂停/返回/RTH/确认可触达，双杆无系统手势冲突，旋转轴零且状态不丢。输入文字时键盘不盖住必要确认，关闭后焦点/滚动恢复。逻辑视口预检与物理设备结果分栏，不相互冒充。

### U10 — 大字可见、实际可达与当前输入提示需分开验收

**静态证据：** [V125_PROGRESS](V125_PROGRESS.md)仍保留候选08 Text200情况下部分Home/Replay操作需滚动到达和完整可点击矩阵的验证缺口；这是覆盖缺口，不直接判定“按钮不可达”。[FlightDesignV1.cs](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.FlightDesignV1.cs)约169行指引固定键盘WASD/RF等，未据触控/选定手柄替换；[DesignV1.cs](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.DesignV1.cs)约110–114行引导控制提示也以键盘为主。候选08已修first-Tab/Space、专业Home字符重叠和Replay重复单位，不能重新列为当前已证故障。

**建议：** 按当前合法操纵源显示Mode 2杆说明、键鼠快捷键或实际控制器按钮，并保留查看其他输入的入口；不用“最后触发事件的设备”假定操纵源。大字重排允许滚动，但主CTA、错误与恢复入口要有清楚焦点/滚动路径；按钮有可理解标签，错误含问题＋动作，不只用颜色。独立记录Text倍率、UI/HUD倍率和OS缩放。

**验收：** 每个关键流程在Text100/125/150/200与UI/HUD配置矩阵下，真点击/触摸/键盘导航到主CTA，不只检查worldBound；FocusIn后可见且无遮挡，模态只在自身范围循环并恢复焦点。触控、键盘、两种实物控制器分别核提示与实际动作。系统文字、VoiceOver/辅助访问能力另测；本轮不作已支持声明。

## 5. P2：说明、编辑效率与场景真实感

| 项目 | 静态证据/边界 | 建议与待验收条件 |
| --- | --- | --- |
| U11 校准步骤应与实际动作一致 | [DesignV1.cs](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.DesignV1.cs)约125–175行侧栏写Center/Capture range/Verify channels/Save；实际[Pages.cs](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.Pages.cs)约187–229行第三步用文字要求核对方向并提供Save Calibration，未核验四轴方向动作；第四步已是Calibration saved/Start Again，侧栏却仍写Save。旧流程读的是混合Raw，不是新合同来源快照。 | 先接来源绑定再统一步骤；明确居中、走完整量程、四轴方向/归中验证、保存四阶段，完成态与待执行态分开。未实际验证不宣称Verified；换设备/映射取消当前草稿并说明原因。验收步骤标题、进度、按钮与实际状态一一对应，保存失败保留可重试草稿。 |
| U12 编辑行为与单位应一致 | [DesignV1.cs](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.DesignV1.cs)约363–384行OSD拖动即时而数字坐标需Preview重建；约235、438行风仍硬写mph；Mission编辑约478行部分m/mph标签固定。 | OSD同一属性用一致预览/应用语义，键盘增量、撤销/重做按PRD目标另实现，不宣称已有。Units切Metric后所有非有意混合的飞行物理显示一致，内部SI和CSV不随显示单位变更。验收拖动/数字/键盘得到同位置，取消不落盘；ft↔m往返无累计漂移，默认US与FPV有意inch/mm/KV混合仍清楚解释。 |
| U13 真实场景应服务判断，不以逼真背景替代地图/资产完成 | 当前地图仍离线程序化训练场景；概念/菜单图与运行3D不同，真实纹理并不证明地理准确、真实空域、DJI网格/材质已获准。 | 与UI/美术线共同评估飞行低空、靠近地表、远景/近景切换时的材质尺度、重复纹理、接缝和光影连续性；先让障碍距离、地形高差、航向与落地点易判断。纹理高低质量只改变场景细节，不改变输入响应、关键HUD/警告对比度或触点。验收需同一路线/时间/质量档实际运行截图或视频＋性能数据；菜单Hero更真实不计为场景已修。不得为真实感伪写Google Maps/真实地点接入，也不把新概念图替换23张目标。 |

真实纹理的资产制作/渲染预算由独立UI/美术调研提出，本报告仅提出UX可辨识与验证边界；不假定有已授权素材，不新增模型或生成图稿。

## 6. 建议冻结的一套流程/状态语义

下表是后续实现建议，不是新代码合同已获准；若改变共享接口或Auto选源语义，先由负责人补充并冻结合同，再分端接线。

| 上下文 | 可以执行 | 返回/恢复规则 |
| --- | --- | --- |
| SetupDraft / Brief / Preflight | 修改草稿、实际硬检查、理解确认、修设备/选现有原型 | 辅助页回原origin；保留draft，仅相关变化作废对应验证；不静默默认化。 |
| Flight | 唯一合法来源的四轴、明确飞行命令、关键动作意图/确认 | Esc/控制器Menu立即暂停；方向键只操纵飞机；RTH所有入口同一命令。 |
| PausedUser / PausedInterrupt | 查看目标/遥测、修输入、安全配置、保存/放弃 | 累积原因独立；清输入，不因关闭弹层/重连/回前台自动恢复。 |
| ResumePendingNeutral | 观察真实来源是否归中、显示原因、用户明确Resume | 所有原因解除＋实际中立才回Flight；不能从Suspend零帧推断中立。 |
| Settings / Modal / TextEntry | 本层导航与编辑，不向飞行派发命令 | Cancel回原层与焦点；设置草稿与即时UI保存各有明确策略。 |
| Results / Replay | 续练/重试、回放seek与本层Space播放、导出已有记录 | 不派发飞行H/P/Backspace；重试复用已保存配置并重做相关预飞；记录保存事务只执行一次。 |

## 7. 后续验证交付顺序

1. 先冻结U01–U04输入上下文/中断/来源/关键命令接口，按已有Gate A/B/C/D执行；纯测试只证明纯逻辑，native/物理测试单列。
2. 页面程序线做U05–U08流程保持、保存事务、按钮语义；独立QA跑首次体验、自由飞修设备、专业预飞、暂停退出、失败恢复和结果续练路径。
3. iPad/iPhone线各自做U09–U10实际空间/触点/键盘/旋转/多点触控验证，桌面做窗口和外设验证；Windows独立构建/运行结果不由macOS代签。
4. UI/美术线做U13场景真实感与可读性对照，保留23张原始视觉目标和6原型/141审核目录边界；用户逐机型确认和最终产品验收仍由用户完成。

完成标准不是“建议写完”或“控件边界通过”，而是对应状态和操作可用、失败可恢复、记录不丢、三端证据分开，并向用户明确仍未测/未接入项。本次只完成调研报告，未批准或实施上述修改。

报告自检：33处本地文件引用全部存在；无行尾空白。该自检不代替独立QA、本机应用行为验证或用户验收。
