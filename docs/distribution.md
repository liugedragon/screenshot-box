# 构建与安装

简体中文 · [English](en/distribution.md)

需要 Windows 10 2004（19041）或更新版本，x64。源码构建使用 .NET SDK 10.0.401；优先使用项目 `.tools/dotnet/dotnet.exe`，否则使用本机 SDK。依赖由锁文件固定，运行 `scripts/build.ps1` 会检查锁文件、编译应用并执行核心模块测试。

`scripts/package.ps1 -Version 0.1.2` 先构建测试，再产生自带 .NET 运行时的 `artifacts/ScreenshotBox-0.1.2-win-x64.zip`。用户不必安装 Python、.NET 或登录账号；中文 OCR 模型随包携带。包中只包含 Windows x64 原生组件，包含依赖锁文件、许可证、每个文件的校验清单。ZIP 的 SHA-256 写在同目录 `.sha256` 文件中。

`scripts/installer.ps1 -Version 0.1.2` 使用 Inno Setup 编译器，把已生成的版本目录打成 `ScreenshotBox-0.1.2-win-x64-setup.exe`，并生成单独校验文件。默认查找 `.tools/inno/ISCC.exe`，也可传入 `-CompilerPath`。安装向导支持简体中文与英文，默认安装到当前用户的 `%LOCALAPPDATA%/Programs/ScreenshotBox`，可在目录选择页更改为其他有写入权限的位置，创建开始菜单入口和系统“已安装应用”卸载入口，不需要管理员权限。

如果只使用 ZIP，可以解压后直接启动 `ScreenshotBox.exe`。ZIP 还包含 `installer/install.ps1` 和 `installer/uninstall.ps1`，供不使用安装向导的用户安装到当前用户目录。安装脚本也支持 `-Destination` 自定义目录，卸载仅删除清单内的应用文件，保留额外资料。这两个脚本不能在程序运行期间替换或删除应用文件，需要先正常退出应用。

卸载只移除应用文件与入口，保留 `%LOCALAPPDATA%/ScreenshotBox` 中的设置和资料库以及用户指定的其他资料目录。资料迁移应使用应用的 ZIP 备份与恢复功能；不要在应用运行期间只复制 `library.db`。

发布前仍需在真实 Windows 桌面上确认安装、首次离线启动、框选与剪贴板、OCR、搜索、备份恢复和卸载行为。自动测试覆盖存储一致性和矩形几何，不等同于已经测过真实双屏不同缩放、锁屏、远程桌面或所有剪贴板目标软件。

当前构建不带项目自己的 Authenticode 代码签名。Windows 可能显示未识别发布者。Inno Setup 编译工具的官方数字签名已验证，但这不代表生成的应用或安装包有项目签名。
