# Independent desktop08 supplemental v1

冻结范围只补三项既存桌面缺口和实际Replay重复单位语义。旧Space v1/v2/v3、Pause、dynamic v1/v2/v3、Replay typography v1/v2、全部原失败与23概念图不变；本合同不替代旧合同。

## 精确矩阵与断言

- 根分别在实际macOS逻辑1440×900、1920×1080运行同一probe。UI/HUD100，内置Text100/200；报告记录Screen实际尺寸、panel scale和实际UI文字/geometry，不冒充iOS/iPad。
- Aerial fresh flight空焦点→真实合成InputSystem Tab→Space：首次必须为`Button#action-pause`，一次Submit；真实可见modal仅1，含`Flight paused`和`Resume Flight`，不含`Session objective`，焦点为Pause内Resume。不能以Paused=true替代路径断言。
- 独立Fresh→Escape打开Pause，7个按钮真实Tab/Shift-Tab循环始终在modal内且往返恢复原焦点；paused record JSON/profile内存与磁盘不变。Enter Resume后modal为0、空焦点；Space及四Arrow→Space均不误暂停/RTH/拍照/结束或换record，不直接Focus/Blur产品目标制造通过。
- Objective HUD按钮实际可见时，从fresh的首Tab继续有界16次Tab达到它再Enter；隐藏时用首Pause→Enter→Tab实际达到Session Objective→Enter。核真实顶层Objective标题、返回按钮、暂停和焦点包含；Tab/Shift-Tab不逃逸，Enter只关闭顶层，嵌套时回Pause后Escape恢复。关闭后空焦点Space无动作。缺前提依然FAIL，独立场景重建只防止首路径错误吞掉其余门禁。
- Professional实际Home的固定fixture标题`Bridge Inspection Fundamentals`、subtitle、说明、Continue Assignment均需存在。保留title36px/description16px USS基准×Text倍率、字体0.25px容差；最终contentRect宽MeasureTextSize测行高，保留原3px容差；四元素两两worldBound不可相交（不新增相交容差）。记录实际宽高、required、字体、hero/parent几何并保存原PNG。几何通过不等于图稿一致。
- Replay通过产品public BeginReplay/Seek(10)设置明确QA fixture，不冒充Pointer/设备路径。Text100/200×Imperial/Metric；三个实际`v1-replay-chart-heading`须语义唯一Altitude/Ground Speed/Battery，每行正好一个可见value和unit。value必须精确仅数字：12m高度、4m/s速度、92电量分别按显示制式换算格式；单位单独精确为ft/m、mph/km/h、%。由实际稳定UI树的text核对，包括Battery，不靠源码grep断言native通过。不改变record/profile。
- 任何失败/runtime/异常/未完成整体FAIL；完整零失败才PASS_AUTOMATED_ONLY。没有报告、缺目标、UNRENDERED和未实测不计通过。首批07c基线和08结果同probe，保留失败。若probe自身需修订，新增v2合同/source/freeze，v1和失败不覆写、不削原断言。

## 根的注入及执行接口

编译脚本只创建临时副本、注入、签名，不启动Unity/native；根使用未注入稳定候选：

```sh
bash verification/v125-qa/desktop08/build-desktop08-probe.sh '/Users/bytedance/Documents/ChatGPT/飞行模拟器/AeroSim/Builds/macOS-V1-Candidate-20261010-07c/AeroSim.app'
```

使用最后一行真实app路径，不能照抄XXXXXX占位；每个候选/视口用新data与results目录。建议先07c两视口，再08同矩阵：

```sh
DESKTOP08_QA_APP='/private/tmp/aerosim-desktop08-v1.XXXXXX/AeroSim-UIQA.app'
DESKTOP08_QA_PROFILE=$(mktemp -d /private/tmp/aerosim-desktop08-profile.XXXXXX)
DESKTOP08_QA_OUTPUT='/Users/bytedance/Documents/ChatGPT/飞行模拟器/verification/v1-qa/results/candidate-07c/desktop08-v1-1440x900'
mkdir -p "$DESKTOP08_QA_OUTPUT"
"$DESKTOP08_QA_APP/Contents/MacOS/AeroSim" --desktop08-qa "$DESKTOP08_QA_OUTPUT" \
  --data-dir "$DESKTOP08_QA_PROFILE" -screen-width 1440 -screen-height 900 \
  -screen-fullscreen 0 -logFile "$DESKTOP08_QA_OUTPUT/player.log"
```

另一次改1920/1080和输出目录后缀；新08用其实际未注入app路径和candidate-08目录。输出`desktop08-report.json`及8张原PNG（两个Pause首路径、两个Home、四个Replay）。原始结果留verification，紧凑摘要单独提交。旧Space仍须原样1440/1920/1280复跑；原Pause844、dynamic v3 844和Replay typography v2 1920仍完整保护。

状态在冻结时为native NOT_RUN /人工PENDING。独立QA不启动Unity/native、不改生产/Git。根唯一构建/运行及最终提交推送；未实测物理控制器、iPhone/iPad真机、23图全面一致和最终用户验收均不宣称通过。
