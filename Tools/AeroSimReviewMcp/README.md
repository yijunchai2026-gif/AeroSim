# AeroSim Review MCP / 外部 Harness

这是一个**零第三方依赖**的 Python stdio JSON-RPC/MCP 服务。默认保持原 Unity Editor Review MCP 协议和工具兼容；通过 `--mode runtime` 可切换到正在运行的 Player，只暴露 Phase 4 只读 Runtime 子集。

## 前提与启动

1. 使用 Unity Hub 或命令行打开 Unity 项目 `AeroSim/AeroSim`，等待脚本编译完成。Editor 必须保持运行，Unity 侧 Review MCP bridge 才能消费请求。
2. 检查自动定位结果和传输目录：

```bash
python3 Tools/AeroSimReviewMcp/aerosim_review_mcp.py health
```

3. 直接发送一次请求（用于 harness 调试）：

```bash
python3 Tools/AeroSimReviewMcp/aerosim_review_mcp.py request inspect_scene --timeout 30 --arguments '{}'
```

服务默认从当前目录及其父目录、脚本所在仓库根目录和这些位置的 `AeroSim/` 子目录中确定性定位 Unity 项目。也可使用 `--project /绝对/或/相对/Unity项目路径`，或设置 `AEROSIM_UNITY_PROJECT`。候选目录必须同时包含 `Assets/` 和 `ProjectSettings/`。

## Claude Desktop / MCP 配置示例

请将路径替换为本机仓库的绝对路径；stdio 模式下不要向 stdout 输出调试文本。

```json
{
  "mcpServers": {
    "aerosim-review": {
      "command": "python3",
      "args": [
        "/absolute/path/to/AeroSim/Tools/AeroSimReviewMcp/aerosim_review_mcp.py",
        "serve",
        "--project",
        "/absolute/path/to/AeroSim/AeroSim",
        "--timeout",
        "30"
      ]
    }
  }
}
```

支持 `initialize`、`notifications/initialized`、`tools/list` 和 `tools/call`。`tools/list` 与 `AeroSimReviewContracts.cs` 的只读及变更工具集合保持一致。

## 文件传输协议

每个 `tools/call` 生成唯一 ID，并以原子重命名方式写入：

- 请求：`<UnityProject>/Library/AeroSimReview/inbox/<id>.json`
- 响应：`<UnityProject>/Library/AeroSimReview/outbox/<id>-<runId>.json`（兼容旧版 `<id>.json`）

请求使用 `aerosim.review-mcp/v1`。服务等待同 ID 的固定文件名或带 run ID 后缀的响应；超过 `--timeout` 后返回明确的 MCP tool error。若请求尚未被 Unity 消费，超时时会删除 inbox 文件；若 Unity 已消费，请将操作视为可能仍在完成，不应盲目重试变更工具。

`assetPath` 与 `secondaryAssetPath` 拒绝绝对路径、`..` 穿越以及符号链接逃逸。传输目录在创建前也会检查解析后的路径，禁止写出 Unity 项目根目录。未知工具和未知参数字段在入队前拒绝。

## Runtime Player 只读模式（Phase 4）

Player 启动后，Unity 侧只在 `Application.persistentDataPath/AeroSimReview` 下创建固定的 `inbox/`、`outbox/` 与 `runs/`。Python 端必须显式指向这个 `AeroSimReview` 目录（或设置 `AEROSIM_RUNTIME_REVIEW_ROOT`），不会猜测或扫描用户目录：

```bash
python3 Tools/AeroSimReviewMcp/aerosim_review_mcp.py health \
  --mode runtime \
  --runtime-root "/absolute/persistentDataPath/AeroSimReview"

python3 Tools/AeroSimReviewMcp/aerosim_review_mcp.py request validate_scene \
  --mode runtime \
  --runtime-root "/absolute/persistentDataPath/AeroSimReview" \
  --timeout 30
```

Runtime 协议为 `aerosim.runtime-review-mcp/v1`，仅暴露：

- `inspect_scene`
- `capture_game_view`
- `get_console_logs`
- `get_performance_metrics`
- `validate_scene`

该模式不包含任何写工具，也不接受资产路径、组件名、命令或脚本参数。请求限制为 64 KiB；Unity 主线程串行消费，按文件写入时间 FIFO（文件名仅作同时间戳 tie-breaker），每次轮询最多增量枚举 32 项，队列深度最多 32，且每秒最多处理 4 个请求，避免一次性全目录数组和排序。`inspect_scene` 使用 4096 对象上限和 1 秒缓存，`get_performance_metrics` 复用同一有界层级样本并使用 1 秒缓存；响应会明确返回采样时间、缓存年龄、范围及是否截断。`validate_scene` 只执行可自动核验的基础 G0/G1/G2（草稿支撑 raycast、有限 transform、尺度、Sandbox 边界、Collider/Renderer），明确不替代地质连续性、玩家路线、正常视角构图或人工视觉判断。截图保存在隔离的 `runs/<runId>/` 内；`includeUi=false` 会在截图协程启动前失败，不生成或返回截图 artifact。

Editor 默认模式、`aerosim.review-mcp/v1`、`Library/AeroSimReview` 队列和原工具集保持不变。

## Evidence Bundle

截图、诊断、检查发现、清单和变更记录由 Unity 侧生成。响应可返回 `runId`、`evidenceDirectory`、`artifacts` 与 `findings`。Evidence manifest 的预期契约为 `aerosim.review-evidence/v1`，并区分：

- `reviewStatus`：是否完成审阅；
- `gateStatus`：是否运行并通过 gate；
- `productionStatus`：生成内容仍可能是 `GENERATED_UNREVIEWED`。

文件存在或 MCP 请求成功不等于视觉审阅、资产 gate 或生产验收已经完成。

## Phase 1–3 安全边界

- **Phase 1（只读证据）**：优先使用 `capture_*`、`inspect_*`、日志、性能、raycast、可见对象和 validator 工具；不应改变项目内容。
- **Phase 2（受控变更）**：`set_*`、组件/材质/网格/Prefab/Importer 等工具属于变更操作；调用方应提供并记录 `transactionId`，依赖 Unity Undo/事务记录，并在 Evidence Bundle 中核对前后状态。
- **Phase 3（保存与 gate）**：`run_asset_gate`、`save_scene`、`save_prefab`、重导入和回滚属于显式边界操作。保存前必须审阅证据；成功响应不自动代表人工批准、生产就绪或所有 Unity 验证通过。

外部 harness 不绕过 Unity 的序列化操作约束，不并发假定 AssetDatabase、Prefab、Scene、PlayMode、BuildPipeline 或视图截图是安全的，也不会直接编辑 Unity 资产。

## 合同测试

```bash
python3 -m unittest discover -s verification/review-mcp -p 'test_*.py' -v
```

合同测试覆盖完整 `tools/list`、路径越权拒绝、未知工具拒绝、明确超时，以及 MCP 初始化/通知行为。测试使用临时伪项目，不启动 Unity；因此测试通过**不表示**已执行 Unity Editor 集成验证。
