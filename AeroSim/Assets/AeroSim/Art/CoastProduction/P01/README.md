# Coast Production P01 — 悬崖资产包

> **PROCEDURAL / GENERATED SOURCE — NOT A SCAN**

本目录是独立、可迁移的海岸悬崖资产库，不修改或依赖共享场景组装文件。所有几何由 `Editor/CoastP01Generator.cs` 程序化生成；`Source/generated_image_035637.png` 仅作为已授权的生成式岩石材质源。不得把本包描述为摄影测量、激光扫描或实景扫描资产。

## 交付结构

- `package-manifest.json`：集成器入口，固定 10 个子资产及材质参数。
- `Manifests/*.json`：10 个明确子资产契约。
- `Meshes/*.asset`、`Prefabs/*.prefab`：运行生成器后得到的 Unity 资产。
- `Materials/CoastRockP01.mat`：共享材质实例。
- `Shaders/CoastRockP01.shader`：世界空间三平面投射、层理、宏观变化和湿润控制。
- `Source/`：生成式源图与来源声明。
- `Editor/CoastP01Generator.cs`：确定性生成器。
- `Tools/validate_p01.py`：不启动 Unity 的静态校验器。

## 生成

在 Unity 6000.3.25f1 菜单运行 `AeroSim/Coast P01/Generate Asset Library`，或：

```sh
Unity -batchmode -quit -projectPath "$PWD" -executeMethod AeroSim.Editor.CoastP01Generator.Generate
```

生成器仅写入本目录下的 `Materials`、`Meshes`、`Prefabs`；不会打开或保存任何 `.unity` 场景。

## 集成约定

1. 从 `package-manifest.json` 读取 `prefabPath`，不要依赖场景内对象名。
2. 单位为米，Y-up，+Z-forward，pivot 位于资产底部中心。
3. 视觉资产默认不带碰撞；使用 `P01-COL-01` 碰撞代理或由集成器按 manifest 组合。
4. 材质参数是初始建议值；天气系统只应驱动 `_Wetness`。
5. `distant-silhouette` 面向 250–2000 m 使用，不应在近景替代主悬崖。

## 校验

```sh
python3 Assets/AeroSim/Art/CoastProduction/P01/Tools/validate_p01.py
```

校验涵盖：标签、源图 SHA-256、10 个唯一子资产、路径/Unity meta、Shader 属性、材质绑定、生成物、碰撞代理语义，以及包内不得出现共享场景文件。
