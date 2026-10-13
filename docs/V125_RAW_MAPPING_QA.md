# AeroSim 1.25 raw mapping：独立 QA 结果与边界

2026-10-12，America/Los_Angeles。状态 **PURE_MAPPING_VALIDATION_PASSED / GATE_A_OPEN / UNWIRED / PENDING_USER_SCOPE_CHOICE**。实际独立严格编译 exit0；**46,992 checks / 0 failures / runner exit0**。这只通过 System-only 四轴路径/metadata/指纹子集，不是 raw 实采、live授权或1.25交付PASS。唯一 worktree 为 `v125-remote-review/飞行模拟器`，分支 `codex/v125-raw-mapping`，根指定基线 `c1916a0f63a4a17d2af8767e4fd2dcdcab79ee2f`；不修改原开发目录、不自行 Git、不启动 Unity/native、不接触用户存档。

## 1. 独立性与先锁预期

准备期完整读取 AGENTS、[Gate A](V125_GATE_A_CONTRACT.md)、[raw mapping 提案](V125_RAW_MAPPING_CONTRACT_PROPOSAL.md)，当时没有读取新生产实现或作者测试。读取时 SHA：AGENTS `52a30e1d1ce42d5c11ac7ab1efadbd081e846e62ebd399ee9c030fa2cc0d999d`；Gate A `f2237ca63358afcd2ee7c76eb828609e61b8094a15ecdd43476d5f7012257697`；提案 `b451456087f961fc201adb2fa11500a2c54e8004893618a257ba5aab382a3d62`。根先冻结两份独立预期，后冻结16文件；QA核对全部SHA后才全文读取357行生产源并写独立driver。始终没有读作者测试或用作者输出倒写预期。

独立[八个预编码向量](../verification/v125-qa/raw-mapping/independent-preencoded-vectors.json)包含精确字段序列、UTF-8 编码字节的 Base64、字节数、字面 SHA-256。使用提案字段自行编码，Python strict UTF-8 + hashlib 与 `/usr/bin/openssl dgst -sha256` 双路交叉计算；不调用生产 `HashFields`。向量的 synthetic type/format 只是 DTO 输入，不代表实际 Unity/硬件。

| 字面向量 | 字节数 | 字面指纹 |
| --- | ---: | --- |
| Keyboard | 69 | `54de1323081596043fac0089d2d021ed51d91512c64f5e4d6360a49d4b0b5051` |
| Touch | 63 | `6587ee3d86628693f0d9d924451b86f5230c71828068f589341200f989ca69b6` |
| ASCII 四轴 | 343 | `2aa66c21618ab6cf5fd506646c01ed0f02a8ef92b66428f22cf7f507a0290ca5` |
| T/Y 对换 | 343 | `a3681a8ac65da528513371aac9c6bf2ae39f59ead4a2c984c26753fd9a3e0a0c` |
| 中文 + NFC é | 346 | `8963fefc3a33d0fc9bdd3aabd9cf5f07ea8c0a13874f6d5ee5272bbd2654f6da` |
| 中文 + NFD e+acute | 347 | `f9a80e6e31dc8b45732cc82fb3db9433fb2538b3d98c485042d9d0f1805fdc0d` |
| UInt32 最大值/64bit/flags/有限量程 | 372 | `1aadd3a67fad31898254ed33620277be7a63322029a85921897b1050e167641b` |
| Gamepad固定四轴 | 363 | `598003c38a291d4f90791b22497163d62e4d4ff372fa9d88d8ed832373c30fd4` |

## 2. 独立检查范围与字面预期

所有失败检查完整 empty-result invariant：`IsValid=false, Mapping=null, Controls=[], MappingFingerprint="", RawConvention=""`，至少一条排序稳定诊断。没有发生普通业务异常；不复用之前成功结果。成功检查 T/Y/P/R 四轴顺序、全部原始 metadata、空 Diagnostics、指纹和严格状态。

[字面 JSON 样例](../verification/v125-qa/raw-mapping/independent-prepared-expectations.json)先锁输入、结果投影与冻结Field/诊断排序，包含有效四轴、顺序、Unicode、case、duplicate、device-selector、通配、Button、unknown metadata、多错误收集、UInt32/float/flags、正负零与不可变序列。JSON属于独立QA投影，不声称生产有JSON序列化API。[实际driver](../verification/v125-qa/raw-mapping/independent-driver.cs)直接读取全部8个向量/26个样例，最后序列case实际clear输入list、再次validate、尝试IList写入并核旧结果不变，不只是复制常量。

| 组 | 独立计划 / 字面预期 |
| --- | --- |
| Kind/API gate | Keyboard/Touch `GetBuiltIn` 仅 `BuiltInValid`；其余含未知 kind `Rejected/UnsupportedBuiltInKind`。Hardware Keyboard/Touch/None/未知值 `UnsupportedHardwareKind`；Joystick null mapping 始终 `Missing/MappingMissing`；Gamepad 有 supplied mapping 始终 `Rejected/MappingNotEditable`，即使 mapping 等于 builtin且 layout/catalog 同时错误。合法 Joystick 仅 `JoystickCandidate`，绝不 UserValidated。 |
| 固定四轴/次序 | 有效 synthetic四轴返回 mapping及controls T/Y/P/R，锁 ASCII字面hash；24种catalog排列不变。mapping T/Y对换锁第二字面hash并变更 Controls顺序。Gamepad只认 leftStick/y,x/rightStick/y,x；缺轴拒绝，不用trigger/dpad补；Hardware GetBuiltIn不能伪造目录。 |
| 精确大小写/Unicode | 目录 `yaw/x` 请求 `Yaw/x` 为 axis1 `AxisNotFound`；NFC/NFD path 不互认，不normalize；layout NFC/NFD锁不同hash。非ASCIIUTF8字节数而非UTF16长度；非法单独高/低surrogate拒绝。`fr-FR/tr-TR`文化重复向量结果不变。 |
| 路径语法 | 每个mapped axis轮换测试 null、empty、开头/结尾slash、`//`、`.`/`..`段、`*?{}<>[]:`、反slash、ASCII及Unicode whitespace/control、selector/usage/alias；非法path诊断不回显原文。合法 `axis.v1/x` 不做parent traversal。合法但未命中 `Gamepad/throttle/y` 返回 AxisNotFound，不剥prefix/suffix。 |
| Cross-device边界 | `<OtherDevice>/throttle/y` 或 `/other/throttle/y` 语法拒绝；`OtherDevice/throttle/y` 不suffix-match同catalog的 `throttle/y`。DTO没有device handle，因此无法证明真实混轴检测；伪造canonical目录可以结构通过，只是candidate，后继adapter须证明同一live实例。 |
| Duplicate/目录阶段 | 每个axis pair和三轴同path，涉及每轴各DuplicateMappedPath，不择首。重复选中/未选中的合法catalog path，以及同path不同type均全局DuplicateCatalogPath；24排列诊断稳定。null目录MissingCatalog；null项MissingControlDescriptor；empty目录四条AxisNotFound；unknown ValueKind在目录阶段UnsupportedControlValueKind，即使未选。目录阶段错误抑制mapping/metadata后阶段。 |
| 类型与metadata | 选ButtonFloat axis为ButtonAxisNotAllowed，Other为AxisTypeMismatch且不追加Axis metadata错误；合法未选Button/Other+nullpreprocess允许且hash不变。所选type null/empty/whitespace/control/invalidUTF8，format=0，size=0，preprocess=null，unknownClamp均拒绝。非32bit及任意非零UInt32 format可结构通过，不字符串化FourCC或只认FLT。Axis subclass FullName改动使hash变。 |
| Finite/量程 | 7个Single字段逐个×NaN/+Infinity/-Infinity×四轴共84组合，关闭开关也拒绝。Clamp四值枚举：启用等界允许、倒序拒绝；None关闭倒序有限允许。Normalize=true：zero=min或低于min允许，zero=max/大于max以及min>=max拒绝；关闭normalize有限倒序允许。有限-10/20、负/零ScaleFactor、0.125、小幅有限量程不被校准阈值拒绝、不clamp raw（本模块无raw数值）。正负零同hash；R精度不是F2。 |
| 指纹差异/不变 | layout、每path、type、format、size及11个preprocess字段分别有效单改hash必变（+0/-0例外）；依T/Y/P/R的所有control全部入hash、paths先行。未选合法项增加/移除/重排不变。API不接serial/source/revision/processor stack，不能凭同hash取得授权。 |
| 不可变/无状态 | 反射sealed/get-only、输入DTO constructor不修正/trim/抛业务异常、result/diagnostic constructor无public、AxisMapping不公开array/Paths/index。输入List/array调用后替换/清空，不影响旧Controls/hash；如结果支持IList写入须拒绝。失败再调用/成功再调用/不同catalog再调用，旧result及输入DTO逐值不变。 |
| 范围与兼容 | 新模块System-only，无Unity/LocalStore/IO/读值/filter/calibration/合轴状态；不签发UserValidated/Available/Identity/SourceId，不持久化。保护旧源码和历史92/32FAIL检查器字节，不运行/改断言来制造新通过。 |

## 3. 已完整读取 root 冻结澄清

根冻结 [contract-freeze.json](../verification/v125-qa/raw-mapping/contract-freeze.json) SHA `5cb1e381594dabc278f4f7cc1b2c1fbea9183fb1662cc9cd2b442feb12c9fffd`，及 [addendum-v1](../verification/v125-qa/raw-mapping/contract-addendum-v1.json) SHA `e430504527b08cf7d9df0237684ab4a2016df814c9f8fc12c6eee30cd48d4428`。两处此前文字缺口已经根冻结，独立样例使用冻结Field字典和Gamepad缺轴Rejected，不从生产输出补预期。

新增精确断言：Stage4非法path/未知ValueKind分别收集；Stage4/5重复仅分组合法path；Stage6缺轴或Button/Other只短路本轴metadata，Axis type/format/size各自独立收集；缺Preprocess不抑制其他metadata错误。有Preprocess时未知Clamp及所有非有限字段都收集；范围诊断仅依赖相关有限操作数，其他float错误不抑制可算范围失败。type额外拒逗号、正反slash、等号、冒号；仍不能仅凭type文本证明CLR类型或同device所有权。后继如addendum再改API，须先改并重新冻结预期，而非由实现倒写。

## 4. 实际执行与 mutation

执行 `bash verification/v125-qa/raw-mapping/independent-runner.sh`。使用[独立runner](../verification/v125-qa/raw-mapping/independent-runner.sh)和已有Roslyn/Mono，`-warn:4 -warnaserror+`严格纯编译；不启动Unity/native、不读存档。真实结果和source/exe/log/driver/runner SHA、完整命令见[compact结果](../verification/v125-qa/raw-mapping/independent-results.json)。基准产物仅 `/private/tmp/aerosim-raw-mapping-independent.fUUaiK`，不提交临时exe/log。

8个预编码向量与26case各在 invariant/en-US/fr-FR/tr-TR/ar-EG 下实际执行。`-0` fixture不使用 `float.Parse("-0")`，而通过 `BitConverter`构造0x80000000；输入与返回metadata的ClampConstant/NormalizeZero保持负零sign-bit，共80次核查，同时指纹仍与+0的字面hash一致。

| 隔离mutation | 严格编译 | runner | 实际 checks / failures | 关键检出 |
| --- | --- | --- | ---: | --- |
| lookup Ordinal→OrdinalIgnoreCase | exit0 | exit1 | 42,410 / 36 | 冻结CaseMismatch完整拒绝投影；另有x/X目录异常，非唯一检出 |
| 放行所选ButtonFloat | exit0 | exit1 | 46,982 / 22 | 冻结Button短路诊断，以及合法Button metadata仍必须拒绝 |
| 指纹漏Invert字段 | exit0 | exit1 | 46,992 / 79 | 预源码锁定硬件字面hash与逐轴invert敏感性 |
| JoystickCandidate→BuiltInValid | exit0 | exit1 | 46,992 / 71 | 字面候选状态，不把结构合法变成builtin/live授权 |

四个mutation只改唯一temp目录内生产源副本，全部先编译成功，再被同一未改driver/两个JSON拒绝；没有降低断言、删旧失败或把编译失败算检出。新增模块未发现冻结纯契约缺口；这不证明后继adapter/owner没有缺口。

## 5. 字节守卫、美术审查与文档门禁

实际只读复核：24基线文件 + 23原概念图 + 原开发目录3份dirty meta + 16 source-freeze文件全部SHA匹配，7份机型审核稿均 `pending_user`、用户批准0。两名美术报告全文读取且匹配冻结SHA：[网格/近景](V125_REMOTE_COAST_MESH_ART_REVIEW.md) `bdd2389555e8728448f3279a7d82343d4ca270cef342bc73598caae6d6984829`；[材质/水面/天气/植被](V125_REMOTE_COAST_SURFACE_ART_REVIEW.md) `53c6347083372b77ab20b2e49bc5ff54f639cb545379877f7c59ef145c4a64bf`。QA没有重渲染、改资产或独立代签视觉。

未关闭风险包括P11过暗capture/缺prefab与导入metadata漂移、单位/LOD/碰撞验证；P04/P05仍为规格参考；P02纹理“绑定却不贡献输出”；P06不透明图集与box植被不等于cutout物种；水体/天气whole atlas不等于单元FX和linear mask/移动性能。这些是已读美术报告的范围内风险，不是本QA的新运行视觉实测。

原文档检查器SHA `b3b34d0999920cdc98e0423bb501d1dfbc05b82dd225c7230af410e8f7d4036b`未改，[incoming记录](../verification/v125-qa/raw-mapping/incoming-prd-audit.json)仍 **92 checks / 32 inherited failures / exit1 / FAIL_DOCUMENT_QA**。实际在rootstage后用原checker添加本batch6文档/progress并执行 `--require-tracked`：整体 **374 checks / 32 inherited failures / exit1 / FAIL_DOCUMENT_QA**；单独新增scope **282个文档存在/相对链接/已跟踪依赖检查、92条引用、0新失败、0 publicationPendingUnique**。这里只是新增scope的链接/依赖PASS，不能升级为全PRD或产品语义PASS；QA自行没有Git写操作，根随后需re-stage本报告与compact结果最终字节。

## 6. 范围外门禁保留

incoming PRD v4与1.25三端/23原图冲突等待用户决定；本模块只处理双方需要的公共纯子集，不改总体目标、旧失败或reference。指纹只证明metadata编码相同，不证明用户live四轴核对、实际raw/processor bypass、source可用、rearm、store、UI/三端接线、真机或外观批准。Gate A/1.25交付及人工验收均未关闭。
