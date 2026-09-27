# 界面检查记录

简体中文 · [English](../ui-review.md)

## Linux 0.1.5-linux.1 · 2026-09-27

环境：WSL Ubuntu 20.04.3、XLaunch X11、Avalonia 12.1.3。检查使用合成资料，在自带运行时的发行程序中显示原生窗口并渲染客户区。设置窗口高 680 DIP，详情栏在布局完成后生成图片。

英文浅色和中文深色各通过 20 项应用检查，覆盖独立进程读取图片剪贴板、重复启动激活、编辑内容与焦点及光标保留，并通过 X11 几何信息确认 760 DIP 窗口。英文图库导入六张彩色示例图，识别得到 79 个文字行框；原图预览按整行高亮保修关键词。

[资料库](../images/en/linux-library.png)、[深色图库](../images/en/linux-library-dark.png)、[设置](../images/en/linux-settings.png)、[原图高亮](../images/en/linux-preview.png)。验证范围与限制见 [Linux 说明](linux.md)。

## 0.1.3 · 2026-09-26

环境：Windows 11 x64，2560 × 1440 单屏，125% 缩放。使用合成资料、程序操作 WPF 控件，并查看导出的客户区图片。

| 区域 | 检查内容 | 图片 |
| --- | --- | --- |
| 语言设置 | 提供跟随系统、简体中文和 English。系统中文使用简体中文，其他系统语言使用英文；应用修改后重启。 | [英文设置](../ui-review-images/0.1.3/en-settings.png)、[中文系统自动选择](../ui-review-images/0.1.3/system-zh-settings.png)。 |
| 资料库 | 导航、按钮、状态和详情切换语言；标题、标签、备注和 OCR 内容保持原样。窄窗口保留截图、导入和详情操作。 | [英文资料库](../ui-review-images/0.1.3/en-library.png)、[英文窄窗口](../ui-review-images/0.1.3/en-narrow.png)、[中文资料库](../ui-review-images/0.1.3/zh-library.png)。 |
| 详情 | 小窗口中的英文字段、未保存提示及固定保存页脚可读。 | [英文小详情窗](../ui-review-images/0.1.3/en-editor-small.png)。 |
| 截图与调色盘 | 英文工具名、提示、参数和保存操作可完整显示，RGB/Hex 控件及应用按钮使用当前界面语言。 | [320 DIP 画笔栏](../ui-review-images/0.1.3/en-capture-narrow.png)、[英文调色盘](../ui-review-images/0.1.3/en-palette.png)。 |

六种工具、四种宽度（320/560/720/960 DIP）共 24 个英文布局样本均通过工具栏边界、文字完整及调整点中心无遮挡检查，见[布局日志](../ui-review-images/0.1.3/layout-log.txt)。语言选择器的三项和当前策略也有[应用集成检查](validation.md#013--2026-09-26)覆盖。

图片来自构建后的应用窗口。尚未完成完整人工语言切换和重启流程，也未安装其他 Windows 显示语言包。系统错误和 Windows 文件对话框可能使用操作系统自身语言。其他布局和硬件限制见下文。

## 0.1.2 · 2026-09-26

环境：Windows 11 x64，2560 × 1440 单屏，125% 缩放。检查通过启动 WPF 窗口、程序操作控件、导出客户区并查看图片完成。使用隔离资料库与合成课程图片。完整人工键鼠流程、混合 DPI 和外接屏测试尚未完成。

### 资料库、详情和设置

| 问题 | 修改 | 图片或检查结果 |
| --- | --- | --- |
| 90 DIP 排序框截断中文选项；长标签挤占工具区。 | 排序框宽度改为 112 DIP，标题占剩余空间并省略；标签限制宽度并显示完整提示。 | [浅色资料库](../ui-review-images/0.1.2/ui-light.png)、[窄窗口](../ui-review-images/0.1.2/ui-narrow.png)。 |
| “收藏”和“保存”混用。 | 截图操作为“保存并复制”，分类为“最近保存”，资料编辑为“保存修改”。 | [空资料库](../ui-review-images/0.1.2/ui-empty.png)、[深色资料库](../ui-review-images/0.1.2/ui-dark.png)。 |
| 主详情和独立编辑窗可覆盖彼此修改，草稿状态不明确。 | 同一图片仅开放一个编辑入口；显示未保存状态，关闭保留草稿，保存后更新状态。 | [详情窗](../ui-review-images/0.1.2/ui-editor-dark.png)、[小详情窗](../ui-review-images/0.1.2/ui-editor-small.png)；草稿及数据库写入检查通过。 |
| 点击标签等同关键词搜索，包含 OCR 同词但未打标签的图片。 | 先按精确标签成员筛选，再组合关键词、排序和分页。 | UI 标签检查及 Core 回归测试通过。 |
| 预览截获滚动条点击；最小 5% 缩放无法适应长图。 | 仅图片区域支持拖动平移；长图适应当前窗口，实际大小按当前 DPI 换算。增加复制、导出及 Ctrl+C/0/1。 | [原图及文字行高亮](../ui-review-images/0.1.2/ui-preview.png)；20000 px 长图随窗口增减重算，1000 px 原图实际显示 1000 个物理像素。 |
| 设置操作状态随内容滚出窗口。 | 按用途分组，固定底部状态与“完成”按钮。 | [设置顶部](../ui-review-images/0.1.2/ui-settings.png)、[底部](../ui-review-images/0.1.2/ui-settings-bottom.png)、[浅色小窗口](../ui-review-images/0.1.2/ui-settings-small-light.png)。 |
| 缩略图大小和工具参数未及时持久保存。 | 缩略图变更延迟保存；截图结束和退出时保存工具偏好，启动时加载。 | 参数持久化路径检查。 |
| 图片预览快捷键可能拦截按钮的空格和回车。 | 限制快捷键在图片列表内触发，并排除按钮事件来源。 | 以保存按钮为事件来源的窗口按键处理器检查通过。 |

[诊断窗口](../ui-review-images/0.1.2/ui-diagnostic.png)显示缺失原图、识别失败及模型信息。短详情窗通过滚动访问全部字段；窄主窗口折叠详情，可打开独立详情窗。

图片导出检查还发现客户区截图遗漏根布局外边距。修正导出范围后重新生成设置和详情图片。

### 截图工具与调色盘

| 工具 | 参数与行为 | 图片 |
| --- | --- | --- |
| 画笔、箭头、矩形 | 红 `#E63737`、蓝 `#1769C2`、绿 `#18A75B`、黄 `#F4C430`、白、黑；1–32 物理像素笔宽。每条标注保存独立颜色和宽度。 | [画笔](../ui-review-images/0.1.2/capture-large-pen.png)、[箭头](../ui-review-images/0.1.2/capture-large-arrow.png)、[矩形](../ui-review-images/0.1.2/capture-large-rectangle.png) |
| 调色盘 | RGB 滑条、Hex 输入、颜色预览和应用；无效输入保留调色盘并提示。 | [调色盘](../ui-review-images/0.1.2/palette-large.png)、[浅色窗口](../ui-review-images/0.1.2/palette-light.png) |
| 橡皮擦 | 4–80 物理像素大小，沿路径恢复截图原像素，移除画笔、箭头、矩形或马赛克。 | [橡皮擦](../ui-review-images/0.1.2/capture-large-eraser.png) |
| 马赛克 | 6–32 物理像素块大小，各矩形保存自己的参数。 | [马赛克](../ui-review-images/0.1.2/capture-large-mosaic.png) |
| 历史操作 | Ctrl+Z 撤销、Ctrl+Y 重做；清空可撤销，新标注替换旧重做分支。 | [选区工具栏](../ui-review-images/0.1.2/capture-large-select.png) |
| 窄窗口 | 小于 420 DIP 时采用当前工具加菜单，只展开当前参数。 | [画笔](../ui-review-images/0.1.2/capture-narrow-pen.png)、[橡皮擦](../ui-review-images/0.1.2/capture-narrow-eraser.png)、[工具菜单](../ui-review-images/0.1.2/tools-menu-narrow.png) |

共保留 27 张工具界面图：六种工具 × 四种宽度共 24 张，加两张调色盘和一张工具菜单。320/560/720/960 DIP 的 24 个样本均无工具栏越界，八个调整点中心均未被遮挡，见[布局与控件日志](../ui-review-images/0.1.2/validation-log.txt)。控件检查应用了 13.25 px 笔宽、RGB `#7F3FBF` 和 Hex `#8247CB`。

检查中修正了三处问题：窄工具栏遮住两个调整点、全局文字样式覆盖紧凑按钮字号、滑块缺少可见轨道。工具栏使用独立浅色样式，统一选中、悬停、焦点和禁用状态，蓝色用于主要保存操作。

标注输出由[合成像素探针](../../tests/ScreenshotBox.Capture.Probe/README.zh-CN.md)检查，35 项通过。连续 20 次桌面采集的 GDI 句柄数保持 1→1；热键冲突处理、裁剪、撤销和取消见[原生运行日志](../ui-review-images/0.1.2/native-runtime-log.txt)。

### 当前布局限制

缩略图用于辨认图片，阅读小字需要打开原图。短详情窗需要滚动；空间不足时，截图工具栏可能放在选区内部。工具栏、调色盘及笔刷大小圈不进入导出图片。24 个布局样本未覆盖所有选区位置和显示器组合。其他待测项见[测试记录](validation.md#待验证)。

## 0.1.0 / 0.1.1 历史检查

| 问题 | 修改与历史图片 |
| --- | --- |
| 深色应用主题使截图栏按钮混用深浅色。 | 为截图工具栏设置独立浅色配色。[修改前](../ui-review-images/before/capture-large-select.png)、[修改后](../ui-review-images/capture-large-select.png)。 |
| 展开画笔参数后，工具栏超出窗口或覆盖调整点。 | 重新测量工具栏并定位，窄窗口缩减空白。四宽度、三模式的 12 个样本通过。[窄画笔栏](../ui-review-images/capture-narrow-pen.png)、[历史日志](../ui-review-images/validation-log.txt)。 |
| 短详情窗的图片占用过多空间。 | 常规预览高度 140 DIP，短窗口 90 DIP，固定保存页脚。[修改前](../ui-review-images/before/editor-light-small.png)、[修改后](../ui-review-images/editor-dark-small.png)。 |
| 空库展示整块禁用详情表单，导航选中状态不明确。 | 增加选择图片提示，统一导航选中状态。[空资料库](../ui-review-images/ui-empty.png)、[浅色](../ui-review-images/ui-light.png)、[深色](../ui-review-images/ui-dark.png)。 |
| 深色设置下拉框白底白字，详情滚动条贴近内容。 | 修正主题资源，正文右侧保留 12 DIP。[设置](../ui-review-images/ui-settings.png)、[深色详情](../ui-review-images/editor-dark.png)。 |
| 实际大小按 DIP 显示，125% 缩放下放大原图。 | 按当前窗口 DPI 换算，1000 px 图像显示为 1000 物理像素。[原图预览](../ui-review-images/ui-preview.png)。 |

这些版本的画笔只有两种颜色、两档宽度；0.1.2 参数见上表。历史图片保留原版本界面。
