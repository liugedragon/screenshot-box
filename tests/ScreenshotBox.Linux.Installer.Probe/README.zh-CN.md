# Linux 安装检查

[English](README.md)

```bash
bash tests/ScreenshotBox.Linux.Installer.Probe/verify.sh
python3 tests/ScreenshotBox.Linux.Installer.Probe/verify-archive.py
```

测试在 `artifacts/` 下创建一个小型合成发行目录，以及隔离的 XDG 数据和设置目录，使用包含中文与空格的安装路径，不向当前用户的桌面目录写入菜单或自动启动项。

检查涵盖损坏的旧清单、注入暂存复制及发布失败后的回滚、同名未归属文件保护、校验错误时拒绝覆盖、更新时文件归属、保留用户文件和资料库、桌面入口归属、正常卸载，以及拒绝清单路径越界和指向安装目录外的符号链接。日志与 `result.json` 保存在生成的目录中。

开发测试需要 Bash、GNU coreutils 和 Python 3；正式安装脚本不需要 Python。这些检查不启动桌面菜单入口，也不验证真实已安装应用进程。

归档检查验证 644/755 权限、不含本机用户信息的所有者字段、文件内容完整性和符号链接拒绝。构建文件系统忽略 `chmod` 时，归档仍会写入统一权限。
