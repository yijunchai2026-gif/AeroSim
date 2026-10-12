# P09 海岸 LookDev 参数包

负责人：**Coast Art Lead-09**。本目录是隔离的数据与评审合同，不接线、不修改共享场景、不迁移渲染管线。

## 交付

- `profiles/`：晴天、阴天、雨天曝光，海面、岩石、植被、工业设施色域，雾色、云影色、夜间预留，共 10 份 profile manifest。
- `builtin-lighting-colorgrading-contract.json`：Built-in 下灯光、雾、反射与项目自有 ColorGrading 的参数边界；不依赖 URP/HDRP 或 Post Processing 包。
- `GOLDEN_SHOT_COMPARISON.md`：A–H 黄金镜头的同机位比较、读图重点和证据边界。
- `source/coast-lookdev-reference.png`：复制自 `artifacts/generated_image_025614.png`，只作为构图、明度层级、材质分色与大气深度参考。

## 视觉基准

参考图的核心目标是冷暖受控的北太平洋海岸：天空高光保留层次，海面蓝灰而非电蓝，玄武/板岩保持中性灰与湿边差异，针叶林深绿但不压成黑块，工业设施以低饱和镀锌钢、混凝土和小面积安全黄从自然环境中读出。远山、海雾和云影用于建立尺度，不用重滤镜掩盖几何或材质缺陷。

## 状态边界

静态校验只证明 JSON 完整、枚举/数值范围、源图散列和隔离声明一致。Unity 导入、运行时接线、A–H 原生截图、目标硬件性能与人工美术验收均保持 `NOT_RUN`。夜间 profile 是禁用预留，不能作为当前天气/任务选项。

运行静态校验：

```bash
python3 assets/p09-lookdev-pack-v1/validate.py
```
