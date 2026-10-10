# 1.25 PRD 重整独立 QA

状态：**PASS_AUTOMATED_ONLY，已独立终验冻结v2的文档、依赖与图稿身份**。本文不是 1.25 产品、旧候选08、视觉一致性或用户外观验收签字。独立成员只写本文和 `verification/v125-qa/prd-review/`，不修改 PRD、应用源码、资产、共享进度或 Git。

## 读取与字节基线

2026-10-10 完整读取仓库 `AGENTS.md` 及整理前 2,560 行 PRD。先查看 Git 状态，保留既有未提交修改。整理前 PRD SHA-256 为 `a850dc579798c4e88362c0833f78596ca6b805fb7c181a8d087c231654aa01a3`。完整锁定记录见 [baseline.json](../verification/v125-qa/prd-review/baseline.json)。

- 23 张历史原始概念图均按 [既有参考清单](../verification/v1-qa/reference-manifest.json) 重算 SHA-256，全部吻合；未用截图替换目标。
- 原始画布为 1672×941；参考归一化是等比例 contain 至 1920×1080，不裁切、不非等比拉伸。这个尺度是对照方法，不是手机设计或真机测试证据。
- 锁定原 Mavic Pro `draft-01`、`draft-02` PNG 和完整提示词字节；当前 `dji-mavic-pro` / `draft-02` / `ace4e4d0edad8555ac726a8b2c674c4a7513201cc8c01bcac8927fd12c462931` / `pending_user` 不得因制作 Mavic 4 Pro 被错绑或批准。
- 新 Mavic 4 Pro 必须是独立 `dji-mavic-4-pro`，加入审核稿不等于正式可飞资产。用户“基本正确”等未绑定 ID、revision 与真实散列的描述不能转成 `approved`。
- 准备阶段追加锁定本轮可能纳入Git的既存候选08六张精选截图，记录各自真实尺寸与SHA-256；这些只是既存证据字节，不是本轮新构建或对旧探针的重新签收。

## 实际看图：原稿与旧数字不能混为一个合同

已通过 `view_image` 以 original 分辨率实际打开原03航拍首页、05自由飞设置、10专业仪表盘、16美国准备、22设置、航拍训练HUD，以及2016 Mavic Pro `draft-02`。下表为独立人眼观察；近似比例不是从可编辑设计文件提取的正式测量值，也不代表实现已一致。

| 核对点 | 原图观察与重整要求 |
|---|---|
| 字号、外壳 | 原03/10/22顶栏约76原图像素，等比例到1920约87px；侧栏约216原图像素，约248px。标题显著大于旧32px建议；不能用64/240和旧字号同时解释同一视觉目标。需要把旧数值标历史，图稿校准值与实际实现分开。 |
| HUD与地图 | 原HUD是左下大圆雷达，约310原图像素直径，并非240×180矩形；左教学卡、底遥测和右工具栏也不能被旧小组件尺寸覆盖。图中没有永久非导航水印；保留原PNG，在文字中说明最新实现应补模拟标签，而不是偷偷改原图。 |
| 专业页面 | 原10有桥梁任务Hero、窄主CTA、三行计划、下方技能/报告；它不是海岸Hero。旧08局部字形修复不能抵消配图、层级、密度差异。未重新构建或签收候选08。 |
| 回放密度 | 又实际打开原15和既存08的1920/Text100 Replay与Professional Home截图。原15首屏同时有海岸轨迹、完整图表/输入、四节点时间线、当前警告及播放条；08截图是简化3D山地、不同视角按钮状态、时间线无事件且剩余控制不在同一首屏。单位可见只出现一次不等于页面视觉或事件交互已一致。 |
| 平台 | 原图为桌面16:9完整页面，不提供iPhone/iPad横竖屏稿或safeArea实测。旧Windows首发/移动后续与现三端独立产品线应明确先后；不能把桌面窄视口称为iOS验证。 |
| 地图服务 | 原05场景选择与Training Airspace是教学环境；原16是法规学习/FAA资源清单，包含“not flight authorization”提示，不是Google Maps、实时空域或LAANC授权界面。模拟场景、教育链接、未来真实地图目标、当前未接入事实必须分层。 |
| 机型 | 原05示例名是Lightweight Camera Drone，不等于已完成DJI机型目录；2016 Mavic Pro稿可见深灰、单摄、黄色臂条。其身份与Mavic 4 Pro必须独立，不从“无人机轮廓可辨”推导准确结构或用户批准。 |

## 可重复自动检查与准备结果

工具：[prd-document-check.mjs](../verification/v125-qa/prd-review/prd-document-check.mjs)。检查Markdown内本地图片/文件链接、引用式链接和HTML图片/链接，排除标题锚点、外网URL及代码示例；检查Git tracked状态但不执行Git写操作。对未tracked的存在文件只报告发布待办，不伪装为GitHub已发布。绝对本机路径即使存在也报不可移植；旧历史断链保留分类，不豁免失败。

```sh
node verification/v125-qa/prd-review/prd-document-check.mjs --prepare
node --test verification/v125-qa/prd-review/prd-document-check-test.mjs
node verification/v125-qa/prd-review/prd-document-check.mjs --require-tracked --doc docs/V125_ART_MAVIC4_REFERENCE.md --doc docs/V125_ARCHITECTURE.md --doc docs/V125_PRD_CONFLICT_REVIEW.md --doc docs/V125_DESKTOP08_DASHBOARD_VISUAL.md --doc docs/V125_DESKTOP08_REPLAY_VISUAL.md --doc docs/V125_GATE_A_CONTRACT.md
node --test verification/v125-qa/test-catalog-review.mjs
```

准备运行在增加参考清单锁定断言前为490检查、4失败、213个本地链接出现、23原始图、4当前稿。真实输出保存在 [prepared-results.json](../verification/v125-qa/prd-review/prepared-results.json)。首次脚本自测误把引用定义算作链接数量，6/7断言失败；已把样例期望修正为6并保留全部实际链接测试后重新运行，不是调整PRD或降低资产检查门槛。

准备失败：旧第22章的图册HTML、`ART_DIRECTION_V2.md` 两链接是本机绝对路径；旧ArtV2应用链接既绝对又已不存在，因此共4项失败。建议图册/文档改仓库相对路径；历史应用只记录本机位置与存在性，不给伪GitHub链接。

准备时10个存在但未tracked目标：候选08六张PNG、Dashboard/Replay两个视觉文档、未完成的候选08QA文档、Gate A合同候选。根负责人须按本轮真实发布范围处理依赖；没有冻结验证的旧批次不能由本轮整理工作代签。全量Unity/QA原始产物不因链接存在而纳入提交。

检查器独立合成回归 [prd-document-check-test.mjs](../verification/v125-qa/prd-review/prd-document-check-test.mjs) 实际10测试/10通过/0失败：改原图字节、删原图嵌入、缺文件、绝对链接、错绑2016机型、新Mavic4复用旧图、图像散列变更、模糊反馈擅自批准、陈旧画廊版本均被拒绝。fixture明确使用假图像字节，不批准真实条目，测试临时目录已由程序清理。原“至少23原图链接出现”断言从解析器自测移入实际报告检查，条件没有弱化；删原图的负例仍报失败并输出证据。

## 终验与人工边界

本轮最终依据为[冻结v2](../verification/v125-qa/prd-reorganization-freeze-v2.json)。独立成员实际运行上列命令，不用根负责人的结果替代执行；紧凑记录见[final-results.json](../verification/v125-qa/prd-review/final-results.json)。原[冻结v1](../verification/v125-qa/prd-reorganization-freeze.json)与首次准备失败原件保留，不以最终通过倒写历史。

2026-10-10T20:02:57Z独立终验：PRD加六份依赖文档共816检查、0失败、242个本地文件链接出现、未tracked发布依赖0、遗留断链0；19个冻结文件逐字节重算全部一致。23原图及参考清单、2016 Mavic Pro的两PNG/两提示词、六张既存08截图均与准备基线吻合；完整2016当前manifest行也未变。画廊仍132主条目＋9版本、5机型有当前稿、0用户批准，生产接入门禁关闭。tracked包含根明确暂存的文件，只证明本地依赖已纳入发布范围，不证明远端已上传。

目录门禁实际30测试/30通过/0失败；新增负例证明2016 Mavic确认不能批准Mavic 4 Pro或512GB版本。检查器独立反例再次10测试/10通过/0失败。均在隔离fixture执行，无真实机型批准、无新native测试。首次准备490/4的失败分类不删：根已把两个历史绝对文档入口改成相对路径，把不存在的历史.app明确改为纯路径说明；六张精选截图、已完成视觉文档和Gate A候选列入本轮依赖，未完成08 QA入口从PRD移除而不代签。

最终PRD SHA-256为`cc72557d8f3fa0bb899495516418908fba811d63d80fa553631a3c87bdbbf1a6`；冻结v2 SHA-256为`8399bc2bdbd4ea076cb71fe0c8effec5f1f8b3ca2a0ac4c9914b4e48ce6389ff`；新Mavic 4 Pro PNG为`efdc1b0d6c5bf307968731e1c5f0afd9f40aa3e4e5a4eb3deb5382e090eaa9ab`。v2仅改PRD的Replay事实描述：既存探针`duration = 20`、`Seek(10)`，现在写“合成20秒fixture在第10秒采样”；旧冻结不能用于证明新PRD，故本QA等待v2后复跑全部命令并重算19指纹。

独立读完全部重整改动及最终六文档措辞。下述冻结前三处冲突现已逐一澄清：通用训练机和具名DJI物理档案分层、HUD安全区不跨设备硬编码、校准仅复用匹配平台/实例/来源的有效记录。首次引导/FPV不强制所有端连接实体遥控器；Metric m/s和Aviation目标与现候选m/km/h事实分开。Dashboard/Replay文档仍明确VISUAL_DIFFERENT与Flight default NOT_RUN，Gate A仅为契约候选而非已接线，iOS模块存在不等于Xcode/导出/真机通过。未发现需要扩展本批范围的新增阻断冲突。

### 冻结前第二次文本与新稿检查

独立读完第0章、整理所涉及的全部PRD改动文本、完整架构勘误/原方案与完整冲突审查、美术交接文档。发现10.2匿名类别标定、18.4.2旧中央安全区硬编码、18.5.2无条件复用校准三处仍可能与新入口语义相撞，已交根负责人冻结前澄清，未越权修改生产文件。技术审查的C18“建议1秒”不是根采用的800ms，PRD第0章明确选800ms；保留建议报告不反写为采用证据。

实际以 `view_image(detail="original")` 打开新 `mavic-4-pro-draft-01.png`，并读取完整实际提示词；不是依赖提示推定画面成立。可见独立Mavic 4 Pro标题、四视图、pending_user页脚、灰壳、黄橙桨尖，以及黑色球形三摄（上方削角框、下左小框、下右大圆框）；未沿用2016稿的黄色臂双条与小银单摄，身份在图面上可区分。FRONT仍带俯视、侧面遮挡、多个电机下有明显尖脚、云台侧轴较亮且厚、鼻肩圆窗突出；四图不是同一真实网格投影，机械比例/接口/桨相位未闭合。没有底部/背面稿或运行网格，不能称精准机械复刻。

本QA没有重新访问DJI网页或量取CAD；官方详图对照由[独立美术交接](V125_ART_MAVIC4_REFERENCE.md)明确记录实际观察范围和残差，不能冒称本QA又做了同一次官方照片核验。上述独立本地图像观察支持身份隔离与待审边界，不形成用户外观批准。

本轮没有Unity构建、iOS导出/签名/真机、实物遥控器、多点触控、真实空域/多人服务或法律/版权审查。自动数字只检验文档结构与文件字节，不能证明视觉一致、机体机械准确、原生平台可用、目录无遗漏、合法授权或用户验收。最终用户确认只由用户完成。
