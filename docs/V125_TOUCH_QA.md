# 1.25 纯触点子集：独立 QA 冻结结果

2026-10-10，任务 `INPUT-TOUCH-01`。冻结实现通过本次独立纯 C# 验证：**514,425 checks，0 failures**。这只是 Gate B 双杆所有权核心与已有 InputRouter 算法兼容性，不是完整 1.25、真实 pointer capture、Unity UI 或三端设备验收。最终用户验收仍为 `pending_user`。

独立 QA 先完整读取 [冻结契约](V125_TOUCH_CONTRACT.md)、[契约指纹](../verification/v125-qa/touch-contract-freeze.json)、[开发者交接](V125_TOUCH_IMPLEMENTATION.md) 及共享 `InputRouter/FlightControlFrame`；实际 SHA 与冻结记录相符。未用开发者 17,175 条断言代签独立结论。新建整数下标/数组参考状态机，按契约独立描述事件转换，每事件对照返回值、两 owner、四轴、暂停状态和 raw Snapshot。

## 验证结果

| 检查 | 实际结果 | 说明 |
| --- | --- | --- |
| 独立参考模型逐事件比对 | 25,746 events；514,425 checks；0 failures | 含初始化、定向事件、压力事件及 router 断言；不是 514,425 个独立场景 |
| 定向契约序列 | 178 events | 第三指、重复 Down、跨杆、未知/旧 ID、双向单杆终止、极端值和生命周期 |
| 主动高占用压力序列 | 128 轮；13,568 events | 其中 12,544 次显式要求两 owner 同时持有，并同时检查参考模型和实际状态；包含单杆取消、保留另一杆、重获 owner 与清空阶段，不把全部压力事件说成双 owner |
| 确定性随机参考序列 | 12,000 events；seed `1251010` | owner 偏置事件与非法 enum/ID/数值混合；不是只随机 idle |
| 全部事件双 owner 占用 | 参考与实际均 13,443 events | 每次状态逐项一致；压力之外也计入定向/随机双 owner |
| Touch → InputRouter | 13 frame cases | 原始轴映射、filter 仅一次、单源、不可用中立、Fixed 不回退、重复 ID、暂停/菜单 |
| 原输入算法回归 | 3,050 checks；0 failures | 原 `InputRouterTests.cs` 与 runner 未改，原样复跑 |
| 美术冻结后原目录门禁 | 28 tests PASS；0 FAIL；0 SKIP | 真实目录隔离副本 + 合成确认记录；没有真实机型确认或工作区画廊写入 |
| 编译/runner | 全部 `-warn:4 -warnaserror+` 严格编译通过；`bash -n` 通过 | Unity 随附 Roslyn/Mono 仅作 C# 工具链，无 Unity editor/player/native 启动 |

明确覆盖 owner/nonowner NaN 与 ±Infinity：非 owner 不变；owner Move 任一轴非有限时该杆两轴归零而 owner 留存。覆盖无效 enum、负 ID、ID 0 与 `int.MaxValue`、`float.MinValue/MaxValue`、按轴 clamp 而非圆形归一化。End/Cancel/CaptureOut 两边分别验证，只释放对应 owner，不伤另一杆。Clear 保持暂停；Suspend/Resume 重复及乱序清空，不复活旧 owner。

raw Snapshot 轴顺序逐事件验证为 `left.y,left.x,right.y,right.x`，保留一个固定 `Kind=Touch` SourceId。router 的非线性预期由独立双精度公式算出，容差 `1e-6`；另验证该数值与第二次过滤明显不同，不只使用 linear filter 遮住重复过滤问题。

## 故障注入：同一套未改测试必须拒绝

使用 `apply_patch` 创建临时机械替换工具，读取已复核 SHA 的源，写入 `/private/tmp/aerosim-touch-mutation.TR0RqU/` 的三个副本；未修改工作区实现。runner 的可选源参数仅用于隔离 mutation。三个副本均严格编译成功、执行完整同一套独立测试，然后以 exit code 1 拒绝；不是把编译失败当作测试发现。

| 明确故障 | 独立失败数 / 514,425 checks | 首个检出位置 |
| --- | --- | --- |
| Begin 两边删除目标杆已占用检查，允许 owner steal | 64,919 | 定向 event 5：第三指 Down 的返回、owner 和轴不应变化 |
| CaptureOut 错调另一杆 End | 4,235 | 定向 event 14：左杆 CaptureOut 携右 owner ID 不应释放右杆 |
| Clear 漏清右 owner/右轴 | 28,953 | 定向 event 38：Clear 后右杆和 sample 必须中立不可用 |

mutation 是人工合成错误，不是冻结实现已发现这三个缺陷；3/3 检出仅证明这些故障能被此套件发现，不能等同于无缺陷证明。mutation 源 SHA 与实际返回计数见 [紧凑 JSON](../verification/v125-qa/touch-state-independent-summary.json)。临时编译产物不提交。

## 冻结与复核指纹

下列 SHA-256 是实际文件字节散列；测试、runner 交出后停止写。完整源/合同/旧套件/目录输入指纹在 JSON 的 `sha256` 中，最终独立 QA 再读取逐项校验。

| 文件 | SHA-256 |
| --- | --- |
| `docs/V125_TOUCH_CONTRACT.md` | `119e8e576033ecbd1bc1c6de7ceb4dde50a3518745479fa524b07fbe9e72f952` |
| `verification/v125-qa/touch-contract-freeze.json` | `09fb2d6aefb5f9d1cd63139ee29ea762b6015950d46781ec4c3c5e8e772a5b0a` |
| `TouchInputState.cs` | `2319cdc4e7dd9aa4510565caba6b1d0be5a712fadd8e032328f0ec236b470fb6` |
| `FlightControlFrame.cs` | `aedd8c7593bf61f61cfbd7c2fcc082570828f0f8d486ec39b2d8a0c232fd773d` |
| `InputRouter.cs` | `991a36ee16244bb3d1e1e60578331c0481cc994464bcc6be105076d4cb5006ef` |
| `TouchInputStateIndependentTests.cs` | `38bc0844c436fd54f711a1c7ca39e986293f9f718b6efe59c8476dc34eab79fe` |
| `build-touch-state-independent-tests.sh` | `5491ae9499d030f5d3b637b123ff1e73c5737123669b34206f4ae286065fca15` |
| 原 `InputRouterTests.cs` | `ced96011c456c7ca0c4376a32ed0b1a9f25454a2064dadf327b89b7396898129` |

复跑命令（仓库根目录）：

```sh
bash -n verification/v125-qa/build-touch-state-independent-tests.sh
bash verification/v125-qa/build-touch-state-independent-tests.sh
bash verification/v125-qa/build-input-router-tests.sh
node --test verification/v125-qa/test-catalog-review.mjs
# Optional: pass an isolated faulty TouchInputState.cs copy; expect exit 1.
bash verification/v125-qa/build-touch-state-independent-tests.sh /private/tmp/aerosim-touch-mutation.TR0RqU/owner-steal.cs
```

## 同批艺术记录：仅字节与门禁复核

两位美术冻结后实际读取 current manifest 与 PNG，核对唯一 current row、具体 ID/revision、真实 SHA、PNG 签名及 1536×1024 尺寸。此独立 QA 没有重新查看官方参考或签署视觉准确性；负责人另行实际看图，不能混称本表是外观验收。

| 当前记录 | 状态 | 真实 PNG SHA-256 |
| --- | --- | --- |
| `dji-mavic-pro` / `draft-02`，manifest 1 row | `pending_user` | `ace4e4d0edad8555ac726a8b2c674c4a7513201cc8c01bcac8927fd12c462931` |
| `dji-inspire-1` / `draft-03`，manifest 1 row | `pending_user` | `94a2245bd26a4bfdd930ea0b6d51f596e9bf4d1459f88861329a4c4e695e4491` |

Mavic/Inspire 原 `draft-01` PNG 与提示词逐字节对照基线 Git HEAD `94c0df61221936a1dc8a81ca3558bd29d009b5b4`，全部相同：原 PNG SHA 分别 `70570d7d9b15aabf4ef7f1c7d0b65c0da8fd3cb1eacbc003bc1351a686cddeb4` 和 `9ca8f89268abcb28b73a2e3b910a5838bdc034b0188ab01b6bf0a6a08380f3a2`。Inspire 失败中间稿 `draft-02` 与提示词存在，PNG SHA `d7ee1ee8d9ae193f520618a688042a6f12db0449264c587a167b56d819a3fc60`，未被 current-03 覆盖。完整历史散列保存在 JSON。

28 项目录门禁仍验证 132 主条目 + 9 variants、4 个 current art ID、当前全部 `pending_user` 和具体 hash/revision 确认绑定。不据此宣称 DJI 全目录无遗漏、正式飞行选机、实际浏览器可用、真实网格/材质或逐机型确认完成；没有点击真实确认、没有让 QA/美术代用户确认。

## 未验证边界和后续责任

- **同 ID 复用后的迟到旧事件**：纯 API 只有 pointerId，没有会话/代际。旧 ID 已无 owner 或 ID 不同时可拒绝；同 ID 新 Down 后，迟到旧 Move 无法辨别。本套件明确展示该事件会被核心接受，不把它当作被安全拒绝。UI adapter 必须在进入核心前检查 pointer capture 绑定代际/会话/生命周期。
- adapter 仍须先清 owner 再 ReleasePointer 防重入，Detach/禁用/旋转/切页/OS 中断主动 Clear/Suspend；本次没有实现或实测这些接线。
- 没有实测真实 pointer capture、双手触控、安全区域、横竖屏、操作系统输入中断、外接摇杆/手柄、桌面键盘、iPad/iPhone 真机或性能。纯 C# 状态序列不替代三端设备结果。
- Gate A 采样迁移、完整 Gate B 协调器、Gate C 正式应用接线、Gate D 三端验收仍不能由本次结果关闭。1.0/07c 未通过证据保持原边界；不以版本号升级掩盖缺口。
- 本岗位只写本测试、runner、紧凑 JSON 与本文档；未改契约/实现/旧测试/艺术资产/画廊，未做 Unity/native 构建或 Git 写操作。根负责人统一整合、提交及尝试 push，并如实记录认证/网络失败；本报告不声称已上传 GitHub。
