# AeroSim 1.25 恢复基线：独立 QA

复核时间：2026-10-09 18:40（America/Los_Angeles）。当前已测应用版本为 **1.0.0 Candidate 05**，本报告是 1.25 的继承基线，不是 1.25 完成或用户验收证明。独立 QA 读取已有原始结果、Git diff、冻结合同与截图，运行工具单测；未启动 Unity 构建，未修改生产代码、旧合同或原始参考，未提交 Git。最新 Pause/Telemetry 原生运行由根负责人执行。

## 原始证据与实际统计

| 证据 | 原始结果 | 覆盖边界 |
| --- | --- | --- |
| `/private/tmp/aerosim-v1-smoke05-player.log` | 66 PASS / 0 FAIL | 2026-10-09 13:54 PT 的 Candidate 05 macOS 原生 smoke；模拟键盘、手柄、触控输入与真实本地模拟器/存储业务，不是物理控制器或 iOS 实测 |
| `verification/v1-qa/results/candidate-05/native/desktop-1920x1080/report.json` | 37 cases / 3945 checks / **54 FAIL** / 0 runtime | UI/HUD 100%、Text 100%；34 状态与 3 probes，不是全缩放矩阵 |
| `verification/v1-qa/results/candidate-05/native/phone-landscape-844x390/report.json` | 71 cases / 5018 checks / **61 FAIL** / 0 runtime | 桌面逻辑窗口 844×390，UI/HUD 100%、Text 100%/200%；不能代替 iPhone safeArea、多指触控或真机性能 |
| `verification/v1-qa/results/candidate-05/space-submit-v3/space-submit-v3-report.json` | **PASS_AUTOMATED_ONLY**：1911 checks / 0 FAIL / 0 runtime；24 条 UNRENDERED | 独立 1920×1080 合成输入探针；不是上述 34-state 矩阵或视觉验收 |
| `verification/v1-qa/results/candidate-05/pause-telemetry-844x390/pause-telemetry-report.json` | **FAIL**：58 checks / **26 FAIL** / 0 runtime；34 traces | 根负责人于 UTC 2026-10-10T01:39:44 实跑；Text 100%/200%，实际 Pointer/Pick/业务状态检查 |
| `verification/v1-qa/results/candidate-05/visual/summary.json` | **VISUAL_DIFFERENT 23/23**；manualAcceptance=PENDING | 23 张原始概念图与 1920×1080 实际截图比较，不能因几何无新硬失败改写为视觉通过 |

证据 SHA256 见 [机器可读基线](../verification/v125-qa/baseline-summary.json)。原始报告和全量 PNG 留在本地；紧凑基线保留完整失败统计及指纹。

### 失败归类保持原样

桌面 54 FAIL 全部是旧文字规格冲突：`legacy-prd.topbar-height-64` 27 条、`legacy-prd.nav-width` 27 条。报告仍是 54 FAIL；可区分无该 snapshot 合同的新交互/文字失败与旧规格冲突，不能称整体 PASS。

844×390 的 61 FAIL 为 60 条旧规格冲突（54 topbar、6 nav）及 1 条真实交互失败：`interaction.nested-confirm-escape`，目标为 Escape 取消确认并回到暂停层。不能把这个失败归入旧 64/240 规格。

Space v3 保留 Enter/Escape/Gamepad 三种恢复方式 × 四方向 Arrow→Space，以及刻意 Tab→Space、原始轴输入、Controller 展开、Tuning numeric 和 Replay rate 的补充检查。此次报告 1911 条全部通过；24 条闭合 `Advanced Input Mapping` Foldout 的 contentContainer 后代被记录为 UNRENDERED，**不计成 PASS**。已真实展开后另测，不豁免一般零尺寸。四种 Text 下的独立文本行框通过不代表 UI/HUD/Text 混合全矩阵、字体外观或全部滚动内容已人工验收。

### 新 Pause/Telemetry 证据：实际按钮被 Toast 遮挡

此次 58 checks 中有 26 FAIL，而不是 58 FAIL。报告记录 Exit Without Saving 与 Flight Telemetry 的按钮矩形在 ScrollView 可视区内，但 `panel.Pick(center)` 返回根层 VisualElement/Label，未命中目标 Button。点击后仍只有 1 个暂停 modal、`dialogClose=False`；确认从未实际打开，随后的 Escape 直接恢复飞行。两种 Text 均复现。

独立 QA 实看以下未编辑截图，确认 OSD 受限提示 Toast 覆盖暂停按钮：

- `results/candidate-05/pause-telemetry-844x390/Text100-nested-confirm.png`：Toast 约 x84–624/y287–362；Exit 中心 y347 落在其内，画面仍为暂停层。
- `results/candidate-05/pause-telemetry-844x390/Text100-telemetry-sixth-value.png`：画面仍为暂停层，提示覆盖 Flight Telemetry。
- `results/candidate-05/pause-telemetry-844x390/Text200-nested-confirm.png`：大字 Toast 约占 y150–362，覆盖多项暂停按钮。

六条 telemetry 值断言在每种 Text 均失败，Back to Flight 未找到；不能据此独立断言值格式算法错误，因为面板没有打开。失败与其前置 Pointer/modal 失败均原样保留。Text200 的 battery/timer 另实测 `visible=False`、`reason=osd-no-safe-slot`，因此“Pause→Telemetry 可查看全部值”入口必须先可点击。不得拿隐藏指标后零碰撞代替遥测可用性。

05 binary **已有** Flight Telemetry 按钮；不能把全部失败解释成旧 binary 缺此按钮。下一轮需要修复 Toast 指针/层级遮挡，并以新冻结构建重跑同一 probe。读信息/取消应保持 profile、record identity、原暂停状态；真实桌面逻辑窗口通过后仍需 iPhone/iPad 多指触控与安全区验证。

## 构建、源码与文档时序

已测 Candidate 05 构建：`/private/tmp/aerosim-v1-build.BPEAKU/AeroSim.app`。`unity.log` 第 3699 行记录 `Build Finished, Result: Success.`；Info.plist 的 `CFBundleShortVersionString=1.0.0`。`AeroSim.Runtime.dll` mtime 为 2026-10-09T13:54:12-0700，SHA256 为 `8e9e31398287d615bb9698cf36a869a1ee55799e4ff061b186f29321091af6ce`。

当前 `AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.FlightDesignV1.cs` mtime 为 13:59:02 PT，晚于构建及手机矩阵报告；没有冻结源码清单将当前每一字节绑定到上述 binary。因此旧测试结果只证明被测 binary 的对应范围，不能自动扩展到当前未重新构建的源码。文件 mtime 本身也不足以判断哪条函数在 binary 中不存在；本轮实际 trace 已证明 Telemetry 按钮存在。

工作区 `git diff` 包含 panel-root Arrow guard/注销、连续 mesh 渐变绕向、Replay rate 宽度、真实 Slider 内部 TextField 样式、触控摇杆布局与 OSD/暂停信息逻辑等变动。本次只读审查，不替根负责人确认这些变动全部已测。三张美术 `.png.meta` 的现有 diff 为 YAML 空值尾空格变化，不属于图像内容变化。

旧 `docs/V1_PROGRESS.md` 仍以 Candidate 03/04 为主体；`docs/V1_QA_PLAN.md`、`space-probe-v3-contract.md`、`space-probe-v3-freeze.json` 保留生成时的 NOT_RUN 说明。原始 Candidate 05 报告证明 v3 后来已实跑；原 freeze/旧报告继续保留历史含义，不回写制造通过。`pause-telemetry-probe-freeze.json` 的生成时 NOT_RUN 也已由本次新增 FAIL 证据补充。

当前生产项目 `ProjectSettings.asset` 版本仍为 1.0.0。Candidate 05 只在 `/private/tmp` 中确认存在；稳定目录仍为 `AeroSim/Builds/macOS-V1-Candidate-20261009-03/AeroSim.app`，暂不能把该路径称最新05。1.25 的试用入口须绑定新构建、版本及实际测试证据。

## 保留项、完整性与交付状态

23 个原始参考的实际 SHA256 全部匹配 `reference-manifest.json`；manifest SHA256=`54100c6a1a108827e8edf09440864ec12aace0acbde0005199c86fb4f6107799`。Space v3 与 Pause probe 的冻结源指纹均全部匹配。桌面 33 张、手机 66 张报告内 PNG 均实际存在，图册不是缺图后的推定通过。

原 0.4 本地源码分支 `codex/v04-ui-proportions` 保留于 `8460d57d8dfa7dfbb7bb248f241847fec1902924`，该分支 `bundleVersion=0.4.0`。本地可运行副本 `AeroSim/Builds/rollback-v04-ui.VdvFoW/AeroSim.app` 的 Info.plist 也为 0.4.0；本轮未覆盖它。

仓库为 [AeroSim](https://github.com/yijunchai2026-gif/AeroSim)。复核开始时 HEAD=`eb29e81bfe6f50793e1729eb55229a5a55a7f666`（Candidate 03 checkpoint），有已存在的未提交源码及图册/QA工具。根负责人现已报告 GitHub Desktop 恢复可操作并完成 branch publish，正在核验远端 SHA；独立 QA 不把旧“锁屏”描述当当前阻塞，也不以 UI 的 publish 提示代替远端验证。最终上传状态及本批 commit 链接由根负责人核实。

## 1.25 继承门禁

1. 修复已复现的暂停 Toast 遮挡；冻结源码、新构建、新 report，Pause/Telemetry probe 原 58 条全部保留，失败原因不得用直接 Invoke 或删除断言规避。
2. 冻结完整矩阵仍有 7 个逻辑视口，桌面 6 UI/HUD × 4 Text、tablet/phone 3×2。05 现有两种 snapshot 不能代表完整矩阵；其余视口、混合缩放、portrait 和设备安全区仍待验证。
3. 23 张原始概念图逐页保留，并为各设备的新 UI 建立经过用户确认的参考；几何/文本自动结果与视觉、用户最终验收分别记录。
4. 电脑真实键盘与物理摇杆/手柄，iPad/iPhone 双虚拟摇杆、多指触控、方向切换、安全区、性能须各产品线实际验证。现有桌面合成输入不足以宣布 iOS/iPad 完成。
5. 新 DJI 机型逐项由用户确认外观；待确认数据不能进入正式已批准资产。目录完整性、官方名称/年代/简介出处、审核状态存储、三端筛选和选机型真实接入需要独立门禁，不能以列表数量代替“从最早到当前无遗漏”。
6. 每批代码/文档/图稿完成相称验证，再由根负责人提交、push 并检查远端 SHA；保留 0.4，未经用户许可不合并 main。

本轮实际运行 `python3 -m unittest discover -s verification/v1-qa -p 'test_*.py' -v`：**25 tests PASS**。覆盖缺失/重复/错误合同、旧失败保留、原图锁定、掩码边界、v1/v2/v3不可变源适配。只证明 QA 工具行为，不增加应用原生通过数或视觉验收。
