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

本批代码/图稿/文档已**本地提交**为 `7a23c00619987733698b6e8c39a8455cf708f012`，[GitHub提交目标链接（尚未上传，当前可能不可访问）](https://github.com/yijunchai2026-gif/AeroSim/commit/7a23c00619987733698b6e8c39a8455cf708f012)。实际`git push origin HEAD:refs/heads/codex/v125-product-lines`与随后`ls-remote`均exit128：`could not read Username ... terminal prompts disabled`。因此上传状态为 **UNPUSHED_AUTH_BLOCKED**；本地缓存origin仍`6b47aac516c7f5113cec82fc4beb4357467d5942`，不是本轮新远端证据。main、1.0与0.4分支SHA未变，0.4应用未覆盖。

下一次恢复先检查GitHub访问和本地ahead提交，上传本批及此断点记录后再核远端SHA；不得重复生成图稿/构建/重跑已完成合同，也不得把缓存origin或构建成功当push成功。用户可在GitHub Desktop的AeroSim仓库中完成正常Push origin/Fetch origin；若登录或仓库权限需要用户操作，明确请求恢复访问，不提取密码或擅改账号安全设置。需先补上传；待确认的机型和三端未完成项继续如实保留。

## 第三批：双杆触点纯核心与两机型修订稿

2026-10-10 恢复时 HTTPS 认证仍不可用；只读 SSH 检查亦没有已加载身份或默认密钥，没有创建密钥、接受未知主机密钥、读取秘密或更改账号安全。前述 `7a23c00` 与上传断点 `94c0df6` 仍为本地 ahead 提交，不能当作已上传。前批“先补上传”的建议没有实现；本波依据用户继续本地可做工作及 AGENTS 的上传失败记录规则，限定推进不依赖认证/外观批准的纯核心与审核位图。最终交付仍必须恢复上传并核对真实远端 SHA。

- 根作为共享输入技术负责人先冻结 [双杆触点契约](V125_TOUCH_CONTRACT.md)，[冻结指纹](../verification/v125-qa/touch-contract-freeze.json) 为 `119e8e576033ecbd1bc1c6de7ceb4dde50a3518745479fa524b07fbe9e72f952`；契约开头“尚未执行测试”是冻结时状态，后续实际结果另记，不修改已冻结契约。
- 触点程序员只新增 `TouchInputState.cs/.meta`、开发者测试/runner 和 [实现交接](V125_TOUCH_IMPLEMENTATION.md)，停止写入后交独立 QA。源 SHA `2319cdc4e7dd9aa4510565caba6b1d0be5a712fadd8e032328f0ec236b470fb6`；根复跑严格编译为 **17,175 开发者检查/0失败**，原输入回归 **3,050/0**。这是 Gate B 纯状态子集，未接旧 FlightInput/UI、未新建 Unity binary，不能把07c旧应用证据移作本源码的集成通过。
- 左右 owner 共享一个 Touch source；第三指、跨杆、重复 Down 拒绝，取消只清对应杆，Suspend/Resume 清零后须新 Down；无效 owner Move 清该杆两轴。仅有 pointerId 无法辨别复用同 ID 的迟到旧事件，后续适配器必须加入会话/绑定代际，不能宣称真实 capture/OS 中断已经验证。
- 独立 QA 另写数组参考状态机逐事件比对：**514,425检查/0失败**，25,746事件（178定向、13,568主动高占用、12,000确定性随机），实际双owner事件13,443，13项纯router组合；根另读全源并严格编译复跑同值。三个临时错误实现都成功严格编译、被未改测试拒绝，而非拿编译错误当检出；具体负例和SHA见 [独立QA](V125_TOUCH_QA.md) / [紧凑证据](../verification/v125-qa/touch-state-independent-summary.json)。不称真实触控或应用生命周期验收。
- 两名美术分别冻结原版 Inspire 1 X3 **draft-03**（SHA `94a2245bd26a4bfdd930ea0b6d51f596e9bf4d1459f88861329a4c4e695e4491`）和 Mavic Pro **draft-02**（SHA `ace4e4d0edad8555ac726a8b2c674c4a7513201cc8c01bcac8927fd12c462931`）。根实际查看两份current及Inspire02中间稿：纵杆/斜撑及紧凑相机局部改善，但机械连接、脚/云台、跨视图、投影、底部/材质仍未通过。01旧稿、02失败中间稿与真实提示词不覆盖。当前均 `pending_user`，不是最终3D，不接正式飞行。
- 审核页重新生成为同四个机型的唯一current，目录仍132+9、4幅current、0用户批准；目录SHA `4beb519cb142ce156d33464c0ef85d674921df2013bf3f32771f7fbff0cf909c` 不变。原28目录门禁根/独立QA复跑PASS/0FAIL/0SKIP，涵盖revision/hash变更使旧确认失效，但不替代实际浏览器交互。新稿用相对路径同步 [PRD25.8](../飞行模拟器产品设计文档.md#258-第三批机型审核稿记录局部修订不是最终3d)，[25.9](../飞行模拟器产品设计文档.md#259-双虚拟摇杆纯触点实现边界) 另记三端待接线/真机界限；23原始目标不变。

全部成员冻结停止写后，根最终核对独立报告30个指纹与三个mutation的精确替换重建散列；30条本批文档本地链接有效、生成页当前03/02与pending状态一致，88个Assets meta GUID有效且唯一。07c稳定副本Runtime DLL实际SHA仍 `caf2331190601b9bddab0ccfbc312fab894baa0c20a9d58a101a208e15d5e061`，未作新Unity构建或冒用旧native通过。只纳入本批24个明确文件，历史3个meta及候选04/Wave B无关项继续保留；暂存空白检查无警告。

本批已**本地提交** `32e118b3e55b74b5f5c80d101812ee865db61c94`：[GitHub提交目标（尚未上传，可能无法访问）](https://github.com/yijunchai2026-gif/AeroSim/commit/32e118b3e55b74b5f5c80d101812ee865db61c94)。随后实际push与ls-remote均exit128：`could not read Username for https://github.com: terminal prompts disabled`。状态仍 **UNPUSHED_AUTH_BLOCKED**，没有成功push或新远端SHA；origin缓存不是证明。前两笔本地 `7a23c00` / `94c0df6` 同样未补上传，本波断点记录也须一起上传。未改仓库可见性/认证设置、未合并main或force-push。

后继先核实际认证与ahead状态，认证恢复后正常Push origin/Fetch origin并独立核远端SHA；不重复本波生成、构建或已完成合同。用户需在GitHub Desktop的AeroSim仓库确认正常登录与仓库权限并执行Push origin；负责人目前不能安全取得终端凭据。继续不依赖外观批准/认证的本地工作时，仍遵守独立QA、冻结、每批提交和实际上传失败记录，不把未上传分支当最终交付。三端正式接线、真机、全缩放/23图与机型逐一确认仍未完成，1.25没有发布。

## 第四批：候选08桌面局部修复（正在独立回归收尾）

2026-10-10 用户再次明确“开始1.25”，从已有08源码/原生结果恢复，不重做旧构建与已完成矩阵。恢复时本地HEAD仍 `2c2597411e94dcaa1fd568b8bf42729dbd2a6aa5`、ahead4，08尚未提交；新一次非交互 `ls-remote` 实际exit128、HTTPS无Username凭据，未补上传旧四笔。账号/仓库权限不修改，不把origin缓存当新远端SHA。额度中断的独立QA/第二美术已接力恢复，无购买额度或reset。

- 页面程序员冻结Pause局部tabIndex=1和Professional Home明确copy宽/自然高，根另修Replay三行分离value/unit；正常Flight/旧HUD保留suffix。五个业务文件及关键基线源冻结在 [08源冻结](../verification/v125-qa/desktop08/candidate08-source-freeze.json)，构建前后10个SHA一致。不缩36/16字号、不隐藏Objective、不修改原23图/旧probe/旧失败。
- 统一Unity08实际成功，应用仍 **1.0.0开发候选**；未注入app为 `/private/tmp/aerosim-v1-build.z7j0iF/AeroSim.app`，Runtime DLL SHA `0aec831cff961caafa77a38f90bdf0335ff4026b817ccde9fd71b8ed21e99f9f`。07c/07b/06/0.4不覆盖；本批稍后保存独立稳定副本，尚未当最终1.25交付。
- 同一 [补充探针v1](../verification/v125-qa/desktop08/desktop08-contract-v1.md)：真实07c1440为215/20FAIL、1920为220/15FAIL；08分别215/0、220/0，均0runtime。关闭误入Objective的首Tab/Space、窄列行框和英制/公制三行重复单位，保留旧35FAIL真实证据；不调整字号/行框容差。两视口合计435，不是每视口435，更不是23图验收。
- 原Space v3在08的1440×900、1920×1080、1280×720均实际1911/0/0runtime，每份各24UNRENDERED单列不计PASS。原Pause/Telemetry844×390实测74/0/0runtime。dynamic v3、Replay typography v2和smoke在此次恢复时仍待完整运行/独立报告收尾，不借07c旧通过；最终本批结果另追加。
- 两名美术分别实看原图与08首页/Replay两尺寸、Text100/200，保留 **VISUAL_DIFFERENT**。1440/Text100首页叠字、Replay Text100重复单位确已目视关闭；桥任务配海岸、hero高度、CTA宽、首屏密度、场景/图表层级等仍偏离原目标。Text200首屏未出现的遥测/CTA不作目视通过；屏外滚动/点击尚未被本补充验证。见 [Dashboard对照](V125_DESKTOP08_DASHBOARD_VISUAL.md) / [Replay对照](V125_DESKTOP08_REPLAY_VISUAL.md)。正常Flight默认HUD此波未另取新图，不用Pause截图替代。
- 精选6幅原始PNG字节副本已同步 [PRD25.10](../飞行模拟器产品设计文档.md#2510-第四批候选08桌面局部修复与实际效果)，历史07c修复前/08修复后分开。全量原报告/截图仅本机保留；概念目标和运行截图不混用。
- 技术负责人 [Gate A候选契约](V125_GATE_A_CONTRACT.md) 已 root 冻结，SHA `f2237ca63358afcd2ee7c76eb828609e61b8094a15ecdd43476d5f7012257697`；不是实现/接线/设备通过。修正旧错误路径推断：Unity.app同级 `PlaybackEngines/iOSSupport` 文件确存在，仅 **IOS_MODULE_FILES_PRESENT**；当前 `xcode-select` 仍CommandLineTools，`xcodebuild -version`实际失败，完整active Xcode不可用。Editor target支持/新export/真机均未验证，历史产物不可代签。标准1.16 iOS generic Joystick不等于支持Gamepad，三端需分别验证兼容协议。

恢复后另分配下一批**纯身份解析子集**：实现者只写InputDeviceIdentity新文件与开发者测试，独立QA另写规范化/哈希向量和错误实现测试。它不在已构建08二进制内；其源码即使稍后纯编译通过，也不能套本批native结果。固定布尔依据在 [身份子集补充冻结](../verification/v125-qa/identity-contract-freeze.json)，明确interface与layout两者均非空。未做live registry/碰撞绑定、配置迁移、Unity设备采样或三端UI适配；后继须单独冻结/验证/提交。

## 本轮：PRD冲突重整与Mavic 4 Pro展示（用户直接请求，2026-10-10）

用户要求“检查设计文档前后冲突、重新整理；Mavic Pro展示采用pro4图片”。本轮只推进文档/审核画廊范围，不继续或代签之前未完候选08源码与纯身份实现；用户总体“基本正确”不构成逐机型、revision、真实散列批准。

- 文档修订为v1.6，产品目标仍1.25、实际应用仍1.0.0候选08。第0章为唯一当前合同/阅读索引，第17章三训练工作台与三设备线分开，第18章23原始页面概念图不改，第21–25章保留历史批次/失败。技术成员的23项冲突/歧义见[审查](V125_PRD_CONFLICT_REVIEW.md)；版本/平台、图稿尺寸、输入与校准、暂停/快捷键、返航800ms、单位、地图目标、隐私和发布链接已澄清。
- root以builtin imagegen全新生成`dji-mavic-4-pro` / draft-01，1536×1024，SHA `efdc1b0d6c5bf307968731e1c5f0afd9f40aa3e4e5a4eb3deb5382e090eaa9ab`，源与项目副本cmp一致。root与独立美术都实际看图、以官方照片作参考；提示词及[独立美术记录](V125_ART_MAVIC4_REFERENCE.md)保留。球形三摄身份可辨，后脚、相机框/轴座、传感窗及跨视图仍未完全一致，pending_user位图不是最终3D。
- 新图相对路径嵌入PRD0.6，生成画廊为141项目/5机型有current稿/0用户批准；2016 Mavic Pro两个PNG/提示词/完整current manifest行、512GB独立身份不变，目录SHA仍`4beb519cb142ce156d33464c0ef85d674921df2013bf3f32771f7fbff0cf909c`。未经用户单独确认不能正式选机飞行。
- root复跑目录30项及独立checker隔离回归10项，总40 PASS/0 FAIL/0 SKIP；包括错ID、旧图复用、错SHA、自动批准和陈旧画廊拒绝。文档检查（PRD＋六依赖文档、require-tracked）为816检查/0失败、242本地链接，23原图嵌入与字节不变。19个初次冻结指纹逐一吻合；最后按已有probe修正“20秒fixture在第10秒采样”措辞，不冒称新native结果，保留初次freeze并新增[冻结v2](../verification/v125-qa/prd-reorganization-freeze-v2.json)。独立最终报告由[文档QA](V125_PRD_REORGANIZATION_QA.md)另记，root不以自己的复跑替代其签收。
- PRD已有08六精选PNG和两份已完成视觉差异文档随本次文档依赖纳入；Dashboard原档只作本机索引，不上传全量QA，未完成08独立QA链接不作为交付。Gate A契约候选按已冻结原字节纳入，仅是方案，不是已实现/接线。此前五业务源码、纯身份实现/QA及其他历史未选内容仍未提交，本轮没有Unity新构建、iOS真机、地图接入或最终用户验收。
- 架构第6章已加勘误：旧错误路径导致“iOSSupport缺失”推断撤回；本轮只读再确认同级模块目录存在和active developer dir为CommandLineTools。不能由此推断Editor target/导出/签名/真机通过，也没有安装/配置系统或购买服务。

本次验证后由root明确选文件本地commit，再实际push/ls-remote。旧ahead4提交尚未补上传，终端认证阻塞仍需新操作验证；此段不预称上传成功。后继从最新Git及本段/QA恢复，不重新生成本轮稿或回滚历史证据；候选08整批QA与三端开发断点继续保留。

独立成员已实际完成v2终验并停止写：816/0文档检查、30/0目录测试、10/0检查器反例、19/19指纹一致；root核对其最终QA文档SHA `ddd3f4552d80164cb3954c82253b1c96ed533eeb246f58b08a1e672c58b50e25`和[紧凑结果](../verification/v125-qa/prd-review/final-results.json) SHA `7cf0b11d3f620a218224ebcb2c9125ffaf90c2faaaff7bdd9e6b0cd2ae4aee32`均吻合。最终PRD SHA `cc72557d8f3fa0bb899495516418908fba811d63d80fa553631a3c87bdbbf1a6`，不把自动文档验收扩展为机体机械准确或产品完成。全工作区旧3个图像meta尾空格未修、不纳入；本批暂存空白检查通过。

本轮27个明确文件已本地提交为 `e5519fa9c781674d6c0557b79d0aeb0310e0f48e`：[GitHub提交目标（尚未上传，可能无法访问）](https://github.com/yijunchai2026-gif/AeroSim/commit/e5519fa9c781674d6c0557b79d0aeb0310e0f48e)。实际非交互push与随后新ls-remote均exit128：`could not read Username for 'https://github.com': terminal prompts disabled`。状态为 **UNPUSHED_AUTH_BLOCKED**，没有新远端SHA，旧origin缓存不作证明。最后追加已完成QA文档的本地依赖复查为844/0、251处链接，与独立816/0检查的输入范围不同，不改写独立结果。此前未选业务源码仍留在工作区，未合并main或force-push。此上传回执另作本地提交并实际重试；认证恢复后须正常Push origin并核对远端，不重做本轮图稿和验证。

## 本轮：UX / UI / 场景优化调研（2026-10-10）

用户要求先由UX/UI调研是否需要优化，强调界面简单清楚、操作前后一致、iPad/iPhone适配及真实纹理；随后要求继续。根本轮按调研范围执行，不继续之前未完08业务/纯身份实现，不直接改应用或替用户确认新机型。

- 三路分别完成[UX流程](V125_UX_FLOW_AUDIT.md)、[UI/移动布局](V125_UI_MOBILE_AUDIT.md)、[场景材质](V125_SCENE_REALISM_AUDIT.md)，根整合为[优化调研报告](V125_UX_UI_OPTIMIZATION_RESEARCH.md)。已有源码和PNG按静态风险/历史观察/当前合同分层，不伪称用户访谈、新native运行或真机验证。场景成员曾因额度中断，用户继续后恢复到原范围，未购买额度/使用reset。
- 重点建议：统一命令/暂停原因/中立恢复、修设备后返回保留SessionDraft、分组保存策略；按可用空间做iPad宽窄布局和iPhone横竖屏HUD，真机验证pt与panel/像素和safeArea；先改材质类别与米尺度、法线/光滑度、近景结构/水面/光照，再扩场景。移动/预算均为待实施建议，不降低23原图或继承功能要求。
- 后续独立QA指出UI首次稿的“全部设置draft”与现PRD的UI/HUD/Text独立即时保存冲突；作者修正并保留首次SHA。根再按QA意见去掉矩阵旧30映射的固定当前状态基数，改按冻结实现清单；旧PRD/V1映射和课程基数不重写。
- 4报告及源守卫已[冻结](../verification/v125-qa/ux-ui-research-freeze.json)，其中31个已存在源/PRD/目录/新稿指纹按本轮真实dirty工作区捕获，是“只读保持”守卫而非运行构建冻结。root复核31/31及5/5研究指纹一致；PRD仍v1.6、原始23图、机型PNG和批准状态均不改。当前仍6个旧运行原型与141个独立审核项目，5个current稿/0用户批准，新Mavic 4 Pro不接正式飞行。
- root文档工具对PRD＋4报告（require-tracked）复查为1075/0、329处本地文件链接、未发布依赖0；它只查文档/引用/既有门禁，不能当UI/UX可用性、场景真实感、3端性能或用户验收通过。独立终验见下方，实际Git上传结果另记，不预称上传成功。
- 开始HEAD `241aa545ff3e35bd5c436bc7cadcdb37948496d3`；既存未选业务源、3个旧meta、纯身份/旧候选04/Wave B与原QA产物均保留，本轮只选调研文档和紧凑证据。认证此前仍阻塞，本批提交后再次实际push并核远端，不把origin缓存当新上传证据。

独立终验已实际完成，范围为 **PASS_DOCUMENT_REVIEW_ONLY**：PRD＋4冻结报告1075检查/0失败、329处本地文件引用、未发布依赖0；另独立复跑目录30项与文档检查器隔离反例10项，合计40 PASS/0 FAIL/0 SKIP。研究冻结5/5、只读源守卫31/31、原23图23/23、旧Mavic与精选/历史核对PNG均字节一致；141目录仍pending_user、5份current稿、0用户批准。两项文档口径冲突已关闭，不代表对应运行行为已实现。见[独立QA](V125_UX_UI_RESEARCH_QA.md)，SHA `4ed4b96a8d8c54799d7d47a2b7b721b8838effead85188923b22ffee5ad99c02`；[紧凑结果](../verification/v125-qa/ux-ui-research-results.json)，SHA `ebdc8c088e92587eaa70b0f73800adfed34510bd0258dfba3ee65e31e6f76521`。本轮没有新Unity构建、移动真机、运行性能或材质改造；1.25仍未完成交付，Git上传结果另记。

追加自身QA文档后，root与独立成员分别只读复查同一扩大范围：PRD＋4报告＋QA文档1142检查/0失败、351处本地文件链接、未发布依赖0，暂存空白检查exit0；不覆盖原四报告1075/0的冻结证据。独立成员已停止写入，源与报告保持冻结。

本轮9个明确文件已本地提交为 `3ed9fa87c847fb280c31170ee3a3c3a7e1264063`：[调研提交目标（尚未上传，可能无法访问）](https://github.com/yijunchai2026-gif/AeroSim/commit/3ed9fa87c847fb280c31170ee3a3c3a7e1264063)。实际非交互push及随后新的ls-remote均exit128：`could not read Username for 'https://github.com': terminal prompts disabled`。本批状态为 **UNPUSHED_AUTH_BLOCKED**，无法取得新远端SHA，旧origin缓存不作上传证明。未改GitHub账号/可见性/安全设置、未读取或绕过凭据；认证恢复后应正常Push origin并核对远端分支，不重新调研或覆盖既存未选代码。此真实上传回执另作本地提交并再实际重试一次；若认证仍阻塞，停止同状态重试并向用户说明。

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
| 第三波根/共享核心技术负责人 | 冻结触点接口、页面/文档整合、验证和Git | V125_TOUCH_CONTRACT.md、touch-contract-freeze.json、本文/PRD/生成审核页 |
| 第三波触点程序员 | 三端共享双杆纯触点所有权与开发者自测 | TouchInputState.cs/.meta、TouchInputStateDeveloperTests.cs、开发runner及V125_TOUCH_IMPLEMENTATION.md |
| 第三波独立QA | 另写参考状态机、错误实现负例及当前图稿门禁 | 新独立触点tests/runner/summary、V125_TOUCH_QA.md |
| 第三波两名美术 | 原版Inspire/Mavic局部修订，保留旧稿，逐机型待确认 | 各自inspire/mavic资产目录与专项美术文档 |
| 第四波电脑页面程序员/根整合 | Pause意图焦点、首页copy、Replay独立单位 | FlightDesignV1.cs、Product/Responsive.uss；根独占Flight.cs/Pages.cs |
| 第四波独立QA | 07c→08同probe对照、旧Space/Pause/dynamic/Replay保护、compact证据 | desktop08新probe/contract/summary、V125_DESKTOP08_QA.md，不改原工具 |
| 第四波技术负责人 | Gate A raw/身份/旁路事务精确候选及真实依赖边界 | V125_GATE_A_CONTRACT.md，只读其他源码/本机SDK |
| 第四波两名UI美术 | 原图与08实际Dashboard、Replay质性对照，不代签像素验收 | V125_DESKTOP08_DASHBOARD_VISUAL.md、V125_DESKTOP08_REPLAY_VISUAL.md |
| 下一波纯身份程序员/独立QA | descriptor解析首子集；独立预期和mutation在冻结后接力 | InputDeviceIdentity新文件、identity开发/独立tests各自互斥、专项实现/QA文档 |
| 本轮UX技术/页面审查 | 导航、保存、暂停、返航与输入上下文静态调研 | V125_UX_FLOW_AUDIT.md，只读运行源码 |
| 本轮UI美术/移动布局审查 | 原图与历史候选对照、iPad/iPhone布局与触点建议 | V125_UI_MOBILE_AUDIT.md，不改图稿或移动代码 |
| 本轮场景3D美术审查 | 材质类别、米尺度、shader/几何/水面与性能调研 | V125_SCENE_REALISM_AUDIT.md，不生成/接入新资产 |
| 本轮根整合/独立QA | 建议整合、冻结与只读守卫、独立检查器回归和交付 | V125_UX_UI_OPTIMIZATION_RESEARCH.md、V125_UX_UI_RESEARCH_QA.md、ux-ui-research-*.json、本文 |

并发上限四个运行成员，角色按波次接力，不并行争用 Unity 构建。后续设备程序员/QA 按技术审查分配，跨线支援由根负责人记录交接。

## 后继门禁

1. 对账目录历史/地区/硬件缺口，继续按官方材料补齐；132+9 是当前待确认清单，不标成完整闭合。
2. 等用户逐机型看初稿、提交确认或修改意见；其他美术继续按机型制作。程序与美术不得批量代确认或默认接受。
3. 当前最新已构建08，先完成本批剩余原生保护与独立QA/提交；之后按输入Gate A–D推进，纯身份新源不可冒用08通过。局部1440焦点/行框已修，完整缩放矩阵与23原图视觉未关闭。历史失败与原图保留，三端正式接线仍未完成。
4. 已修正历史“iOS模块缺失”的错误位置检查：实际模块文件存在，但Editor target可用性未验；完整active Xcode当前不可用。后继先确认工具链，再新export/真机；历史导出不可替代本版本验证，不自动安装/改developer目录或签名账号。
5. 审核稿逐机型获得用户确认后才可制作/接入最终资产；共享核心与设备端其他工作不因此暂停。
6. 达到三端可交付候选后提供链接，停改等待用户验收。

## 自动续作

自动化 `aerosim-1-0` 已成功更新为“AeroSim 1.25 多产品线续作”，维持每小时检查同一线程；完整产品线、逐机型确认和最终发送链接要求已写入保存指令。无可行动新变化时保持安静，只在实质进展、完成或需要用户处理的阻塞时通知；不自动消耗 reset 或购买额度。
