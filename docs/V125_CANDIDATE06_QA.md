# AeroSim 1.25 继承修复：Candidate 06 独立 QA

复核日期：2026-10-09（America/Los_Angeles）。应用仍是 **1.0.0 开发候选06**，其修复供 1.25 继承。本次独立 QA 只读源码、原始报告和截图；未启动 Unity 构建或应用，未修改冻结探针/原始参考，未代用户确认机型，未提交 Git。原生运行由根负责人执行。

## 已实测的修复范围

| 证据 | 真实结果 | 范围 |
| --- | --- | --- |
| `verification/v125-qa/candidate06-native-smoke.txt` | **66 PASS / 0 FAIL** | macOS 原生 smoke；本地模拟器、存储及合成输入，不是 iOS/物理控制器验证 |
| `verification/v1-qa/results/candidate-06/pause-telemetry-844x390/pause-telemetry-report.json` | **74 checks / 0 FAIL / 0 runtime**，PASS_AUTOMATED_ONLY；38 traces | 844×390 桌面逻辑窗口，Text100/200，真实合成 Pointer/Pick、Escape、modal/record/profile 状态 |
| `verification/v1-qa/results/candidate-06/space-submit-v3/space-submit-v3-report.json` | **1911 checks / 0 FAIL / 0 runtime**，PASS_AUTOMATED_ONLY；24 UNRENDERED | 1920×1080 独立输入/行框回归；UNRENDERED 不算 PASS |

详见 [紧凑证据摘要](../verification/v125-qa/candidate06-summary.json)。Candidate 05 的 Pause/Telemetry 58 checks/26 FAIL 原报告完整保留；06 同一冻结探针能成功进入 nested/Telemetry 后执行原先不能执行的条件检查，故检查数成为 74。没有删旧断言或改失败为通过。

此次按钮中心的 Pick 路径实际命中 `Button#action-exit-without-saving`、`Button#action-flight-telemetry`、`Button#action-back-to-flight`。Exit 点击后 `modalCount=2/dialogClose=True`，Escape 后 `modalCount=1/dialogClose=False/paused=True`；Telemetry 打开与 Back 的业务状态也实际往返。record identity、record JSON、profile 内存/磁盘保留断言通过。六个值与模拟器独立格式化值一致；这证明当前读值/取消操作范围，不证明飞行参数真实还原全部 DJI 飞机。

独立 QA 实看六张未编辑 PNG：Text100/200 的 `nested-confirm`、`telemetry-sixth-value` 和 `telemetry-back-paused`。两种 Text 下确认框实际显示，Telemetry 的六项数据可读，返回后仍为暂停菜单，未见 Candidate 05 的巨大 Toast 覆盖操作项。

## 源码与被测构建

根冻结/构建目录为 `/private/tmp/aerosim-v1-build.9GLOTk/AeroSim.app`。其 Info.plist 保持 1.0.0，`AeroSim.Runtime.dll` mtime 为 2026-10-09T18:44:41-0700，指纹见摘要。复核时三个关联源文件 mtime 为 18:44:17 PT，早于 DLL；根负责人执行统一构建后再跑上述原生结果。摘要保存当前源指纹以供后续辨别，不能用本结果自动证明之后继续修改的源码。

源码修复包含：Toast 容器与文字 `pickingMode=Ignore`；暂停/确认/目标/Telemetry 打开时 DismissToast；暂停中不再显示瞬态 Toast；受限 OSD 状态改由暂停说明、可发现图标/tooltip 表达。根还调整紧凑 OSD 安全布局；本 probe 只覆盖指定小横屏，不能推广全部布局通过。

`PauseTelemetryProbe.cs` SHA256 仍为 `22c0c2339fe1ddb73b2bf453faa992d8b1f42591e84b53cbe9499d4d940c9694`，与原冻结文件一致。Space v3 原合同和源码未由本轮 QA 修改；旧报告不回写。

## 保留的 P1：动态弹层没有遵守 Text200

实看 Text100/200 的 Telemetry 两图：动态创建的标题、说明和六项值字号相同；背景暂停标题/按钮则明确已放大。原始 probe 提供独立量化佐证：Altitude 两种 Text 都是 `bounds=(169,162,496,35)`、`required height=27`；Distance/Horizontal/Vertical/Battery/Elapsed 两种 Text 都是相同位置、`height=28/required height=20`。nested-confirm 文字也没有随 Text200 放大。

所以 **74/0 证明值/可读行框/交互，但不能证明动态弹层 200% 字体设置已生效**。当前探针不含“新增 modal 字号=基线×Text”断言。源码在 `ShowConfirm`、`ShowObjective`、`ShowDesignFlightTelemetry` 加入新树后没有触发现有 `ApplyTypography`；这是与实际现象一致的原因判断，不能以静态阅读代替修后复测。

后续修复需在新动态树样式完成后纳入基线字体缓存/缩放，避免把已放大的字号重复当基线。冻结新候选，保留现有 probe，再新增独立字体倍率检查，并实看 Text100/125/150/200、滚动可达与焦点可见性。06 源码与结果先保留，不能在构建中途改源再套用本 PASS。

Text200 的 OSD battery/timer 在当前844×390受限布局中仍报告 `visible=False/osd-no-safe-slot`；六项值可从现在可点击的暂停 Telemetry 读取。这是有边界的降级行为，不是全部遥测始终出现在 HUD 的证明。仍需真实移动安全区和混合极限缩放验证。

## DJI 逐机型审核页的独立状态 QA

新增测试 `verification/v125-qa/test-catalog-review.mjs`，仅在独立临时目录和明确 synthetic QA 数据上点击“确认”，不会批准真实 DJI 条目。测试覆盖机型 ID 唯一、资料域/HTTPS、seed 全待审、图片字节/hash、不可信图片路径与 script 关闭标签转义；页面覆盖缺图禁确认、截至日发布资料待核条目禁确认、跨机型/旧稿hash/旧revision/旧catalog记录失效、修改意见、撤回、单机型而非批量确认、工程核收继续 pending。

整合时真实发现并报告：研究目录使用 `schemaVersion='1.0.0'/asOfDate`，模板初版使用数字1/asOf；hold状态实际在 `cutoffStatus`；Phantom 初稿族名 ID 与目录精确首代 ID 不一致。根负责人修 adapter/guard/manifest；最终运行统计与源指纹以紧凑摘要为准。测试的真实目录整合使用临时副本，避免覆盖根负责人的正式 HTML。

修复后三个差异的回归与其余状态/安全测试全部通过：`node --test verification/v125-qa/test-catalog-review.mjs` 为 **25 tests PASS / 0 FAIL / 0 SKIP**。当前真实目录 132 个基础研究条目、9 个独立外观版本，共 141 个审核项、13 个开放核查缺口；生成画廊中全项仍为 pending_user。2 个初稿仅关联 `dji-phantom-1/draft-02` 与 `dji-mini-4-pro/draft-01`，稿件散列与磁盘文件核对。本数量不代表完整性缺口已关闭。

工程仍须核收用户导出记录并重新计算当前稿件 SHA256；localStorage 与普通 JSON 导出不是认证签名。生成器会核对生成时图片字节，但当前画廊外链本地 PNG：生成之后私自替换 PNG、不重新生成 HTML，会造成显示字节与内联旧 hash 不一致。正式资产接入必须重新校验并取得绑定该稿件的用户确认；不能将网页“已记录确认”直接当工程 approved。以后可用冻结目录或内嵌图片字节进一步收紧这一边界。

真实浏览器还需验证：132 基础研究条目与各独立外观版本数量、Phantom/Mini 两个初稿正确关联，搜索和产品线/状态筛选，空结果，手机/平板/桌面缩放，键盘焦点、本机保存不可用和导出。根负责人本次尝试通过内置浏览器打开本机审核 HTML，被 `file://` 安全策略拒绝；没有改协议或换浏览器绕过。本批浏览器缩放/布局未实测，mock DOM 测试不能代替浏览器布局、图片正确性或用户批准。

## 共享输入核心的独立算法复跑

独立 QA 实际执行 `bash verification/v125-qa/build-input-router-tests.sh`：**3050 pure C# checks / 0 failures**，编译开启 warn-as-error。该脚本使用 Unity 自带编译器/runtime 编译纯算法测试，没有启动 Unity Editor/Player。输出明确写明 `not wired into FlightInput`；所以只证明 InputRouter/FlightControlFrame 当前算法范围，不代表已经接入三个设备产品线、真实屏幕摇杆或物理手柄实测。源指纹与命令保存在紧凑摘要。

## 当前结论

Candidate 06 修复已实跑范围内的 Toast 指针遮挡、nested-confirm/Telemetry 往返，并通过 Space 回归。动态弹层 Text200 仍是 P1；完整七视口/混合缩放、23原图视觉一致性、全部真实设备/物理摇杆、目录全量无遗漏证明和逐机型用户确认仍待完成。23 张原概念与原05 VISUAL_DIFFERENT 证据均保留，本轮没有06完整视觉比较，不能把05统计改称06已验证。

保留 0.4 和 1.0；本轮工作分支由根负责人维护为 `codex/v125-product-lines`。最终提交/推送/远端 SHA 由根负责人核实，独立 QA 不声明自己已上传。仓库：[AeroSim](https://github.com/yijunchai2026-gif/AeroSim)。
