# Independent Replay USS Typography v1

新增快速合同，保留原Space/Pause和Dynamic Typography v1/v2/v3全部文件及失败证据。目标是检测旧linebox合同不能捕获的错误字体基准，不从被测binary的Text100重采一个可能已错的基线。

## 冻结来源与范围

- UI/HUD100，根先用1920×1080实际macOS逻辑视口；Text100/125/150/200。桌面视口不能替代iOS真机。
- 独立期望来源为未改的USS：Product.uss:303 `.design-v1 .side-nav .nav-label` **17px**；FlightDesignV1.uss:11 `.v1-flight-action .design-button-label` **16px**；FlightDesignV1.uss:129 `.v1-replay-disclaimer` **12px**。源码SHA与字面规则在新freeze冻结；误采默认14不能被“binary自身baseline”正当化。
- 所有actualFont需在冻结token×倍率绝对0.25px容差内。Aerial Nav精确6个语义标签，Replay精确12个FlightAction子Label（1个fixture event），恰好1个原文disclaimer；不能没有target就通过。
- 在真实产品Home、Records、Replay树测量，不造替代页面。初始Home用产品ShowPage；随后真实合成Pointer点Flight Records→Records里的Replay，回Home→Records→Replay重开，同倍率重复RefreshUiPreferences3次。Pointer必须contained及panel Pick真实命中；最多有界第二次ScrollTo，不反射/Invoke产品callback。
- 新建绝对隔离data目录，先验证LocalStore目录不是正式档案。只保存明确QA synthetic session（非pilot achievement、无courseId）；不得用用户存档。每档有意改偏好后建立profile期望，导航/replay/reapply不能改变Profile内存/磁盘、原保存session字节或Replay源record JSON。
- 首次、Home返回及重开逐目标核正geometry和实际文字MeasureTextSize需要的linebox。不是原概念图视觉证明；不将offscreen当缺失，也不把未主动滚到全页所有元素当全可见验收。四档截图供独立看图。

## 判定和执行

任一字体、计数、linebox、pointer、数据不变断言失败或runtime/probe异常，整体FAIL。全部执行零失败才PASS_AUTOMATED_ONLY。先同探针跑06和07b并保留真实结果，再新07c；不按实现改合同、改USS或容差制造通过。运行时长目标约10–20秒，受实际native帧/场景创建影响，不是定时保证。

根执行复制注入脚本（不启动Unity/app）：

```sh
bash verification/v125-qa/build-replay-typography-probe.sh '/absolute/candidate/AeroSim.app'
```

输出临时AeroSim-UIQA.app绝对路径。根用该副本实际可执行文件运行，传独立目录：

```sh
'/private/tmp/aerosim-replay-typography.XXXXXX/AeroSim-UIQA.app/Contents/MacOS/AeroSim' \
  --data-dir '/private/tmp/new-isolated-qa-data' \
  --replay-typography-qa '/absolute/new-results' \
  -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 \
  -logFile '/absolute/new-results/player.log'
```

独立QA不运行原生或Unity，根统一调度；不批准机型、最终人工验收或Git操作。
