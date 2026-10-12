# P04 起降设施资产包

独立、场景无关的起降设施资产定义，面向米制、Y-Up、右手坐标系工作流。资产包只包含源图、10 个子资产 manifest、材质/Decal 合同、碰撞与尺寸合同及静态校验，不包含也不修改任何共享场景、Prefab 或关卡文件。

## 内容

- `package.manifest.json`：资产包索引与源文件锁定信息。
- `manifests/`：10 个独立子资产 manifest。
- `contracts/material.contract.json`：混凝土、金属、涂料与湿迹材质接口。
- `contracts/decal.contract.json`：标线、磨损、盐蚀、积水污迹的独立 Decal 接口与排序。
- `contracts/collision_dimensions.contract.json`：尺寸、原点、碰撞、排水坡度和装配规则。
- `sources/`：锁定的混凝土源和 Decal atlas 源。
- `validation/validate.py`：只读静态校验器。

## 装配基准

- 平台设计尺寸：`24.0 × 24.0 m`，总高 `0.55 m`。
- 平台顶面：`Y=0.00 m`；混凝土基座向下延伸至 `Y=-0.55 m`。
- H 标线中心和资产原点：`(0, 0, 0)`。
- 排水槽位于平台 `+X` 边，纵向沿 `Z`；平台面向排水槽做 `1.5%` 单向坡。
- Decal 接触偏移：顶面上方 `0.003–0.012 m`，避免 Z-fighting。

## 使用边界

1. 运行时材质实例必须独立，不得修改项目共享材质。
2. 标线几何遮罩为程序化/矢量输入；`P04_decal_atlas_source.png` 只作为磨损、盐蚀、湿迹及标线破损调制源，不假定其灰底自带透明通道。
3. Atlas 入库前按合同生成 alpha；禁止将灰底直接作为透明度。
4. 碰撞仅使用合同指定的简化体；Decal 和纯视觉磨损层不生成碰撞。
5. 本包没有场景依赖，集成方自行实例化，不自动写入共享场景。

## 静态校验

```bash
python3 P04_TakeoffFacility_AssetPack/validation/validate.py
```
