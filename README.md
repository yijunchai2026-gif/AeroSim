# AeroSim

本地 Unity 飞行模拟器项目。主干为 `main`；首次提交保存 2026-10-06（America/Los_Angeles）当前工作区源码快照。

## 项目结构

- `AeroSim/Assets`：运行时代码、编辑器工具、场景、Shader、UI 与美术资源。
- `AeroSim/Packages`、`AeroSim/ProjectSettings`：Unity 依赖与项目设置。
- `assets/`：产品设计参考图片。
- `飞行模拟器产品设计文档.md`：产品与 UI/UX 设计要求。
- `verification/ui-qa-tools/`：缩放 QA 工具；验证目录保留紧凑摘要。

使用 Unity Hub 打开 `AeroSim/`，编辑器版本见 `AeroSim/ProjectSettings/ProjectVersion.txt`。

## 当前基线的已知差异

本次 Git 提交是**当前工作区快照**，不是经过重新构建验证的 0.4 发布源码：

- 本地 `AeroSim/Builds/macOS-AircraftV3/AeroSim.app` 的版本为 0.4.0。
- 当前 `ProjectSettings.asset` 中 `bundleVersion` 为 0.5.0。
- 当前 `ProjectSetup.cs` 中配置方法将版本设置为 0.3.0。
- 当前存在部分仅剩 `.meta` 的多机型脚本项（如 AircraftCatalog、AircraftVisualModel、AeroSimApplication.Hangar）。

本轮只建立版本管理，没有恢复代码、修复 UI、修改版本号或重新构建。历史 QA 结果对应当时被测的应用，不作为当前源码可复现构建的证明。

## Git 与本地产物

Unity Library、Builds、Logs、UserSettings，以及自动生成的 QA 截图、原始大体积报告和运行日志不纳入 Git，原文件仍保留在本机。源码使用的美术贴图与设计参考图片纳入 Git。

本地 HTML 图册引用的部分 QA 截图属于被忽略的生成产物，单独克隆源码后不会自动包含这些截图；需保留原本的本地验证目录，或在源码与构建一致后重新生成。部分历史文档指向旧版本，应以本说明的当前基线差异为准。

2026-10-07 已通过 GitHub Desktop 发布到私有仓库 [yijunchai2026-gif/AeroSim](https://github.com/yijunchai2026-gif/AeroSim)。本地 `main` 已连接 `origin/main`。构建产物和自动生成的 QA 大文件仍只保留在本机，未随源码上传。
