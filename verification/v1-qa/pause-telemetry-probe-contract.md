# 独立Pause/Telemetry诊断v1：native NOT_RUN

只跑Flight一页，Text100/200；不继承v3的20页文字批次，不改任何冻结v1/v2/v3/34-state文件。供根定位候选05手机横屏 `interaction.nested-confirm-escape`。不替换原71/5018/61/0报告，也不把已知巨大toast遮挡删成PASS。

实际步骤：

- FreshFlight→真实Pointer Pause→ScrollTo Exit Without Saving→真实Pointer→Escape。逐次记录button世界矩形、ScrollView viewport、panel.Pick(center)完整祖先路径/是否该Button、click前后Modal count、Paused、page、私有 `_flightDialogClose != null`、record id、焦点。Pick不命中或ScrollTo没使button完全进入viewport各自FAIL，但仍发同坐标事件以保留现场，不直接Invoke按钮callback。
- 必须实际打开nested确认（2可见modal＋dialogClose非null），Esc后应回到原暂停层（1 modal＋dialogClose null＋原record未变），不是只检查Paused而漏掉从未打开确认。
- 第二个新flight四项OSD enabled、位置设中心触发受限安全布局，分别Text100/200；记录各指标visible/隐藏reason/tooltip。实际Pause→Flight Telemetry，六标签与此时真实Simulator Position/velocity/Battery/Elapsed独立格式化值精确对应；逐项ScrollTo检行高与viewport，Back to Flight必须恢复原暂停状态/record identity和完整record JSON不变。
- 两个流程都比较LocalStore.Profile内存JSON和隔离profile.json字节内容，验证读信息/取消不损坏保存的profile。不执行Finish/保存成绩、不伪造照片、数据明确QA。
- 10张原始PNG：nested确认、Esc后、运行中OSD受限状态、Telemetry最后值、Back暂停，各Text100/200。六值逐项检查，不宣称单张手机首屏能同时展示六值。

```bash
bash verification/v1-qa/build-pause-telemetry-probe.sh /private/tmp/aerosim-v1-build.BPEAKU/AeroSim.app
# 使用上条最后一行真实QA副本路径。
PAUSE_QA_APP=/private/tmp/aerosim-pause-probe.XXXXXX/AeroSim-UIQA.app
PAUSE_QA_PROFILE=$(mktemp -d /private/tmp/aerosim-pause-profile.XXXXXX)
PAUSE_QA_OUTPUT=/Users/bytedance/Documents/ChatGPT/飞行模拟器/verification/v1-qa/results/candidate-05/pause-telemetry-844x390
"$PAUSE_QA_APP/Contents/MacOS/AeroSim" --pause-telemetry-qa "$PAUSE_QA_OUTPUT" \
  --data-dir "$PAUSE_QA_PROFILE" -screen-width 844 -screen-height 390 \
  -screen-fullscreen 0 -logFile /private/tmp/aerosim-pause-telemetry-player.log
```

输出pause-telemetry-report.json，width/height保留实际，traces与checks分开；FAIL/runtime退出2，仅全断言通过退出0/PASS_AUTOMATED_ONLY。原生未运行；静态C# against05Managed compile exit0，bash syntax PASS。本probe未运行Unity/native、未修改生产/Git。

边界：这是桌面逻辑844×390的合成鼠标/键盘，不是真手机touch/safeArea。记录祖先Pick用于诊断，真实事件仍可能由于焦点/scroll在下一frame变化而不同，不能仅凭Pick=True宣布点击业务成功，必须看modal状态断言。Telemetry读取的暂停kinematic velocity按产品当前含义可为0，不声称显示暂停前历史速度。Label精确值+行框不等于字体/布局与23原概念视觉一致。
