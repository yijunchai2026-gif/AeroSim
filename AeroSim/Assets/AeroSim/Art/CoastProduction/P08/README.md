# P08 天气视觉资产包

隔离交付目录，负责人 **Coast Art Lead-08**。包含细雨、暴雨、镜头雨滴、地表流痕、积水涟漪、低雾、海雾、云影、雨幕、风驱喷雾 10 份子资产 manifest，以及 `WeatherVisualContract`、质量档、关闭路径和只读 `EnvironmentState` 适配器。

## 边界

- 源图 `source/weather-atlas-reference.png` 复制自 `artifacts/generated_image_035555.png`，SHA-256 `7db8075a5b0a8558282b15c3276c9c081a298b083633303fc906511a66e176e1`，2880×2880 RGB8。
- 图中有雨线、水滴/流痕、涟漪和雾噪声板块；当前仅作为视觉源/参考。未切片、无 alpha、未去光照、未证明无缝，不能宣称已完成运行时贴图。
- `adapter/environment-state-adapter.json` 是稳定的数据映射；`adapter/WeatherVisualAdapter.cs` 是无 Unity 场景副作用的参考实现。
- 适配器只读消费 `EnvironmentState` 快照，不修改共享 `EnvironmentState`，不创建/组装场景对象，不自动接线。消费者显式选择接入。

## 连续天气

`Clear / Overcast / Rain` 只定义锚点。实际权重由 `cloudAmount / precipitation / fogDensity / wind / wetness / puddleAmount / vfxDensity` 连续计算；禁止仅按 `weatherId` 做三档硬切。

## 质量与关闭

质量档为 Low / Medium / High / Ultra，适配器永不提升调用方请求档。全局 Off、VFX Off、单资产 Off、无效输入、缺失资源与 Dispose 均有确定关闭/降级路径；关闭后权重归零，资源释放责任限定为适配器自有句柄。

## 校验

运行 `python3 assets/p08-weather-visual-pack-v1/validate.py`。本次仅做静态校验；Unity 导入、图集切片、视觉校准、真机性能与泄漏验证均保持 `NOT_RUN`。
