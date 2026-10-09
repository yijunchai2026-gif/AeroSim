# AeroSim

本地 Unity 飞行模拟器项目。主干为 `main`；首次提交保存 2026-10-06（America/Los_Angeles）当前工作区源码快照。

## 项目结构

- `AeroSim/Assets`：运行时代码、编辑器工具、场景、Shader、UI 与美术资源。
- `AeroSim/Packages`、`AeroSim/ProjectSettings`：Unity 依赖与项目设置。
- `assets/`：产品概念效果图、0.4 精选实际截图（`ui-v04/`）与历史截图（`ui-history/`）。
- `飞行模拟器产品设计文档.md`：产品与 UI/UX 设计要求。
- `verification/ui-qa-tools/`：缩放 QA 工具；验证目录保留紧凑摘要。

使用 Unity Hub 打开 `AeroSim/`，编辑器版本见 `AeroSim/ProjectSettings/ProjectVersion.txt`。

## 1.0 开发中（2026-10-08）

工作分支为 `codex/v1-design-parity`，不覆盖 0.4 试用包、不自动合并 `main`。技术、页面、独立 QA、场景开发与两名美术已分工交付首轮实现，正在真实 Unity 构建和逐页面对照；尚未宣布视觉一致或用户验收通过。目标是产品文档中 23 张原始概念图，而不是重新制作参考图迁就代码。

见[产品文档第 24 章](飞行模拟器产品设计文档.md#24-10-多角色开发与图稿对齐2026-10-08)、[技术计划](docs/V1_TECHNICAL_PLAN.md)、[页面规格](docs/V1_SCREEN_SPEC.md)、[QA 计划](docs/V1_QA_PLAN.md)。新环境和透明设备资产随文档入库，实际飞行仍由实时 3D 渲染。

隔离构建入口 `AeroSim.Editor.ProjectSetup.BuildDesignV1Candidate` 要求 `AEROSIM_V1_BUILD_PATH=/private/tmp/aerosim-v1-build.XXXXXX/AeroSim.app`。`verification/v1-qa/` 包含独立截图比较与原生比例测试工具；测试输出被忽略，精选证据另外入库。iOS 真机、Google Maps/实时空域与教员多人服务不能据桌面页面宣称已完成。

2026-10-09 开发候选 03：[23 页原图/原生图册](assets/ui-v1/candidate-20261009-03/gallery.md)、[66 项原生 smoke](verification/v1-qa/candidate-03-native-smoke.txt)、[续作断点](docs/V1_PROGRESS.md)。候选尚有视觉、缩放和焦点缺陷；不是最终 1.0 或已验收版本。每小时断点检查在额度恢复后续作，不购买额度或自动使用重置券。

## 0.4 UI 比例修复（2026-10-07）

修复工作在 `codex/v04-ui-proportions` 分支中，未自动合并主干。

- 恢复同一任务原有的 AircraftCatalog、AircraftVisualModel、Hangar 六机型源码，统一项目和构建版本为 **0.4.0**。
- 设置页新增 UI Scale、HUD Scale 独立保存，以及 100/125/150/200% Text Size，立即生效。
- 64px 顶栏；240/72px 导航；居中内容最大 1600px；按逻辑窗口宽度设置边距。小窗口与放大文字使用重排和滚动。
- HUD 48px 顶栏 / 56px 底栏、18px 主数值 / 12px 次标签、240×180 小地图；不安全的 OSD 锚点回退至安全槽位，保护中央视野。
- 暂停/确认弹窗使用有界滚动区，桌面触控摇杆按需显示。放大文字时辅助 HUD 收起，Objective 按钮仍可打开任务内容。
- OSD 编辑预览对越界位置进行显示夹限；过窄时有冲突的项目转为可读排列，不因窗口尺寸变化改写已保存锚点。
- 实际截图验收页：`AeroSim-0.4-UI比例修复.html`。完整结果：`verification/ui-scale-qa-v04-ui-fix-20261007-release/summary.json`。

此轮修复比例与可达性，不声称所有页面信息架构已经逐像素复刻概念图。手机/平板为桌面逻辑视口探测，未完成 iOS 真机验证；文字测量候选仍需结合实际截图人工验收。

完整矩阵：13 组、1,718 状态，几何/设计数值/交互/回归失败均为 0，运行错误为 0；原生六机型与飞行回归 120 通过 / 0 失败。保留 8,551 条文字测量候选，不把这些候选或尚未人工复核的状态宣称为全面视觉验收通过。200% 全页面文字压力测试仅覆盖 1280 和 1920 宽度下的 100% UI/HUD 倍率，未覆盖全部混合倍率组合。

初次主干快照 `a76fb8b` 存在源码/交付件不一致、0.5 项目版本和缺失多机型脚本问题；历史 QA 对应当时被测应用，不能作为新构建证明。

## 重建与验证

常规 0.4 构建入口：`AeroSim.Editor.ProjectSetup.BuildAircraftPreview`。隔离候选入口 `BuildUiCandidate` 要求 `AEROSIM_UI_BUILD_PATH` 指向新建的 `/private/tmp/aerosim-ui-build.*/AeroSim.app`，便于不覆盖当前试用版进行验证。

`bash verification/ui-qa-tools/build-qa.sh ABSOLUTE_CANDIDATE_APP` 复制并注入临时 QA 应用；随后将返回的临时 app 路径传给 `run-qa.sh`。测试使用独立 `--data-dir`。`node verification/ui-qa-tools/aggregate-ui-fix.mjs` 生成本轮截图报告。旧 `aggregate.mjs` 仅用于历史基线，不能用于修复版。

## Git 与本地产物

Unity Library、Builds、Logs、UserSettings，以及自动生成的 QA 截图、原始大体积报告和运行日志不纳入 Git，原文件仍保留在本机。源码使用的美术贴图与设计参考图片纳入 Git。

本地 HTML 图册引用的部分 QA 截图属于被忽略的生成产物，单独克隆源码后不会自动包含这些截图；需保留原本的本地验证目录，或在源码与构建一致后重新生成。部分历史文档指向旧版本，应以本说明的当前基线差异为准。

产品文档已更新至 v1.4：[第 23 章](飞行模拟器产品设计文档.md#23-04-ui-比例修复效果图与验收2026-10-08)直接内嵌 30 个 0.4 页面/状态、2 个机库滚动视图和 6 组比例对照。23 张概念图与 37 张历史截图一并保留，所有文档内嵌图片都随仓库提交，在 GitHub 可直接显示；不需要本机 `verification` 图片目录。概念图、运行截图和未验收限制分别标注。`publish-design-gallery.mjs` 从原始本机 QA 归档复制精选图，不重新生成截图。

后续代码/文档更新按“验证 → commit → push → 返回 GitHub 地址与具体提交链接”交付；不自动合并主干。完整约定见 [AGENTS.md](AGENTS.md)。

2026-10-07 已通过 GitHub Desktop 发布到私有仓库 [yijunchai2026-gif/AeroSim](https://github.com/yijunchai2026-gif/AeroSim)。本地 `main` 已连接 `origin/main`。构建产物和自动生成的 QA 大文件仍只保留在本机，未随源码上传。
