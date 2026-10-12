# AeroSim1.25 候选08独立电脑QA

本轮只补既存1440三项缺口与Replay重复单位语义，不扩成23图或三端验收。根唯一构建/原生运行；本QA没有启动Unity/native、修改生产源码/旧probe/旧证据或Git。

## 独立冻结与当前状态

- [补充合同v1](../verification/v125-qa/desktop08/desktop08-contract-v1.md)、[source/script冻结v1](../verification/v125-qa/desktop08/desktop08-freeze-v1.json)在2026-10-10 16:31:38 UTC冻结。Roslyn对未注入07c Managed实际静态编译exit0，Bash语法PASS；这不是运行通过。
- 未注入07c稳定副本Runtime DLL独立重核为`caf2331190601b9bddab0ccfbc312fab894baa0c20a9d58a101a208e15d5e061`；旧Space/Pause/dynamic/Replay v2工具11个指纹重核并固定在新freeze，不改旧文件。
- 新补充使用实际macOS逻辑1440×900/1920×1080、UI/HUD100、Text100/200。首Tab→Space必须进入含Flight paused/Resume的唯一Pause overlay而非Objective；真实Tab/Shift-Tab modal包含及Objective键盘可达，关闭后空Space/Arrow→Space不误动作。
- Professional Home保留36/16 USS基准×Text倍率、0.25px字体容差和原3px行框容差；实际最终宽度测文字并核标题/副标题/说明/CTA矩形不相交。Replay只从实际三行chart UI树核Imperial/Metric数字及单独单位，覆盖Battery；不是源码grep通过。
- 根实际候选08Unity成功构建；独立读日志`/private/tmp/aerosim-v1-candidate08-build.log:3724`为`Build Finished, Result: Success.`，10个source freeze SHA逐一匹配，未注入应用`/private/tmp/aerosim-v1-build.z7j0iF/AeroSim.app` Runtime SHA为`0aec831cff961caafa77a38f90bdf0335ff4026b817ccde9fd71b8ed21e99f9f`。应用仍1.0.0开发候选，不改称已完成1.25。
- 2026-10-10 22:34 UTC独立收尾：五个业务diff仅为hero可读宽度/响应式、Pause首Tab顺序和Replay拆分单位；10个生产source、15个probe/合同指纹、12份新旧raw JSON及6张精选PNG全部重核一致。原35个补充失败仍按同序rule/stage在08关闭，旧失败报告未改。
- 根保存的稳定入口`AeroSim/Builds/macOS-V1-Candidate-20261010-08/AeroSim.app`已独立核147个文件的相对路径、长度与SHA，和被测temp原app全树一致，无symlink；Runtime仍为上述SHA、Plist仍1.0.0。可执行文件SHA为`15c6787c334c7eaeb7835c92e38185cfecc3fc0e609fdf30ef09194a1ac7ce20`。这里只证实原样保存，不声称独立重新启动稳定副本；Builds不提交GitHub。

## 相同冻结v1的实际07c→08对照

| 实际候选/逻辑视口 | checks | FAIL | runtime | 原PNG | 判定 |
| --- | ---: | ---: | ---: | ---: | --- |
| 07c / 1440×900 | 215 | 20 | 0 | 8 | FAIL，旧缺陷真实复现 |
| 07c / 1920×1080 | 220 | 15 | 0 | 8 | FAIL，旧缺陷真实复现 |
| 08 / 1440×900 | 215 | 0 | 0 | 8 | PASS_AUTOMATED_ONLY |
| 08 / 1920×1080 | 220 | 0 | 0 | 8 | PASS_AUTOMATED_ONLY |

全部UI/HUD100、Text100/200、panelScale1；无fatal。报告SHA和全部失败分类在[紧凑证据](../verification/v125-qa/desktop08/desktop08-summary.json)。相同视口两版所有rule/stage顺序逐项一致，原35个补充FAIL在08全转PASS，探针不变、不需v2、不削断言或改容差。435项只指此局部门禁。

- 07c1440两字号首Tab都是Objective，Space打开Session objective/Back to Flight，虽paused仍有各3FAIL；1920Text100首Pause正确，Text200误入Objective有3FAIL。08两视口/两字号共4次首Tab均`action-pause`，一次Space实际1个Flight paused overlay、Resume焦点且无Objective标题，实际模态路径已明确，不仅是Paused条件。
- 08各视口40个Tab/Shift-Tab包含、12个关闭后空Space/Arrow→Space不误动作断言通过。1440两字号/1920Text200直接HUD Objective真实Tab可达；1920Text100 HUD隐藏时Pause→Tab Session Objective→Enter实际打开嵌套Objective，关闭只移顶层，恢复Pause后Escape回运行。未用Focus/Blur或私有callback代替业务路径。
- 07c1440Text100 title198×88而required198×218、description198×44而required198×59各FAIL。08同场景title356×88/required356×88、description356×44/required356×39；36/16基准未改。Text200为72/32，1440标题1055×175/required175、说明1055×78/required78；1920标题1519×88/required88、说明1519×44/required39。原3px linebox容差及0.25px字体容差不变。
- 两版两制式/两字号的Replay各12个数字value语义断言：07c三行均含suffix，真实`39 ft`旁又`ft`、`8.9 mph`旁又`mph`、`92%`旁又`%`；Metric同样重复。08 actual UI tree三行value/单位精确为Imperial `39|ft / 8.9|mph / 92|%`、Metric `12|m / 14.4|km/h / 92|%`，两个字号全部通过。不是源码grep通过，也不是Pointer进入路径声明。

独立实看07c/08 1440Home100原PNG：旧标题字形溢出叠到subtitle/说明，新稿不再叠字。旧worldBound彼此不相交仍PASS，说明**矩形不相交单独不足**；必须结合未改的行框失败与真实截图，不用新几何计数掩盖旧溢出。另实看08首Pause、Imperial Replay100，以及两视口Home200。Text200的1440说明延伸到首屏contentViewport下方、CTA未同屏；1920全宽hero使status下移。补充probe未实际滚动/点击这些Home与Replay屏外控件，不能宣称全部同屏、滚动交互或点击已通过。Pause截图底部菜单同样需滚动。

原07c 1440 Space三FAIL/24UNRENDERED和07b Replay38字体FAIL保留原文件与散列，不被08新结果覆盖。

## 原冻结合同08回归：已运行与未运行分开

| 实际08保护 / 逻辑视口 | checks / FAIL / runtime | 单独呈现注记 | 当前独立复核 |
| --- | --- | --- | --- |
| 原Space v3 / 1440×900 | 1911 / 0 / 0 | 24 UNRENDERED，不计PASS | PASS_AUTOMATED_ONLY |
| 原Space v3 / 1920×1080 | 1911 / 0 / 0 | 24 UNRENDERED，不计PASS | PASS_AUTOMATED_ONLY |
| 原Space v3 / 1280×720 | 1911 / 0 / 0 | 24 UNRENDERED，不计PASS | PASS_AUTOMATED_ONLY |
| 原Pause / 844×390 | 74 / 0 / 0 | 10原PNG；12真实遥测值/12滚动可读 | PASS_AUTOMATED_ONLY |
| dynamic v3 / 844×390 | 2209 / 0 / 0 | 24原PNG；v1/v2失败保留 | PASS_AUTOMATED_ONLY |
| Replay typography v2 / 1920×1080 | 1454 / 0 / 0 | 8原PNG；固定USS token保护 | PASS_AUTOMATED_ONLY |
| 应用原生smoke | 66 / 0 / 0 | 原未注入08应用；独立profile；合成输入 | PASS_AUTOMATED_ONLY |

三份已完成Space完整JSON独立实读，实际Screen与输出尺寸一致、各1911条无false、runtime0，presentationNotes真实各24条均UNRENDERED。1280另有58原PNG，完整保留高级foldout展开、numeric/rate逐项滚动、heldArrow输入和原输入断言；不是大字Home/Replay全内容滚动验收。原Pause实际844×390、74断言无false、runtime0，2个嵌套确认/返回暂停、12真实六值及滚动可读、record/profile不变均通过；这是逻辑横屏合成Pointer/Key，不是真机。SHA及本机raw路径在紧凑证据；11个旧工具指纹仍与freeze一致，不删断言/扩容差/把关闭foldout改计通过。本收尾复核已有完整报告，不重复这些原生测试。

dynamic v3新08全文实读2209/0/0、fatal空，实际initial/final844×390，512独立倍率字体、272弹层逐项滚动行框、24实际resize及105record/105profile不变断言完整通过，包含live100→200→100、快速开关和disable→enable完成检查。Replay typography v2全文实读1454/0/0、fatal空，实际1920×1080，528固定USS字体、376行框、20真实Pointer目标、16原Space前置页面及public BeginReplay路径断言完整执行，含重apply/重开/档案不变。不是复用07c报告；probe/v1/v2历史失败与0.25px字体/3px行框容差原样。

原生smoke来源已独立找到：`/private/tmp/aerosim-candidate08-native-smoke.log`的Mono/UnitySubsystems明确指向被测temp08 app，66条PASS与[08紧凑smoke结果](../verification/v125-qa/desktop08/candidate08-native-smoke.txt)前66行逐项、逐序一致，结果末行为`Failures: 0`。原日志SHA为`6574507b3b0dd4af7c5e81381878542d1f2621c7e6168774de0d2a0f0efce65e`；结果SHA为`e20adee356fedd5bf50b8afd20ae39774545c600964579db324c19822ce3cefa`。`PreviewCapture.cs`实际将同一结果列表逐条写日志、最终写`--test-results`。实际照片路径均在独立`/private/tmp/aerosim-desktop08-smoke-profile.eKVE9e/`，六个文件及对应档案/CSV/report存在，PNG头为1280×720；rapid capture的不同文件可以有相同画面，不能把“证据身份/文件不同”说成每次像素内容都不同。

这66项包含实际合成Pointer启动、Keyboard移动/暂停、保存/Replay/export/RTH/照片证据，但Gamepad为`InputSystem.AddDevice`合成设备，Touch为输入通道赋值，不是实体手柄、触屏摇杆/iOS连接验收。日志/计数无运行错误；有macOS secure-coding warning和Unity退出线程提示，未把它们藏作设备通过。原进程退出码未保存，本QA不虚构exit0；不根据旧07c相似计数继承结果。本次局部自动收尾结论为**PASS_AUTOMATED_ONLY**，只关闭本表的既有自动门禁，不宣布1.25完成。

另实际打开新08 Confirm200、Telemetry200和Replay150-SpaceCoroutine原PNG：确认框标题/描述/按钮层级放大，遥测首屏仅到Altitude、其余值/返回需要滚动；Replay150侧栏/动作层级保持但全工作台未同屏。本次目视不扩为全部232张08 raw PNG已人工通过；dynamic的272逐项滚动检查只覆盖它自身三类弹层，不给Home/Replay屏外控制或三端设备补签。

根正安排的后继input identity纯实现不在本次08冻结二进制内，本报告不能作为其集成、输入设备或三端接线通过证据；只核本波10文件source freeze与被测Runtime，不把后继工作区源码借套08native结果。

6张根精选PNG已独立逐字节核对与各自raw SHA一致：[07c旧Home1440](../assets/ui-v125/candidate-20261010-08/home-1440-text100-before07c.png)、[08 Home1440](../assets/ui-v125/candidate-20261010-08/home-1440-text100-after08.png)、[08 Home1920](../assets/ui-v125/candidate-20261010-08/home-1920-text100-after08.png)、[08首Pause](../assets/ui-v125/candidate-20261010-08/pause-1440-first-tab-space.png)、[08 Replay1920 Text100](../assets/ui-v125/candidate-20261010-08/replay-1920-text100.png)、[08 Replay1440 Text200](../assets/ui-v125/candidate-20261010-08/replay-1440-text200.png)。完整raw报告/PNG留本机verification且受忽略规则保护，不承诺GitHub能加载raw链接；精选是实际运行截图，不是概念参考。

两名独立美术分别[Dashboard](V125_DESKTOP08_DASHBOARD_VISUAL.md)及[Flight/Replay](V125_DESKTOP08_REPLAY_VISUAL.md)继续标VISUAL_DIFFERENT；局部叠字/重复单位改善不能替代原23图目标。Text200各视口所有页面的滚动可达、屏外目标点击与全内容可见仍未被本补充验证，NOT_RUN；不要把正的worldBound或单张首屏截图改写成可用性通过。

## 根的交接与门禁

精确注入/运行命令在补充合同；运行参数为`--desktop08-qa`，报告`desktop08-report.json`。先相同v1在07c两视口建立真实基线，再08同矩阵；每次新的隔离data/results目录，原app不改。预计每视口8张PNG，实际数量另复核。

08上述原样Space v3的1440/1920/1280、Pause844×390、dynamic v3 844×390、Replay typography v2 1920×1080及原生smoke已有真实结果，独立重核production freeze / Runtime SHA / probe SHA / 完整报告与PNG后局部收尾。后续改源码须按风险重新验证，不能直接沿用08通过；时序probe需修订时保留v1失败、另加v2，不回写原合同。

报告/geometry/字号通过均最多PASS_AUTOMATED_ONLY，不证明23原始概念图全面一致、全缩放、物理控制器、iPhone/iPad真机、安全区或最终用户验收。机型仍须用户逐机型确认；本QA无权代签。

根统一验证后提交并推送，[仓库](https://github.com/yijunchai2026-gif/AeroSim) / [开发分支](https://github.com/yijunchai2026-gif/AeroSim/tree/codex/v125-product-lines)。本成员不做Git写入，不将未证实的push或构建当GitHub交付；根最终提供真实commit与远端SHA。
