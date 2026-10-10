# AeroSim 1.25 DJI 全历史机型研究目录（待逐机型确认）

资料截至：2026-10-09。目标应用版本：1.25。目录版本：1.25.0-research-1。

这是供选机目录、逐机型审核画廊与 3D 美术排产使用的**官方资料研究底稿**，不是已完成的 3D 资产库，也不是已证明“一个都不少”的全量 SKU 认证。本批只完成型号整理与门禁数据，不改变 0.4 / 1.0 运行应用。

机器可读文件：[dji-aircraft.json](../assets/catalog-v125/dji-aircraft.json)。

## 1. 本批覆盖与不能宣称的内容

本批包含 132 个主条目、9 个涂装/颜色子条目，共 141 个待确认外观项目；分属 19 个产品家族、8 类应用领域，引用 72 个官方来源。主条目也含具有独立身份的硬件修订、地区机体及 DIY 机架，**不是 132 款全新整机的销量统计**。

130 个主条目已有截至日的官方目录/公告/资料身份依据；T200 与 FlyCart 200 两项只有动态官方技术/兼容性资料身份，公开发布证据仍待闭合。它们标记 `hold_release_evidence`，不宣称截至日已上市。

全部主型与子版本均为 `appearanceStatus=pending_user`，已确认数量为 **0**；`productionAssetAllowed=false`。型号资料存在、程序校验通过和概念图生成，都不表示用户认可外观，也不表示飞机已经能在模拟器中飞行。

| 类别 | 主条目数 |
| --- | ---: |
| 早期 DIY / 机架平台 | 8 |
| 消费级航拍 | 49 |
| FPV / 沉浸式飞行 | 4 |
| 专业影视航拍 | 6 |
| 行业 / 测绘 / 巡检 | 34 |
| 农业植保 / 多光谱 | 25 |
| 运输 / 运载 | 3 |
| 教育 / 大疆技术生态 | 3 |

数字仅表示本批可追溯记录量。没有完成全球各地区监管 SKU、所有载荷套餐及全部历史修订的穷尽对账；剩余缺口见第 6 节。

## 2. 型号边界与年份规则

- 独立机体/传感器硬件型号与官方硬件修订均在顶层 aircraft 分别记录（包括 Phantom 2 Vision+ V2.0/V3.0、Mavic 4 Pro 512GB），例如 Mavic 2 Pro 与 Zoom、Mavic 3E 与 3T/3TA、Matrice 4E/4T、MG-1P/RTK。Cine 具有机体硬件差异，不能只当遥控器套餐。
- 有明确官方商品身份的颜色/特别涂装作为子条目，例如 Spark 五色、Mavic Pro Alpine White、Phantom 4 Pro Obsidian、Inspire 1 Pro Black、Tello Iron Man Edition。子条目有自己的稳定 ID 和确认门禁，不能继承母型通过状态。
- Phantom Pro+/Advanced+、普通 Fly More/RC/Goggles 套餐不增加一架独立飞机。Mavic 4 Pro Creator Combo 内的 512GB 机体确有存储硬件差异，因此单独记录为顶层硬件机型，而不是把整套配件计作一个机型。[官方发布说明](https://www.dji.com/es/media-center/announcements/dji-release-mavic-4-pro)
- 风火轮与筋斗云保留为早期 DJI 自有机架/飞行平台；2006–2011 年的 XP、Ace、Naza、Wookong 等飞控不是完整 DJI 机体，不臆造同名无人机。
- Tello/Tello EDU 归入“大疆技术生态”，制造商保留 Ryze Tech，不伪称为 DJI 自产整机。[教育生态说明](https://www.dji.com/global/newsroom/news/robomaster-tello-talent-drone)
- `releaseYear` 只填官方有年份依据的发布事件；无法确认留 `null`。它不保证是每一地区的首次销售年份。国内企业时间线、全球发布和初始固件事件按 `releaseYearBasis` 区分，不能把支持文件上传日或停产日当首发。
- DJI 中文企业发展轴将初代 Phantom 发布事件列入 2012，而常见零售历史以 2013 为起点；此目录保留其官方事件年份并标记冲突，不强行宣称首次全球销售年。Dock 2/Matrice 3D 全球发布为 2024，国内 Dock 2 事件为 2023；RoboMaster TT 中国时间线为 2020，全球新闻稿为 2021。[DJI 企业发展轴](https://careers.dji.com/zh-CN/about)、[Dock 2 全球公告](https://enterprise.dji.com/news/detail/dji-dock-2-release)

## 3. 逐机型确认与选机板块接入合同

1. 审核画廊从 `aircraft` 和各条目的 `variants` 读取，按类别 → 产品家族 → 已证实年份浏览；未知年份单列“年份待核对”，发布证据待核对单列，不混进“最新已上市”。
2. 每张卡片显示英文商品名、中文简介、年份事件/地区说明、官方资料入口，以及醒目的“外观待用户确认”。美国界面默认英文，介绍翻译和 US units 不改变型号身份。
3. 每个独立机型/子版本的审核页至少需要正面、侧面、顶面、四分之三视图，并明确展开/折叠、载荷、电池、桨叶、颜色/标识。照片是官方参考、概念稿还是运行截图必须分别标记。
4. 用户对**这一项、这一版稿件**明确确认后，记录 ID、稿件版本、SHA-256、原始用户决定和时间。根负责人、美术、QA、程序与自动化不得代替用户确认，母型通过也不得批量放行子版本。
5. 当前 JSON 的 `review` 和 `previewFiles` 留空，避免把不存在的审核图或确认记录写成完成。新图可以进入审核画廊；未经确认不得进入正式选机飞行或最终生产 3D 资产。
6. 即使用户认可外观，最终网格、UV、PBR 材质、LOD、碰撞体和动力学资料还需各自验证。图片初稿不等于可运行 3D 模型，几何验证不等于视觉一致性验收。
7. 用户确认阻塞的是对应资产的正式接入，不阻塞不依赖已确认美术的跨设备程序、摇杆、目录检索、独立 QA 等工作。

### JSON 读取要点

- 根级 `schemaVersion`、`catalogVersion`、`asOfDate`、`counts` 明确数据版本与边界。
- `categories`、`families` 提供中英文导航及稳定 ID；`aircraft[].id` 永久用于审核、资产与用户决定关联，不能按数组位置关联。
- `sourceIds` 对应根级 `sources`；`sourceUrls` 可以直接用于产品页“官方资料”。
- `releaseYear=null` 不能用 0、当前年或母型年份替换；时间排序中未知项分组，不假装有完整年代顺序。
- `cutoffStatus=hold_release_evidence` 项只能作为“资料待核对”候选，不能作为已上市宣称。
- `simulationStatus=not_integrated`、`assetStatus=not_created` 表示本目录没有完成资产/飞行接入；不能把名称选择视为已完成模型差异。

## 4. 主条目逐项目录

下表全部外观待用户确认。年份后加“全球”表示全球发布事件，“国内”表示中文企业时间线；“—”表示尚未证实。官方出处以具体商品/支持/资料页优先；数字规格未逐型号实测，不作为 AeroSim 当前功能承诺。

### 1. Flame Wheel / 风火轮

| # | 机型 / 稳定 ID | 已证实事件年 | 中文简介 | 官方依据 |
| ---: | --- | --- | --- | --- |
| 1 | Flame Wheel F450<br>`dji-flame-wheel-f450` | — | 早期开放式四旋翼机架套件，为自行装配飞控、动力和载荷提供平台。 | [Flame Wheel ARF KIT Downloads](https://www.dji.com/downloads/products/flame-wheel-arf) |
| 2 | Flame Wheel F330<br>`dji-flame-wheel-f330` | 2012 | 较小的风火轮四旋翼机架；需单独审核机臂、中心板和动力安装结构。 | [Flame Wheel ARF KIT Downloads](https://www.dji.com/downloads/products/flame-wheel-arf)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 3 | Flame Wheel F550<br>`dji-flame-wheel-f550` | 2012 | 风火轮六旋翼机架套件，呈六臂开放结构。 | [Flame Wheel ARF KIT Downloads](https://www.dji.com/downloads/products/flame-wheel-arf)<br>[年份证据](https://careers.dji.com/zh-CN/about) |

重点边界：

- Flame Wheel F450：公司年表证明风火轮系列始于2011；没有把系列年份直接当作F450具体型号的首发年。

### 2. Spreading Wings / 筋斗云

| # | 机型 / 稳定 ID | 已证实事件年 | 中文简介 | 官方依据 |
| ---: | --- | --- | --- | --- |
| 4 | Spreading Wings S800<br>`dji-spreading-wings-s800` | 2012 | 早期专业六旋翼平台，采用模块化机臂并服务于航空摄影载荷。 | [Spreading Wings S800 Announcement](https://www.dji.com/ie/newsroom/news/hex-rotor-platform-spreading-wings-s800)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 5 | Spreading Wings S800 EVO<br>`dji-spreading-wings-s800-evo` | — | S800系列的EVO硬件升级机型，须与原始S800分别核对外观和结构。 | [Spreading Wings S800 EVO Announcement](https://www.dji.com/hk-en/newsroom/news/dji-released-s800-evo) |
| 6 | Spreading Wings S900<br>`dji-spreading-wings-s900` | — | 面向专业航拍的碳纤维六旋翼平台。 | [DJI Product Archive](https://www.dji.com/products?t=1428071452743) |
| 7 | Spreading Wings S1000<br>`dji-spreading-wings-s1000` | 2014 | 面向影视航拍的八旋翼平台，具有可折叠机臂和可收放起落架。 | [Spreading Wings S1000 Announcement](https://www.dji.com/pr/newsroom/news/dji-released-its-new-spreading-wings-s1000-octocopter-platform)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 8 | Spreading Wings S1000+<br>`dji-spreading-wings-s1000-plus` | 2014 | S1000系列的后续八旋翼平台，需独立检查结构升级。 | [DJI Product Archive](https://www.dji.com/products?t=1428071452743)<br>[年份证据](https://careers.dji.com/zh-CN/about) |

### 3. Phantom / 精灵

| # | 机型 / 稳定 ID | 已证实事件年 | 中文简介 | 官方依据 |
| ---: | --- | --- | --- | --- |
| 9 | Phantom 1<br>`dji-phantom-1` | 2012 | 早期一体化四旋翼航拍平台；外观重点为白色固定机臂机身及高脚架。 | [Phantom 1 Downloads](https://www.dji.com/downloads/products/phantom)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 10 | Phantom FC40<br>`dji-phantom-fc40` | — | Phantom早期配套摄像头机型，官方列出720p摄像方案。 | [Phantom 1 Downloads](https://www.dji.com/downloads/products/phantom)<br>[DJI Product Archive](https://www.dji.com/products?t=1428071452743) |
| 11 | Phantom 2<br>`dji-phantom-2` | — | 第二代Phantom平台，支持外接Zenmuse航拍云台。 | [Phantom 2 Downloads](https://www.dji.com/downloads/products/phantom-2)<br>[DJI Product Archive](https://www.dji.com/products?t=1428071452743) |
| 12 | Phantom 2 Vision<br>`dji-phantom-2-vision` | 2013 | 配备一体化摄像头的Phantom 2航拍机型。 | [DJI Product Archive](https://www.dji.com/products?t=1428071452743)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 13 | Phantom 2 Vision+<br>`dji-phantom-2-vision-plus` | 2014 | Phantom 2搭载三轴摄像云台的机型；V2/V3硬件修订作为独立审核子项。 | [DJI Product Archive](https://www.dji.com/products?t=1428071452743)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 14 | Phantom 3 Professional<br>`dji-phantom-3-professional` | 2015 | Phantom 3系列的4K相机机型。 | [Phantom 3 Advanced Official FAQ](https://www.dji.com/phantom-3-adv/info)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 15 | Phantom 3 Advanced<br>`dji-phantom-3-advanced` | 2015 | Phantom 3系列Advanced机型；官方FAQ列出与Professional的相机和充电器差异。 | [Phantom 3 Advanced Official FAQ](https://www.dji.com/phantom-3-adv/info)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 16 | Phantom 3 Standard<br>`dji-phantom-3-standard` | 2015 | Phantom 3系列的入门机型，采用与Advanced/Professional不同的图传配置。 | [Phantom 3 Advanced Official FAQ](https://www.dji.com/phantom-3-adv/info)<br>[DJI Product Archive](https://www.dji.com/products?t=1428071452743)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 17 | Phantom 3 4K<br>`dji-phantom-3-4k` | 2016 | Phantom 3系列的另一款4K机型，须与Professional分别审核。 | [DJI CES 2016 Recap](https://www.dji.com/altitude/what-you-missed-at-ces-2016)<br>[DJI Product Archive](https://www.dji.com/products?t=1428071452743) |
| 18 | Phantom 3 SE<br>`dji-phantom-3-se` | — | Phantom 3系列SE机型，官方历史目录单独保留其产品入口。 | [DJI Product Archive](https://www.dji.com/products?t=1428071452743)<br>[DJI Download Center](https://www.dji.com/downloads) |
| 19 | Phantom 4<br>`dji-phantom-4` | 2016 | Phantom第四代一体化四旋翼平台。 | [DJI Product Archive](https://www.dji.com/products?t=1428071452743)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 20 | Phantom 4 Pro<br>`dji-phantom-4-pro` | 2016 | Phantom 4系列面向专业航空摄影的机型；外观审核包括相机、避障传感器和机身。 | [DJI Product Archive](https://www.dji.com/products?t=1428071452743)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 21 | Phantom 4 Advanced<br>`dji-phantom-4-advanced` | — | Phantom 4系列Advanced机型，官方目录与Pro分别列出。 | [DJI Download Center](https://www.dji.com/downloads)<br>[Official Historic Discontinued Aircraft List](https://repair.dji.com/help/content?customId=01700007185&documentType=&lang=en&paperDocType=ARTICLE&re=DK&spaceId=17) |
| 22 | Phantom 4 Pro V2.0<br>`dji-phantom-4-pro-v2` | 2018 | Phantom 4 Pro的V2硬件版本，需单独核对桨叶、动力和图传配置。 | [DJI Download Center](https://www.dji.com/downloads)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 23 | Phantom 4 RTK<br>`dji-phantom-4-rtk` | 2018 | 用于测绘的Phantom 4 RTK机型，外观审核需包含顶部定位模块。 | [Official Enterprise RTK Aircraft Inventory](https://repair.dji.com/help/content?customId=01700043529&documentType=&lang=zh-CN&paperDocType=ARTICLE&re=CN&spaceId=17)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 24 | P4 Multispectral<br>`dji-p4-multispectral` | — | 基于Phantom机体的多光谱航测机型，用于农业数据采集；应审核独特多镜头云台。 | [DJI Download Center](https://www.dji.com/downloads)<br>[Official Enterprise RTK Aircraft Inventory](https://repair.dji.com/help/content?customId=01700043529&documentType=&lang=zh-CN&paperDocType=ARTICLE&re=CN&spaceId=17) |
| 25 | Phantom 2 Vision+ V2.0<br>`dji-phantom-2-vision-plus-v2` | — | 母型的独立硬件修订版；需单独提交稿件和用户确认，不从母型继承通过状态。 | [Phantom 2 Vision+ V2.0 Store Archive](https://store.dji.com/be/product/phantom-2-vision-plus-2-0) |
| 26 | Phantom 2 Vision+ V3.0<br>`dji-phantom-2-vision-plus-v3` | — | 母型的独立硬件修订版；需单独提交稿件和用户确认，不从母型继承通过状态。 | [Phantom 2 Vision+ V3.0 Store Archive](https://store.dji.com/lt/product/phantom-2-vision-plus?set_region=LT) |

重点边界：

- Phantom 1：2012来自DJI公司年表；早期市场常见2013商业发售时间，两者事件口径不同，具体上市日仍待交叉核对。
- Phantom 2：Phantom 2具体V2硬件修订仍列在coverageGaps；软件v2.0不是机体版本的证据。
- Phantom 2 Vision+：通用产品页与V2/V3实物的对应关系尚需核对，避免把通用名称重复计算为第三个硬件机型。
- Phantom 4 Pro：Pro+对应带屏遥控器配置，作为套餐记录，非额外机体。Obsidian生产版云台涂层应按2017-09-26更新后的实物审核。
- Phantom 4 Advanced：Advanced+是带屏遥控器配置，不能重复统计为独立机体。
- Phantom 4 Pro V2.0：Pro+ V2.0为遥控器配置套餐，非额外机体。
- Phantom 4 RTK：DJI年表同时含2017农业解决方案中的RTK条目与2018正式发布条目；保留2018发布口径，精确首发事件待核对。
- Phantom 2 Vision+ V2.0：独立硬件版本单列为顶层 aircraft；用户确认不从母型继承。 与基础 Vision+ 条目的具体代际/初始版本关系未闭合；保留独立硬件候选并单独审核，不宣称新增一代产品。
- Phantom 2 Vision+ V3.0：独立硬件版本单列为顶层 aircraft；用户确认不从母型继承。 与基础 Vision+ 条目的具体代际/初始版本关系未闭合；保留独立硬件候选并单独审核，不宣称新增一代产品。

### 4. Mavic / 御 / Mavic

| # | 机型 / 稳定 ID | 已证实事件年 | 中文简介 | 官方依据 |
| ---: | --- | --- | --- | --- |
| 27 | Mavic Pro<br>`dji-mavic-pro` | 2016 | Mavic系列首代折叠航拍机，搭载三轴4K云台与OcuSync图传。 | [Mavic Pro Alpine White Announcement](https://www.dji.com/newsroom/news/dji-launches-limited-edition-mavic-pro-alpine-white-combo)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 28 | Mavic Pro Platinum<br>`dji-mavic-pro-platinum` | 2017 | Mavic Pro家族的Platinum机型，需单独检查机身颜色和桨叶。 | [DJI Download Center](https://www.dji.com/downloads)<br>[Mavic Pro Alpine White Announcement](https://www.dji.com/newsroom/news/dji-launches-limited-edition-mavic-pro-alpine-white-combo)<br>[年份证据](https://www.dji.com/media-center/announcements/dji-to-showcase-two-new-drones-and-introduce-unique-sphere-mode-at-ifa-2017) |
| 29 | Mavic 2 Pro<br>`dji-mavic-2-pro` | 2018 | Mavic 2系列Pro相机机型，与Zoom的相机外形分别审核。 | [Mavic 2 Downloads](https://www.dji.com/downloads/products/mavic-2)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 30 | Mavic 2 Zoom<br>`dji-mavic-2-zoom` | 2018 | Mavic 2系列变焦相机机型。 | [Mavic 2 Downloads](https://www.dji.com/downloads/products/mavic-2)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 31 | DJI Mavic 3<br>`dji-mavic-3` | 2021 | Mavic第三代影像航拍机，与Classic和Pro保持独立机体审核。 | [DJI Mavic 3 Downloads](https://www.dji.com/downloads/products/mavic-3)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 32 | DJI Mavic 3 Cine<br>`dji-mavic-3-cine` | — | Mavic 3系列Cine机型，面向专业影像工作流。 | [DJI Mavic 3 Downloads](https://www.dji.com/downloads/products/mavic-3)<br>[DJI Fly Binding Guide — Official Aircraft List](https://repair.dji.com/help/content?customId=en-us03400006568&documentType=artical&lang=en&paperDocType=paper&re=US&spaceId=34) |
| 33 | DJI Mavic 3 Classic<br>`dji-mavic-3-classic` | 2022 | Mavic 3系列Classic机型，独立审核其单主相机云台外观。 | [DJI Mavic 3 Classic Downloads](https://www.dji.com/downloads/products/mavic-3-classic)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 34 | DJI Mavic 3 Pro<br>`dji-mavic-3-pro` | 2023 | Mavic三摄机型，以多焦段影像为主要定位。 | [DJI Mavic 3 Pro Downloads](https://www.dji.com/downloads/products/mavic-3-pro)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 35 | DJI Mavic 3 Pro Cine<br>`dji-mavic-3-pro-cine` | — | Mavic 3 Pro系列Cine影像机体版本，独立审核配置与铭牌。 | [DJI Mavic 3 Pro Downloads](https://www.dji.com/downloads/products/mavic-3-pro)<br>[DJI Fly Binding Guide — Official Aircraft List](https://repair.dji.com/help/content?customId=en-us03400006568&documentType=artical&lang=en&paperDocType=paper&re=US&spaceId=34) |
| 36 | DJI Mavic 4 Pro<br>`dji-mavic-4-pro` | 2025 | 新一代Mavic三摄旗舰，外观审核需覆盖云台、传感器布局和机臂。 | [DJI IFA 2026 Official Portfolio](https://www.dji.com/media-center/announcements/ifa-2026-dji-berlin-osmo-360-2-romo-2-power-system)<br>[DJI Mavic 4 Pro and 512GB hardware, 2025-05-13](https://www.dji.com/es/media-center/announcements/dji-release-mavic-4-pro)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 37 | DJI Mavic 4 Pro 512GB<br>`dji-mavic-4-pro-512gb` | 2025 | 母型的独立存储硬件版本；需单独提交稿件和用户确认，不从母型继承通过状态。 | [DJI Mavic 4 Pro and 512GB hardware, 2025-05-13](https://www.dji.com/es/media-center/announcements/dji-release-mavic-4-pro) |

重点边界：

- DJI Mavic 3 Cine：Cine是机体影像/存储版本，区别于Fly More套餐；具体首发年尚未用独立发布公告核验。
- DJI Mavic 4 Pro：512GB存储版本走单独配置审核，不将Creator Combo视为新的飞机型号。
- DJI Mavic 4 Pro 512GB：独立硬件版本单列为顶层 aircraft；用户确认不从母型继承。

### 5. Air / Air

| # | 机型 / 稳定 ID | 已证实事件年 | 中文简介 | 官方依据 |
| ---: | --- | --- | --- | --- |
| 38 | Mavic Air<br>`dji-mavic-air` | 2018 | 第一代Air便携折叠航拍机。 | [DJI Download Center](https://www.dji.com/downloads)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 39 | Mavic Air 2<br>`dji-mavic-air-2` | 2020 | Air系列第二代航拍机。 | [DJI Download Center](https://www.dji.com/downloads)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 40 | DJI Air 2S<br>`dji-air-2s` | 2021 | 采用一英寸成像方案的Air系列机型。 | [DJI Download Center](https://www.dji.com/downloads)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 41 | DJI Air 3<br>`dji-air-3` | 2023 | Air系列双主摄航拍机。 | [DJI Download Center](https://www.dji.com/downloads)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 42 | DJI Air 3S<br>`dji-air-3s` | 2024 | Air系列双摄旅拍机，面向日夜风光拍摄。 | [DJI Download Center](https://www.dji.com/downloads)<br>[年份证据](https://careers.dji.com/zh-CN/about) |

### 6. Mini / Mini

| # | 机型 / 稳定 ID | 已证实事件年 | 中文简介 | 官方依据 |
| ---: | --- | --- | --- | --- |
| 43 | Mavic Mini<br>`dji-mavic-mini` | 2019 | Mini系列首代轻量折叠航拍机。 | [DJI Download Center](https://www.dji.com/downloads)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 44 | DJI Mini 2<br>`dji-mini-2` | 2020 | Mini系列4K轻量折叠航拍机。 | [DJI Download Center](https://www.dji.com/downloads)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 45 | DJI Mini SE<br>`dji-mini-se` | — | Mini系列入门机型；与Mini 2 SE分别审核。 | [DJI Download Center](https://www.dji.com/downloads) |
| 46 | DJI Mini 3 Pro<br>`dji-mini-3-pro` | 2022 | Mini系列Pro机型，官方年表记录其影像能力升级。 | [DJI Download Center](https://www.dji.com/downloads)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 47 | DJI Mini 3<br>`dji-mini-3` | — | Mini 3系列非Pro机型，与Pro机身和传感器分别核对。 | [DJI Download Center](https://www.dji.com/downloads) |
| 48 | DJI Mini 2 SE<br>`dji-mini-2-se` | — | Mini系列入门航拍机，官方与Mini 4K共用下载入口但为不同命名机型。 | [DJI Mini 2 SE / Mini 4K Downloads](https://www.dji.com/downloads/products/mini-2-se) |
| 49 | DJI Mini 4 Pro<br>`dji-mini-4-pro` | 2023 | Mini系列搭载全向避障的Pro机型。 | [DJI Download Center](https://www.dji.com/downloads)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 50 | DJI Mini 4K<br>`dji-mini-4k` | — | Mini系列4K入门机型，与Mini 2 SE保持独立条目。 | [DJI Mini 2 SE / Mini 4K Downloads](https://www.dji.com/downloads/products/mini-2-se) |
| 51 | DJI Mini 5 Pro<br>`dji-mini-5-pro` | 2025 | 一英寸大底成像的Mini系列Pro机型。 | [DJI IFA 2026 Official Portfolio](https://www.dji.com/media-center/announcements/ifa-2026-dji-berlin-osmo-360-2-romo-2-power-system)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 52 | Mavic Mini (Japan 199 g Edition)<br>`dji-mavic-mini-jp` | — | 面向日本市场的199g版本Mavic Mini，需独立核对电池、重量标识和机身细节。 | [Mavic Mini Japan Edition Official Blog](https://viewpoints.dji.com/blog/mavic-mini-takes-off-around-the-world) |
| 53 | DJI Mini 2 (Japan 199 g Edition)<br>`dji-mini-2-jp` | — | 日本版Mini 2，官方日文手册列出199g重量及不同电池续航条件。 | [DJI Mini 2 Japanese User Manual](https://dl.djicdn.com/downloads/DJI_Mini_2/20240110/DJI_Mini_2_User_Manual_JP.pdf) |

重点边界：

- Mavic Mini (Japan 199 g Edition)：英文括号限定是目录说明文字，非声明DJI官方商品英文全名。
- DJI Mini 2 (Japan 199 g Edition)：英文括号限定是目录说明文字。日本版参数不能直接复制到其他地区版。

### 7. Spark / 晓

| # | 机型 / 稳定 ID | 已证实事件年 | 中文简介 | 官方依据 |
| ---: | --- | --- | --- | --- |
| 54 | Spark<br>`dji-spark` | 2017 | 可掌上起飞并支持手势控制的小型航拍机，官方提供五种机身颜色。 | [DJI Spark Launch Announcement](https://www.dji.com/newsroom/news/dji-launches-spark-the-easy-and-fun-camera-drone-for-everyone)<br>[年份证据](https://careers.dji.com/zh-CN/about) |

### 8. Flip / Flip

| # | 机型 / 稳定 ID | 已证实事件年 | 中文简介 | 官方依据 |
| ---: | --- | --- | --- | --- |
| 55 | DJI Flip<br>`dji-flip` | 2025 | 可折叠全包围桨叶保护结构的Vlog航拍机。 | [DJI Flip Launch Announcement](https://www.dji.com/pr/media-center/announcements/dji-release-flip-us) |

### 9. Neo / Neo

| # | 机型 / 稳定 ID | 已证实事件年 | 中文简介 | 官方依据 |
| ---: | --- | --- | --- | --- |
| 56 | DJI Neo<br>`dji-neo` | 2024 | 轻小型掌上Vlog无人机。 | [DJI Download Center](https://www.dji.com/downloads)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 57 | DJI Neo 2<br>`dji-neo-2` | 2025 | Neo系列后续掌上机型，官方年表强调全向避障。 | [DJI IFA 2026 Official Portfolio](https://www.dji.com/media-center/announcements/ifa-2026-dji-berlin-osmo-360-2-romo-2-power-system)<br>[年份证据](https://careers.dji.com/zh-CN/about) |

### 10. Lito / Lito

| # | 机型 / 稳定 ID | 已证实事件年 | 中文简介 | 官方依据 |
| ---: | --- | --- | --- | --- |
| 58 | DJI Lito 1<br>`dji-lito-1` | 2026 | Lito系列面向初学者的航拍机；外观和功能以对应官方产品资料为依据。 | [DJI IFA 2026 Official Portfolio](https://www.dji.com/media-center/announcements/ifa-2026-dji-berlin-osmo-360-2-romo-2-power-system)<br>[DJI Official Releases — Lito April 23 2026](https://www.dji.com/au/media-center/announcements) |
| 59 | DJI Lito X1<br>`dji-lito-x1` | 2026 | Lito系列的X1机型，与Lito 1分别审核相机和传感器布局。 | [DJI IFA 2026 Official Portfolio](https://www.dji.com/media-center/announcements/ifa-2026-dji-berlin-osmo-360-2-romo-2-power-system)<br>[DJI Official Releases — Lito April 23 2026](https://www.dji.com/au/media-center/announcements) |

### 11. DJI FPV & Avata / DJI FPV / Avata

| # | 机型 / 稳定 ID | 已证实事件年 | 中文简介 | 官方依据 |
| ---: | --- | --- | --- | --- |
| 60 | DJI FPV<br>`dji-fpv` | 2021 | DJI整机FPV平台，面向沉浸式飞行。 | [DJI Download Center](https://www.dji.com/downloads)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 61 | DJI Avata<br>`dji-avata` | 2022 | Avata首代沉浸式飞行整机。 | [DJI Download Center](https://www.dji.com/downloads)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 62 | DJI Avata 2<br>`dji-avata-2` | 2024 | Avata第二代沉浸式飞行整机。 | [DJI Download Center](https://www.dji.com/downloads)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 63 | DJI Avata 360<br>`dji-avata-360` | 2026 | Avata系列360°成像无人机，结合全景拍摄与沉浸式飞行。 | [DJI Avata 360 Official Store](https://store.dji.com/ca/product/dji-avata-360-dji-rc-2)<br>[DJI IFA 2026 Official Portfolio](https://www.dji.com/media-center/announcements/ifa-2026-dji-berlin-osmo-360-2-romo-2-power-system)<br>[年份证据](https://dl.djicdn.com/downloads/DJI_Avata_360/20260423/DJI_Avata_360_Release_Notes_EN.pdf) |

重点边界：

- DJI Avata 360：官方2026展会页确认存在；没有以展会年份替代确切首发年份。

### 12. Inspire / 悟

| # | 机型 / 稳定 ID | 已证实事件年 | 中文简介 | 官方依据 |
| ---: | --- | --- | --- | --- |
| 64 | Inspire 1<br>`dji-inspire-1` | 2014 | 采用可变形机臂的早期4K专业航拍机。 | [Inspire 1 Raw Support — Hardware Revision Differences](https://www.dji.com/support/product/inspire-1-raw)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 65 | Inspire 1 V2.0<br>`dji-inspire-1-v2` | — | Inspire 1硬件修订版本；官方FAQ区分其云台减震安装板。 | [Inspire 1 Raw Support — Hardware Revision Differences](https://www.dji.com/support/product/inspire-1-raw) |
| 66 | Inspire 1 Pro<br>`dji-inspire-1-pro` | — | 采用Zenmuse X5相机的Inspire 1专业影像机型。 | [Inspire 1 Raw Support — Hardware Revision Differences](https://www.dji.com/support/product/inspire-1-raw)<br>[Inspire 1 Pro/Raw Downloads](https://www.dji.com/downloads/products/inspire-1-pro-and-raw) |
| 67 | Inspire 1 RAW<br>`dji-inspire-1-raw` | — | Inspire 1家族的RAW影像机型，独立审核相机、存储和机身配置。 | [Inspire 1 Pro/Raw Downloads](https://www.dji.com/downloads/products/inspire-1-pro-and-raw)<br>[Inspire 1 Raw Support — Hardware Revision Differences](https://www.dji.com/support/product/inspire-1-raw) |
| 68 | Inspire 2<br>`dji-inspire-2` | 2016 | 第二代专业影视航拍机，采用可更换云台载荷平台。 | [DJI Download Center](https://www.dji.com/downloads)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 69 | DJI Inspire 3<br>`dji-inspire-3` | 2023 | 面向电影制作的8K专业航拍机。 | [DJI Download Center](https://www.dji.com/downloads)<br>[年份证据](https://careers.dji.com/zh-CN/about) |

重点边界：

- Inspire 2：X4S/X5S/X7载荷配置作为挂载配置核对，不把每种云台都增算成一个独立机体。

### 13. Mavic Enterprise & Multispectral / 御行业 / 多光谱

| # | 机型 / 稳定 ID | 已证实事件年 | 中文简介 | 官方依据 |
| ---: | --- | --- | --- | --- |
| 70 | Mavic Pro (Police Edition)<br>`dji-mavic-pro-police` | — | 官方服务目录单列的警用版 Mavic Pro；外壳、标识和载荷需要单独核对。 | [Official Enterprise RTK Aircraft Inventory](https://repair.dji.com/help/content?customId=01700043529&documentType=&lang=zh-CN&paperDocType=ARTICLE&re=CN&spaceId=17) |
| 71 | Mavic 2 Enterprise (Zoom)<br>`dji-mavic-2-enterprise-zoom` | 2018 | 折叠行业机，采用变焦相机及模块化行业配件接口。 | [Mavic 2 Enterprise Downloads](https://www.dji.com/downloads/products/mavic-2-enterprise)<br>[Mavic 2 Enterprise Zoom announcement, 2018-10-29](https://www.dji.com/newsroom/news/dji-mavic-2-enterprise) |
| 72 | Mavic 2 Enterprise Dual<br>`dji-mavic-2-enterprise-dual` | — | Mavic 2 行业系列的可见光与热成像双传感器机型。 | [Mavic 2 Enterprise Downloads](https://www.dji.com/downloads/products/mavic-2-enterprise) |
| 73 | Mavic 2 Enterprise Advanced<br>`dji-mavic-2-enterprise-advanced` | 2020 | 升级后的双传感器折叠行业机，需独立核对相机组件。 | [Mavic 2 Enterprise Advanced Downloads](https://www.dji.com/downloads/products/mavic-2-enterprise-advanced)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 74 | DJI Mavic 3E<br>`dji-mavic-3e` | 2022 | Mavic 3 行业系列中面向测绘和高效航拍工作的机型。 | [Mavic 3 Enterprise Official Specs](https://enterprise.dji.com/mavic-3-enterprise/specs)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 75 | DJI Mavic 3T<br>`dji-mavic-3t` | 2022 | Mavic 3 行业系列的热成像机型，用于巡检和搜救任务。 | [Mavic 3 Enterprise Official Specs](https://enterprise.dji.com/mavic-3-enterprise/specs)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 76 | DJI Mavic 3T Advanced<br>`dji-mavic-3t-advanced` | — | 官方规格及固件下载列出的 Mavic 3T Advanced 硬件机型。 | [Mavic 3 Enterprise Official Specs](https://enterprise.dji.com/mavic-3-enterprise/specs)<br>[Mavic 3T Advanced Named Firmware Downloads](https://enterprise.dji.com/mavic-3-enterprise/downloads) |
| 77 | DJI Mavic 3M<br>`dji-mavic-3m` | 2022 | 面向农业监测和测绘的 Mavic 3 多光谱机型。 | [Mavic 3M Official Downloads](https://www.dji.com/downloads/products/mavic-3-m)<br>[年份证据](https://careers.dji.com/zh-CN/about) |

重点边界：

- Mavic Pro (Police Edition)：英文括号标识为本目录规范化译名，不是已证实的官方英文商品名。 不可将普通 Mavic Pro 材质直接认定为警用版。
- Mavic 2 Enterprise (Zoom)：早期公告称 Mavic 2 Enterprise；Zoom 在后续官方资料中用于与 Dual 区分，不增算第二个同名机体。
- DJI Mavic 3T Advanced：规格页明确列 DJI Mavic 3TA；下载页列 Mavic 3T Advanced 的独立离线固件，日期为 2025-12-11 / 2026-05-07。 部分 FAQ 仍称行业系列只有两个版本，不能用旧 FAQ 删除此条；首次发布日期仍待核对。

### 14. Matrice / 经纬

| # | 机型 / 稳定 ID | 已证实事件年 | 中文简介 | 官方依据 |
| ---: | --- | --- | --- | --- |
| 78 | Matrice 100<br>`dji-matrice-100` | 2015 | 可供开发者扩展和实验的早期 Matrice 四旋翼平台。 | [DJI Download Center](https://www.dji.com/downloads)<br>[DJI Product Archive](https://www.dji.com/products?t=1428071452743)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 79 | Matrice 600<br>`dji-matrice-600` | 2016 | 可搭载相机与行业载荷的六旋翼大型飞行平台。 | [DJI Download Center](https://www.dji.com/downloads)<br>[DJI Product Archive](https://www.dji.com/products?t=1428071452743)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 80 | Matrice 600 Pro<br>`dji-matrice-600-pro` | 2016 | Matrice 600 的专业升级机型，具有不同飞控配置和载荷平台配置。 | [DJI Download Center](https://www.dji.com/downloads)<br>[Matrice 600 Pro announcement, 2016-11](https://www.dji.com/newsroom/news/m600-pro) |
| 81 | Matrice 200<br>`dji-matrice-200` | 2017 | M200 系列基础行业机型，面向巡检和数据采集。 | [Matrice 200 Series Official Comparison](https://www.dji.com/products/compare-m200-series)<br>[M200 / M210 / M210 RTK announcement, 2017-02-26](https://www.dji.com/media-center/announcements/dji-introduces-m200-series-drones-built-for-enterprise-solutions) |
| 82 | Matrice 210<br>`dji-matrice-210` | 2017 | M200 系列的多云台挂载机型，可按任务选择载荷位置。 | [Matrice 200 Series Official Comparison](https://www.dji.com/products/compare-m200-series)<br>[M200 / M210 / M210 RTK announcement, 2017-02-26](https://www.dji.com/media-center/announcements/dji-introduces-m200-series-drones-built-for-enterprise-solutions) |
| 83 | Matrice 210 RTK<br>`dji-matrice-210-rtk` | 2017 | M200 系列内加入 RTK 定位的行业机型。 | [Matrice 200 Series Official Comparison](https://www.dji.com/products/compare-m200-series)<br>[M200 / M210 / M210 RTK announcement, 2017-02-26](https://www.dji.com/media-center/announcements/dji-introduces-m200-series-drones-built-for-enterprise-solutions) |
| 84 | Matrice 200 V2<br>`dji-matrice-200-v2` | — | M200 系列第二代基础机型，机体和电子系统修订需单独核对。 | [Matrice 200 V2 Official Support](https://www.dji.com/ca/matrice-200-series-v2) |
| 85 | Matrice 210 V2<br>`dji-matrice-210-v2` | — | M210 第二代多云台行业机型，不与初代共用已确认身份。 | [Matrice 200 V2 Official Support](https://www.dji.com/ca/matrice-200-series-v2) |
| 86 | Matrice 210 RTK V2<br>`dji-matrice-210-rtk-v2` | — | M210 第二代 RTK 定位机型，包含相应天线及接口变化。 | [Matrice 200 V2 Official Support](https://www.dji.com/ca/matrice-200-series-v2) |
| 87 | DJI Matrice 300 RTK<br>`dji-matrice-300-rtk` | 2020 | 多载荷 RTK 行业飞行平台，面向复杂巡检作业。 | [DJI Download Center](https://www.dji.com/downloads)<br>[DJI Enterprise Portfolio](https://enterprise.dji.com/)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 88 | DJI Matrice 350 RTK<br>`dji-matrice-350-rtk` | 2023 | 继承 Matrice 大型平台的升级 RTK 行业机型。 | [DJI Download Center](https://www.dji.com/downloads)<br>[DJI Enterprise Portfolio](https://enterprise.dji.com/)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 89 | DJI Matrice 30<br>`dji-matrice-30` | 2022 | 紧凑一体化行业机型，将多种可见光观测能力集成到固定相机模块。 | [Matrice 30 Official Support — Two Sensor Versions](https://www.dji.com/support/product/matrice-30)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 90 | DJI Matrice 30T<br>`dji-matrice-30t` | 2022 | M30 系列热成像机型；相机面板不同于普通 M30。 | [Matrice 30 Official Support — Two Sensor Versions](https://www.dji.com/support/product/matrice-30)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 91 | DJI Matrice 30 (Dock Version)<br>`dji-matrice-30-dock` | — | 适配初代 DJI Dock 的 M30 机场版，需核对充电接口等机体差异。 | [DJI Dock FAQ — Dock Hardware Versions](https://enterprise.dji.com/dock/faq) |
| 92 | DJI Matrice 30T (Dock Version)<br>`dji-matrice-30t-dock` | — | 适配初代 DJI Dock 的 M30T 机场版，包含热成像及机场充电配置。 | [DJI Dock FAQ — Dock Hardware Versions](https://enterprise.dji.com/dock/faq) |
| 93 | DJI Matrice 3D<br>`dji-matrice-3d` | 2024（全球） | 为 Dock 2 设计的自动化测绘飞行器。 | [DJI Dock 2 / Matrice 3D Series](https://enterprise.dji.com/dock-2)<br>[DJI Dock 2 / Matrice 3D and 3TD global launch, 2024-03-26](https://enterprise.dji.com/news/detail/dji-dock-2-release) |
| 94 | DJI Matrice 3TD<br>`dji-matrice-3td` | 2024（全球） | Dock 2 对应的热成像巡检与安防机型。 | [DJI Dock 2 / Matrice 3D Series](https://enterprise.dji.com/dock-2)<br>[DJI Dock 2 / Matrice 3D and 3TD global launch, 2024-03-26](https://enterprise.dji.com/news/detail/dji-dock-2-release) |
| 95 | DJI Matrice 4E<br>`dji-matrice-4e` | 2025 | Matrice 4 紧凑行业系列的测绘机型。 | [DJI Matrice 4 Series — 4E / 4T](https://enterprise.dji.com/matrice-4-series)<br>[Matrice 4E / 4T announcement, 2025-01-08](https://www.dji.com/media-center/announcements/matrice-4-series) |
| 96 | DJI Matrice 4T<br>`dji-matrice-4t` | 2025 | Matrice 4 系列多传感器热成像机型。 | [DJI Matrice 4 Series — 4E / 4T](https://enterprise.dji.com/matrice-4-series)<br>[Matrice 4E / 4T announcement, 2025-01-08](https://www.dji.com/media-center/announcements/matrice-4-series) |
| 97 | DJI Matrice 4D<br>`dji-matrice-4d` | 2025 | 为 Dock 3 设计的行业飞行器，也可配合对应遥控器单独使用。 | [DJI Dock 3 / Matrice 4D Series](https://enterprise.dji.com/dock-3)<br>[Dock 3 / Matrice 4D / 4TD announcement, 2025-02-27](https://www.dji.com/au/media-center/announcements/dji-release-dock-3) |
| 98 | DJI Matrice 4TD<br>`dji-matrice-4td` | 2025 | Dock 3 对应的热成像飞行器，需独立审核多传感器相机外观。 | [DJI Dock 3 / Matrice 4D Series](https://enterprise.dji.com/dock-3)<br>[Dock 3 / Matrice 4D / 4TD announcement, 2025-02-27](https://www.dji.com/au/media-center/announcements/dji-release-dock-3) |
| 99 | DJI Matrice 400<br>`dji-matrice-400` | 2025 | 新一代大型多载荷行业飞行平台。 | [Matrice 400 Official Support](https://www.dji.com/support/product/matrice-400)<br>[DJI Enterprise Portfolio](https://enterprise.dji.com/)<br>[年份证据](https://careers.dji.com/zh-CN/about) |

重点边界：

- DJI Matrice 30 (Dock Version)：官方 FAQ 将 Matrice 30 Dock Version 与普通版本区分；Dock 本体不是无人机。
- DJI Matrice 3D：此处 2024 是官方全球发布年份；国内 Dock 2 时间线较早，首次地区发布仍需另核对。
- DJI Matrice 3TD：全球发布年份，不等于所有地区最早上市年份。

### 15. Wind / Wind 定制平台

| # | 机型 / 稳定 ID | 已证实事件年 | 中文简介 | 官方依据 |
| ---: | --- | --- | --- | --- |
| 100 | Wind 1<br>`dji-wind-1` | — | 官方支持资料列出的早期定制行业飞行平台。 | [Wind 1 / Wind 2 / Wind 4 / Wind 8 Gimbal Support](https://repair.dji.com/help/content?customId=01700001344&lang=en&paperDocType=ARTICLE&re=US&spaceId=17)<br>[Official Historic Discontinued Aircraft List](https://repair.dji.com/help/content?customId=01700007185&documentType=&lang=en&paperDocType=ARTICLE&re=DK&spaceId=17) |
| 101 | Wind 2<br>`dji-wind-2` | — | 官方支持资料列出的定制行业平台，保留独立型号。 | [Wind 1 / Wind 2 / Wind 4 / Wind 8 Gimbal Support](https://repair.dji.com/help/content?customId=01700001344&lang=en&paperDocType=ARTICLE&re=US&spaceId=17)<br>[Official Historic Discontinued Aircraft List](https://repair.dji.com/help/content?customId=01700007185&documentType=&lang=en&paperDocType=ARTICLE&re=DK&spaceId=17) |
| 102 | Wind 4<br>`dji-wind-4` | 2017 | AirWorks 2017 公告推出的可定制行业飞行平台。 | [DJI Wind 4 / Wind 8 AirWorks 2017 Announcement](https://www.dji.com/lt/media-center/announcements/dji-reveals-new-technology-to-accelerate-enterprise-drone-adoption-at-airworks-2017) |
| 103 | Wind 8<br>`dji-wind-8` | 2017 | 与 Wind 4 同时公布的定制行业飞行平台；按独立机体审核。 | [DJI Wind 4 / Wind 8 AirWorks 2017 Announcement](https://www.dji.com/lt/media-center/announcements/dji-reveals-new-technology-to-accelerate-enterprise-drone-adoption-at-airworks-2017) |

重点边界：

- Wind 1：需补充官方三视图、具体载荷及外观版本，不能由后续 Wind 4/8 猜测。
- Wind 2：首次发布日期及型号外观资料仍待补充。

### 16. AGRAS MG / MG 农业植保

| # | 机型 / 稳定 ID | 已证实事件年 | 中文简介 | 官方依据 |
| ---: | --- | --- | --- | --- |
| 104 | AGRAS MG-1<br>`dji-agras-mg-1` | 2015 | DJI 早期一体化农业植保飞机，面向农作物喷洒作业。 | [Official Agras Historic and Current Model Inventory](https://repair.dji.com/help/content?customId=01700009427&documentType=&lang=zh-CN&paperDocType=ARTICLE&re=CN&spaceId=17)<br>[Agras Discontinued Model Inventory](https://repair.dji.com/help/content?customId=01700043739&documentType=&lang=en&paperDocType=ARTICLE&re=US&spaceId=17)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 105 | AGRAS MG-1S<br>`dji-agras-mg-1s` | — | MG-1 系列升级植保机，需核对其作业系统和机体细节。 | [Official Agras Historic and Current Model Inventory](https://repair.dji.com/help/content?customId=01700009427&documentType=&lang=zh-CN&paperDocType=ARTICLE&re=CN&spaceId=17)<br>[Agras MG-1S Availability Announcement](https://www.dji.com/media-center/announcements/availability-of-the-dji-agras-mg-1s) |
| 106 | AGRAS MG-1S RTK<br>`dji-agras-mg-1s-rtk` | — | MG-1S 的 RTK 定位硬件机型；独立天线/配置需审核。 | [Agras Discontinued Model Inventory](https://repair.dji.com/help/content?customId=01700043739&documentType=&lang=en&paperDocType=ARTICLE&re=US&spaceId=17) |
| 107 | AGRAS MG-1S Advanced<br>`dji-agras-mg-1s-advanced` | 2017 | 官方 MG 系列中单列的 MG-1S Advanced 植保机。 | [Official Agras Historic and Current Model Inventory](https://repair.dji.com/help/content?customId=01700009427&documentType=&lang=zh-CN&paperDocType=ARTICLE&re=CN&spaceId=17)<br>[Agras Discontinued Model Inventory](https://repair.dji.com/help/content?customId=01700043739&documentType=&lang=en&paperDocType=ARTICLE&re=US&spaceId=17)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 108 | AGRAS MG-1P<br>`dji-agras-mg-1p` | 2017 | MG-1P 系列的农业植保基础机型。 | [MG-1P / MG-1P RTK Official Product Page](https://www.dji.com/mg-1p?from=nav&site=brandsite)<br>[Official Agras Historic and Current Model Inventory](https://repair.dji.com/help/content?customId=01700009427&documentType=&lang=zh-CN&paperDocType=ARTICLE&re=CN&spaceId=17)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 109 | AGRAS MG-1P RTK<br>`dji-agras-mg-1p-rtk` | 2017 | MG-1P 系列的 RTK 定位版本。 | [MG-1P / MG-1P RTK Official Product Page](https://www.dji.com/mg-1p?from=nav&site=brandsite)<br>[Agras Discontinued Model Inventory](https://repair.dji.com/help/content?customId=01700043739&documentType=&lang=en&paperDocType=ARTICLE&re=US&spaceId=17)<br>[年份证据](https://careers.dji.com/zh-CN/about) |

重点边界：

- AGRAS MG-1S：官方全球供应公告可确认身份；首次地区发布时间尚未统一，年份暂不猜测。
- AGRAS MG-1P RTK：年份来自官方 MG-1P 系列发布时间线；独立 RTK 版本具体发布日待核对。

### 17. AGRAS T / T 农业植保

| # | 机型 / 稳定 ID | 已证实事件年 | 中文简介 | 官方依据 |
| ---: | --- | --- | --- | --- |
| 110 | DJI AGRAS T16<br>`dji-agras-t16` | 2018（国内） | 早期 T 系列农业植保机。 | [Official Agras Historic and Current Model Inventory](https://repair.dji.com/help/content?customId=01700009427&documentType=&lang=zh-CN&paperDocType=ARTICLE&re=CN&spaceId=17)<br>[Agras Discontinued Model Inventory](https://repair.dji.com/help/content?customId=01700043739&documentType=&lang=en&paperDocType=ARTICLE&re=US&spaceId=17)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 111 | DJI AGRAS T20<br>`dji-agras-t20` | 2019（国内） | T 系列农业无人机，面向植保作业。 | [Official Agras Historic and Current Model Inventory](https://repair.dji.com/help/content?customId=01700009427&documentType=&lang=zh-CN&paperDocType=ARTICLE&re=CN&spaceId=17)<br>[Agras Discontinued Model Inventory](https://repair.dji.com/help/content?customId=01700043739&documentType=&lang=en&paperDocType=ARTICLE&re=US&spaceId=17)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 112 | DJI AGRAS T30<br>`dji-agras-t30` | 2020（国内） | T30 代农业喷洒作业机型。 | [Official Agras Historic and Current Model Inventory](https://repair.dji.com/help/content?customId=01700009427&documentType=&lang=zh-CN&paperDocType=ARTICLE&re=CN&spaceId=17)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 113 | DJI AGRAS T10<br>`dji-agras-t10` | 2020（国内） | 与 T30 同代的紧凑植保机。 | [Official Agras Historic and Current Model Inventory](https://repair.dji.com/help/content?customId=01700009427&documentType=&lang=zh-CN&paperDocType=ARTICLE&re=CN&spaceId=17)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 114 | DJI AGRAS T40<br>`dji-agras-t40` | 2021（国内） | T40 代喷洒与播撒农业平台。 | [Official Agras Historic and Current Model Inventory](https://repair.dji.com/help/content?customId=01700009427&documentType=&lang=zh-CN&paperDocType=ARTICLE&re=CN&spaceId=17)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 115 | DJI AGRAS T20P<br>`dji-agras-t20p` | 2021（国内） | T20P 农业作业机，不与 T20 合并。 | [Official Agras Historic and Current Model Inventory](https://repair.dji.com/help/content?customId=01700009427&documentType=&lang=zh-CN&paperDocType=ARTICLE&re=CN&spaceId=17)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 116 | DJI AGRAS T50<br>`dji-agras-t50` | 2022（国内） | T50 代农业作业平台。 | [Official Agras Historic and Current Model Inventory](https://repair.dji.com/help/content?customId=01700009427&documentType=&lang=zh-CN&paperDocType=ARTICLE&re=CN&spaceId=17)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 117 | DJI AGRAS T25<br>`dji-agras-t25` | 2022（国内） | T25 农业机型，保留独立机体审核。 | [Official Agras Historic and Current Model Inventory](https://repair.dji.com/help/content?customId=01700009427&documentType=&lang=zh-CN&paperDocType=ARTICLE&re=CN&spaceId=17)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 118 | DJI AGRAS T60<br>`dji-agras-t60` | 2023（国内） | T60 代农业作业平台。 | [Official Agras Historic and Current Model Inventory](https://repair.dji.com/help/content?customId=01700009427&documentType=&lang=zh-CN&paperDocType=ARTICLE&re=CN&spaceId=17)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 119 | DJI AGRAS T25P<br>`dji-agras-t25p` | 2023（国内） | T25P 农业作业机，不与 T25 合并。 | [Official Agras Historic and Current Model Inventory](https://repair.dji.com/help/content?customId=01700009427&documentType=&lang=zh-CN&paperDocType=ARTICLE&re=CN&spaceId=17)<br>[Agras T25P Official Product Page](https://ag.dji.com/t25p)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 120 | DJI AGRAS T100<br>`dji-agras-t100` | 2024（国内） | T100 代大型农业作业平台。 | [Official Agras Historic and Current Model Inventory](https://repair.dji.com/help/content?customId=01700009427&documentType=&lang=zh-CN&paperDocType=ARTICLE&re=CN&spaceId=17)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 121 | DJI AGRAS T70<br>`dji-agras-t70` | 2024（国内） | 官方国内资料单列的 T70 农业机型。 | [Official Agras Historic and Current Model Inventory](https://repair.dji.com/help/content?customId=01700009427&documentType=&lang=zh-CN&paperDocType=ARTICLE&re=CN&spaceId=17)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 122 | DJI AGRAS T100S<br>`dji-agras-t100s` | 2025（国内） | T100S 农业机，不能仅凭名称当作 T100 换色。 | [Official Agras Historic and Current Model Inventory](https://repair.dji.com/help/content?customId=01700009427&documentType=&lang=zh-CN&paperDocType=ARTICLE&re=CN&spaceId=17)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 123 | DJI AGRAS T70S<br>`dji-agras-t70s` | 2025（国内） | T70S 农业机，需独立核对机体和作业系统。 | [Official Agras Historic and Current Model Inventory](https://repair.dji.com/help/content?customId=01700009427&documentType=&lang=zh-CN&paperDocType=ARTICLE&re=CN&spaceId=17)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 124 | DJI AGRAS T55<br>`dji-agras-t55` | 2025（国内） | 官方时间线与农业目录收录的 T55 农业机。 | [Official Agras Historic and Current Model Inventory](https://repair.dji.com/help/content?customId=01700009427&documentType=&lang=zh-CN&paperDocType=ARTICLE&re=CN&spaceId=17)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 125 | DJI AGRAS T70P<br>`dji-agras-t70p` | — | 官方全球农业页面列出的 T70P 作业机，支持多种农业任务配置。 | [Agras T70P Official Product Page](https://ag.dji.com/t70p)<br>[DJI Agriculture Portfolio](https://ag.dji.com/) |
| 126 | DJI AGRAS T200<br>`dji-agras-t200` | — / 发布待核对 | 官方技术参数索引已出现 T200；尚待补足截至日的公开发布与外观资料。 | [Official Agras Historic and Current Model Inventory](https://repair.dji.com/help/content?customId=01700009427&documentType=&lang=zh-CN&paperDocType=ARTICLE&re=CN&spaceId=17) |

重点边界：

- DJI AGRAS T16：年份来自 DJI 中文企业发展轴，不能等同于美国或其他地区上市年份。
- DJI AGRAS T20：年份来自 DJI 中文企业发展轴，不能等同于美国或其他地区上市年份。
- DJI AGRAS T30：年份来自 DJI 中文企业发展轴，不能等同于美国或其他地区上市年份。
- DJI AGRAS T10：年份来自 DJI 中文企业发展轴，不能等同于美国或其他地区上市年份。
- DJI AGRAS T40：年份来自 DJI 中文企业发展轴，不能等同于美国或其他地区上市年份。
- DJI AGRAS T20P：年份来自 DJI 中文企业发展轴，不能等同于美国或其他地区上市年份。
- DJI AGRAS T50：年份来自 DJI 中文企业发展轴，不能等同于美国或其他地区上市年份。
- DJI AGRAS T25：年份来自 DJI 中文企业发展轴，不能等同于美国或其他地区上市年份。
- DJI AGRAS T60：年份来自 DJI 中文企业发展轴，不能等同于美国或其他地区上市年份。
- DJI AGRAS T25P：年份来自 DJI 中文企业发展轴，不能等同于美国或其他地区上市年份。
- DJI AGRAS T100：年份来自 DJI 中文企业发展轴，不能等同于美国或其他地区上市年份。
- DJI AGRAS T70：年份来自 DJI 中文企业发展轴，不能等同于美国或其他地区上市年份。
- DJI AGRAS T100S：年份来自 DJI 中文企业发展轴，不能等同于美国或其他地区上市年份。
- DJI AGRAS T70S：年份来自 DJI 中文企业发展轴，不能等同于美国或其他地区上市年份。
- DJI AGRAS T55：年份来自 DJI 中文企业发展轴，不能等同于美国或其他地区上市年份。
- DJI AGRAS T70P：官方页面确认为 T70P；中文企业发展轴 2024 年只写 T70 系列，不能据此推断 T70P 的首次年份。 产品页内部页面标题出现 AG811；这是待核对内部标识，不额外计作一个市场型号。 与 T70/T70S 的地区命名或机体关系未证实，先保留独立型号并明确缺口。
- DJI AGRAS T200：仅以官方技术资料索引确认名称，不宣称截至日已公开上市，不猜测规格或外观。

### 18. FlyCart / FlyCart 运载

| # | 机型 / 稳定 ID | 已证实事件年 | 中文简介 | 官方依据 |
| ---: | --- | --- | --- | --- |
| 127 | DJI FlyCart 30<br>`dji-flycart-30` | 2023 | DJI 运载无人机系列首代机型，面向货物运输任务。 | [DJI Agriculture Portfolio](https://ag.dji.com/)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 128 | DJI FlyCart 100<br>`dji-flycart-100` | 2025 | FlyCart 系列大型民用运载平台。 | [DJI Agriculture Portfolio](https://ag.dji.com/)<br>[年份证据](https://careers.dji.com/zh-CN/about) |
| 129 | DJI FlyCart 200<br>`dji-flycart-200` | — / 发布待核对 | 官方兼容性支持资料已列 FlyCart 200；公开发布日期仍待核实。 | [Official SDK Product Inventory — FlyCart 200 Listed](https://repair.dji.com/help/content?customId=01700000763&documentType=&lang=en&paperDocType=ARTICLE&re=US&spaceId=17) |

重点边界：

- DJI FlyCart 200：不能将支持矩阵出现的型号自动视为截至日已上市；不编造外观、尺寸或性能。

### 19. Tello / RoboMaster TT / Tello / RoboMaster TT 教育

| # | 机型 / 稳定 ID | 已证实事件年 | 中文简介 | 官方依据 |
| ---: | --- | --- | --- | --- |
| 130 | Tello<br>`dji-tello` | — | Ryze Tech 推出的轻型教育/娱乐无人机，采用 DJI 技术并在 DJI 生态中销售。 | [Ryze Tello DJI Store](https://store.dji.com/se/product/tello?vid=38421)<br>[RoboMaster TT Global Announcement and Tello EDU History](https://www.dji.com/global/newsroom/news/robomaster-tello-talent-drone) |
| 131 | Tello EDU<br>`dji-tello-edu` | 2018 | Ryze Tech 的可编程教育机型，需与普通 Tello 外观/功能分别核对。 | [RoboMaster TT Global Announcement and Tello EDU History](https://www.dji.com/global/newsroom/news/robomaster-tello-talent-drone) |
| 132 | RoboMaster TT (Tello Talent)<br>`dji-robomaster-tt` | 2020（国内） | DJI 教育生态中的可编程无人机与扩展组件平台。 | [RoboMaster TT Global Announcement and Tello EDU History](https://www.dji.com/global/newsroom/news/robomaster-tello-talent-drone)<br>[年份证据](https://careers.dji.com/zh-CN/about) |

重点边界：

- Tello：不能把 Tello 制造商写成 DJI；属于用户要求的大疆生态范围。
- RoboMaster TT (Tello Talent)：中国企业时间线记录 2020，全球新闻稿日期为 2021-05-18，两者按地区事件并存。 创造力套装/传感器扩展模块属于配置；审核需包括选定扩展模块，不把每个模块当独立无人机。

## 5. 外观与配色子版本（每项独立确认）

以下 9 项仅为涂装/配色特别版；独立硬件修订已经提升至第 4 节的顶层 aircraft，不遗漏为仅母型选项。这些项目不把母型重复计作新一代产品，但外观审核门禁独立。Spark 的母型审核与配色审核是不同层级；母型确认不自动确认任何颜色。

| 母型 | 子版本 | 稳定 ID | 类型 | 官方依据 |
| --- | --- | --- | --- | --- |
| Phantom 4 Pro | Phantom 4 Pro Obsidian | `dji-phantom-4-pro-obsidian` | appearance_edition | [官方资料1](https://www.dji.com/media-center/announcements/update-phantom-4-pro-obsidian-gimbal-system)<br>[官方资料2](https://www.dji.com/media-center/announcements/dji-to-showcase-two-new-drones-and-introduce-unique-sphere-mode-at-ifa-2017) |
| Mavic Pro | Mavic Pro Alpine White | `dji-mavic-pro-alpine-white` | appearance_edition | [官方资料1](https://www.dji.com/newsroom/news/dji-launches-limited-edition-mavic-pro-alpine-white-combo) |
| Spark | Spark Alpine White | `dji-spark-alpine-white` | colorway | [官方资料1](https://www.dji.com/newsroom/news/dji-launches-spark-the-easy-and-fun-camera-drone-for-everyone) |
| Spark | Spark Sky Blue | `dji-spark-sky-blue` | colorway | [官方资料1](https://www.dji.com/newsroom/news/dji-launches-spark-the-easy-and-fun-camera-drone-for-everyone) |
| Spark | Spark Meadow Green | `dji-spark-meadow-green` | colorway | [官方资料1](https://www.dji.com/newsroom/news/dji-launches-spark-the-easy-and-fun-camera-drone-for-everyone) |
| Spark | Spark Lava Red | `dji-spark-lava-red` | colorway | [官方资料1](https://www.dji.com/newsroom/news/dji-launches-spark-the-easy-and-fun-camera-drone-for-everyone) |
| Spark | Spark Sunrise Yellow | `dji-spark-sunrise-yellow` | colorway | [官方资料1](https://www.dji.com/newsroom/news/dji-launches-spark-the-easy-and-fun-camera-drone-for-everyone) |
| Inspire 1 Pro | Inspire 1 Pro Black Edition | `dji-inspire-1-pro-black` | appearance_edition | [官方资料1](https://www.dji.com/cn/media-center/announcements/dji-introduces-the-inspire-1-pro-black-edition) |
| Tello | Tello Iron Man Edition | `ryze-tello-iron-man` | appearance_edition | [官方资料1](https://store.dji.com/tw/product/tello-iron-man-edition) |

## 6. 覆盖缺口与证据冲突

`completeness=not_proven` 必须保留，关闭下列缺口也不能凭感觉改成“已证明全收录”。

### G01-historical-completeness

历史及地区型号完整性尚未被证明。官方多入口已经交叉收录，但目录并非已证明无遗漏的 DJI 全量 SKU 清单。

下一步：按官方全型号索引、历史支持/停产表及每一代用户手册持续对账；新增型号保留独立待审状态。

### G02-t200-cutoff

T200 已在官方农业技术索引出现，但截至日公开发布/地区可用性证据尚未闭合。

下一步：读取技术索引指向的完整手册、初始固件、公开公告；证据闭合前不得归入已上市机型。

### G03-flycart200-cutoff

FlyCart 200 仅确认官方支持矩阵名称，缺少截至日公开发布证据。

下一步：补足官方产品页、初始固件及首发公告日期；保持发布证据待核对。

### G04-phantom2-hardware

Phantom 2 的 V2 硬件修订与套装/飞控 Naza-M V2 文字易混淆；目前未建立可追溯的独立机体版本记录。

下一步：对比官方原始/更新用户手册和产品公告的电机、ESC、遥控器/机体变化，不把软件 V2 当机体。

### G05-p2vision-revision-overlap

Phantom 2 Vision+ 基础条目与官方商店 V2.0、V3.0 的具体硬件继承关系及初始版本命名尚未完全闭合。

下一步：以手册、修订公告及原装照片绑定每个审核稿；暂保留两个子版本，不合并为已确认外观。

### G06-regional-hardware

只明确收录已证实的日本 199g Mini、警用版及 T70/T70P 等型号；FCC/CE/SRRC/日本认证 SKU、出口农业机型和电池差异尚未逐项清点。

下一步：逐型号核对官方地区用户手册及铭牌；同硬件无线法规版本不机械增加独立机体，有外观/结构差异才拆分。

### G07-release-date-order

部分早期型号仅能确认名称，年份留空。Phantom 企业时间线 2012 与常见市场 2013、Dock 2 国内/全球事件及 TT 2020/2021 属于不同事件，不能无证据统一成首次全球上市。

下一步：逐项补足官方首次公告日期和市场字段；时间轴将未知年份独立展示，禁止通过发布时间/停产日期猜年份。

### G08-appearance-and-payload-combinations

母型、颜色、载荷、护罩、折叠/展开和农业作业组件尚未全部形成可核对图稿；所有外观均需用户单独确认。

下一步：每一项提交正面、侧面、顶面、四分之三图及稿件散列；载荷配置清楚标注，资产修改重新确认。

### G09-production-assets

本目录没有最终 3D 网格/UV/材质，也没有视觉或动力学实测。官方网页存在参考图片不代表可直接复制其美术资产。

下一步：用可追溯参考制作原创审核稿与 3D 资产，核查素材使用权限和商标呈现；用户确认后再接入正式选机飞行。

### G10-current-us-availability

目录收录表示存在官方型号资料，不等同于当前美国在售、进口许可、飞行许可或 FAA 合规状态。

下一步：把美国采购/法规信息做独立有日期的资料层；不要从全球目录推断美国可用性。

### G11-mini2s-typo

官方绑定帮助页出现 Mini 2 S 文本，缺少其他官方型号目录相互支持，可能与 Mini 2 SE 混淆。

下一步：向独立官方产品/手册证据对账；未闭合前不创建臆测的 Mini 2 S 飞机。

### G12-cine-storage-scope

Cine 与 Mavic 4 Pro 512GB 是有硬件差异的配置，不是把每个 Creator/Fly More/RC 套装计为另一架飞机。硬件外观是否可共用还需核对。

下一步：核对官方机体说明、铭牌和端口，硬件差异独立保留，普通遥控器套餐仅显示为配置。

### G13-official-page-conflicts

官方动态页面有陈旧 FAQ、内部型号标题、明显年份笔误及地域跳转；需保留具体证据作用，不把支持矩阵或上传时间作为上市证明。

下一步：Mavic 3TA 优先参考规格/独立固件；T70P 保留商品名，不额外创建 AG811；引用停产表仅用于身份/生命周期。

## 7. 不计入独立机型的项目

- Fly More Combo、Creator Combo without distinct aircraft hardware、DJI RC / DJI RC-N / Goggles bundle、Phantom 4 Pro+ / Advanced+ controller bundles、Tello Boost Combo、Agriculture Solution Pack 1.0 / 2.0 / 3.0：同一机体配不同遥控器、电池或配件套餐不增算独立飞机；Cine/512GB 等真正机体硬件差异已另外记录。
- XP2.0 / XP3.1、Ace One / Ace Waypoint、Wookong-H / Wookong-M、Naza-H / Naza-M / Naza-M Lite / Naza-M V2、A2 / A3 / A3 Pro、E-series propulsion / Snail：早期 DJI 产品含飞控、地面站及动力系统，不是具有独立 DJI 机体身份的完整无人机。
- DJI Dock / Dock 2 / Dock 3、Zenmuse payloads、Ronin / Osmo / DJI Action、Goggles / Air Unit / RC、AeroScope、RoboMaster S1 / EP / EP Core、ROMO：地面基站、相机载荷、控制器、图传和地面机器人不是无人机；作为相关配置或生态介绍，不占机型数量。

## 8. 官方来源清单与核查边界

本批读取官方页面或搜索返回的官方摘要；来源的 `retrievalStatus` 在 JSON 中保留为 `page_read` / `search_result_read`，不伪称已逐页下载全文。页面为动态内容，未保存完整历史快照；截至日判定以明确日期证据优先。以下链接只提供事实追溯，不授予官方照片、商标或 3D 资产的复制使用权。

- `downloads`：[DJI Download Center](https://www.dji.com/downloads)（page_read；检索日 2026-10-09）
- `products-archive`：[DJI Product Archive](https://www.dji.com/products?t=1428071452743)（page_read；检索日 2026-10-09）
- `support`：[DJI Product Support](https://www.dji.com/support)（page_read；检索日 2026-10-09）
- `history`：[DJI Official Corporate Timeline](https://careers.dji.com/zh-CN/about)（page_read；检索日 2026-10-09）
- `flame`：[Flame Wheel ARF KIT Downloads](https://www.dji.com/downloads/products/flame-wheel-arf)（page_read；检索日 2026-10-09）
- `s800`：[Spreading Wings S800 Announcement](https://www.dji.com/ie/newsroom/news/hex-rotor-platform-spreading-wings-s800)（search_result_read；检索日 2026-10-09）
- `s800evo`：[Spreading Wings S800 EVO Announcement](https://www.dji.com/hk-en/newsroom/news/dji-released-s800-evo)（search_result_read；检索日 2026-10-09）
- `s1000`：[Spreading Wings S1000 Announcement](https://www.dji.com/pr/newsroom/news/dji-released-its-new-spreading-wings-s1000-octocopter-platform)（search_result_read；检索日 2026-10-09）
- `phantom1`：[Phantom 1 Downloads](https://www.dji.com/downloads/products/phantom)（search_result_read；检索日 2026-10-09）
- `phantom2`：[Phantom 2 Downloads](https://www.dji.com/downloads/products/phantom-2)（search_result_read；检索日 2026-10-09）
- `p2visionv2`：[Phantom 2 Vision+ V2.0 Store Archive](https://store.dji.com/be/product/phantom-2-vision-plus-2-0)（search_result_read；检索日 2026-10-09）
- `p2visionv3`：[Phantom 2 Vision+ V3.0 Store Archive](https://store.dji.com/lt/product/phantom-2-vision-plus?set_region=LT)（search_result_read；检索日 2026-10-09）
- `p3comparison`：[Phantom 3 Advanced Official FAQ](https://www.dji.com/phantom-3-adv/info)（search_result_read；检索日 2026-10-09）
- `ces2016`：[DJI CES 2016 Recap](https://www.dji.com/altitude/what-you-missed-at-ces-2016)（search_result_read；检索日 2026-10-09）
- `obsidian`：[Phantom 4 Pro Obsidian Production Finish Update](https://www.dji.com/media-center/announcements/update-phantom-4-pro-obsidian-gimbal-system)（search_result_read；检索日 2026-10-09）
- `mavic2`：[Mavic 2 Downloads](https://www.dji.com/downloads/products/mavic-2)（page_read；检索日 2026-10-09）
- `mavic3`：[DJI Mavic 3 Downloads](https://www.dji.com/downloads/products/mavic-3)（page_read；检索日 2026-10-09）
- `mavic3pro`：[DJI Mavic 3 Pro Downloads](https://www.dji.com/downloads/products/mavic-3-pro)（page_read；检索日 2026-10-09）
- `mavic3classic`：[DJI Mavic 3 Classic Downloads](https://www.dji.com/downloads/products/mavic-3-classic)（page_read；检索日 2026-10-09）
- `binding`：[DJI Fly Binding Guide — Official Aircraft List](https://repair.dji.com/help/content?customId=en-us03400006568&documentType=artical&lang=en&paperDocType=paper&re=US&spaceId=34)（search_result_read；检索日 2026-10-09）
- `alpine`：[Mavic Pro Alpine White Announcement](https://www.dji.com/newsroom/news/dji-launches-limited-edition-mavic-pro-alpine-white-combo)（search_result_read；检索日 2026-10-09）
- `mini2se`：[DJI Mini 2 SE / Mini 4K Downloads](https://www.dji.com/downloads/products/mini-2-se)（page_read；检索日 2026-10-09）
- `mini-jp`：[Mavic Mini Japan Edition Official Blog](https://viewpoints.dji.com/blog/mavic-mini-takes-off-around-the-world)（search_result_read；检索日 2026-10-09）
- `mini2-jp`：[DJI Mini 2 Japanese User Manual](https://dl.djicdn.com/downloads/DJI_Mini_2/20240110/DJI_Mini_2_User_Manual_JP.pdf)（search_result_read；检索日 2026-10-09）
- `spark`：[DJI Spark Launch Announcement](https://www.dji.com/newsroom/news/dji-launches-spark-the-easy-and-fun-camera-drone-for-everyone)（search_result_read；检索日 2026-10-09）
- `flip`：[DJI Flip Launch Announcement](https://www.dji.com/pr/media-center/announcements/dji-release-flip-us)（search_result_read；检索日 2026-10-09）
- `ifa2026`：[DJI IFA 2026 Official Portfolio](https://www.dji.com/media-center/announcements/ifa-2026-dji-berlin-osmo-360-2-romo-2-power-system)（page_read；检索日 2026-10-09）
- `ibc2026`：[DJI IBC 2026 — Lito launched in 2026](https://www.dji.com/media-center/announcements/ibc-2026-dji-sdr-transmission-2-video-us)（search_result_read；检索日 2026-10-09）
- `news2026`：[DJI Official Releases — Lito April 23 2026](https://www.dji.com/au/media-center/announcements)（search_result_read；检索日 2026-10-09）
- `avata360store`：[DJI Avata 360 Official Store](https://store.dji.com/ca/product/dji-avata-360-dji-rc-2)（search_result_read；检索日 2026-10-09）
- `inspire1`：[Inspire 1 Pro/Raw Downloads](https://www.dji.com/downloads/products/inspire-1-pro-and-raw)（page_read；检索日 2026-10-09）
- `inspire-revisions`：[Inspire 1 Raw Support — Hardware Revision Differences](https://www.dji.com/support/product/inspire-1-raw)（search_result_read；检索日 2026-10-09）
- `inspire-black`：[Inspire 1 Pro Black Edition Announcement](https://www.dji.com/cn/media-center/announcements/dji-introduces-the-inspire-1-pro-black-edition)（search_result_read；检索日 2026-10-09）
- `enterprise`：[DJI Enterprise Portfolio](https://enterprise.dji.com/)（page_read；检索日 2026-10-09）
- `m200`：[Matrice 200 Series Official Comparison](https://www.dji.com/products/compare-m200-series)（search_result_read；检索日 2026-10-09）
- `m200v2`：[Matrice 200 V2 Official Support](https://www.dji.com/ca/matrice-200-series-v2)（search_result_read；检索日 2026-10-09）
- `m30`：[Matrice 30 Official Support — Two Sensor Versions](https://www.dji.com/support/product/matrice-30)（search_result_read；检索日 2026-10-09）
- `m30dock`：[DJI Dock FAQ — Dock Hardware Versions](https://enterprise.dji.com/dock/faq)（search_result_read；检索日 2026-10-09）
- `dock2`：[DJI Dock 2 / Matrice 3D Series](https://enterprise.dji.com/dock-2)（page_read；检索日 2026-10-09）
- `dock3`：[DJI Dock 3 / Matrice 4D Series](https://enterprise.dji.com/dock-3)（page_read；检索日 2026-10-09）
- `m4`：[DJI Matrice 4 Series — 4E / 4T](https://enterprise.dji.com/matrice-4-series)（page_read；检索日 2026-10-09）
- `m400`：[Matrice 400 Official Support](https://www.dji.com/support/product/matrice-400)（search_result_read；检索日 2026-10-09）
- `m2enterprise`：[Mavic 2 Enterprise Downloads](https://www.dji.com/downloads/products/mavic-2-enterprise)（page_read；检索日 2026-10-09）
- `m2advanced`：[Mavic 2 Enterprise Advanced Downloads](https://www.dji.com/downloads/products/mavic-2-enterprise-advanced)（page_read；检索日 2026-10-09）
- `m3enterprise`：[Mavic 3 Enterprise Official Specs](https://enterprise.dji.com/mavic-3-enterprise/specs)（search_result_read；检索日 2026-10-09）
- `m3advanced`：[Mavic 3T Advanced Named Firmware Downloads](https://enterprise.dji.com/mavic-3-enterprise/downloads)（search_result_read；检索日 2026-10-09）
- `m3m`：[Mavic 3M Official Downloads](https://www.dji.com/downloads/products/mavic-3-m)（page_read；检索日 2026-10-09）
- `rtk-list`：[Official Enterprise RTK Aircraft Inventory](https://repair.dji.com/help/content?customId=01700043529&documentType=&lang=zh-CN&paperDocType=ARTICLE&re=CN&spaceId=17)（search_result_read；检索日 2026-10-09）
- `wind`：[DJI Wind 4 / Wind 8 AirWorks 2017 Announcement](https://www.dji.com/lt/media-center/announcements/dji-reveals-new-technology-to-accelerate-enterprise-drone-adoption-at-airworks-2017)（search_result_read；检索日 2026-10-09）
- `wind-support`：[Wind 1 / Wind 2 / Wind 4 / Wind 8 Gimbal Support](https://repair.dji.com/help/content?customId=01700001344&lang=en&paperDocType=ARTICLE&re=US&spaceId=17)（search_result_read；检索日 2026-10-09）
- `eos`：[Official Historic Discontinued Aircraft List](https://repair.dji.com/help/content?customId=01700007185&documentType=&lang=en&paperDocType=ARTICLE&re=DK&spaceId=17)（page_read；检索日 2026-10-09）
- `agriculture`：[DJI Agriculture Portfolio](https://ag.dji.com/)（page_read；检索日 2026-10-09）
- `ag-list`：[Official Agras Historic and Current Model Inventory](https://repair.dji.com/help/content?customId=01700009427&documentType=&lang=zh-CN&paperDocType=ARTICLE&re=CN&spaceId=17)（page_read；检索日 2026-10-09）
- `mg1s`：[Agras MG-1S Availability Announcement](https://www.dji.com/media-center/announcements/availability-of-the-dji-agras-mg-1s)（search_result_read；检索日 2026-10-09）
- `mg1p`：[MG-1P / MG-1P RTK Official Product Page](https://www.dji.com/mg-1p?from=nav&site=brandsite)（search_result_read；检索日 2026-10-09）
- `ag-eos`：[Agras Discontinued Model Inventory](https://repair.dji.com/help/content?customId=01700043739&documentType=&lang=en&paperDocType=ARTICLE&re=US&spaceId=17)（search_result_read；检索日 2026-10-09）
- `ag-props`：[Agras Arm and Motor Numbering — Geometry References](https://repair.dji.com/help/content?customId=01700008426&lang=zh-CN&paperDocType=ARTICLE&re=CN&spaceId=17)（search_result_read；检索日 2026-10-09）
- `ag-t70p`：[Agras T70P Official Product Page](https://ag.dji.com/t70p)（page_read；检索日 2026-10-09）
- `ag-t25p`：[Agras T25P Official Product Page](https://ag.dji.com/t25p)（page_read；检索日 2026-10-09）
- `sdk-inventory`：[Official SDK Product Inventory — FlyCart 200 Listed](https://repair.dji.com/help/content?customId=01700000763&documentType=&lang=en&paperDocType=ARTICLE&re=US&spaceId=17)（page_read；检索日 2026-10-09）
- `tello`：[Ryze Tello DJI Store](https://store.dji.com/se/product/tello?vid=38421)（search_result_read；检索日 2026-10-09）
- `education`：[RoboMaster TT Global Announcement and Tello EDU History](https://www.dji.com/global/newsroom/news/robomaster-tello-talent-drone)（search_result_read；检索日 2026-10-09）
- `tello-iron`：[Tello Iron Man Edition DJI Store](https://store.dji.com/tw/product/tello-iron-man-edition)（search_result_read；检索日 2026-10-09）
- `dock2-release`：[DJI Dock 2 / Matrice 3D and 3TD global launch, 2024-03-26](https://enterprise.dji.com/news/detail/dji-dock-2-release)（page_read；检索日 2026-10-09）
- `mavic4-release`：[DJI Mavic 4 Pro and 512GB hardware, 2025-05-13](https://www.dji.com/es/media-center/announcements/dji-release-mavic-4-pro)（page_read；检索日 2026-10-09）
- `avata360-release-notes`：[DJI Avata 360 release notes including 2026-03-26 initial release](https://dl.djicdn.com/downloads/DJI_Avata_360/20260423/DJI_Avata_360_Release_Notes_EN.pdf)（page_read；检索日 2026-10-09）
- `m600pro-release`：[Matrice 600 Pro announcement, 2016-11](https://www.dji.com/newsroom/news/m600-pro)（search_result_read；检索日 2026-10-09）
- `m200-release`：[M200 / M210 / M210 RTK announcement, 2017-02-26](https://www.dji.com/media-center/announcements/dji-introduces-m200-series-drones-built-for-enterprise-solutions)（search_result_read；检索日 2026-10-09）
- `m4-release`：[Matrice 4E / 4T announcement, 2025-01-08](https://www.dji.com/media-center/announcements/matrice-4-series)（search_result_read；检索日 2026-10-09）
- `dock3-release`：[Dock 3 / Matrice 4D / 4TD announcement, 2025-02-27](https://www.dji.com/au/media-center/announcements/dji-release-dock-3)（search_result_read；检索日 2026-10-09）
- `m2enterprise-release`：[Mavic 2 Enterprise Zoom announcement, 2018-10-29](https://www.dji.com/newsroom/news/dji-mavic-2-enterprise)（search_result_read；检索日 2026-10-09）
- `ifa2017`：[Mavic Pro Platinum and Phantom 4 Pro Obsidian announcement, 2017-08-31](https://www.dji.com/media-center/announcements/dji-to-showcase-two-new-drones-and-introduce-unique-sphere-mode-at-ifa-2017)（search_result_read；检索日 2026-10-09）

## 9. 本批工程自检与交付状态

已执行 Node 工程自检并通过：JSON 可解析；主型与子版本 ID 全局唯一；产品家族/类别与来源引用均有效；所有 141 项待用户确认；生产资产允许数为 0；132 / 9 / 130 / 2 的计数与实际数组一致；带年份条目都有官方年份证据链接；9 个子版本均无独立硬件条目。此检查不证明目录穷尽完整，也不是用户视觉确认。

本批没有运行 Unity、iOS/iPad 真机、外接摇杆或飞机动力学测试；没有用户视觉验收。根负责人仍需对实际画廊、资料筛选和确认记录流程进行独立 QA，再将已验证相关文件提交、推送到 GitHub。
