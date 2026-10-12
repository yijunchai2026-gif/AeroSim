# P05 工业设施资产包

独立交付目录，仅包含 P05 的规格、清单、参考图与静态校验工具；不写入或修改任何共享场景组装文件。

## 内容

- `manifests/`：10 个子资产 manifest。
- `standards.json`：统一 modular kit、坐标、pivot、snap grid、PBR、LOD 与碰撞基线。
- `manifest.schema.json`：manifest JSON Schema。
- `source/generated_image_035559.png`：镀锌板、铆钉/螺栓、接缝及盐雾氧化的视觉参考。
- `tools/validate_manifests.py`：零第三方依赖静态校验器。
- `validation_report.md`：本次校验结果。

## 资产清单

| ID | 子资产 | 用途 |
|---|---|---|
| P05_IF_PANEL | 仪器房板材 | 仪器房外墙、转角、封边与检修板模块 |
| P05_IF_GUARDRAIL | 护栏 | 平台和走道防护模块 |
| P05_IF_GRATING | 格栅 | 走道、平台与踏步模块 |
| P05_IF_PIPE_RACK | 管架 | 管线支撑框架模块 |
| P05_IF_BEACON | 灯标 | 工业警示灯与安装底座 |
| P05_IF_CABLE | 缆索 | 电缆/钢缆样条模块 |
| P05_IF_BOLT_SET | 螺栓组件 | 标准紧固件与实例化组件 |
| P05_IF_HINGE | 铰链 | 门板与检修盖旋转组件 |
| P05_IF_SALT_OXIDATION | 盐雾氧化 | 镀锌钢盐雾老化材质变体 |
| P05_IF_COLLISION_PROXY | 金属碰撞代理 | 可复用简化碰撞体模块 |

## 坐标与命名

- 右手坐标；Z 向上、X 向前、Y 向右；单位 cm。
- 网格：`SM_P05_<TYPE>_<NAME>_<SIZE>_L##`。
- 碰撞：`UCX_<RenderMeshName>_##`；复杂碰撞仅在 manifest 明确允许时使用。
- 材质：`M_P05_*`；材质实例：`MI_P05_*`；纹理：`T_P05_*_<BC|N|ORM|E|M>`。

## 使用方法

1. 先读取 `standards.json`，再读取目标 manifest；manifest 的 override 优先。
2. 以 `snap_grid_cm` 和各模块 `sockets` 组装，禁止缩放碰撞体来修正尺寸。
3. 参考图仅用于材质与表面语言，不将图中接缝/螺钉阴影烘入所有资产。
4. 执行静态校验：`python3 P05_industrial_facilities_asset_pack/tools/validate_manifests.py`。

## 边界

本包为资产生产 manifest 与技术标准交付，不包含共享场景实例、关卡组装、引擎导入产物或版本提交。