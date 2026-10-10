# AeroSim 1.25 多产品线技术架构与交接契约

调研日期：2026-10-09，美国洛杉矶时间。目标来自用户最新要求：电脑、iPad、iPhone 各有独立产品线，由总负责人统一指挥并调配程序员；三端均支持摇杆，电脑还支持键盘；每个 DJI 机型外观逐一由用户确认；每批已验证改动提交并推送 GitHub；可交付后发送试用链接和验收入口。

本文是基于当前工作树的实施方案，**不是三个平台或全部机型已完成的证明**。本轮仅做技术只读检查与文档新增，没有 Unity 构建、Apple 签名、安装或真机测试。

### 当前阅读勘误（2026-10-10）

原文第6章检查了错误的`Unity.app/Contents/PlaybackEngines`位置，得出的“iOSSupport缺失”推断已撤回；真实模块文件位于Unity.app同级`PlaybackEngines/iOSSupport`。文件存在仅为`IOS_MODULE_FILES_PRESENT`，不证明Editor iOS target、新导出、编译、签名或真机可用。完整active Xcode仍不可用。原错误检查保留并明确标注，不据此重复安装模块。

本文其余源码/接口表为2026-10-09方案快照；其中“尚未实现”不覆盖后来单独验证的纯router/触点owner/身份子集，也不把它们当三端接线通过。最新状态以[1.25断点](V125_PROGRESS.md)和[PRD第0章](../飞行模拟器产品设计文档.md#0-当前执行合同与阅读索引)为准。当前Mavic主展示改用独立`dji-mavic-4-pro`生成审核稿；不更名或批准2016`dji-mavic-pro`。

## 1. 当前代码证据与可复用边界

| 项目 | 实际代码或本机证据 | 1.25 的含义 |
| --- | --- | --- |
| 引擎与输入 | `AeroSim/ProjectSettings/ProjectVersion.txt` 为 Unity `6000.3.25f1`；`AeroSim/Packages/manifest.json` 包含 Input System `1.16.0`、UI Toolkit、Physics；`ProjectSettings.asset` 的 `activeInputHandler: 1` | 三端继续使用同一 Unity 引擎、物理和输入包，减少算法与回放分叉；不以新建三个相互独立的模拟引擎实现独立产品线 |
| 飞行与记录 | `Runtime/Simulation/FlightSimulator.cs` 的 `Begin/BeginReplay/Pause/Stop/Seek`、`FlightConfig`、`SessionRecord`、`TelemetryFrame` 已有真实会话与遥测 | 共享核心保留会话、暂停、任务、照片证据、回放语义；平台页面调用同一服务 |
| 当前物理 | `FlightSimulator.BuildAircraft()` 固定 `Body.mass = .8f`；`ApplyAssisted()` 使用 Normal/Sport 的通用速度；Manual 使用全局 `rate/thrust`；`SessionRecord.physicsVersion` 默认仍为 `AeroSim-0.2` | 现有外观切换不等于每个 DJI 的真实物理。新机型需分别标记外观与飞行参数验证状态；版本迁移需要真实算法/数据版本，不能只改字符串 |
| 当前机型 | `Runtime/Domain/AircraftCatalog.cs` 只有 Mini 4 Pro、Air 3S、Mavic 3 Pro、Avata 2 四个 DJI 近似外观和两个通用训练机 | 需要重建完整历史目录、权威来源、变体和用户确认记录；旧四种也不能自动推定为已获用户逐机型批准 |
| 当前渲染 | `AircraftVisualModel.cs` 用六种 `AirframeShape` 和程序化基础网格；`AeroSimApplication.Hangar.cs` 逐机生成 `768×432` RenderTexture，和飞行/回放复用外观 | 可复用预览渲染与资产入口，但增加名字、换壳纹理不能体现 Phantom、Inspire、行业/农业等独特结构；全目录不能每次同步生成数百张 RT |
| 当前布局 | `AeroSimApplication.cs::OnGeometryChanged()` 按逻辑宽高启用 `layout-desktop/tablet/phone`；`Responsive.cs` 执行比例、字号和叠列；移动端读 `Screen.safeArea` | 这些是统一页面的响应式降级，尚不是三条独立设备产品线。必须增加平台壳、各端交互/导航/机型页/HUD 与独立规格 |
| 当前版本 | `ProjectSettings.asset` 为 `bundleVersion: 1.0.0`；`ProjectSetup.Configure()` 仍写 `0.4.0`，`BuildDesignV1Candidate()` 再写 `1.0.0` | 构建流程需要集中版本清单和输出目录，避免 Configure 误回退版本。用户显示版本保留“1.25”，建议原生三段版本为 `1.25.0`；改版本字段本身不构成交付 |
| 既有 QA | `PreviewCapture.cs` 用虚拟 Keyboard/Gamepad 并直接写 `TouchLeft/TouchRight` 测输入；`verification` 明确桌面窄视口不是 iOS 真机；23 张原图仍须逐页检查 | 复用现有 QA，但新增真实触控事件和设备验证层；以测试注入向量证明四轴转换，不能替代双手多点触控或物理摇杆实测 |

原始 23 张概念图继续作为已存在页面的视觉参考。新设备专属页面和新机型目录需要对应图稿与交互规格，概念图与真实截图分别记录；程序测试通过不自动形成用户视觉验收。

## 2. 产品线及根负责人的指挥规则

以下为岗位分工与文件所有权方案，不表示这些岗位已经全部派出。四个并发槽按波次使用，总负责人始终掌握最终集成；每条线保留自己的待办、交接记录和 QA 结果，程序员可以被调往其他线。

| 产品线 | 负责人/程序员职责 | 独立 QA 的职责 | 对外产物 |
| --- | --- | --- | --- |
| 电脑 Desktop | 设备线负责人冻结桌面规格；页面程序员负责宽屏/窗口导航与机库；输入程序员负责键盘、鼠标、物理摇杆/手柄、轴映射与校准 | 桌面 1280/1680/1920/高分屏及缩放、按键冲突、摇杆连接/掉线/重连、RT/资产性能；Windows 如未构建需明确单列 | macOS 候选应用与设备线测试证据；Windows 必须有独立构建/测试后才列为可试用 |
| iPad | 设备线负责人冻结平板规格；程序员负责大屏分栏、机型详情、双虚拟摇杆、横竖屏/多任务尺寸与外接控制器 | 真实 iPad 触控双指、转屏、安全区、外接控制器、暂停恢复、内存与热降频；桌面平板视口证据单列 | iPad 页面图稿、逻辑视口证据、iOS 导出与实际设备运行证据 |
| iPhone / iOS 手机版 | 设备线负责人冻结手机规格；程序员负责窄屏导航、拇指操作区、双摇杆、机型分页详情、刘海/Home 指示器安全区 | 真实 iPhone 双指与第三触点、电话/后台中断、旋转/暂停、紧凑 HUD、弱性能设备和控制器 | iPhone 页面图稿、逻辑视口证据、iOS 导出与实际设备运行证据 |
| 共享核心 | 技术负责人控制输入、会话、机型、设置/存档、任务、遥测和资产契约；核心程序员维护模型和迁移，不接管各端页面排版 | 核心 QA 独立检查数据迁移、输入来源隔离、确认门禁、旧回放与资源释放；三端线回归共享变更 | 稳定 API、版本化数据与可重复核心测试 |
| 机型 / 3D 美术 | 历史/经典机型美术、现代消费机美术、FPV/行业/农业美术按机型 ID 分配；专人负责材料/LOD/渲染和图稿；目录程序员维护数据与用户审核入口 | 逐机检查官方来源、轮廓、摄像头、传感器、折叠机构、起落架、旋翼布局；确认记录与图稿 revision 一致 | 每型简介、来源、正/侧/俯图、可旋转预览、用户审核记录、获准正式资产 |
| 集成 / 交付 | 根负责人唯一处理共享入口、源码冻结、Unity 构建与提交推送；核对每个线的实际完成状态 | 验证冻结源码 SHA、构建哈希、机器/平台/设备、结果/截图引用；独立视觉 QA 不修改参考图 | 1.25 候选、GitHub commit/分支/远端 SHA、各设备可用入口和用户验收待办 |

独立产品线首先指独立设计、职责、验收与设备交付记录；iPhone/iPad 可由同一个 Universal iOS 二进制承载各自 UI。是否需要两个商店应用属于后续发布选择，不需要复制共享引擎才能实现产品线独立。

### 调援与交接

1. 每项任务登记任务 ID、产品线、拥有者、独占文件、依赖接口版本、预期输出和 QA 动作。成员启动前读 Git 状态与工作记录，保留已有改动。
2. 同一时段一个文件只有一个写入者。根负责人控制 `AeroSimApplication.cs`、既有 `Responsive.cs/FlightDesignV1.cs`、`FlightInput.cs`、`TrainingData.cs`、`ProjectSetup.cs` 等共享热点；其他成员先用新平台文件或提补丁建议。
3. 调援前原负责人交接已完成步骤、具体失败与测试证据，并停止写对应文件。接手者先核对差异和接口，不重复重做已验证工作；用户机型确认不因调援失效或被代理替代。
4. 技术负责人确认跨端接口变更；三端调用方收到迁移说明后分线回归。共享核心变动不能只在作者自己的产品线验证。
5. 根负责人统一冻结，然后由唯一构建岗位生成候选。QA 测冻结构建；期间新的页面/美术改动进入下一批。最终人工验收由用户完成。

## 3. 输入能力：已有基础与实际缺口

### 已存在的基础

`Runtime/Input/FlightInput.cs::Raw()` 输出 `Vector4(throttle, yaw, pitch, roll)`。左触摸杆映射油门/偏航，右杆映射俯仰/横滚；Input System Gamepad 的 left/right stick 使用相同顺序；Joystick 从映射轴取值；键盘支持 `R/F`、`E/Q`、`W/S`、`D/A`、方向键与 `PageUp/PageDown`。`Read()` 执行 deadzone、reverse、expo、sensitivity。

`FlightSimulator.Update()` 还处理 Escape/手柄 Start 暂停、H 返航、C 视角、P/右肩键拍照和 Backspace 重置。`InputCapabilityService` 有 Gamepad/Joystick/Touchscreen/Keyboard 能力检测和设备变更事件；`OnInputCapabilitiesChanged()` 在控制器掉线时暂停真实会话。

`AeroSimApplication.Flight.cs::TouchStick` 已有 PointerDown/Move/Up 与 PointerCaptureOut 复位；`FlightDesignV1.cs` 把双触摸杆加入 HUD，在移动平台或窄视口显示。电脑的 Show Touch Controls 设置可以显示相同杆，但默认宽屏隐藏。1.25 桌面应有显式摇杆/控制器入口、四轴读数与可选屏幕摇杆，不将“有键盘”当成“有物理摇杆”。

### 必须补的缺口

| 缺口 | 实际影响 | 下一批实现与验收 |
| --- | --- | --- |
| 所有来源直接相加再 clamp | 两个控制器/触控与键盘同时作用可能放大输入，当前 Gamepad 存在时 Joystick 被排除；没有明确当前操纵者 | 引入按来源采样和显式选择/接管，默认单操纵源；键盘的会话指令独立。验证两个来源同时产生输入时按规则选中，不意外叠加 |
| 摇杆校准作用于混合结果 | `Read()` 检测任何 `Joystick.current` 和非空 savedUtc 后，对混合的键盘/触控/Gamepad 输入都减中心/除量程 | 校准只应用于匹配 device identity 的 Joystick 原始四轴；触控/键盘/手柄使用各自过滤。验证仅连接摇杆而不操纵也不会改变其他输入 |
| 轴索引越界被 clamp | 错误映射静默重复最后一轴；`JoystickAxes()` 枚举的轴次序和各设备不同 | 保存匹配设备的 control path，迁移旧索引；无效映射呈现可修复提示。验证重连/更换型号/缺轴 |
| TouchStick 新 PointerDown 可覆盖旧 `_pointer` | 第三触点可抢占正在使用的一个杆，未明确处理 PointerCancel | 首触点独占单杆、PointerCancel/CaptureOut/禁用/离页统一清零；真正双指事件检查两杆独立，第三指无抢占 |
| 后台与失焦没有专用生命周期清零 | 当前 `runInBackground` 开启，找不到 OnApplicationPause/OnApplicationFocus 处理，已保存触控向量可能遗留 | 平台生命周期统一 Suspend：暂停模拟、复位触控/命令锁存、归零 haptics；回来保持暂停并由用户恢复。验证后台、来电、失焦和控制器断开 |
| 映射与校准只一组全局设置 | `Calibration.device` 和整组 UserSettings 不支持每设备独立 profile | 按 device identity 存输入 profile；各线共享数据模式但 UI 独立。确认过滤参数合法，避免 deadzone=1 引入除零 |
| UI 与飞行的按键冲突 | 1.0 已加 Arrow/Space guard，状态依赖界面焦点；当前 sim 直接轮询所有键 | Flight/Menu/Replay input context 分层；文本编辑/暂停菜单不执行飞行重置/拍照/返航。保留已有独立 Space 探针并新增按键上下文动作检查 |
| 物理 DJI 控制器未验证 | 检测为通用 HID/Gamepad 不能证明任意 DJI 控制器可在 iOS/电脑直接使用 | 优先证明通用 Input System Gamepad/Joystick；逐实际型号记录接口和匹配。硬件不可识别显示能力限制，不展示虚假已连接 |

## 4. 稳定接口方案

以下是**拟新增契约**，当前源码尚未实现这些类型。接口先冻结，再由各产品线消费；已有四轴顺序、遥测语义和存档兼容保持明确。

| 契约 | 最小数据/行为 | 所有权与调用方 |
| --- | --- | --- |
| `FlightControlFrame` | `throttle/yaw/pitch/roll` 各 `[-1,1]`，`sourceId/sourceKind`，时间戳，context，one-shot commands；抬手/取消/暂停时轴归零 | 共享输入采样；`FlightSimulator` 消费已筛选的 frame，各设备壳不直接改 `CurrentInput` |
| `IFlightInputSource` | `Id/Capabilities/ReadRaw/Reset`；Keyboard、Gamepad、Joystick、Touch 各自实现，选中的四轴源唯一 | 输入线；iPad/iPhone 只向自己的 TouchSource 提交指针结果，桌面提供硬件输入 |
| `InputRouter` | `SetContext(Flight/Menu/Replay)`、`SelectSource`、`Suspend/Resume`、`ReadFrame`；校准按源施加，合法性校验和设备变化事件 | 共享核心；三端 UI 调用统一能力/选择/断线契约 |
| `PlatformPresentationContext` | `platformLine`、物理尺寸与 UI 逻辑尺寸、safeArea、orientation、textScale、preferredInput、lifecycle、质量预算 | 平台入口；页面选型和 HUD 布局使用该对象，不能把 width<760 直接等同 iPhone |
| `IPlatformShell` | 路由/导航、机型列表与详情布局、HUD placement、控制器配置 UI；统一 session/view model | Desktop/iPad/iPhone 文件独立。共享图标/颜色/字体 token 可复用，各壳对其设备交互负责 |
| `AircraftDefinition` | 稳定 `aircraftId`、官方名称/别名、系列、年代/来源、简介、variant、物理规格的来源、assetRevision、physicsProfileId、各状态 | 目录线提供；选型 UI 使用稳定 ID，别名只用于旧存档迁移，缺项不得无声退回 Mini 4 Pro |
| `AircraftReviewRecord` | `aircraftId/assetRevision`、reviewState、确认人/时间、用户消息依据、备注、图稿哈希 | 根负责人根据用户实际回复登记；美术/程序员不得自动写 `UserConfirmed` |
| `IAircraftAssetProvider` | `LoadPreview(id,revision)`、`LoadApprovedFlightAsset`、`Release`；失败有真实状态；LOD/材料按预算选择 | 美术资产线与共享渲染线；机型列表用轻量缩略图，详情才加载一个可旋转预览 |
| `ISessionService` | 继续 `Begin/Pause/Finish/Replay/Seek` 的语义；机型切换/离页对真实会话的影响明确 | 核心线；页面不能为了看设置或切详情而隐式销毁飞行，暂不支持的动作明确留在会话前后 |

首次迁移不重写全部模拟器。先让旧 `FlightInput` 委托 InputRouter，保留已验证的四轴消费点；机型扩容先引入读目录与审核状态，再接获准资产。`FlightConfig` 与 `SessionRecord` 将稳定机型 ID 和 visual/physics revision 一并记录，旧名称存档通过 alias 明确迁移，旧回放读取所需 revision 的缺失要可见。

## 5. 机型目录与用户逐型确认门禁

“从最早到现在一个都不能少”需要可核查目录。目录岗位按官方产品/下载/手册/停产资料核对历史消费、FPV、行业、农业和独特飞行平台，记录核查截止日期；每个独立飞行机型/官方硬件变体有稳定记录。套装、遥控器组合、颜色/地区名称与同机别名建立映射，不能重复算成新无人机或静默漏掉变体。来源不足的候选留在“待核实”清单，不能直接删除造成完整假象。

每型在审核中心都展示官方名称、年代、类别、简短介绍和已核验来源，以及待核验字段；美术产物包含同 revision 的正/侧/俯视图、结构细节和可旋转预览。美国 UI 使用英文、美国单位与可切换公制；核验来源保留原始量值，单位换算不制造新的官方性能。

状态流必须有独立记录：`CatalogUnverified → ReferenceVerified → DraftArtwork → AwaitingUser → UserConfirmed → ProductionAsset → Integrated → DeviceQA`。用户退回则进入 `ChangesRequested`，下一版改 revision 后再次等待确认。`UserConfirmed` 只由用户明确消息形成；批准旧 revision 不能自动批准结构/材料大改后的新 revision。

- 待确认也可以在目录和专用审核页浏览；不能进入新 1.25 正式飞行选择列表。
- 每个机型分别确认，不按整个系列一次代替所有机型。即使外观接近，DJI 各硬件变体分别核对。
- 1.25 的启动、任务、Academy、Challenge、Professional、回放入口均检查审核和 asset revision，不能仅禁用机库按钮留下其他直达路径。
- 旧 0.4/1.0 近似模型保存在原候选/原分支。1.25 新审核记录从真实用户确认起算，不把原 demo 自动批准。
- 如果目录/每型外观还在等用户确认，可以交付带审核中心的开发候选并准确显示完成数量；“所有机型的正式版全部完成”必须等对应确认和设备门禁通过。

## 6. iOS / iPad 构建、设备与签名边界

2026-10-09 本机只读检查结果：

- **历史错误检查（结论撤回）**：当时只查`6000.3.25f1/Unity.app/Contents/PlaybackEngines`，在该错误位置未见iOSSupport，不能推出模块缺失。2026-10-10改查同级`/Applications/Unity/Hub/Editor/6000.3.25f1/PlaybackEngines/iOSSupport`，模块文件存在。Mac文件夹内`UnityEditor.iOS.Extensions.Xcode.dll`也不能单独证明iOS target可用。
- `xcode-select -p` 为 `/Library/Developer/CommandLineTools`；`xcrun --find xcodebuild` 报找不到工具；标准 `/Applications/Xcode.app` 和 Xcode-beta.app 不存在。当前未发现可用完整 Xcode。
- `ProjectSettings.asset` 为 `com.aerosim.training`、iOS 最低版本 `15.0`、iPhone 的 scripting backend `1`；`ProjectSetup` 有 Universal iPhoneAndiPad 与 IL2CPP 导出入口。仅有设置不能形成实际导出或运行证据。
- 未查看私密签名凭据。当前配置的 Apple team/provisioning 字段为空、automatic signing 为关闭；账号、设备注册和发布签名需要实际设置并取得相应授权。
- `verification/GRAPHICS_V2.md` 记载 2026-10-05 旧版 iOS 成功导出、当时无完整 Xcode。该历史证据不覆盖本机当前模块、当前1.25代码或设备运行。

执行顺序为：完成可本地做的共享核心与独立布局 → 验证现有Unity iOS target，并准备完整active Xcode（确有缺项再按工具审批下载/配置，不默认重装模块）→ 单独冻结源码、导出Universal iOS Xcode工程 → 原生编译 → 配置有授权的开发签名 → 安装iPhone和iPad → 分线真实设备QA。iOS Simulator、桌面逻辑视口、工程导出、Apple原生编译、签名、真机与TestFlight是不同证据字段。本轮文档整理未执行安装、切换developer目录或签名。

电脑线先交付有证据的 macOS；若电脑范围扩展到 Windows，则另装对应构建支持并在 Windows 实际运行，不能把 macOS 二进制链接当作两者可用。发布与 TestFlight/托管下载链接只在该产物实际存在且权限可用时提供。

## 7. 下一批可实施文件分工

先由根负责人归档当前已验证 1.0 工作与失败证据、修正断点，按用户 GitHub要求处理 commit/push。根负责人已通知建立 `codex/v125-product-lines` 并推送候选03基线；后续交付由根核对远端 SHA，明确保留 `codex/v04-ui-proportions` 及原 0.4 应用。分支迁移由根处理，成员不自行 checkout 或 merge。现有未提交工作不得误归为自己新增或被覆盖。

| 波次 / 文件边界 | 唯一写入岗位 | 本批范围 / 交接 |
| --- | --- | --- |
| A：`docs/V125_ARCHITECTURE.md` | 技术负责人 | 本文；只读证据与冻结契约，根审核后分派实现 |
| A：新 `docs/V125_AIRCRAFT_CATALOG.md`、目录数据文件与来源表 | 目录程序员 / 产品研究 | 全型号核查、变体/alias、首批审核清单；具体新数据路径先登记，避免与美术清单冲突 |
| A：新 `assets/aircraft-review/<aircraftId>/<revision>/` | 每型指定美术 | 两名以上美术按系列/机型 ID 互斥制作审核图；来源、revision 与结构说明，不能直写正式飞行资源目录 |
| B：新 `Runtime/Input/InputRouter.cs`、`FlightControlFrame.cs` 和各 Source | 输入程序员 | 输入来源隔离、上下文、校准、指针/生命周期复位；交付核心验收与旧行为兼容说明 |
| B：旧 `FlightInput.cs`、`FlightSimulator.cs`、`TrainingData.cs` | 根 / 核心整合程序员 | 最小接线、机型ID/revision存档迁移；先冻结 A/B 接口再动既有热点，不与其他成员同时写 |
| B：新 `Runtime/Platform/PlatformPresentationContext.cs` | 平台核心程序员 | 按真实平台、尺寸/安全区、能力提供上下文；无完整设备前只标逻辑视口 |
| C：新 `AeroSimApplication.DesktopV125.cs`、`Resources/UI/DesktopV125.uss` | Desktop 页面程序员 | 电脑专属选型和详情/控制器 UI；主入口仅由根接线 |
| C：新 `AeroSimApplication.IpadV125.cs`、`Resources/UI/IpadV125.uss` | iPad 页面程序员 | 平板双栏与触控 HUD；依赖共享 context/router/机型 view model |
| C：新 `AeroSimApplication.IphoneV125.cs`、`Resources/UI/IphoneV125.uss` | iPhone 页面程序员 | 手机列表/详情/底部导航和拇指 HUD；不得复制物理或机型目录 |
| C：新 `Runtime/Domain/AircraftReviewRegistry.cs`、`AircraftDefinition.cs`、资产 provider | 目录/资产程序员，按文件登记 | 确认门禁、轻量列表、按需3D加载、正式选择检查；目录与审查数据统一来源 |
| D：旧 `AeroSimApplication.cs`、`Responsive.cs`、`Flight.cs/FlightDesignV1.cs`、`ProjectSetup.cs` | 根负责人与唯一构建岗位 | 分平台壳接线、版本清单、隔离构建输出。冻结后各成员停止写入构建源 |
| 每波：新 `verification/v125-qa/` 工具/紧凑结果，`docs/V125_*_QA.md` | 独立 QA（测试不由对应页面作者批准） | 固定测试动作和原图不改；按输入/页面/设备/审核门禁出独立结果，根筛选必要证据提交 |

并发建议是每波根 + 两名实施成员 + 一名 QA/技术审查；需要三端同时开发或三名美术时接力分波。根可根据阻塞把程序员调援，但先完成文件所有权交接，不靠大量空代理代替产物。

## 8. 冻结、验收与交付门禁

1. **接口与数据门禁**：稳定 ID/alias 唯一；目录状态与介绍有来源；每型外观确认绑定 revision 和用户依据；不把未知机型静默解析为默认飞机；既有存档/回放有迁移证据。
2. **输入门禁**：各四轴方向与 deadzone/expo 正确；不同源不误叠加/误校准；触控双指独立、第三指不抢占、松手/取消/后台清零；物理控制器断线暂停、重连后不自动飞；菜单和文本编辑不触发飞行命令。合成设备与真实硬件结果分列。
3. **布局门禁**：每条设备线的首页、选机型、详情、审核、控制器、飞行、暂停、回放各有规格和实测截图。检查安全区、字号100–200%、产品倍率、缩放、转屏/窗口、控制区互不遮挡；已有23原图逐页保留视觉差异记录。默认电脑控制键/摇杆入口与移动双摇杆必须实际可操作。
4. **资产门禁**：正式资产必须 UserConfirmed；正侧俯/旋转预览来自同 revision；不同机型结构和材料真正区分；美术渲染图不冒充运行时3D。机型列表按需加载与资源释放有性能证据，三端 LOD/纹理预算经实际测量再定。
5. **设备门禁**：macOS 原生、iPad 真机、iPhone 真机分别记录构建 SHA/哈希、设备与系统、输入硬件、帧率/内存/热状态。没有设备只能交付该线开发预览/逻辑视口证据，不把模拟分辨率升级为设备通过。
6. **GitHub 门禁**：根选择已验证文件，排除凭据/Unity缓存/全量QA原始截图；commit 后 push，再比较远端分支 SHA。认证/网络失败如实标明本地 commit 和未完成 push；仓库为 `https://github.com/yijunchai2026-gif/AeroSim`；未获明确许可不合并 main、不 force-push。
7. **用户交付门禁**：全部达到范围对应的可交付标准后，给用户真正可访问的1.25试用入口、设备覆盖/剩余边界、机型审核入口、GitHub地址及具体commit/分支。用户未逐型批准或未验收时保留待办；发送开发候选链接不能替代用户最终验收。

总负责人按每条线分别判断进展。某端已可运行可以先提供注明范围的候选预览，但整个1.25完成必须同时满足已承诺设备、机型与GitHub交付要求；“只写1.25版本号”不能让任何门禁通过。
