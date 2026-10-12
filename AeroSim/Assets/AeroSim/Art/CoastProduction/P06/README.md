# P06 海岸植被资产包

隔离交付目录，负责人 **Coast Art Lead-06**。本包定义 10 个海岸植被子资产的 atlas 区域、alpha-cutout、风摆、季节/天气湿润、LOD/billboard 与生态分布合同；**不接入运行时，不修改共享场景或环境组装**。

## 来源与边界

- Atlas：`source/coastal-vegetation-atlas.png`，复制自 `artifacts/generated_image_035552.png`。
- SHA-256：`c82d1f17f49ed33dc359a002332bd39cb0d5759bbf2fe926654352ab87ce6b6f`；2880×2880 RGB8。
- 原图为黑底 RGB atlas，不含可直接使用的 alpha。生产纹理必须按合同提取 matte、去黑边、颜色扩边并生成 mip coverage；当前不把黑色当作已完成透明通道。
- 参数为 LookDev/生态散布起点，不是实测植物学、风洞或性能数据。

## 交付状态

10 份子资产 manifest 与统一 `material-contract.json` 可独立静态校验。alpha 派生、网格/card 制作、billboard 烘焙、Unity 导入、运行时视觉和目标机性能均保持 `NOT_RUN`/`NOT_DERIVED`。执行 `python3 validate.py` 只检查文件、哈希、合同完整性和参数边界。
