# Ubuntu / Linux 试用版

简体中文 · [English](../linux.md)

ScreenshotBox 的 Linux 前端使用 Avalonia 开发。**0.1.5-linux.1** 是面向 **Ubuntu x64、X11 桌面**的试用版，与 Windows 应用共享资料库格式、字面检索、备份校验和 OCR 队列。

## 可用功能

- 框选截图，调整选区大小或整体移动。使用画笔、箭头、矩形和马赛克标注；调整工具大小、擦除标注和撤销。
- 保存并复制、仅复制或导出 PNG。保存后在后台使用 CPU 识别文字。
- 导入 PNG/JPEG 图片，编辑标题、备注和标签，添加星标，移到回收站或恢复。
- 搜索标题、备注、标签和图片文字，支持中文两字词。预览原图，缩放、平移并高亮匹配的整行文字框。
- 使用浅色、深色或系统主题。语言可跟随系统，也可在设置中选择简体中文或英文。
- 备份资料库，恢复到空目录，或迁移到自选目录；迁移后保留原目录。

Linux 试用版暂不提供托盘和登录自启动。关闭资料库窗口会退出应用。截图后仍可通过窗口打开资料库。

## 运行发行包

在[发行页](https://github.com/liugedragon/screenshot-box/releases)下载 Linux `.tar.gz`。包内包含 .NET、中文 OCR 模型和原生库，使用时不需要开发 SDK、Python 或 CUDA。

```bash
sha256sum -c ScreenshotBox-0.1.5-linux.1-linux-x64.sha256
tar -xzf ScreenshotBox-0.1.5-linux.1-linux-x64.tar.gz
cd ScreenshotBox-0.1.5-linux.1-linux-x64
./ScreenshotBox.Linux
```

运行时需要可连接的 X11 显示服务。如果 Ubuntu 登录界面提供 Xorg 会话，可先选择该会话。Wayland 会话下禁用截图与全局快捷键，仍可导入和搜索已有图片。

程序依赖正常 Linux 桌面系统提供的 X11、fontconfig、ICU、OpenSSL 和 C/C++ 运行库。目前已在 Ubuntu 20.04.3、glibc 2.31 上运行验证。遇到缺少共享库的问题，可检查：

```bash
ldd ./ScreenshotBox.Linux
ldd ./libSkiaSharp.so
ldd ./libHarfBuzzSharp.so
ldd ./libonnxruntime.so
ldd ./libe_sqlite3.so
```

## 为当前用户安装

在解压后的发行目录执行：

```bash
./installer/linux/install.sh
```

默认安装到 `~/.local/opt/ScreenshotBox`，为当前用户添加应用菜单入口。可以指定其他安装位置：

```bash
./installer/linux/install.sh --prefix "$HOME/Applications/ScreenshotBox"
```

默认安装可运行 `~/.local/opt/ScreenshotBox/installer/linux/uninstall.sh` 卸载。自定义安装位置时使用对应路径：

```bash
./installer/linux/uninstall.sh --prefix "$HOME/Applications/ScreenshotBox"
```

卸载删除程序及菜单入口，保留资料库和设置。Linux 安装脚本不添加登录自启动项。

## 资料与设置

| 内容 | 默认位置 |
| --- | --- |
| 原图、缩略图和数据库 | `$XDG_DATA_HOME/ScreenshotBox/library`；未设置时使用 `~/.local/share/ScreenshotBox/library` |
| 设置 | `$XDG_CONFIG_HOME/ScreenshotBox/settings.json`；未设置时使用 `~/.config/ScreenshotBox/settings.json` |

在设置中修改资料目录。迁移目录或切换语言后应用重新启动。Linux 与 Windows 使用同一种备份格式，恢复时选择新的空目录。

选择“跟随系统”时，应用依次检查 `LC_ALL`、`LC_MESSAGES` 和 `LANG`。中文区域设置使用简体中文，其他区域设置使用英文。“跟随系统”标签随界面语言翻译。

截图快捷键默认为 `Ctrl+Alt+S`，可在设置中改为 `Alt+A` 等组合。应用检查常见桌面保留组合及实际 X11 全局占用；无法完整检测其他应用内部的快捷键。新快捷键注册失败时保留原来的有效快捷键。

## WSL 与 XLaunch

WSL 可以运行 Linux 程序，通过 XLaunch/VcXsrv 显示窗口。请按本机 X 服务配置 `DISPLAY`，并允许 Windows 防火墙中的相应连接。正确地址取决于 WSL 网络配置，不同电脑可能不同。

X11 截图后端采集的是 X 服务的桌面，**不能截取 Windows 桌面或 Windows 应用窗口**。X11 快捷键也需要键盘事件到达该 X 服务。截图普通 Windows 应用时，请使用 Windows 版本。

XLaunch 适合检查 Linux 窗口、截图工具和剪贴板组件，不能完整复现 Ubuntu 的窗口管理器、托盘、桌面快捷键及多屏环境。

## 构建与测试

需要 .NET SDK **10.0.401**、GCC、binutils、curl 和 Python 3。中文测试图片需要 `fonts-noto-cjk` 等支持中文的字体。中文模型和字典的固定下载地址及 SHA-256 见 [`models/chinese/sources.json`](../../models/chinese/sources.json)。

```bash
python3 scripts/fetch-models.py
bash scripts/build-linux-sqlite.sh
dotnet restore src/ScreenshotBox.Linux/ScreenshotBox.Linux.csproj --locked-mode
dotnet build src/ScreenshotBox.Linux/ScreenshotBox.Linux.csproj --configuration Release --no-restore
bash scripts/package-linux.sh 0.1.5-linux.1
```

打包前通过 [`build-linux-sqlite.sh`](../../scripts/build-linux-sqlite.sh) 构建固定的 SQLite 3.53.3。NuGet 包自带的 Linux SQLite 库需要 glibc 2.33，因此发行包使用兼容 Ubuntu 20.04 基线的源码构建。构建该基线的发行包时，需使用 glibc 2.31 或等效工具链。

[Linux 服务验证](../../tests/ScreenshotBox.Linux.Services.Probe/README.zh-CN.md)运行真实 CPU 中文 OCR，检查文字框、字面检索、图片写入和数据恢复。现有 Core 测试也可在 Linux 上运行，测试前将兼容的 SQLite 库放入输出目录的 `runtimes/linux-x64/native/`：

```bash
dotnet restore tests/ScreenshotBox.Core.Tests/ScreenshotBox.Core.Tests.csproj --locked-mode
dotnet build tests/ScreenshotBox.Core.Tests/ScreenshotBox.Core.Tests.csproj --no-restore
cp artifacts/native/linux-x64/libe_sqlite3.so \
  tests/ScreenshotBox.Core.Tests/bin/Debug/net10.0/runtimes/linux-x64/native/libe_sqlite3.so
dotnet test tests/ScreenshotBox.Core.Tests/ScreenshotBox.Core.Tests.csproj --no-build --no-restore
```

## 验证记录与限制

**Ubuntu 20.04.3 / WSL、.NET 10.0.12** 上的结果：

| 范围 | 结果 |
| --- | --- |
| 共享 Core 测试：坐标几何、中文字面查询、元数据、任务代数竞态和备份校验 | 42 项通过 |
| Linux 服务，使用 SkiaSharp 3.119.4 和真实 CPU PP-OCRv5 中文模型推理 | 41 项通过 |
| Linux 截图组件检查 | 42 项通过 |
| 英文浅色与中文深色应用组件：剪贴板、重复启动、编辑焦点及真实 760 DIP 窗口 | 两种语言各 20 项通过 |
| 当前用户安装事务、所有权、回滚及卸载 | 19 项通过 |
| SQLite 3.53.3 源码构建 | SOURCE_ID 与源码校验值一致；FTS5、JSON、数学函数、R-tree 和所需导出通过；最高 glibc 依赖为 2.29 |
| OCR 共享模块提取后的 Windows WPF Release 构建 | 通过，无警告或错误 |

服务验证的合成图片识别出 5 行文字框。仅凭图片文字即可搜索“课程”“订单”“保修”、`B204`、`English` 和 `2026-09-27`。上述结果属于程序化组件与集成检查，不能据此认定所有桌面快捷键、合成器或粘贴目标都已验证。

Wayland 截图和基于 portal 的快捷键尚未实现。混合 DPI 多屏、实机 Ubuntu 桌面、常用 Linux 应用的手工图片粘贴，以及未安装开发工具的新机器断网运行，尚未验证。OCR 可能识别错误，高亮按识别出的整行显示。Linux 试用版的验证范围独立于 Windows 发行版。

组件版本、原始许可正文和模型来源见[第三方软件与模型](third-party.md)。

## 界面与记录

[英文应用检查](../test-results/linux-0.1.5-linux.1/app-en.json)、[中文应用检查](../test-results/linux-0.1.5-linux.1/app-zh.json)、[安装检查](../test-results/linux-0.1.5-linux.1/installer.json)。记录使用合成资料，工作区路径已替换为占位符。

![英文 X11 资料库](../images/en/linux-library.png)

图片来自运行中的原生应用窗口，使用六张合成英文示例图，OCR 产生 79 个行框。
