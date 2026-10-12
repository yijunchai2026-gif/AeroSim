# P07 水体特效资产包

隔离交付目录，负责人 **Coast Art Lead-07**。包含远海泡沫、岸浪长条、碎浪、回流、岩石包边、潮池边、雨滴涟漪、浪花粒子、湿边 mask、foam decay 共 10 份子资产 manifest，以及独立 `AeroSim/P07/FoamAtlas` Shader/材质合同。

## 来源

- `source/foam-atlas-reference.png` 复制自 `artifacts/generated_image_035643.png`。
- SHA-256：`1afda591f7a75dc2cdcd80934d7626429ca22da5d1310eeb3786b1b20908433f`；2880×2880 RGB8。
- 原图为黑底 RGB 图，Shader 以 `max(R,G,B)` 派生覆盖度，不宣称源图带透明通道。

## 变体合同

- 天气：`Clear` / `Overcast` / `Rain`。
- 质量：`Low` / `High` / `Ultra`。
- 每个材质必须各启用一个天气关键字和质量关键字；参数及采样预算见 `foam-atlas-material-contract.json`。
- `_AtlasRect` 使用左下角 UV 原点的 `[offsetX, offsetY, scaleX, scaleY]`，manifest 同时记录原图左上角像素裁切框，避免坐标歧义。

## 集成边界

本包未改 `Ocean`、未改共享环境组装、未做运行时接线；Shader 保持在资产包隔离目录。静态结构、JSON、哈希、枚举与 Shader 合同已校验。Unity 导入、Shader 编译、画面和目标设备性能仍为 `NOT_RUN`。
