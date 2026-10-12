# RD-02 / RD-06 Mini 4 Pro 接口审计与 Intake 准备

## 结论

当前 Mini 4 Pro 正式内容状态为 `CONTENT_NOT_DELIVERED`。本次只建立无成本接入合同、文档和静态校验，不采购、不制作、不生成或伪装正式高模/贴图/Prefab。现有程序化机体继续保持 `ProgrammaticPrototype / Unverified`，UI 不得宣称正式身份。

## 接口审计

| 接口 | RD-02 提供 | RD-06 消费 | 当前结论 |
|---|---|---|---|
| 资产身份 | `runtimeId=mini4`、稳定 asset/catalog ID | `ProductionAssetLoader.Inspect("mini4")` | 保持 `Unverified` |
| 质量状态 | `ProgrammaticPrototype`，有序状态机 | 仅接受 `ProductionReady` | 未满足，拒绝正式装配 |
| 内容交付状态 | `productionContentStatus` | 只有精确为 `DELIVERED` 才继续 | 当前 `CONTENT_NOT_DELIVERED`，fail-closed |
| DCC 来源 | production root、FBX/GLB、SHA-256 | loader/import gate 复核 | 字段为空，未交付 |
| Prefab 定位 | 可选 prefab/resource/address 字段 | provider 解析与装配 | 字段为空，未交付 |
| 装配合同 | socket、LOD、碰撞、材质预算 | assembly validator | 文档化，暂无实物可验证 |
| UI 真值 | 仅双门通过才可 claim identity | Hangar 读取 loader + asset state | 继续显示 prototype / unverified |

## Fail-closed 顺序

1. RD-06 adapter 对缺失/空白交付状态默认赋值 `CONTENT_NOT_DELIVERED`。
2. `ProductionAssetLoader.Inspect` 要求交付状态精确为 `DELIVERED`；否则立即返回 `ContentNotDelivered`。
3. 即使状态被更新，仍须通过 RD-02 `ProductionApproved + ProductionReady`、manifest 全量校验、正式源路径/格式/SHA-256、prefab resource/address 检查。
4. provider 找不到 Prefab 或装配校验失败时继续拒绝；没有程序几何或 fallback 正式资产。

## 已准备接口

- Intake 目录：`Assets/AeroSim/Art/AircraftProduction/mini4/`
- Placeholder：`intake-manifest.placeholder.json`
- 命名、贴图通道和 Prefab 装配模板：目录 `README.md`
- 人工验收表：`INTAKE_CHECKLIST.md`
- 静态门禁：`verification/rd06/validate_mini4_pro_intake.py`

正式内容未来到货后必须以实际哈希、实际路径和实测证据替换 placeholder；不得仅修改状态绕过门禁。
