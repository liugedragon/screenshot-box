# ScreenshotBox.Linux

简体中文 · [English](README.md)

基于 Avalonia 12.1.3 的 X11 试用版界面。与 Windows 共享 `LibraryStore` 和 `OcrQueue`，截图和快捷键使用 X11 接口。

## 构建

安装 `global.json` 指定的 .NET SDK、GCC、Python 3 和 Ubuntu 桌面基础库。Python 用于构建脚本，运行应用不需要 Python。

在仓库根目录执行：

```bash
python3 scripts/fetch-models.py
bash scripts/build-linux-sqlite.sh
dotnet restore src/ScreenshotBox.Linux/ScreenshotBox.Linux.csproj --locked-mode
dotnet build src/ScreenshotBox.Linux/ScreenshotBox.Linux.csproj -c Release --no-restore -m:1
bash scripts/package-linux.sh
```

打包脚本使用已校验的源码构建 SQLite 替换 NuGet 二进制。Ubuntu 20.04 需要此步骤，因为 SQLitePCLRaw 自带库要求更高的 glibc。使用说明和验证范围见 [Linux 文档](../../docs/zh-CN/linux.md)。

`MainWindow` 管理资料库和选择状态；`PreviewWindow` 将 OCR 行框映射到缩放后的图像坐标；`SettingsWindow` 保存语言、主题、快捷键与资料目录。`Native` 负责 X11 采集和热键注册。`AnnotationModel` 将标注对象与冻结原图分开保存。`LinuxImageLibrary` 在写入数据库记录前保存图片文件。

重复启动通过私有 Unix 套接字激活已有进程。切换语言和迁移目录时，应用先停止 OCR 工作线程并释放套接字，再启动新进程。Linux 与 Windows 使用独立的中间构建目录。
