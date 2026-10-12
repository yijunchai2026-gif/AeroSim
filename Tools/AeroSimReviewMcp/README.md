# AeroSim Review MCP / 外部 Harness

这是一个**零第三方依赖**的 Python stdio JSON-RPC/MCP 服务。它只负责把 MCP 工具调用安全地投递给正在运行的 Unity Editor，并等待 Editor 写回结果；实际检查、截图、变更和 Evidence Bundle 均由 Unity 侧实现。

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
