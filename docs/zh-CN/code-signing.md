# 代码签名政策

简体中文 · [English](../code-signing.md)

已发布的 Windows 0.1.4 安装包和主程序**未签名**。目前没有签名服务商批准 ScreenshotBox。[发行页面](https://github.com/liugedragon/screenshot-box/releases/tag/v0.1.4)列出文件和 SHA-256 校验值；哈希一致只能确认文件身份，不能证明软件安全。核对方法见[发行检查](distribution.md#发行检查与签名)。

仓库所有者维护发行流程并决定发布版本。当前项目由一名维护者管理：[liugedragon](https://github.com/liugedragon)负责提交源码、审查外部贡献以及批准发行和签名请求。其他贡献者的修改须经审查后合并。未来的签名请求须由仓库所有者批准。如果以后发布已签名版本，构建将在 GitHub 托管的 Windows 构建机上运行，记录源码提交，锁定依赖并核对 OCR 模型哈希。[候选构建流程](../../.github/workflows/windows-candidate.yml)目前只生成供检查的**未签名**文件，不发布发行版。

ScreenshotBox 将截图、资料信息和识别文字保存在所选的本地资料目录中。使用期间不会将其发送到服务器，也不会下载 OCR 模型。用户可以主动导出图片或备份。安装程序默认选中当前用户的登录自启动选项；安装时可以取消，也可以在任务管理器中禁用。安装程序会创建卸载入口；[使用说明](usage.md)列出了安装和数据位置。卸载后资料目录仍会保留。

如果以后发布已签名版本，发行说明将标出证书发布者及签名核对方法。有效签名可验证发布者与文件完整性，但不能保证新下载文件不出现 [Microsoft SmartScreen 信誉提示](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation)。
