# 打包与安装

简体中文 · [English](../distribution.md)

## 构建发行包

安装向导要求 **Windows 11 x64**（22000 及更新版本），主要开发与测试环境为 Windows 11。便携 exe 的目标平台最低为 Windows 10 19041，但尚未实测 Windows 10 兼容性。构建环境见[构建说明](build.md)。

```powershell
powershell -ExecutionPolicy Bypass -File scripts/package.ps1 -Version 0.1.4
powershell -ExecutionPolicy Bypass -File scripts/installer.ps1 -Version 0.1.4
```

`package.ps1` 编译和测试后生成完整发行目录及 `artifacts/ScreenshotBox-0.1.4-win-x64.zip`。包中包含 .NET 运行时、Windows x64 原生组件、OCR 模型、许可证、依赖锁文件和逐文件校验清单 `FILE-SHA256SUMS.txt`。ZIP 的 SHA-256 写入旁边的 `.sha256` 文件。

`installer.ps1` 使用 Inno Setup 将已有发行目录打成 `ScreenshotBox-0.1.4-win-x64-setup.exe`，并生成独立校验文件。默认编译器为 `.tools/inno/ISCC.exe`，可用 `-CompilerPath` 指定。

## 安装包与 ZIP

安装向导支持简体中文和英文，默认安装到当前用户的 `%LOCALAPPDATA%\Programs\ScreenshotBox`，目录页可选择其他可写位置。安装创建开始菜单和 Windows 卸载入口，不需要管理员权限。

ZIP 解压后运行 `ScreenshotBox.exe`，无需安装 SDK、Python 或单独的 .NET 运行时。请保留原生库和模型，不要只复制 exe。

ZIP 还附带 [`installer/install.ps1`](../../installer/install.ps1) 与 [`installer/uninstall.ps1`](../../installer/uninstall.ps1)。安装脚本可用 `-Destination` 指定位置。从 0.1.4 起，脚本安装默认注册登录启动，可传 `-NoAutoStart` 取消；脚本卸载仅在 Run 项指向该安装时删除。卸载只删除清单中的应用文件，保留额外文件。升级或卸载前从托盘退出应用。

应用语言默认跟随 Windows 界面语言，中文系统使用简体中文，其他系统使用 English。设置中可选择跟随系统、简体中文或 English，重启后生效；与安装向导语言独立。

## 登录启动（0.1.4）

安装向导默认在当前用户登录时启动 ScreenshotBox，启动任务可以取消，完成页的启动选项同样在托盘启动。向导在 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 写入名为 `ScreenshotBox` 的字符串，内容为带引号的安装 exe 路径和 `--background` 参数。

Windows 通过任务管理器的“启动应用”控制该项。向导和脚本安装保持项名不变，不写 `StartupApproved`，保留已有禁用状态。升级时取消任务会删除 Run 项，卸载也删除该项。

Run 命令超过 260 字符时会在安装前拒绝；需选择更短的安装目录或取消登录启动。便携运行不添加该注册表项。

## 用户资料

卸载保留 `%LOCALAPPDATA%\ScreenshotBox` 中的设置和资料库，以及用户指定的其他资料目录。迁移资料请用应用的迁移、备份或恢复功能；不要在运行时只复制 `library.db`。

## 发行检查与签名

发行检查包括安装、离线启动、截图、剪贴板、OCR、搜索、备份恢复与卸载。各版本测试环境和结果见[验证记录](validation.md)，未验证条件见[已知限制](limitations.md)。

已发布的 **0.1.4** 安装包与 `ScreenshotBox.exe` 均未进行 Authenticode 签名。Edge 可能显示“通常不会下载”。微软将此归为[下载信誉提示](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation)，它本身不是病毒检出；刚签名的新文件也可能显示该提示。无需关闭浏览器或 Windows 的安全保护。

从 [0.1.4 发行页](https://github.com/liugedragon/screenshot-box/releases/tag/v0.1.4)下载，并与旁边的 `.sha256` 文件核对。在下载目录打开 PowerShell 运行：

```powershell
(Get-FileHash .\ScreenshotBox-0.1.4-win-x64-setup.exe -Algorithm SHA256).Hash.ToLowerInvariant()
```

安装包预期 SHA-256 为 `96b7ddfbad053630706e3c188ea0f22a31549f00df90a016744b100b2220a38e`，大小 88,097,504 字节；ZIP 的 SHA-256 为 `5acb89e67c4032ff4187063ebc41748a6d7640040cba00f861944778f609e1b9`。一致只能证明下载文件与该发行附件相同，不能证明发布者身份或代码无害。如果 Windows 明确报告病毒，请停止运行，并在 Issue 中提供检测名称和文件哈希。

后续直接下载的发行版需要与已验证发布者身份对应、能在全新 Windows 电脑上获得信任的代码签名证书。两个打包脚本都接受 `-RequireSignature -CertificateThumbprint <40 位十六进制证书指纹> -TimestampUrl <CA 提供的 RFC 3161 地址>`，也可用 `-SignToolPath` 指定 Windows SDK 的签名工具。`package.ps1` 先签名 `ScreenshotBox.exe`，再生成校验清单和 ZIP；`installer.ps1` 要求主程序已签名，并让 Inno Setup 为卸载程序和安装包签名。缺少必要的签名或校验失败时，脚本会停止。私钥不要放进仓库。

当前构建机没有发行签名证书，因此尚未用这条流程生成签名发行版。发布前还需在干净的 Windows 环境验证签名、安装和下载表现。有效签名可改善发布者身份和信誉，但不保证新文件立刻不再提示；[微软说明](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation)会同时考虑文件哈希和发布者信誉。现有 0.1.4 附件与标签保持不变。
