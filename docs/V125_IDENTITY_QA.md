# AeroSim 1.25 Gate A：纯输入身份独立 QA

状态：**PASSED_PURE_IDENTITY_SUBSET / 仅纯身份解析独立 QA 通过**。只验证 [Gate A 契约](V125_GATE_A_CONTRACT.md)中纯身份解析子集与 [root 精确补充](../verification/v125-qa/identity-contract-freeze.json)，不是完整 Gate A 或三端验收，不写用户配置、不启动 Unity、不提交 Git。首次准备断点为`PREPARED / 尚未编译或执行`，当时本文SHA-256为`4e6978001ebd9013a9ef3c4c1797de7df5297497ac5a753e936c48b4dd592b2d`；下列原设计和固定预期未为结果改写，执行证据另追加于末尾。

## 独立预期，先于读取实现

已完整读 AGENTS、契约、补充及 FlightControlFrame。`hasModelEvidence` 必须为 `interfaceName非空 AND layout非空 AND (product非空 OR VID/PID双方存在且非零)`；字段 null→空、NFC/Trim、保留大小写，数值 absent→空、present→invariant decimal。SHA-256 输入按每字段的 **UTF8 字节数**加冒号前缀，不用 UTF16 字符数；serialKey 对完整 model 字段加最后 serial 字段散列，不使用 hash-of-hash。model/serialKey 不包含运行时 deviceId、runId、sourceGeneration、显示名、驱动版本或原 capabilities。弱实例必须使用 `session:<SourceId>`，不能按同型号 modelKey 共用身份。

以下四个固定预期在未读生产算法/开发者自测时独立计算。字段顺序为 `identity-v1,kind,interfaceName,manufacturer,product,layout,hidVendorId,hidProductId,hidUsagePage,hidUsage`；serial 是末尾额外字段。

| 向量 | model 输入字节的 UTF8 文本展示 | 预期 model SHA-256 | serial / 预期 serial SHA-256 |
| --- | --- | --- | --- |
| ASCII + embedded colon | `11:identity-v18:joystick3:HID4:Acme12:Flight Stick8:Joystick4:46605:221361:11:4` | `89ce3642f4c8ca8f8664b0ed80c31a9e036cc881dc4f2822ddd24f591fbeed5f` | `Serial:A` / `14cadee8e2b28ca1271bc628538fb77e1e8c4acc7d7568d3d7efc0d2c93eba28` |
| Unicode、emoji、冒号 | `11:identity-v17:gamepad6:蓝牙5:Café8:🎮:座8:GámePad0:0:1:11:5` | `7077f0cd0172402640bc31170a2eb52d2fa89ec273879f3544ff6581d93ebc1f` | `序:列🎛` / `d2cc060745b44b0d3249598dbf76d646d37cb783f72a08f108e13ce7e479207c` |
| optional 全空 | `11:identity-v18:keyboard0:0:0:8:Keyboard0:0:0:0:` | `6857a938d81f8d909c785f051faf346bf4fdcad6ea69fb36a5f0742c882af942` | 无 serial |
| nullable signed 数值 | `11:identity-v18:joystick3:HID0:0:8:Joystick2:-12:-22:-32:-4` | `72cfa0966a586f6894112b5bb54b8c136b605670f51e6efdb1f536afe2bea048` | `S` / `ce128a51bc6341c9433c5dca16bd21209727056cd90d747cc151939e382aaeeb` |

这些 serial 均为合成固定测试数据，不是真实设备信息。随后由独立 MemoryStream/UTF8 字节预期与固定散列交叉核对，不复制开发者算法。

## 执行前冻结的待执行项目（保留原准备断点）

- 逐项覆盖 modelEvidence 真值表、强/弱置信度、空 serial、同型号两实例、重新启动/代际变化、强 key 不随运行时变化、serial 大小写、NFC/Trim 与字段边界。
- malformed Unicode / 非法运行时 handle / 非硬件 kind 明确拒绝；结果及错误只含合成预期或稳定错误码，不泄漏传入 raw serial。
- 固定 seed 的独立随机向量与 property 检查，不将 live registry 重复 serial 处理、真实 HID 能力、校准绑定、sidecar、飞行接线算在此子集中。
- 严格 Roslyn `-warn:4 -warnaserror+` 编译，Unity bundled Mono 纯 C# 执行；原 InputRouter 3,050 和 Touch 514,425 套件原样复跑。
- 至少三份仅在 `/private/tmp` 的 mutation：大小写折叠、UTF8 字节长误换字符长、弱身份误用 modelKey（或 runtime ID 进入持久 key）。每份先严格编译成功，再用未修改的独立测试退出 1；编译失败不算检出。

首次准备边界为Unity/native、移动真机、物理控制器和最终用户验收：**NOT_RUN / pending_user**，本次实际执行后这些边界仍未关闭；源码冻结、SHA、实际计数、输出目录和mutation结果补充如下。

## 2026-10-10 实际执行与冻结证据

root批准源码冻结后才完整读取243行生产`InputDeviceIdentity.cs`及112行`FlightControlFrame.cs`并执行；此前已完整读合同/精确补充、原517行独立测试和runner，用独立Node UTF8字节前缀SHA核对四固定向量，model及serial预期4/4吻合。没有改固定值、oracle、测试断言、生产实现或开发者测试来制造通过。

[root源码冻结](../verification/v125-qa/identity/identity-source-freeze.json)SHA-256为`ff4b9d6c3ecd29975a58bc7227d281b000d17e9747f82d3fe70977cc49415c05`，执行前后均独立重算8/8文件一致；生产Identity SHA为`88a215d7431824c3c3062d377c4d2e35a7bdd3af81348bf1add4aca2adca084f`。合同`f2237ca63358afcd2ee7c76eb828609e61b8094a15ecdd43476d5f7012257697`、补充`3db85a4f2dd502fa8755673019f9ea736894dda27df58b0baccf5e95e813d3ed`均未变。

| 实际命令/套件 | 严格编译及实际结果 | 隔离执行目录 |
| --- | --- | --- |
| `bash verification/v125-qa/identity/build-identity-independent-tests.sh` | Roslyn `-warn:4 -warnaserror+`编译成功；310,390 checks / 0 failures，exit0；directed3,300、random6,000、invalid46、culture4，seed1251011 | `/private/tmp/aerosim-identity-independent.1B6P9C` |
| 原`build-input-router-tests.sh` | 原runner warnaserror编译成功；3,050 / 0，exit0 | `/private/tmp/aerosim-input-core.6iswB8` |
| 原`build-touch-state-independent-tests.sh` | `-warn:4 -warnaserror+`编译成功；514,425 / 0，exit0；25,746 events，seed1251010 | `/private/tmp/aerosim-touch-independent.kkuR2K` |

编译器为已安装Unity6000.3.25f1附带Roslyn，执行器为附带Mono，仅调用这些纯C#工具，不启动Unity Editor/Player。编译器、Mono及mscorlib/System/System.Core实际SHA保存在[独立紧凑结果](../verification/v125-qa/identity/InputDeviceIdentityIndependentResults.json)。原Identity测试SHA仍为`7d0f66dd9c17333ce3b6d3ee4bfe746adadef9fa5bc2baa0dfa5ba2fd1c249bb`，runner仍为`d176f2f0128af30a122689ba7a42dba31174bddbeecdfe3bb0faca99428e17ba`；3050与514425原测试/runner字节均未改。

### 三份隔离mutation：编译成功后才判检出

仅复制冻结生产源到`/private/tmp/aerosim-identity-mutations.3Bw2Ae`并通过apply_patch各改一处；同一未修改独立runner以路径参数执行。每份都严格编译成功、执行完整310,390检查，随后exit1；不把编译失败、缺文件或异常启动当测试检出。

| mutation | 唯一机械替换及拒绝原因 | 实际 failures / exit | 可执行目录 |
| --- | --- | --- | --- |
| CaseFoldIdentity.cs | Normalize后加`ToLowerInvariant()`；固定散列、规范字段及保留大小写断言拒绝 | 54,190 / 1 | `/private/tmp/aerosim-identity-independent.P2HA2T` |
| Utf16LengthIdentity.cs | HashFields前缀从UTF8 `value.Length`改为UTF16 `field.Length`；Unicode/emoji固定向量及随机字节散列断言拒绝 | 23,053 / 1 | `/private/tmp/aerosim-identity-independent.9AeyHw` |
| WeakModelIdentity.cs | weak DeviceIdentity从`session:<SourceId>`改为modelKey；同型号多实例及重启隔离断言拒绝 | 9,326 / 1 | `/private/tmp/aerosim-identity-independent.eXESMy` |

紧凑JSON记录每份mutation源SHA和exact before/after替换。复跑应从冻结源新建唯一temp目录、核对唯一替换位置后执行`bash verification/v125-qa/identity/build-identity-independent-tests.sh <isolated-source.cs>`；不得修改生产源或测试预期。temp目录是本次本机证据，不保证重启后仍存在，不上传所有二进制/原日志。

### 严格范围结论

本次纯层符合冻结的NFC/Trim、ordinal大小写、UTF8字节前缀、完整descriptor＋serial散列、正运行时handle、强/弱会话分工、稳定拒绝码、不可变DTO及不保留raw serial的独立断言。`reported_serial`只是单描述符报告证据，不等于live唯一性授权或密码学认证；两个相同serial本次会返回相同key，实时registry仍须另测双方ambiguous及阻止自动校准。

完整Gate A中的registry/raw四轴映射、同帧缓存、校准绑定、迁移、sidecar failedIO与Unity合成设备旁路/热变更均**NOT_RUN**；Gate B/C、UI capture、后台/旋转、真实控制器和iPad/iPhone均未执行。原Touch同pointerId复用边界保留，纯算法回归不代签真实指针生命周期。未改23原图、正式资产批准、PRD或业务源码，也不替用户完成最终验收；停止写入后由root整合、提交及核对远端。
