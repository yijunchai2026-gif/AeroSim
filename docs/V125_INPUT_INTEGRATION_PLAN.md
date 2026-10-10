# AeroSim 1.25 三产品线输入接线执行计划

冻结日期：2026-10-09，America/Los_Angeles。本计划依照最新 AGENTS、V125_PROGRESS 和当前源码，只新增本文。**旧 FlightInput 仍未委托纯 InputRouter；本计划不代表接线、移动设备或物理摇杆已完成。** 本轮根负责人先完成候选07动态字体修复和新机型审核稿；以下输入任务是后继批次，不能混入已冻结候选06/07的测试结论。

## 1. 当前具体调用点与替换边界

| 现有调用点 | 当前行为 / 风险 | 后继接线动作 |
| --- | --- | --- |
| `Input/FlightInput.cs::Raw()` | 触控、Gamepad或Joystick、键盘相加后clamp；Gamepad存在时不读Joystick；轴索引越界静默clamp | 替换为独立 source samples；遍历有资格设备，不把某类 `.current` 当作全部设备；四轴交给 InputRouter 单源仲裁 |
| `FlightInput.Read()` | 只要任意Joystick连接且 savedUtc非空，就对已合成轴应用全局校准 | 仅返回路由的正式frame→Vector4；不在委托后再做一遍 deadzone/expo/reverse/calibration |
| `Pages.cs::ControllerPage()` Capture Center / Capture Range / Save | 中心、范围来自合成 Raw；保存 device 是 PrimaryDeviceLabel；另device变更可能让步骤捕获不同设备 | 校准向导锁定明确source/deviceIdentity/control paths；只读该源未过滤原始样本；设备变化中止该次draft；保存绑定identity的profile |
| `Flight.cs::Update()` 校准实时标签与_minimum/_maximum | 每帧取Raw，可能混入键盘/其他手柄；UI progress range固定[-1,1] | 读校准锁定源；数值展示真实raw，图条按该源量程显示，不能为UI条宽先clamp保存值 |
| `DesignV1.cs::DesignController()` | 重排 ControllerPage 原控件，原actions保持真实绑定 | 根只接一次共享校准view model；设备程序员用各自排版消费，不复制三份校准算法 |
| `DesignV1.cs::StickReadout` 的 `FlightInput.Raw()` | 图示由合成input驱动，没有区分正式响应/原始轴 | 飞行页图示绑定正式 CurrentInput与source label；控制器配置页绑定指定设备的raw，注明两种含义 |
| `PreviewCapture.cs` Gamepad与Touch smoke | 虚拟Gamepad / 直接写TouchLeft/Right，检查Raw数值 | 保留旧测试证据；新smoke经适配器输入与Read检查，新增来源ID和场景断言；不能改旧失败阈值制造通过 |
| `Simulator/FlightSimulator.Update()` | 调用Read；H/C/P/Backspace独立直接轮询，即使Paused部分仍能执行 | 在单一入口应用context与command规则；冻结四轴顺序和遥测语义，保持现有物理更新节奏 |
| `Flight.cs::TouchStick` | PointerDown覆盖_pointer；CaptureOut不检查pointer；无Cancel；写static TouchLeft/Right | 由根统一替换pointer状态入口；各端视图只负责位置/尺寸，触点独占和复位属于共享TouchInputState |
| `Application.cs::OnInputCapabilitiesChanged()` / OnDisable | 设备掉线会暂停，但应用未有Pause/Focus生命周期接线 | 增加统一中断协调器；清touch、路由、命令锁存与振动；不允许每条线都注册一次相同回调 |

当前纯核心 `FlightControlFrame.cs/InputRouter.cs` 已有独立3,050算法检查，Auto活动优先、Touch>Gamepad>Joystick>Keyboard、同类按ID、Fixed断线neutral、校准按源/身份隔离。这些策略从本批继续复用，任何策略变化先修改契约和独立测试。

## 2. 先冻结适配接口，再动旧接线

后继新文件使用以下具体接口（**拟新增，尚未实现**）：

| 文件 / 接口 | 输入与输出 | 必须保证 |
| --- | --- | --- |
| `Runtime/Input/UnityFlightInputSources.cs`：`CollectSamples()` | 返回复用容器中的当前 InputSample，Keyboard/Gamepad/Joystick/Touch 各自一个唯一ID | raw未校准且未混合；同物理设备不生成重复ID；source不可用则移除或available=false；采样容器不把上一帧断线值留下 |
| `UnityFlightInputSources.ReadRawSource(sourceId)` | 明确 sourceId 的InputSample与available/map状态 | 校准页不调用正式Read；Joystick raw保留真实非[-1,1]量程；映射失效拒读，不能重复最后轴 |
| `Runtime/Input/InputDeviceIdentity.cs`：`Resolve(device)` | runtime sourceId、持久profileKey、identityConfidence、description摘要 | runtime deviceId用于会话ID；持久identity不使用displayName或deviceId替代硬件身份 |
| `Runtime/Input/TouchInputState.cs`：`Begin/Move/End/Cancel/Clear` | left/right、pointerId、归一化杆向量；输出单个touch InputSample | 触点ID独占、无第三指抢占、两杆共享一个四轴source；指针取消前后幂等；所有坐标转换完成后再提交轴 |
| `Runtime/Domain/InputProfileV125.cs`：`InputProfileStore/MigrateLegacy` | 版本化输入配置和只读旧Calibration | 不损坏schema2存档/成绩；新失败保留legacy原数据；校准明确绑定身份/控制路径 |
| `Runtime/Input/FlightInputLifecycle.cs`：`Interrupt/RequestResume/SetPageContext` | 当前会话、平台focus/pause、设备掉线、UI菜单状态 | 中断集合与session绑定；重复事件幂等；只在用户明确恢复、原因解除且控制中立后解锁 |

根负责人修改旧 `FlightInput.cs`，保留四轴 `Vector4` 输出和 `JoystickAxes()` 的过渡接口，但迁移每个caller：

1. `Read()`：采样→按source配置filter/calibration→InputRouter.ReadFrame→Vector4。禁止把原Raw合成值送router或对frame再滤一次。
2. 正式输入在同帧缓存一次（如以Time.frameCount为缓存key），UI读数和遥测使用相同frame；context、失焦、断线、设置变更使缓存立即失效。不要因多处读数造成同帧不同操纵来源。
3. 校准页改调用`ReadRawSource`，保留明确设备锁定draft；把旧`Raw()`标为过渡诊断入口并迁走调用点。若暂保留，不能继续返回合成轴，更不能用于校准保存。
4. one-shot命令独立于四轴：Flight允许飞行命令，Menu只允许对应菜单动作和恢复/关闭，Replay不允许H/P/Backspace触发飞行。焦点在TextField/映射编辑时禁止普通字母触发飞行；Escape/Start的消费层唯一，避免同一帧菜单关闭又暂停/恢复。
5. Simulator只消费正式frame/commands；不把平台的raw四轴直接写CurrentInput。FixedUpdate继续用已采样CurrentInput，不在这次输入改造顺带改变物理时序。

## 3. 校准数据迁移与deviceIdentity

本地 Input System 1.16 源码证实 `InputDevice.deviceId` 由runtime分配；`InputDeviceDescription` 有interfaceName、manufacturer、product、serial（可缺省）、capabilities。当前Calibration.device只保存人类可读PrimaryDeviceLabel，**不足以自动证明是同一物理设备**。该判断基于安装包源码，而不是对新Unity API的猜测：`Library/PackageCache/com.unity.inputsystem@552acc76aabe/InputSystem/Devices/InputDevice.cs` 和 `InputDeviceDescription.cs`。

### 新配置记录

每source profile至少保存：`schemaVersion=1`、`profileKey`、`kind`、`identityConfidence`、`identityDescriptor`、`mappingRevision`、四个relative control paths、每轴min/center/max、deadzone、四轴reverse、savedUtc。expo/sensitivity可从现有global值初始化，但之后每源配置对象独立。新profile使用旁路文件（如persistentData/AeroSim/input-profiles-v125.json），不要求第一批重写原Profile schema；提交仓库的是模式/代码，不是用户设备配置。

identity设计：有硬件serial时用interface/manufacturer/product/layout/serial的规范化组合并散列；HID vendor/product等稳定结构用于设备型号辅助。**不能直接散列整个capabilities JSON当硬件唯一ID**，因为字段顺序/运行参数可能变化；不要把driver version当同一设备的强制唯一条件。无serial且同型号多实例时标`model_only`，本次由用户选择明确实例，跨重启默认需复核/重校准，不静默给两只同型号摇杆共用校准。displayName用于呈现，runtime deviceId用于当前sourceId，二者分工明确。

### 旧数据逐分支处理

| 原状态 | 迁移动作 | 用户可见结果与拒绝条件 |
| --- | --- | --- |
| savedUtc为空 | 保留deadzone/reverse为初始偏好，不安装任何校准中心/范围 | 未校准；键盘/触控/手柄可按自己的filter继续使用 |
| savedUtc非空，device只是名字 | 保留legacy完整快照和“待绑定校准”记录；不自动套到当前Joystick | 显示旧校准需要绑定/验证；源选择明确后重新捕获中心/范围，才能成为强身份记录 |
| center/min/max为null/长度非4/NaN/非法次序 | 拒绝校准，保留legacy供复查；过滤偏好单独合法化 | 明确哪条轴无效；正式该校准不生效，不抛异常或重置其他存档 |
| 旧轴index在当前列表内 | 把当前锁定设备的轴转换为relative path，记mappingRevision；只作为待核对映射 | live逐轴验证后保存；同index不能用于另一个设备 |
| index越界 / path不再存在 | 将该映射标无效，不能Mathf.Clamp到末轴 | 引导用户选择轴；invalid来源available=false，Fixed模式neutral |
| 无serial、同型号多只 | 不把型号hash当单机身份批准 | 逐次选择/校准，或提供显式用户复核，不自动跨实例复制 |
| 新格式读取/写盘失败 | 不删除legacy，不改原Profile/成绩；保留内存draft和错误提示 | 不宣称已保存或退出校准成功；重启读旧已提交配置 |

Capture Center与Range全程绑定同source/identity/mappingRevision；键盘/触控旁路输入不会影响极值。开始前做4轴映射/available校验；掉线、换设备或改映射立刻取消draft并清_minimum/_maximum/_center。新range有效条件按源原始量程与min<center<max判定，不能沿用旧固定`maximum-minimum>=.5`来排除合法小范围设备。Save前再核同一identity，原始数值保存；正式router才校准/过滤。

## 4. 触点所有权与中断状态机

共享TouchInputState将**左右杆合成一个touch source**，不是左右两个竞争来源。保存`leftPointerId/rightPointerId`和各杆值；采样顺序继续为left.y/left.x/right.y/right.x。

| 事件 | 条件与动作 | 不变式 |
| --- | --- | --- |
| Down进入空闲杆 | pointer尚未拥有另一杆，接受owner；捕获pointer；提交该杆值 | 同pointer不能同时操纵两杆；已经有owner的杆拒绝后来的Down |
| 第三指Down / 另一杆owner跨入 | 不替换既有owner，不重置另一杆；HUD普通按钮依自身pointer处理 | 双手正在操纵时第三指不抢油门/偏航 |
| Move | 仅ownerId可更新当前杆；用view WorldToLocal和当前几何转换 | 未拥有pointer的Move不污染值；无限/无有效几何输入归零 |
| Up/Cancel | 仅匹配owner的事件归零该杆并释放捕获；先清owner，再release，以处理捕获回调重入 | 一个杆松手，另一个杆仍可独立保持；重复End/Cancel幂等 |
| CaptureOut | 检查pointerId是否当前owner后归零；忽略其他pointer丢捕获 | 不因为另一pointer捕获变化错误清掉正在操作的杆 |
| Detach/禁用/打开暂停或确认弹层/转屏重布局 | 显式Clear两杆、清pointer owner并释放旧捕获，重新绘制knob中心 | UI销毁或隐藏也不会依赖一个不存在的PointerUp |
| OS pause / focus loss | 统一Interrupt，清两杆+命令锁存+router/cache、停止振动、暂停当前会话 | 本帧中断之后轴为零；没有自动恢复飞行 |

`PointerCancelEvent`可用于取消交互；Unity6.3说明禁用元素默认不接该事件，因此禁用/切页必须显式Clear，不能只注册Cancel回调。[Unity6.3 PointerCancelEvent](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/UIElements.PointerCancelEvent.html)

中断协调器保存focusLost、applicationPaused、controllerUnavailable、userPaused等原因，不在收到任一false事件时整体解除另一原因。`OnApplicationPause(false)`可能是启动通知；页面/路由尚未初始化时只更新状态，不能Resume并启动飞行。`OnApplicationFocus(false)`在桌面Cmd/Alt-Tab可触发；实际iOS后台需同时观察pause。回调幂等、UI线程处理，只在真实运行会话中暂停/记录事件。[Unity6.3 OnApplicationPause](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.OnApplicationPause.html)、[OnApplicationFocus](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.OnApplicationFocus.html)

恢复顺序固定：系统原因全部解除→用户点击Resume→确认touch owner已清、键盘飞行键已释放、所选硬件轴按**本源**calibration/deadzone回到中立→清one-shot锁存→router.Resume/Flight context→恢复模拟。保持按键/推杆返回应用时留在“释放控制后恢复”状态。不能用已Suspended router.ReadFrame的neutral结果判定硬件真的中立；共享输入岗位应提供同一过滤实现的`EvaluateSourceForRearm`诊断（后继新增并测试），防止复制两套归一化。旧确认弹层Close()的自动Pause(false)必须经过中断原因检查，不能盖过刚发生的后台/掉线暂停。

## 5. 三端独立文件所有权与交接执行

下面任务是**下一波角色/文件约定**，不是本轮已派出岗位。共享文件每次一个写入者；设备线负责人验本线结果，独立QA不得由该实现者代签。根负责人决定开始/交接并唯一构建；候选07字体文件继续由当前页面程序员拥有。

| 任务ID / 唯一岗位 | 独占文件 | 具体交付 / 依赖 / 本批完成门槛 |
| --- | --- | --- |
| INPUT-CORE-01：共享输入程序员 | 新UnityFlightInputSources.cs、InputDeviceIdentity.cs；后继经根批准扩展InputRouter测试 | 提供独立raw samples、ID策略、source availability与中立诊断；不改页面；跑3,050原核心检查+新身份/映射/重连/两同型号测试。freeze接口给三线 |
| INPUT-DATA-01：共享配置程序员（可接力同一成员） | 新Domain/InputProfileV125.cs与迁移测试 | 上表legacy分支全部有读取/拒绝/恢复测试；旧profile/session字节未改；IO失败不误保存；交接profile ID到CORE和Desktop |
| INPUT-TOUCH-01：共享触控程序员 | 新TouchInputState.cs、pointer状态测试 | 真正owner ID状态机、两杆单source；Down/Move/Up/Cancel/CaptureOut/Detach/第三指均有事件序列测试；iPad/iPhone只调用，不复制状态机 |
| DESKTOP-INPUT-01：电脑程序员 | 新AeroSimApplication.DesktopInputV125.cs、Resources/UI/DesktopInputV125.uss | 桌面source选择、Fixed/Auto提示、键盘控制键表、外接设备4轴可视/校准页面、屏幕双杆显式可用；用INPUT-DATA/CORE接口。1280×720与1920×1080、Text100/200真实点击/输入测试；屏幕杆鼠标验证与物理摇杆分列 |
| IPAD-INPUT-01：iPad程序员 | 新AeroSimApplication.IpadInputV125.cs、Resources/UI/IpadInputV125.uss | 平板横屏左右拇指双杆、竖屏控制布局、外接source切换与控制器详情；TouchInputState适配view坐标；safeArea与分屏重布局主动CancelAll。1024×768/768×1024逻辑视口只是布局门槛；iPad真机双指/后台/外接控制器另验 |
| IPHONE-INPUT-01：iPhone程序员 | 新AeroSimApplication.IphoneInputV125.cs、Resources/UI/IphoneInputV125.uss | 手机版竖屏、横屏双杆和紧凑flight controls；tap target/刘海/Home区、旋转中取消旧触点；844×390/390×844逻辑视口先验无遮挡。真机三指/屏边手势/后台中断/手柄另验 |
| INPUT-ROOT-01：根 / 唯一共享整合者 | 旧FlightInput.cs、Flight.cs、Application.cs、Pages.cs/DesignV1.cs必要caller、Simulator.cs，新增FlightInputLifecycle.cs | 先接共享输入/配置，再挂三端view；改TouchStick委托sharedstate，改Raw callers，统一command/context。页面成员不同时写这些热点；保留字体07和原23图的变化边界 |
| QA-DESKTOP-INPUT、QA-IPAD-INPUT、QA-IPHONE-INPUT：独立QA分线接力 | 新verification/v125-qa/input-integration/工具和各线紧凑结果文档 | 冻结契约、真实事件路径/四轴/命令/中断/布局结果；分别标synthetic、logical viewport、physical controller、real iOS device，不将计数混算 |

三端共同API变更要先由技术负责人/root批准并同步调用方；借调时原成员交出本任务ID、未完成差异和测试结果并停止写文件，再由root赋给接手者。任何成员不得自己启动第二个Unity构建、切分支或提交其他人的半成品。

## 6. 顺序与可执行验收门禁

### Gate A：原始采样与迁移（不启动设备UI）

冻结CORE/DATA两任务，独立QA复跑原3,050检查并追加：持有键盘+非中立手柄无叠加、pad中立时键盘可用、JoystickA校准不影响Keyboard/Touch/Pad/JoystickB、Fixed断线零输入、同型号2台无自动共享身份、null/长度3的旧数组不崩、非法index不偷偷映射末轴、IO失败保留原配置与成绩。Gate A失败不动旧Read接线。

### Gate B：共享触点与生命周期序列

冻结TOUCH，独立事件序列至少包含：left Down(id11)/right Down(id12)→两杆同时Move→第三Down(id13)拒绝→left Cancel(id11)→right仍活动→CaptureOut(id13)不清right→rightUp(id12)归零；全流程Detach、暂停开弹层、OSpause/focus乱序重复、丢捕获、disable/enable、pointer重新分配均无stale input。构造事件只能证明逻辑；之后应用真实UI事件重演，真机最终另验。

### Gate C：旧输入正式接线后的原生回归

根冻结INPUT-ROOT，不在QA中途改源；新构建记录源码SHA、Runtime DLL hash和candidate路径。保留原06/07 smoke/Space/PauseTelemetry字体门禁，再运行适配器输入和校准向导实操；读数与Simulator.CurrentInput/Telemetry同源同轴。校准时另device/keyboard输入不影响min/max；文本编辑输入P/H不拍照/返航；Paused中Backspace不重置；Escape/Start单次行为；focuslost清轴并冻结时钟、返回按住杆不自动飞，explicitResume+neutral后可恢复。

### Gate D：电脑、iPad、iPhone分别交付

- 电脑：两个指定窗口尺寸及缩放/文字100–200%，屏幕杆拖动、键盘与外接Gamepad/Joystick，各轴实际动作、Fixed掉线/重连、Cmd-Tab实际运行证据；物理未到位时该字段pending，不阻止其他纯代码工作。
- iPad：逻辑1024×768/768×1024、safeArea/分屏先通过；Unity iOS模块/Xcode可用后导出和编译；真实iPad双指/第三指、转屏/分屏、后台、手柄、安全区、帧率/内存分别记录。
- iPhone：逻辑844×390/390×844先验HUD/双杆互不遮；真实iPhone屏边手势/三指、Home/来电后台、保持输入返回、转屏与手柄独立证据。

每线的“逻辑视口通过”“原生编译通过”“真实设备通过”“物理控制器通过”是独立字段。上游字体候选07验证只修那一批字体，不自动升级本输入计划状态；用户每型外观确认继续独立有效，未经确认资产不得通过输入/机型选择入口接入。

## 7. 动态字体调度的并行只读风险建议

先前只读发现Responsive.cs的`_baseFonts`、`_typographyScheduled`与30ms deferred ApplyTypography可能有继承累乘/脱panel后gate未清风险，已发送当前modal程序员与root。23:45额度恢复后复查**新的未构建工作树补丁**，已看见以下源码处理（本成员没有编辑这些文件）：

1. 新增scheduled item、root/generation；OnDisable和root Detach调用CancelTypography并Pause、清gate/队列，陈旧generation回调退出；scope检查同panel且属于当前root树。原队列卡死风险已有源码级处理，不继续当作仍存在的已证实缺陷。scheduler只在attached panel时执行，Pause可取消，已核Unity6.3主源。[IVisualElementScheduledItem](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/UIElements.IVisualElementScheduledItem.html)
2. `_originalFonts`保存首次inline状态，移除/cancel先恢复再删基线；预置timer没有original记录时fallback恢复_baseFonts值。同元素带scaled inline重挂的先前风险已有处理，等待QA重挂/disable-enable验证，不再直接报告当前必定累乘。
3. fresh tree先统一采所有Text基线、再放大父子，覆盖“同一新树一边改父一边读子”风险。三个当前弹层附在非Text root，ActionButton使用自身text，页面成员明确本批不覆盖所有通用嵌套Text形态。
4. **仍需限定为拓展QA风险**：未来在一个已cached且Text200的TextElement父下追加无独立USS字号的子TextElement，fresh pass采resolvedStyle仍可能继承已缩放值；仅凭resolved值无法判定继承与用户自定义同字号。需有该具体动态路径/真实断言后再扩展基线来源策略，不能在目前三个弹层没有该路径时声称已复现失败。
5. FlightDesignV1的timer专属基线compact26/full42/fallback22仍必须保持；已有原值恢复fallback，QA验缩放/布局切换后timer正确，而非统一覆写源token。
6. `RefreshUiPreferences()`另外排队的geometry callback未由CancelTypography持有/取消，这是既有低优先生命周期边界。先做快速禁用/启用与root替换测试，若有失败再单独处理，不扩大本次07弹层字体修复范围。

冻结后的验证应覆盖Text100→125→150→200→100同弹层反复切换、快关重开、延时任务未执行即disable/enable、page/root替换，测实际字号与geometry。本轮只有只读源码复查，未启动应用；这些处理是否实际通过候选07须由页面程序员和独立QA留证。

## 8. 本计划交付边界

只新增本文；无生产源码改动、构建、Git操作或设备设置。本批文档由根纳入验证后统一提交推送，仓库：[AeroSim](https://github.com/yijunchai2026-gif/AeroSim)，分支：[codex/v125-product-lines](https://github.com/yijunchai2026-gif/AeroSim/tree/codex/v125-product-lines)。尚无本文独立commit，本成员不代root宣称上传。
