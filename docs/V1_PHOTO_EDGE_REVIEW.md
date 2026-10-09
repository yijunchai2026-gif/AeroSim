# 1.0 照片边界审查与下一波有限分包

日期：2026-10-09。先只读审查当前源码和 `V1_PROGRESS.md`；随后根授权本轮小补丁，修改 `Domain/TrainingData.cs` 并新增 `Domain/EvidencePaths.cs` 与 meta。本次未做 Unity 构建、运行应用或 Git 操作。候选 02 smoke 的 56 PASS 来自主集成人已运行证据，不是本文件新执行的结果。安全补丁通过全 Runtime Roslyn 编译与 diff 空白检查；候选 03 仍需统一构建和原生验证。

## 1. 已存在链路与证据强度

`AeroSimApplication.Flight.cs.EnsureSimulator()` 连接 `FlightSimulator.PhotoCaptureRequested` 到 `SavePhotoEvidence()`。请求冻结原 record、sessionId、目标、序号、时刻、姿态、camera、FOV、唯一文件名；保存同步执行真实 `Camera.Render()`，不跨帧读取当前会话。临时文件 `CreateNew` → `Move`，成功后才把 evidence 标 saved、增加 photos、推进专业目标。失败为 failed 事件，质量固定未评估。

`finally` 恢复 camera 的 targetTexture/位置/旋转/FOV 与全局 active RT，释放临时 texture/image，只删除本次确实创建的临时文件。不会因为预先存在同名 .tmp 就删除它。存档仍在 Finish/原有保存链路发生，不应把 PNG 写成功等同于整个档案持久化成功。

当前 `Diagnostics/PreviewCapture.cs` 已包含以下断言：专业目标顺序完成、PNG 存在、metadata 目标/状态/唯一名、同帧三拍、相机/active RT 还原、立即 Finish 后原 session 仍有全部照片。不能将这些断言扩大成写失败、拒拍、路径安全和旧档案迁移也通过。

## 2. 可在原生应用验证的 API 与缺测试

所有测试必须用独立 `--data-dir` 和只属于该批次的临时文件；恢复每一项全局状态后再跑后续 smoke，不污染真实用户档案。下表是待执行规格，不是 PASS 清单。

| 用例 | 精确入口/设置 | 预期断言 | 当前覆盖 |
|---|---|---|---|
| 写路径为普通文件 | 初始化隔离 LocalStore 后，在 QA 临时目录创建一普通文件；测试中用反射临时设置 `LocalStore.DirectoryPath` 私有 setter 到该文件路径；`app.StartFlight(config)`、`sim.Body.position.y>2`、`sim.CapturePhoto()`，finally 恢复 setter | 返回 false；新增一 failed evidence 与 capture-failed event；photos/GateIndex 不增；无错误目标推进；无成功 PNG；camera target/active RT/pose/FOV 还原；只产生预期 Warning | 缺 |
| 写失败专业任务 | 使用上述故障位置和 inspection config；飞机处于合法距离，不是过近/过远而提前拒拍 | 必须走真实 render→文件失败路径；失败仍停在同目标，恢复目录后重拍才 saved；新 sequence 唯一，先前 failed 不覆盖 | 缺 |
| 没有 writer | 隔离创建 `FlightSimulator`，调用 `Begin(config)`，不注册 `PhotoCaptureRequested`，取高度>2；调用 `CapturePhoto()` | false；failed 原因为无 writer；photos/目标不增；不留 pending；无 PNG。清理独立 sim/world，避免与 app 的 simulator 争用 | 缺 |
| writer 抛异常 | 独立 sim 注册 `PhotoCaptureRequested += request => throw ...` | false；failed、事件/原因明确；未推进目标；不冒出未处理运行异常 | 缺 |
| writer 未完成回执 | 独立 sim 注册空回调或只保存请求；调用 `CapturePhoto()` | **当前会留下 pending 并返回 false**；没有超时/取消。当前同步 app writer 不触发，但协议允许该状态，必须记录，不称请求必定终态 | 缺；已确认源码存在边界 |
| Paused 拒拍 | 活跃 session 合法高度，记录 photos/evidence/events 数量；`sim.Pause(true)` 后 `CapturePhoto()` | false；无新 evidence、事件、PNG、目标变化；恢复 Pause(false) 后可拍 | 缺独立断言 |
| Replay 拒拍 | 保存含 frames 记录，`app.BeginReplay(record)`；记录列表与原文件内容；`CapturePhoto()` | false；原 record/evidence/文件原封不动；seek/play 仍可工作 | 缺独立断言 |
| 非运行/低高度/过远/过近 | `sim.Stop()` 或高度<2；专业目标距离>18 或<safeDistance | false；无新 metadata。分别证明拒拍原因，不把存储故障和业务拒拍混为一个用例 | 部分普通流程，不完整 |
| 同帧连拍 | 真实 app writer，`CapturePhoto()` 连调三次 | 独立 evidence id/sequence/filename、三个 1280×720 可解码 PNG、time/pose 与请求一致；RT/相机还原 | 已有基本断言；解码尺寸/像素关联仍缺 |
| Capture→Finish→新会话 | capture 后立即 `sim.Finish()`、`app.StartFlight(newConfig)` | 所有成功 evidence 只归旧 session；新会话 photos=0；旧 JSON 和 PNG仍存在；course attempts 不重复增加 | 已有 Finish/Home；新会话/attempts缺 |
| 切换 camera/RT | 预设非 null camera.targetTexture 与 RenderTexture.active，保存一次；再切换视角/Replay | 两个 RT 与 pose/FOV恢复原值，不拍错相机；随后 replay仍能渲染，不泄漏跨页资源 | 已有两个 target 等值；非null/FOV/pose缺 |
| 重复回执 | 独立 sim writer持有 request，`CompletePhotoCapture(request,true)` 后再次调用 success/failed | status只转一次、计数一次、目标一次、事件一次。**该 public API 本身不验证磁盘PNG**，只能测试状态幂等，不能把它当照片生成测试 | 缺 |
| 旧 JSON | 在隔离目录放无 `photoEvidence` 的有效0.4 session，Profile.sessions指向它，`LocalStore.Sessions()`→报告/回放/导出 | 空 metadata、旧 photos 保留、不凭计数伪造图；frames/events null恢复空集合；无异常；新存档可再次读取 | 缺 |
| 畸形/部分数据 | session 的 photoEvidence 包含 null 项、events 的 kind=null、id=null/路径字符串；请求报告/导出 | 不崩溃、不越界、不链接无关文件；目前只补了集合null，没有逐项/ID校验，不能宣称全坏档容错 | 缺；存在需加固项 |

失败测试应在渲染器可用的原生运行环境做；只用 Roslyn 或 mock File.Exists 不证明 RenderTexture恢复。无 writer/状态机测试可以先单元覆盖，最终仍需要原生 smoke。

## 3. 报告路径与文件安全：初审发现及本轮修复

- `SavePhotoEvidence()` 检查 request.FileName 为 basename 且冻结 sessionId 与 record.id 一致，自产 session/capture ID 为 GUID，正常拍摄不会选择外部文件。CreateNew/不覆盖和临时文件所有权检查已经存在。
- UI `DesignReport()` 检查 `photo.sessionId==r.id`、basename、fullpath 位于 LocalStore 目录、File.Exists，再解码照片；链接用 `new Uri(...).AbsoluteUri`。图加载失败有文字状态。它仍未验证文件与 evidence.id/sequence 对应、PNG扩展/内容上限、符号链接真实目的地。
- HTML `LocalStore.ExportReport()` 对文字做 HTML encode，对文件名做 URI encode，并拒绝带目录的 filename；但其 available 判断 **缺少 sessionId==r.id**。因而篡改的本地记录可以引用目录内别的文件。正常自产记录不能触发此问题，不应把它描述成已发现公网攻击。
- ExportReport/ExportCsv/SaveSession/SaveNotes 使用 `r.id` 拼文件名，Sessions 使用 Profile.sessions中的 id 拼读取路径，没有统一 session ID/路径约束。人为编辑或未来导入的 JSON可以形成路径越界；`Path.GetFileName(photo.fileName)` 对图片的保护不自动保护报告输出路径。UI/HTML两套逻辑还可能产生不同安全判定。
- 两处只做字符串 fullpath 检查，不解析符号链接。目录里的 link可指向外部位置。由于现在只读取本地自产记录，威胁范围主要为坏档/未来导入，不是网络服务；仍建议在导入/分享前修好。

建议下一轮由单一 Domain/存储成员实现 `ValidateSessionId`（当前/legacy有效GUID策略）、`ResolvePhotoEvidencePath(record,evidence)`（session/id/序号/PNG扩展/规范化路径/禁止link或验证真实路径）、有限文件大小与解码尺寸、null项过滤；UI与HTML共用resolver。输出路径也统一用安全方法。旧档案不符合安全格式时只读恢复/明确错误，不任意写到外部、不删除旧文件。

上述段落保留初审证据，**不代表下列补丁后的状态仍完全未修复**。根授权后已实现：

- `EvidencePaths.ValidateSessionId(string id)`：合法 ASCII 字母/数字/hyphen/underscore ID，不强制 GUID，不破坏合法 QA fixture；非法参数抛 `ArgumentException`。
- `EvidencePaths.ResolveOutput(string fileName)`：安全 basename、规范化路径保持 LocalStore 目录内，检查已存在文件的 `ReparsePoint`/目录属性并拒绝；非法名抛 `ArgumentException`，链接/目录抛 `IOException`。Mac/Mono 的链接属性行为仍待原生测试，不把静态检查称平台实测。
- `EvidencePaths.ResolvePhoto(SessionRecord record, PhotoEvidence evidence)`：校验 session关联、saved状态、PNG后缀、正序号、native GUID captureId 与完整文件名对应；允许合法旧 `{session}-photo-{sequence}.png` convention但不自动制造旧metadata；无效/缺失/link返回null。
- `LocalStore.SaveSession/SaveNotes/ExportCsv/ExportReport/Sessions` 使用上述路径约束；读取时索引 ID 必须与 record.id一致。HTML照片使用同一 resolver、过滤null evidence项。`SaveNotes(record)`不修改course.attempts；不把批注当新训练。
- 根与 UI 成员负责把照片 writer 和视图/旧批注按钮接统一API，并对同步exports/notes异常给用户错误提示。本成员没有改他们文件。

尚缺：坏档events项细校验、图片大小/尺寸上限、防同机恶意进程在路径检查和I/O间主动换link（TOCTOU）、全部失败边界native tests。原生恶意fixture与暂停/回放拒拍由根补入后统一运行；本审查不预报PASS。

可验证恶意 fixture：`r.id="../outside"`，filename=`../secret.png`、绝对路径、别的 session 的照片、非PNG文件、带引号/HTML的name、basename symlink。测试只在拥有的临时目录构造；预期不越界写、不链接/解码不相干文件、用户文案不成为HTML/脚本，原文件保留。当前这些不应标通过。

## 4. 对 V1_PROGRESS 的续作建议：有限分包而非新范围

现断点准确保留候选02失败、候选03待构建、23图未通过和外部边界，继续使用原工具契约。下一波先完成03稳定化与推送，再按以下有限包接力。每包均有一个明确所有者，公共 `TrainingData.cs`/路由由根集中合并，不能多人同时改。每包独立QA，不改变原图或阈值；美术按新赛道/机体/场景素材分工，不重复生成已有图。

| 包/目标图 | 单包交付与文件边界 | 最小门禁 |
|---|---|---|
| A 安全与照片回归（12/17） | Domain成员新增安全resolver/坏档恢复；根桥接现存储调用；QA独占新增照片原生回归脚本，不改34-state冻结契约 | 上述失败/拒拍/legacy/path全部实际运行；正常56 smoke不倒退 |
| B 五步+设置事务（01/22） | 页面成员新 Onboarding/Settings partial与USS；Domain成员只新增draft辅助/偏好迁移；根改路由 | 五步真实Back/必选/重入；Save/Cancel/Restore/Preview恢复；US/Metric/Aviation单位；23图相关页重新比对 |
| C 六赛道圈赛（07/09） | 模拟成员新 ChallengeCatalog/RaceSession；HUD成员只新 race view；两美术分别路线缩略图与工业/户外赛道材质 | 六条不同真实路线、方向/跳门/计圈、Timed/Manual Practice、真best/delta、本地Ghost、记录回放对应 |
| D 逐轴build+OSD（08/18） | Domain新增AxisRates/BuildProfile/OsdLayout；页面成员新Tuning/OSD partial；根把模型接入 physics/HUD | 三轴独立物理曲线、版本/草稿/A-B；OSD选择/样式/预设/UndoRedo，preview与最终HUD一致 |
| E 专业任务地图与编辑（11/12/19/20/21） | 专业成员新增MissionDraft/Object/Trigger/Validation与真实场景坐标视口；根整合8目标和Capture条件；美术补桥梁目标/地图场景细节 | 8目标同源、地图选拖/撤销/校验/预览；五态飞检；目录搜索筛选前置；照片证据与目标/朝向规则一致 |
| F 结果/报告/回放闭环（14/15/17） | 记录成员ScoreBreakdown/最终帧/练习快照；UI成员仅对应views；PDF实现另读PDF技能与库许可，不擅自装收费服务 | 真分项、最后帧、下一课、PracticeFromHere不改原成绩；照片事件跳转；真PDF阅读器实开、内容不虚构 |
| G 真实教员LAN（13） | 单独本地host/协议/客户端/审计模块，不把现Instructor Review改名Live；不开放公网或启用收费账号 | 四独立flight客户端、Hint正确对象、接管同意/拒绝/撤销/掉线归还/审计；localhost与真实LAN证据区分 |

以上不是批准删减其余图：03/04/05/06/10/16、航拍HUD及共用页面仍受23图视觉和30流程门禁。每包完成后更新原生图册、紧凑结果、PRD和GitHub commit；错误和差异保留，主分支不自动合并。LAN包即使实现仍不代表公网/四真实设备验证通过。

Google计费/密钥、全美最新空域、iOS真机/发行、真实DJI硬件验证保留外部条件，不在这些有限本地包中擅自启动收费、部署公网或宣称替代完成。
