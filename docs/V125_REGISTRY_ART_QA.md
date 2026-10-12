# AeroSim 1.25：registry / 新机型审核画廊独立文档 QA

当前结论：**PASS_AUTOMATED_GALLERY_DOCUMENT_ONLY**。自有报告及 progress 续接加入暂存后，扩大 `--require-tracked` 复核为 1,095 检查 / 0 失败 / exit 0、332 次本地链接、0 未暂存依赖。这里是自动文档、字节和审核门禁验证，不是浏览器交互、23 页一比一视觉、设备或用户验收。

本成员完整读 AGENTS、生成器/检查器及两位美术交接，只新增独立测试和本报告/紧凑 JSON；没有改产品源、图稿、用户确认或 Git。额度中断前已完成的测试与实际看图不重做；2026-10-12 恢复时重新核对同一冻结字节后，继续未完成的文档依赖审计。

## 冻结与真实执行

[整合输入冻结](../verification/v125-qa/registry-art-source-freeze.json) SHA-256：`96013e4c9e857b3081939fedacd67aa143dc2cdb8e02646c340052132c1e3471`，23/23 输入文件匹配；[registry source freeze](../verification/v125-qa/registry-source-freeze.json) 14/14、[只读守卫](../verification/v125-qa/registry-wave-readonly-guard.json) 39/39 一致。守卫包括 16 份旧文件和 23 张原始概念图，其中旧 5 份 current 图稿保持原字节；字节一致不等于视觉验收。

Node.js v24.18.0，原样执行三个 `node --test --test-reporter=tap` 套件，全部退出 0：

| 套件 | 测试 / 失败 / 跳过 |
| --- | --- |
| 现有目录回归 | 34 / 0 / 0 |
| 文档检查器规则 | 13 / 0 / 0 |
| [独立画廊增量](../verification/v125-qa/registry-art/independent-gallery-tests.mjs) | 21 / 0 / 0 |
| 合计 | 68 / 0 / 0 |

实际输出在额度中断后仍可恢复；精确运行时刻没有保留，不补写。恢复后 23+14+39 个冻结字节再核一致，因此不重复三套已完成运行。三份恢复控制台 TAP 副本另存本机临时目录，逐字与实际工具输出一致；位置和真实 SHA 在紧凑结果内，准确标为事后保留副本，不伪称原始带时戳运行日志，也不提交副本。命令、工具、SHA、计数和边界见[紧凑结果](../verification/v125-qa/registry-art/gallery-document-results.json)。C# registry 的算法/mutation 由[另一独立 QA](V125_REGISTRY_QA.md)执行，这里的字节核对不挪用其算法计数。

第一阶段独立测试原稿 SHA `de627261bdec3b5923fcb02083eb2efd3bebffc9e41cd6ae8832223703f7b4b8`，仅 19 项合成/旧守卫通过。根在最终冻结前发现最后尚未运行的 PRD probe 错写英文 `candidate08`；实际文档声明为“已构建应用：1.0.0 开发候选08”。只修测试预期，不为通过测试改产品文档。修后冻结测试 SHA `85bd3081049a1d921b43689f9c1ef40dc7fbabc53e61fc3781206120d0910820`，阶段 19 项再通过，然后最终完整 21 项通过；原准备结果不是最终真实画廊通过。

## 审核状态与负例

实际审核页/manifest 一致为 132 主条目＋9 独立版本＝141 项、7 份 current 稿、0 用户批准、134 项无 current 稿。Phantom 4 与 Air 2S 只允许各自精确机型 ID、目录、当前 PNG 散列、`generated_review_bitmap`、`pending_user`、非空 revision 和现有官方 HTTPS 来源。目录无遗漏仍未证明，任何待确认稿都未接正式飞行。

隔离负例覆盖错型号/地区变体/重复稿、跨 namespace、路径逃逸/外部地址/错扩展名、缺或错 SHA、改字节、非 pending、错误素材类型、空 revision、缺/伪造来源，以及旧机型、旧 revision/image/catalog、自动化/工程通过的本机缓存不能继承。测试的 DOM/点击是 VM 合成节点，不是真实浏览器；真实目录初始化使用空内存 store，未点击真实机型批准按钮。

网页 localStorage/导出记录不是签名或用户身份认证。手工可编辑的本机记录不能自动批准工程资产，根仍须核收用户逐机型、具体版本及当前 PNG 散列的确认。

## 实际看图与未通过项

完整阅读 [Phantom 4 美术交接](V125_ART_PHANTOM4.md)、[Air 2S 美术交接](V125_ART_AIR2S.md)，并实际用 `view_image` 看过两个项目 PNG，均 1536×1024：

- [Phantom 4 draft-01](../assets/v125-art/phantom4/phantom-4-draft-01.png)，SHA `f5d855115819f4fbe1c1b4ec738cb0fbfc087338bd78d127f71f5d30df85d518`。白色固定 X 轮廓、四桨、单摄可辨；侧面云台/连接与正面及三分之四不一致，镜头/电机偏粗，俯视机头方向和尺度未校准。
- [Air 2S draft-01](../assets/v125-art/air2s/air-2s-draft-01.png)，SHA `05f07935cd8a1f60890d082a297ae1e13145f0633afb8e9e8060d56b0f580d2f`。浅灰展开四臂、黑桨橙尖、单摄与前/上感知窗可辨；支架厚度、传感窗投影、底部/后视和尺寸一致性仍有风险。

本 QA 没有看官方照片或实物，不把美术成员的参考查阅冒称为自己的验证。两图仍 `pending_user`，观察轮廓不等于外观准确、工程蓝图、可运行网格、真实 PBR 材质或用户批准。[PRD](../飞行模拟器产品设计文档.md)第 0 章、25.14–25.15 与最新 progress 的真实边界保持：应用仍 1.0.0 开发候选08，没有新 native。

## 文档依赖与历史失败

2026-10-12 独立执行 `prd-document-check.mjs --require-tracked`，首次范围为 PRD、progress、两美术说明及 registry proposal/implementation/QA：**1,052 检查 / 0 失败 / exit 0；318 次本地链接，0 未暂存依赖**。工具输出过大被截断，完整执行退出 0，完整 summary/sourceSHA 块保留并恢复；没有伪称保存该次完整输出散列。

根随后明确暂存本报告/结果及 progress 续接，扩大范围加入本报告，实际 **1,095 检查 / 0 失败 / exit 0；332 次本地链接、0 未暂存依赖**，画廊和 manifest 各 7 稿、`GALLERY_FROZEN_DRAFTS_MATCH` PASS。扩大复核捕获完整检查器 JSON 至内存进行解析和 SHA 核对，只保存紧凑摘要/指纹，不上传完整检查数组；本段交接文字更新后，再对最终文档字节复核，精确来源 SHA、完整 stdout SHA 和时间在结果内。

另一独立 QA 的整合前 **583 / 1 / exit 1** 历史原样保留：当时画廊 5 稿、manifest 6 稿，唯一失败为 `GALLERY_FROZEN_DRAFTS_MATCH`，17 个依赖尚待 stage。现在最终图库与 manifest 都 7 稿，68 条回归和上述 tracked 审计通过，关闭此陈旧 HTML 差异；不改写原失败记录或把它说成当时就通过。

浏览器实际加载/缩放/焦点/导出、触点、Unity/native、物理控制器、iPad/iPhone 真机、最终 3D/PBR、真实 Google Maps/空域及 23 页视觉一致均 **NOT_RUN / NOT_VALIDATED**。Gate A 和 1.25 整体未完成，用户外观确认与最终验收不得由自动化替代。本成员不做 Git 写入或冒称上传；具体 commit/push/新鲜远端 SHA 由根统一交付。
