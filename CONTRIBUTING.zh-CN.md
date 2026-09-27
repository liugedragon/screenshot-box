# 贡献指南

简体中文 · [English](CONTRIBUTING.md)

## 报告问题

查看[已知限制](docs/zh-CN/limitations.md)，然后使用 [Issue 模板](https://github.com/liugedragon/screenshot-box/issues/new/choose)。请提供：

- 应用与操作系统版本，以及安装方式。Linux 请注明发行版和 X11/Wayland 会话。
- 显示器数量、分辨率、各屏幕缩放和相对位置。
- 复现步骤、预期结果、实际结果和错误原文。
- 适用时提供合成或脱敏的测试图片。

请勿上传私人截图、资料库、识别出的个人内容、密钥或账号信息。

## 建议功能

说明使用场景、遇到的问题和目前的替代做法。较大的功能或新依赖请先开 Issue 讨论，已有计划见 [ROADMAP](ROADMAP.zh-CN.md)。

## 提交修改

1. Fork 仓库，为一个明确的修改创建分支。
2. 按[构建说明](docs/zh-CN/build.md)准备 Windows x64 和 .NET SDK 10.0.401。
3. 运行 `powershell -ExecutionPolicy Bypass -File scripts/build.ps1`。涉及截图或标注时，运行[截图像素测试](tests/ScreenshotBox.Capture.Probe/README.zh-CN.md)。
4. 提交 PR，说明问题、修改后的行为、验证方法和未测试的条件。界面修改请附使用合成资料的窗口截图。

Linux 修改按 [Linux 构建与测试说明](docs/zh-CN/linux.md)和[服务检查](tests/ScreenshotBox.Linux.Services.Probe/README.zh-CN.md)准备；涉及截图时还需运行 [X11 截图检查](tests/ScreenshotBox.Linux.Capture.Probe/README.zh-CN.md)。上面的步骤描述 Windows 构建。

保持依赖锁文件。新增组件或模型需记录来源、版本、许可证和分发方式。模型、用户数据和构建输出不提交到 Git。文档与界面文字修改需同步中英文；新增界面控件遵循[设计规范](docs/zh-CN/design.md)。

涉及存储、坐标、检索或 OCR 状态的修改，请补充相应回归测试。纯文档修改检查链接和排版即可。

## 许可证

提交贡献表示同意以本项目的 MIT 许可证发布该贡献。第三方材料须保留其原有许可证和声明。
