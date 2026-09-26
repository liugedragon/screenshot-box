# 安装程序的自动启动检查

[English](README.md)

这两份 Windows PowerShell 5.1 脚本使用隔离样例检查自动启动注册。测试编译一个小型 Windows 程序，并复制正式安装脚本，将应用 ID、Run 值名和开始菜单目录替换为随机标识，不安装或启动完整的 ScreenshotBox。

运行环境为 Windows 11 x64。Setup 检查需要 Inno Setup 6；若编译器不在 `.tools\inno\ISCC.exe`，通过 `-CompilerPath` 指定。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests\ScreenshotBox.Installer.Probe\Verify-Startup.ps1 -CompilerPath "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
powershell -NoProfile -ExecutionPolicy Bypass -File tests\ScreenshotBox.Installer.Probe\Verify-ScriptStartup.ps1
```

每次测试在 `artifacts/` 下新建目录，保存日志和 `result.json`，并清除自己的注册项。可选的 `-OutputDirectory` 必须指向尚不存在的目录，路径宜短，以便检查自动启动命令长度边界。

Setup 检查默认选中、更新时保留选择、取消自动启动、路径变化、260 字符命令限制、路径引号和卸载。脚本检查还覆盖 `-NoAutoStart`、保留其他安装目录拥有的 Run 值，以及保留安装清单之外的文件。

测试前后比较所有原有 HKCU Run 和 StartupApproved 值。测试期间不要修改启动设置或运行其他安装程序。测试不会将快照写回用户注册表；只在自己的随机名称下写入一个不解析其含义的 StartupApproved 字节值，用于验证安装脚本保留该值，不依赖 Windows 未公开的编码。

这些检查不覆盖实际重新登录、在任务管理器中点击禁用或托盘交互，这些项目需要另外进行应用和 Windows 检查。进程超时后，脚本只终止自己启动的那一个进程。

微软文档说明了 [Run 命令长度限制](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys)以及 [Windows 设置和任务管理器的自动启动管理](https://support.microsoft.com/en-us/windows/experience/startup-boot/configure-startup-applications-in-windows)。
