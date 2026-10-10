# AeroSim 1.0 历史断点与续作

最后更新：2026-10-09（美国洛杉矶时间）。本文件是开发断点，不是完成或人工验收证明。

最新授权已扩展为 1.25，多产品线、逐机型用户确认和最终试用链接见 [V125_PROGRESS.md](V125_PROGRESS.md)。下面候选03/04条目是历史证据；候选05实际结果见 [独立基线 QA](V125_BASELINE_QA.md)，最新原生候选06仍为1.0.0，已测修复与保留问题见 [独立06 QA](V125_CANDIDATE06_QA.md)。1.25首批代码已在 `codex/v125-product-lines` 推送，恢复时从1.25断点继续，不按旧文档重复已完成工作。

## 当前状态

- 候选03分支 `codex/v1-design-parity`，保留原 0.4 分支与应用；提交 `eb29e81bfe6f50793e1729eb55229a5a55a7f666` 已于本次恢复后通过 GitHub Desktop 推送，UI `push complete`，远端分支为同一 SHA。终端 HTTPS 仍缺认证，桌面登录可用。当前 1.25 分支为 `codex/v125-product-lines`。GitHub：<https://github.com/yijunchai2026-gif/AeroSim>。
- 技术负责人已审查 23 页真实能力缺口；UI 程序员、独立 QA、环境/设备/海岸美术已分波次参与。额度恢复后复用原成员与文件分工。
- 已新增 1.0 原生页面壳、HUD、真实回放视口/图表、工业/桥梁场景细节、照片证据可靠性及三类美术资产。所有能力仍须逐项验证。
- 最新已运行候选 03：`/private/tmp/aerosim-v1-build.v1uVO6/AeroSim.app`，本机稳定副本 `AeroSim/Builds/macOS-V1-Candidate-20261009-03/AeroSim.app`。原生 smoke **66 PASS / 0 FAIL**；1920×1080、UI/HUD/Text 100%：37 cases / 3926 checks / 55未通过（54旧规格冲突+1文字候选）/ 0 runtime errors。
- 飞检 Toggle 越界与原生 Space 勾选/键盘启动已修，Controller专属壳与Home首屏布局已捕获。独立Space探针54检查仍3 FAIL：恢复后DownArrow会把none焦点移到Pause；没有直接证明误暂停，应作为待修后继风险。
- 1280×720/Text200：37 cases / 2978 checks / 90未通过（1 OSD位置回归、35文字候选、54旧规格冲突）/ 0runtime。另独立QA确认Tuning五个numeric内框实际仅8px高、有裁切，冻结Label/Button门禁未覆盖。不能把“仅1文字候选”扩大成所有文字无裁切。
- 产品文档与开发对照 HTML 当前为候选 03 的全部23真实截图；01/02图册为历史失败证据。23原图仍全部VISUAL_DIFFERENT，完整全量缩放矩阵尚未完成，用户未验收。

## 下一步：候选 04 稳定化

1. 候选03与真实失败证据已保存为上述提交且已上传；后续候选仍不视为最终1.0。旧候选04/05检查结果以新的独立基线 QA 为准。
2. 根修仅非暂停Flight下的Arrow KeyDown默认焦点移动，仍保留原始飞行输入和Tab；修紧凑OSD忽略安全保存锚点。页面成员修Text200固定文字框、ProHero行框、Tuning内部numeric裁切与Home渐变实效。
3. QA 保持候选02的34截图状态+3 probes契约冻结；独立probe补Arrow→Space实际动作风险。所有原始参考图/阈值保持不变。
4. 源码冻结后构建04，66 smoke、独立Space、1920、1280/Text200和手机逻辑视口先过实际缺陷门禁，再完整矩阵；单列文字候选和旧文字规格冲突，不删除失败制造通过。
5. 发布最新23图/PRD/紧凑结果，commit→push验证远端SHA。不自动合并main。之后按技术审查有限包补五步引导/设置草稿、圈赛等真实功能。

### 候选04正在验证

- 已冻结源码并完成Unity原生构建：`/private/tmp/aerosim-v1-build.4FCI7Q/AeroSim.app`。
- 原生smoke完成66 PASS / 0 FAIL，独立v2输入/文字探针正在运行。尚未运行04完整矩阵，不能宣称比例已全部通过。
- 原34状态契约不修改；独立v2保留旧断言并增加12组真实Arrow→Space、刻意Tab→Space、Slider内部TextElement行框和100%/200%截图。工具21单测通过，不替代应用结果。
- 页面与技术成员只读准备下一波五步引导/设置事务，不在冻结构建中途改变源码。

## 续作约束

- 不覆盖或删除原 0.4，不合并 `main`，不 force-push，不提交凭据/缓存/全量原始矩阵。
- 恢复时先读 AGENTS.md、Git 状态、当前成员状态和本文件；不要重复已完成的素材生成或原图清单。
- 额度耗尽时保留断点；本任务已设置每小时 heartbeat（`aerosim-1-0`）尝试续作，不购买额度或自动消耗重置券。续作受可用额度、Codex 运行与主机状态影响。
- Google Maps 计费/密钥、最新全美限制区、真实 DJI 控制器认证、iOS 真机/发布与用户最终验收仍是独立条件；不拿示意图、桌面窄屏或版本号替代。
- 完整剩余范围见 [技术范围审查](V1_SCOPE_REVIEW.md)。本地可做的五步引导、草稿事务、六赛道圈赛、逐轴调参、八目标任务、编辑器、PDF、真实教员 LAN 会话不得因外部服务未配而跳过。
