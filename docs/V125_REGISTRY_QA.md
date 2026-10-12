# AeroSim 1.25：identity registry 独立 QA

2026-10-11，phase 2最终结论：**PASS_PURE_IDENTITY_REGISTRY_ONLY**。独立纯逻辑套件严格编译成功，15,889项检查/0失败/退出0；四项隔离源码变异均成功编译后被原样套件检出。不是Gate A关闭、应用接线或1.25交付通过。

以下phase 1是历史准备记录：当时状态为PREPARED_NOT_RUN，本成员完整读取 AGENTS、[132行提案](V125_REGISTRY_CONTRACT_PROPOSAL.md)、[根冻结](../verification/v125-qa/registry-contract-freeze.json)及 [usage 澄清v1](../verification/v125-qa/registry-contract-addendum-v1.json)、[溢出澄清v2](../verification/v125-qa/registry-contract-addendum-v2.json)。当时未打开新 `InputIdentityRegistry.cs` 或作者 registry 测试，也未编译/运行它们。收到phase 2明确授权后才完整读取359行冻结实现；作者registry测试正文始终未读。

## 1. 先于实现审阅的独立预期

[prepared-vectors.json](../verification/v125-qa/registry/prepared-vectors.json)为直接供后续套件消费的字面轨迹：49组、199次完整快照调用、3个反射边界安排，另有9个非法构造runId及8条结构/不可变输出断言。数字是**计划数据数量**，不是通过计数。向量的generation、IdentityEpoch、CatalogRevision和三类delta均逐阶段手工列出；没有用新registry执行结果生成预期，也不复制作者的状态机算法。

数据只有合成DTO。A/B/C分别为runtime ID 7/11/19、对象token 701/1101/1901；A1表示本run下Joystick 7的gen1，A2为gen2。实际预期中保存完整SourceId，不由测试读取实现后决定。示例：

`joystick:0123456789abcdef0123456789abcdef:0000000007:0000000001`

ModelKey/SerialKey字面值来自先前冻结resolver的长度前缀UTF-8规范，用独立Node字节/SHA转换得到；没有调用registry或作者测试。resolver与FlightControlFrame仍分别锁`88a215d7431824c3c3062d377c4d2e35a7bdd3af81348bf1add4aca2adca084f`、`aedd8c7593bf61f61cfbd7c2fcc082570828f0f8d486ec39b2d8a0c232fd773d`。后续比较IdentityDescriptor时可用此既有冻结DTO的规范字段，但不可由新registry决定预期confidence或key。

主49轨迹实际冻结SHA为`3fba38278ddaee8b6c8e21b75fb8ffa81586530bc48216b1c1e157e47513b4c1`。根在只读作者初稿时另发现耗尽槽仍Active但未发布ID的碰撞边界，明确冻结了 [v3政策](../verification/v125-qa/registry-contract-addendum-v3.json)，不回写原freeze。本QA在未读作者源码时按新增合同另写 [v3补充向量](../verification/v125-qa/registry/prepared-vectors-addendum-v3.json)：4组、21次Apply、4个反射安排，覆盖已冲突两源之一耗尽、首次Active即耗尽与首次peer、已有强peer变Ambiguous、缺model证据的SessionOnly peer。只有真正inactive/omission解除peer冲突；无ID的耗尽Active不能成为“唯一身份”证据。总计划为53组/220次Apply/7个边界安排；phase 1当时全NOT_RUN。phase 2基线及四项mutation的同一个独立runner已实际消费两个JSON；两份预期字节未修改。

### 手工状态转换摘要

| 输入轨迹 | 固定预期 |
| --- | --- |
| 首次A Active → 同快照/反序/重复 | A1、epoch1、catalog1不变；无delta |
| A Active → Removed/Disabled/Disconnected/Excluded/Unsupported → 重复 → Active | 首次撤销A1及binding，catalog2；重复不增长；恢复A2、epoch1、catalog3，不自动接管Fixed旧A1 |
| A仅usage增长 | 同A1/epoch；catalog+1，仅mappingdelta=A1；重复stage无delta |
| A撤销revision和usage同时增长 | 撤销旧A1、新A2/epoch1；mappingdelta空，按v1澄清；不表示已初始化映射 |
| A强serial → 加同serial B → 加第三只C → 去C → 去B | A epoch1→2→2→2→3；B/C初入epoch1；始终全组Ambiguous/session identity；去C只撤销C，不重失效A/B；去B同时invalidates A1/B1且恢复A强confidence |
| 三只同时冲突的6种输入排列 | 都gen1/epoch1/catalog1，全A/B/C invalidated；来源输出ordinal排序，无“先到者”例外 |
| 缺model证据但同非空SerialKey | SessionOnly也参加冲突；进入/解除同SourceId的epoch增长，不能只扫描ReportedSerial |
| 同型号无serial A/B | ModelOnly但两个session DeviceIdentity，撤销A不动B epoch，不生成ProfileKey/复制校准 |
| 同raw serial不同kind或serial大小写差异 | 按既有完整SerialKey ordinal判断；不同key不混组 |
| 任一坏观察/null列表/null项/重复ID/token/倒退revision | 全批Rejected，清当前、撤销全部旧active及binding；重复Rejected在空态不增长；恢复用新generation，保留token/revision tombstone |
| generation已经Int32.Max，需要新代际 | 该槽AppliedWithErrors/GenerationExhausted；旧ID不复用，其他槽继续；不是全批结构性Reject |
| 耗尽槽仍有结构有效Active且与peer同SerialKey | 槽不发布ID但仍计collision；peer继续/进入Ambiguous/session；首次Ambiguous epoch1，真正inactive/omission后peer退出时epoch+1，按v3补充 |
| epoch溢出，旧catalog1且非空 | Faulted清空、catalog2、旧active撤销一次；之后永远同空snapshot/同revision/空delta，按v2澄清 |
| catalog已Int64.Max，相同快照 → 有效变化 | 相同快照不Fault；变化Faulted并清空，catalog保持Max；永不wrap，后续不能重激活 |

预期Sources及delta在准备阶段就ordinal排序、去重；Int64以十进制字符串编码，避免JSON数字精度损失。新的registry错误枚举拼写未在提案中定义，因此结构性错误先锁“非空固定错误码、无输入内容、重复诊断稳定”，phase 2核正式枚举；不擅自发明错误名改变契约。GenerationExhausted语义已有明确冻结。

## 2. phase 1测试计划与变更门禁（真实执行结果见第4节）

1. 根明确source freeze后，才阅读完整新实现及公开API，核SHA与旧resolver/frame/router/touch守卫。此时不得改手工轨迹去迎合实现；真正契约缺口先报根，必要时另加合同版本及预期，不回写已冻结v1。
2. 编写 `InputIdentityRegistryIndependentTests.cs`，直接反序列化上述JSON并逐步Apply/对照字面Snapshot和delta。新增适配代码只连接冻结的API与DTO；不另做一个和作者相似的registry算法来产生期待值。所有步骤都检查Current与ApplyResult snapshot、状态、revision、全来源字段与排序/去重。
3. 额外结构断言：保留旧snapshot再Apply不得改它；输入集合Apply后改变不影响Current；get-only DTO/只读集合不可写穿；上一调用delta不污染下一调用；使用Console捕获检查错误不泄露合成serial/display/version/capabilities。边界只允许反射改私有计数，无生产测试后门。
4. 用既有Unity安装携带的Roslyn/dotnet编译工具与Mono运行**纯控制台**测试，严格`warnaserror+`；不打开Unity Editor/原生AeroSim，不轮询设备、不访问实际存档。编译驱动和源码SHA单独记录，编译失败不算测试通过。
5. 原样resolver合同回归另核，不借候选08 native计数给新registry签字。新套件为三个隔离mutation保持字节不变：只标后插入者Ambiguous、弱身份使用ModelKey、撤销后复用generation；根随后要求第四项恢复“仅发布条目计serialCounts”的旧逻辑，由v3向量检出。每个变异源码必须先严格编译成功，再由未改套件非零退出并有具体断言失败；编译错误不算mutation检出。
6. phase 2真实结果写[紧凑记录](../verification/v125-qa/registry/registry-independent-results.json)，保留source/probe/vector SHA、命令、退出码、失败及边界。根负责限定stage、提交/push及远端SHA；本成员不做Git写入。

## 3. phase 1历史检查

当时已实际只读JSON解析、计划计数、case ID唯一性、Int64字符串、预期delta排序去重，以及7个合同/旧源码SHA核对；主49轨迹指纹未因新增v3而改变。这只验证测试数据可读取与指纹一致，**不是registry实现执行**。phase 1当时C#驱动尚未完成，严格编译、实现测试、mutation均NOT_RUN，待根source freeze通知；这段历史不能替代第4节结果。

## 4. phase 2实际执行与冻结证据

完整读 [source freeze](../verification/v125-qa/registry-source-freeze.json)后核对14个冻结文件，执行前及收尾均14/14匹配。freeze SHA为`873c61e578789b15370eb6dada1abc6a882182f1b26c2b7d9b6d36bc8d33fc5c`；被测registry SHA为`2536d814d4430b436ad322b602943b8a88de7ff294203034c91cbc960a730ba2`。root作者套件回归属于root的独立来源，本成员未运行作者套件，亦不将作者计数混入本套件。

[独立驱动](../verification/v125-qa/registry/InputIdentityRegistryIndependentTests.cs)逐步骤读取冻结字面预期，而非运行另一套registry算法生成预期。驱动SHA为`c924cbdbb3d161a54e8105b5bbcef5773ed09d6620f7c56cd0d20dc6978ef775`；[runner](../verification/v125-qa/registry/build-registry-independent-tests.sh) SHA为`40e0fca7a69e0fe76dade2dfb014c119c54b0c1b8d1061d7488253f2c9332886`；v3 JSON SHA为`9eaa91f9ff71eb57aa2320f57fd08b785349b3bd4791677aedf13d916c3858de`。四次mutation前后这四份测试/预期文件指纹不变。

实际命令：

```sh
bash verification/v125-qa/registry/build-registry-independent-tests.sh
bash verification/v125-qa/identity/build-identity-independent-tests.sh
```

使用既有Unity 6000.3.25f1安装的Roslyn/dotnet与Mono编译/运行控制台，不启动Editor或Player。严格参数为`-nologo -noconfig -nostdlib+ -langversion:latest -warn:4 -warnaserror+ -target:exe`。runner的`bash -n`检查通过。

| 实际独立运行 | 严格编译 | 检查/失败 | 进程退出 | 范围 |
| --- | --- | --- | --- | --- |
| 新registry原始源码 | 成功 | 15,889 / 0 | 0 | 53轨迹、220次Apply；包括v3的4轨迹/21次Apply、7个反射边界安排、9个非法constructor |
| 原样既有resolver回归 | 成功 | 310,390 / 0 | 0 | 3,300 directed、6,000 random、46 invalid、4 culture，seed1251011；单独计数，不与registry相加 |

新registry基线EXE位于`/private/tmp/aerosim-registry-independent.UbexsF/InputIdentityRegistryIndependentTests.exe`，SHA为`e1412460c694a68a9b47aa92577710fd5ca9e0c93fa95703f90b4ab26b59b353`。结果来自本成员实际工具控制台；未冒称另有既存基线raw日志文件。resolver EXE SHA为`852de56162d467c75cc31357e5c8f7d781531d39e7e0eb67277f71d67c182c6e`。

每个变异仅写入隔离`/private/tmp/aerosim-registry-mutations.TALK5B/`源码，不改生产文件；命令为`bash verification/v125-qa/registry/build-registry-independent-tests.sh <隔离源码.cs>`。四次均真实严格编译成功，再由**未改独立套件**非零退出。EXE、源码及控制台副本SHA、完整命令和首次失败项见[紧凑结果](../verification/v125-qa/registry/registry-independent-results.json)。

| 变异 | 检查/失败 | 退出 | 首次具体检出 |
| --- | --- | --- | --- |
| 冲突仅后成员Ambiguous，首成员保持强身份 | 15,878 / 195 | 1 | `collision_enter_third_exit_epoch_barrier/1:entry.epoch`、confidence |
| 弱身份改用ModelKey | 15,889 / 19 | 1 | `omission_empty_restore_tombstone/0:identity.device-exact` |
| 撤销后复用generation | 15,879 / 118 | 1 | `revoke_disabled/3:entry.generation`、SourceId |
| serialCounts仅计算成功发布ID的条目 | 15,881 / 56 | 1 | `v3_existing_ambiguous_peer_stays_ambiguous_when_A_exhausts/2:entry.epoch`、confidence |

各次都实际消费全部53轨迹/220次Apply（含v3 4/21）、7个边界安排和9个constructor；检查总数因条件式只读/DTO检查不同可略有不同。第四项由冻结v3向量的实际断言检出，不是编译失败或仅书面解释。

收尾 [只读守卫](../verification/v125-qa/registry-wave-readonly-guard.json)39/39匹配，含23/23原始概念图及16份旧源码/目录/稿件文件；候选08稳定副本Runtime SHA仍为`0aec831cff961caafa77a38f90bdf0335ff4026b817ccde9fd71b8ed21e99f9f`，这里只核字节、未重复原生QA。未修改源码、作者测试、合同、预期JSON、参考图或用户批准状态。

另做整合前只读文档预检查：`node verification/v125-qa/prd-review/prd-document-check.mjs --doc docs/V125_REGISTRY_QA.md`默认同时包含PRD，583项/1失败、247次本地链接、17个待stage依赖。唯一失败为画廊冻结稿5份与新Air 2S加入后的manifest6份暂不匹配，已交根负责人整合；无缺失链接、绝对链接或原图哈希失败。此时没有执行`--require-tracked`，不是最终发布检查通过；本QA不改别线的画廊或Git。

## 5. 仍未通过的交付边界

本波identity-only，不是完整raw source registry、mapping验证、router/Fixed仲裁、校准、应用接线或Gate A关闭。候选08二进制、23张原始图、真实控制器/iOS/iPad验证及逐机型pending_user不因本波纯逻辑测试通过而获得验收；不宣称1.25完成。

[GitHub仓库](https://github.com/yijunchai2026-gif/AeroSim) / [1.25工作分支](https://github.com/yijunchai2026-gif/AeroSim/tree/codex/v125-product-lines)为现有入口，不是本波已提交/上传的证明。最终真实commit/push由根统一提供。
