# 独立Space Probe v3：新增补充诊断，native NOT_RUN

v1/v2/34-state原文件和合同完全不变。生成器验证immutable v1与v2源hash，机械生成临时base/适配partial；全部v1、v2输入断言和Replay字宽断言保留。candidate04 v2原始 **1739/55FAIL/0runtime**、报告SHA `469f4d14bf5f5bb87eadb5ed73d65cf7cd4025bc6b93386a71c1e4ac4b605292` 保留，不被v3重算。

## 唯一有边界的分类变化

只识别 `Foldout.text == Advanced Input Mapping && value == false && foldout.contentContainer.Contains(textElement)`。对应未呈现子元素加入报告 `presentationNotes`，classification **UNRENDERED**，记录文字/字号比例/祖先resolved display、visibility、inlineOverflow及worldBound、内容容器身份。UNRENDERED不是PASS，也不是所有0尺寸的豁免；标题不在contentContainer内，不跳过标题。

candidate04强证据为四Text各6个未展开子文本＝24条v2正尺寸误分类；v3运行时实际note数量由真实树决定，不硬编码24制造通过。未匹配该具体Foldout语义的0尺寸仍FAIL。原v2五页四Text完整测量仍执行；本次不存在通用Clip/Scroll尺寸豁免。

## 新实际步骤

- 保留全部Enter/Escape/Gamepad恢复×四方向→Space、刻意Tab→Space、菜单/暂停导航/Toggle/Objective/确认框业务断言。
- 四Text重新打开Controller，通过实际Toggle.Focus +真实Space Submit展开Advanced Input Mapping，不直接设置foldout.value。必须由closed变expanded；找到Deadzone label、其numeric内部文字、四Reverse labels共6项；逐项ScrollTo后验正尺寸、字体测量高/宽、真实ScrollView viewport容纳，并各拍PNG（预计24张）。这不是物理校准测试，不翻转reverse配置冒充成功。
- 五Slider×四Text逐项ScrollTo实际numeric TextElement；验真实静态值宽高、viewport容纳，保存20张滚动PNG。v2允许输入水平滚动，v3补充静态完整值字宽；不以Mg0123的宽替代实际数字宽。
- Replay速度PopupTextElement四Text滚动定位，原宽断言不变，增加viewport/真实4PNG；数据是明确synthetic QA replay，不伪造成绩或照片。未在本probe验证实际速度选项业务变化。
- 四Arrow按住期间（真实InputSystem键状态）测FlightInput.Raw对应pitch/roll的±1、其他轴0、Read未被UI guard屏蔽、Simulator.CurrentInput实际收到、Flight UI焦点不变、session仍running；松开Raw归零，再真实Space无误动作。证据为合成键状态/模拟器输入，不能宣称物理键盘/遥控器/iOS/轨迹动力学完整验证。

所有新检查仍按false计FAIL，runtime错误计入；只在全部检查无失败时状态PASS_AUTOMATED_ONLY、退出0。UNRENDERED metadata不改变旧报告，不计成PASS；任何断言或runtime失败退出2。参考23原图不改，截图不编辑，不代表视觉验收。报告仍独立于34-state聚合。

## 根负责人运行命令

```bash
# 只复制/编译/注入独立新副本，不启动Unity或native，不改原交付app。
bash verification/v1-qa/build-space-submit-probe-v3.sh /absolute/uninstrumented/candidate05/AeroSim.app
# 使用上条命令最后一行实际新路径。
SPACE_V3_APP=/private/tmp/aerosim-space-v3.XXXXXX/AeroSim-UIQA.app
SPACE_V3_PROFILE=$(mktemp -d /private/tmp/aerosim-space-v3-profile.XXXXXX)
SPACE_V3_OUTPUT=/Users/bytedance/Documents/ChatGPT/飞行模拟器/verification/v1-qa/results/candidate-05/space-submit-v3
"$SPACE_V3_APP/Contents/MacOS/AeroSim" --space-submit-qa-v3 "$SPACE_V3_OUTPUT" \
  --data-dir "$SPACE_V3_PROFILE" -screen-width 1920 -screen-height 1080 \
  -screen-fullscreen 0 -logFile /private/tmp/aerosim-space-submit-v3-player.log
```

输出 `space-submit-v3-report.json`，包括原v1 hash、v2原scope、v3新scope和candidate04原v2 report hash。检查/截图数以实际内容为准；不要因缺目标无崩溃称PASS。必有目标计数断言，缺任何Slider/Foldout/rate即失败。

## 静态验证与限制

v2/v3生成器8个unit PASS；新C# against未注入candidate03 Managed Roslyn compile exit0；bash语法通过。只做静态编译，未启动Unity/native、未改生产/Git。**native NOT_RUN / 人工PENDING**，待根05构建后实际验证。

暂不覆盖Phone safeArea、Flight OSD central遮挡、全部23视觉、完整UI/HUD/Text矩阵；文本容差3逻辑px不是像素一致。UNRENDERED祖先证据不能自动证明Unity实际draw clipping机制；展开截图才用于排除真实内容缺陷。高级区域/大字数字截图需要实看caret/字形与焦点可见性，MeasureTextSize不能替代用户验收。
