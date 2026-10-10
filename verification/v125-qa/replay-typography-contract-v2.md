# Independent Replay USS Typography v2

这是追加合同，不修改Replay v1、Dynamic v1/v2/v3或原Space/Pause。保留v1全部Pointer/计数/固定字体/linebox/数据不变断言，追加实际产品`BeginReplay(record)`的coroutine进入路径。v1三版均通过并不能抹除旧Space中的77条PASS evidence字体差异。

## 冻结的追加范围

- UI/HUD100，1920×1080实际macOS逻辑视口，Text100/125/150/200。冻结USS期望仍为side-nav子Label17px、FlightAction子Label16px、disclaimer12px，绝对字体容差0.25px、linebox容差3px，不从binary Text100重采基准。
- v1原始Records→Replay、返回Home重开和reapply3真实合成Pointer流程完整先执行；Pointer containment/Pick、精确6导航/12动作/1disclaimer、数据不变检查与v1一致。
- 然后各Text倍率追加原Space V2的前置页面顺序：FPV Tuning→Aerial Settings→Aerial Controller→Professional Home。每页调用产品ChangeWorkspace与PreparePreview，明确核真实CurrentPage；随后ChangeWorkspace Aerial并调用产品public BeginReplay。在同一真实产品UI树核固定USS token和逐项linebox，截图；reapply3、Home返回、public BeginReplay重新进入继续核字体、计数与数据不变。
- 追加路径每次等待沿用旧Space精确Settle：0.10秒后两次yield null，共两轮；这是检查调用所在帧阶段差异，不是缩减容差或强制应用字体。记录BeginReplay前后Time.frameCount以方便溯源。追加路径不是Pointer入口，不能把它冒称用户触控/键盘实测；不反射/Invoke私有callback、不改被测树字体/布局。
- 使用v1同一个明确标记QA synthetic的隔离存档（非pilot achievement，无courseId）。每档有意设置textScale与workspace后冻结profile；Replay/reapply/Home/重开不得改变profile内存及磁盘、保存session字节或Replay源record JSON。绝对隔离data目录必须先验证；不得用真实用户档案。
- 首次追加Replay、Home返回及重开各目标须positive且实际文字MeasureTextSize可容纳。四张v1截图与四张追加路径截图用于独立看图；linebox通过不证明所有目标在同一个首屏、全缩放矩阵或23概念图视觉验收。

## 判定与执行

任一v1或追加断言失败、runtime/probe异常均整体FAIL；完整执行零失败才PASS_AUTOMATED_ONLY。先同一冻结探针跑06/07b/07c，保留三版原始JSON与PNG，不为通过变更合同。约15–25秒目标受实际native帧/场景创建影响，不能保证总用时。

根统一复制注入并运行，独立QA不启动Unity或native：

```sh
bash verification/v125-qa/build-replay-typography-probe-v2.sh '/absolute/uninstrumented/candidate/AeroSim.app'
```

输出临时副本路径；用副本可执行文件传以下参数及新隔离data/results/log绝对目录：

```sh
'/private/tmp/aerosim-replay-typography-v2.XXXXXX/AeroSim-UIQA.app/Contents/MacOS/AeroSim' \
  --data-dir '/private/tmp/new-isolated-qa-data' \
  --replay-typography-qa-v2 '/absolute/new-results' \
  -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 \
  -logFile '/absolute/new-results/player.log'
```

报告为`replay-typography-v2-report.json`。原v1报告原样保留。该追加测试不是机型批准、iOS真机、地图接入或用户最终验收。
