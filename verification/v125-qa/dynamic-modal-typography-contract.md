# Independent Dynamic Modal Typography v1

独立新增合同；旧 Pause/Telemetry、Space v1/v2/v3、缩放矩阵及原报告完全保留。冻结后候选06和07使用同一探针、脚本和合同，不针对实现放宽断言。

## 输入、数据与基线

- 只在临时复制的 `AeroSim-UIQA.app` 注入独立 DLL，构建脚本不启动 Unity 或应用，不修改原候选。运行要求显式绝对路径 `--data-dir` 和 `--dynamic-modal-typography-qa`；探针在修改偏好前核对 LocalStore 实际目录与隔离路径一致且不是正式档案目录。
- `Pause → Exit Without Saving / Session Objective / Flight Telemetry` 必须用真实合成 InputSystem Mouse pointer 进入；按钮需要滚动到视口内，panel Pick 命中该按钮或其子元素。不得 Invoke click callback、反射调用 ShowConfirm/ShowObjective/Telemetry，或直接造替代弹层。
- 每类弹层独立在 Text100、UI/HUD100 下建立实际 `resolvedStyle.fontSize` 基线，不读取产品 `_baseFonts` 或使用写死字体数值。相对树路径、类型和语义角色核对结构。包含所有非空 TextElement：标题、描述、值、Button 本身以及实际存在的 child Label；不能跳过隐藏/裁切元素以制造通过。
- 目标倍率 Text100/125/150/200，即 1/1.25/1.5/2。每个元素必须满足 `actualFont == independentlyObservedText100Font × multiplier`，绝对容差0.25逻辑像素。新增 button child 不得因父文字已经放大而二次继承翻倍。

## 固定测试范围

1. 三类弹层 × 四倍率，各覆盖首次创建、3次重复 RefreshUiPreferences、实际 window resize 及恢复、关闭后重开、Escape取消、2次快速 pointer 开关后再次打开。相同独立基线反复使用，不能在放大后重采基线。
2. 每类弹层在仍然打开时 Text100→200→100，并在 Text200 中重复3次 apply；已有元素缩放可恢复且不累乘。
3. 每类弹层中排队一次偏好更新，随后同帧 disable AeroSimApplication，再 enable。实际 OnEnable 可重建 Home，不要求旧弹层树继续存在；随后新隔离 flight 用 pointer 再开弹层，测200→100→200→100，保证调度状态没有永久卡住。
4. 所有初始、resize-other、reopen、live倍率和生命周期关键阶段，逐 TextElement ScrollTo 并量实际 linebox：正的可见 bounds、所测 text width/height 不超 contentRect（容差3）、滚到可读状态后 bounds 不超 ScrollView viewport（容差3）。其他重复 apply/恢复/快速开关阶段仍完整量字体和树身份。
5. 每次取消，保持原暂停态、只移除顶层弹层；原 record 同引用且 JSON 不变，Profile 内存和磁盘字节不变。resize/reapply 同样不改变这些数据。有意更改 fixture 偏好后才更新期望 profile 快照，不能把取消时的变化吞掉。不得点 Confirm Exit、保存成绩或批准机型。
6. resize必须核实 Screen 实际尺寸，不把请求成功当实际变更。另一尺寸为1280×720或1024×768（初始宽<1200时选前者），最终恢复启动尺寸。

## 证据与判定

- JSON记录每条检查、pointer Pick路径、Text100逐元素基线、每次实际字体/期望字体/linebox/viewport、时间、应用版本、实际窗口及异常。关键阶段PNG供独立人眼检查。
- 任一断言失败、runtime Error/Exception/Assert 或 probe 异常，结果 FAIL。只有完整执行且全部检查通过才 `PASS_AUTOMATED_ONLY`；没有数据不是通过。图稿一致性、物理设备和人工验收均不得从该标签推断。
- 先在未含修复的06 binary运行，记录真实失败；再对新冻结07用完全相同文件复测。若发现测试程序自身错误，记录失败与修订原因，建立新合同版本重新跑两候选；不得偷偷改冻结合同。
- 仅 macOS 逻辑视口/合成输入，不是 iPad/iPhone 真机、safe area、物理双触点、手柄或23原图视觉通过证明。

## 根负责人运行

```sh
bash verification/v125-qa/build-dynamic-modal-typography-probe.sh '/absolute/candidate/AeroSim.app'
```

脚本最后输出临时复制 app 绝对路径。根负责人新建独立 data 和 results 目录后运行该副本可执行文件：

```sh
'/private/tmp/aerosim-dynamic-typography.XXXXXX/AeroSim-UIQA.app/Contents/MacOS/AeroSim' \
  --data-dir '/private/tmp/explicit-isolated-data' \
  --dynamic-modal-typography-qa '/absolute/verification/results/dynamic-typography-844x390' \
  -screen-width 844 -screen-height 390 -screen-fullscreen 0 \
  -logFile '/absolute/verification/results/dynamic-typography-844x390/player.log'
```

若 macOS 实际可执行文件名不同，以副本 `Contents/MacOS/` 的实际名称为准。目录不得复用正式档案；根可另跑桌面初始视口，移动大小始终标逻辑视口。root统一构建/运行；独立QA仅编译核查、冻结与复核原始证据。
