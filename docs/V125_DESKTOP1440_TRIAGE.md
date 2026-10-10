# AeroSim 1.25：1440×900 电脑端三项既存缺口诊断

诊断日期：2026-10-10，America/Los_Angeles。岗位：第二页面程序员，只读诊断。先完整阅读 AGENTS、[开发断点](V125_PROGRESS.md)与[输入接线计划](V125_INPUT_INTEGRATION_PLAN.md)，核对 Git 状态并保留所有已有变化。本轮只新增本文；**未修改生产源码、QA 合同、概念图或截图，未启动 Unity/native 应用，未执行 Git 写入。** 方案等待根负责人下一波批准和分配，不混入已经冻结/构建的 07b。

## 1. 结论与实际证据

同一原 Space v3 合同下，06 与 07b 的 1440×900 报告均为 **1,911 checks / 1,908 PASS / 3 FAIL / 0 runtime errors**；三项失败的 `rule/evidence/passed` 逐字一致。两份 Professional Home Text100 PNG 字节也完全一致，已用 `view_image` 实际打开两图，看到标题窄列折行溢出并与副标题、说明重叠。它们是既存窄桌面覆盖缺口，**不是已证明由 07b 引入的新回归，也没有被本轮修复**。

| 原报告 / 实际视口 | checks / FAIL / runtime | 状态 | Tab 首焦点 |
| --- | --- | --- | --- |
| 06 `space-submit-v3` / 1920×1080 | 1911 / 0 / 0 | PASS_AUTOMATED_ONLY | `action-pause` |
| 07b `space-submit-v3-1920x1080` / 1920×1080 | 1911 / 0 / 0 | PASS_AUTOMATED_ONLY | `action-pause` |
| 06 `space-submit-v3-1440x900` / 1440×900 | 1911 / 3 / 0 | FAIL | `action-objective` |
| 07b `space-submit-v3-1440x900` / 1440×900 | 1911 / 3 / 0 | FAIL | `action-objective` |

四份报告均保留 **24 UNRENDERED**，不是 PASS。应用报告版本均为 **1.0.0 开发候选**。原探针 `SpaceSubmitProbe.cs:112` 明确设置 UI100 / HUD100；原 v2 正文内部遍历 Text100/125/150/200。这里没有改探针、重启应用或把 UI/HUD/Text 比例混为一项。

原始本地证据（verification 原产物不作为本次新增提交范围）：

- [06 / 1920 JSON](../verification/v1-qa/results/candidate-06/space-submit-v3/space-submit-v3-report.json)，SHA256 `e1a2f62adcb7c08db91605acc414f19d155200f13570b2870b09c2e3b4f28342`。
- [07b / 1920 JSON](../verification/v1-qa/results/candidate-07b/space-submit-v3-1920x1080/space-submit-v3-report.json)，SHA256 `52cacf57be3379b99be5848f52e00266f3040d62af79c73928d031f11a28488d`。
- [06 / 1440 JSON](../verification/v1-qa/results/candidate-06/space-submit-v3-1440x900/space-submit-v3-report.json)，SHA256 `5e0f1d5193fdb5831a8411d3bddf28bf3d41ddf2fa54a8dec4d15fa19a3a4676`。
- [07b / 1440 JSON](../verification/v1-qa/results/candidate-07b/space-submit-v3-1440x900/space-submit-v3-report.json)，SHA256 `f28d07651f56737f1cecfc837059cd0d9cd4058f222a2671e7283f858fc6b09f`。
- [06 / 1440 Professional Home Text100 PNG](../verification/v1-qa/results/candidate-06/space-submit-v3-1440x900/text100-Professional-Home.png)与[07b 同状态 PNG](../verification/v1-qa/results/candidate-07b/space-submit-v3-1440x900/text100-Professional-Home.png)，共同 SHA256 `d5a6334270bf4346de720de62a238a2cea4107054292ea132f08e22aaefe46b2`。
- 另实际打开[07b / 1920 Text100 PNG](../verification/v1-qa/results/candidate-07b/space-submit-v3-1920x1080/text100-Professional-Home.png)作窄宽对照；其标题在两行内、说明可读，没有 1440 的标题/说明交叠。这只是本页运行截图对照，不是与原概念图一致的结论。

## 2. DESKTOP-TAB-01：布局可见性改变首个自然 Tab 目标

原失败：`v2.intentional-tab-selects-pause`，证据 `focus=Button#action-objective visible=True submitCount=23`。

静态调用链可直接核对：

1. `AeroSimApplication.FlightDesignV1.cs:107–108` 的 `BuildDesignFlight()` 先把 Objective 按钮加到 flight bar，再加 Pause；`FlightAction()` 在 172–179 行生成 `action-objective/action-pause`，没有为两者设置显式 `tabIndex`。
2. `Resources/UI/Responsive.uss:73–74` 默认隐藏 `.objective-toggle`，在 `.hud-compact` 内显示它。
3. `LayoutDesignFlightHud()`（`FlightDesignV1.cs:410–415`）在 logical width `<1560`、height `<700` 或 Text `>1.1` 时设置 `hud-compact`。因此 UI/HUD100 的 1440×900 会显示 Objective，1920×1080 的 Aerial Text100 则隐藏它。
4. `GuardDesignFlightNavigation()`（74–79 行）有意放行 Next/Previous；它保护四方向不抢飞行焦点，但没有规定第一次刻意 Tab 的产品顺序。
5. 原 v2（`SpaceSubmitProbeV2.cs:44–48`）从 FreshFlight 真正发 Tab，再断言焦点标题是 Pause；自然树序下，1440 第一个可见按钮变为 Objective，与真实报告吻合。

**下一个 Space 通过并不表示选中了 Pause。** 同一 1440 报告的 `v2.intentional-tab-space-activates-pause` 为 PASS，但实际焦点变成 `action-back-to-flight`；1920 是 `action-resume-flight`。Objective 弹层会为阅读而把 Simulator.Paused 置 true，恰好满足旧后继断言的 paused/submit 条件。保留原断言与报告，不把这条 PASS 当成 Pause 菜单路径的充分证明。

最小修复建议（未实施）：保持 23 图要求的按钮视觉排列与 Objective 的可访问性，把 flight bar 的 Pause 明确设为首 Tab 入口。例如创建 Pause 局部引用，仅在 HUD 这个 Pause 上设置正 `tabIndex`（如 1），其余自然顺序保留，并原生验证本项目实际 focus ring。**不在进入 Flight 时自动 Focus Pause**，不为过测试隐藏/禁用 Objective，不改箭头 guard 或空焦点 Space 语义。若本项目 focus ring 的正 tabIndex 与 modal 层有实测冲突，再单独选择“仅飞行无焦点的刻意 Next/Previous 进入 Pause”的实现；不可同时叠加两套处理造成一次 Tab 两跳。

下一波补充诊断可明确检查 Pause overlay 可见、Objective overlay 不在树中、首 Space 后焦点为 Resume Flight；作为新冻结补充检查，**不回写/削弱原 v1/v2/v3 合同**。同时验证 Shift-Tab、Objective 仍能到达、打开/关闭 modal、Escape/Enter/Gamepad 恢复和焦点为空时 Space 不动作。

## 3. DESKTOP-HOME-01：Hero 窄文本列与最终折行高度不一致

两项原失败都是 `v2.text-linebox-readable`（Text100 / Professional-Home）：

| 文本 | 实际 content / font | 最终宽度下测量需要 | 差额 |
| --- | --- | --- | --- |
| Bridge Inspection Fundamentals | 198×88 / 36px | 198×218 | 内容高度少 130px |
| Plan the mission, capture each target and review your recorded evidence. | 198×44 / 16px | 198×59 | 内容高度少 15px |

报告的标题 worldBound 为 `201×96`，说明为 `201×52`。原 v2 以实际 `contentRect.width` 和 `whiteSpace=Normal` 测同一文本，保留 `height+3>=required.y` 的原容差（`SpaceSubmitProbeV2.cs:56–68`）；这里不改为字体缩小、固定截断、ellipsis、overflow hidden 或容差放宽。截图看到的交叠支持真实排版缺口，而非只凭 MeasureTextSize 判定像素质量。

直接相关的静态结构：

- `DesignDashboard()`（`AeroSimApplication.DesignV1.cs:386–396`）把标题/副标题经 `DesignPanel→Panel()` 直接加到 assignment panel；说明也是其直接 Label 子元素，没有一个专门的、宽度已确定的 copy 容器。`Pages.cs:81–85`、`Application.cs:523–527` 没有设置这些 Label 的测量后高度。
- `Product.uss:146–150` 为横排 flex columns，main `.design-column` 分配剩余空间，而 `.design-side` 固定 390px、不收缩；hero 的 padding 为30px（253行）。标题36px和直接 `.wrap` 各限制 `max-width:55%`（254–255行）。
- `.design-v1 .design-assignment-description`（367行）再次约束 `max-width:55%`，且 `height:auto/min-height:52px/flex-shrink:0`。最小52px不是最终宽度下三行所需高度的保证，真实 content 只有44px。
- `Responsive.cs:156–175` 的页面 stack 门槛是 width `<1280`、height `<500` 或 Text `>1.1`；1440×900 / Text100 仍横排。`Responsive.uss:168–169` 与 `Product.uss:392` 仅在 phone/text-expanded 把 hero copy 的 max-width 放至100%。Text125起同时进入 stack，因此同一报告标题宽1049、对应45/54/72px字体行框可容纳，不是 Text100 支路已经可靠。
- 1920报告标题 content338×88/required338×88，说明338×44/required338×39；1440宽变198但标题高度仍88、说明仍44。现有“文字百分比上限 + 自然测量/自动高度”的组合没有保证最终窄宽度对应的折行高度。

**确定范围：** 三项实际失败、图像交叠、上述可见性/约束分支均有真实证据。**尚未确定：** 原报告没有 hero/content ancestor 全部 resolved width/maxWidth/flex measure pass，也未记录 Yoga 每轮可用宽度；不能仅因数字接近双重百分比，就声称已证明“Unity百分比计算两次”或特定引擎 bug。也不能把重复 USS 的55%规则说成百分比一定相乘。具体测量失配机制需最小补丁后的新冻结原生结果验证；本轮不扩展成通用布局引擎改造。

最小修复候选（未实施）：

1. 先在 **Professional Dashboard hero 局部** 将标题/说明的“只有 `max-width:55%`”改为明确 `width:55%; max-width:100%`，配合 `height:auto; flex-shrink:0`，使字体自然测量基于确定宽度；保留36px/16px基线，不给当前案例硬写218/59px。
2. phone、tablet/stack、text-expanded 路径同步将这两个 copy 元素 `width:100%; max-width:100%`；不要只沿用现有 max-width100 而让新 width55 留在大字/窄屏。类选择器需压过 `Product.uss:367` 的 description 高优先级规则。
3. 先保留 status 的390px和原布局门槛，原生验证 Text100 折行/高度/按钮后移是否关闭两项缺口。若明确 width 仍复现测量失配，再将 hero copy 放入一个非 TextElement 的确定宽度容器、子文本全宽自动高；不在一轮里同时改所有页面断点与全局 Label。
4. 不把通过 linebox 当成图稿一致：桥梁任务仍使用海岸背景、宽版全宽 CTA 等视觉偏差不是本次三项诊断已关闭的范围。23张原图必须继续独立视觉 QA，不能替换原参考。

## 4. 文件所有权与接线边界

以下是待根批准的下一波分工，不代表现已授权成员改共享文件：

| 任务 / 唯一写入岗位 | 建议文件边界 | 交接要求 |
| --- | --- | --- |
| DESKTOP-TAB-01 / 电脑页面程序员 | `Runtime/AeroSimApplication.FlightDesignV1.cs` 的 flight bar 建立块 | 07b当前字号岗位交出文件后才能改；不改字体队列、modal guard、纯输入算法或旧 QA |
| DESKTOP-HOME-01 / 电脑页面程序员（可接力） | `Resources/UI/Product.uss` 的 dashboard copy规则、`Resources/UI/Responsive.uss` 的相应宽度覆盖 | 默认不改DesignV1树、不改图片；若需copy容器，再由根单独分配`DesignV1.cs`，避免输入集成并写热点 |
| 独立电脑 QA | 新补充诊断/紧凑证据；只读原Space工具和报告 | 先冻结新增断言与source hash；原合同原样复跑，不由实现者代签 |
| 独立视觉 QA | 新候选必要截图与原23图对照记录 | 原图不改；概念/运行图分列，候选未解决和人工PENDING保留 |
| 根 / 唯一整合者 | freeze、构建、实际原生运行、进度与 GitHub交付 | 不在07b验证中途混入修复；源码SHA/Runtime DLL SHA/候选路径新记；保留06/07b和0.4 |

不要混入[三端输入 Gate A–D](V125_INPUT_INTEGRATION_PLAN.md#6-顺序与可执行验收门禁)：本页不会实现 FlightInput 委托、触点owner、校准迁移或生命周期；它们仍有各自文件所有权、逻辑/真机门禁。

## 5. 下一候选复测矩阵（本轮 NOT_RUN）

原 Space v3 将 UI/HUD 重置100，内置文本四档。不能用传入偏好或只修改输出目录伪称原合同覆盖了其他 UI/HUD倍率。其他倍率以新独立补充冻结测试覆盖。

| 门禁 | 视口 / 比例 / 场景 | 原样保留的预期与证据 |
| --- | --- | --- |
| 最小同视口回归 | 1440×900、1920×1080；UI/HUD100；原Space v3含Text100/125/150/200 | 原合同全部assert执行；1440三项应关闭、0 runtime；UNRENDERED按实际树另记、不计PASS；不硬编码1911适配所有尺寸 |
| 电脑输入规划尺寸 | 1280×720；同上原Space v3 | 输入计划既定桌面尺寸也跑原合同；五页面、advanced foldout、数字/Replay实际内容仍验 |
| HUD断点邻域 | 1559×900、1560×900；UI/HUD100 | 原Space v3各跑隔离输出，验证Objective显示切换不会改变Pause首Tab；保存焦点路径/submitDelta |
| 字号/页面断点邻域 | logical1279/1280、1919/1920宽；Home Text100/125/150/200，另 live100→200→100 | 新补充诊断记录hero父/子resolved宽高、font、MeasureTextSize和按钮间距；实际PNG检查不叠字、可滚动可点击；不得改原probe容差 |
| 焦点补充 | Aerial/FPV/Professional；Text100/125/150/200；FreshFlight、Tab/Shift-Tab、Pause/Objective开关、Enter/Esc/Gamepad恢复 | 首刻意Tab进Pause，继续导航可达Objective；模态焦点不逃逸；恢复后四Arrow→Space与空焦点Space无误操作；原v1/v2/v3同时保留 |
| 07b功能保护 | 新候选原生smoke、原Pause/Telemetry、根正在冻结的动态字体独立合同 | 全部新binary重跑，不借用07b通过；生命周期/比例/业务断言不删除 |
| UI/HUD缩放保护 | 新补充各100/125/150/200逻辑panel倍率，1280×720/1440×900/1920×1080；Text四档按风险补全 | 分开记录Screen尺寸与logical尺寸；计算出的logical桌面不当iOS真机；至少覆盖局部修复和模态路径 |
| 视觉与设备 | 新候选ProfessionalHome必要PNG→原概念图；物理键盘/外接摇杆；iPad/iPhone各线独立 | 像素/图稿一致、实际硬件输入、真机安全区/双指/后台/性能分别标状态；没有实测仍PENDING/NOT_RUN |

## 6. 本轮证据边界与额外未调查项

- 当前`FlightDesignV1.cs` SHA256为 `a77b47a0ac4d8a26527f84aa241e8af7bb980bf13e0a7616a97d0311aa8ea00e`，`Responsive.cs`为 `aeb602c63e142540b62b4a9e781ea963338afa342458e4b2086cda33bcaca305`，与[07b source freeze](../verification/v125-qa/candidate07b-source-freeze.json)相同。相关Product/Responsive/FlightDesignV1 USS及DesignV1.cs无当前Git diff；本成员没有修改它们。06的源记录与binary边界见[candidate06-summary](../verification/v125-qa/candidate06-summary.json)，不以现在的dirty源码冒充06构建源码。
- “相同”仅限定上述三项失败：两份1440报告全部1911个pass/fail模式相同、24 notes相同，但 **77条PASS evidence不同**，全部位于Aerial-Replay Text200（零基indices1549–1625）。例如06 Home font34、Play/Pause32、disclaimer24，07b三者均font28。Space行框合同不验完整token倍率，故仍PASS。已发给根字号验证岗位作为页面切换/Replay基线拓展风险；本轮没有做实运行复现或根因调查，不能据此断言是确定的新字体回归，也不能声称全部1911条evidence相同。
- 本轮只读解析JSON、核散列、读源码、实看既有原始PNG；未生成新的运行通过结果。纯InputRouter 3,050算法检查、desktop synthetic InputSystem、logical窄视口分别是不同证据，不等于物理控制器/iPad/iPhone真机，也不等于23图全面一致或用户验收。
- 本文待根检查并统一commit→push→核远端SHA；本成员没有独立提交或上传。仓库：[AeroSim](https://github.com/yijunchai2026-gif/AeroSim)；分支：[codex/v125-product-lines](https://github.com/yijunchai2026-gif/AeroSim/tree/codex/v125-product-lines)。根交付时提供实际本批commit链接，未经许可不合并main。
