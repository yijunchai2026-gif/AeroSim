# AeroSim 1.0 独立视觉与回归验收

对应 2026-10-08 用户要求：跟 23 张原效果图做成相同页面结构和视觉，最终由用户验收。独立 QA 不自行确认 milestone，也不把 0.4 几何零失败当作 1.0 视觉一致。

## 目标与证据

- `verification/v1-qa/reference-manifest.json` 锁定 23 张原 PNG 的 SHA256、**1672×941** 尺寸、页面映射和核心 ROI。原 PNG 不改。
- 原图等比 Lanczos **contain** 到 1920×1080、居中 `#090D14` 补边，不 stretch/crop；报告记录倍率/目标尺寸/pad。运行截图必须真实 1920×1080，绝不伸缩让结果更像目标。
- 基准 UI/HUD/Text100%。首次引导必须到学习路径选择步骤，不以档案填写步骤替代；教员四宫格若本地模拟必须明示；结果/回放记录为 QA fixture，不冒充用户成绩。
- ROI 按图手工视觉分区，不是精确 Figma 组件尺寸；逐区像素和边缘指标与标注图供人工验收。
- 没有专属概念图的机库/记录/暂停等依 PRD 和共同组件检查，不伪造原设计图。

## 原图与 PRD 冲突

旧 PRD 顶栏64、导航240/72 与部分原图桌面外壳约88/248 不一致。最新用户以效果图为准，技术负责人指定 1.0 的88/248 壳并逐页验收。旧合同和历史报告不改；原生 QA 的临时复制版把旧冲突改名 `legacy-prd.*`，保留失败单列，不删除断言。其它可达性/字体/HUD安全区继续独立测试；冲突不能通过隐去动作入口刷通过。

新版原生门禁另验88顶栏、248/72导航，HUD88顶栏/80遥测条、真实回放RenderTexture、所有可见HUD区域/标签不越界、中央视野避让和模块不重叠。88/80是候选统一组件约定；各概念图存在局部尺寸差，最终逐页视觉ROI仍独立判定，不把组件数值通过当作原图完全相同。新界面带图标按钮的Button.text可能为空，QA通过子元素design-button-label识别动作；Results显式选Replay Flight，不假定其主按钮永远是回放。

Candidate01独立审查记录在 `verification/v1-qa/candidate-01-review.md`，23/23仍VISUAL_DIFFERENT。真实发现6页主列坍缩，不能归文字测量误报；新桌面主列≥160px断言仅防灾难性布局回归，不是原图精确一致性门禁。真实键盘断言保留，新增只读KeyDown/NavigationMove/Submit、焦点路径、Toggle.focusable诊断，区分UI事件翻译与业务失败，不消费/改写测试输入结果。

## 工具命令

```bash
QA_PYTHON=/Users/bytedance/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/bin/python3
"$QA_PYTHON" verification/v1-qa/test_compare.py

# 根负责人提供候选，工具只复制/注入 temp，绝不替换交付应用。
bash verification/v1-qa/build-instrument.sh /absolute/path/AeroSim.app
# 最后一行返回 /private/tmp/aerosim-uiqa.XXXXXX/AeroSim-UIQA.app
bash verification/v1-qa/run-matrix.sh /private/tmp/aerosim-uiqa.XXXXXX/AeroSim-UIQA.app

"$QA_PYTHON" verification/v1-qa/compare.py \
  --captures verification/v1-qa/results/native/desktop-1920x1080 \
  --output verification/v1-qa/results/visual \
  --native-report verification/v1-qa/results/native/desktop-1920x1080/report.json
```

读取 `100-text100-STATE.png`（新原生QA）、`100-text1-STATE.png`（历史工具）或 `STATE.png`。同目录 `report.json` 提供真实版本、日期、视口/比例；外部捕获可提供 `capture-meta.json`，字段 `version`、`captureSource`、`startedUtc`、`cases`。没有元数据是 `UNVERIFIED`，不能确认构建来源。

输出紧凑 `summary.json`、可展开 `report.html`、并排/50%overlay/差异热图/ROI标注 PNG。只写派生产物，原图与运行截图只读。退出码2表示缺图/参考错误/视觉差异；退出码0仍是 **REVIEW_REQUIRED**，不是人工通过。

矩阵定义直接内嵌 `run-matrix.sh`，没有另一个 `matrix.json`。脚本逐配置保留 `execution.json` 的真实应用退出码；单配置exit2/异常不会阻止后续配置。全部结束由 `aggregate-native.py` 检查七组视口、34状态身份和6×4比例完整性，生成 `native-summary.json`，保留硬失败/文字候选/旧合同冲突/非零退出。缺report为RUN_ERROR或NOT_RUN；已有report的目标目录必须换新批次，不能复用旧证据。最终非零退出/未通过/需复核返回2，只有完整原生断言无失败才标PASS_AUTOMATED_ONLY，仍不是视觉/用户验收。

增加 `Interaction-Modal-OSD-ReplayContext`：Objective阅读后Esc关闭且恢复原运行状态；暂停后退出确认Esc回暂停而不解冻；FPV两个安全锚点实际遥测坐标改变并保存重载；跨工作台回放标明记录的真实工作区。Candidate02每配置为34×比例组合+3个独立交互/偏好探针。冻结工具后再构建/运行，同一批次运行中不升级契约。

Candidate02新增3状态，保留旧通用自由飞行两状态 `Aerial-FlightGeneric` / `FPV-FlightGeneric` 与 `Aerial-ResultsFailure`，不删除旧检查。23映射页的FPVFlight改用真实Warehouse/Manual/race/gates配置，AerialFlight改用真实a10 Return-to-Home课程；结果/回放/报告使用明确标注的合成成功QA记录（41帧、5个带QA synthetic标签的事件、88分、passed=true），不是实飞记录/用户成就/真实照片。失败记录仍2帧、0事件、32分、passed=false；所有fixture照片数0、无伪造图像。原始candidate01报告不回写或重算为候选02通过。

Candidate02独立结论见 `verification/v1-qa/candidate-02-review.md`：原生37/3949/83/0，原6页坍缩修复但4项Toggle真越界、6键盘失败仍在，23视觉仍不同。候选03新增InputActions Space后，用独立 `build-space-submit-probe.sh` / `SpaceSubmitProbe.cs`验证真实Submit业务、隐藏modal焦点残留、飞行Arrow/Stick导航不误触且菜单/暂停导航仍正常。其报告 `space-submit-report.json`独立保存，不改变冻结矩阵契约；目前编译通过，不推断native通过。

Candidate03真实复核见 `verification/v1-qa/candidate-03-review.md`：冻结snapshot37/3926/55/0（54legacy＋1文字候选，无该合同硬失败）；独立Space54checks仍3FAIL为DownArrow窃取Pause焦点。Space一次翻转、真实launch、Stick导航和nestedconfirm焦点通过；额外实看发现Tuning内部numeric TextElement16px字号仅8px字框，未被Label/Button门禁覆盖，仍是真裁切。23页仍VISUAL_DIFFERENT。不得把冻结几何无hard失败或checkpoint提交等同全面视觉验收。

## 发布精选真实图册

```bash
/Users/bytedance/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node \
  verification/v1-qa/publish-gallery.mjs \
  --captures verification/v1-qa/results/native-candidate/desktop-1920x1080 \
  --batch candidate-20261008-01
```

发布要求完整23张1920×1080、UI/HUD/Text100%原生截图及原生report。任一缺图/元数据不符/原图哈希改变直接拒绝；旧0.4不能改标签冒充1.0。同批次若来源report/PNG不同拒绝覆盖。脚本只字节复制，不重采样、不画图，输出 `assets/ui-v1/批次/manifest.json`（应用版本/时间/视口/比例/来源reportSHA/每图SHA）、GitHub可直接显示的 `gallery.md`，及根目录 `AeroSim-1.0-开发对照.html` 全23页原图与实际并排。视觉与人工验收始终PENDING；主产品文档由根负责人引用此图册。

## 判定

- 无截图 `NOT_RUN`；原图SHA/尺寸不同 `REFERENCE_CHANGED`；截图尺寸不符 `INVALID_CAPTURE`。
- 默认无遮罩。全图/各ROI分别报告 RGB绝对误差均值MAE、差异>16/255的像素比例、灰度边缘MAE。筛选阈值 MAE≤3、差异≤1%、edgeMAE≤5 不等于“一模一样”。
- `exactUnmodifiedMatch` 仅未遮罩像素严格相同为真，但还要检查功能/语义；贴设计图做不可交互全屏背景不能通过。
- 可选 `--masks masks.json`，格式 `STATE: [{"rect": [x,y,w,h], "reason": "具体时变区域理由"}]`。每个遮罩披露；保留 raw/unmasked 两套分数。遮罩>40%或遮完核心ROI `MASK_REVIEW_REQUIRED`，禁止遮掉布局/文字/主动作制造一致。
- 任何裁切、低清、错误图标、假设备连接/成绩/实时飞行审批、未实现交互均需复核。所有 `manualAcceptance` 为 `PENDING`。

## 缩放与键鼠/保存回归

1920×1080、1280×720、2560×1080、3840×2160 进行**6×4笛卡尔积**：UI/HUD80/90/100/110/125/150%，Text100/125/150/200%。UI与HUD独立即时生效/重载/回菜单由偏好回归另验，矩阵同倍率不替代独立保存。1024×768、844×390、390×844 测80/100/150%×Text100/200%；这些仅macOS逻辑视口，不是iOS/iPad真机。

每组合测试34状态、主动作滚动可达、六机型、原生指针启动/暂停/继续。新输入探针用真正 InputSystem Keyboard 的 Tab、Enter、Space、Esc 测首页→课程→简报→飞前检查→飞行→暂停/继续→结果→回放，暂停12次Tab不逃出。初始Focus用于定位待测控件，不冒充完整顺序焦点验收。人工补充Shift+Tab/方向键/手柄、焦点可见性、禁用/加载/异常恢复。

全部PNG/日志不提交，仅必要摘要/精选证据。共享机器GUI仅根负责人启动，避免争用屏幕。单元测试只证明比较工具逻辑，不代表1.0候选构建已经测试。

## 最终人工门禁

| 项目 | 要求 | 默认 |
|---|---|---|
| 23页视觉 | 并排/overlay逐项确认结构、字体、颜色、图标、图片清晰度，记录剩余差异 | NOT_RUN / PENDING |
| 主流程 | 三工作区、机型、飞行、暂停、结算、回放、编辑/报告操作真实生效 | NOT_RUN |
| 缩放 | 完整矩阵、200%文字无裁切、关键动作可达 | NOT_RUN |
| 真实性 | 离线/真实设备能力/本地教员/合成成绩明示，不冒充Google/FAA实时许可 | PENDING |
| 设备/性能 | 桌面无崩溃/帧率；移动设备另行真机测 | NOT_RUN |
| 用户milestone | 用户打开新构建后明确确认 | PENDING |
