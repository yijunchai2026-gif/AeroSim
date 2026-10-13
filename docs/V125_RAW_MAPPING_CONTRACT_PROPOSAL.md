# AeroSim 1.25：raw 四轴路径与 mapping fingerprint 最小契约提案

2026-10-12，America/Los_Angeles。状态：**PROPOSAL / 待根负责人冻结；未实现、未测试、Gate A 未关闭**。根指定读取基线为 `c1916a0f63a4a17d2af8767e4fd2dcdcab79ee2f`，本成员不自行执行 Git。本次只新增本文，交出后停止写入；生产源、作者测试、独立 QA 预期及冻结文件须根另行授权。工作区仅为既定 `v125-remote-review/飞行模拟器` worktree，不修改原开发目录。

## 1. 实际断点与本波边界

已完整阅读 worktree 的 AGENTS、[Gate A](V125_GATE_A_CONTRACT.md)、[最新 progress](V125_PROGRESS.md)、FlightControlFrame.cs、InputDeviceIdentity.cs、InputIdentityRegistry.cs、当前 incoming FlightInput.cs，另核对[registry 提案](V125_REGISTRY_CONTRACT_PROPOSAL.md)及其 freeze/v1/v2/v3 addendum。已完成的 registry/resolver 验证不重复运行，也不把它们算成 mapping 验证。

当前 FlightInput 仍以 `.current` 取设备、读取 stick `ReadValue()`、按 index clamp 取 Joystick、来源相加，并对合并结果套 LocalStore 全局校准；没有调用纯 identity/registry/router/touch 新核心。其读取 SHA 为 `c7a2e9da426b0a527511a645442ed4161f9754ff19d039509bd7c51539fa2a99`，不同于 Gate A 的历史旧源指纹；本文不改它，也不把 incoming 代码当作已接线。

本波建议只实现 **System-only 的静态四轴目录校验、不可变 DTO 与确定性指纹**，新文件建议 `Runtime/Input/RawInputMapping.cs/.meta`，namespace `AeroSim.Input`。文件名、meta、作者 runner/测试/实现交接由根冻结后分配；不得借机创建另一份 identity registry。

根负责人另发现 incoming PRD v4.0 与本线程既定1.25三端/23原图目标有冲突，正在请求用户决定优先级。本文**不是总体目标改判**，不把v4自动升为唯一授权，也不修改PRD/旧Gate A或迁移原视觉目标；这里只提出两种目标都需要的公共输入纯子集，不能据本文产生正式接线或新用户映射授权。

不含设备轮询、Unity/InputSystem 引用或实际控制器对象、raw 数值读取/缓存、source availability、mapping 状态存储/MappingRevision 自增、SetMapping 持久写操作、filter、校准、sidecar、旧 index 迁移、router/生命周期/UI/三端接线。没有 Unity/native 构建、真实用户存档访问、物理控制器或 iPad/iPhone 测试。本模块验收最多称 **PURE_MAPPING_VALIDATION_PASSED**；不能称 raw 采样、processor-bypass、热插拔或 Gate A 完成。

## 2. 拟公开 API：合法映射候选不是 UserValidated 授权

```csharp
// 拟新增；不是当前已存在 API。
RawMappingValidationResult RawInputMapping.ValidateHardware(
    FlightInputSourceKind kind, string layout,
    IReadOnlyList<RawControlDescriptor> catalog, AxisMapping requestedMapping = null);
RawMappingValidationResult RawInputMapping.GetBuiltIn(FlightInputSourceKind kind);
```

这两个函数无状态、无 I/O，不修改参数、不生成 SourceId/ProfileKey、不保持 catalog/mapping revision，不抛常规无效输入异常。调用方串行提供稳定 DTO/list；函数先防御性复制集合引用，DTO 全部 sealed/get-only。内存耗尽等进程故障不包装成普通成功或业务拒绝。

| 调用 | 本子集结果 | 后继正式 MappingStatus 的界限 |
| --- | --- | --- |
| GetBuiltIn(Keyboard/Touch) | BuiltInValid，固定指纹；Mapping=null、Controls 为空 | 可提供不可编辑逻辑映射；本模块不读取按键/触点，不证明 SourceAvailable |
| GetBuiltIn(Gamepad/Joystick/None/未知值) | Rejected / UnsupportedBuiltInKind | 不伪造硬件目录或默认 Joystick 轴 |
| ValidateHardware(Gamepad, ..., requestedMapping=null) | 只验证固定 `leftStick/y,leftStick/x,rightStick/y,rightStick/x`；成功 BuiltInValid | 缺任一合法轴为 Invalid，不改读 trigger/dpad，不提供 remap |
| ValidateHardware(Gamepad, ..., requestedMapping 非 null) | Rejected / MappingNotEditable，即使四路径恰好等于 built-in | 未来 SetMapping 仍须拒绝，不以等值操作偷偷开放 remap |
| ValidateHardware(Joystick, ..., requestedMapping=null) | Missing / MappingMissing | 初始 Missing，无 index/枚举次序推测 |
| ValidateHardware(Joystick, ..., 四轴全部合法) | **JoystickCandidate**，有指纹和解析到的四轴 metadata | 只是结构候选；必须后继对当前明确实例做 live 逐轴验证，才能变 UserValidated |
| ValidateHardware(Keyboard/Touch/None/未知值, ...) | Rejected / UnsupportedHardwareKind | 后继 SetMapping 对 Keyboard/Touch/Gamepad 返回 MappingNotEditable；这里不是 SetMapping |
| 硬件布局/目录/映射/metadata 有错 | Rejected，完整失败结果 | 不发布部分轴、不回退默认、不修改原有效映射；有状态 owner 未来按 Gate A 分别处理编辑拒绝与当前目录失效 |

本子集不声明生产 `MappingStatus.UserValidated` 枚举或签发“用户已确认”布尔量。JoystickCandidate 禁止直接进入正式采样可用性判断；未来 owner 须同时具备当前 binding 与用户 live 核对证据。静态 hash/测试不能代签该证据。

## 3. 不可变 DTO 的精确字段

`AxisMapping` 四个 get-only string：ThrottlePath/YawPath/PitchPath/RollPath；constructor 只接这四值，不接 index、绝对设备路径或可变数组，不另公开 Paths 数组/集合。允许保留非法/null 值供验证器显式拒绝，不在 constructor trim、normalize、修正或抛业务异常。

`RawControlDescriptor`：Path、ValueKind、ControlTypeFullName、StateFormatCode（UInt32，FourCC 的原32位值）、SizeInBits（UInt32）、Preprocess。`RawControlValueKind = AxisFloat | ButtonFloat | Other`。目录包含同一设备的全部控制项；只有未来 adapter 实际判断 `AxisControl && !(ButtonControl)` 才提交 AxisFloat，不能只靠字符串包含 Axis 自证类型。ButtonFloat 明确不合格，Axis 子类允许但实际 FullName 仍入指纹。Preprocess 对选中 AxisFloat 必需；未选中 Button/Other 可为空。

`RawAxisPreprocess` get-only 字段：Clamp、ClampMin、ClampMax、ClampConstant、Normalize、NormalizeMin、NormalizeMax、NormalizeZero、Invert、Scale、ScaleFactor。所有数值字段为 Single；Clamp 使用本模块稳定枚举 `RawAxisClamp = None | BeforeNormalize | AfterNormalize | ToConstantBeforeNormalize`，相应指纹码为 `none/before_normalize/after_normalize/to_constant_before_normalize`，不以未来 Unity enum 数值 cast 猜对应。该枚举/码属于**候选 DTO 协议**；后继薄层需另核实际 1.16 enum 并明确映射，本文没有验证薄层。

`RawMappingValidationStatus = BuiltInValid | JoystickCandidate | Missing | Rejected`。`RawMappingValidationResult` sealed/get-only：Status、IsValid（只在前两状态 true，**不是可飞行授权**）、Kind、RawConvention、Mapping、Controls、MappingFingerprint、Diagnostics。硬件成功 Controls 为 T/Y/P/R 顺序长度4的不可变 descriptor 列表；built-in 成功为不可变空列表。结果不能暴露输入数组或内部 List，旧结果在再次调用后不变。

`RawMappingDiagnostic` sealed/get-only：Error（RawMappingError）、AxisIndex（T/Y/P/R=0/1/2/3；全局=-1）、Path（仅合法 canonical path；非法文本为空）、Field（固定字段名/空）。Controls/Diagnostics 的公开类型分别为 `IReadOnlyList<RawControlDescriptor>` / `IReadOnlyList<RawMappingDiagnostic>`。成功 Diagnostics 为空；全部失败 Mapping=null、Controls 为空、MappingFingerprint 和 RawConvention 为空，IsValid=false，Diagnostics 至少一项，没有“失败但带可激活上次 mapping”的结果。输入 DTO 的公开 constructor 参数按上方字段顺序逐项接收；输出 result/diagnostic constructor 为 internal，不由调用方签发有效结果。

## 4. 精确路径、目录与重复规则

1. path 是从该控制的 parent/name 链到设备根前逐段拼出的 device-relative path；根设备名字不参与。本子集只校验提供的 canonical 目录，**不能证明 adapter 没有跨设备混轴或伪造 metadata**。
2. 字符串必须非 null/非空、strict UTF-8 可编码；不 trim、不 NFC/NFD 转换、不大小写折叠。所有 lookup 和重复检查使用 `StringComparer.Ordinal`。`x` 与 `X` 不等价；Unicode 合成/分解不同路径也不合并。
3. `/` 唯一分隔符；不能开头/结尾 `/`、出现 `//`、空段或整段 `.`/`..`；拒绝 `* ? { } < > [ ] :`、反斜杠（U+005C）、任何 Unicode whitespace/control 字符。无 URL 解码、转义解码、alias/usage/device-selector 解析；字面 `.` 可存在于非 `.`/`..` 段，不能自己扩展成 parent traversal。
4. 不接 `TryGetChildControl` 的宽泛匹配或首项行为。对四路径逐级等值的 canonical 字符串 exact lookup；不做 suffix/前缀删除。例如 `<Gamepad>/leftStick/y`、`/device/axis` 语法拒绝；`Gamepad/leftStick/y` 不剥设备前缀，只有与设备根前目录完全相同的合法 path 才可能命中，通常返回 AxisNotFound。目录生成排除 root 是未来 adapter 义务。
5. 四条请求路径须 ordinal 不同；重复的每个轴都给 DuplicateMappedPath，不择第一项。目录里任意重复 canonical path（即使未被选四轴引用）给 DuplicateCatalogPath，整次拒绝；不能借列表顺序决定物理轴。空目录合法但四轴均 AxisNotFound；null 目录为 MissingCatalog。
6. null 目录项、非法目录 path、未知 ValueKind 均目录级拒绝。合法、未选中的 Button/Other 不影响合法四轴。选中项必须 AxisFloat；ButtonFloat 为 ButtonAxisNotAllowed，Other 为 AxisTypeMismatch；不能拿 button/Vector2/Integer 作 float 轴。
7. catalog 输入顺序只影响复制过程，不影响解析结果、fingerprint 或诊断排序。结果不保存目录索引；旧负/越界 index 不存在于公开面，也不允许 clamp 为末轴。

layout 必须非 null/非空、strict UTF-8、无任何 control 字符及首尾 whitespace；保留输入字节的大小写与 Unicode 形式、不重命名、不猜 Gamepad/Joystick 默认 layout。ControlTypeFullName 对选中轴必须非空、strict UTF-8且无任何 whitespace/control，原样保留；不含 AssemblyQualifiedName/版本/本机 DLL 路径。StateFormatCode 必须非0，SizeInBits 必须大于0，不假设只有 FLT/32 位才是 AxisControl。

## 5. 量程与 preprocess：只检查 metadata，不模拟读取

选中四轴的上述七个 float 字段全部必须有限，**即使开关关闭也检查并加入 hash**。未知 Clamp、缺 Preprocess、空/非法 type、format=0、size=0 均拒绝。不得把 NaN/Infinity 元数据清成0或默认值。

候选 metadata 量程政策如下，须随本文一同冻结：Clamp 非 None 时 `ClampMin <= ClampMax`（相等允许，表示确定性的窄/常量区间）；Normalize 为 true 时 `NormalizeMin < NormalizeMax` 且 `NormalizeZero < NormalizeMax`。NormalizeZero 可等于或低于 NormalizeMin，**不能错误套用校准的 min<center<max 规则**。关闭的 clamp/normalize 只要求字段有限，不强加无效但未使用区间的次序。ScaleFactor 为有限负数或0允许；Invert/Scale/Normalize 为 bool，不重算或折叠等价配置。

这些检查不声称实际控制器 raw 被限制在 metadata 区间；normalize 也不能自动推导“物理量程为 [-1,1]”。本模块不计算/返回 raw 轴值，不验证采集范围/中心，不添加 `.5` 或 `.1` 校准阈值、不 clamp -10/20 之类有限样本。未来采样必须保留 `ReadUnprocessedValue()` 返回的有限值，在整源 NonFiniteRaw 时 fail-closed，AeroSim filter/calibration 则继续由后继按既存核心执行一次。

Gate A 已将硬件 raw 定为 `unity-unprocessed-control-v1`，即绕过 control processor、**不保证绕过 AxisControl 布局级 Preprocess，也不等于 USB/HID 字节**。本模块不执行 preprocess，所以既不能证明 Unity processor bypass，也不能据 DTO 判定 OS 原始范围。

## 6. MappingFingerprint 的唯一编码

算法复用现有 `InputDeviceIdentity.HashFields` 的 strict UTF-8/字节长度十进制前缀/SHA-256 小写 hex（同 namespace internal helper），不另造 encoder、不改已冻结 identity 源。对每字段编码 `UTF8 byte count + ':' + UTF8 bytes`，按下列精确顺序，无 JSON、分隔符拼接或本机文化参与。

硬件完整字段序列：

1. `mapping-v1`、layout、`unity-unprocessed-control-v1`。
2. 四个 canonical path，T/Y/P/R 顺序，**先写完四 path**。
3. 对每个选中 control 再依 T/Y/P/R 写：ControlTypeFullName、StateFormatCode、SizeInBits、Clamp code、ClampMin、ClampMax、ClampConstant、Normalize、NormalizeMin、NormalizeMax、NormalizeZero、Invert、Scale、ScaleFactor。

UInt32 用 invariant decimal，不用 FourCC.ToString() 的显示/空格格式；bool=`0/1`；finite Single 用 invariant round-trip `R`，正负零都规范为 `0`，不降精度到 F2，不把小幅量程视作0。硬件不增加 SourceId、runtime ID、serial/model/profile key、catalog/mapping revision、输入次序、显示名、processor stack 或校准数值；kind 由 identity/binding 判断，不是本次硬件字段序列的一部分。返回值为64字符 lowercase hex，不加自造前缀。

Keyboard/Touch 分别严格复用 Gate A 四字段：

```text
mapping-v1, keyboard, logical-key-state-v1, keyboard-bindings-v1
mapping-v1, touch, touch-normalized-v1, dual-stick-typr-v1
```

Keyboard 语义仍为 T=`R-F + PageUp-PageDown`、Y=`E-Q`、P=`W-S + UpArrow-DownArrow`、R=`D-A + RightArrow-LeftArrow`；Touch 仍为 left.y/left.x/right.y/right.x。这里不执行按键 sum/clamp 或 Touch Snapshot。builtin API 不接受自定义路径/目录或布局，不生成硬件 AxisMapping。未来 sidecar 的 builtin axisPaths=null/mappingRevision=1 由 store/owner承担。

layout/path/type/format/size 或任一 preprocess 字段的有效规范值改变必须改变指纹（正负零统一为0是唯一明确的数值等价例外）；未选中合法目录项增加/移除/重排、processor stack、设备/身份/显示名/revision 改变不能改变同一映射指纹。指纹只说明指定 metadata 与配置相同，不认证 adapter、硬件、用户 live 验证或校准；后继看到变化必须按 Gate A 取消 draft/旧校准授权，不能只比较路径。

## 7. 全部普通失败码、顺序与原状态保护

候选 `RawMappingError` 声明顺序：None、UnsupportedBuiltInKind、UnsupportedHardwareKind、MappingNotEditable、MappingMissing、InvalidLayout、MissingCatalog、MissingControlDescriptor、InvalidCatalogPath、UnsupportedControlValueKind、DuplicateCatalogPath、InvalidMappingPath、DuplicateMappedPath、AxisNotFound、ButtonAxisNotAllowed、AxisTypeMismatch、InvalidControlType、InvalidStateFormat、InvalidStateSize、MissingPreprocess、UnsupportedClamp、NonFinitePreprocess、InvalidClampRange、InvalidNormalizeRange。None 不作为实际诊断项输出。

阶段顺序固定，不依目录排列选“第一条错误”：① kind/edit gate；② Joystick null mapping；③ layout；④ catalog 结构与 path/ValueKind/重复；⑤ 四请求 path/重复；⑥ exact lookup/type/metadata。①②③失败不继续；④/⑤各自收集该阶段所有错误后拒绝、不进入后阶段；⑥收集各轴所有错误。这样 null mapping 始终 Missing（不被同时缺目录改成另一状态）；Gamepad supplied mapping 始终 MappingNotEditable（不先解析所供路径）。

Diagnostics 按阶段、AxisIndex、合法 Path ordinal、Field ordinal、Error 枚举声明顺序稳定排序，完全相同项去重。目录全局错误 AxisIndex=-1；非法 path 不回显任意原文本。DuplicateCatalogPath 每个重复合法 path 一条；DuplicateMappedPath 对每个涉及轴一条。⑥未找到轴不继续检查该轴 type/preprocess；type 错不继续按 Axis metadata 检查它。

一次纯调用失败不会修改旧结果或任何 owner 状态。后继**用户编辑请求被拒绝**时必须保留上次有效映射；后继**当前已安装映射因目录/preprocess变更失效**时必须使其 Invalid/不可用，不能把“拒绝候选时保留旧映射”错误扩展为沿用已失效控制对象。此两种行为本子集不实现。

## 8. 与已有 identity/registry 的接口边界

不修改 InputDeviceIdentity、InputIdentityRegistry、FlightControlFrame/InputSample、InputRouter 或 TouchInputState。InputIdentityRegistry 的 Active 只代表当前合格身份观察，不等于 mapping/采样可用。新的函数不接 `InputIdentityResult` 或 `LiveRegistryApplyResult`，不需要重跑 collision/generation 算法；未来 owner 必须从**同一个仍当前的 LiveIdentityEntry**采目录、验证并绑定结果，而不是按型号找另一个设备补轴。

后继 binding 至少继续包含 SourceId、SourceGeneration、InstanceToken、InvalidationRevision、UsageRevision、IdentityEpoch、CatalogRevision、MappingRevision、MappingFingerprint；本次 result 不伪造这些 revision。旧 Gate A draft 元组与 registry 后加 IdentityEpoch 的消费由后继 owner/store另冻，不在本文写校准API。

RevokedSourceIds 必须撤销旧 mapping/control/cache；InvalidatedBindingSourceIds 必须取消旧校准 draft，即使 mapping 指纹没变；MappingRevalidationRequiredSourceIds 只对同 SourceId usage 增长重新核四轴，不能自动颁发 UserValidated。新代际初始无用户映射授权，即使同 serial/同指纹；复制候选须 live 核对。Ambiguous 可保留独立合法候选/后继未校准 raw 路径，但禁止自动持久强校准，本模块不能解除 ambiguous。

没有按 ModelKey/serial/display label 自动复用映射或 ProfileKey 的分支。Keyboard/Touch builtin 与 Gamepad builtin 不装 JoystickCalibration。强身份校准重绑、弱身份 recapture、mapping revision overflow、缓存同帧失效、sample Availability、Fixed/Auto以及平台支持都留后继 Gate A/B/C，不以纯函数成功关闭这些门禁。

## 9. 最小独立测试表（全部 NOT_RUN）

独立 QA 应在读生产实现前固定字面输入、结果和至少一套 hash 编码预期，不从实现输出倒写 hash；根 source freeze 后才读取生产源并运行。作者与 QA 文件互不覆盖，既有身份/registry/router/touch 套件和旧失败保持原字节。

| 组 | 必需断言 |
| --- | --- |
| API/immutable | 每种 kind 状态矩阵；所有 null/未知 enum 返回指定失败；输入集合后续修改不改旧结果，sealed/get-only与只读集合不能覆盖对象；重新调用无 revision/状态变化 |
| 精确 path | 正常嵌套四轴；大小写/Unicode形式不同不命中；空/绝对/device-selector/通配/usage/alias/双slash/反slash/点段/空白/非法UTF8拒绝；`Gamepad/…` 不 strip；负/越界 index 不存在 |
| duplicate/目录 | 每轴重复含三轴同 path；目录同 path 的同/异类型、甚至未选 path也拒绝；所有排列结果/诊断/hash相同；null目录/null项/空目录、未知ValueKind；不择首项 |
| type/metadata | ButtonFloat/Other拒绝；AxisFloat子类FullName入hash；所选缺type/preprocess/format/size拒绝；合法未选Button/Other不污染指纹 |
| 量程 | 有限 -10/20、小数/非对称metadata保留；七float各NaN/+Inf/-Inf含关闭字段拒绝；Clamp等界允许/倒序拒绝；Normalize zero=min/低于min允许、zero>=max或min>=max拒绝；关闭区间倒序有限允许；scale负/0允许 |
| 字面hash | 硬件四path先行及每control完整字段顺序；UTF8字节数而非字符数；非ASCII长度；fr-FR/tr-TR等culture不变；有限Single `R`及正负零；两个builtin预期hash分别锁定 |
| hash差异/不变 | layout/path/type/format/size/每个preprocess字段单改必变；目录重排/未选项增删不变；同metadata可在不同source/identity使用同hash但不取得binding授权；processor不在DTO/hash |
| Gate A兼容边界 | JoystickCandidate永不声称UserValidated/Available；Gamepad固定四轴/缺轴拒绝/supplied mapping不可编辑；函数不引用Unity/LocalStore、不读值、不做filter/calibration或合轴；旧源SHA保持 |

至少三项隔离 mutation：① ordinal exact lookup改为大小写/后缀或首项匹配；②目录重复取第一项或允许Button；③指纹漏normalize/invert/scale字段（选择一个已锁字面差异向量）；建议再加④有效Joystick结果改成BuiltInValid而绕过候选状态。mutation 必须严格编译成功，然后被未改独立套件 exit1 检出；编译失败不算检出。纯runner继续既有 Roslyn/Mono `warnaserror`、唯一 `/private/tmp` 产物，不运行Unity/native或访问真实存档；实际计数/退出码只能执行后记录。

## 10. 只读指纹、外部核对限制与交接

本次 worktree 没有 Gate A 所列 Library package-cache 文件。只读访问官方1.16 AxisControl API及官方仓库tag raw URL均未取得内容；搜索仅返回官方1.4.3旧API，不拿它替代1.16确认。上述 Clamp DTO/量程政策因此明确为待冻结的纯协议选择，不冒称已读取当前 SDK enum/薄层支持；后继 Unity adapter 别直接 cast，须按实际1.16源码/API另验。历史 Gate A 的本机SDK阅读证据保持原文，不在本文补造新包验证。

| 本次只读文件 | SHA-256 |
| --- | --- |
| AGENTS.md | `52a30e1d1ce42d5c11ac7ab1efadbd081e846e62ebd399ee9c030fa2cc0d999d` |
| docs/V125_GATE_A_CONTRACT.md | `f2237ca63358afcd2ee7c76eb828609e61b8094a15ecdd43476d5f7012257697` |
| docs/V125_PROGRESS.md | `9cbab36d4480f718ac16f5ac8b841f7ec9b1909e627d96a516c494c6d67e2b34` |
| Runtime/Input/FlightControlFrame.cs | `aedd8c7593bf61f61cfbd7c2fcc082570828f0f8d486ec39b2d8a0c232fd773d` |
| Runtime/Input/InputDeviceIdentity.cs | `88a215d7431824c3c3062d377c4d2e35a7bdd3af81348bf1add4aca2adca084f` |
| Runtime/Input/InputIdentityRegistry.cs | `2536d814d4430b436ad322b602943b8a88de7ff294203034c91cbc960a730ba2` |
| Runtime/Input/FlightInput.cs | `c7a2e9da426b0a527511a645442ed4161f9754ff19d039509bd7c51539fa2a99` |

Runtime/...路径位于 `AeroSim/Assets/AeroSim/`。本文不自引用SHA，根另存真实冻结字节。唯一新增文件为本文；既有源/PRD/progress/图稿/批准状态、候选08二进制及原目录不变。本成员不commit/push，统一交付由根负责人负责；[GitHub仓库](https://github.com/yijunchai2026-gif/AeroSim)是既存入口，不是本提案已上传证明。
