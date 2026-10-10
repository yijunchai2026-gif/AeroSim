# AeroSim 1.25 共享输入核心第一批

日期：2026-10-09，America/Los_Angeles。状态：**算法实现并通过作者纯 C# 测试，尚未接线到应用**。未启动 Unity、原生应用或真实控制器；没有电脑/iPad/iPhone 集成、设备或视觉验收结论。

## 文件边界

本批仅新增：

- `AeroSim/Assets/AeroSim/Runtime/Input/FlightControlFrame.cs` 与 `.meta`。
- `AeroSim/Assets/AeroSim/Runtime/Input/InputRouter.cs` 与 `.meta`。
- `verification/v125-qa/InputRouterTests.cs`、`build-input-router-tests.sh`。
- 本文。

旧 `FlightInput.cs`、`TrainingData.cs`、Application、Simulator、ProjectSetup 都未由本成员修改；其他成员的已有改动保留。根负责人负责选择批次、独立 QA、冻结构建、commit 与 push。新增源码在 Unity 工程目录中，但未调用的类不会自动改变旧输入行为。

## 四轴与来源契约

`FlightControlFrame` 为无 Unity 依赖的 readonly struct，顺序保持已有遥测的 **Throttle / Yaw / Pitch / Roll**。输出轴有限且位于 `[-1,1]`；`SourceId` 和 `SourceKind` 指明唯一来源；`Neutral` 为没有来源的四轴零值。`HasSource` 和 `IsNeutral` 分别表示存在来源、轴中立，不互相混用。

`InputSample(sourceId, kind, throttle, yaw, pitch, roll, deviceIdentity, available)` 是各平台在当前帧交给核心的原始样本。原始值不先 clamp，以便 HID 的 `[-10,20]` 等量程能正确校准。`Available=false` 表示断线/取消的来源；没有提交的来源不会沿用旧帧输入。样本没有设备轮询、触点绑定或会话操作。

`InputFilterSettings(deadzone, expo, sensitivity, reverseThrottle, reverseYaw, reversePitch, reverseRoll)` 每个来源独立配置。`Default` 为 `.08 / .3 / 1`；`Linear` 为 `0 / 0 / 1`。默认 struct 值是零 sensitivity（有意静音）；使用 `Default` 属性获取产品默认参数。

`AxisCalibration(minimum, center, maximum)` 为单轴原始校准，`JoystickCalibration` 将四轴绑定到非空 `DeviceIdentity`。所有类型只依赖 System/System.Collections.Generic，可以在 Unity 之外编译。

## 单源选择规则

`InputRouter.ReadFrame(IReadOnlyList<InputSample>)` 每次只返回一个来源的四轴，永不相加。样本先做本源过滤/校准，再判断活动：存在任意非零过滤后轴为活动输入。

| 模式/情况 | 行为 |
| --- | --- |
| `SelectAutomatic()` | 活动来源优先于中立来源；多个活动来源按 Touch → Gamepad → Joystick → Keyboard 顺序选一个 |
| 同类来源同时活动 | 按 `SourceId` 的 ordinal 字符序选一个，不依赖枚举/插入/连接顺序 |
| 手柄已连接但中立、键盘有输入 | 选择键盘，不让中立的手柄吞掉键盘控制 |
| 全部来源中立 | 选择可用来源中的最高优先级，输出四轴零；与完全没有设备的 source-free neutral 区分 |
| `SelectFixed(sourceId)` | 仅指定 ID 有资格输出；缺失/断线时返回 source-free neutral，不自动接管到其他设备 |
| 重复 `SourceId` | 同帧该 ID 的所有样本 fail closed；平台适配器必须保证 ID 唯一并移除断线旧实例 |
| 未知 kind、空 ID、不可用 sample | 忽略样本，不作为控制来源 |

自动模式当前逐帧仲裁，**没有保持操纵权、时间锁存或显式确认接管 UI**；用户固定选择可使用 Fixed。不同来源同时操纵时，不将一个来源的油门与另一个来源的偏航拼接。后续若产品需要操纵者锁存，应追加明确契约与测试。

## 校准隔离与畸形值

`SetFilter(sourceId, settings)` 配置该源过滤器。`SetJoystickCalibration(sourceId, calibration)` 保存该源的校准；传 null 移除。校准生效必须同时满足：sample kind 为 Joystick、同 sourceId 有校准、sample 的 deviceIdentity 非空并与保存值 ordinal 精确匹配。触控、键盘和 Gamepad 即使同 ID 误配置了 JoystickCalibration，也不会应用摇杆中心/量程。

正确校准先对中心两侧分别归一化，再 clamp 原始轴；用 double 中间计算避免 float 大量程相减溢出。校准的任一轴若 min/center/max 非有限或不满足 `min < center < max`，该轴归零，其余有效轴仍独立工作。设备 ID 不匹配时不套用其他设备校准，使用该来源的常规过滤。

- NaN/Infinity 原始轴立即归零，有限超量程值最终 clamp。
- 非法 deadzone（负数、≥1、非有限）回退 `.08`；合法高值最多 `.95`，避免接近1的除法不稳定。
- expo 非有限回退 `.3`，有限值 clamp 到 `[0,1]`。
- sensitivity 非有限或负数回退 `1`，合法值最大 `2`；`0` 可用于静音。
- reverse 在 deadzone 后、expo 前对指定轴应用；所有步骤结束输出再次检查有限/范围。

这修复的是独立算法中的串源/叠加风险。旧 `FlightInput.Read()` 尚未委托本路由，所以旧应用的串源校准缺陷仍须根负责人接线后验证。

## 中断与上下文

`SetContext(Flight/Menu/Replay)` 会清空 LastFrame；Menu 和 Replay 中 ReadFrame 均返回中立。`Suspend()` 立即清空并阻止后续输入；`Resume()` 是明确解除暂停的 API；`Clear()` 只清空最近输出，不删除配置或固定来源设置。

核心不拥有指针：UI 适配器必须在 PointerUp/Cancel/CaptureOut/禁用/切页/后台时复位对应 touch sample，并在后台或失焦时调用 Suspend。**Clear 本身不能取消实际设备，Resume 后提交仍被按住的样本会再次生效**；真实恢复动作与要求松手的门禁由平台生命周期/触控适配器实现并测试。本批不提供飞行一键命令或 pause/RTH/photo 轮询，旧键盘命令在后继整合时也必须服从上下文。

## 本轮验证

运行：`bash verification/v125-qa/build-input-router-tests.sh`。

脚本用 Unity 6000.3.25f1 附带 Roslyn、.NET 编译启动器和 Mono，引用 4.8 API，以 `-warnaserror+` 编译两个核心源文件和测试文件后执行测试程序。输出在独立 `/private/tmp/aerosim-input-core.*` 中，不写运行应用、用户存档或冻结探针，也不启动 Unity Editor/Player。

结果：**3050 checks / 0 failures，exit 0**；shell `bash -n` 通过。作者算法自测后，独立QA已按相同冻结源码另行复跑3050/0，指纹与范围见 [候选06/输入/审核工具独立QA](V125_CANDIDATE06_QA.md)。这仍不是设备或视觉验收。

针对现有真实缺口的测试包括：两源同时输入不叠加、连接但中立控制器不吞键盘、Fixed 掉线无隐式接管、四轴顺序/反向/deadzone/expo、校准不影响键盘/触控/Gamepad、不同设备不套外来校准、非对称中心量程、畸形范围逐轴清零、巨大/很小有限范围、NaN/Infinity、暂停/菜单/回放/失去样本清零。另有固定 seed 125 的3000组畸形参数组合检查有限值和范围。

| 冻结文件 | SHA-256 |
| --- | --- |
| FlightControlFrame.cs | `aedd8c7593bf61f61cfbd7c2fcc082570828f0f8d486ec39b2d8a0c232fd773d` |
| InputRouter.cs | `991a36ee16244bb3d1e1e60578331c0481cc994464bcc6be105076d4cb5006ef` |
| InputRouterTests.cs | `ced96011c456c7ca0c4376a32ed0b1a9f25454a2064dadf327b89b7396898129` |
| build-input-router-tests.sh | `31d898b0c64d64f096d69063cebfd451009dd4992645b925a76c500669e01c22` |

## 根负责人下一批整合

1. 先由独立 QA 阅读选择策略与误校准用例，复跑当前冻结四文件；任何修改后更新 SHA 和结果。
2. 新适配器分别采样 Keyboard/Gamepad/Joystick/Touch，并用稳定且唯一的 sourceId/deviceIdentity；将旧校准的 device 字段与实际适配器 identity 做显式迁移。Joystick 轴配置无效不能继续静默 clamp 成另一个轴。
3. 让旧 FlightInput 委托 InputRouter 的 frame，再转换为 Unity Vector4；不要先在 Raw 中合并来源再送 router。Raw 校准页应单独读取待校准源，保持未校准值，与正式过滤后 Read 分离。
4. 根负责人处理真实双指/第三指抢占、取消事件、后台/失焦与已暂停会话恢复，device change 驱动 sample availability/context；既有 Space/Arrow guard 及命令上下文进行应用级回归。
5. 冻结后重新构建、运行应用级输入/暂停探针和页面比例/视觉检查；再独立完成物理控制器、iPhone/iPad 触控与后台测试。当前候选06或旧候选的结果不能证明本算法已集成。

提交和推送由根负责人处理，本成员没有 commit/push。仓库：[AeroSim](https://github.com/yijunchai2026-gif/AeroSim)，工作分支：[codex/v125-product-lines](https://github.com/yijunchai2026-gif/AeroSim/tree/codex/v125-product-lines)。具体提交链接须由根完成上传并核对远端 SHA 后补充。
