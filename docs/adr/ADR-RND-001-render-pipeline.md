# ADR-RND-001：v4 渲染管线主干策略

- **状态**：INTERIM_ACCEPTED
- **工作包 / 里程碑**：RD-01 / M0
- **机器可读源**：`AeroSim/Assets/AeroSim/Resources/RD01/rd01-baseline-manifest.json`
- **迁移批准**：NOT_APPROVED

## 背景

v4.0 PRD 要求 Built-in、URP、HDRP 仅做隔离 spike；批准前不得全量迁移。当前 `GraphicsSettings` 未绑定 SRP，Packages 未安装 URP/HDRP，因此当前主干事实是 Built-in。当前没有可复跑的 v4 三管线 spike 证据。

## 决策

主干暂时保留 Built-in。Built-in、URP、HDRP 的对比只能在隔离工程或分支中执行。只有三套 spike 都有同口径场景、质量、硬件、性能与截图证据，并由后续 ADR 明确批准，才允许迁移主干。

## 后果

1. RD-07 可以消费当前 Built-in 合同，但管线相关接口必须隔离。
2. `REQ-RND-001` 保持 `NOT_RUN`；本 ADR 不把未运行的 spike 写成 PASS。
3. 自动化只校验声明、当前管线和状态一致性，不能替代视觉评审、性能实测或人工签字。
4. 若后续迁移，必须用新 ADR 取代本决策，并保留可回退路径。

## 尚缺证据

- Built-in 上限样片：NOT_RUN
- URP 隔离样片：NOT_RUN
- HDRP 隔离样片：NOT_RUN
- 目标硬件 1440p High 性能：NOT_RUN
- QA / 技术美术 / CTO 签字：NOT_RUN
