# RD-09 Visual Lab / 性能 / 证据捕获

## 范围

本工作包只增加可复现的视觉判断与采集基础设施，不修改核心场景、机体、材质或灯光内容。Visual Lab 消费：

- RD-01 `Rd01Manifest`：证据 ID、冻结 source/build 与 A–H case ID；
- RD-04 `CameraState` / Camera Diagnostic / vFOV→hFOV：相机与 HUD 语义；
- RD-07 `EnvironmentState` / `EnvironmentDiagnostics`：天气、时间、seed 与联动完整性；
- RD-08 `EnvironmentGoldenViews`：英雄区 1/5/15/40/80 m 固定视点。

## 页面

入口：`Settings → Visual Lab`。

页面实现顶部运行条、左控制轨、中央 viewport、右性能栈和底部捕获带。支持 Reference、Candidate A、Candidate B、A/B Split、Wipe 与 Quad Quality。未采集的 Quad 档位明确显示 `NOT CAPTURED`，不复制当前帧冒充四档证据。A/B 冻结槽只保存在当前进程，重启后重新采集。

Visual Lab 固定启动状态为 Pacific Coast / Clear / 16:30 / seed 41001。任何天气或质量变化先 staging，并要求重启固定状态，以免在同一 A/B 中混入多个变量。High 是 ART TARGET，Ultra 仅是 HEADROOM；工具不把 Ultra 画面当成 High 内容正确的替代。

## 性能采集

`VisualLabProfiler` 每个采样点记录：

- CPU/GPU frame time；
- allocated/reserved/Mono memory 与 frame GC alloc；
- Draw、SetPass、triangle；
- 活跃 renderer、去重 shared material；
- 已创建 RenderTexture 数量与估算驻留字节。

摘要输出 P50/P95/P99。`residencyTrendMbPerMinute` 是 reserved memory 对时间的一元线性回归斜率。GPU counter 不可用时保留 0，不用 CPU 时间伪造 GPU 时间。

**门禁纪律：**页面短采样和批处理短采样状态都是 `CAPTURED_UNREVIEWED`；`gateStatus` 固定为 `NOT_RUN`。REQ-PERF-001 仍要求冻结目标硬件、非 Development Player、High、预热 120 秒、固定回放 30 分钟、三次冷启动，并由 QA 审核 P95/P99 与持续驻留趋势。当前代码不会自动写 PASS。

## 截图与 manifest

每个截图使用原生 2560×1440 PNG，文件名包含 RD-01 G3 case、A–H、距离与质量。写入前后恢复 pilot Camera transform/FOV、Camera targetTexture 和 active RenderTexture。artifact 记录：

- RD-01 case ID；
- 相机位置/旋转、vFOV/hFOV；
- 距离、路径、字节数和 SHA-256；
- `CAPTURED_UNREVIEWED` 状态。

`manifest.json` 冻结 build/source、Unity/product、OS/CPU/GPU、pipeline、分辨率、质量、Camera、完整 EnvironmentState、性能摘要和截图清单，并直接关联 RD-01 manifest ID/schema/source/build。缺 build identity、30 分钟运行、完整环境联动或 40 张矩阵时，顶层状态为 `INCOMPLETE_EVIDENCE`。

## 可复跑命令

Unity Player 参数：

```text
--capture-visual-lab <output-dir>
--visual-lab-seconds 1800
--visual-lab-warmup-seconds 120
--visual-lab-quality High
--visual-lab-weather Clear
--target-hardware-profile HW-DESKTOP-1440P-HIGH-001
--build-id <immutable-build-id>
--source-commit <40-char-lowercase-sha>
--test-results <result.txt>
--data-dir <isolated-data-dir>
```

该批处理采集 A–H × 1/5/15/40/80 m、性能样本和 manifest。每次进程只记录 `coldStartRuns=1`；三次冷启动必须由三个独立 Player 进程产生三个不可变包，再由 QA 共同审核，不能用参数伪造计数。即使参数为 1800 秒，也只生成待审核事实，不自动判定 PASS。

## 当前验证边界

本轮执行了仓库级静态合同校验和 Git whitespace 校验。当前环境没有 Unity Editor/Player，未执行 Unity 编译、原生截图、目标硬件性能、30 分钟/三冷启或视觉人工验收，因此这些项目均为 `NOT_RUN`。
