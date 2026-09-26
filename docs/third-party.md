# 第三方软件与模型

简体中文 · [English](en/third-party.md)

下表对应当前 `packages.lock.json` 的实际解析版本。构建使用 `--locked-mode`；发布包带有锁文件、许可证正文、第三方通知以及每个文件的 SHA-256。项目没有复制 ShareX 源码；截图交互的参考不构成代码依赖。

| 发布组件 | 锁定版本 | 许可证与来源 |
| --- | --- | --- |
| .NET / Windows Desktop Runtime | 10.0.12，win-x64 | MIT，官方运行时 NuGet 包内正文及 .NET 第三方通知 |
| WPF-UI、WPF-UI.Abstractions | 4.3.0 | MIT，包内 LICENSE.md 与 ThirdPartyNotices.txt；[官方项目](https://github.com/lepoco/wpfui) |
| RapidOcrNet | 4.2.0 | Apache-2.0，包含上游 LICENSE.txt / NOTICE.txt；[官方项目](https://github.com/BobLd/RapidOcrNet) |
| Clipper2 | 2.0.0 | Boost Software License 1.0，官方 NuGet 包 License.txt；[官方项目](https://github.com/AngusJohnson/Clipper2) |
| Microsoft.ML.OnnxRuntime、Managed | 1.29.0 | MIT，包内正文和 ThirdPartyNotices.txt；[官方项目](https://github.com/microsoft/onnxruntime) |
| SkiaSharp、SkiaSharp.NativeAssets.Win32 | 3.119.1 | MIT，包内正文；原生 Skia 等的许可证见包内 THIRD-PARTY-NOTICES.txt；[官方项目](https://github.com/mono/SkiaSharp) |
| Microsoft.Data.Sqlite、Core | 10.0.12 | MIT，官方源码正文；[官方项目](https://github.com/dotnet/efcore) |
| SQLitePCLRaw.bundle_e_sqlite3、core、provider.e_sqlite3、lib.e_sqlite3 | 2.1.12 | NuGet 声明 Apache-2.0，附上游正文；其中 SQLite 引擎为[公共领域软件](https://www.sqlite.org/copyright.html)，不能把引擎本身说成 Apache 授权 |
| System.Numerics.Tensors | 9.0.0 | MIT，包内 LICENSE.TXT 与 THIRD-PARTY-NOTICES.TXT |
| Microsoft Visual C++ Runtime，应用目录 x64 CRT | 14.51.36247.0 | 微软专有可再分发组件，**不是 MIT / Apache 开源组件**；由[官方已签名 Redistributable](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist)提取，微软条款随包 |
| PP-OCRv5 检测、文字方向模型 | RapidOcrNet 4.2.0 包内 v5 默认文件 | PaddleOCR / RapidOCR 上游 Apache-2.0，正文随包；[PaddleOCR](https://github.com/PaddlePaddle/PaddleOCR)、[RapidOCR](https://github.com/RapidAI/RapidOCR) |
| PP-OCRv5 中文识别模型及字典 | RapidOCR v3.9.2 模型发布 | 上游 Apache-2.0；具体下载地址、大小、SHA-256 固定在 `models/chinese/sources.json`，并在打包时核对 |

`SkiaSharp.NativeAssets.Linux.NoDependencies` 与 `SkiaSharp.NativeAssets.macOS` 3.119.1 是 RapidOcrNet / SkiaSharp 的传递依赖，保留在锁文件以便复现；Windows 发布阶段只保留 win-x64 原生文件，不分发 Linux/macOS 二进制。RapidOcrNet 的默认拉丁文识别模型与字典不使用，打包时仅删除这两个明确命名的文件，保留检测和方向模型以及单独提供的中文识别模型。

`licenses/sources.json` 记录每份许可证正文的官方来源和 SHA-256。来自 NuGet 包的正文提取自本机已按官方 NuGet 内容哈希恢复的包，来源链接直接指向对应版本官方包；外部正文来自项目官方固定提交、标签或 Apache 官方网站。PaddleOCR 许可证正文下载时的文件内容由 SHA-256 固定。

开发测试依赖是 Microsoft.NET.Test.Sdk 17.14.1、xUnit 2.9.3、xunit.runner.visualstudio 3.1.1；它们只在测试项目中使用，不进入安装包。Inno Setup 7.1.0 是本地安装程序编译工具，未把开发工具本身分发给用户；其使用规则见[官方许可证](https://github.com/jrsoftware/issrc/blob/is-7_1_0/LICENSE.TXT)。工具下载已核验 Windows Authenticode 签名和发布者 Pyrsys B.V.。由本项目生成的安装程序当前尚未购买签名证书，不能把“编译工具有签名”宣传成“ScreenshotBox 安装包有签名”。

每次更新依赖或模型，都要重新核对锁文件、许可证与第三方通知并生成包内校验清单。许可证表用于追溯，不代替随附原始许可证条款。

Visual C++ Runtime 的再分发以拥有有效 Visual Studio 许可为前提。本次构建电脑已有 Visual Studio Community 2022。微软[再分发清单](https://learn.microsoft.com/en-us/visualstudio/releases/2022/redistribution)和[本地部署说明](https://learn.microsoft.com/en-us/cpp/windows/choosing-a-deployment-method)允许按相关条款在应用目录分发未修改的 CRT 文件。`native/vc-runtime-x64/sources.json` 固定官方下载、整体安装程序与每个 DLL 的哈希，打包时逐个验证微软数字签名。只保留 x64 DLL，不运行系统级 CRT 安装；因此无需管理员权限。微软推荐集中安装以便系统更新，本项目采取本地部署后应随应用更新维护这些运行时文件。源码仓库的 MIT 许可证不替代 CRT 的微软条款。
