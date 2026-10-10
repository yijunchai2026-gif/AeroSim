# AeroSim 1.25 Gate A：原始采样、身份与旁路配置契约候选

2026-10-10，第四波技术负责人交接。状态为 **CONTRACT_CANDIDATE / 待 root 冻结**，不是实现、测试通过或 Gate A 关闭。本文只把[接线计划](V125_INPUT_INTEGRATION_PLAN.md)的 CORE/DATA 拟接口收窄为下一波可实施的最小契约；本波只新增本文，没有改源码、存档、其他文档或 Git，没有启动 Unity/native、安装依赖或登录。root 冻结时另保存本文字节 SHA；不在本文内自引用散列。

已完整阅读 AGENTS、[恢复断点](V125_PROGRESS.md)、[接线计划](V125_INPUT_INTEGRATION_PLAN.md)、[输入核心](V125_INPUT_CORE.md)、[触点契约](V125_TOUCH_CONTRACT.md)、[触点独立 QA](V125_TOUCH_QA.md)，及 FlightInput、InputCapabilityService、FlightControlFrame、InputRouter、TouchInputState、Domain/TrainingData 的完整源码；另只读核对旧 ControllerPage 捕获/保存调用与真实安装包 API。当前 `FlightInput.Raw/Read` 仍合并来源、套全局校准，Gate A 不接旧应用；07c/0.4、23 张原图与历史失败保留。

## 1. 实际 API 依据，不使用 `.current` 代替设备列表

本地 `AeroSim/Packages/manifest.json` 与安装包 `package.json` 均为 Input System **1.16.0**，包仓库 revision `39ff980990a69de94d44cf1935dc5a8a6884103a`。本地包根为 `AeroSim/Library/PackageCache/com.unity.inputsystem@552acc76aabe/`，下表路径相对该根；Library 只读，不提交它。

| 核对项 | 本地真实定义 / 结论 |
| --- | --- |
| 所有当前设备 | `InputSystem/InputSystem.cs:1060`，`InputSystem.devices`；Gamepad/Joystick `.all` 也存在，适配器只需统一遍历 devices 一次 |
| 是否还可读取 | `Devices/InputDevice.cs:301` 的 `added`、`:202` 的 `enabled`、`:310` 的 `remote`；移除对象仍可存在，不能把非 null 当可用 |
| 运行时 ID | `Devices/InputDevice.cs:355` 的 `deviceId` 来自 runtime；layout 重建可能保留 ID，不能拿它当跨运行硬件身份 |
| 可用描述字段 | `Devices/InputDeviceDescription.cs` 的 `interfaceName/deviceClass/manufacturer/product/serial/version/capabilities`；serial 可缺省，capabilities 是接口专用 JSON |
| 重连不证明同机 | `InputSystem.cs:1063–1091` 明示描述匹配可能把另一只同型号设备误判 Reconnected；本契约不接受该事件作为校准认证 |
| 未经过 processor 的轴 | `Controls/InputControl.cs:1558` 的 `InputControl<float>.ReadUnprocessedValue()`；`AxisControl.cs:240` 的读取仍可能执行布局级 `Preprocess`（normalize/clamp/scale/invert） |
| 路径解析陷阱 | `Controls/InputControl.cs:680` 的 `TryGetChildControl<T>` 支持路径匹配且多匹配只取第一个，类型不符可能抛异常；不把带通配符的配置直接传它 |
| HID 型号辅助项 | `Plugins/HID/HID.cs:938` 的 `HIDDeviceDescriptor` 有 `vendorId/productId/usagePage/usage`；只抽取这四项，不散列整个 capabilities JSON |
| 变更种类 | `Devices/InputDeviceChange.cs` 实际包含 Added/Removed/Disconnected/Reconnected/Enabled/Disabled/UsageChanged/ConfigurationChanged/SoftReset/HardReset；旧 capability service 只监听前四类，尚不能承担本契约 |

硬件轴“raw”固定表示 **`unity-unprocessed-control-v1`**：不加 AeroSim deadzone/expo/reverse/sensitivity/calibration，不调用 `ReadValue()` 再重复过滤，也不表示原始 USB/HID 整数字节。保留读取所得有限值的真实量程，不在适配器 clamp Joystick raw 到 `[-1,1]`；布局级归一化已经发生时不能声称拿到了未归一化硬件量程。Keyboard 为 `logical-key-state-v1`，Touch 为 `touch-normalized-v1`，不把它们描述为硬件轴字节。官方 [1.16 Devices](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.16/manual/Devices.html) 补充核对设备描述和移除读取限制；实现以本地上述源码为准。官方 API 两页本次 web 读取返回 Internal Error，未据它们作验证结论。

## 2. 唯一实现者与最小公开面

下一波 `INPUT-GATE-A-01` **由一个共享输入/配置实现者顺序拥有 CORE + DATA**，三端不各写一份。建议只新增 `Runtime/Input/InputDeviceIdentity.cs`、`Runtime/Input/RawInputSourceRegistry.cs`、`Runtime/Input/UnityFlightInputSources.cs`、`Runtime/Domain/InputProfileV125.cs` 及各自 32hex 唯一 meta、自测/runner/交接；具体文件清单由 root 冻结。独立 QA 另写新测试，不改实现者自测或既有 3,050/514,425 套件。旧 FlightInput/Capability/页面/Simulator/生命周期由 root 在 Gate C 接线，本实现者不碰。

纯类型使用 System，不依赖 Unity；Unity 薄层仅采集真实设备/控制器字段并传入纯 registry。为纯测试注入 `IRawSourceBackend`（设备描述快照、精确轴目录、读取值和变更通知）及 `IInputProfileFileIO`（读/写临时文件/原子提交）；不复制业务算法到 fake。冻结公开面如下，返回 DTO 均不可变或深拷贝，不能让调用方修改 store 的 committed 状态：

```csharp
// AeroSim.Input; 拟新增，尚未实现。
InputIdentityResult InputDeviceIdentity.Resolve(InputDeviceDescriptor descriptor, RuntimeSourceHandle handle);
IReadOnlyList<InputSourceDescriptor> UnityFlightInputSources.GetSources();
IReadOnlyList<InputSample> UnityFlightInputSources.CollectSamples(int frameIndex);
RawSourceRead UnityFlightInputSources.ReadRawSource(string sourceId, int frameIndex);
MappingResult UnityFlightInputSources.SetMapping(string sourceId, AxisMapping mapping);
void UnityFlightInputSources.Invalidate();
// AeroSim.Domain; 拟新增，尚未实现。
InputProfileLoadResult InputProfileStore.Load();
LegacyMigrationResult InputProfileStore.MigrateLegacy(LegacyCalibrationSnapshot legacy);
InputProfileSaveResult InputProfileStore.Save(InputProfileDocument candidate, long expectedRevision);
```

`InputSourceDescriptor` 至少包含 SourceId/Kind/IdentityResult/DisplayLabel/SourceGeneration/CatalogRevision/MappingRevision/MappingFingerprint/MappingStatus/AvailabilityReason。`InputIdentityResult` 至少含 Confidence/ModelKey/SerialKey/DeviceIdentity/IdentityDescriptor；Resolve 是无副作用纯函数，weak ProfileKey 只在用户创建该配置 draft 时生成一次，不在每帧 Resolve 生成新 GUID。`RawSourceRead` 包含同一 descriptor 与 `InputSample Sample`，`SourceAvailable` 必须等于 `Sample.Available`，不另维护第二套布尔状态。`MappingResult` 为 Accepted/Rejected + 错误轴和原因；拒绝时保留原有效映射，不偷偷改索引。`InputProfileLoadResult`、`InputProfileSaveResult` 必须有明确 status、commitState、revision 与错误 stage，不以异常吞掉后返回 success。

`CollectSamples` 只取本次 registry 中当前来源，每个来源最多一个 sample；返回复用只读容器，有效期到下一次 Collect/Invalidate，消费者若需保留必须拷贝。同帧且 CatalogRevision/MappingRevision 未变时复用同一采样；Invalidate、设备事件或 SetMapping 成功立即使同帧缓存无效。旧帧未提交的来源不保留旧轴。此处不设置 Flight/Menu/Replay、不 Resume、不读正式 router 的零输出判中立；完整 rearm 和 one-shot 命令仍属于后继 Gate B/C。

## 3. SourceId 与强/弱身份的精确分工

### 3.1 会话来源，不持久复用

- 每次输入服务启动生成一个 32 位小写 hex `runId`（Guid `N`）；硬件 SourceId 为 `kind:runId:deviceId:sourceGeneration`，kind 使用 `keyboard/gamepad/joystick`，两个正整数用 invariant `D10`。同一次连接且同一对象/配置代际不变时 ID 不变；不含显示名。
- 统一遍历 `InputSystem.devices`，按 Gamepad、Joystick、Keyboard 的互斥类型分支归类；同设备对象只生成一个来源。其他设备不假扮 Joystick。remote 默认不作为本地飞行来源；合成设备只通过明确测试 backend 进入，测试结果标 synthetic。
- sourceGeneration 从 1 起；Removed/Disconnected/Disabled、对象被替换、ConfigurationChanged/SoftReset/HardReset 均撤销现有 binding、取消校准 draft 并递增下一激活代际。Added/Reconnected/Enabled 只建立尚未激活的新代际；同一撤销/激活阶段的重复通知幂等，不能为 Added+Reconnected 造两份样本。UsageChanged 使目录/缓存失效并重新验证映射，不仅更新标签。
- 旧 Fixed SourceId 断线后仍固定旧 ID，输出 source-free neutral，不按型号/serial 自动改成新 ID；新连接是否接管由 Gate C 用户选择处理。强校准可重新绑定合格新源，但不能隐式改变 Fixed 操纵权。
- Touch 原样使用已有 `TouchInputState.SourceId`（默认 `touch:dual`）；两杆合一个源，不由 Touchscreen 个数生成第二个 Touch。屏幕杆可在电脑由鼠标/UI pointer 驱动，因此不能要求 `Touchscreen.current != null` 才提供屏幕杆；实际 UI capture 不属于 Gate A。

### 3.2 持久身份与规范化

`IdentityConfidence = reported_serial | model_only | session_only | ambiguous`。这些是输入绑定置信等级，不是用户外观批准或密码学设备认证。

descriptor 固定字段顺序为 `identity-v1, kind, interfaceName, manufacturer, product, layout, hidVendorId, hidProductId, hidUsagePage, hidUsage`；每个字符串 null→空、Unicode NFC、Trim，保留大小写并 ordinal 比较；数值 absent→空，否则 invariant decimal。对每字段用 `UTF8字节数十进制 + ':' + UTF8字节` 拼接，再 SHA-256 小写 hex：得到 `modelKey=model:<hash>`。**不包含** displayName、运行时 deviceId/连接顺序、driver version 或原 capabilities JSON。HID JSON malformed 时四项全部 absent 并附诊断；没有手写猜测字段顺序的 parser。

`hasModelEvidence` 精确定义为 interface/layout 非空，且 product 非空或 VID/PID 两者非零。非空 serial 也按 NFC/Trim、保留大小写，作为最后一个长度前缀字段追加，得 `serial:<hash>`；serial为空时SerialKey为空。有 serial 且 hasModelEvidence 时 confidence=reported_serial；无强身份但 hasModelEvidence 为 model_only，否则 session_only。持久 descriptor 只保存 serial 的 SHA-256，不在报告/日志暴露原 serial。两个 live 来源产生相同非空 serialKey 时两者标 ambiguous：禁止自动绑定任何保存校准；可在映射有效时独立读取 raw/使用未校准 filter，但不能为它们分别伪造“强身份”。冲突消失后也要重新走校准绑定检查，不延用正在捕获的 draft。

| 情况 | ProfileKey / InputSample.DeviceIdentity / 允许的校准 |
| --- | --- |
| reported_serial，唯一 live 匹配 | `ProfileKey=serial:<hash>`；DeviceIdentity 使用同 key | 仅当 kind=Joystick、映射指纹/四轴有效且记录合格时可自动装到该新 SourceId；其他 kind 永不应用 JoystickCalibration |
| 无 serial、有型号资料 | ProfileKey 使用新 `weak:<guidN>`，不是 modelKey；DeviceIdentity=`session:<SourceId>` | 用户选择明确实例并本次重新捕获后仅当前代际有效；重启、掉线、disable/reset/config 变更均需重新捕获，不仅点“是同一台”就复用旧中心/量程 |
| 描述不足 | 新 `weak:<guidN>`，confidence=session_only；DeviceIdentity=`session:<SourceId>` | 同上，记录不具备跨连接自动身份能力 |
| serial 冲突或重复 ProfileKey | confidence=ambiguous | 不安装持久校准；显示冲突，禁止保存为强身份覆盖另一记录 |

无 serial 的两只同型号设备可以有相同 modelKey，但各有 SourceId、weak ProfileKey 与当前 session identity；**不得按 modelKey 批量套用校准**。weak 记录可持久保存供用户查看/映射候选，但 Load 后状态为 `needs_recapture`，session identity 不保存为“可恢复授权”。模型映射候选也必须由当前明确实例进行 live 逐轴核对，不能因型号相同直接变成 UserValidated。

## 4. 四轴 mapping、raw 与 SourceAvailable

四轴固定顺序 **Throttle/Yaw/Pitch/Roll**。硬件 `AxisMapping` 保存四条 device-relative canonical path（不是 index、不是含设备前缀的 absolute path）。路径由 controls 的 parent/name 链生成，逐级 ordinal 精确匹配；禁止空、`*`、`?`、`{}`、`<>`、开头 `/`、`.`/`..` 段和嵌入设备前缀。四条必须不同且每条精确匹配同 device 的一个 `AxisControl`、非 `ButtonControl`；目录冲突/类型错误/缺轴整个 mapping Invalid。不要直接使用 TryGetChildControl 的首个宽泛匹配；不要把越界 index clamp 成末轴。Keyboard/Touch 是下述不可编辑 built-in 逻辑映射，不走 AxisControl 路径验证；对它们调用 SetMapping 返回 Rejected/MappingNotEditable。

- Gamepad 固定 built-in 路径：`leftStick/y,leftStick/x,rightStick/y,rightStick/x`，分别读 axis 的 ReadUnprocessedValue；不先读经过 stickDeadzone 的 Vector2。此最小 Gate A 不提供 Gamepad remap，SetMapping 返回 Rejected/MappingNotEditable；若缺任一固定轴则 MappingInvalid，不退到猜测axis。
- Keyboard 在电脑产品线使用现有控制键：T=`R-F + PageUp-PageDown`；Y=`E-Q`；P=`W-S + UpArrow-DownArrow`；R=`D-A + RightArrow-LeftArrow`。**仅同一键盘内部**各轴 sum 后 clamp[-1,1]；不与其他来源相加。读取按键状态，不包含 H/C/P/Backspace/Escape/Start 一键命令。
- Touch 原样读取 Snapshot；`left.y,left.x,right.y,right.x`，mapping 为 built-in。owner idle 或 TouchInputState suspended 时 SourceAvailable=false、四轴零。
- Joystick 初始 MappingStatus=Missing；无默认 index 推测。用户明确映射 live 验证后才为 UserValidated。旧 index 转 path 仅是候选，不是可用 mapping。

`MappingStatus = BuiltInValid | UserValidated | Missing | Invalid`。硬件 MappingFingerprint 是固定顺序的 layout、rawConvention、四个 path、每 control 的类型名/stateBlock.format/sizeInBits 和 AxisControl 的 clamp/clampMin/clampMax/clampConstant/normalize/normalizeMin/normalizeMax/normalizeZero/invert/scale/scaleFactor 的同一长度前缀 SHA；bool 用 `0/1`，float 用 invariant round-trip `R`。Keyboard/Touch 的指纹分别仅对 `mapping-v1,keyboard,logical-key-state-v1,keyboard-bindings-v1` 与 `mapping-v1,touch,touch-normalized-v1,dual-stick-typr-v1` 四字段使用同一编码，axisPaths=null、mappingRevision=1。processor stack 不加入（raw 不执行它）；layout/preprocess/path改变则指纹改变、旧校准和 draft 失效，mappingRevision 递增。Gate A 保存 calibration 只接受当前完整指纹，不能让同路径的新布局继续套旧量程。

硬件 SourceAvailable 的必要条件为：本代际设备仍在设备快照、added、enabled、非 remote、平台允许该 kind、mapping 为 BuiltInValid/UserValidated、读取四轴成功且四值有限。任何条件失效整个 source sample 四轴零且 `Available=false`，附下列明确原因；不继续最后一次采样，不静默换设备或映射。此适配层的 NonFiniteRaw 是整源 fail closed；原纯 router 的逐轴 NaN 清理契约保持不变。

| AvailabilityReason | 结果 |
| --- | --- |
| Ready | Available=true；即使轴全部零，也是真正可用的中立源 |
| NotFound / Removed / Disabled / RemoteExcluded / PlatformUnsupported | Available=false；不能用 non-null `.current` 覆盖 |
| MappingMissing / MappingInvalid | Available=false；Fixed 返回 neutral，Auto 可选择其他合法源但不把其标为坏源接管成功 |
| ReadFailed / NonFiniteRaw | Available=false，raw 读取错误显式报告；不得捕获中心/范围或用零填出的假中心保存 |
| TouchIdle / TouchSuspended | Available=false，与硬件 connected-neutral 区分 |

`ReadRawSource` 对不存在 ID 返回 NotFound 的零 sample；invalid mapping/source 不抛常规状态异常。成功的 raw 数值未经过 AeroSim filter/calibration，另键盘/手柄/触控不会改变其极值。`CollectSamples` 和 `ReadRawSource` 使用同一次采样缓存，不能 calibration 页面每读一次产生新来源。Auto/Fixed 单源、优先顺序与过滤公式继续调用原 InputRouter；Gate A 不改该核心策略。

## 5. Sidecar 数据与 schema2 迁移

唯一正式路径为 `Path.Combine(LocalStore.DirectoryPath, "input-profiles-v125.json")`，继续尊重已有 `--data-dir`；不另外猜 persistentDataPath，不修改 `profile.json` 的 schemaVersion=2、不调用 LocalStore.Save、不改成绩/session/notes。此 sidecar 无 repo 用户数据提交。Gate A 的 store 不引用 LocalStore 静态状态，目录与只读 legacy snapshot 由未来 root 接线传入。

`InputProfileDocument` 使用 strict JSON：`schemaVersion=1`、`revision`（首次 1，以后 +1）、`profiles[]`、`legacyMigrations[]`。所有 enum 用上文稳定字符串；未知 schema/enum、重复 ProfileKey、非法必填值整个文档 InvalidData/UnsupportedSchema，不部分安装“看起来正常”的记录，不把未来 schema 改标1。

每 profile 必需字段：`profileKey, kind, identityConfidence, modelKey, serialKey(可空), identityDescriptor, bindingPolicy(serial_auto/session_recapture/builtin), mappingRevision, rawConvention, mappingFingerprint, axisPaths(硬件长度4，Keyboard/Touch为null), filter{deadzone,expo,sensitivity,reverse[4]}, calibrationState(none/valid/needs_recapture), calibration{minimum[4],center[4],maximum[4]}(可null), savedUtc`。Keyboard/Touch 若存 filter，ProfileKey 分别固定 `builtin:keyboard-v1` / `builtin:touch-v1`、bindingPolicy=builtin、identityConfidence=session_only、modelKey/serialKey/identityDescriptor均为空，不伪造硬件serial；Gamepad/Joystick 使用第3节身份记录。profile 内不持久保存 runtime SourceId、session identity 或 Fixed 当前源。`savedUtc` 为 UTC ISO8601 `O`；null calibration 的 none 状态可以只有 mapping/filter。Joystick valid calibration 必须四轴有限且各 `min<center<max`，无旧 `.5` 幅度阈值；小幅有限量程和非对称量程允许，验证/归一化用 double。non-Joystick 禁止 valid calibration。

新 Save 严格拒绝非法 filter：deadzone `[0,.95]`、expo `[0,1]`、sensitivity `[0,2]` 且有限；reverse 长度4。**只有 legacy 初始化**使用既有 InputFilterSettings 的清理规则，并记录具体修正原因；不让新 UI 错值静默变成“已保存”。weak 已保存校准数值仅用于审核记录，Load 一律 needs_recapture，不能装进 router。

`MigrateLegacy` 是幂等的纯函数，返回候选与 issue 列表，不写磁盘、不得改变传入数组/原Profile；以旧 profile 原始字节 SHA256 为 migrationId。legacyMigrations 保留该 SHA、device/savedUtc、四旧 index、reverse、deadzone/expo/sensitivity、center/minimum/maximum 原始 snapshot 与 issue。为保留 null/错长度/NaN/Infinity 但仍写 strict JSON，snapshot 中数字保存为 invariant `R` 文本（包括特殊值名称），新正式校准数字只准有限值。只保存相关校准/偏好快照，不复制完整含个人资料的旧Profile。

| legacy 分支 | 确定行为 |
| --- | --- |
| savedUtc 为空 | 只生成未绑定 legacy 偏好候选；不安装 center/range，即便数组看起来合法 |
| savedUtc 非空、只有 display label | 标 `needs_binding_and_recapture`；保留完整相关 snapshot，不能因名称等于 PrimaryDeviceLabel 自动套用 |
| null/非4数组、非有限、次序非法 | 列出每轴 issue，全部 legacy 校准拒绝；filter 偏好单独清理，不把其他存档恢复成新Profile |
| 有合法旧 index | 对用户锁定当前实例的 exact axis 列表转换 path，状态 candidate；用户 live 验证后才 UserValidated，重新捕获后才可保存 valid |
| index 负数/越界、path缺失 | MappingInvalid；不 clamp、不保存末轴、不从另 device 补轴 |
| 同 migrationId 已记录 | 返回 AlreadyRecorded、不新增重复记录、不覆盖用户新校准；迁移不自动提交 |
| 旧 profile 读取失败 / 新 sidecar 读取失败 | 返回 FailedIO，保留旧文件与内存draft；不假称“无旧配置”后覆盖保存 |

校准 draft 始终绑定 `(SourceId, DeviceIdentity, SourceGeneration, MappingRevision, MappingFingerprint, CatalogRevision)`；只有无关设备变化可在重新验证本源后更新 CatalogRevision，不取消另源有效草稿；当前源掉线/重设/换映射/身份冲突即取消。CaptureCenter/Range/Save 每一步再次读 RawSourceRead；不可用立即阻止保存，不能将 fail-closed 零样本当真实中心。四轴 live 验证、中心/范围采集的 UI 属 Gate C，但 Gate A 纯验证器必须能测试此绑定规则。

## 6. failedIO 的事务边界

store 只有一个实现/写入者，Save 串行执行，不宣称跨进程锁或掉电级 durability。结果 `status=Saved|ValidationRejected|RevisionConflict|FailedIO`；`commitState=NotCommitted|Committed|Unknown`，还含 `stage` 与可显示错误。draft 与 CommittedDocument 永远分离；调用者不能先修改 committed 对象，再让写盘失败假装回滚。

1. Load 的 Missing 只建立 revision0 空内存状态；Loaded 才安装验证完整文档。InvalidData/UnsupportedSchema/FailedIO 保留原文件，阻止默认覆盖；恢复/清空必须后继有明确用户动作，不自动拿空文档写回。
2. Save 先 deep-copy、验证、检查 expectedRevision 和当前磁盘 revision/hash（并与 Load 的基线一致）；冲突为 RevisionConflict/NotCommitted，不产生覆盖。未知提交状态时禁止再次 Save，须重新 Load 对账。
3. 序列化候选到同目录唯一 `input-profiles-v125.json.tmp.<guidN>`，UTF8无BOM；写入/flush/关闭，再读取临时文件验证 schema/revision及候选字节 SHA。此前任何 IOException/UnauthorizedAccessException 为 FailedIO/NotCommitted：旧正式文件、旧 committed 内存、原Profile/session字节不变，draft可重试。
4. 正式文件存在时使用同文件系统 `File.Replace(temp, destination, uniqueBackup)`；不存在时 `File.Move(temp,destination)`。**原子替换/移动是唯一 commit point**；不使用先删正式文件、再 Move 的非原子 fallback。平台不支持 Replace 则 FailedIO，不伪报保存。不因清理临时/backup失败把已提交配置回退成旧内存；backup不自动删除。
5. 提交操作成功后才安装候选 committed 内存、revision、缓存失效，返回 Saved/Committed。操作抛异常时做一次有界磁盘对账：目的字节精确等于完整候选→Saved/Committed（附提交异常诊断）；精确等于旧基线或首次仍不存在→FailedIO/NotCommitted；无法读取/其他字节→FailedIO/Unknown，保留旧内存和 draft，但禁止激活本次候选与继续保存，提示“保存结果未确定，需重新读取”。**Unknown 不能承诺磁盘未变**，也不声称校准已保存。
6. 不在失败路径调用 LocalStore.Save、SaveSession、覆盖 profile/session/成绩或把旧文件删掉。清理只允许本次精确 temp 路径；失败可留残件供 QA，不扫目录。Load 只读正式文件，不擅自把任意临时文件晋升正式。

I/O 注入必须覆盖 read/write/flush/temp readback/Replace/Move 的前后故障，包括“提交已完成但抛错”和“对账也读失败”；只有提交前故障才可断言字节未变。保存结果只关闭数据事务门禁，不代表飞行输入已经接线。

## 7. 三产品线支持与本机依赖边界

官方 [Input System 1.16 支持矩阵](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.16/manual/SupportedDevices.html) 将通用 Joystick 标为 Mac Yes、iOS No；iOS 支持表列 MFi 与特定 Gamepad，不能把 Gamepad 支持推广为任意 USB/HID 摇杆。官方 [HID](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.16/manual/HID.html) 描述有限的 generic HID 支持。下表“可实施”是路径存在/官方能力，不是三端应用已实现。

| 产品线 | Gate A 输入路径 / 尚缺能力 | 必须另留的独立验收 |
| --- | --- | --- |
| 电脑（当前 macOS） | keyboard、屏幕双杆、Gamepad、Joystick/HID；只采4轴、配置同一套；旧 FlightInput 尚未接入 | 真窗口与UI pointer、实体键盘/手柄/USB摇杆、两同型号、热拔插、Cmd-Tab、中立恢复与性能；synthetic不代签physical |
| iPad | 屏幕双杆 + OS可识别Gamepad 共用采样/配置；generic Joystick 在标准1.16 iOS不是已支持，需后继受支持的适配/SDK或明确兼容Gamepad模式，当前不得“通用摇杆已支持” | 真机双指/第三指、转屏、safeArea/分屏、后台、外接设备和OS型号/连接协议；1024×768/768×1024逻辑视口只验布局 |
| iPhone | 同上，屏幕双杆和受支持Gamepad；不能通过电脑版 joystick sample 推断iOS能力 | 真机屏边/Home区、多指/中断/旋转/返回保持输入/手柄/性能；844×390/390×844不代签真机 |

2026-10-10 本次只读环境复核修正旧“iOS模块缺失”推断：实际模块在 `/Applications/Unity/Hub/Editor/6000.3.25f1/PlaybackEngines/iOSSupport`（**Unity.app 同级**），不是 Unity.app/Contents/PlaybackEngines/iOSSupport。已实际列到 iOS Extensions/Common/Xcode DLL、arm64/x64 Native dylib、Trampoline/Classes、Libraries 的 libiPhone-lib.a（229,589,000字节）/dev（389,920,688字节）/baselib、sim框架与构建程序。因此可证明 **IOS_MODULE_FILES_PRESENT**，不能继续把错路径检查当模块整体缺失，也不能据文件存在证明模块完整性/当前 Editor 可用性。没有调用 `BuildPipeline.IsBuildTargetSupported`、没有 export/build/install。

`/Applications/Xcode.app` 不存在；对 /Applications 的文件清单没有找到 Xcode 的 xcodebuild。`xcode-select -p` 实际为 `/Library/Developer/CommandLineTools`；`xcodebuild -version` 实际失败并要求完整 Xcode。因此 **ACTIVE_XCODE_UNAVAILABLE**；未全盘搜索或声称任何其他路径绝无 Xcode。iOS target support=UNVERIFIED、当前候选导出/原生编译=NOT_RUN、iPad/iPhone真机=NOT_RUN；旧 Library/Bee 的 iOS 产物不是当前版本验证。不得安装、改 developer dir、登录签名或买硬件来扩大本波权限。

## 8. 下一波最快子集与严格自测/独立 QA

最快不依赖外观批准/GitHub认证的顺序：**纯身份/映射 registry → 旁路迁移与事务 store → 不被旧 FlightInput 调用的 Unity 薄采样层 API 编译检查**。只新增模块，不改真实用户目录，不让未确认机型进入飞行，不需要新美术稿或发外部消息。首个本地阶段可先交纯层自测+独立QA；Unity薄层即使只编译通过仍标 ADAPTER_API_COMPILED，不能标实际采样/整GateA完成。GitHub认证阻塞继续由 root 验证→本地commit→尝试push→如实记录，不能把本契约/本地实现当最终交付。

| 门禁 | 实现者自测与独立 QA 都必须有的具体断言 |
| --- | --- |
| 身份/来源 | 同serial不同runtimeID强校准只绑定匹配映射；不同serial不串；无serial两同型号与重启/重连需重新捕获；重复serial ambiguous；displayName/version/capabilities字段顺序改变不造新唯一身份；布局/路径改变撤销校准；Added+Reconnected/Removed+Disconnected重复通知不重样本；固定旧代际不自动接管 |
| raw/可用性 | pad中立+键盘活动；pad/joystick同时存在；keyboard+touch+pad不合轴；-10/20等有限量程保留；非有限整源拒读；missing/重复/通配/错type映射、负/越界index不取末轴；disabled/removed对象和读取抛错归零不可用；同帧缓存无旧来源/映射残留，ReadRawSource不执行filter |
| router兼容 | 原3,050原样复跑；四轴/filter仅一次；JoystickA校准不影响Keyboard/Touch/Pad/B；Fixed断线neutral；保留原Touch套件与已知复用同pointerId边界，不为了GateA删断言 |
| migration | savedUtc空/名字型/合法与畸形数组/null/长度3/NaN/Infinity/非法次序逐分支；小量程可用；idempotence；candidate index→path不是UserValidated；所有输入快照与旧Profile/schema2/成绩/session bytes SHA不变 |
| 数据验证 | DTO deep copy；非法schema/enum/repeated key/filter拒绝；weak Load needs_recapture；保存校准须当前source+identity+generation+mapping；另设备变化不能让草稿偷换当前设备；读取坏文件不自动写空默认 |
| IO事务 | 每个故障阶段注入；原正式文件/committed状态/draft和legacy基线逐项比对；Replace不支持不走删后Move；冲突/重复Save；成功提交后抛错识别Committed；读回也失败识别Unknown且禁再Save；成功后重启能只读完整新revision |

独立 QA 必须在 root 冻结后读取实际源/合同 SHA，再独立写预期身份规范化向量、source状态机与I/O事务参考结果，不仅复跑开发者断言。至少三项隔离 mutation：按modelKey给两同型号共用校准、非法index clamp到末轴、写盘前先更新committed/异常仍报成功；先严格编译成功，再用未改套件以exit1拒绝，不能拿编译失败当检出。所有纯跑器用既有 Roslyn/Mono严格编译（warnaserror），临时产物进 /private/tmp 唯一目录，不启动Unity/native、不写真实用户存档。

API编译可只读引用现有 `Library/ScriptAssemblies/Unity.InputSystem.dll` 与6000.3.25f1 Unity managed模块，在独立temp编译新薄层，不调用InputSystem静态runtime读取；必须记录引用DLL SHA。这只验证实际API表面，后继 root 才能冻结新Unity应用并用真实/合成设备事件重演。Gate A关闭至少需要纯层独立通过+适配器Unity合成设备读取/processor-bypass/热变更证据（可由root后继统一Editor/native验证）；iOS/physical结果仍不能由此关闭。

## 9. 本波已核对的源指纹与交接

以下为读取时实际字节 SHA-256，不是实现冻结或测试结果；root 并行改页面文件不在此范围。本文交出后本成员停止写入，由 root 决定冻结/实现，不无限扩研究。由于 GitHub认证断点仍在，本成员没有提交/推送，也没有新增可用试用链接。仓库：[AeroSim](https://github.com/yijunchai2026-gif/AeroSim)，工作分支：[codex/v125-product-lines](https://github.com/yijunchai2026-gif/AeroSim/tree/codex/v125-product-lines)；具体本次commit/真实远端SHA由root统一交付。

| 文件（Runtime/...统一位于AeroSim/Assets/AeroSim，其余仓库根相对） | SHA-256 |
| --- | --- |
| AGENTS.md | `527b4f5b2a386cedfc82ff5d82da3de09c7e4039dab03f1e5a3cb6adec6c44e2` |
| docs/V125_PROGRESS.md | `0018d0eee843aebf32e69425539be08c4aa1f33a64877959ad7135a81ce654f6` |
| docs/V125_INPUT_INTEGRATION_PLAN.md | `094c7debbd7528c22d2339fc1786ff0c205ba4755051276d57119499f5b80754` |
| docs/V125_INPUT_CORE.md | `de3c5ea607902fa299bdef67f620743a7004ea40e4814cb9c03ff5a6b157c246` |
| docs/V125_TOUCH_CONTRACT.md | `119e8e576033ecbd1bc1c6de7ceb4dde50a3518745479fa524b07fbe9e72f952` |
| docs/V125_TOUCH_QA.md | `3e18e9257ba81294ebdcf522da45f02fb06cfa73b87b1c8bdd0c7a7227cd2409` |
| Runtime/Input/FlightInput.cs | `113676769109ce041f09248b8b254dbe6302e7af700f82e68066966a988c33d9` |
| Runtime/Input/InputCapabilityService.cs | `e950095f3c73748062871930bca94ebc07deff302c58a4704018f5b0393eca94` |
| Runtime/Input/FlightControlFrame.cs | `aedd8c7593bf61f61cfbd7c2fcc082570828f0f8d486ec39b2d8a0c232fd773d` |
| Runtime/Input/InputRouter.cs | `991a36ee16244bb3d1e1e60578331c0481cc994464bcc6be105076d4cb5006ef` |
| Runtime/Input/TouchInputState.cs | `2319cdc4e7dd9aa4510565caba6b1d0be5a712fadd8e032328f0ec236b470fb6` |
| Runtime/Domain/TrainingData.cs | `c9f14a837015c859a110431a7215ab831231001a7cd43dc7707bac995054d1cd` |
| AeroSim/Packages/manifest.json | `bf0dd81a8c4a2fcccda9749ef84068ae864e7c1e8c963921fa11bf4eafbf267a` |

下表路径相对本地包根（Library忽略，不应提交）；给实施者复核真实SDK而非只凭本文记忆：

| 包文件 | SHA-256 |
| --- | --- |
| package.json | `1f5245bd0503f24189417c3ebf89d12df288d1a6258844665e72cbb6485576b0` |
| InputSystem/Devices/InputDevice.cs | `ba7998be9cbe251bdc30881f210a9b571a9d657cac6d6c78102614e7ba2559bc` |
| InputSystem/Devices/InputDeviceDescription.cs | `291065ded0e4d4aa580808b821a026f19daa42717ce6e031f43e5c25190bf1dc` |
| InputSystem/Devices/InputDeviceChange.cs | `65ab60220980cb484d4053e53e85b254aba3a19ef2101704522b3f610466eff2` |
| InputSystem/Devices/Gamepad.cs | `85b9abb41c2413cdc74e21c0e9ad9139497dc319f830a9084780da6026c9e44b` |
| InputSystem/Devices/Joystick.cs | `e48680d0a2a8e67a5d372eb332599b26dffcc96aa0093b5c0285715154ec3164` |
| InputSystem/Controls/InputControl.cs | `2d265f101991e8bfcfaf2241bb2e1fe3244f96c538b666502596351ba5c20a29` |
| InputSystem/Controls/AxisControl.cs | `23299620aceecddbc571694258e8a93a33849e73b21be66ee82b5276b83166f0` |
| InputSystem/InputSystem.cs | `340a3fc6e51bf23273356a690d8e0db45a6fb60dea902017af053093f99c1115` |
| InputSystem/Plugins/HID/HID.cs | `c108f3becdd8ba9daf6beaa34f001b15f3e221bbb3da43f4c6175908c695259c` |
