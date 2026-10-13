# AeroSim：纯 raw mapping 子集作者实现交接

2026-10-12，America/Los_Angeles；交接时钟读取为 `2026-10-13 00:41:29 UTC`，不是精确测试运行时刻。状态 **AUTHOR_PURE_MAPPING_TESTS_PASSED / 待根 source freeze 与独立 QA**。本批仅实现已冻结的公共输入纯子集，不决定 incoming PRD v4 与本线程1.25三端/23原图目标冲突的优先级。根尚在等待用户决定总体范围；本文不能作为改判或正式接线授权。

## 1. 冻结依据与实际五文件

完整读取[提案](V125_RAW_MAPPING_CONTRACT_PROPOSAL.md)、[contract freeze](../verification/v125-qa/raw-mapping/contract-freeze.json)及[addendum v1](../verification/v125-qa/raw-mapping/contract-addendum-v1.json)，实际 SHA 分别为 `b451456087f961fc201adb2fa11500a2c54e8004893618a257ba5aab382a3d62`、`5cb1e381594dabc278f4f7cc1b2c1fbea9183fb1662cc9cd2b442feb12c9fffd`、`e430504527b08cf7d9df0237684ab4a2016df814c9f8fc12c6eee30cd48d4428`，读取与结束复核一致。没有修改它们、root guard 或独立预期/driver。

只新增 freeze.allowedAuthorFiles：

- [RawInputMapping.cs](../AeroSim/Assets/AeroSim/Runtime/Input/RawInputMapping.cs)及[meta](../AeroSim/Assets/AeroSim/Runtime/Input/RawInputMapping.cs.meta)：357行System-only生产源，不引用Unity或InputSystem。meta GUID=`de8cd83fe1df4b9683f1c7bc3352dc19`；本次 `rg` 在 Assets meta 里仅命中此一项。
- [作者测试](../verification/v125-qa/raw-mapping/RawInputMappingDeveloperTests.cs)：648行合成metadata、字面预期、独立编码与定向/随机检查。
- [作者runner](../verification/v125-qa/raw-mapping/build-raw-mapping-developer-tests.sh)：23行，使用现有 Roslyn/Mono 4.8-api，只编译FlightControlFrame、InputDeviceIdentity、本源和作者测试，产物位于唯一 `/private/tmp` 目录；可通过 `bash` 运行，不要求更改安装或Unity项目设置。
- 本文。本文不自引用散列，根另捕获实际字节。

## 2. 实现范围与不能升级的结果

仅两个公开静态函数：ValidateHardware/GetBuiltIn。AxisMapping、RawControlDescriptor、RawAxisPreprocess、RawMappingValidationResult、RawMappingDiagnostic均sealed/get-only；结果 Controls/Diagnostics 是防御性只读副本，不暴露可变输入数组或List。输入DTO允许保留坏值供显式验证，不在构造时自动修正。

Joystick结构合法返回 **JoystickCandidate**，不是UserValidated、SourceAvailable或用户live逐轴核对证明；初始null mapping返回Missing。Gamepad只核固定leftStick.y/x、rightStick.y/x四canonical path；缺轴返回Rejected，任何supplied mapping都返回MappingNotEditable。Keyboard/Touch只返回既定逻辑builtin指纹，不读取键盘/触点或创建硬件目录。

路径为ordinal exact、device-relative，禁止模糊选择器/通配/非法分段/空白/控制字符/非法UTF8，不做大小写、Unicode、URL、前缀或suffix转换。整目录重复path拒绝，即使未选中；选中Button/Other拒绝，不能按index夹取末轴。阶段短路、全部诊断聚合、Field、排序与去重按addendum执行。

所选四轴metadata独立检查type/format/size/preprocess；七个float包括关闭字段全部有限。clamp非None且已知时只在两有限端点上比较；normalize的min/max与zero/max各自仅在其有限操作数上比较，另一坏值不隐藏可判断的错误。normalizeZero低于/等于min允许、at/above max拒绝是冻结的保守metadata协议，不声称Unity同样拒绝它。没有执行preprocess或推导物理raw量程，没有`.5/.1`校准阈值。

硬件63字段严格为mapping-v1/layout/rawConvention → 四path → T/Y/P/R各14metadata字段。复用既存identity internal长度前缀SHA helper；Uint32用invariant decimal、finite Single用invariant R、bool=0/1、±0统一为0。所有preprocess字段入hash，processor stack/source身份/revision不入hash。builtin仍分别使用Gate A既定四字段。

本源没有sample/raw值、缓存、MappingRevision状态、SetMapping、filter、校准、sidecar、identity代际/碰撞算法、设备轮询或source绑定。没有改旧FlightInput、UI、PRD、progress、图稿、正式资产或应用接线。root只读核得1.16 SDK证据已保留在addendum；作者没有下载/克隆或复制SDK、没有新增Unity薄层，真实enum转换仍后继显式核对。

## 3. 实际作者验证与失败记录

实际命令：`bash verification/v125-qa/raw-mapping/build-raw-mapping-developer-tests.sh`，以Roslyn `-warn:4 -warnaserror+ -noconfig -nostdlib+`及已有Mono执行。无Unity引用DLL、无Unity/native进程或真实控制器/用户存档访问。

第一次runner在 `/private/tmp/aerosim-raw-mapping-developer.Fpi9ao` 严格编译exit1：作者测试的后续局部变量zero与先前foreach变量zero有C#作用域冲突，报CS0136两处；没有执行测试，不计PASS。仅把后续变量改为unsignedZero；未改变生产源、预期hash或断言。

修后第二次完整runner实际 **22,576 checks / 0 failures / exit0**，1,200随机case、seed1251012。产物目录 `/private/tmp/aerosim-raw-mapping-developer.8HEwAr`，exe SHA `97aed90124407f411c84e5a8000dd6617a5029a987896e17b4b67ead91f95650`。runner末行实际返回该目录；不是独立QA或应用二进制。随后 `bash -n` runner exit0。

覆盖状态/失败阶段矩阵；精确path与非法Unicode；所有重复/错误目录排列；Button/Other/未知kind；缺type/format/size/preprocess与独立错误收集；四轴×七float×三种NaN/Infinity×启用/关闭；clamp、normalize、scale边界、小量程与±0；每轴每指纹字段差异、未选项和排列不变；fr-FR/tr-TR/ar-SA/zh-CN等文化；readonly反射和实际集合写入拒绝；1,200固定种子有限Single bit-pattern与十种单错案例。没有重复既存identity/registry/router/touch套件来增加此计数，也没有删旧失败。

四个字面hash预期由Ruby独立UTF8字节长度前缀/Digest计算后写入作者测试，未取模块输出回填；另有作者测试中的独立framing/SHA参考编码进行对照。最初Ruby因本机locale为US-ASCII拒绝非ASCII脚本文字，exit1；显式 `-KU` 后成功得到预期，无文件写入。字面值如下：

| fixture | 预先计算的SHA-256 |
| --- | --- |
| Keyboard builtin | `54de1323081596043fac0089d2d021ed51d91512c64f5e4d6360a49d4b0b5051` |
| Touch builtin | `6587ee3d86628693f0d9d924451b86f5230c71828068f589341200f989ca69b6` |
| SyntheticStick四轴 | `621a4bb6c1a96203a841e4db2a9bfdafa9da7988c3c15364295c815b888e9f0a` |
| 非ASCII layout/path/type与UInt32最大值 | `467475a3add4fc269a5a5493d4f67f8d7f79e40f0eeb1ebb83f251ea90840598` |

## 4. 结束指纹与交出后停写

| 作者文件 | SHA-256 |
| --- | --- |
| Runtime/Input/RawInputMapping.cs | `4e89d44094262f72dd11edfb7e45aa5fa0d76e36955eafdccbac9f9c4076ae9c` |
| Runtime/Input/RawInputMapping.cs.meta | `2a43c207aa40f20e980b7377fa1eefc49758af77f43a9ec5d205364e45cd2751` |
| verification/v125-qa/raw-mapping/RawInputMappingDeveloperTests.cs | `080ecec325d147f3ae4ec8ac74d14ffc91ab7dc2f79253f365f4158eb000d5e6` |
| verification/v125-qa/raw-mapping/build-raw-mapping-developer-tests.sh | `a46b79c023270e74fb7b7f684d1ee41b573330314753f4c9df0829e9844e824d` |

Runtime/...位于AeroSim/Assets/AeroSim。结束只读复核四个既存源仍为：FlightControlFrame=`aedd8c7593bf61f61cfbd7c2fcc082570828f0f8d486ec39b2d8a0c232fd773d`；InputDeviceIdentity=`88a215d7431824c3c3062d377c4d2e35a7bdd3af81348bf1add4aca2adca084f`；InputIdentityRegistry=`2536d814d4430b436ad322b602943b8a88de7ff294203034c91cbc960a730ba2`；incoming FlightInput=`c7a2e9da426b0a527511a645442ed4161f9754ff19d039509bd7c51539fa2a99`。提案中的progress散列只代表提案时读取，root有自己的新进度整合，作者不修改冻结提案来追写它。

交接后本成员停写，等待root全文复核/实际source freeze，独立QA才消费已锁定字面预期并跑新driver/mutation。作者零失败不代替独立QA、实际Unity processor-bypass/设备事件、正式mapping授权、缓存/校准事务、三端输入或移动/物理设备验收。当前只能交 **作者纯mapping验证通过**；总体Gate A/产品交付未关闭，没有新试用应用或用户批准。

本成员未做Git操作或push，整合/验证/提交与上传由根负责人统一负责。[GitHub仓库](https://github.com/yijunchai2026-gif/AeroSim)是既存入口，不是本五文件已上传证明。
