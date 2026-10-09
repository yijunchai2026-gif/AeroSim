# AeroSim 1.0：23张目标与30个现有状态映射

日期：2026-10-08。逐张视觉检查后的实施清单；未完成验收。目标图位于 `../assets/aerosim-us-*`，现状位于 `../assets/ui-v04/`。图片不能反过来由现状生成一个更容易实现的“新目标”。

## 参照归一与冲突规则

源图实测 1672×941，并非原生1920×1080。使用统一等比尺度 `s = min(W/1672, H/941)`，居中偏移 `((W-1672*s)/2,(H-941*s)/2)`；矩形、字框和图标同步变换，禁止分别缩放X/Y。1920×1080时`s≈1.14772`，源图约1919×1080，剩余不足1px按固定采样规则处理。

下面边界是对原图的视觉测量近似，服务于布局结构辨认，不应假装逐像素已经标定。QA再将全部主结构精确测量成fixture。图03顶栏约76px、侧栏约216px；图06/07约82px顶栏、07侧栏约272px；不能所有页面硬套0.4的64/240。概念图的强蓝色、场景暗角和字体尺度也与旧18.3推荐token不完全一致。此次视觉优先按原图，安全/真实语义更正必须明示。

共享菜单壳实现起点为1920基准顶部88、导航248，每张图的具体区域由QA逐一测量并覆写。来源是源图而非0.4；不能只用88/248常量检查替代效果图比对，原图结构或视觉细节未匹配继续记FAIL。

图中Ready to Fly、设备数字、学员和日期是示例。只在隔离QA档案用fixture重现示例；普通运行显示真实档案/输入/会话值；视觉验收分别比较布局与真实状态，不要求造假数据。

## 23个目标：结构、交互与缺口

| ID / 概念图 | 当前运行状态 | 原图必保留结构 | 0.4准确缺口与1.0动作 | 优先级 |
|---|---|---|---|---|
| 01 `01-onboarding-learning-path` | Aerial-Onboarding | 全幅暗海岸；居中问题；三张大图卡；底5节点/Back/Continue；无常驻侧栏 | 当前3步长表单不是3卡5步。实现明确选择训练线、保存草稿、未选不能继续；补齐偏好/性能/设备/确认步骤 | P0 |
| 02 `02-controller-calibration` | Aerial-Controller | 左5步竖轨；中央大透明控制器+摇杆方向；右Live Input四轴；底帮助/返回/继续 | 当前一列轴条与映射控件。拆成三栏/四校准阶段；输入驱动图形；无设备时真实错误与键鼠继续。图中“Connect Aircraft”需更正为“Connect Controller” | P0 |
| 03 `03-aerial-home` | Aerial-Home | 侧导航；顶Hero约1080×412源像素+右Controller约324宽；二层学习路径/Readiness；底两挑战卡 | 现布局相近但顶64、字母图标、场景裁图模糊、文字小/位置不同；补原创线性图标、高分辨率无字Hero、真实继续课程/进度、卡片精准比例 | P0 |
| 04 `04-aerial-academy` | Aerial-Academy | 三列：分类~340、路径~574、详情~492源px；节点连接；选中蓝框；预览/时长/目标/两按钮 | 当前分类dropdown+双列课程卡。重构选择模型与三栏、缩略图/节点状态；锁定文案与规则一致；Watch Demo必须有真实演示或清楚不可用状态 | P0 |
| 05 `05-free-flight-setup` | Aerial-FreeFlight | 左大场景约1048×478；下四场景卡；右Flight Setup；底设备/实测性能/Start Flight吸附 | 当前长单表单，启动在下面。重构图/场景条/参数抽屉/底栏；机型选择与Hangar同一状态；模式/风/时间立即预览并实际传入config。不能硬写Estimated120FPS | P0 |
| 06 `06-fpv-home` | FPV-Home | 无侧栏全幅工业夕照；右4大入口；底左机体/模式/相机角、中央Quick Flight、右进度 | 当前小Sunset banner+长卡且有侧栏。单独沉浸模板、原创工业无字图/实际背景；快速启动保留真实控制要求；进度来自档案 | P0 |
| 07 `07-fpv-challenges` | FPV-Challenges | 侧筛选+主3×2图卡+右450源px详情；排序/网格切换；laps/personal-best；Timed/Practice/Ghost | 当前四环境文字卡、同六门路线，无laps/ghost区。实现筛选/选中/真实挑战配置；未实现的ghost/排行榜不能假正常；六赛道素材与路线匹配 | P0 |
| 08 `08-fpv-tuning` | FPV-Tuning | 3栏My Builds/~280、中央3D机体+3轴响应图、右Rates/~392；底unsaved/save-new/discard/A-B | 当前同一个rate长表单+单曲线，无build版本/每轴rate。建草稿、版本、3轴模型及真实曲线；输入数值同步；离开未保存确认；A/B需同指标会话比较 | P1 |
| 09 `09-fpv-race-osd` | FPV-Flight | 无壳/侧栏；白色mono OSD，模式顶左、laps顶中、link/voltage顶右；底best/大timer/delta/speed/alt | 当前通用带4遥测卡和操作按钮HUD。独立竞速模板，实际圈计时/最佳/差值；不伪造电压或Link；保留Pause和键鼠/触控恢复可达 | P0 |
| 10 `10-professional-dashboard` | Professional-Home | Greeting；两列下一任务/资格；训练计划横表；下技能复习/最近报告 | 当前通用hero卡+累计stat。补任务卡、真实本地分配、进度表与报告；未授权执照资格不可变“Valid”；无需用假的姓名/到期日填空 | P1 |
| 11 `11-professional-preflight` | Professional-Preflight | 大左任务地图/路线/限制区，右5检查项/展开RTH编辑；底counts/返回/受阻Start | 当前4确认checkbox，无地图/自动检测/五态。建检测与ack区分、错误原因、RTH校验和实际修复；图中5items却4passed+warning+action共6需修正 | P0 |
| 12 `12-professional-mission` | Professional-Flight | 任务进度顶栏；左In-flight导航；主桥柱视口；左下地图；底遥测；右目标8行/详情/照片 | 当前通用HUD、三目标，场景不完整桥梁。创建可飞桥梁/对应8目标、照片实际证据、距离/角度真实计算；图像质量未分析不得“Good” | P0 |
| 13 `13-instructor-live-session` | Professional-Instructor | 四画面2×2；右学员/进度/设备/事件；Send Hint/Request Control；底审计 | 当前本地分配/记录列表，没有真实视频会话。先结构与本地复核；完整要求仍包含联网会话/同意/延迟/审计。四随机或同一相机不能假称4 Learners Live；此项未做完不能称全量1.0 | P1高风险 |
| 14 `14-flight-results` | Aerial-Results | 模糊最后帧；中大面板；结果+总分；4指标；事件横线；正/改进两卡；Next/Try/Replay | 当前普通长结果表和通用评分。补最终帧与结果面板；指标按实际任务计算，不能把88/2.4s硬写；失败态/无下一课/回放缺失均可恢复 | P0 |
| 15 `15-replay-analysis` | Aerial-Replay | 应用壳；左嵌入飞行视口/视角tabs；右3曲线+双摇杆；下事件时间线/详情/controls | 当前回放复用全屏飞行HUD。嵌入RenderTexture、绑定frames绘图、seek联动/事件点击、播放/速度/相机/导出；Practice From Here不修改原成绩 | P0 |
| 16 `16-us-flight-readiness` | Aerial-Readiness | 3用途tabs；左5资源横卡+Learn/Review；右Before Outdoors插画/清单/免责声明/资源CTA | 当前4checkbox+3外链。建不同用途内容、来源与Last Reviewed、链接反馈和本地阅读状态；不声称检查已授权或FAA认证 | P0 |
| 17 `17-professional-report` | Aerial-Report，专业共享 | 顶标题/得分/姓名/date/export；下评分/路线/摘要3栏；底事件表/教员反馈 | 当前长事件/notes/导出面板。补真实分项指标、轨迹图、目标摘要、点击回放证据；PDF真实生成或清楚标Print/Save as PDF；不得虚构图像评分、签名与学员权限 | P1 |
| 18 `18-fpv-osd-editor` | FPV-OsdEditor | preset顶部；左组件库；中16:9真实预览/安全框/选中handle；右属性；底issues/undo/redo/reset/save | 当前4token+toggle，预览Coast。补选择、属性绑定、归一位置↔源px换算、撤销/重做、冲突/边界、预设；颜色/字号/opacity和最终HUD一致 | P1 |
| 19 `19-professional-mission-brief` | Professional-Brief | 左路线地图约826宽；右Objective/Steps/Risks/Pass Criteria；底read-risk gate/返回/飞检 | 当前单卡说明+目标列表。地图与任务同数据，步骤/风险/标准分卡；风险勾选仅承认阅读，不假授权；Back保留selection | P0 |
| 20 `20-professional-training-catalog` | Professional-Catalog | 上search/filter；左Skill Path；中央多列表格；右模块预览/按钮/目标/前置 | 当前任务卡列表，无检索/过滤/排序/技能树/详情。建立所见表格绑定本地课程/任务状态；实际可操作而非假12模块，空结果/锁定同设计体系 | P1 |
| 21 `21-professional-mission-editor` | Professional-MissionEditor | 顶save/preview/validate/submit；左对象库；中地图选中目标/区域；右属性；底事件时间线/validation | 当前坐标文本长表。建画布选择/拖拽↔world转换、对象库、真实规则校验/草稿、触发事件时间线；Submit若无审查服务不能假提交成功 | P1高风险 |
| 22 `22-global-settings` | Aerial-Settings | 左General/Graphics/Audio/Controls/Accessibility/Privacy；主General form+右live-preview；下Startup；底restore/cancel/save | 当前统一长表单。建分类、草稿Save/Cancel、即时预览并同设置持久化；三单位/Aviation需实际换算；有服务才启用content updates，不假云同步 | P0 |
| HUD `flight-training-ui-v1` | Aerial-Flight | 顶飞行状态；左教学/RTH card；左下圆雷达；右相机工具竖条；底统一H/D/HS/VS胶囊 | 当前通用分散4metrics+下动作条。重建独立航拍HUD、800msRTH hold/高度配置/接管状态、地图放大、真实Photo；Video按钮若无录制不能假Rec；加永久模拟导航水印 | P0 |

## 30个当前状态反向覆盖

| 当前状态/PNG | 1.0目标归属 | 不能遗漏的真实动作 |
|---|---|---|
| Aerial-Onboarding | 01；新增后续引导步状态 | 训练线、档案、偏好、返回、完成保存 |
| Aerial-Home | 03 | Continue/start、4节点、2挑战、设备、readiness |
| Aerial-Controller | 02 | 检测/4轴/校准/错误/保存/测试 |
| Aerial-Academy | 04 | 类别、选课、前置、详情、打开简报 |
| Aerial-Brief | 无专属图，沿用19的航拍简化版 | 目标、模式、控制说明、返回/飞检 |
| Aerial-Preflight | 无专属图，11的航拍简化版 | 检测、ack、错误、Start门禁 |
| Aerial-FreeFlight | 05 | 四场景/选机/天气/风/时间/模式/启动 |
| Aerial-Challenges | 无专属图，07的航拍同系版 | 挑战选择/详情/真实成绩/启动 |
| Aerial-Hangar | 无专属图，08机体展示的独立机库同系版 | 六机型预览/滚动/选择/保存/进入飞行 |
| Aerial-Settings | 22及全部分类子状态 | scale/单位/图形/声音/无障碍/保存取消 |
| Aerial-Readiness | 16 | 三用途tabs、资源、来源、更新边界 |
| Aerial-Records | 无专属图，20的记录表同系版 | 列表/空态/过滤/选择/结果/回放/报告 |
| Aerial-Results | 14 | 真实成绩/失败态/重试/下一课/回放 |
| Aerial-Report | 17共享 | 真实事件/轨迹/照片/批注/导出 |
| Aerial-Flight | HUD | 物理/输入/暂停/RTH/接管/降落/照片/结束 |
| Aerial-Pause | 无专属图，模态组件规则 | Resume/reset/save-finish/quit/cancel |
| Aerial-Replay | 15共享 | play/seek/speed/camera/events/export |
| FPV-Home | 06 | Quick Flight、学院/挑战/机库调参/回放 |
| FPV-Academy | 无专属图，04路径拓扑+FPV路线 | 阶段/课程/说明/飞检/启动 |
| FPV-Challenges | 07 | filter/sort/select/Timed/Practice/真实lap |
| FPV-Tuning | 08 | drafts/per-axis/curves/versions/discard/test |
| FPV-OsdEditor | 18 | select/drag/property/preset/undo/save |
| FPV-Flight | 09 | manual/gates/laps/collision/reset/pause/finish |
| Professional-Home | 10 | assignment/progress/skills/reports/catalog |
| Professional-Catalog | 20 | search/filter/sort/detail/brief/locked态 |
| Professional-Brief | 19 | risks/targets/map/read-gate/preflight |
| Professional-Preflight | 11 | 5检查项/自动检测/修复/阻止与允许 |
| Professional-MissionEditor | 21 | draft/select/move/properties/validate/preview |
| Professional-Instructor | 13；本地复核与联网实时模式区分 | assignment/replay/review；联网功能单列门禁 |
| Professional-Flight | 12 | target顺序/目标详情/实际Photo/RTH/report |

Hangar两张滚动截图不是额外页面；缩放截图不是额外目标。0.4长页只截一个视窗，1.0要验证滚动区全部动作。FPV和Professional也要用同一真实结果/回放/报告流程测试，不只航拍入口测试。

## 控件与边界一致性

源图没有绘制的页面用同产品线组件体系，但在验收表明确“派生设计，非独立原始概念图”；不能宣称有30张原图。状态仍需默认/悬停/按下/焦点/禁用/加载/错误，窄屏详情抽屉保持所有原信息可达。

所有数字/文字/图标为原生元素，不烧进大背景；无字插画仅负责美术。高分辨率素材不能改变图03的海岸构图、图06的工业夕照、图12的桥梁主题。动画、背景视频和照片化场景与静态原图的差别独立记录；一处差异不能通过重画目标绕过。

## 本轮视觉基准如何验收

逐页输出原图、真实运行图、叠加和差异；保持统一视口/倍率/字体/静态时间。原图像素对照与功能交互分开出结果。图稿中生成式文字逻辑错误修正表：02连接真实飞机误导；04“Unlocked”与锁图标冲突；11检查统计不一致；13远程Live不支持；17图像质量、PDF与审阅人；18不同单位样例；22离线内容更新。更正语义不免除该处布局/字体/颜色一致检查。

签收页必须让用户逐个目标确认。完成所有23结构和30真实流程、解决严重差异、独立QA通过后才交1.0人工验收候选；任一目标的服务/场景/交互未实现保持明确缺项，不更改完成定义。
