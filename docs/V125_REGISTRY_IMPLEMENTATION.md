# AeroSim 1.25：纯身份 snapshot registry 作者实施交接

2026-10-11，America/Los_Angeles。状态：**AUTHOR_IMPLEMENTED / STRICT_PURE_TESTS_PASSED / 待根源码冻结与独立 QA**。这是 Gate A 的 identity registry 子集，不是 raw registry、Gate A 关闭、正式输入接线、三端实测或 1.25 完成。

## 1. 授权、冻结契约及唯一文件所有权

根负责人完整读取并接受 [132 行提案](V125_REGISTRY_CONTRACT_PROPOSAL.md)，保存 [registry-contract-freeze.json](../verification/v125-qa/registry-contract-freeze.json) 后授权本成员实施。已完整读取其 rootDecisions 及 [v1 usage 澄清](../verification/v125-qa/registry-contract-addendum-v1.json)、[v2 overflow 澄清](../verification/v125-qa/registry-contract-addendum-v2.json)、[v3 耗尽但仍 Active 的冲突澄清](../verification/v125-qa/registry-contract-addendum-v3.json)；原提案/原 freeze/v1/v2 未修改。

本成员仅新增五个实施文件：

- `AeroSim/Assets/AeroSim/Runtime/Input/InputIdentityRegistry.cs` 与 `.meta`。
- `verification/v125-qa/registry/InputIdentityRegistryDeveloperTests.cs`。
- `verification/v125-qa/registry/build-registry-developer-tests.sh`。
- 本文。

此前提案是另一已明确授权的文档文件；本次未改其字节。resolver/frame/router/touch/旧 FlightInput、应用/UI/图稿、其他成员文件、真实用户目录与 Git 均未修改。没有启动 Unity Editor/Player、native 应用、设备轮询、配置写盘或新增运行包；当前候选08二进制仍不包含本次子集接线。未写独立 QA 或 mutation，由根冻结后另派独立成员进行。

## 2. 实际公开面：仅 System/单写入者

namespace `AeroSim.Input`，复用既有 FlightInputSourceKind 与 InputDeviceIdentity；不添加第二个 SourceId/身份键 codec。`InputIdentityRegistry(string runId)` 用原 resolver 校验 runId，非法构造参数为固定 ArgumentException；普通 snapshot 错误返回状态/固定 enum，不把输入文本或原 serial 拼入错误。

| 类型 | 实际 get-only 字段 / API |
| --- | --- |
| LiveIdentityObservation | RuntimeDeviceId、Kind、InstanceToken、InvalidationRevision、UsageRevision、State、Descriptor |
| LiveIdentityEntry | Identity（原 InputIdentityResult）、SourceGeneration、InstanceToken、InvalidationRevision、UsageRevision、IdentityEpoch |
| LiveIdentitySnapshot | CatalogRevision、Sources（防御性只读副本） |
| LiveRegistryDiagnostic | Error（LiveRegistryError）、RuntimeDeviceId、IdentityError（原 InputIdentityError） |
| LiveRegistryApplyResult | Status、Snapshot、Errors、RevokedSourceIds、InvalidatedBindingSourceIds、MappingRevalidationRequiredSourceIds；所有集合防御性只读 |
| InputIdentityRegistry | `ApplySnapshot(IReadOnlyList<LiveIdentityObservation>)`、`Current`；无采样/Save/ProfileKey/校准 API |

State 为 Active/Disabled/Disconnected/Removed/RemoteExcluded/PlatformUnsupported；Status 为 Applied/AppliedWithErrors/Rejected/Faulted。Active 仅为 adapter 提交的当前合格本地身份观察，**不能当 SourceAvailable=true、控制轴有效或校准许可**。

协议错误码包括 MissingSnapshot/MissingObservation、非正 runtime/token/invalidation/负 usage、unsupported kind/state、duplicate runtime/token、token ownership 冲突、revision 倒退、IdentityResolutionFailed、GenerationExhausted、IdentityEpochExhausted、CatalogRevisionExhausted、RegistryFaulted。单条 resolver 拒绝原因保留在 IdentityError；错误按 runtime ID/enum 排序去重，重复 token 的全体观察均给出诊断，不依赖先出现哪项。

## 3. 生命周期、冲突与不可变提交

先复制输入列表并完整验证；调用方约定不能在 Apply 过程中修改集合。输入 DTO 不可变。所有 snapshot 使用完整设备清单，省略即撤销旧来源。实际 device-object token、真实撤销/usage 阶段 revision **仍须未来 adapter 提供**；这里不把合成数字称为已经实现 OS 事件合并。

验证失败整批 Rejected、清空当前来源并撤销所有旧 binding，保留原 generation/token/revision tombstone；非法快照里的较高 revision 不会成为已提交历史。连续空态拒绝不产生 generation/catalog churn。inactive-only 历史可更新，但当前不可变来源不变时 CatalogRevision 不增长（rootDecisions）。

新 Active 代际从 1 起；相同对象/撤销 revision/原 resolver 型号与 serial 键保持代际；断线/省略/对象替换、reset/config stage、有效 descriptor 改变撤销旧 ID 并建立下一代际。metadata/version/忽略 JSON 不进入身份判断。最后代际计数不按型号或连接顺序传给另一槽；旧 ID 不再发布，本类不改变 Fixed 选择。

完整 Active **观察集合**按相同非空 canonical SerialKey 前置分组；即使某个有效 Active 观察因 GenerationExhausted 无法发布 SourceId，它仍参与 collision（v3），不得把“不可发布”错当“已经离线”。可发布成员全体重复均 Ambiguous，包括型号证据不足但拥有相同非空键的描述。真正省略或显式非 Active 才不参加本地 collision。耗尽槽仍只有 per-slot 错误，不为其生成/重用 ID。冲突结果保留原键和 descriptor，DeviceIdentity=`session:<SourceId>`；唯一强来源保留原 serial key，弱来源各自 session key。没有持久 ProfileKey 分配、filter/calibration 容器或跨实例复制。

每个新 SourceId（包括初始 Ambiguous）IdentityEpoch=1。初始 Ambiguous ID 必须列入 InvalidatedBindingSourceIds；同 SourceId 的 collision 进入/退出递增 epoch 并 invalidate，但不 revoke SourceId。同组增加/删除第三项而存量成员仍 Ambiguous 不反复增长它们 epoch。来源表、所有 delta 与协议诊断均排序确定，不暴露“先到来源暂时为强身份”的结果。

UsageRevision 只对**同一仍当前 SourceId**列 mapping revalidation delta；若同时建立新代际，旧 ID 撤销，新 ID 不列 usage-only delta（v1）。本类不执行映射或恢复 UserValidated；未来调用方仍须实际检查。

代际 Int32 耗尽只禁用该槽并返回 AppliedWithErrors，不回绕，其他合法槽可继续发布。IdentityEpoch/CatalogRevision Int64 耗尽进入终态 Faulted、清所有旧来源并撤销一次；epoch 耗尽清非空来源时 catalog 未 Max 则增加一次，到 Max 则保持 Max（v2）。进一步 Apply 返回同一个空 snapshot、同 revision、空 delta，不重新激活或消费代际。

## 4. 作者严格纯验证与未验证范围

`bash -n verification/v125-qa/registry/build-registry-developer-tests.sh` exit 0。runner 仅用本机已有 Unity 6000.3.25f1 附带 Roslyn/.NET 与 Mono 4.8 API 编译 frame/resolver/registry/作者测试，`-warn:4 -warnaserror+`，然后运行纯 C# 程序；不是 Unity/native 构建。

v3 修复后最终作者运行：**33,883 checks / 0 failures / exit 0**。产物目录 `/private/tmp/aerosim-registry-developer.xEQjTB`；测试 exe SHA-256 为 `12eddccbf18eb52695804b6d2debc66bccd8828f339a7c9a336a2c8f86aa4607`。此前 15,658/33,843/33,857 运行是修复前作者开发结果，不当作 v3 修复后或独立 QA/冻结后的测试。

根源码复核发现旧计数在成功分配 generation 后进行，耗尽 Active 槽被 continue 而遗漏，可令另一 live 同 serial 来源错误恢复强身份。根先冻结 v3，再授权本成员将冲突计数前置为全部已验证 Active 观察。新增 26 个作者边界断言：存量 Ambiguous 同键设备一槽耗尽仍维持幸存者 barrier、首见幸存者初始 epoch=1 且 binding invalidated、证据不足但非空 serial、所有顺序一致、重复耗尽幂等、真正省略/disabled 才允许 epoch-gated 退出，以及不同 serial 耗尽槽不造成假冲突。反射 fixture 同时设置最大 generation 的 active entry/slot/current，避免把不一致测试状态当真实运行证据。

定向覆盖：五类非活动态/省略与重复阶段、同 tick barrier、usage 与 reset 合并、对象/descriptor 替换、弱身份隔离、collision 双方/第三项/解除/初始 epoch、证据不足的 nonempty serial、serial case、remote exclusion、原 resolver 键复用、非法输入与坏快照无 stale active、token/revision tombstone、坏快照不提交新 revision、构造拒绝、集合/DTO immutable、无原 serial 保留、全六排列、四种 culture、Int32/Int64 overflow 与终态 delta，以及多槽 overflow 诊断排序一致。

另有 900 组三种硬件 kind 的定向重复/重连/冲突/usage 序列；随机 fixture-state 参考测试 **1,200 ticks、6 synthetic slots、seed 1251011**，预期代际/epoch/catalog/delta 按独立 fixture 状态计算后对照 production registry，输入每 tick shuffle。参考不读取 registry 私有状态，overflow 测试才通过反射注入边界，不加生产测试后门。作者测试不是独立 QA；root 仍需冻结实际字节、另派成员编写预期及至少三项严格编译后检出的 mutation。

旧 resolver/frame/router/touch 当前 SHA 与此前冻结一致；仅做哈希确认，未把旧计数当本模块的新验证。新增 meta GUID `c492de91ff1944e2884188b4c656d523`，只读核查 Assets 的 **90 个 meta GUID** 均为 lowercase32hex、无重复，没有启动 Unity 导入。

未验证/未实现：真实 Unity 设备事件与 raw、processor bypass、mapping/filter/calibration、profile 事务、应用接线/Fixed 行为实际运行、physical controller、iPad/iPhone、真实 pointer UI、OS focus/background、任何新 native binary。所有此类字段均 NOT_RUN/NOT_IMPLEMENTED，不关闭整体 Gate A 或 1.25。

## 5. 实施交出指纹

| 文件（Runtime 相对 AeroSim/Assets/AeroSim） | SHA-256 |
| --- | --- |
| Runtime/Input/InputIdentityRegistry.cs | `2536d814d4430b436ad322b602943b8a88de7ff294203034c91cbc960a730ba2` |
| Runtime/Input/InputIdentityRegistry.cs.meta | `23cb422fb45e6f8e35ed8b473f9a27b14ef11dfd6436fe3334bdfa7b019c70b3` |
| verification/v125-qa/registry/InputIdentityRegistryDeveloperTests.cs | `325ddb5b6a41db19840d64841d54fcaece0827418116c0ca1a78d8122a8f98b7` |
| verification/v125-qa/registry/build-registry-developer-tests.sh | `94a9ffedb85e43b7c2169f4add1b2414fdb38d67325a452b2344e8340e3d7595` |
| docs/V125_REGISTRY_CONTRACT_PROPOSAL.md（未改） | `629b4c2c68ced3bc3ea07c781ade52aa32e5a85adac53e9c23e9cd2ed6acfd2c` |
| verification/v125-qa/registry-contract-freeze.json（未改） | `b2db0dc5a707fb7d47425d066fffd3d5cfadaeb1e90469931c686d27ab31772f` |
| verification/v125-qa/registry-contract-addendum-v1.json（未改） | `ea105a124be6eb63039a24efd0cb2dff8ef2a2f4afbc775759cb387cc8fd4542` |
| verification/v125-qa/registry-contract-addendum-v2.json（未改） | `3ba7e0569811104abb91d40b9b18ce9ab79db39a56b4d7a20c2837042665c881` |
| verification/v125-qa/registry-contract-addendum-v3.json（根新增，本成员只读） | `144f93e27a8dd6039fa5e539e70f971cd5dc5331933fb96042f873bd915a1696` |
| Runtime/Input/InputDeviceIdentity.cs（未改） | `88a215d7431824c3c3062d377c4d2e35a7bdd3af81348bf1add4aca2adca084f` |
| Runtime/Input/FlightControlFrame.cs（未改） | `aedd8c7593bf61f61cfbd7c2fcc082570828f0f8d486ec39b2d8a0c232fd773d` |
| Runtime/Input/InputRouter.cs（未改） | `991a36ee16244bb3d1e1e60578331c0481cc994464bcc6be105076d4cb5006ef` |
| Runtime/Input/TouchInputState.cs（未改） | `2319cdc4e7dd9aa4510565caba6b1d0be5a712fadd8e032328f0ec236b470fb6` |

本文不自引用散列；根保存 source freeze 时另存实际字节。本成员交出后停止写入，后续任何修复都须根重新授权与重冻，不能在 QA 读取途中改源。仓库：[AeroSim](https://github.com/yijunchai2026-gif/AeroSim)；分支：[codex/v125-product-lines](https://github.com/yijunchai2026-gif/AeroSim/tree/codex/v125-product-lines)。本成员未 commit/push，具体提交和新鲜远端 SHA 由根统一交付，现有链接不证明本批已上传。
