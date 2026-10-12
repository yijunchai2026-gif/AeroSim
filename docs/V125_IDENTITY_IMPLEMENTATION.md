# AeroSim 1.25：Gate A 纯身份子集实施交接

2026-10-10，America/Los_Angeles。状态：**作者实现 / 严格纯 C# 自测通过 / 待根负责人及独立 QA**。这是单一设备描述解析子集，不是 Gate A 关闭、三端输入接线、硬件可用性或 1.25 完成。

## 范围与文件所有权

本成员完整阅读 AGENTS、[Gate A 候选契约](V125_GATE_A_CONTRACT.md)、[共享输入核心](V125_INPUT_CORE.md)、FlightControlFrame.cs、现有严格纯测试跑器及 root 的[身份精确补充冻结](../verification/v125-qa/identity-contract-freeze.json)。原候选字节没有更改；补充明确 `interfaceName AND layout`、nullable signed integer decimal 和 serial 的完整字段编码。

本成员仅新增以下五个文件，不修改根负责人的候选08冻结源码、旧 FlightInput/InputRouter/Touch、校准配置、实际存档、原图或其他成员文件：

- `AeroSim/Assets/AeroSim/Runtime/Input/InputDeviceIdentity.cs` 及 `.meta`。
- `verification/v125-qa/identity/InputDeviceIdentityDeveloperTests.cs`。
- `verification/v125-qa/identity/build-identity-developer-tests.sh`。
- 本文。

没有启动 Unity Editor/Player 或 native 应用，没有硬件轮询、外部消息、Git commit/push；统一整合、冻结、验证和提交仍由 root 负责。新增未被调用的纯类不会把旧应用自动升级为新输入行为。

## 公开类型与 fail-closed

namespace 为 `AeroSim.Input`，复用 `FlightInputSourceKind`；五个 DTO/算法类型只用 System，不依赖 Unity：

| 类型 | 公开面 / 边界 |
| --- | --- |
| `InputDeviceDescriptor` | sealed、get-only。构造参数为 kind、interfaceName、manufacturer、product、layout、四个 nullable int HID 字段、serial、displayName、version、capabilitiesJson；只保留规范化模型证据及散列，后三项无身份用途且不保留 |
| `InputIdentityDescriptor` | sealed、get-only 的持久候选描述：`FormatVersion=identity-v1`、小写 kind、四字符串、四 nullable int、`SerialSha256`；不含 runtime、原 serial、显示名、version 或 capabilities JSON |
| `RuntimeSourceHandle` | sealed、get-only：`Kind, RunId, RuntimeDeviceId, SourceGeneration`；两个数字使用 Int32，由未来 registry 维护代际，本类型没有事件状态机 |
| `InputIdentityResult` | sealed、get-only：`IsValid, Error, Kind, SourceId, Confidence, ConfidenceCode, ModelKey, SerialKey, DeviceIdentity, IdentityDescriptor, HasModelEvidence` |
| `InputDeviceIdentity.Resolve(descriptor, handle)` | 无副作用、无设备读取、无 GUID 分配，返回单 descriptor 结果 |

`IdentityConfidence` 为 `ReportedSerial/ModelOnly/SessionOnly/Ambiguous`；`ConfidenceCode` 分别固定为 `reported_serial/model_only/session_only/ambiguous`。这个单条 resolver 不产生 Ambiguous；枚举只保留后继 registry 需要的契约值，不能据此声称 live serial 冲突已经检测。

`InputIdentityError`：`None/MissingDescriptor/MissingHandle/UnsupportedKind/KindMismatch/InvalidDescriptorText/InvalidRunId/InvalidRuntimeDeviceId/InvalidSourceGeneration`。只支持 Keyboard/Gamepad/Joystick；Touch 原来双杆 SourceId 不经本硬件 resolver。null、未知/None/Touch kind、kind 错配、非小写32hex runId、非正 runtime ID/代际都拒绝。模型/serial 非法 Unicode 由 NFC 或 strict UTF8 检查拒绝，输出固定错误码，不拼输入到异常/日志。

拒绝结果 `IsValid=false`、kind=None、SourceId/ModelKey/SerialKey/DeviceIdentity 为空、IdentityDescriptor=null、HasModelEvidence=false。Confidence 默认 SessionOnly 仅为无效结果的枚举占位，消费者必须先判断 IsValid，不能把它当有效 session 来源。

## 精确编码和身份隔离

SourceId 为 `kind:runId:runtimeDeviceId:sourceGeneration`，kind 小写，runId 严格32位小写hex、不 trim/改大小写；两个正 Int32 使用 invariant `D10`。单次相同 handle 重复解析稳定，不含显示名称。Int32.MaxValue 为合法十位数字；代际溢出/设备事件撤销属于后继 registry，不在本模块暗自循环。

model 固定字段顺序：`identity-v1, kind, interfaceName, manufacturer, product, layout, hidVendorId, hidProductId, hidUsagePage, hidUsage`。字符串 null→空、NFC→Trim，保留大小写；数值 absent→空、present→invariant decimal。对每字段使用 UTF8 **字节数** 十进制、冒号、该 UTF8 字节拼接，再 SHA256 小写hex得到 `model:<hash>`。

serial 同样 NFC/Trim，非空时作为**第11个长度前缀字段**追加到全部 model 字段，再散列得到 `serial:<hash>`；不是把 model hash、SerialSha256 或其他分隔字符串作为替代输入。`IdentityDescriptor.SerialSha256` 另为 normalized serial 本身 UTF8 的 SHA256，用于未来持久描述；原 serial 只作为构造参数和局部变量短暂参与计算，没有存入任何 DTO field/property，也不进入报告文本。空/空白 serial 的两个 serial 散列字段都为空。

root 补充的精确型号依据为：

```text
interfaceName 非空 AND layout 非空
AND (product 非空 OR (VID present AND PID present AND VID != 0 AND PID != 0))
```

有依据且非空 serial → ReportedSerial，DeviceIdentity=SerialKey；有依据但无 serial → ModelOnly；没有依据，即使 serial 非空，也只为 SessionOnly。两种弱情况 DeviceIdentity=`session:<SourceId>`，不会以 ModelKey 批量套校准，也不每帧生成 `weak:<guid>`。实际 weak ProfileKey 只在未来用户创建配置 draft 时分配，本模块没有 ProfileKey 或配置写入。

HID 四数值在这层只做确定 decimal 编码，负值与零不会被悄悄 clamp/猜测；**不是验证真实 USB/HID 范围或证明该设备可用**。本模块不解析 capabilities JSON；未来硬件薄层负责可靠 HID parser、malformed 时四项 absent 及诊断，不能把这里忽略 JSON 说成 parser 已实现。

ReportedSerial 只表示单条描述报告了足够的型号和 serial，**不证明 live 唯一、真实物理硬件认证或允许安装校准**。本批故意测试同 serial 不同 handle 返回相同 SerialKey/不同 SourceId，保留后继 registry 必须同时标 Ambiguous 的缺口，不把单函数结果误作自动校准授权。映射指纹、校准安装、代际撤销、持久迁移、sidecar IO、真实来源目录和生命周期均尚未实现于此子集。

## 作者验证（独立 QA 另行）

运行 `bash verification/v125-qa/identity/build-identity-developer-tests.sh`；shell `bash -n` 通过。runner 使用现有 Unity 6000.3.25f1 附带 Roslyn/.NET 启动器和 Mono 4.8 API，以 `-warn:4 -warnaserror+` 只编译 FlightControlFrame.cs、本实现、作者测试，然后运行纯程序。产物在唯一 `/private/tmp/aerosim-identity-developer.NwKIxn`；没有 Unity/native 或用户目录写入。

实际结果：**10,630 checks / 0 failures / exit 0**。作者固定 ASCII、Unicode/emoji/组合音符、全空三组 model/serial SHA 向量另外用 Node crypto 的内存字节编码生成常量，测试读取生产 DTO 对照，不调用生产 HashFields 作为 oracle；这是作者校验，不代替独立 QA。

覆盖：UTF8 byte prefix/NFC/Trim/colon、null/empty/whitespace、保留 case、serial 与 model 散列差别、metadata/version/运行时身份不进入 model、nullable absent/zero/负值/extreme decimal、三硬件 kind、跨运行/ID/代际强弱身份、同型号不同 serial、不完整型号+有serial仍弱、144项型号证据矩阵、非法 Unicode/null/kind/run/ID/代际 fail-closed、四种 culture 的 invariant 结果、1,200项规范化/代际稳定性向量、反射检查 DTO get-only/readonly backing fields/不保留 synthetic serial。失败日志只给测试名称，不打印输入或实际设备 serial。

新增 meta GUID 为 `85805895bf744024a142f8090fa79366`。本次只读检查 Assets 的 **89** 个 meta GUID 均为 lowercase32hex、无重复。没有调用 Unity 导入；独立 QA 应冻结实际字节并另写测试及至少三项隔离 mutation，严格编译成功后由测试拒绝，不能将编译失败当检出。

## 实施冻结指纹

| 文件 | SHA-256 |
| --- | --- |
| `docs/V125_GATE_A_CONTRACT.md` | `f2237ca63358afcd2ee7c76eb828609e61b8094a15ecdd43476d5f7012257697` |
| `verification/v125-qa/identity-contract-freeze.json`（root 只读补充） | `3db85a4f2dd502fa8755673019f9ea736894dda27df58b0baccf5e95e813d3ed` |
| `Runtime/Input/FlightControlFrame.cs`（复用、未修改） | `aedd8c7593bf61f61cfbd7c2fcc082570828f0f8d486ec39b2d8a0c232fd773d` |
| `Runtime/Input/InputDeviceIdentity.cs` | `88a215d7431824c3c3062d377c4d2e35a7bdd3af81348bf1add4aca2adca084f` |
| `Runtime/Input/InputDeviceIdentity.cs.meta` | `5068d83ae1380509460d35606b10f3037379e668d5bcd2dd96ad59837b971e7d` |
| `verification/v125-qa/identity/InputDeviceIdentityDeveloperTests.cs` | `3cb4d9264419a2999bc488c8f77a98def6e5a22bef4f26e3984e329b1dbecb50` |
| `verification/v125-qa/identity/build-identity-developer-tests.sh` | `7c4d8fd76dead55869e0556977f764ad81589aa3cca7cbaa4b0a1f8a49518007` |

Runtime 路径统一位于 `AeroSim/Assets/AeroSim/`。本文不自引用散列；root 冻结时另存本文 SHA。作者交接后停止写入，后继修正必须重新冻结和验证。

仓库：[AeroSim](https://github.com/yijunchai2026-gif/AeroSim)；分支：[codex/v125-product-lines](https://github.com/yijunchai2026-gif/AeroSim/tree/codex/v125-product-lines)。本成员没有提交或上传；具体 commit、push 成败和新鲜远端 SHA 由 root 统一提供，不能以这些现有链接声称本批已经上传。
