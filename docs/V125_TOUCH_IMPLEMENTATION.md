# 1.25 共享触点状态：开发者实现交接

任务 `INPUT-TOUCH-01`，2026-10-10，冻结契约见 [V125_TOUCH_CONTRACT.md](V125_TOUCH_CONTRACT.md)。本批只实现 Gate B 的纯 C# 触点子集；状态为开发者实现/自测通过，独立 QA 尚待根负责人另行接力，不能代签。

## 实现与文件所有权

- 新 `AeroSim/Assets/AeroSim/Runtime/Input/TouchInputState.cs`、同名 `.meta`：无 Unity 类型或硬件/UI 调用；复用已有 `FlightControlFrame` 的有限值与轴范围基础函数，不重复 deadzone/expo。
- 新 `verification/v125-qa/TouchInputStateDeveloperTests.cs` 与 `build-touch-state-developer-tests.sh`：开发者算法自测，独立 QA 不复用其断言作为独立结论。
- 本交接文档；未修改旧 FlightInput、InputRouter、页面、产品文档、PROGRESS 或其他岗位文件。Git 整合与上传由根负责人统一执行。

左右杆只生成固定 SourceId 的单个 `Kind=Touch` InputSample，轴序保持 `left.y,left.x,right.y,right.x`。非 owner/跨杆/第三指/重复 Down/不合法 side 或 ID 不能改状态；无效 Begin 不获取 owner。owner Move 中任一非有限坐标使本杆两轴归零但保留 owner，另一杆不受影响。End、Cancel、CaptureOut 只释放匹配 owner。Clear 保持暂停状态；Suspend 清后暂停，Resume 再清后解除暂停，恢复后必须新 Down。

## 开发者验证结果

运行命令（仓库根目录）：

```sh
bash -n verification/v125-qa/build-touch-state-developer-tests.sh
bash verification/v125-qa/build-touch-state-developer-tests.sh
bash verification/v125-qa/build-input-router-tests.sh
```

使用 Unity 6000.3.25f1 随附 Roslyn/Mono 作为纯 C# 编译/执行环境，`-warnaserror+`、无 Unity editor/player 启动或原生构建。

| 验证 | 结果 | 范围 |
| --- | --- | --- |
| 开发者定向断言 | 375 checks，0 failures | 双杆事件序列、第三指、跨杆、重复 Down、owner/非 owner 终止、无效 enum、负/零/最大 ID、NaN/±Infinity、有限极值 clamp、无圆形归一化、Clear/Suspend/Resume、单 source 与 filter 仅一次 |
| 开发者确定性不变式序列 | 2,400 事件 × 7 checks = 16,800 checks，0 failures | seed `1251010`；拒绝事件不变、轴有限有界、owner 唯一、无 owner 零轴、暂停清空、sample identity/availability/order；不是独立参考状态机 QA |
| 开发者合计 | 17,175 checks，0 failures | 纯状态算法 |
| 原 InputRouter 回归 | 3,050 checks，0 failures | 原文件未改，独立旧算法套件原样复跑 |
| runner shell 语法 | 通过 | `bash -n` |
| 新 meta | 通过 | `ef91848508ec4cb89b1a15d9bfe62591`，32 位小写十六进制，整个 Assets 精确匹配一次 |

测试可执行文件仅留在 `/private/tmp/aerosim-touch-developer.JZ3Cat` 与 `/private/tmp/aerosim-input-core.yRDlYg`，不纳入仓库。全工作树 `git diff --check` 的已有 trailing whitespace 仅来自开始时已修改的三个 Art/DesignV1 meta，未触碰这些用户改动；本批新文件单独检查。

## 冻结 SHA-256

交出下面源与测试后停止写入；独立 QA 如遇缺陷先交根负责人，不在其验证过程中修改被冻结文件。

| 文件 | SHA-256 |
| --- | --- |
| TouchInputState.cs | `2319cdc4e7dd9aa4510565caba6b1d0be5a712fadd8e032328f0ec236b470fb6` |
| TouchInputState.cs.meta | `fe88669a9ae8a31d977953a2d1f823ca279f86ddd7be4b9be059d88cbb37a0d9` |
| TouchInputStateDeveloperTests.cs | `89c073df679934bff79597e67413b3e77fac1ff570f54dc77cc903b0818bcdca` |
| build-touch-state-developer-tests.sh | `54734682501d4f531e727cc81b42b28fdf78eb536674b035d8cb53007805c068` |

## 限制与后继门禁

- Clear/释放后迟到事件的拒绝，针对已无 owner 或不同 ID/不同杆的情况。仅带 pointerId 的纯核心不能辨别已复用同 ID 的旧事件；后续 UI adapter 必须带会话/绑定代际，在进入核心前拒绝失效绑定事件。
- UI adapter 要先清 owner 再 ReleasePointer 防重入；Detach、禁用、旋转/重布局、切页与暂停主动 Clear/Suspend，不依赖 PointerUp。此处未接入或验证真实 pointer capture、UI 生命周期、三端布局/safeArea、物理摇杆、双手触控或 iPad/iPhone 真机。
- 未完成 Gate A 采样/迁移、完整 Gate B 中断协调器、Gate C 正式接线或 Gate D 三端设备验证；不据这批计数称 1.25 完成。
- 没有运行 Git 写操作、提交或 push；GitHub 认证阻塞的状态由根负责人在尝试上传后如实记录。本交接不声称已上传。保留 0.4/1.0、23 原图与未通过证据；未获用户逐机型确认的新外观不接正式飞行。
