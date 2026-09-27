# 架构

简体中文 · [English](../architecture.md)

## 模块

| 模块 | 职责 |
| --- | --- |
| `ScreenshotBox.Core` | SQLite 资料库、搜索、OCR 任务代数、备份与恢复；不依赖 WPF。 |
| `ScreenshotBox.App/Native` | Win32 热键、GDI 桌面采集、每屏遮罩、选区、标注与 PNG 输出。 |
| `ScreenshotBox.App/Services.cs` | Windows 原图与缩略图、剪贴板重试。 |
| `ScreenshotBox.Ocr` | 共享的 CPU OCR 队列与模型生命周期。 |
| `ScreenshotBox.Linux` | Avalonia 资料库界面、X11 截图与快捷键、标注和 PNG 导入。 |
| `PreviewWindow` | 原图、文字行框、缩放和平移。 |

主窗口使用可观察列表展示资料，视图处理焦点、选择和窗口事件。数据操作在 Core 和 Services 中完成。`SelectionGeometry` 独立处理矩形计算。

## 启动与托盘（Windows）

正常启动打开资料库。`--background` 初始化窗口原生句柄、截图热键和托盘图标，不显示主窗口。两种模式都会恢复待识别任务。双击托盘图标或选择“打开资料库”显示窗口。

安装向导在当前用户的 Windows Run 项注册带引号的 exe 路径与 `--background`，运行程序本身不注册启动项。向导不改写 Windows 的 `StartupApproved` 状态；项名固定为 `ScreenshotBox`，卸载时删除。

每用户互斥锁保证单实例。再次正常启动会通知已有实例打开资料库；再次后台启动直接退出，不显示窗口。后台启动失败时写入 `%LOCALAPPDATA%\ScreenshotBox\diagnostics\startup-error.txt`，不弹出对话框。

Linux 试用版使用私有 Unix 套接字处理重复启动，保留可见资料库窗口，不注册登录自启动或托盘。

下面的截图、标注、预览和编辑章节描述 Windows 前端。检索、OCR 和存储逻辑由两个版本共享；前端差异见 [Linux 说明](linux.md)。

## 截图坐标

虚拟桌面原点可以为负。应用枚举所有屏幕，计算联合物理矩形，并用 GDI 冻结画面。鼠标位置由 `GetCursorPos` 读取；选区与标注统一使用物理像素。

每块显示器有独立遮罩窗口，显示尺寸按本屏 DPI 转为 DIP。最终裁剪原点为 `全局选区坐标 − 虚拟桌面左上坐标`，宽高直接取选区物理像素；标注渲染时减去选区原点。几何模块处理反向框选、移动夹紧和调整边界。

exe 的 manifest 声明 PerMonitorV2。DPI 验证需启动 exe，`dotnet` 宿主不含该声明。多屏实测范围见[验证记录](validation.md)。

## 标注与预览

每项标注存储颜色、大小和源像素位置。马赛克使用块平均色，橡皮恢复冻结原图的像素。操作历史包含清空，支持撤销和重做；新编辑使重做分支失效。调色盘只改变后续笔画与形状的颜色。

预览把原图和 OCR 整行框放在同一源像素 Grid 中，统一缩放后由 ScrollViewer 平移。实际大小使用 `1 / DpiScale`，因此 1000 px 图片在 125% 屏幕上仍占 1000 个物理像素。适应窗口按当前可用尺寸重算，允许长图低于 5% 的缩放；拖动只从图片开始。

## 搜索

标题、备注、标签和 OCR 文字使用参数化 `instr(lower(column), lower($q))` 查询。引号、百分号和下划线作为字面内容，ASCII 字母不区分大小写。

SQLite FTS5 的默认 `unicode61` 不进行中文词语切分，trigram 对少于三字符的全文查询有限制，因此使用子串扫描支持短中文词。时间索引与分页减少展示开销，大资料库的查询性能仍需测量。

标签分类使用确定性的 SQLite 成员函数，在 `WHERE` 中与关键词、星标等条件相交，再排序和分页。标签按中英文逗号分隔、Trim 和 `OrdinalIgnoreCase` 比较，保留原始编辑文本。

OCR 原文和行框使用原图坐标保存。高亮范围是引擎提供的整行框。搜索不做繁简转换、拼音或语义匹配。

## OCR 队列与状态

图片写入成功后建立资料记录并加入 Channel 队列。单消费者复用模型并限制 CPU 线程，状态和任务代数持久保存。

开始任务取得 generation。删除、重试和相关状态改变更新代数；写回结果时验证代数、Processing 状态和未删除条件，过期结果被丢弃。启动将中断的 Processing 恢复为 Pending。失败保留图片与错误，可手动重试。

识别中删除后立即恢复时，旧任务可能仍占用去重槽。释放槽后再次检查记录，仅在未删除且仍 Pending 时重新入队；Failed 不自动无限重试。

## 编辑与数据过渡

独立编辑窗打开时，同一条目的主详情字段禁用。保存更新内存条目与已保存基准；关闭编辑窗保留会话草稿。退出、备份、迁移和恢复前统一处理保存、放弃或取消。

备份使用 SQLite `BackupDatabase` 取得一致快照，按快照清单打包不可变原图和缩略图，包含回收站记录。恢复校验路径、结构、版本、完整性和文件清单，只写入新的空目录。迁移与恢复成功后重启，原库保留。数据过渡期间阻止新增图片和元数据写入。

另存和导出先写同目录临时文件并刷新，成功后原子替换，失败清理临时文件并保留已有目标。

缩略图大小、标注参数和语言偏好存入本地设置。语言偏好为跟随系统、简体中文或 English。跟随系统时读取 `GetUserPreferredUILanguages` 返回的首个 Windows 显示语言，失败时回退到 `GetUserDefaultUILanguage`：`zh-*` 使用简体中文，其余使用英语。手动选择覆盖系统规则，启动时应用，切换后重启；图片文字和用户分类保留原文。全局热键通过 Win32 注册。

## 组件职责

WPF UI 提供 Windows 主题和控件，Avalonia 提供 Linux 界面；Microsoft.Data.Sqlite/SQLite 提供事务数据库；RapidOcrNet 调用检测、方向和识别模型；PaddleOCR 提供训练权重；ONNX Runtime 在 CPU 上推理；SkiaSharp 处理 OCR 图像。

ScreenshotBox 实现选区交互、热键更新、标注历史、调色盘、资料视图、检索、任务状态、备份恢复和集成测试。组件版本与许可证见[第三方说明](third-party.md)。
