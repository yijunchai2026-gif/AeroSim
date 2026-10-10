# AeroSim 1.25 经典电影机型美术：DJI Inspire 1 原版 X3 审核稿

日期：2026-10-09（America/Los_Angeles）

产品线：DJI 目录 / 经典电影机型 3D 美术

机型 ID：`dji-inspire-1`（与冻结目录 `assets/catalog-v125/dji-aircraft.json` 一致）

当前稿件：`draft-01`

外观状态：`pending_user`。尚未获得用户对本机型、本稿版本与图像散列的单独确认。

## 稿件用途与边界

本稿是内置 image_gen 真实生成的摄影级 3D 风格**外观审核位图**，不是 DJI 官方照片，不是可运行网格、CAD、UV / LOD / 碰撞器或最终 PBR 材质。仅可进入外观审核画廊；不得进入正式选机飞行或最终 3D 资产。

目标限定为 **2014 原版白壳 DJI Inspire 1 + Zenmuse X3 FC350**，机臂降下的着陆模式。不是 Inspire 1 V2.0、Pro、RAW、X5 / X5R、黑版、Inspire 2 或 Inspire 3。目标为同一构型四视图，但本轮已发现框架拓扑漂移，**没有证明严格四视图一致**。

## 当前审核稿

![DJI Inspire 1 原版白壳 X3 draft-01：生成四视图，待用户确认，框架一致性未通过](../assets/v125-art/inspire/inspire-1-x3-draft-01.png)

- 图像：`assets/v125-art/inspire/inspire-1-x3-draft-01.png`
- 尺寸 / 格式：1536 × 1024，RGB PNG。
- SHA-256：`9ca8f89268abcb28b73a2e3b910a5838bdc034b0188ab01b6bf0a6a08380f3a2`。
- 视图标签：FRONT、LEFT SIDE、TOP、THREE-QUARTER；目标左侧视鼻朝左，顶视鼻朝上。
- 紧凑审核清单：[review-drafts.json](../assets/v125-art/inspire/review-drafts.json)。
- 提示词文件：[inspire-1-x3-draft-01.prompt.txt](../assets/v125-art/inspire/inspire-1-x3-draft-01.prompt.txt)。
- 执行方式：内置 `image_gen`，一次文字生成；无 CLI / API fallback。
- 实际产物已从内置工具返回的 `generated_images` 路径复制到仓库目录，并使用 `view_image` 打开仓库中的实际 PNG 检查；没有用占位图或手画结构图替代生成。
- 生成前观看的官方参考没有作为图片输入传入此次生成；提示词使用观察后的文字约束。

## 官方依据与版本区分

检索 / 观察日期：2026-10-09（America/Los_Angeles）。只采用 DJI 官方主源及官方 CDN 手册；搜索结果中第三方转载不作为依据。

| 官方来源 | 本轮核对内容 |
| --- | --- |
| [DJI 2014 Inspire 1 发布说明](https://www.dji.com/ch/newsroom/news/announcing-the-dji-inspire-1) | 原版机型身份、碳纤维变形机臂、可拆卸相机与云台。 |
| [DJI Inspire 1 发布活动说明](https://www.dji.com/newsroom/news/inspire-1-launches-at-dji-evolution-event-in-san-francisco) | 2014 年原版发布背景；不以之后的 V2.0 页面代替原版身份。 |
| [DJI Inspire 1 官方 V1.0 用户手册](https://dl.djicdn.com/downloads/inspire_1/en/Inspire_1_User_Manual_v1.0_en.pdf) | 官方 CDN 当前这份 V1.0 文件为 2015.3 版；实际渲染并完整观察第 6 页着陆 / 运输模式、第 7 页 X3 安装与自紧桨、第 8 页升级的 1345s 快拆桨说明、第 9 页 Aircraft Diagram。将原装自紧桨与后续升级快拆桨分开，不把本手册版次称为 2014.12 原版。 |
| [DJI 旧官方 Inspire 1 产品页](https://www-v1.dji.com/inspire-1.html) | 实际浏览侧面结构展示：白色曲面外壳、黑色后电池、碳杆与变形机构、白色小型相机及黑色云台；页面的购买链接指向 V2.0，不能用购买链接证明图中所有零件均是原版。 |
| [DJI Inspire 1 V1.0 官方支持 / FAQ](https://www.dji.com/support/product/inspire-1) | 区分原版 / V2.0 / Pro：原版与 V2.0 都使用 X3，但 V2.0 的 X3 安装在 X5 减振板上；Pro 使用 X5。资料同时包含升级桨信息，不能把所有配件混到首发构型。 |
| [DJI Zenmuse X3 官方页面](https://www.dji.com/zenmuse-x3) | X3 机型身份与小型一体相机，不换成 X5 / X5R 可换镜头相机。 |

官方 PDF 与临时渲染页只用于观察，没有提交完整手册或参考截图，也没有将官方图片冒充原创资产。

## 实际看图后的差异与未解决项

以下为美术自查，不是独立 QA 结论，更不替代用户确认。

| 检查项 | 实际观察与保留问题 |
| --- | --- |
| 白壳 / 碳杆 / 后部电池 / X3 大轮廓 | 可以辨认为 Inspire 1 系列的白壳、黑色机构与小型白色 X3 风格相机；未出现黑版、X5 可换镜头、RAW SSD、双电池或鼻部独立 FPV 相机。细节精度未通过。 |
| 着陆态 | 四视图目标均是机臂向外下降、脚接地、相机悬于脚之间；没有生成升臂飞行态或平放运输态。着陆角和脚部几何没有尺寸测量。 |
| **框架拓扑 / 四视图一致性** | **主要未通过项**：LEFT SIDE 明显存在连接前后电机的纵向碳杆，TOP 却偏向四个由机身径向伸出的 X 型机臂，左右前后纵杆关系未保留；不能认定四视图为同一严格模型。需要后续专门修订。 |
| 着陆脚结构 | 当前脚偏直、偏粗，三分之四视图的电机脚更像竖直片状脚；官方侧面展示中的倾斜 / 三角支撑没有准确还原。 |
| X3 云台与安装板 | 没有明显生成大尺寸 X5 相机，但黑色支架和上方偏航组件偏厚，安装连接近似；未逐零件证明它是原版 X3 连接而非 V2.0 X5 减振板结构。 |
| 电机 / 桨 | TOP 与 THREE-QUARTER 可辨认四电机、四双叶桨；侧 / 正视自然遮挡。自紧桨黑 / 灰螺帽的分配、3510 电机形状、桨叶轮廓与标记尚未逐面核验，不宣称机械准确。 |
| 正交与方向 | 标签完整；TOP 能看到较长脚部并带透视，FRONT / LEFT SIDE 也不是标定正交投影。不能作为尺寸精确建模蓝图。 |
| LED / 微小硬件 | 前后 LED 颜色与位置、接口、线缆、机身螺丝、电池表面、云台铭文、底部视觉 / 超声传感器、背面和底面均未完整核对。 |
| 真实比例 | 未由可测量原机、CAD 或摄影测量验证；没有真实网格、关节变形动画或最终材质，不宣称 1:1 或可运行。 |

优先后续美术修订是恢复同一原版 H / T 式变形碳杆框架的四视图一致性，再校正着陆脚与 X3 支架。当前保留初稿和已记录缺陷供审核，不能以 `pending_user` 或生成成功掩盖结构问题。

## 完整实际生成提示词

以下文本与仓库提示词文件一致，为本次内置工具实际使用内容：

```text
Use case: product-mockup
Asset type: AeroSim 1.25 individual aircraft appearance review bitmap, draft-01; neutral studio turnaround for user review, not an operational mesh or official product photograph.
Primary request: Generate one photorealistic 3D-style four-view turnaround sheet of the ORIGINAL DJI Inspire 1 from its 2014 launch, white standard aircraft with its original Zenmuse X3 camera and original mounting system. NOT Inspire 1 V2.0, Pro, RAW, Inspire 2 or Inspire 3.
Scene/backdrop: clean pale neutral-gray studio background, generous whitespace, subtle grounded shadows, four equal panels in a simple 2x2 layout.
Subject: the EXACT SAME single aircraft in all four panels, in fully deployed LANDING MODE with the transforming carbon-fiber arms LOWERED, standing on its four separate motor-pod landing feet. White aerodynamic elongated central shell with a curved tapered front nose, exposed black mechanical underframe and a tall rear intelligent battery. Long dark carbon-fiber tubes form a left and a right fore-aft rotor rail, each rail carrying one front and one rear motor. From the central transforming mechanism, angled dark carbon-fiber spars slope outward and downward to these rotor rails in landing mode; thin lower bracing rods complete the original articulated T-shaped frame. Four motors total, four large gray/dark two-bladed original 13-inch self-tightening propellers total, with small black/gray hub nuts; no bright colored tips. Under each motor housing is a long slender landing leg with a short small rubber foot, four separate feet, NOT continuous Phantom skids. The white upper central shell is higher than the lowered motor pods.
Camera: the original compact white Zenmuse X3 FC350 rounded camera housing hangs beneath the front center on a black 3-axis gimbal. One black circular wide-angle front lens on the white camera; slender black side gimbal support and cylindrical yaw motor above it. Lens points FORWARD in all panels. Show the ORIGINAL small X3 mounting connection under the aircraft, not the broad upgraded X5 damping plate. Camera remains safely above the floor between the lowered legs. Do not add a second FPV camera to the nose.
Style/medium: photorealistic premium studio product rendering, realistic satin white polymer shell, dark woven carbon-fiber tubes, black metal gimbal and motor housings, realistic thin gray propeller blades. Engineering-conscious appearance, no fantasy redesign.
Composition/framing: TOP LEFT true FRONT view from aircraft nose; TOP RIGHT true LEFT SIDE view with nose pointing LEFT; BOTTOM LEFT straight TOP view with nose at TOP; BOTTOM RIGHT elevated FRONT LEFT THREE-QUARTER view. Front/side/top should be near orthographic; perspective only for the three-quarter view. Fully show aircraft silhouette and every visible propeller tip with ample panel padding, no cropping. Maintain identical shell, landing-mode arm angle, camera, battery, rotor arrangement, leg shapes and markings across views; naturally occluded far-side parts need not be artificially separated.
Lighting/mood: broad softbox studio light, neutral daylight white balance, controlled shadows to clearly show white shell and carbon-fiber bracing.
Text (verbatim): small dark-gray sans-serif view labels only: "FRONT", "LEFT SIDE", "TOP", "THREE-QUARTER". No title, no decorative annotations, no technical dimensions.
Constraints: four views of ONE original 2014 white Inspire 1 + X3 configuration, all arms lowered in landing mode, same camera orientation, exactly four motors and four two-bladed propellers per aircraft, authentic articulated carbon-fiber frame, complete silhouettes. This is an appearance REVIEW draft, not an approved final 3D asset.
Avoid: black Inspire 1 Pro body; Zenmuse X5/X5R interchangeable lens camera; RAW SSD; V2.0 broad X5 damping plate; quick-release red-marked 1345T prop hubs; Inspire 2 dual-battery body or built-in FPV camera; Inspire 3 large camera; raised in-flight landing configuration; flat travel mode; Phantom molded white arms or continuous landing skids; Mavic folding arms; extra rotors; prop guards; remote controller; accessories; exploded views; watermark; copy of a manual page.
```

## 用户确认门禁与交接

本轮没有用户确认记录。`appearanceStatus` 必须保持 `pending_user`；负责人、美术、程序员、QA 与自动化不得代用户确认。用户决定须绑定 `dji-inspire-1`、`draft-01` 与上列 SHA-256；改变外观后重新确认。

根负责人整合主产品文档与审核画廊，完成独立 QA、范围核查、Git 提交与推送。本美术任务仅新增本目录和本文档，没有改 catalog、审核模板、主产品文档或 Unity，没有运行 Git 提交、推送、切分支或合并操作。
