# 从源码构建

简体中文 · [English](../build.md)

## 环境

- Windows x64；主要开发与测试环境为 Windows 11。
- .NET SDK **10.0.401**。
- Windows PowerShell 5.1 或 PowerShell 7。

在仓库根目录运行：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/fetch-models.ps1
powershell -ExecutionPolicy Bypass -File scripts/build.ps1
powershell -ExecutionPolicy Bypass -File scripts/package.ps1 -Version 0.1.3
```

脚本优先使用 `.tools/dotnet/dotnet.exe`，否则使用本机 SDK。[`global.json`](../../global.json) 固定 SDK 补丁范围，各项目的 `packages.lock.json` 锁定直接与传递依赖，恢复使用 `--locked-mode`。

## 模型与发布

[`fetch-models.ps1`](../../scripts/fetch-models.ps1) 在开发阶段下载中文识别模型与字典，核对字节数和 SHA-256 后替换文件。来源和校验值在 [`models/chinese/sources.json`](../../models/chinese/sources.json)。

发行应用从旁边的 `models` 目录读取模型，OCR 不需要联网。发布目标为 **win-x64 CPU**，包含 .NET 运行时、检测、方向和中文识别模型。WPF 资源、ONNX Runtime 和 SkiaSharp 依赖完整目录，因此关闭单文件发布和裁剪。安装包构建见[打包与安装](distribution.md)。

## 测试

[`build.ps1`](../../scripts/build.ps1) 检查锁定依赖、编译应用并运行 Core 测试。截图标注使用独立的[合成像素测试项目](../../tests/ScreenshotBox.Capture.Probe/README.zh-CN.md)。[Windows CI](../../.github/workflows/windows.yml) 运行这两类检查。

发行 exe 可运行集成自测，使用独立目录和合成资料：

```powershell
.\artifacts\ScreenshotBox-0.1.3-win-x64\ScreenshotBox.exe --self-test --data-dir E:\temp\ScreenshotBox-test
```

结果写入该目录的 `self-test.json`。检查包括中文 OCR、源像素行框、中文字面搜索、剪贴板读回和备份恢复。外部应用粘贴、多屏拖动和物理断网需单独测试，结果见[验证记录](validation.md)。

模型、构建工具和生成文件由 `.gitignore` 排除。
