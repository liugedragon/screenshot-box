# 测试记录

简体中文 · [English](../validation.md)

## GitHub 托管的 Windows 候选构建 · 2026-09-28

[工作流 36439669958](https://github.com/liugedragon/screenshot-box/actions/runs/36439669958)在 GitHub 托管的 Windows 2025 构建机上完成，源码提交为 `8060a57ebf678fc9bae97bcb5d835469ecc51d66`。流程安装 .NET SDK 10.0.401 和通过哈希核对的 Inno Setup 7.1.0，获取固定版本的中文 OCR 模型，然后生成未签名的 `0.1.4-ci.36439669958` ZIP 与安装包。工作流核对了两个 SHA-256 文件，并将产物和 `BUILD-PROVENANCE.json` 上传为保留 14 天的 Actions 附件。此次确认了托管打包流程，没有运行安装后的程序、执行 Authenticode 签名或发布发行版。

## Windows 签名流程 · 2026-09-28

在 Windows 11、PowerShell 5.1 和 Inno Setup 7.1 上，更新后的打包脚本通过语法检查。临时构建的未签名发行 ZIP 和测试安装包均成功生成，主程序的逐文件校验清单与 ZIP 校验文件一致。要求签名却未提供证书指纹，或指纹不在证书存储区中时，脚本均在生成发行文件前停止。测试签名器即使返回成功，只要未给卸载程序添加签名，Inno Setup 仍会拒绝编译。临时测试文件已清理。

本机没有可用的代码签名证书及私钥，因此**尚未**生成或测试已签名 ZIP、安装包和卸载程序。取得正式证书后，仍需在干净的 Windows 环境核验签名和下载行为。

## 0.1.4 下载检查 · 2026-09-28

Edge 显示的“通常不会下载”属于文件信誉提示；截图未显示病毒检出。已发布的安装包（88,097,504 字节）和 ZIP（113,771,310 字节）与 GitHub 发行附件 SHA-256 一致。`Get-AuthenticodeSignature` 对安装包及解压后的 `ScreenshotBox.exe` 均返回 `NotSigned`。

本机已启用 Microsoft Defender Antivirus，病毒特征在当天更新。对本地安装包和主程序的定向扫描完成，未发现与这两个文件匹配的新检测。此结果只代表这一台电脑和当时的特征版本，不能证明文件绝对安全，也不能建立 SmartScreen 下载信誉。[下载检查记录](../test-results/windows-0.1.4-download-check.json)。0.1.4 的发行二进制和标签没有修改。

## 0.1.4 · 2026-09-26–27

环境：Windows 11 x64，单屏 2560 × 1440、125% 缩放；.NET SDK 10.0.401，运行时 10.0.12，CPU OCR。Release 构建为 0 警告、0 错误，42 项 Core 测试全部通过。

### 应用集成检查

构建后的 exe 分别使用独立合成资料库运行，两种语言的进程退出码均为 0：

| 界面 | 结果 | 首次 OCR |
| --- | --- | ---: |
| English | 43 项通过 | 735 ms |
| 简体中文 | 43 项通过 | 605 ms |

检查覆盖 OCR 与原图行框、中文和符号查询、资料编辑、剪贴板读回、重试、删除竞态、备份恢复、原子覆盖、草稿、预览尺寸和语言设置。公开报告：[英文](../ui-review-images/0.1.4/en-self-test.json)、[简体中文](../ui-review-images/0.1.4/zh-self-test.json)。报告省略本地路径与临时资料 ID，OCR 文字来自合成测试资料。

### 后台启动

启动脚本分别运行英文和简体中文各 13 项检查，全部通过，用户设置保持不变。测试使用独立实例标识和资料目录，包含真实原生窗口句柄、托盘图标和热键注册。

- 后台启动未加载或显示资料库窗口。
- 隐藏窗口占有截图热键，竞争注册被拒绝。
- 第二个后台进程退出，不打开资料库。
- 再次正常启动打开已有资料库，托盘与热键继续有效。
- 关闭窗口后回到托盘，再次后台启动仍不显示窗口。

报告：[英文](../ui-review-images/0.1.4/en-startup-test.json)、[简体中文](../ui-review-images/0.1.4/zh-startup-test.json)。构建后在仓库根目录复现：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/test-startup.ps1
```

可用 `-ExePath` 指定解压的发行程序，`-OutputDirectory` 指定新的结果目录。未进行真实登出再登录和任务管理器启用、禁用操作。

### 安装启动项

隔离注册测试中，Inno Setup 安装程序通过 25 项，PowerShell 安装脚本通过 20 项。测试使用独立安装标识和 Run 项名，原有 Run 与 StartupApproved 值保持不变。

覆盖默认启动选项、带引号的中文自选路径和 `--background`、升级保留选择、取消与重新选择、安装前拒绝超过 260 字符的命令，以及卸载移除启动项。脚本检查还验证了取消或卸载时保留其他安装占有的 Run 项和清单外文件。

StartupApproved 以不透明测试数据处理，未解码 Windows 格式，正式安装程序不写入该项。测试未操作任务管理器或进行真实登录。报告：[Inno Setup 25 项](../ui-review-images/0.1.4/installer-startup-check.json)、[PowerShell 20 项](../ui-review-images/0.1.4/script-startup-check.json)。

### 候选安装包与卸载

当地时间 2026-09-27，候选包安装到含中文和空格的自选目录。安装、卸载退出码均为 0，497 个已安装 exe/dll/onnx 的哈希与发行目录一致，开始菜单快捷方式存在。

Run 项使用带引号的安装 exe 路径与 `--background`，StartupApproved 保持不变。安装后的程序在英文与简体中文下各通过 13 项启动检查，在跟随系统、英文和简体中文下各通过 43 项应用检查。跟随系统在测试电脑选择中文，首次 OCR 为 990 ms。

第二个独立进程恢复 1 项中断 OCR 任务，课程查询返回 4 项，备注查询返回 2 项，缺失原图 0 项。测试进程的 PATH 仅含 Windows 目录，DOTNET_ROOT 不存在，代理不可用；运行时从安装目录加载，系统网络未物理隔离。

卸载移除 exe、卸载登记与 Run 项。数据库哈希、5 张原图、5 张缩略图和额外用户文件保持不变，用户设置未变。

[安装报告](../ui-review-images/0.1.4/installer-check.json)以 SHA-256 标识之前测试的候选包，省略本地路径与临时标识。最终程序从[源码提交 c6739d3](https://github.com/liugedragon/screenshot-box/commit/c6739d39c843bd360b885a3becdfeeb50a9ddf90)重新构建，使 SourceLink 元数据对应已提交源码。应用代码和依赖未变，可执行文件、程序集和调试符号的元数据发生变化。

重新构建的程序通过 43 项应用检查，英文与简体中文下各通过 13 项启动检查，中文 OCR 为 1865 ms。[重构建报告](../ui-review-images/0.1.4/committed-binary-check.json)记录了这些结果。元数据更新后的程序未重复上述完整安装与卸载流程。最终文档打包以重新构建的二进制为基准，下载文件的哈希随发行附件提供。真实登录和任务管理器控制仍未验证。

### Windows 图标

Windows `LoadImage` 按 16、20、24、32、40、48、64、128、256 px 加载原创 ICO。九项均返回指定尺寸，包含透明与不透明像素。[图标报告](../ui-review-images/0.1.4/icon-load-test.json)以 SHA-256 标识资源，源文件与生成方法见[设计说明](design.md#应用图标)。

## 0.1.3 · 2026-09-26

环境：Windows 11 x64，单屏 2560 × 1440、125% 缩放；SDK 10.0.401，随包运行时 10.0.12，CPU OCR。App Release 构建为 0 警告、0 错误。

### 应用集成检查

直接启动构建目录中的 `ScreenshotBox.exe`，分别使用三个语言策略和独立合成资料库：

| 策略 | 实际界面 | 结果 | 首次 OCR |
| --- | --- | --- | ---: |
| 跟随系统 | 测试电脑显示简体中文 | 43 项通过 | 595 ms |
| English | 英文 | 43 项通过 | 566 ms |
| 简体中文 | 简体中文 | 43 项通过 | 589 ms |

检查包括语言设置持久化、界面资源和应用错误翻译、旧配置缺少语言、未知值回退、手选覆盖系统、System 策略持久化及语言三选项。原有的中文 OCR、搜索、资料编辑、剪贴板读回、重试、备份恢复、原子覆盖、草稿和预览尺寸检查在三次运行中均通过。用户输入的标题、备注、标签和识别文字保持原语言。

系统语言映射使用参数化检查：`zh-CN`、`zh-TW`、`zh-HK`、`zh-Hant` 使用简体中文；`en-US`、`fr-FR`、`ja-JP`、`de-DE`、`ko-KR` 使用英文。测试电脑的 Windows 显示语言为中文，未安装或切换其他操作系统语言。

公开报告：[跟随系统](../ui-review-images/0.1.3/system-self-test.json)、[英文](../ui-review-images/0.1.3/en-self-test.json)、[简体中文](../ui-review-images/0.1.3/zh-self-test.json)。从构建目录或解压后的应用目录复现，每种语言使用一个新的测试资料路径：

```powershell
$testData = Join-Path $env:TEMP ("ScreenshotBox-test-" + [guid]::NewGuid().ToString("N"))
.\ScreenshotBox.exe --self-test --language en-US --data-dir $testData
```

其他策略将 `en-US` 换为 `System` 或 `zh-CN`。命令行语言覆盖仅用于测试模式，日常使用在设置中选择语言。

### 英文截图布局

六种工具在 320/560/720/960 DIP 四种宽度下共 24 个英文工具栏样本。工具栏均在窗口范围内，标签完整，未遮挡调整点中心。见[布局日志](../ui-review-images/0.1.3/layout-log.txt)和[界面检查记录](ui-review.md)。

### 候选安装包与卸载

0.1.3 候选安装包安装到含中文和空格的自选目录，退出码 0，安装目录登记和开始菜单快捷方式正确。安装后的 497 个 exe/dll/onnx 哈希与发行目录一致。

安装后的程序在跟随系统、英文和简体中文三种策略下各通过 43 项检查。跟随系统运行显示中文，OCR 为 556 ms，识别后工作集为 363.0 MiB。第二个独立进程恢复 1 项中断任务，“课程”查询返回 4 项，备注查询返回 2 项，缺失原图 0 项。

测试进程 PATH 仅含 Windows 目录，DOTNET_ROOT 指向不存在位置，代理不可用；运行时从安装目录加载。系统网络未物理隔离。

卸载退出码 0，程序与卸载登记移除。数据库哈希、5 张原图、5 张缩略图和额外用户文件保持不变。

[安装检查摘要](../ui-review-images/0.1.3/installer-check.json)以 SHA-256 标识本次候选安装包，省略本地绝对路径及任务标识。本记录在最终包同步文档之前完成，最终下载文件的校验值见发行附件。硬件与人工流程待测项见文末。

## 0.1.2 · 2026-09-26

环境：Windows 11 x64（内核 10.0.26340），i7-14650HX，单屏 2560 × 1440、125% 缩放；.NET SDK 10.0.401，随包运行时 10.0.12，CPU OCR。

本记录区分核心自动测试、合成像素测试、程序化 Windows 集成检查和安装检查。界面检查通过创建 WPF 窗口、调用控件并查看客户区截图完成；完整的人工键鼠流程及其他待测场景列在文末。测试资料均为合成图片。

### 自动测试

| 项目 | 结果 | 覆盖范围 |
| --- | --- | --- |
| Core / SQLite | 42 通过，0 失败、0 跳过 | 中文短词和符号查询、精确标签、索引更新、持久化、筛选后分页、删除恢复、OCR 任务竞态、备份恢复、选区几何 |
| 截图标注像素探针 | 35 通过，0 失败 | 正负桌面原点、裁剪、六色、7.5/32 px 笔宽、箭头、反向矩形、6/32 px 马赛克、橡皮、撤销重做、清空和历史分支 |
| App Release 构建 | 0 警告、0 错误 | 锁定依赖恢复及 Windows 应用构建 |

精确标签测试覆盖标题或 OCR 同词排除、`%`、`_`、引号、中英文逗号、空白、大小写、编辑后重启，以及与关键词、星标、排序和分页组合。OCR 任务测试检查过期任务及已删除图片的结果不被写回。

[Windows CI 记录](https://github.com/liugedragon/screenshot-box/actions/runs/36243627343)在提交 `9d01e0a` 上通过 42 项 Core 测试及 35 项像素检查。当前工作流见 [windows.yml](../../.github/workflows/windows.yml)。

在 Windows 的仓库根目录复现：

```powershell
.\scripts\build.ps1 -Configuration Release
dotnet restore tests/ScreenshotBox.Capture.Probe/ScreenshotBox.Capture.Probe.csproj --locked-mode
dotnet run --project tests/ScreenshotBox.Capture.Probe/ScreenshotBox.Capture.Probe.csproj --configuration Release --no-restore
```

[像素探针说明](../../tests/ScreenshotBox.Capture.Probe/README.zh-CN.md)列出检查方法。该探针只使用内存中的合成图案；桌面采集、热键、工具栏布局及剪贴板由以下集成检查覆盖。

### Windows 集成检查

- 连续采集 20 次 2560 × 1440 桌面，GDI 句柄数保持 1→1；DPI 为 120，线程 DPI awareness 为 2（PerMonitorV2）。桌面图像只在内存中使用。
- 热键检查覆盖 Alt+A、多修饰键、数字键、常见系统保留组合、全局占用冲突、更新失败后保留原注册及释放后重新注册。
- 200 × 160 物理像素裁剪、标注合成、撤销恢复原像素、马赛克及取消无产物通过。
- 六种工具 × 四种窗口宽度（320/560/720/960 DIP）的 24 个样本中，工具栏均在窗口范围内，遮挡调整点中心数均为 0/8。
- 控件检查覆盖 13.25 px 笔宽、RGB `#7F3FBF`、Hex `#8247CB`、无效 Hex 提示及六工具菜单。模拟显示设置变化后，截图取消并解除事件订阅。

公开日志：[原生运行](../ui-review-images/0.1.2/native-runtime-log.txt)、[布局与控件](../ui-review-images/0.1.2/validation-log.txt)。截图及布局修正见[界面检查记录](ui-review.md)。

### 发行程序与安装

最终安装包安装到含中文和空格的自选目录，退出码 0，安装目录登记和开始菜单快捷方式正确。安装后的 497 个 exe/dll/onnx 文件与发行目录一致。

直接启动安装后的 `ScreenshotBox.exe`，使用独立合成资料库。测试进程的 PATH 仅含 Windows 目录，DOTNET_ROOT 指向不存在位置，代理不可用；运行时从安装目录加载。系统网络未隔离。

29 项布尔检查全部通过，进程退出码 0：

- 1000 × 600 中文图片输出四行文字、原图行框和置信信息；首次 OCR 684 ms。
- “课程”“订单”“保修”、`AB-20260926`、`English`、`100%`、`A_B` 七种查询通过；备注和标签修改后可检索。
- 剪贴板图片写入后读回尺寸为 1000 × 600。
- 缺少原图时识别失败，补回原图并重试后可搜索；识别中删除、恢复并重新入队通过。
- 备份恢复保留图片、分类和文字。第二个独立进程恢复 1 项中断识别，“课程”查询返回 4 项，原备注返回 1 项，缺失原图 0 项。
- PNG 和图片导出覆盖成功；目标被文件锁定时，旧文件逐字节不变，临时文件清理完成。
- 独立编辑窗草稿、保存状态、单一编辑入口及精确标签筛选通过。
- 125% 缩放下，实际大小模式中的 1000 px 图片占 1000 个物理像素；20000 px 长图适应窗口并随窗口大小重算。
- 用保存按钮作为 WPF 窗口按键处理器的事件来源，空格和回车未被图片预览快捷键截获。

卸载退出码 0。程序与卸载登记移除，数据库 SHA-256、5 张原图、5 张缩略图及安装目录内额外用户文件保持不变。

包内校验覆盖 617 个文件、33 个原生 x64 库和 8 个微软 CRT 签名；中文模型及字典与来源哈希一致。以下为已发布 0.1.2 文件：

| 文件 | 字节数 | SHA-256 |
| --- | ---: | --- |
| ZIP | 111,799,384 | `43a934744867e7857dcd0fc432c058d03b376be96b08579d3552d14148b84a57` |
| 安装包 | 85,945,228 | `78affe92730737e5908effc010b4fad1318dad434b6c8ec37cc3976f6c4a747e` |

下载与校验文件见 [0.1.2 发行页](https://github.com/liugedragon/screenshot-box/releases/tag/v0.1.2)。完整安装日志和结构化结果为本地构建记录：`artifacts/installer-shipping-validation-0.1.2/result.json`；这些文件未提交到源码仓库。

发行程序自测可在新建的测试资料目录运行，输出 `self-test.json`；后续独立启动恢复中断任务，输出 `restart-test.json`：

```powershell
$testData = Join-Path $env:TEMP ("ScreenshotBox-test-" + [guid]::NewGuid().ToString("N"))
.\ScreenshotBox.exe --self-test --data-dir $testData
.\ScreenshotBox.exe --resume-ocr-test --data-dir $testData
```

### 内存观测

0.1.2 多窗口集成测试在 OCR 后工作集为 361.3 MiB。0.1.1 常规模式空资料库（托盘和热键启用、模型未加载）启动 6 秒后工作集为 148.3 MiB、私有内存 94.4 MiB。0.1.1 多窗口测试 OCR 后为 360.3 MiB，空闲 90 秒释放模型后为 281.1 MiB。以上为单机测量，窗口数量与模型状态不同。

## 历史记录

### 0.1.1

- Core 33 项通过；新增“最近保存先筛选再排序/分页”回归。
- 发行 exe 检查中文 OCR、七种查询、剪贴板读回、删除恢复竞态、备份恢复和实际像素大小。
- 第二次并发启动 440 ms 退出，首进程继续运行；独立启动恢复 1 项中断任务，缺少原图 0 项。
- 修复 SQLiteProvider 冷启动问题：Core 显式初始化 SQLitePCL bundle。

### 0.1.0

- Core 32 项通过，0 失败、0 跳过。
- 当前用户静默安装、自选中文路径、安装后集成检查和卸载通过。卸载保留数据库、3 张原图、3 张缩略图及额外用户文件。
- ZIP：108,968,831 字节，SHA-256 `601c35fb9d44859f51ad27c937a403313357437e41eb1993d66ac530d9cf789e`。
- 安装包：83,286,837 字节，SHA-256 `3ad3b6858d9b83e1462c07edb0e5acaabb83e6a7d4fe40eea3b0d9144d5692d6`。

## 待验证

- 实机双屏混合 DPI、主屏左侧或上方屏幕、跨屏鼠标框选及外接屏拔插。
- 常见外部应用粘贴、完整人工键鼠流程及长期连续使用。
- 物理断网且未安装开发 SDK 的新 Windows 环境。
- 更多系统缩放比例、显示器布局及大规模资料库性能。
- 设置中目录选择、迁移、确认对话框和自动重启的完整人工流程。

当前的剪贴板读回、几何测试、显示设置回调及随包运行时检查分别覆盖相应组件，不包含上述实机使用场景。
