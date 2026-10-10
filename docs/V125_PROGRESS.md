# AeroSim 1.25 开发断点

开始：2026-10-09（America/Los_Angeles）。用户指定 18:25 启动；额度限制下排程写入失败。18:37 heartbeat 实际检查到额度恢复，随后成功更新现有自动化并开始工作。时间差如实保留。

这是开发断点，不是 1.25 发布或人工验收记录。Unity 应用当前仍为 1.0.0 候选；本批新增目录审核工具不会把旧应用改标为 1.25。

## 当前目标

- 电脑、iPad、iPhone 三条独立产品线；共享核心、DJI 目录/3D 美术、独立视觉 QA 提供支援。根负责人统一调度、整合与构建，分波次明确文件所有权。
- 三端均支持屏幕双摇杆及外接控制器；电脑保留键盘控制。各端分别验布局/缩放、安全区域、触点、后台中断和性能。
- DJI 从早期到当前逐机型清单与介绍，先交外观审核画廊，**每个机型由用户逐一确认**。未确认禁止正式资产接入；已确认外观改变后重新审。
- 每批代码、文档和图稿适当验证后 commit → push → 核对远端 SHA。保留 0.4 和 1.0，未经许可不合并 main。
- 最终发可用 1.25 试用链接、GitHub 地址/分支/具体 commit 和三端验收证据，等待用户最终验收。

## 断点恢复与交付

- 当前分支：`codex/v125-product-lines`；基础提交 `eb29e81bfe6f50793e1729eb55229a5a55a7f666`。
- 1.0 候选03基础提交曾未上传；本次 GitHub Desktop 恢复可操作，Publish branch 返回 `push complete`，本地远端分支 `origin/codex/v1-design-parity` 为上述 SHA。新 1.25 批次另行提交/推送。
- 开始时存在候选04/05源码、图册和未提交探针；保留这些改动。新目录审核工具的提交不盲目包含全部旧改动。
- 本批代码/文档/图稿已提交并推送：[c48121ee4bc592d4329a877bfa25544fece0d952](https://github.com/yijunchai2026-gif/AeroSim/commit/c48121ee4bc592d4329a877bfa25544fece0d952)，[开发分支](https://github.com/yijunchai2026-gif/AeroSim/tree/codex/v125-product-lines)。GitHub Desktop `push complete` 后另执行 Fetch origin，返回 `fetch complete`，`HEAD` 与 `origin/codex/v125-product-lines` 精确一致。main 仍 `07a9f091ebd24d37e8889a94f286c11237168d46`，没有合并。
- 本次未纳入的历史项仍在工作区：3个旧图像meta的尾空格diff、候选04图册/复核、3份Wave B方案文档。没有丢弃或假称已交付这些未选范围。
- 候选05原始 smoke/缩放/Space/视觉结果见 [独立基线 QA](V125_BASELINE_QA.md)。源码时间晚于 binary，后续修改需要新冻结构建，不能套用旧通过结果。
- 18:40 左右根运行候选05隔离 Pause/Telemetry 原生 probe：58 checks、26 FAIL、0 runtime errors；真实复现 OSD toast 遮挡暂停菜单点击。原始 probe 合同保留，修复后用新候选复测。
- 当前没有新机型经过用户确认，没有 1.25 iPhone/iPad 真机记录。

## 第一批已冻结的进展

- 目录冻结：132 个主机型/硬件研究条目、9 个独立配色/涂装版本，共 **141 个审核项目**；19 家族、8 类别、72 条官方来源。包含 2 个 `hold_release_evidence` 条目；全量无遗漏仍 `not_proven`，不能把计数当成完整性证明。所有项目仍待用户确认。
- 逐机型审核入口：[AeroSim-1.25-机型逐一审核.html](../AeroSim-1.25-机型逐一审核.html)。搜索、分类、介绍、官方出处、单机型确认/修改和导出；132+9 分别审核，子版本不继承主机型确认。初始 0 个批准、2 幅外观位图。无图和截至日证据待核项禁确认。
- 两名美术交付首代 Phantom draft-02、Mini 4 Pro draft-01；[原图与提示记录已加入产品文档](../飞行模拟器产品设计文档.md#253-第一批逐机型外观稿等待用户确认)。它们是生成的审核位图，不是最终网格/UV/PBR，没有接入飞行。
- 三端共享输入算法层冻结，单输入源仲裁、设备校准隔离、数值清理与暂停/菜单/回放上下文清零。严格编译及 3,050 算法检查通过；尚未接入旧 FlightInput 或三端应用，不是实际双触点/控制器验证。见 [输入核心](V125_INPUT_CORE.md)。
- 候选06统一构建后，macOS 原生 smoke **66/0**、同一 Pause/Telemetry 探针 **74/0/0 runtime**、Space v3 **1911/0/0 runtime**，保留 24 个 UNRENDERED 项而不计通过。原候选05的 58/26 失败与原探针保留。候选06仍标 1.0.0，供1.25继承。见 [独立 QA](V125_CANDIDATE06_QA.md)。
- 动态新建确认/Telemetry 字体没有完整遵守 Text200，独立 QA 实看并量化为 P1；完整缩放矩阵、23 图视觉差异、iPad/iPhone 真机和物理摇杆仍未通过。不可把上述零失败计数当成全面视觉验收。
- 本机审核 HTML 的自动浏览器打开被 `file://` 安全策略拒绝。本批没有浏览器缩放、键盘焦点、PNG 实际加载和导出交互的实测声明；不换协议/其他浏览器绕过。生成器与独立合成 DOM 状态 QA 不替代这些测试。
- 独立 QA 冻结结果：审核工具25 tests PASS/0 FAIL/0 SKIP，含真实冻结目录及2稿清单的隔离生成；输入算法独立复跑3,050/0。根另外核对05精选23截图与23原图的全部散列、源报告散列均一致；图册仍标05历史及PENDING，未冒充06或1.25。
- 候选06本机稳定副本：`AeroSim/Builds/macOS-V1-Candidate-20261009-06/AeroSim.app`，从被测构建原样复制，Runtime DLL SHA256同为 `fe1e7a097e140687e60fa6827feda6f473b3e18c56e232e02e8780b4ff746ef7`。该目录受.gitignore保护，不提交应用二进制；0.4及候选03仍原位保留。
- 网页本机记录只是用户选择草稿：正式接入需用户交付逐机型记录，根负责人重新核对目录、稿件 revision 和当前 PNG 字节 SHA256。外链 PNG 在生成 HTML 后变化时不得接受旧散列确认，localStorage/普通 JSON 不是认证签名。

## 第二批：动态弹层字体、两份新稿与输入接线方案

恢复日期：2026-10-09 23:45 左右（America/Los_Angeles）额度检查恢复可用；期间失败成员接力恢复，没有购买额度或使用 reset。本批跨过本地午夜，日志保留实际UTC时间，稳定副本以保存日期2026-10-10命名，历史断点不覆盖。

- 页面程序员完成 Confirm / Session Objective / Flight Telemetry 新树字体倍率及调度生命周期修复。根冻结后首次07构建失败（`ExecuteLater`返回void被赋给item、InputRouter.meta为33hex）；保留[07初始冻结](../verification/v125-qa/candidate07-source-freeze.json)，根修复API调用和无引用的GUID后重新[冻结07b](../verification/v125-qa/candidate07b-source-freeze.json)，Unity实际构建成功。不用算法编译代替Unity集成。
- 07b实际应用版本仍 **1.0.0**；Runtime DLL SHA256为 `6f487a915564c1a5ab62e4ca57e9c5345f858e1526328984dd7537191537ea96`。稳定副本为 `AeroSim/Builds/macOS-V1-Candidate-20261010-07b/AeroSim.app`，字节与被测构建一致，受.gitignore保护；06、03和0.4保留。
- 新独立动态字体v1合同在06为2185检查/208FAIL/0runtime，在07b为2185/48FAIL/0runtime。06的160个字体倍率失败在07b转通过，但两版均有48个快速Pointer状态时序失败，不能把v1整份报告标通过。独立QA据真实trace另冻v2，只添加release后事件分派flush，不删断言/放宽字体裁切容差，两个binary须重新跑；最终结果见[07独立QA](V125_CANDIDATE07_QA.md)。
- 07b原生smoke66PASS/0FAIL、原Pause/Telemetry合同74/0/0runtime；新1440×900 Space v3覆盖为1911/3FAIL/0runtime、24UNRENDERED。暴露Tab首焦点和Professional Home两个Text100行框问题。根补同视口实际06为1911/3/0、07b1920为1911/0/0；3条FAIL逐字相同，故为既存缺口。另有77条Replay Text200 PASS evidence的字体不同，不能声称全报告相同。原错误旧CLI参数运行未启动v3，已终止，不计测试通过。
- 新两名美术分工交付原版Inspire 1 + X3和原版Mavic Pro四视图，根实际打开检查并纳入[PRD第二批图稿](../飞行模拟器产品设计文档.md#255-第二批逐机型外观稿inspire-1-与-mavic-pro)。前者TOP框架拓扑明显漂移、后者投影/云台/桨叶细节未闭合；均为pending_user位图，不是最终网格。目录保持132+9、现4幅稿、0用户批准。
- 审核生成器支持legacy/current/inspire/mavic四个受限文件路径；只增加实际核对的HTTPS `www1.djicdn.com` / `www4.djicdn.com` 精确官方图片host，不泛放CDN任意域。独立测试28PASS/0FAIL/0SKIP，保留原25项，并增加四namespace/精确ID与散列/域名负例。浏览器实测边界仍未解除。
- 技术负责人交付[三端输入接线方案](V125_INPUT_INTEGRATION_PLAN.md)，明确源身份/旧校准迁移、双触点owner、后台中断与中立重启、各端文件所有权及Gate A–D。它是下一波可执行计划，不是已经接线；原纯输入算法3,050/0复跑通过，尚无真机或物理控制器证据。
- 动态v2只修probe事件flush，真实06为2209/160字体FAIL，07b为2209/4快速scroll FAIL；追加v3只允许一次有界重ScrollTo、不删原断言，真实06为2209/160字体FAIL，07b为2209/0/0runtime。各版合同、原JSON/散列及失败不覆盖。
- 根根据Replay旧PASS evidence中的错误字体基准，追加实际两帧USS读取边界，冻结并构建[07c](../verification/v125-qa/candidate07c-source-freeze.json)。构建Success、Runtime DLL SHA256 `caf2331190601b9bddab0ccfbc312fab894baa0c20a9d58a101a208e15d5e061`；稳定原样副本 `AeroSim/Builds/macOS-V1-Candidate-20261010-07c/AeroSim.app`，实际版本仍1.0.0开发候选。07b/06/03/0.4不覆盖。
- 07c实际smoke66/0、Pause844×390为74/0/0runtime、动态v3为2209/0/0runtime、Space1920×1080为1911/0/0runtime。Space1440×900仍1911/3/0，24UNRENDERED不计通过；该份全部1911 rule/evidence/pass独立比对与06一致，77条字号漂移证据恢复为侧栏34、动作32、免责声明24。其余桌面布局缺口没有解决，见[诊断与下一波方案](V125_DESKTOP1440_TRIAGE.md)。
- 独立固定USS字号Replay v1在06/07b/07c各778/0/0runtime，覆盖真实Records Pointer进入、Text100/125/150/200及重开/偏好/档案不变。此入口没有复现旧coroutine路径差异；追加[冻结v2](../verification/v125-qa/replay-typography-freeze-v2.json)保留全部v1、补同Space页面顺序与public BeginReplay帧阶段，实际06为1454/0/0runtime、07b为1454/38字号FAIL/0runtime、07c为1454/0/0runtime。保留07b失败和原合同，不修改token/容差制造通过；这只关闭局部字号门禁，不代表全缩放/23图/三端交付完成。
- 23张原概念图与旧05/06原始报告不改；本批精选截图分别标06/07b历史与07c当前局部实际运行证据，已[同步PRD](../飞行模拟器产品设计文档.md#257-候选07c字体基准调度与实际截图)，不替代视觉目标或用户验收。独立QA实际看图另记Replay图表重复单位“ft ft / mph mph”，未修改冻结源码，留下一批修复；旧自动字号/行框合同不覆盖文案语义。

本批最后Replay入口对照已实际跑完，独立QA冻结并停止写入：[QA文档](V125_CANDIDATE07_QA.md)、[紧凑证据](../verification/v125-qa/candidate07-summary.json)，51个指纹复核一致。根复跑28目录测试、3050纯输入检查及新文档路径检查；仅本批53个明确文件纳入本地提交，历史无关改动保留。

提交前空白检查仅提示新增Replay v2探针与注入脚本各一个EOF空行；为保留被编译/测试的冻结字节及散列，未在验证后格式化这两个文件。业务源码及目录改动无该提示，不影响编译或上述实际断言；不是隐藏测试失败。

上传通道当前阻塞：终端HTTPS无可用凭据，GitHub Desktop电脑操作通道多次超时；GitHub connector只读该私有仓库返回404（不能推断仓库不存在或自行改可见性）。Desktop标准凭据helper需DESKTOP_PORT、不能独立正常调用；标准Credential Manager在禁止交互下也没有可用认证。未打印/提取凭据、未更改权限。尚无本批push成功与新远端SHA，不声称已上传。后继恢复先核实际Git及上传状态，再处理下一批。

## 团队和文件边界

| 波次/负责人 | 本批职责 | 文件范围 |
|---|---|---|
| 根负责人 | 自动化更新、分支/交付、审核页、确认门禁、原生集成 | AGENTS.md、本文、PRD 新章节、审核网页与生成工具、已协调的集成代码 |
| 目录工程师 | 官方 DJI 全历史目录调研与介绍 | docs/V125_DJI_CATALOG.md、assets/catalog-v125/dji-aircraft.json |
| 技术负责人 | 三端独立产品线、共享接口、现有输入/设备缺口、纯输入核心算法 | docs/V125_ARCHITECTURE.md、docs/V125_INPUT_CORE.md、Runtime/Input 新类型和算法测试 |
| 独立 QA | 候选05已有证据、源码/binary边界、审核页复核 | docs/V125_BASELINE_QA.md、verification/v125-qa/ 紧凑证据 |
| 早期机型美术 | 首个经典机型的官方对照与待确认图 | assets/v125-art/legacy/、docs/V125_ART_LEGACY.md |
| 现代机型美术 | 首个现代机型的官方对照与待确认图 | assets/v125-art/current/、docs/V125_ART_CURRENT.md |
| 第二波页面程序员 | 三类动态弹层字号与字体队列生命周期 | Responsive.cs、Flight.cs、FlightDesignV1.cs、OnDisable字体取消 |
| 第二波技术负责人 | 三端输入迁移/接线方案与只读风险复核 | docs/V125_INPUT_INTEGRATION_PLAN.md |
| 第二波电影机型美术 | Inspire 1原版X3稿及官方对照 | assets/v125-art/inspire/、docs/V125_ART_INSPIRE.md |
| 第二波折叠机型美术 | Mavic Pro原版稿及官方对照 | assets/v125-art/mavic/、docs/V125_ART_MAVIC.md |
| 第二波独立QA | 新旧动态字体、旧Space/Pause合同复核、4稿门禁测试 | docs/V125_CANDIDATE07_QA.md、verification/v125-qa/新冻结/紧凑证据 |

并发上限四个运行成员，角色按波次接力，不并行争用 Unity 构建。后续设备程序员/QA 按技术审查分配，跨线支援由根负责人记录交接。

## 后继门禁

1. 对账目录历史/地区/硬件缺口，继续按官方材料补齐；132+9 是当前待确认清单，不标成完整闭合。
2. 等用户逐机型看初稿、提交确认或修改意见；其他美术继续按机型制作。程序与美术不得批量代确认或默认接受。
3. 最新字体源码/构建为07c，完成最后Replay入口对照和本批交付后按输入接线Gate A–D推进；1440桌面布局/焦点缺口、完整缩放矩阵和23原图视觉仍需修。历史失败与原图不删除。三端实际接线还未完成，方案不是实测。
4. 安装/确认当前 Unity iOS Support 和 Xcode 后再导出/真机；当前技术检查发现这些依赖缺失，历史导出不可替代本版本验证。
5. 审核稿逐机型获得用户确认后才可制作/接入最终资产；共享核心与设备端其他工作不因此暂停。
6. 达到三端可交付候选后提供链接，停改等待用户验收。

## 自动续作

自动化 `aerosim-1-0` 已成功更新为“AeroSim 1.25 多产品线续作”，维持每小时检查同一线程；完整产品线、逐机型确认和最终发送链接要求已写入保存指令。无可行动新变化时保持安静，只在实质进展、完成或需要用户处理的阻塞时通知；不自动消耗 reset 或购买额度。
