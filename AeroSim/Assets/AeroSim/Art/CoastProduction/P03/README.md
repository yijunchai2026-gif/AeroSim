# P03 海岸地表资产包

隔离交付目录，负责人 **Coast Art Lead-03**。本包冻结 10 个地表子资产的米尺度、铺贴、宏观变化、法线强度、roughness、LOD 与 streaming 合同；不接入运行时，不修改共享环境组装。

## 来源与边界

- 来源：`source/coastal-aggregate-reference.png`，复制自 `artifacts/generated_image_035645.png`。
- SHA-256：`1ed0eaa6db628cb8835b692e0db4f0f3c7117e8682ee358688474e147ae3c4e6`；2880×2880 RGB8。
- 图像可作为砂、砾石、贝壳的颜色与颗粒词汇来源，但当前并非已证明无缝、去光照或经物理标定的 PBR 贴图。
- manifest 中 `roughness` 与 `normalStrength` 是 LookDev 起始规格，不是实测材料参数。Built-in 使用 `smoothness = 1 - roughness`。

## 交付状态

10 份子资产 manifest 与统一 `material-contract.json` 可做静态校验。贴图派生、去光照、无缝化、法线/高度烘焙、Unity 导入、目标设备 streaming 与视觉/性能验收均明确保持 `NOT_RUN`，避免把源图或规格冒充已完成 PBR 材质。
