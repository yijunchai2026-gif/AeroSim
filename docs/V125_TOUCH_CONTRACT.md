# 1.25 共享双杆触点核心：冻结契约

第三波输入任务 `INPUT-TOUCH-01`，2026-10-10。只实现无Unity依赖的触点所有权逻辑；不接旧FlightInput、页面、物理设备、存档或机型。技术负责人/root冻结接口，程序员实现，独立QA另写测试；尚未执行测试。

## API与状态

- `AeroSim.Input.TouchStickSide { Left, Right }`；`TouchInputState(string sourceId = "touch:dual")`拒绝空/空白sourceId。
- 只读`SourceId`、`int? LeftPointerId/RightPointerId`、`float LeftX/LeftY/RightX/RightY`、`bool IsSuspended`。初始无owner、四轴零、未暂停。
- `bool Begin(side, pointerId, x, y)`：有效side、非负pointerId、有限坐标且未暂停；目标杆空闲且该pointer不拥有另一杆才接受。已占用、第三指、重复Down均拒绝且不改变任何值。每轴clamp[-1,1]；适配器负责几何/圆形归一化，本核心不猜测屏幕坐标。
- `bool Move(side, pointerId, x, y)`：只有当前owner可更新。未知/失效ID不改变值。owner的任一非有限坐标将本杆两轴归零但保留owner；返回true。有限坐标按轴clamp，不滤deadzone/expo、不累加输入。
- `bool End/Cancel/CaptureOut(side, pointerId)`：仅对应owner可清零该杆并释放owner，返回true。其他pointer、另一杆owner、不合法side、重复终止均false且不改另杆。
- `void Clear()`清两owner与四轴，但不改暂停状态。`Suspend()`先清后保持暂停；暂停时不能Begin/Move。`Resume()`再次清并解除暂停，不复活旧owner；恢复后须新Down。
- `InputSample Snapshot()`输出单个`Kind=Touch`源，固定sourceId，轴顺序`left.y,left.x,right.y,right.x`；`Available`只在未暂停且至少一个owner存在时true，空闲或暂停为零且不可用。不同杆不是两个竞争来源。

## 必测序列

Left Down11、Right Down12→双Move→第三Down13拒绝→Left Cancel11→Right仍活动→CaptureOut13无影响→Right Up12零；同pointer跨杆、重复Down不重置、旧Move/End迟到、clear后pointer复用、Suspend/Resume乱序与重复、负/极大ID、无效enum、NaN/Infinity/超范围坐标、router单源兼容与filter仅一次。独立测试应加参考状态机/确定性事件序列，不只复跑实现者断言。

## 文件边界与接线限制

程序员只写`Runtime/Input/TouchInputState.cs`及32hex唯一meta、自测与接力文档。独立QA只写新的验证文件。根负责整合和Git；保留原3050输入算法测试、07c字体源码/构建/失败证据、23原图及0.4。不得自行Unity构建或Git写操作。

这是Gate B的纯触点子集，不是Gate A采样/迁移、应用生命周期协调器或Gate B真实UI事件验收。后续适配器必须先清owner再ReleasePointer防重入；Detach/禁用/旋转/暂停/OS中断主动Clear或Suspend，不依赖PointerUp。真实pointer capture、双手触控、safeArea、外接控制器和iPad/iPhone设备均另验。

GitHub上传暂受认证阻塞；依据用户继续本地任务及AGENTS的失败记录规则推进本独立子集，每批仍验证→本地commit→尝试push→如实记录，不能假称上传或新增正式资产。
