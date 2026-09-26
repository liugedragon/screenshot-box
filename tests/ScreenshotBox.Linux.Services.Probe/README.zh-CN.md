# Linux 服务验证

[English](README.md)

在 Linux 上运行共享 OCR 和资料库代码，不打开桌面窗口。程序生成中英混排的测试图片，所有数据写入新的输出目录，不读取用户资料库。

需要 .NET SDK 10.0.401、GCC、binutils、curl、Python 3，以及支持简体中文的字体，例如 `fonts-noto-cjk`。请先按主构建文档下载 PP-OCRv5 中文识别模型和字典。

在仓库根目录执行：

```bash
scripts/build-linux-sqlite.sh

dotnet restore tests/ScreenshotBox.Linux.Services.Probe/ScreenshotBox.Linux.Services.Probe.csproj --locked-mode
dotnet build tests/ScreenshotBox.Linux.Services.Probe/ScreenshotBox.Linux.Services.Probe.csproj --no-restore
cp artifacts/native/linux-x64/libe_sqlite3.so \
  tests/ScreenshotBox.Linux.Services.Probe/bin/Debug/net10.0/linux-x64/libe_sqlite3.so

dotnet tests/ScreenshotBox.Linux.Services.Probe/bin/Debug/net10.0/linux-x64/ScreenshotBox.Linux.Services.Probe.dll \
  artifacts/linux-services-probe
```

每次运行选择新的输出目录。目录非空时程序会退出，避免覆盖已有文件。

输出包括 `result.json`、`ocr-result.json`、合成原图、独立资料库和恢复后的备份。验证范围包括 PNG 导入、等比例缩略图、原子导出、CPU 中文识别与整行坐标、中文两字查询、符号查询、元数据更新、删除与 OCR 任务代数竞态、原图缺失、备份内容和中断任务恢复。断言失败时程序以失败状态退出。

本程序不验证桌面截图、全局快捷键、剪贴板交互、托盘和 Wayland。
