# P02 潮间带资产包

隔离交付目录，负责人 **Coast Art Lead-02**。本包冻结湿岩、干湿过渡、潮池底、浅水边、藻层、盐结晶、水线、积水、回流槽、潮沟 10 个子资产的独立材质/Shader 合同与天气湿润响应；不接入运行时，不修改共享场景组装文件或 `OceanSurface.shader`。

## 生成源与边界

- `source/wet-rock-reference.png`：复制自 `artifacts/generated_image_035657.png`，保留标签 `GENERATED_SOURCE:generated_image_035657`；SHA-256 `ceb7ff9a0b55ef5801e2bc9d4bf78308c51144c752f04cab2d51ff94e0f77b40`。
- `source/tidepool-bed-reference.png`：复制自 `artifacts/generated_image_035650.png`，保留标签 `GENERATED_SOURCE:generated_image_035650`；SHA-256 `d84db9b1323b596e5166ea08ae1d129cb989401c9598b9877918825c7c2b2f80`。
- 两图均为 2880×2880 RGB8；只作为色彩、表面词汇和 LookDev 参考，不声明无缝、去光照、物理标定或现成 PBR 通道。
- `shader-material-contract.json` 指定独立 Shader 命名空间 `AeroSim/Coast/P02IntertidalSurface`；明确禁止依赖或修改 Ocean 主 Shader。
- `weather-wetness-parameters.json` 仅定义 Clear/Overcast/Rain 的湿润输入与响应范围，不连接共享天气状态。

## 状态

仅执行 manifest 静态校验。贴图派生、Shader 实现、Unity 导入、共享场景接线、视觉标定与性能验收均保持 `NOT_RUN`/`NOT_CONNECTED`。运行 `python3 validate.py` 可复验 JSON、来源哈希、10 个子资产及隔离边界。
