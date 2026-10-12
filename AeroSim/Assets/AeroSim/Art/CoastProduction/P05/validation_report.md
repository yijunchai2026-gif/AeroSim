# P05 静态校验报告

- 校验时间：2026-10-12 03:57 CST
- 校验方式：`python3 tools/validate_manifests.py`
- 结果：**PASS**

## 校验项

| 项目 | 结果 |
|---|---|
| manifest 数量为 10 | PASS |
| 10 个 `asset_id` 唯一且与计划清单完全匹配 | PASS |
| JSON 可解析、必需字段齐全 | PASS |
| `schema_version` / `package_id` 一致 | PASS |
| 参考图相对路径一致 | PASS |
| 参考图 SHA-256 为 `a2e2c8c43ffecc0579ee1daa432bf3d1f07f5ea26ce2710405ebb0d434944d00` | PASS |
| PBR metallic / roughness 范围位于 0–1 且顺序有效 | PASS |
| translation / rotation snap grid 为正值 | PASS |
| 每项均定义 LOD0–LOD3 四档预算 | PASS |
| LOD 屏占比统一为 0.35 / 0.16 / 0.07 / 0.025 | PASS |
| README、schema、standards 文件存在 | PASS |

## 交付边界确认

- 全部新增内容位于独立目录 `P05_industrial_facilities_asset_pack/`。
- 未写入共享场景、关卡、prefab/blueprint 组装文件。
- 未执行版本提交。
