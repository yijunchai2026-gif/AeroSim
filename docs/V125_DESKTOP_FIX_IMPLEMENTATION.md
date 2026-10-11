# AeroSim 1.25：电脑端最小修复实现交接

日期：2026-10-10，America/Los_Angeles。岗位：第四波电脑页面程序员。状态：**源码已冻结，等待根统一候选08构建与独立 QA；不是原生通过、1.25 发布或用户验收。**

实施依据为 [1440×900 三项缺口诊断](V125_DESKTOP1440_TRIAGE.md)。先完整读 AGENTS、[开发断点](V125_PROGRESS.md)及诊断，先查 Git 状态；保留初始三个旧图像 meta、候选04图册/复核及三个 Wave B 文档。本人没有提交、推送、启动 Unity/native 构建或应用，没有改原 probe、原23图、历史报告或字体基准队列。

## 1. 本波改动与影响面

| 文件 / 局部 | 实施内容 | 保持不变 |
| --- | --- | --- |
| `Runtime/AeroSimApplication.FlightDesignV1.cs` / `BuildDesignFlight` flight bar | 给现有 Pause 局部实例设置 `tabIndex = 1`，再按原位置加入 bar | Objective 仍在 Pause 前方的视觉树顺序；没有新 `Focus()`、回调或导航处理；箭头 guard、空焦点 Space、Pause/modal 字体及生命周期原样 |
| `Resources/UI/Product.uss` / dashboard copy | 标题与直接 `.wrap` 子元素使用 `width:55%; max-width:100%; height:auto; flex-shrink:0`；description 高优先级规则同步确定宽度 | 标题 `36px`、全部字体声明、说明原 `min-height:52px`、hero padding/min-height、status 的390px、CTA原约束及间距原样；不写死折行所需像素高度 |
| `Resources/UI/Responsive.uss` 及 Product 原 description 覆盖 | `layout-phone`、`layout-tablet`、`text-expanded` 的标题/直接 wrap/description 均明确 `width:100%; max-width:100%` | 不改全局断点/Label/页面布局，不为 tablet 扩展 CTA；旧 phone/text-expanded CTA max-width100规则保持 |

选择器影响面由源码扫描核对：`design-dashboard-hero` 与 `design-assignment-description` 仅在 `DesignDashboard()` 创建。`Pages.cs` 把 Professional 的 `Page.Home` 和显式 `Page.Dashboard` 路由到此函数，所以两者会使用补丁。Aerial Home 为 `BuildAerialHome()`，FPV Home 为 `DesignFpvHome()`，不创建这两个 class，不被本次局部宽度规则匹配。

`DesignPanel→Panel()` 的标题是直接 `.panel-title` Label，副标题 `NEXT TRAINING TASK` 是直接 `.muted.wrap` Label，说明是直接 `.wrap.design-assignment-description` Label。因此 `.design-dashboard-hero > .wrap` 的确定宽度同时影响原已受55%上限约束的副标题与说明；没有改非 hero 文本，也没有新增树容器或写入 `DesignV1.cs`。

现有 `LayoutDesignPages()` 的 stack 条件为 logical width `<1280`、height `<500` 或 Text `>1.1`。现有 `OnGeometryChanged()` 用 `layout-phone` 覆盖 width `<760` 或 height `<500`，用 `layout-tablet` 覆盖其余760–1279宽，用 `text-expanded` 覆盖 Text `>1.1`。故三种覆盖涵盖现有全部 stack 分支，不需另造 `layout-stack` 或改门槛。Responsive 的 hero copy覆盖含4个class，Product 的 description覆盖含3个class，均优先于2个class的 description基线规则。

## 2. API / 源码核查

已读本机 Unity `6000.3.25f1` 的 `UnityEngine.UIElementsModule.xml`，存在 `Focusable.tabIndex`，值1符合其非负排序 API。另查 [Unity 6.3 focus manual](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-focus-order.html) 和 [官方6000.3 focus ring reference](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.3/Modules/UIElements/Core/VisualElementFocusRing.cs)：同一 scope 中唯一正 `tabIndex` 会先于零索引项；此补丁只给 HUD Pause 加一个正索引，其他元素保留自然顺序。这是 API / reference 阅读，不是已证明本机应用的实际焦点行为。

本实现不改无焦点的 Shift-Tab：它仍依现有 focus ring 反向进入末项；从 Pause 继续 Tab / Shift-Tab、Objective 可到达、modal 焦点不逃逸等都交新原生 QA 实测，不能仅由正索引推断全部焦点门禁通过。进入 Flight 不会自动焦点 Pause；旧 modal trap、Pause 首按钮聚焦和 dialog恢复代码未改。

`width`、`max-width`、`height:auto`、`flex-shrink` 使用 [Unity 6.3 官方 USS 允许语法](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-USS-SupportedProperties.html)。确定宽度意在使自然测量获得目标列宽；是否关闭窄宽最终折行高度缺口须候选08实测，未声称已经证明 Yoga / Unity 的特定 bug 或百分比二次计算。

## 3. 实施者静态自查

- 自查33项全部 PASS：C#整文件与当前HEAD比较只允许这一个Pause建立块替换；Objective视觉树序保留；只有一个正索引；没有自动Focus/第二导航回调；本机API存在；两USS括号平衡；三种基线copy和九个响应copy覆盖声明完整；全部font-size声明未增删/改值；CTA原桌面约束和tablet未扩展。
- 三个业务文件的 `git diff --check` 无输出、exit0；差异仅为上述一个C#建立块及局部dashboard规则。不把33项静态检查记成布局、输入或视觉运行通过，也不替独立QA签字。
- 本人没有C#完整Unity集成编译、原生运行或截图；原07c全部字体通过证据不适用新字节。根将统一构建新候选08；独立QA原样复跑Space/Pause/Replay合同并另冻焦点/局部测量补充，原容差/失败/UNRENDERED不改。

建议独立QA至少覆盖原诊断矩阵中的1440×900与1920×1080、1280×720、HUD1559/1560、Text100/125/150/200及live100→200→100；确认首次前向Tab确为`action-pause`、首Space为Pause overlay而非Objective overlay、焦点进入Resume；确认继续导航仍可达Objective、modal trap及恢复不误操作。Professional Home 的title/description需记录实际最终宽高/字体/MeasureTextSize并实看PNG，按钮后移/可点击、手机及tablet/expanded全宽分别检验。未经实际执行均为 **NOT_RUN**。

## 4. 源码冻结与交接

本岗位结束写入前，三个业务文件 SHA256：

| 文件（相对 `AeroSim/Assets/AeroSim/`） | SHA256 |
| --- | --- |
| `Runtime/AeroSimApplication.FlightDesignV1.cs` | `987e6fd81586443e7e20107c3a448527ab3975c94e1223eb3fa729257ddb02f5` |
| `Resources/UI/Product.uss` | `0b0f3f76696d24d8f6f16ddca6c3e36281b08df60b1f021e8b51cd7637504d8e` |
| `Resources/UI/Responsive.uss` | `2cd2ce8fa9abb26248e99188c623e11ae11ba175142b27acffb643b07d793afd` |

只读核对、本人未改的字体队列 `Runtime/AeroSimApplication.Responsive.cs` 为 `ae7b80aeecc5d8c49f81907236022861d7fd59ea4d8073353a54b38bfdbc9a25`；页面树 `Runtime/AeroSimApplication.DesignV1.cs` 为 `197bd9fc9f4945d41a3779cb88ace2304fcb6efda23d4603311e5f48d6936f96`。此处仅记录交接时本机字节，不把它们改称旧06/07b源码。

本人将三业务文件及本文冻结后交根/独立QA，停止写入。若确定宽度仍失败，先由根重新授权 `DesignV1.cs` copy容器边界再实施，不擅扩scope。

仓库：[AeroSim](https://github.com/yijunchai2026-gif/AeroSim)；分支：[codex/v125-product-lines](https://github.com/yijunchai2026-gif/AeroSim/tree/codex/v125-product-lines)。本波尚无本人commit或push；根统一检查提交/认证阻塞并记录实际commit/远端SHA。此前四笔本地未上传与旧失败不由本文消除，不合并main、不force-push、不改账号安全。三端输入接线、iOS真机/物理控制器、23图视觉一致、逐机型用户确认及最终验收均不在本次静态通过范围，1.25仍是开发中。
