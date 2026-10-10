# 独立Space Probe v2（原生未运行）

保留已提交 `SpaceSubmitProbe.cs` 原字节，SHA256 `fe75cf13cdaf6d59d85e225f470b73ff212be8b73798ce9a546d97f592c1c6bd`。不修改冻结34-state矩阵。v2通过新脚本机械生成temp partial base并编译新 `SpaceSubmitProbeV2.cs`；保留全部v1断言，报告原始v1哈希和新scope。

增加：

- Enter/Escape/Gamepad三种暂停恢复，各测Up/Down/Left/Right共12组**真实Arrow→Space**，不只观察Space→Arrow；焦点不窃取，后继Space不误Pause/RTH/Finish/Photo。
- 真实Tab（无测试Focus代替）选择Pause，再Space必须生效；验证guard不误杀刻意键盘可访问性。
- Text100/125/150/200下的Tuning、Settings、Controller、ProHome、Replay全部实际TextElement；重点含Slider内部TextElement，测content-height与真实字体测量行高，非仅Label/Button。
- 非input单行宽度、wrap后的多行行高也严格检查；input允许水平滚动输入，但不豁免竖向裁切。输入/页面仍真实可交互，长事件明确合成QA数据、不伪造照片/成绩。
- 保留10张真实100/200文字截图供人工核对；不把文本测量自动PASS当视觉一致。

本机以候选03Managed静态编译、生成器4个单元测试、shell语法/差异检查通过；**v2 native NOT_RUN**。所有失败保留，不放宽为“已验收”。文字测量可能仍有待人工识别候选，必须原样报告。

```bash
# 根负责人统一候选04原生构建后，复制并注入独立探针；此脚本不启动Unity或native。
bash verification/v1-qa/build-space-submit-probe-v2.sh /absolute/uninstrumented/candidate04/AeroSim.app
# 使用上一命令最后一行真实路径，不使用旧矩阵或v1已注入的副本。
SPACE_V2_APP=/private/tmp/aerosim-space-v2.XXXXXX/AeroSim-UIQA.app
SPACE_V2_PROFILE=$(mktemp -d /private/tmp/aerosim-space-v2-profile.XXXXXX)
SPACE_V2_OUTPUT=/Users/bytedance/Documents/ChatGPT/飞行模拟器/verification/v1-qa/results/candidate-04/space-submit-v2
"$SPACE_V2_APP/Contents/MacOS/AeroSim" --space-submit-qa-v2 "$SPACE_V2_OUTPUT" \
  --data-dir "$SPACE_V2_PROFILE" -screen-width 1920 -screen-height 1080 \
  -screen-fullscreen 0 -logFile /private/tmp/aerosim-space-submit-v2-player.log
```

输出 `space-submit-v2-report.json`，失败返回2；只有全部断言/运行时无失败返回0，状态PASS_AUTOMATED_ONLY，人工PENDING。报告不参与旧34-state聚合，不覆盖candidate03/v1证据。逻辑窄视口可另跑新隔离批次，仍不等于真机验证。
