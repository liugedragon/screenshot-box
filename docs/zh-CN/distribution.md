# 打包与安装

简体中文 · [English](../distribution.md)

## 构建发行包

应用目标为 Windows 10 2004（19041）及更新版本的 x64 系统，主要测试环境为 Windows 11。构建环境见[构建说明](build.md)。

```powershell
powershell -ExecutionPolicy Bypass -File scripts/package.ps1 -Version 0.1.3
powershell -ExecutionPolicy Bypass -File scripts/installer.ps1 -Version 0.1.3
```

`package.ps1` 编译和测试后生成完整发行目录及 `artifacts/ScreenshotBox-0.1.3-win-x64.zip`。包中包含 .NET 运行时、Windows x64 原生组件、OCR 模型、许可证、依赖锁文件和逐文件校验清单 `FILE-SHA256SUMS.txt`。ZIP 的 SHA-256 写入旁边的 `.sha256` 文件。

`installer.ps1` 使用 Inno Setup 将已有发行目录打成 `ScreenshotBox-0.1.3-win-x64-setup.exe`，并生成独立校验文件。默认编译器为 `.tools/inno/ISCC.exe`，可用 `-CompilerPath` 指定。

## 安装包与 ZIP

安装向导支持简体中文和英文，默认安装到当前用户的 `%LOCALAPPDATA%\Programs\ScreenshotBox`，目录页可选择其他可写位置。安装创建开始菜单和 Windows 卸载入口，不需要管理员权限。

ZIP 解压后运行 `ScreenshotBox.exe`，无需安装 SDK、Python 或单独的 .NET 运行时。请保留原生库和模型，不要只复制 exe。

ZIP 还附带 [`installer/install.ps1`](../../installer/install.ps1) 与 [`installer/uninstall.ps1`](../../installer/uninstall.ps1)。安装脚本可用 `-Destination` 指定位置，卸载脚本只删除清单中的应用文件，保留额外文件。升级或卸载前从托盘退出应用。

应用语言默认跟随 Windows 界面语言，中文系统使用简体中文，其他系统使用 English。设置中可选择跟随系统、简体中文或 English，重启后生效；与安装向导语言独立。

## 用户资料

卸载保留 `%LOCALAPPDATA%\ScreenshotBox` 中的设置和资料库，以及用户指定的其他资料目录。迁移资料请用应用的迁移、备份或恢复功能；不要在运行时只复制 `library.db`。

## 发行检查与签名

发行检查包括安装、离线启动、截图、剪贴板、OCR、搜索、备份恢复与卸载。各版本测试环境和结果见[验证记录](validation.md)，未验证条件见[已知限制](limitations.md)。

应用和安装包目前没有项目 Authenticode 签名，Windows 可能显示未知发布者。
