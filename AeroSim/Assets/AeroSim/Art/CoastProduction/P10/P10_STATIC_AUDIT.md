# P10 Coast 技术美术预算审计

- 审计方式：`STATIC_ASSET_ONLY`
- 审计范围：P01–P09 manifest 声明的资产根
- 审计状态：`CAPTURED_UNREVIEWED`
- ProductionReady：禁止自动设置
- 共享场景：未打开、未保存、未修改

## 十项合同

| ID | 合同 | 静态门禁摘要 |
|---|---|---|
| CAB-01 | 纹理分辨率 | Source ≤ 4096；Standalone ≤ 4096；Mobile ≤ 2048 |
| CAB-02 | mip / streaming | 世界纹理必须启用 mip 与 streaming；生成图源暂免 |
| CAB-03 | 压缩 | 世界纹理禁止 Uncompressed；生成图源暂免 |
| CAB-04 | 材质数 | 每 Renderer ≤ 2；每资产不同材质 ≤ 8 |
| CAB-05 | shader keyword | 声明 keyword ≤ 8；估算显式变体 ≤ 64 |
| CAB-06 | draw call | 静态估算每资产 ≤ 24；Renderer ≤ 16 |
| CAB-07 | LOD | 至少 3 级；LOD0 ≤ 120k tris；LOD1/2 ≤ 55%/25% |
| CAB-08 | billboard | 180m 前切换；720m 剔除；材质 ≤ 1 |
| CAB-09 | collision | 每资产 Collider ≤ 16；禁止 MeshCollider |
| CAB-10 | quality downgrade | Low→Medium→High→Ultra 的纹理、LOD、密度与功能单调递增 |

## 本次静态执行结果

Unity 6000.3.25f1 编译通过，validator 完整执行；门禁发现 3 项既有输入不符合 CAB-02：

1. `Assets/AeroSim/Resources/Art/MaterialsV2/coastal-rock-v2.png` 未启用 streaming mipmaps。
2. `Assets/AeroSim/Resources/Art/MaterialsV2/evergreen-needles-v2.png` 未启用 streaming mipmaps。
3. `Assets/AeroSim/Resources/Art/MaterialsV2/meadow-grass-v2.png` 未启用 streaming mipmaps。

这些 P01–P09 输入未由 P10 自动修复，避免覆盖并行工作包。门禁按预期失败并保留明确诊断。

## 使用

- Editor：`AeroSim > P10 > Validate Coast Art Budget (Static Only)`
- CI：`-executeMethod AeroSim.Editor.CoastArtBudgetValidator.ValidateFromCommandLine`

运行时 draw call、驻留内存与目标硬件性能证据仍由 P09 profiler gate 负责；P10 不把静态估算伪装成运行时通过。
