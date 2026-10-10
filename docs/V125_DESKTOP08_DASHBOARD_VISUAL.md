# 1.25 第四波：Professional Dashboard 独立美术对照

日期：2026-10-10（America/Los_Angeles）。职责只限专业首页的图稿/运行截图视觉对照；Flight/Replay 交给另一位美术。本成员只新增本文，不改生产源码、23 张原始概念图、旧 QA 合同、旧截图或确认记录；未启动 Unity/native 应用，也不执行 Git 写入。根负责人负责实际构建、取图、统一提交和上传。

## 1. 当前结论与证据边界

- **候选08四张首页图已实看：局部叠字关闭，整体仍VISUAL_DIFFERENT**。根交接实际08隔离原生结果后，本成员核PNG散列并用 `view_image` 打开1440/1920、Text100/200四图；没有依据补丁源码推断结果。1440/Text100标题恢复为可读两行，和副标题/说明分离；桥梁任务海岸背景、过高hero、全宽CTA、专业导航及首屏训练计划密度仍与原图不同。
- **07c历史基线：VISUAL_DIFFERENT**。本成员使用 `view_image` 实际打开下面四张07c原生截图，以及第10张原始目标；未使用03/04历史图冒充07c。
- 已确认07c的1440×900 / Text100标题、任务副标题和说明互相覆盖；1920×1080 / Text100可读，但背景题材、首屏内容分布、CTA宽度和业务信息层级仍显著不同。
- 这些是独立人工看图观察，不是本成员执行的新原生交互结果、全缩放矩阵、像素误差指标、物理输入或iPad/iPhone实测。根另执行的新原生探针结果有下面明确来源，不能由截图代替。Text200首屏内容被滚动窗口截断，不等于内容缺失；本成员尚未验证下滚、触点和按钮可点击性。
- 几何/字体测试通过不能代替原图一致性。最终用户验收 **PENDING_USER**。逐机型 `pending_user` 审核位图与本页 UI 对照是不同门禁，本文不为任何机型批准外观。

## 2. 固定参考与实际读取的07c图

视觉目标：[原始专业学员仪表盘](../assets/aerosim-us-10-professional-dashboard.png)，SHA256 `78795b88ace57024e8a2c198bda99b4e02cad84dea3ca7047700e22ccefaaf64`。原图1672×941；按 [页面规格](V1_SCREEN_SPEC.md) 等比例 contain 到目标视口，不横纵分别拉伸、不裁掉偏差。本次没有生成修改版参考或对齐叠图，不声称做了定量像素比较。

原图第18.10.1节的结构是：问候与说明 → 左侧下一任务/右侧资格状态 → 横向紧凑训练计划 → 下方技能复习/最近报告。第24.1节明确原图优先于旧64px/240px文字规格；顶栏约88px、侧栏约248px是1920等比例对齐后的约值，不把旧文字冲突记成新回归。

| 已实看状态 | 原始本地PNG | SHA256 |
| --- | --- | --- |
| 07c / 1440×900 / UI100 HUD100 Text100 | 本机原档`verification/v1-qa/results/candidate-07c/space-submit-v3-1440x900/text100-Professional-Home.png` | `d5a6334270bf4346de720de62a238a2cea4107054292ea132f08e22aaefe46b2` |
| 07c / 1920×1080 / UI100 HUD100 Text100 | 本机原档`verification/v1-qa/results/candidate-07c/space-submit-v3-1920x1080/text100-Professional-Home.png` | `a0683be964bcbd5722d41402a42ea3cdfe0df2a80fca189b19825436258fdf1a` |
| 07c / 1440×900 / UI100 HUD100 Text200 | 本机原档`verification/v1-qa/results/candidate-07c/space-submit-v3-1440x900/text200-Professional-Home.png` | `224c3c111eb543ccb7ef5db3a3d7555706843a736a1f8a8ffcb58ed925f56c34` |
| 07c / 1920×1080 / UI100 HUD100 Text200 | 本机原档`verification/v1-qa/results/candidate-07c/space-submit-v3-1920x1080/text200-Professional-Home.png` | `d2341268885bb13e8490c4fcbd1a6be8ed78a32dc5ed180ff3c67b125afb70c1` |

07c应用版本仍1.0.0开发候选，当前1.25继承的桌面基线；原始Space v3主动设置UI/HUD100、遍历Text100/125/150/200。此表只实看两端文字档位，不能扩成四档全部已人工看图。二进制、源冻结和探针状态见 [07 QA](V125_CANDIDATE07_QA.md)；此处不套用其计数成为08结果。

## 3. 原图观察与07c差异：后续可执行项

| 范围 | 原图目标 / 07c实际可见差异 | 最小后续建议 / 本波边界 |
| --- | --- | --- |
| 任务标题可读性 | 原图两行标题，下方期限/时长/进度/说明彼此分开；07c1440 Text100标题窄列多次折行，侵入副标题与说明，CTA上方成为重叠块 | 08同状态实看恢复两行，标题/副标题/说明/CTA不叠字。新报告title36px、description16px基准未缩小，测量见第4节；只关闭这一局部，不扩大为原图一致 |
| Hero场景与文字对比 | 原图是桥梁、桥墩、河面及小无人机，左侧深色渐变承托文案；07c是亮海岸风景，标题虽说Bridge Inspection，但无桥梁，说明直接压在树木/浅色水面上 | 为专业桥梁任务单独指定匹配环境背景及左侧渐变层；保持真正任务场景/静态菜单图的标签区别。当前不得把新背景冒充可飞行地图，也不可用未确认DJI机型稿作为正式3D资产 |
| 首屏信息密度 | 原图任务卡/状态卡之后完整训练计划三行，再完整技能与报告面板；07c1920 Text100任务卡约516px高，首屏训练计划仅见一行，技能/报告未见；1440 Text100只露Training Plan标题 | 后续单独冻结1920 Text100桌面密度合同，按原图收紧hero垂直空间与卡片间距，训练计划行式布局保持紧凑。不把Text200也强塞原图全部首屏；大字应诚实滚动，待真实滚动测试 |
| 主CTA视觉权重 | 原图Continue Assignment是任务卡左下的内容宽按钮；07c Text100是几乎hero全宽的蓝色带，横贯明亮海岸 | 桌面Text100设置局部按钮内容宽/最大宽并保持触点；窄屏/大字按响应式全宽。此轮只修叠字与焦点，不宣称按钮宽度已改变 |
| 任务元数据与进度 | 原图有Due Today、到期时间、45min、60%条；07c只有NEXT TRAINING TASK及一般说明 | 用真实本地分配/进度模型填这些位置，未有到期时间或进度时给明确无数据/未开始态；不能硬写参考60%/日期形成假训练成绩 |
| 状态卡内容层级 | 原图有三条紧凑资格行、状态标签和详情箭头；07c大行距的Controller Input/Calibration/Records且明确Local simulator readiness | 可对齐行高、分隔线、标签和详情入口，但保留真实校准/记录值及“不是专业资格”的边界；没有合法资格数据不能复制Valid、Safety Briefing Complete或考试资格 |
| 专业导航 | 原图5项：Dashboard/Training Catalog/Missions/Exams/Reports；07c8项，Home/Free Flight/Challenges/Hangar/Editor/Instructor/Records等跨工作台导航混排 | 后续专业外壳重排为原图的信息架构，并将可实现功能保留可达次级入口。未实现考试/教员多人不能增加假完成态；本波不重做导航 |
| 顶部状态与工作台入口 | 原图有Professional Training下拉、Ready to Fly/GPS/Link/Battery；07c只显示Local Training与Offline · No aircraft connected，大字时这些收起 | 原图下拉/状态的视觉位置可迁移；离线状态必须继续是真实措辞，模拟遥测明确标记Simulated。不要为像素复刻硬写真实GPS、无线链路、飞行许可或连接飞机 |
| 色彩/配图/图标 | 原图深蓝梯度面板、图形图标和小预览图形成精密训练感；07c深色平面外壳及大明亮风景，信息层更像航拍游戏首页 | 色彩对比和图标一致性应单独确认，后续配图按专业任务类别缩略图设置。此处为看图质性差异，无采样色值、WCAG仪器或像素误差量化结论 |

可先修真实叠字，再关闭hero题材/文案承托与Text100密度三个局部视觉项；后续导航/资格语义须由产品线负责人单独冻结，不与共享输入Gate A–D混写。

## 4. 候选08真实截图对照

应用仍为1.0.0开发候选。以下来自根运行的冻结 `independent-desktop08-v1` 探针，UI100/HUD100、panelScale1；本成员只读报告、核PNG散列和实际看图，未自行启动运行。两份报告均为 `PASS_AUTOMATED_ONLY`，但其范围只含局部焦点、hero最终行框/不相交、Replay单位语义，不是图稿验收。

| 新候选状态 | 实际PNG / SHA256 | 本成员实际看图结果 | 原图一致性 |
| --- | --- | --- | --- |
| 08 / 1440×900 / Text100 | [精选原字节PNG](../assets/ui-v125/candidate-20261010-08/home-1440-text100-after08.png) / `990ce08b81c6ce8d9bcc34e3258f844f4ce1ad3ab27cafe52c60f84b5594f9f8` | 先前标题/副标题/说明叠字已关闭；标题两行，CTA完整可见。仍是海岸背景、约517px高hero、全宽CTA，首屏Training Plan仅见标题 | VISUAL_DIFFERENT |
| 08 / 1920×1080 / Text100 | [精选原字节PNG](../assets/ui-v125/candidate-20261010-08/home-1920-text100-after08.png) / `bee4373190d4aa77aebce7a5d71fb6db436a998e902d1e19955b3585a62b9fd6` | 标题/说明/CTA分离；说明成为较长一行。训练计划首行可见，原图的紧凑三行训练计划和Skills/Recent Reports未在首屏出现 | VISUAL_DIFFERENT |
| 08 / 1440×900 / Text200 | 本机原档`verification/v1-qa/results/candidate-08/desktop08-v1-1440x900/Professional-Home-Text200.png` / `2e679648cdadee495aaa32548338a8c65b064ad292b47b8fa377f53138ab009e` | 可见标题两行/副标题分离；说明末行在页面滚动窗口下缘之外、CTA未入首屏。未实测向下滚动与按钮命中，不能说大字所有内容同屏可用 | VISUAL_DIFFERENT；滚动可用性NOT_RUN |
| 08 / 1920×1080 / Text200 | 本机原档`verification/v1-qa/results/candidate-08/desktop08-v1-1920x1080/Professional-Home-Text200.png` / `1e91f01a7203e26edd4d6c415ff4babf1d3c38693826f3e389910eb20e4c4189` | 标题、任务副标题、说明、CTA均可见且不互相覆盖；Training Status堆叠到hero下方，只露上部。导航换行、部分顶栏状态收起是此大字响应式实际状态，不当作1920/Text100像素复刻 | VISUAL_DIFFERENT；后续内容滚动NOT_RUN |

本机报告来源：`verification/v1-qa/results/candidate-08/desktop08-v1-1440x900/desktop08-report.json`，SHA256 `b5e9aaf3ea5e9193cd5c39e6a9726721f81cb0c3fbf2a8e967e2722d3584356a`；同目录视口`desktop08-v1-1920x1080/desktop08-report.json`，SHA256 `38816355aa5d604b77836bd1c42c20c1874d17f16eb646817fdee9f90c3a9333`。报告version为1.0.0，分别记录1440×900及1920×1080、runtimeErrors0。原档不上传，完整独立电脑QA仍在收尾，本次只发布视觉对照与精选截图，不借旧07c通过冒充08。2026-10-10文档整理仅将原档链接改成明确本机证据索引，观察结果、PNG字节及未通过项未变。

1440/Text100真实行框：标题content356×88、MeasureTextSize需要356×88、font36；说明content356×44、需要356×39、font16；标题worldBound `(313,319,359,96)`、副标题 `(313,437,359,25)`、说明 `(313,478,359,52)`、CTA `(315,558,638,52)`。这和截图的局部改善一致，不是对任意宽度或字体的保证。

**geometry ≠ 首屏可见**：1440/Text200报告里说明worldBound `(313,816,1058,86)`、CTA `(315,930,1044,69)` 均正尺寸且与前文不相交；CTA起点930本就超出900px屏幕高，说明也被截图下缘滚动窗口截掉。不能把这些通过改写成“Text200全部按钮首屏可见”或“滚动实测已通过”。该路径的下滚可达性、键盘焦点是否自动滚入、pointer真实命中须另保留证据。

新旧截图的Controller Input显示Gamepad / Keyboard + Mouse来自不同隔离QA档案，不能据此声称物理手柄断连回归。两者均不是用户真实训练记录；没有复制原图的虚构姓名、资格状态、GPS、链路或60%成绩。

第3节未关闭的题材、渐变承托、桌面密度、CTA、业务元数据与专业导航项继续有效。此次没有重画背景、修改原图、验证下滚或完成23图全面比较；只完成本页所列四个新截图状态的质性对照。1.25仍未完成，最终用户验收PENDING_USER。

## 5. 交付边界

本成员未提交/上传。当前HTTPS认证阻塞由根负责人记录，不能将本地文档或缓存origin SHA说成已上传。仓库：[AeroSim](https://github.com/yijunchai2026-gif/AeroSim)；分支：[codex/v125-product-lines](https://github.com/yijunchai2026-gif/AeroSim/tree/codex/v125-product-lines)。根交付时补本批实际commit和真实push/远端核验结果；最终用户验收只由用户完成。
