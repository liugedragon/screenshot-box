# 第三方软件与模型

简体中文 · [English](../third-party.md)

下表列出 `packages.lock.json` 中的版本。构建使用 `--locked-mode`；发布包包含锁文件、许可证、第三方通知和文件 SHA-256 清单。界面与交互参考见[设计说明](design.md)。

| 发布组件 | 锁定版本 | 许可证与来源 |
| --- | --- | --- |
| .NET / Windows Desktop Runtime | 10.0.12，win-x64 | MIT，官方运行时 NuGet 包内正文及 .NET 第三方通知 |
| WPF-UI、WPF-UI.Abstractions | 4.3.0 | MIT，包内 LICENSE.md 与 ThirdPartyNotices.txt；[官方项目](https://github.com/lepoco/wpfui) |
| RapidOcrNet | 4.2.0 | Apache-2.0，包含上游 LICENSE.txt / NOTICE.txt；[官方项目](https://github.com/BobLd/RapidOcrNet) |
| Clipper2 | 2.0.0 | Boost Software License 1.0，官方 NuGet 包 License.txt；[官方项目](https://github.com/AngusJohnson/Clipper2) |
| Microsoft.ML.OnnxRuntime、Managed | 1.29.0 | MIT，包内正文和 ThirdPartyNotices.txt；[官方项目](https://github.com/microsoft/onnxruntime) |
| SkiaSharp、SkiaSharp.NativeAssets.Win32 | 3.119.1 | MIT，包内正文；原生 Skia 等的许可证见包内 THIRD-PARTY-NOTICES.txt；[官方项目](https://github.com/mono/SkiaSharp) |
| Microsoft.Data.Sqlite、Core | 10.0.12 | MIT，官方源码正文；[官方项目](https://github.com/dotnet/efcore) |
| SQLitePCLRaw.bundle_e_sqlite3、core、provider.e_sqlite3、lib.e_sqlite3 | 2.1.12 | NuGet 声明 Apache-2.0，附上游正文；其中 SQLite 引擎为[公共领域软件](https://www.sqlite.org/copyright.html) |
| System.Numerics.Tensors | 9.0.0 | MIT，包内 LICENSE.TXT 与 THIRD-PARTY-NOTICES.TXT |
| Microsoft Visual C++ Runtime，应用目录 x64 CRT | 14.51.36247.0 | 微软专有可再分发组件；由[官方已签名 Redistributable](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist)提取，微软条款随包 |
| PP-OCRv5 检测、文字方向模型 | RapidOcrNet 4.2.0 包内 v5 默认文件 | PaddleOCR / RapidOCR 上游 Apache-2.0，正文随包；[PaddleOCR](https://github.com/PaddlePaddle/PaddleOCR)、[RapidOCR](https://github.com/RapidAI/RapidOCR) |
| PP-OCRv5 中文识别模型及字典 | RapidOCR v3.9.2 模型发布 | 上游 Apache-2.0；下载地址、大小和 SHA-256 见 [`models/chinese/sources.json`](../../models/chinese/sources.json)，并在打包时核对 |

`SkiaSharp.NativeAssets.Linux.NoDependencies` 与 `SkiaSharp.NativeAssets.macOS` 3.119.1 是 RapidOcrNet / SkiaSharp 的传递依赖，保留在锁文件以便复现；Windows 发布阶段只保留 win-x64 原生文件，不分发 Linux/macOS 二进制。RapidOcrNet 的默认拉丁文识别模型与字典不使用，打包时删除这两个文件，保留检测和方向模型以及单独提供的中文识别模型。

## 许可证与模型来源

[`licenses/sources.json`](../../licenses/sources.json)记录各许可证正文的官方来源和 SHA-256。NuGet 许可证提取自按官方内容哈希恢复的对应版本包；其他正文来自上游固定提交、标签或 Apache 官网。PaddleOCR 许可证正文也使用内容哈希固定。

中文识别模型和字典的完整下载地址见 [`sources.json`](../../models/chinese/sources.json)：

| 文件 | 字节数 | SHA-256 |
| --- | ---: | --- |
| `ch_PP-OCRv5_rec_mobile.onnx` | 16,631,306 | `5825fc7ebf84ae7a412be049820b4d86d77620f204a041697b0494669b1742c5` |
| `ppocrv5_dict.txt` | 74,012 | `d1979e9f794c464c0d2e0b70a7fe14dd978e9dc644c0e71f14158cdf8342af1b` |

许可证表用于来源追溯，具体条款以随附原文为准。更新依赖或模型时，需核对锁文件、许可证及第三方通知，并重新生成校验清单。

## 开发工具

Microsoft.NET.Test.Sdk 17.14.1、xUnit 2.9.3、xunit.runner.visualstudio 3.1.1 仅用于测试项目，不进入安装包。

安装程序使用 Inno Setup **7.1.0** 编译。工具本身不随应用分发，其条款见[官方许可证](https://github.com/jrsoftware/issrc/blob/is-7_1_0/LICENSE.TXT)。下载的编译器已通过 Authenticode 签名检查，发布者为 **Pyrsys B.V.**。ScreenshotBox 的安装程序当前未签名。

## Visual C++ Runtime 再分发

再分发 Visual C++ Runtime 需要有效的 Visual Studio 许可。构建电脑安装 Visual Studio Community 2022。适用条款见微软[再分发清单](https://learn.microsoft.com/en-us/visualstudio/releases/2022/redistribution)和[部署说明](https://learn.microsoft.com/en-us/cpp/windows/choosing-a-deployment-method)。CRT 文件未经修改，随应用在本地目录部署。

[`native/vc-runtime-x64/sources.json`](../../native/vc-runtime-x64/sources.json)记录官方下载地址、安装程序及各 DLL 的哈希。打包时逐个验证微软数字签名。只包含 x64 DLL，不执行系统级 CRT 安装，无需管理员权限。

微软建议集中部署以便系统更新。本项目使用应用本地部署，运行时文件随应用更新维护；仓库的 MIT 许可证不替代微软 CRT 条款。
