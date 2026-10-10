# 1.25 候选07/07b/07c独立 QA

07首次构建失败；重新冻结的07b与07c成功构建，原生应用仍为 **1.0.0开发候选**。动态弹层字体局部门禁通过，07c独立v3真实2209/0/0 runtime；v1/v2测试时序失败保留。07c恢复旧Space1440回放页的77条字体/几何evidence；新增回放v2也以同探针复现07b的38项字体失败、确认07c零失败。Space1440×900的3项是既存窄桌面缺口，仍未解决；不是1.25最终验收。

## 构建与边界

- 初始[candidate07冻结](../verification/v125-qa/candidate07-source-freeze.json)保留。独立实读 `/private/tmp/aerosim-v1-candidate07-build.log` 第222/717行提示InputRouter.cs.meta无有效GUID，702/708/713行报CS0029（ExecuteLater返回void，却赋给IVisualElementScheduledItem）。
- 根修正API用法与无引用33hex metadata，再[冻结07b](../verification/v125-qa/candidate07b-source-freeze.json)。独立复核全部5个源码SHA匹配；`/private/tmp/aerosim-v1-candidate07b-build.log:949` 为 `Build Finished, Result: Success.`。
- 被测原app `/private/tmp/aerosim-v1-build.6x0OSc/AeroSim.app` 与根保存的稳定副本 `AeroSim/Builds/macOS-V1-Candidate-20261010-07b/AeroSim.app` Runtime DLL SHA为 `6f487a915564c1a5ab62e4ca57e9c5345f858e1526328984dd7537191537ea96`。本QA没有启动Unity或原生应用。
- 根为新树USS采样增加实际两帧屏障，再[冻结07c](../verification/v125-qa/candidate07c-source-freeze.json)，不覆盖07b。独立核07c五个源码SHA全部匹配；`/private/tmp/aerosim-v1-candidate07c-build.log:926`成功，稳定 `AeroSim/Builds/macOS-V1-Candidate-20261010-07c/AeroSim.app` Runtime SHA为`caf2331190601b9bddab0ccfbc312fab894baa0c20a9d58a101a208e15d5e061`。07b源码匹配指当时冻结，不把当前07c Responsive误称07b源码。
- 当前metadata静态核查：AeroSim/Assets/AeroSim下86个.meta，另有Assets/AeroSim.meta，合计87；32小写hex GUID有效且唯一。不抹去首次失败，不代替Unity实际导入。
- 旧Pause/Space源码、合同、历史报告未改，散列一致；新probe只注入临时复制app，真实档案与机型确认不动。

## 动态字体原生对照

同一[Typography v1合同](../verification/v125-qa/dynamic-modal-typography-contract.md)：UI/HUD100，844×390逻辑视口，实际resize1280×720后恢复。Pointer进入Confirm/Objective/Telemetry；16个非空TextElement独立Text100基线，覆盖4档、重apply/重开/resize、live100→200→100及组件disable→enable。

| 原生候选 | checks | FAIL | 字体倍率FAIL | 快速事件FAIL | runtime |
|---|---:|---:|---:|---:|---:|
| 06旧binary | 2185 | 208 | 160 | 48 | 0 |
| 07b | 2185 | 48 | 0 | 48 | 0 |

原JSON在 `verification/v1-qa/results/candidate-{06,07b}/dynamic-fonts-844x390/dynamic-modal-typography-report.json`，SHA见[紧凑证据](../verification/v125-qa/candidate07-summary.json)。07b真实512字体断言、272滚动linebox断言、24resize断言、105record/105profile不变断言均零失败，整体仍FAIL。

48个快速失败为opens/target/Pick/cancel各12。独立trace显示quick0 Pick正确，但release即时检查尚无modal；quick1的before-pointer才显示上次modal已打开，被该modal拦截。两个候选同模式，支持probe早于UI Toolkit事件分派检查状态的时序缺陷，不归为160个产品字体失败。

根授权[独立v2](../verification/v125-qa/dynamic-modal-typography-contract-v2.md)：仅快速release后补EndOfFrame及一帧冲刷，不加0.16秒静置、不删断言、不改容差/基线、不Invoke callback。真实复跑06为2209/160FAIL（全部字体）；07b为2209/4FAIL（字体0FAIL），runtime均0。v1证据保留。

07b四项全部源于Telemetry125 quick0 close：首次ScrollTo后，字体计划布局使按钮y398…442超出viewport20…371；下一次同ScrollTo则Pick命中并正常关闭。正常初始/重开/after-quick的字体/linebox均通过，支持首次滚动偏移在延迟布局后过期的测试时序。新增[独立v3合同](../verification/v125-qa/dynamic-modal-typography-contract-v3.md)只在首轮settle后实际按钮不contained时最多再一次同ScrollTo+settle，记录before/after，仍执行原完整包含/Pick断言。20种Check规则和顺序与v1/v2一致；各版源码/合同/失败报告保留，[v3冻结](../verification/v125-qa/dynamic-modal-typography-freeze-v3.json)不回写。

v3真实双候选复跑：**06为2209/160字体FAIL/0runtime，07b为2209/0FAIL/0runtime（PASS_AUTOMATED_ONLY）**。07b两个Telemetry125 quick close各一次有界重滚动，before y398→after y327，height44、viewport20…371、字体18.75不变；随后原包含/Pick/取消全部通过。各24张原PNG，完整JSON与SHA在紧凑证据中。这关闭此局部自动门禁，不覆盖下面Replay/1440/真机/23图缺口。

同一冻结v3在07c真实复跑 **2209/0FAIL/0runtime**，原始 `candidate-07c/dynamic-fonts-v3-844x390/`有24张PNG。本次并未修改探针、合同、倍率或容差以获得通过。

## 原PNG局部复核（非23原图验收）

06/07b的v1目录各24张真实PNG，不因.gitignore搜索遗漏而假称无图。独立QA已原分辨率查看旧06 Confirm200、新07b Confirm200/Objective200/Telemetry200：标题22→44、说明14→28、按钮15→30确实放大；Objective换行且返回按钮完整。Telemetry200首屏只容纳标题、说明、Altitude，其余值及返回按钮须滚动；逐项ScrollTo通过不等于全部同屏。

根精选拷贝与原PNG散列一致：[06修复前](../assets/ui-v125/candidate-20261010-07b/confirm-text200-before06.png)、[07b修复后](../assets/ui-v125/candidate-20261010-07b/confirm-text200-after07b.png)、[07b Telemetry首屏](../assets/ui-v125/candidate-20261010-07b/telemetry-text200-after07b.png)。不是23概念图逐页一致、完整UI/HUD缩放矩阵、iOS真机、安全区或物理摇杆验收。

另外实看07b Confirm100/125/150四档以及live200→100原PNG：字体逐档增大，恢复100后弹层字体/布局与初始相符。仍为局部视觉复核，不扩为全页验收。

07c另实看Confirm200、Telemetry200及新Replay200原PNG，确认放大的弹窗字体和需滚动的遥测；还对照07b/07c原Space1440 Text200回放截图。07c导航和动作字号不再统一28，标题及元信息正常放大并换行；变大后首屏不容纳全部底部控件，须滚动。这些是实际候选运行截图，不替换原概念图，不证明全页所有字体或全缩放视觉一致。

根新增精选[07c Confirm200](../assets/ui-v125/candidate-20261010-07c/confirm-text200-after07c.png)与[07c Replay1440 Text200](../assets/ui-v125/candidate-20261010-07c/replay-text200-1440.png)独立核与raw字节一致，SHA分别`9e4884bbb83d9d98fa3704e48adc14ec2906140f1ee988e08416ce59c6864604`、`fdf3810bf64d03bfe456030f4fa948000fecfa1dfc249f4de4e69a98a793d568`。

## Replay字体基准风险与追加门禁（非全页验收）

[桌面只读诊断](V125_DESKTOP1440_TRIAGE.md)发现两份1440报告中77条**PASS evidence**不同，位于Aerial-Replay Text200：06 Home34、Play32、disclaimer24，而07b均为28。此前“相同”只指3个FAIL的rule/evidence逐字一致，不是全部1911个evidence相同。旧Space合同只验linebox，不能拦截此类字号基准漂移；动态弹窗512字体PASS也不能覆盖整页Replay。

新增独立[Replay固定USS v1合同](../verification/v125-qa/replay-typography-contract.md)，以Product.uss:303 nav17、FlightDesignV1.uss:11 action子Label16、:129 disclaimer12为冻结来源，不从错误binaryText100重采基线。精确6个Nav、12个Replay action子Label、1个disclaimer；Text四档、真实Records→Replay Pointer入口、Home重开、重复apply3、linebox和隔离profile/session不变。[v1冻结](../verification/v125-qa/replay-typography-freeze.json)不动，06/07b/07c真实各 **778/0FAIL/0runtime，4张PNG**。常规Pointer进入三版都通过，未复现原Space coroutine所在帧的漂移；不能据此删除77条旧差异或宣称07b整页正确。

因此追加[Replay v2合同](../verification/v125-qa/replay-typography-contract-v2.md)，保留v1全部断言，再按旧Space的前置FPV Tuning→Aerial Settings→Controller→Professional Home及public BeginReplay coroutine进入真实回放树，固定token、计数、linebox、reapply与Home重开数据不变。[v2冻结](../verification/v125-qa/replay-typography-freeze-v2.json)三版同探针真实运行完成，不改v1、不调用私有callback或强制字体。追加public路径不是Pointer/真实设备输入测试。

| Replay v2候选 | checks | FAIL | 字体FAIL | runtime | 原PNG |
|---|---:|---:|---:|---:|---:|
| 06 | 1454 | 0 | 0 | 0 | 8 |
| 07b | 1454 | 38 | 38 | 0 | 8 |
| 07c | 1454 | 0 | 0 | 0 | 8 |

07b38项全部在Text150-SpaceCoroutine-Replay与reapply3，各19个目标（6nav+12action+1disclaimer）实际均21px，独立期望分别25.5/24/18px；返回Home重开时已恢复，符合初始新树采样阶段的漂移。原JSON在 `candidate-{06,07b,07c}/replay-typography-v2-1920x1080/replay-typography-v2-report.json`；每版528个字体断言、所有profile/record不变断言，v1原777规则/stage前缀和末尾completion保持完整。旧07b失败不删除。独立实看07b/07c Text150-SpaceCoroutine原PNG，标题与导航/动作层级恢复可见；不是全页所有token验收。

独立对照新07c原Space1440：77条旧差异全部逐字恢复06，Home34、Play32、disclaimer24；本次全部1911个check的rule/evidence/pass与06一致。JSON `candidate-07c/space-submit-v3-1440x900/space-submit-v3-report.json` SHA见紧凑证据。旧合同的3FAIL也照样保留，token恢复不等于窄桌面门禁全通过。

局部看图另记录未关闭视觉问题：回放遥测显示`26 ft ft`与`0.0 mph mph`重复单位，原自动合同未覆盖。本批QA不改生产代码，不将字体token通过扩为整页内容/UI验收。

## 原合同回归与四组审核稿

- smoke `verification/v125-qa/candidate07b-native-smoke.txt`：66 PASS/0 FAIL；Pause原合同 `candidate-07b/pause-telemetry-844x390/`：74/0/0 runtime。
- 07c原样smoke `verification/v125-qa/candidate07c-native-smoke.txt`真实66/0；Pause844真实74/0/0；Space1920真实1911/0/0，24闭合Foldout仍UNRENDERED不计PASS。Space1440真实1911/3/0，未修掉下面3项。
- Space1440×900原合同：**1911/3 FAIL/0 runtime、24 UNRENDERED**。Tab实际聚焦Objective而非Pause；Professional Home Text100标题content198×88，required198×218；说明198×44，required198×59。真实JSON `candidate-07b/space-submit-v3-1440x900/space-submit-v3-report.json` 保留。
- 同视口对照真实完成：07b1920×1080为1911/0/0 runtime；061440×900为1911/3/0，三项规则与evidence和07b1440逐字一致。故这3项是既存窄桌面覆盖缺口，不是本批引入；不能因此删掉缺口。24闭合Foldout项UNRENDERED不计PASS。
- 原25审核工具测试不改（前缀SHA与06历史证据一致），追加3门禁，独立 **28 PASS/0 FAIL/0 SKIP**。临时副本生成：132主+9配色=141；4稿精确映射legacy→dji-phantom-1、current→dji-mini-4-pro、inspire→dji-inspire-1、mavic→dji-mavic-pro，每组1当前稿，PNG字节SHA/revision/官方URL域/pending核对；其余137项缺图禁确认，0真实批准。QA未独立浏览全部来源内容，不能以URL域校验代替目录完整性证明。
- 新CDN只放精确HTTPS www1.djicdn.com/www4.djicdn.com，生成器/UI拒绝HTTP/FTP、伪域与www2/www3。实际HTML和隔离生成4draft IDs一致。浏览器缩放/图片加载/导出交互仍未实测（file://安全阻断，不绕过）；VM不替代浏览器及工程最终复算PNG。
- 机型全部pending_user；位图不是最终3D网格/材质。0.4保留，最终验收只能由用户完成。
- 本QA不做Git写操作。根报告GitHub Desktop连续超时、私有仓库连接器404且Git无认证；本批push尚未证明，不能称已上传。构建、probe与目录测试通过不豁免提交/推送和远端SHA门禁。
