# 候选08：Replay 与 Flight 原图视觉对照

日期：2026-10-10。角色：第四波第二名 UI 美术/只读视觉对照；只写本文，不改生产源码、23 张原始图稿、旧报告、Unity 构建、原生运行或 Git。完整阅读 `AGENTS.md`、`V125_PROGRESS.md`、产品文档第18章规则及18.8/18.11后，逐张实际打开以下 PNG，而不是依据文件名或自动检查推断视觉。

结论：**Replay 仍为 `VISUAL_DIFFERENT`；Flight 默认 HUD 目视为 `NOT_RUN`，当前没有本角色已目视的候选08正常默认 HUD 截图，不签视觉通过。** 本波重复单位局部改善真实可见，不等于整页复刻、23图全面一致、三端实测或用户验收。

## 版本和目视范围

- 原目标保持：[航拍训练 HUD](../assets/aerosim-us-flight-training-ui-v1.png)、[Replay & Analysis](../assets/aerosim-us-15-replay-analysis.png)。它们是原创概念图，不是当前应用截图，不改参考来制造通过。原图按等比 contain 比较，不非等比拉伸或裁掉差异。
- 候选08应用仍为 **1.0.0 开发候选**，为1.25继承；根负责人提供的冻结 Runtime DLL SHA256：`0aec831cff961caafa77a38f90bdf0335ff4026b817ccde9fd71b8ed21e99f9f`。本角色没有重新构建或原生运行，不把根提供的构建身份说成本角色独立 binary 验证。
- 目视候选08：macOS 逻辑窗口1440×900、1920×1080；UI/HUD100%，Replay Imperial Text100/200各两图，Metric Text100两图。另目视07c同补充合同的 Imperial Text100两图作重复单位前后对照。
- 这些是隔离 QA 夹具：页面明确 `QA split-unit fixture — NOT pilot achievement · Score 0`。20秒、39ft/12m等数值为测试数据，不能当真实用户飞行、地图、成绩或DJI遥测。
- 根提供的两视口合计补充435检查/0失败/0运行错误（1440×900为215检查，1920×1080为220检查）只是局部自动门禁。本角色不据此签几何、完整交互、物理设备或视觉验收。Text200首屏未出现的区域没有目视证据，不计目视通过。

根已选出两张本报告涉及的可提交实际截图：[1920×1080 / Text100 Replay](../assets/ui-v125/candidate-20261010-08/replay-1920-text100.png)、[1440×900 / Text200 Replay](../assets/ui-v125/candidate-20261010-08/replay-1440-text200.png)。本角色以 `cmp` 和SHA256核对，两图与下表对应原始PNG字节一致；它们是实际候选08，不是新概念目标。提交/push是否完成由根另核实，这里仅提供仓库内相对路径，不承诺认证阻塞时GitHub已可访问。

## 重复单位：确实修好的是这个局部

| 实际首屏 | 07c 原截图 | 08 原截图 | 本角色目视结论 |
|---|---|---|---|
| Imperial Text100，1440和1920 | `39 ft`旁再有`ft`；`8.9 mph`旁再有`mph`；`92%`旁再有`%` | 数字分别`39`、`8.9`、`92`，各旁只有一份`ft`、`mph`、`%` | 三行重复后缀消除，两视口均直接可见 |
| Metric Text100，1440和1920 | 本角色未打开07c对应Metric图，不推断旧画面 | `12 m`、`14.4 km/h`、`92 %`各一份单位 | 08三行直接可见，数字与单位仍为分开的标签 |
| Imperial Text200，1440 | 未作旧图Text200目视 | 首屏只有预览和播放器，遥测已移至下方 | 三行单位 `NOT_VISIBLE`，不能据首屏证明全部三行 |
| Imperial Text200，1920 | 未作旧图Text200目视 | 首屏底部能见Altitude `39 ft`；其余遥测在下方 | 高度单份可见；速度/电池 `NOT_VISIBLE`，不作目视通过 |

此修复是排除重复单位，不是证明所有遥测数值/历史曲线正确。尚未目视Text200滚动后的全部遥测、Imperial/Metric动态切换、非QA真实记录和所有入口；独立QA的UI树语义检查另有范围，不与目视混用。

## Replay 与原概念的剩余偏差

### 构图和信息密度

原图在一屏容纳海岸预览、右侧三曲线与双杆输入、完整事件时间线、事件说明和第二层播放控制。08在1920/Text100保留左预览/右遥测/下时间线的大方向，但右卡很高、下方留白和卡片高度明显不同；首屏只有空时间线说明与底部按钮的局部，未形成原图那种完整工作台密度。1440/Text100导出按钮另占一行，时间线不在首屏，右卡下端靠近窗口底边。不能只因标题、三曲线和卡片都存在就称布局一模一样。

08的Text200转成“预览→遥测→时间线”纵向结构；1440首屏完全没有遥测，1920只露高度区；左导航 `Flight Records` 自动换两行。当前所见没有把字体缩小或省略来掩盖这些变化，大字号可读性和一屏工作台密度是不同问题。原23图没有Text200对应概念状态，不能据此宣称大字号原图一致；截图能见右侧滚动条，但本角色没有执行滚动、焦点和全部内容可达验证，不能把不在首屏等同已验证可达。

### 主场景与分析叠层

原图是细节丰富的海岸地貌、海浪/建筑/植被与近景路径，可清楚见蓝色→黄色警告→绿色返航轨迹、航向箭头和 `H` 家点。08当前为山地草坡、重复树体、稀疏石块和较平的天空/地表纹理；首屏没有对应轨迹、警告路径或家点叠层。场景题材、材质细节、光照层次、空间叠层都仍不同。这个判断仅来自截图，不推断渲染管线、LOD或资产授权原因。

### 图表和双杆区

08保留原图的高度蓝、地速绿、电量黄配色方向，网格和竖向游标也存在，数字/单位此波恢复清楚。原图的曲线具有起伏、衰减和纵轴刻度；当前夹具显示近线性升高/缓降，截图未见原图的纵轴数值标尺。夹具20秒和原图2:26不是同一飞行，不能拿曲线形状差异直接证明真实数据算法错误；但视觉目标中的轴标尺、单位层级与可读性仍未复刻。

原图两个摇杆旁有 `Left Stick`/`Right Stick` 和逐轴数据；08两个圆杆下把四轴挤成两行文本，缺少相同的左右杆标签编排。零输入来自夹具，不证明真实摇杆或外接控制器已经可用。

### 状态、主动作和控制层级

原图预览顶部 `Pilot View` 为蓝色选中段；08三枚视角按钮均为相近的深色独立按钮，本组截图未见同等级的明显选中态。预览底部原图为圆形暂停与前后控制；08为 `−10s / Play / +10s` 三个矩形按钮。原图时间线有四种事件图标、彩色线段、当前警告说明、蓝色 `Practice from Here` 主动作及下层播放速度/音量；08夹具显示 `No events recorded for this session` 与深色 `Practice Again / Add Note`。空夹具不能证明真实事件功能缺失，但这些截图没有证明原图非空事件、定位练习和控制层级已达到目标。

原图顶栏有Ready to Fly、GPS、Link、电量/分钟状态。08为 `Local Training / Offline · No aircraft connected`，这是真实离线边界表达，不能为了像图而伪造卫星/实体设备连接。但产品仍需明确模拟状态与真实硬件状态如何区分，并按原图的信息层级实现可信的训练状态，而不是把离线声明当原图一致。

### 颜色和表面

08有深蓝底、白字、蓝/绿/黄分析色，方向一致；原图的玻璃/渐变背景、导航蓝选中块、描边亮度、按钮主次关系更强。08背景更平、边界更细、主要动作普遍深色；原图浅蓝海岸预览与深色曲线卡的对比、底部高密度事件带仍未达到。未做逐像素差分或计算配色误差，因此本段是实际目视判断，不编造百分比相似度。

## Flight 原图检查与待补范围

已实际打开航拍HUD原图：全幅海岸场景、顶栏课程/可飞/GPS/Link/电量/Pause，左中Return-to-Home提示与高度按钮，左下圆地图，右竖向摄影工具，底部H/D/H.S/V.S四组遥测。第18.8旧文字示例有公制值，而美国原图为ft/mph；这里按用户原图优先要求，以原图英制构图作为视觉目标，不把旧文字数值改成新通过标准。

当前本角色尚未收到/目视候选08**正常默认Flight HUD**截图。`Aerial-Text100-first-Tab-Space.png`是暂停动作后的截图，不能拿暂停框当默认Flight效果替代；Replay预览也不能替代Flight HUD。故默认HUD单位、警告遮挡、地图/摄影工具、底栏密度、Text200默认HUD、实际双虚拟杆在本报告均仍待目视/设备验证。本波键盘Pause局部门禁由独立QA另记录，不能补签Flight视觉一致。

## 截图与原目标指纹

以下原始QA路径是**本机被忽略、不上传的原始证据**，在工作目录可复核；它们用纯路径列出，不作GitHub图片链接或承诺全量PNG上传。原目标仍为仓库资产；只有上方两幅精选实际截图由根纳入本批提交范围。原始路径相对仓库根，均已实际打开。

| 文件 | SHA256 |
|---|---|
| `assets/aerosim-us-flight-training-ui-v1.png` | `9629a175214a232617c618d5987edfce98deb0721f4191ec3fc0d6f0541db97c` |
| `assets/aerosim-us-15-replay-analysis.png` | `192cd3a041761c4e0a61ecac80da26b445128542f152201fbe6c8561a518cf1e` |
| `assets/ui-v125/candidate-20261010-08/replay-1920-text100.png`（对应已目视原始PNG的字节一致副本） | `c5c1cc8761f15922e6c712649504bdc5a5eff4ee111003e2498c6c69b04d962a` |
| `assets/ui-v125/candidate-20261010-08/replay-1440-text200.png`（对应已目视原始PNG的字节一致副本） | `9a504c9918e95e20abb721be1e8ba063ca5a4edf339abf554add4fa956c8fa3d` |
| `verification/v1-qa/results/candidate-07c/desktop08-v1-1440x900/Replay-Imperial-Text100.png` | `e48c8d08da7bd7c9fb48ba05ab0092b2b8ce41a9cb7d60df767aaeb0673fd8b1` |
| `verification/v1-qa/results/candidate-07c/desktop08-v1-1920x1080/Replay-Imperial-Text100.png` | `94803cc776aa753c3d6daad174ee98a4e8a0480623ec08adac9d639191444483` |
| `verification/v1-qa/results/candidate-08/desktop08-v1-1440x900/Replay-Imperial-Text100.png` | `aec082223f34353584720a616486c999ea5bbdecc6d79765aa1bb08838750597` |
| `verification/v1-qa/results/candidate-08/desktop08-v1-1440x900/Replay-Imperial-Text200.png` | `9a504c9918e95e20abb721be1e8ba063ca5a4edf339abf554add4fa956c8fa3d` |
| `verification/v1-qa/results/candidate-08/desktop08-v1-1920x1080/Replay-Imperial-Text100.png` | `c5c1cc8761f15922e6c712649504bdc5a5eff4ee111003e2498c6c69b04d962a` |
| `verification/v1-qa/results/candidate-08/desktop08-v1-1920x1080/Replay-Imperial-Text200.png` | `92a2f1c704e13bd455bf38b3531bfa91fe4e602eaee7940918f44e6f6c655074` |
| `verification/v1-qa/results/candidate-08/desktop08-v1-1440x900/Replay-Metric-Text100.png` | `655fdd7d0df69e345c43c156a75645a783d8a7a347149663036ed15d1aa97980` |
| `verification/v1-qa/results/candidate-08/desktop08-v1-1920x1080/Replay-Metric-Text100.png` | `73ecc51ea6ac085ea4e175f908d1c0e9b232020844b015cd8c5a554a8022b023` |

后续应先以同一冻结binary保留失败/未目视范围，再补非空真实回放夹具、Text200滚动和默认Flight截图，按原图安排视觉修复。没有iPhone/iPad真机、物理控制器、真实地图/空域或最终用户确认，不以本报告替代这些门禁。根统一提交和实际push/远端SHA核验；本成员没有Git写操作，没有把认证阻塞的分支说成已上传。
