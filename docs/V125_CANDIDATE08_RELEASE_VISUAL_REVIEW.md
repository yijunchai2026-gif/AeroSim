# 候选08：局部交付视觉界限（紧凑复核）

2026-10-10；3D/视觉美术只读复核。完整读当前AGENTS、[Dashboard差异](V125_DESKTOP08_DASHBOARD_VISUAL.md)、[Replay差异](V125_DESKTOP08_REPLAY_VISUAL.md)，并查[08独立QA](V125_DESKTOP08_QA.md)、[PRD第0章/25.10](../飞行模拟器产品设计文档.md)与[23图映射](V1_SCREEN_SPEC.md)。只新增本文；未启动Unity/应用、改代码/图稿、操作Git或用户确认。

**结论：可以把08描述为“桌面局部叠字/单位/暂停焦点修复候选”，不能称23图视觉通过、1.25交付完成或三端已验收。** 应用身份仍1.0.0开发候选；被测Runtime SHA由既存QA提供为`0aec831cff961caafa77a38f90bdf0335ff4026b817ccde9fd71b8ed21e99f9f`，本角色未重新核二进制或原生运行。查阅时08原生smoke仍NOT_RUN；新结果须由根与独立QA另复核，不能用此次看图补签。

## 实际证据与范围

本轮按[锁定manifest](../verification/v1-qa/reference-manifest.json)逐文件重算SHA和PNG头尺寸：**23/23字节/1672×941一致**，无改参考；这只是完整性，不是23页看图验收。实际重新打开原图仅[10专业首页](../assets/aerosim-us-10-professional-dashboard.png)、[15回放](../assets/aerosim-us-15-replay-analysis.png)、[航拍HUD](../assets/aerosim-us-flight-training-ui-v1.png)，其余20张（01–09、11–14、16–22）本轮只核映射/指纹、未重新目视。比较遵循等比contain，不拉伸/裁掉差异，也未计算像素相似度。

以下六张精选PNG均以view_image实际打开并重算SHA（表中仅列前12位定位）。UI/HUD100%；1440图为1440×900、1920图为1920×1080；隔离合成QA不是用户成绩。**其中一张是07c历史基线，不是六张全为08。**

| 实际看图 | SHA前12位 | 本轮直接观察 / 可签范围 |
| --- | --- | --- |
| [07c Home1440/Text100](../assets/ui-v125/candidate-20261010-08/home-1440-text100-before07c.png) | `272cb1cd10ab` | 标题多行溢出并与副标题/说明互压，保留失败基线；不借08通过覆盖。 |
| [08 Home1440/Text100](../assets/ui-v125/candidate-20261010-08/home-1440-text100-after08.png) | `990ce08b81c6` | 标题恢复两行、说明/CTA分离，局部叠字关闭可见；仍桥任务配海岸、全宽CTA，首屏只露Training Plan标题。 |
| [08 Home1920/Text100](../assets/ui-v125/candidate-20261010-08/home-1920-text100-after08.png) | `bee4373190d4` | 文案分离；hero仍大幅占屏，计划仅见一行，原图完整计划/技能/报告密度未达；VISUAL_DIFFERENT。 |
| [08首次Tab/Space后Pause1440/Text100](../assets/ui-v125/candidate-20261010-08/pause-1440-first-tab-space.png) | `1889f4960150` | 所见是Flight paused与醒目Resume；底部退出项未完整露出。截图不能证明键盘事件因果、滚动/点击、默认Flight HUD或移动设备；交互只引用独立QA范围。 |
| [08 Replay1920/Text100](../assets/ui-v125/candidate-20261010-08/replay-1920-text100.png) | `c5c1cc8761f1` | 39 ft、8.9 mph、92 %各一份单位；预览是重复树/平滑山坡，未见原图轨迹/家点；视角选中态弱、无纵轴数值刻度、时间线为空，整页VISUAL_DIFFERENT。 |
| [08 Replay1440/Text200](../assets/ui-v125/candidate-20261010-08/replay-1440-text200.png) | `9a504c9918e9` | 大字重排为纵向；首屏只有标题/预览/播放器，无遥测。三行单位NOT_VISIBLE；未滚动，不能签屏外可达。 |

既存两份美术报告含额外Text200/Metric等原档观察，本轮未打开那些原档，不把其范围改写成本角色实看。空事件夹具和20秒曲线不能证明事件/真实记录算法缺失；同样不能证明原图非空事件工作台已达到。Pause的遮暗画面也不用于量化材质/曝光。

## 局部可交付声明与下一步

08独立QA中的PASS_AUTOMATED_ONLY可支撑其冻结合同内的焦点、行框和重复单位改善；本轮目视支持上表两个Home正常字号和Replay1920的局部结果。**不得扩展为**Home/Replay Text200屏外滚动点击、正常Flight默认HUD、全部倍率、iPad/iPhone/物理控制器、真实地图/空域、所有模型或最终用户验收。以下为视觉优先级，不改既存安全/输入门禁优先级：

| 顺序 | 最大视觉缺口 / 最小可复核下一步 |
| --- | --- |
| V1：首页任务题材与文字承托 | Bridge Inspection应有桥/桥墩/河面题材和左侧深色承托；收紧Text100 hero、CTA与计划行，1920完整呈现原图计划/技能/报告层次。素材仍需独立来源记录；静态菜单图不冒充可飞环境。保留真实本地状态，不复制原图资格/姓名/60%成绩。 |
| V2：可运行场景与回放分析关联 | 最大沉浸差距是山体/岸线轮廓、地表类别/尺度、树群和水面层次，不能只补菜单照片。按[材质专项报告](V125_SCENE_REALISM_AUDIT.md)做一条固定视点样板；Replay再用明确非空夹具复核轨迹/警告/家点、轴刻度、杆标签、选中态和事件主动作，不伪造记录。 |
| V3：补缺状态，再扩23页 | 同冻结构建补正常Flight默认HUD与Home/Replay Text200滚动后的关键操作图及真实交互证据；随后依23图映射逐页核，不以本轮3目标/6精选的局部观察代签。移动新布局与真机性能独立验证，不能缩放桌面图替代。 |

逐型号用户批准仍为**0**（PRD0.6），全部新审核稿pending_user；本轮不生成机型图、不把Mavic4稿接正式飞行、不改变批准状态。第二美术/独立QA可另作补充；本文不是全面23图签收。根负责冻结、验证、明确提交范围及真实push/远端SHA回执。

## 第二美术接力（2026-10-10；追加后冻结）

首次稿SHA256保留为`8acf345bdf0e5d7dbca2f30c2ffeadb59e7fb5444e598360b8bd2a28b9abd17d`（原34行不改）。本角色实际view_image只看原图10、15及08的Home1920/Text100、Replay1920/Text100四张；支持首稿主要差异，非全部23图验收。应用仍1.0.0开发候选，移动真机与全面视觉未通过；未运行Unity/native、操作Git或改任何批准。

- V1／桥检题材与文字承托：[DesignDashboard入口](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.DesignV1.cs) L386–400现指向[CoastHeroV1.png](../AeroSim/Assets/AeroSim/Resources/Art/DesignV1/CoastHeroV1.png)。下波单独交桥/桥墩/河面菜单资产及左暗渐变层，保存来源/SHA；不改原图、不将静态配图冒充可飞地图，也不接入pending_user DJI机型。
- V2／桌面首屏密度：[Product.uss](../AeroSim/Assets/AeroSim/Resources/UI/Product.uss) L253–262的`.design-dashboard-hero`／`.design-table-row`及[Responsive.uss](../AeroSim/Assets/AeroSim/Resources/UI/Responsive.uss) L168–172为入口；先核冻结构建的实际CTA宽度/最终样式，再收紧hero、内容宽CTA和计划行，1920/Text100复拍完整计划/技能/报告层次，保持真实本地状态；Text200另做真实滚动与命中，不缩字代签。
- V3／分析内容：[FlightDesignV1.cs](../AeroSim/Assets/AeroSim/Runtime/AeroSimApplication.FlightDesignV1.cs)的`BuildDesignReplay` L281、`ReplayTelemetryGraph` L575、`ReplayStickDiagram` L617及[FlightDesignV1.uss](../AeroSim/Assets/AeroSim/Resources/UI/FlightDesignV1.uss) L99–119为入口；用明确标记的非空事件夹具逐项交选中视角、纵轴数值、左右杆标签、轨迹/警告/家点与定位练习主动作证据。现空时间线与线性20秒曲线不证明真实算法缺失，已单份单位不回退。
- V4／可运行场景样板：[Visuals.cs](../AeroSim/Assets/AeroSim/Runtime/Simulation/FlightSimulator.Visuals.cs) L79–88／L154–174／L242–268、[LandscapeSurface.shader](../AeroSim/Assets/AeroSim/Resources/LandscapeSurface.shader)、[OceanSurface.shader](../AeroSim/Assets/AeroSim/Resources/OceanSurface.shader)及[MaterialsV2合同](../AeroSim/Assets/AeroSim/Resources/Art/MaterialsV2/PROMPTS.md)为入口；先固定视点核草地5m砖与作者约2m意图、岩石借作树皮的语义、坡面/树群轮廓和岸水关联，独立材质/normal样板再测性能；菜单照片改善不能关闭此项，当前不签3D或移动性能通过。
