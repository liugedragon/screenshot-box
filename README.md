<div align="center">

<img src="assets/screenshotbox.svg" width="64" height="64" alt="截图资料盒图标">

# 截图资料盒 · ScreenshotBox

**截图存下来，按文字找回来。**

简体中文 · [English](README.en.md)

[![Windows CI](https://github.com/liugedragon/screenshot-box/actions/workflows/windows.yml/badge.svg?branch=main)](https://github.com/liugedragon/screenshot-box/actions/workflows/windows.yml)
[![Release](https://img.shields.io/github/v/release/liugedragon/screenshot-box?include_prereleases&label=release)](https://github.com/liugedragon/screenshot-box/releases)
[![MIT](https://img.shields.io/badge/license-MIT-1769C2)](LICENSE)

[**下载安装包**](https://github.com/liugedragon/screenshot-box/releases/download/v0.1.2/ScreenshotBox-0.1.2-win-x64-setup.exe) · [**下载 ZIP**](https://github.com/liugedragon/screenshot-box/releases/download/v0.1.2/ScreenshotBox-0.1.2-win-x64.zip) · [使用说明](docs/usage.md) · [反馈问题](https://github.com/liugedragon/screenshot-box/issues/new/choose)

</div>

课程安排、订单编号、保修日期——截图很快，过几天再找却不容易。截图资料盒把截图和识别出的文字一起保存在电脑里。你可以框选、标注、保存并复制，之后搜“课程”或“订单”找回图片，在原图上看到匹配的文字位置。

面向 **Windows 11 x64**。不需要账号、API 密钥、Python 或独立显卡，中文 OCR 在本地 CPU 上运行。当前版本 **0.1.2 为测试版**，请先阅读[已知限制](docs/limitations.md)。

![资料库：搜索截图文字，查看原图与资料详情](docs/ui-review-images/0.1.2/ui-light.png)

*实际应用界面，展示的是合成测试资料。界面目前以中文为主；中英文文档不代表已有英文界面。*

## 三步开始

1. 安装后按 **Ctrl+Alt+S**，拖动框选。松开后还能移动选区、拖动八个调整点；需要时加画笔、箭头或马赛克。
2. 按 **Enter**，保存并复制，然后回到原来的应用。图片先落盘，文字在后台识别。
3. 从托盘打开资料库，搜截图里的文字。按 **空格**看原图，匹配到的文字行会高亮。

快捷键可以在设置里改成 `Alt+A`、`Ctrl+Shift+Q` 等组合；会检查常见系统组合和全局热键占用；注册失败时保留原快捷键。取消截图按 `Esc`，不会留下图片或资料记录。

## 可以做什么

| 你要做的事 | 软件里的做法 |
| --- | --- |
| 保存一张截图，顺手发给别人 | 框选、调整、保存并复制；也可仅复制或另存 PNG。 |
| 标出重点 | 画笔、箭头、空心矩形；六个常用色、RGB/Hex 调色盘，可调线宽。 |
| 修改刚画的标注 | 橡皮擦恢复原图像素，撤销、重做，清空也能撤销；马赛克块大小可调。 |
| 找到以前截过的内容 | 搜标题、备注、标签和识别文字；支持两字中文、中英混排、日期及编号。 |
| 整理已有图片 | 拖入或批量导入 PNG/JPEG，加标签、备注、星标；删除进入回收站，可恢复。 |
| 读小字或很长的图 | 原图缩放、平移、适应窗口和实际像素大小；搜索高亮跟随图片。 |
| 换一个保存位置 | 在设置中迁移资料库，或用 ZIP 备份恢复到新目录。 |

<details>
<summary>查看截图工具、调色盘和深色界面</summary>

![截图画笔与可调大小](docs/ui-review-images/0.1.2/capture-large-pen.png)

![自定义颜色：RGB 滑条、Hex 输入和颜色预览](docs/ui-review-images/0.1.2/palette-large.png)

![深色资料库](docs/ui-review-images/0.1.2/ui-dark.png)

更多图片和修改记录见[界面检查与自评](docs/ui-review.md)。

</details>

## 下载与数据

| 版本 | 适合谁 | 大小 |
| --- | --- | --- |
| [安装包](https://github.com/liugedragon/screenshot-box/releases/download/v0.1.2/ScreenshotBox-0.1.2-win-x64-setup.exe) | 日常使用；向导中可选安装位置，带卸载入口。 | 约 82 MiB |
| [ZIP](https://github.com/liugedragon/screenshot-box/releases/download/v0.1.2/ScreenshotBox-0.1.2-win-x64.zip) | 不想安装；解压完整目录后运行 `ScreenshotBox.exe`。 | 约 107 MiB |

两种包都带运行时和中文模型，**不要只拿走 exe**。发行包与校验文件在[下载页](https://github.com/liugedragon/screenshot-box/releases/tag/v0.1.2)。升级前从托盘退出旧版。

图片、文字和分类默认在 `%LOCALAPPDATA%\ScreenshotBox\library`，不在安装目录。可在设置里更改保存位置并迁移已有资料。应用不上传图片或文字，识别时不下载模型；卸载保留资料库。关闭主窗口后仍在托盘运行，彻底退出用托盘菜单。

## 先说明几个边界

- 自带运行时和 OCR 模型，发行包不是几 MB；模型首次加载会有延迟。
- OCR 可能识别错低清晰度、手写或复杂背景。高亮按整行显示，没有字符级定位。
- 马赛克是视觉像素化，不提供安全脱敏保证；橡皮擦掉马赛克会恢复原图。
- 当前没有滚动截图、录屏、云同步、自动更新或保存后的再次标注。应用和安装包未数字签名。
- 已在单屏 125% 缩放的 Windows 环境运行验证。混合 DPI 双屏、外部应用真实粘贴、全新机器物理断网流程仍待实测。

项目有 **42 项核心测试和 35 项标注像素检查**。Windows CI 检查编译、存储与合成图像行为；发行包另做了中文 OCR、搜索、备份、安装、重启恢复和卸载保留数据检查。两类证据的范围不同，详见[验证记录](docs/validation.md)。

## 文档

| 使用与开发 | 说明 |
| --- | --- |
| [使用说明](docs/usage.md) | 快捷键、标注、搜索、导入、备份。 |
| [构建](docs/build.md) · [安装与打包](docs/distribution.md) | 从源码运行或生成发行包。 |
| [架构](docs/architecture.md) · [存储与搜索](docs/storage.md) | 物理像素坐标、中文检索、后台任务和数据恢复。 |
| [设计记录](docs/design.md) · [界面自评](docs/ui-review.md) | 参考、样式和真实界面检查。 |
| [验证](docs/validation.md) · [限制](docs/limitations.md) | 测过什么，以及还没测什么。 |
| [第三方组件与模型](docs/third-party.md) | 来源、许可证、版本和校验值。 |

所有文档提供英文对应页，入口见 [English README](README.en.md)。

## 从源码构建

需要 Windows x64 和 **.NET SDK 10.0.401**。在仓库根目录运行：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/fetch-models.ps1
powershell -ExecutionPolicy Bypass -File scripts/build.ps1
powershell -ExecutionPolicy Bypass -File scripts/package.ps1 -Version 0.1.2
```

首次下载模型需要联网。依赖由 `packages.lock.json` 锁定；发行应用不需要 SDK。安装包构建还需要 Inno Setup，见[构建与安装](docs/distribution.md)。

## 一起改进

双屏与不同缩放测试、OCR 失败样本、文档纠错和小问题修复都很有帮助。提交图片请用合成或脱敏资料，写清版本、显示器缩放和复现步骤。[贡献说明](CONTRIBUTING.md)里有测试方法和提交建议。

项目下一步先处理真实使用中的问题，大规模检索性能、英文界面和文字标注暂列为候选，见[后续计划](ROADMAP.md)。

觉得有用，欢迎点个 **Star**；遇到问题也请留下反馈。

## 许可证与致谢

本项目新增代码和原创图标采用 [MIT](LICENSE)。WPF UI、RapidOcrNet、PaddleOCR 模型、ONNX Runtime、SQLite 等组件保留各自许可证；没有自行训练 OCR 模型。完整来源见[第三方说明](docs/third-party.md)。

界面与文档组织参考了 Eagle、ShareX、PowerToys、Flameshot 和 Flow Launcher，没有把这些项目的品牌资产、截图或文案当作本项目内容。[界面参考](docs/design.md) · [README 参考记录](docs/readme-references.md)
