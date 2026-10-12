# AeroSim 1.25：live identity registry 最小契约提案

2026-10-11，America/Los_Angeles。状态：**PROPOSAL / 待根负责人冻结；未实现、未测试、未关闭 Gate A**。读取基线 commit 为 `bf97d1ae75e7b66e3e4d1848613a97eff9b09b2b`。本成员只新增本文；没有改现有身份解析器、旧输入或应用，没有构建、设备轮询、存档写入、Git 写操作。交出后停止写入，是否采用及实施文件由根负责人明确授权。

## 1. 已有要求与候选补充分开

完整读取 [Gate A 候选契约](V125_GATE_A_CONTRACT.md)、[三端接线计划](V125_INPUT_INTEGRATION_PLAN.md)、[身份实现交接](V125_IDENTITY_IMPLEMENTATION.md)、实际 InputDeviceIdentity.cs、FlightControlFrame.cs 和身份冻结记录。以下使用现有 resolver 的实际枚举、键与不可变结果，不另造身份哈希算法。

| 依据 | 本次必须保持的内容 |
| --- | --- |
| Gate A §3.1 明确 | sourceGeneration 从 1 起；掉线、disable、对象替换、config/reset 撤销代际；重复通知幂等；Fixed 旧 ID 不自动接管；UsageChanged 使目录/缓存失效并重新验证映射 |
| Gate A §3.2 明确 | 相同非空 SerialKey 的 live 来源全部 Ambiguous；不自动绑定保存校准；冲突消失重新验证，原捕获 draft 不恢复；无 serial 的同型号实例不能按 ModelKey 复制校准 |
| Gate A §2/§5 明确 | 返回不可变/深拷贝 DTO；无副作用单条 resolver 不分配 weak ProfileKey；草稿绑定本代际与身份；不损坏存档 |
| 现有 resolver 已冻结 | `FlightInputSourceKind.Keyboard/Gamepad/Joystick`；`IdentityConfidence.ReportedSerial/ModelOnly/SessionOnly/Ambiguous`；所有 SourceId、ModelKey、SerialKey 和弱 `session:<SourceId>` 的编码原样复用 |
| **候选补充，须根冻结** | 完整快照而非有顺序的事件队列；稳定对象 token/撤销 revision/usage revision；冲突时 DeviceIdentity 使用 session key；每代际 IdentityEpoch；快照重复 ID/倒退 revision 的整批 fail-closed；溢出不回绕 |

后五项是为消除已有合同未定义的状态机边界提出的设计，不声称产品原文已经规定，也不声称 adapter 已提供这些字段。仅用 Added/Removed 名称而没有对象及撤销阶段 token，无法可靠区分跨 tick 重复通知和一次新的真实 reset；registry 不猜测事件先后。

## 2. 本波范围：identity-only，不冒充 RawInputSourceRegistry 完成

建议新增 `Runtime/Input/InputIdentityRegistry.cs`（namespace `AeroSim.Input`，仅 System）与独立测试；不在本波创建 UnityFlightInputSources 或直接改旧 FlightInput。名字明确 identity-only，避免把缺少 raw/mapping 的文件称为整个 raw registry。

不含 Unity/InputSystem 类型、设备枚举、轴读取、mapping 校验/指纹、filter/calibration、weak ProfileKey 分配、配置 sidecar、router 仲裁、Fixed source 选择、生命周期恢复、UI 或线程同步。只由一个调用方串行提交快照；不承诺并发安全。Touch 仍由原 TouchInputState 单源管理，不进入硬件 registry。Active 在本文只表示“被本地 adapter 作为当前合格身份来源提交”，**不等于 SourceAvailable/可飞行或已校准**。

## 3. 拟公开面和输入协议

```csharp
// 拟新增，不是现有可调用 API。
InputIdentityRegistry(string runId);
LiveRegistryApplyResult ApplySnapshot(IReadOnlyList<LiveIdentityObservation> observations);
LiveIdentitySnapshot Current { get; }
```

constructor 只接受现有 resolver 要求的 32 位小写 hex runId；非法构造参数抛固定、不含输入内容的 ArgumentException，不自动 trim、换大小写或生成新 runId。实际运行 adapter 未来负责每次服务启动生成一个新 runId；纯测试注入固定合成值。

`LiveIdentityObservation` sealed/get-only，字段为：

- `RuntimeDeviceId`（正 Int32）、`Kind`（上述三个硬件 kind）。runtime ID 是 registry 的槽键，不是持久设备身份。
- `InstanceToken`（正 Int64）：未来 adapter 对每个实际 device 对象分配的本运行唯一 token；稳定到该对象结束，不能给新对象重用。一个 token 不得属于两个 runtime ID；registry 保留 run 内 token 归属，不能由更换列表次序重新编号。
- `InvalidationRevision`（正 Int64，初始 1）：同一对象的撤销阶段号；掉线后重连、config/reset 等真实撤销阶段增长一次，该阶段重复通知不再增长。对象替换使用新 InstanceToken，可重新从 1 起。
- `UsageRevision`（非负 Int64，初始 0）：UsageChanged 阶段号，同一事件重复通知不再增长。
- `State`：`Active/Disabled/Disconnected/Removed/RemoteExcluded/PlatformUnsupported`。值及数值范围由新 registry 枚举验证；不传 Unity enum 值猜测对应。
- `Descriptor`：Active 必须是现有有效 InputDeviceDescriptor，且 kind 匹配；非 Active 可为 null，不把非活动描述安装为当前 identity。

一次调用表示**完整当前快照**，不是增量。未列出的旧槽按 Removed 撤销；空列表合法，表示无当前硬件来源。适配器以后需先合并本 tick 的通知为最终快照，再调用一次；同 tick 通知排列不同必须生成相同快照。活跃→掉线→重连若发生在两次采样之间，最终仍 Active，但 InvalidationRevision 必须增长，不能因最终状态相同而漏掉撤销。纯 registry 不能证明 adapter 没有丢事件。

每 runtime ID 在快照中最多一条；整个快照中 InstanceToken 不得重复。相同对象 token 的两种 revision 不得倒退；Inactive/省略不会重置 run 内撤销历史。调用者不可在 Apply 期间修改集合；registry 先复制输入列表引用，Observation/Descriptor 本身不可变，结果使用不可变或只读防御性拷贝，不暴露内部 List/Dictionary。

## 4. 代际状态机与原键精确复用

槽保留 `LastGeneration` 与对象/revision 历史；未见过的槽 LastGeneration=0。只在真正建立一个新 Active 代际时增加一次，绝不在每帧、每条 Added/Reconnected 通知或每条重复 Remove 增加。

| 旧态 → 本次快照 | 状态动作 |
| --- | --- |
| 从未 Active → Active 且有效 | generation=1；创建 RuntimeSourceHandle，调用原 InputDeviceIdentity.Resolve |
| 同对象/撤销 revision/规范化身份不变，保持 Active | 保持 SourceId 与 generation；重复调用幂等 |
| Active → Inactive 或省略 | 撤销旧 SourceId；不发布 active entry；LastGeneration 保留，重复撤销不加代际 |
| Inactive/省略 → Active | LastGeneration+1；即使 serial/型号相同也不能恢复旧 SourceId |
| Active → 新对象、撤销 revision 增长或 descriptor 的有效 identity 改变 | 先撤销旧代际，再建立 LastGeneration+1；防止 adapter 漏报 identity/config 变化仍沿用旧 handle |
| 仅 UsageRevision 增长 | SourceId/generation 不变；标记本源 MappingRevalidationRequired；CatalogRevision 变化；映射与 draft 是否失效由后继 mapping 检查决定，不伪造映射已重新有效 |

“有效 identity 改变”按原 resolver 输出 `Kind/ModelKey/SerialKey/HasModelEvidence` 的 ordinal 精确差异判断；displayName、version、capabilities JSON 顺序等不进入此判断。registry 不重新规范化、重算散列或按 ModelKey 寻找可继承的槽。

建立新代际时只调用 `InputDeviceIdentity.Resolve(descriptor, new RuntimeSourceHandle(kind, runId, runtimeDeviceId, generation))`，逐字保留 resolver 生成的 SourceId（kind 小写、数字 invariant D10）。不创建另一个拼接器。旧 SourceId 被撤销后永久不再发布；本 registry 不修改 Fixed 选择或自动将它跳到新 SourceId。

`LastGeneration == Int32.MaxValue` 后需要新代际时，该槽标 `GenerationExhausted`，不 wrap 到 0/1、不复用旧 SourceId、不隐式换 runId。其他合法槽仍可发布。恢复该耗尽槽需要调用方结束旧 registry，以新的服务 runId 新建实例。

## 5. 全快照 collision：不存在“先到者保留强身份”

先完成全部 Active 槽的单条解析，再按 **ordinal、完全相同且非空的 SerialKey** 分组。组大小 ≥2 时，全组均为 Ambiguous。不能按列表顺序给第一项保留 ReportedSerial，也不能只查刚插入的一项。键本身包含原规范化 model 字段及 kind，registry 不用原 serial 字符串另做跨 kind 合并。

已有 Gate A 原文写的是“相同非空 SerialKey”，故分组也包括有 serial 但型号证据不足、原 resolver 为 SessionOnly 的情况；不能只查原 Confidence==ReportedSerial。空 SerialKey 的同型号来源不冲突，保持 ModelOnly/SessionOnly 的 session 隔离。RemoteExcluded 等非 Active 条目不作为当前本地来源进入 collision 组，这一资格定义为候选政策，未来 adapter 必须明确照做。

推荐在原 InputIdentityResult 的内部构造器创建 registry-owned 不可变结果，不修改原单条 resolver 或 DTO：

- `Error=None, IsValid=true` 表示描述解析成功；`Confidence=Ambiguous, ConfidenceCode=ambiguous` 表示不能作为持久强绑定授权。
- `SourceId/Kind/ModelKey/SerialKey/IdentityDescriptor/HasModelEvidence` 原样保留；不清 SerialKey 让冲突不可诊断。
- **候选补充：**Ambiguous 的 `DeviceIdentity="session:" + SourceId`，而非冲突 SerialKey。这样下游若按该字段匹配，也不会共享同一个保存强身份；仍必须显式检查 Confidence，不将 session fallback 当新校准或批准。
- 冲突消失后重新发布原单条 resolver 的 confidence/key，不继承旧校准、不恢复旧捕获 draft。registry 不持有或安装任何 calibration。

## 6. 不可变输出、撤销和后继 draft 的身份屏障

`LiveIdentityEntry` 包含现有不可变 `InputIdentityResult Identity`、SourceGeneration、InstanceToken、InvalidationRevision、UsageRevision、**IdentityEpoch**。每个新 SourceId 的 IdentityEpoch=1；在该 SourceId 的有效 Confidence/DeviceIdentity 发生变化（如 collision 进入或解除）时增长一次，不因无关设备变化增长。同一代际经历冲突进入→解除，最终 key 即使与之前相同，epoch 已改变；未来 draft 必须绑定该 epoch，不能“值相同”就复活旧草稿。此字段是对旧 draft 元组的明确候选扩展，后继 store/adapter 需根批准后消费，不声称旧接口已经支持。

`LiveIdentitySnapshot` 只包含 get-only CatalogRevision 与 Sources。初始 revision=0/空列表；影响当前来源、身份 epoch 或 usage 校验要求的有效变化才增一次。相同快照、输入列表排列变化、重复阶段通知不增 revision。Sources 按 SourceId ordinal 排序，调用方不能把原输入次序作为仲裁优先级。

`LiveRegistryApplyResult` 含 Applied/AppliedWithErrors/Rejected/Faulted 状态、本次 Snapshot、固定错误码列表及三个排序去重的 delta 列表：

- `RevokedSourceIds`：失去原代际的 ID；未来采样缓存和固定旧 ID 的读取必须据此撤销，不能回放旧值。
- `InvalidatedBindingSourceIds`：撤销代际，以及 collision 进入/解除的仍活跃 ID；future draft 立即取消，不能仅靠重新点击确认恢复。
- `MappingRevalidationRequiredSourceIds`：仍活跃而 usage revision 改变的 ID；只要求 live 重新检查，不把 mapping 自动改成 UserValidated。

三个 delta 只描述该 Apply 调用，不保留作下一次调用的旧 delta；Current 返回最后完整 Snapshot，不包含事件 delta。碰撞成员均会 invalidate binding；增删第三只设备导致 A/B 仍 Ambiguous 且 epoch/identity 不变时，不再无意义重复撤销 A/B，但第三只按它自己的新代际处理。其他无关设备变化不改变本源 IdentityEpoch；后继 draft 按旧合同 revalidate 自身后可更新全局 CatalogRevision。

若 IdentityEpoch 或 CatalogRevision 即将超出 Int64.MaxValue，进入固定 Faulted 状态，发布空当前来源并撤销原 active binding；不回绕。此处属于新保守溢出策略，纯测试可反射构造边界内部状态，不必添加生产测试后门。

## 7. 无效输入的整批 fail-closed 候选政策

Active descriptor null/error、kind 错配/不支持、非正 runtime/token/revision、负 usage、未知 state、重复 runtime/token、token 归属冲突、同对象 revision 倒退、null 列表或 null 条目，均返回 Rejected + 固定错误码，不打印 descriptor、原 serial 或输入文本。现有 InputIdentityError 原样用于单条解析失败；registry 协议错误用新 LiveRegistryError，不修改原枚举。

**候选补充：**上述结构性错误整批拒绝，当前 snapshot 变空、所有旧 Active ID/草稿被撤销；保留 run 内 generation/token tombstone。不能保留旧有效样本伪装“本帧继续可用”，不能取重复 ID 的第一项或静默忽略非法项。后继有效快照重新激活需新代际。连续同样 Rejected 且当前已空不反复增加 generation 或 CatalogRevision。GenerationExhausted 是单槽处理错误，不属于结构性拒绝，可返回 AppliedWithErrors；普通状态错误不抛异常。

这一 whole-batch 政策是 fail-closed 的最小确定性选项，会使一条损坏观察暂时禁用其他本帧来源；产品合同未明确要求全批还是仅本槽。根若选择 per-slot 隔离，必须先重写冻结和独立预期，不能实现时随手改为“忽略坏设备仍保留旧数据”。

## 8. 实现者与独立 QA 应共同覆盖的定向向量

所有输入仅为合成 DTO，不能计入 physical controller、Unity device polling 或 iOS 测试；独立 QA 先冻结本提案字节，再另写状态机预期，不只复跑作者断言。

1. 初次 Active=gen1；相同列表/反序/多次重复调用 SourceId、epoch、revision 稳定；新增+重连重复阶段只有一个 entry。
2. Active→Removed→Disconnected 重复撤销只产生一次 revoked；再次 Active=gen2；Fixed 旧 ID 无当前 entry；同 tick disconnect+reconnect 最终 Active 但 revision 增长仍得到 gen2。
3. 同 runtime ID 新 InstanceToken、config/reset revision 增长、有效 descriptor/layout/serial 改变均撤销；displayName/version/忽略 JSON 变化不撤销；省略设备与完整空快照及时清来源。
4. 同 token revision 倒退、跨 ID token 复用、同列表重复 runtime/token、非法 run/descriptor/kind/Unicode/value 等按 §7；Rejected 后不能保留旧 active entry，恢复也不能复用旧代际。
5. 强 A/B 同 serial、不同 runtime ID：同时均 Ambiguous/session identity 不同；A/B/第三只的所有列表排列完全相同。删除 B 后 A 回到 ReportedSerial，但 IdentityEpoch 增长且 binding delta 必须包含 A；旧 draft 不恢复。
6. 强不同 serial 不冲突；弱同型号没有 serial 保持两个 session key；不产生 ProfileKey，不复制 filter/calibration。型号不足但同非空 SerialKey 也全组 Ambiguous；大小写不同 serial 的键按原 resolver 区分。
7. 新 runId、重连/disable/对象替换前后弱 identity 不同；不能按 ModelKey、displayName、连接顺序猜同一实例。一个弱设备撤销不改变另一弱设备 epoch。
8. UsageRevision 增长仅给相关当前 ID mapping revalidation delta，SourceId 不变；重复 usage stage 幂等；无关 C 改变不撤销 A 的身份绑定。
9. 所有 DTO/集合不可被调用方改写；输出和 delta ordinal 排序、无重复；保存前一次 Snapshot 后再次 Apply 不改变旧 Snapshot；旧 delta 不污染下次 Apply。
10. 强 identity resolver regression 原样通过；无身份键重编码；Int32/Int64 溢出 fail-closed，绝不循环重用 ID；serial/个人原数据不进日志。

隔离 mutation 至少覆盖“只标后插入者 Ambiguous”“modelKey 替代 session identity 造成弱共享”“撤销后重用 generation”三类。必须严格编译成功，再由未改独立套件 exit 1 检出；编译错误不是 mutation 检出。本提案无任何实测计数，所有上述验收均为 NOT_RUN。

## 9. 读取基线指纹与交出边界

| 只读输入 | SHA-256 |
| --- | --- |
| docs/V125_GATE_A_CONTRACT.md | `f2237ca63358afcd2ee7c76eb828609e61b8094a15ecdd43476d5f7012257697` |
| docs/V125_INPUT_INTEGRATION_PLAN.md | `094c7debbd7528c22d2339fc1786ff0c205ba4755051276d57119499f5b80754` |
| Runtime/Input/InputDeviceIdentity.cs | `88a215d7431824c3c3062d377c4d2e35a7bdd3af81348bf1add4aca2adca084f` |
| Runtime/Input/FlightControlFrame.cs | `aedd8c7593bf61f61cfbd7c2fcc082570828f0f8d486ec39b2d8a0c232fd773d` |
| verification/v125-qa/identity/identity-source-freeze.json | `ff4b9d6c3ecd29975a58bc7227d281b000d17e9747f82d3fe70977cc49415c05` |

Runtime 路径统一位于 `AeroSim/Assets/AeroSim/`。本文不自引用 SHA；根另存实际字节冻结。现有 23 张原图、机型 pending_user、候选08二进制、移动/物理设备验证边界均未改变。本成员没有提交/推送，统一整合交付由根负责：[仓库](https://github.com/yijunchai2026-gif/AeroSim)、[工作分支](https://github.com/yijunchai2026-gif/AeroSim/tree/codex/v125-product-lines)。这些现有链接不是本提案已上传的证明。
