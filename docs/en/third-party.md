# Third-party software and models

[English](../third-party.md) · [简体中文](../zh-CN/third-party.md)

The table lists the versions in `packages.lock.json`. Builds use `--locked-mode`. Releases include licenses, third-party notices, lock files, and a file SHA-256 manifest. UI and interaction references are listed in [design notes](../design.md).

| Shipped component | Locked version | License and source |
| --- | --- | --- |
| .NET / Windows Desktop Runtime | 10.0.12, win-x64 | MIT; license text and .NET third-party notices from the official runtime NuGet packages |
| WPF-UI, WPF-UI.Abstractions | 4.3.0 | MIT; bundled LICENSE.md and ThirdPartyNotices.txt; [upstream](https://github.com/lepoco/wpfui) |
| RapidOcrNet | 4.2.0 | Apache-2.0; upstream LICENSE.txt and NOTICE.txt included; [upstream](https://github.com/BobLd/RapidOcrNet) |
| Clipper2 | 2.0.0 | Boost Software License 1.0; License.txt from the official NuGet package; [upstream](https://github.com/AngusJohnson/Clipper2) |
| Microsoft.ML.OnnxRuntime, Managed | 1.29.0 | MIT; package license and ThirdPartyNotices.txt; [upstream](https://github.com/microsoft/onnxruntime) |
| SkiaSharp, SkiaSharp.NativeAssets.Win32 | 3.119.1 | MIT; package license included. Native Skia and other components have their own terms in THIRD-PARTY-NOTICES.txt; [upstream](https://github.com/mono/SkiaSharp) |
| Microsoft.Data.Sqlite, Core | 10.0.12 | MIT; official source license; [upstream](https://github.com/dotnet/efcore) |
| SQLitePCLRaw.bundle_e_sqlite3, core, provider.e_sqlite3, lib.e_sqlite3 | 2.1.12 | NuGet declares Apache-2.0, with upstream text included. The SQLite engine is [public domain](https://www.sqlite.org/copyright.html). |
| System.Numerics.Tensors | 9.0.0 | MIT; bundled LICENSE.TXT and THIRD-PARTY-NOTICES.TXT |
| Microsoft Visual C++ Runtime, app-local x64 CRT | 14.51.36247.0 | Proprietary Microsoft redistributable. Extracted from the [official signed Redistributable](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist); Microsoft terms included. |
| PP-OCRv5 detection and text-line orientation models | Default v5 files in RapidOcrNet 4.2.0 | PaddleOCR / RapidOCR upstream Apache-2.0 terms included; [PaddleOCR](https://github.com/PaddlePaddle/PaddleOCR), [RapidOCR](https://github.com/RapidAI/RapidOCR) |
| PP-OCRv5 Chinese recognition model and dictionary | RapidOCR v3.9.2 model release | Upstream Apache-2.0. Exact download URLs, sizes, and SHA-256 hashes are pinned in [`models/chinese/sources.json`](../../models/chinese/sources.json) and checked during packaging. |

`SkiaSharp.NativeAssets.Linux.NoDependencies` and `SkiaSharp.NativeAssets.macOS` 3.119.1 are transitive RapidOcrNet / SkiaSharp dependencies. They remain in lock files for reproducibility, but the Windows release does not ship their Linux or macOS binaries.

RapidOcrNet's default Latin recognition model and dictionary are unused. Packaging removes those two files, retaining the detection and orientation models and the separately supplied Chinese recognition model.

## License and model provenance

[`licenses/sources.json`](../../licenses/sources.json) records the official source and SHA-256 of each license text. NuGet license files come from local packages restored against official NuGet content hashes; their source links point to the exact package versions. Other texts come from pinned upstream commits, tags, or the official Apache website. The downloaded PaddleOCR license text is pinned by its content hash.

The Chinese recognition assets are pinned as follows; the complete URLs are in [`sources.json`](../../models/chinese/sources.json):

| File | Bytes | SHA-256 |
| --- | ---: | --- |
| `ch_PP-OCRv5_rec_mobile.onnx` | 16,631,306 | `5825fc7ebf84ae7a412be049820b4d86d77620f204a041697b0494669b1742c5` |
| `ppocrv5_dict.txt` | 74,012 | `d1979e9f794c464c0d2e0b70a7fe14dd978e9dc644c0e71f14158cdf8342af1b` |

The table indexes sources; the original license texts contain the applicable terms. Dependency or model updates require a fresh review of lock files, licenses, notices, and the release checksum manifest.

## Development-only tools

Microsoft.NET.Test.Sdk 17.14.1, xUnit 2.9.3, and xunit.runner.visualstudio 3.1.1 are used only by test projects and are not included in the installer.

Inno Setup **7.1.0** is the local installer compiler; the development tool itself is not distributed to users. Its terms are in the [official license](https://github.com/jrsoftware/issrc/blob/is-7_1_0/LICENSE.TXT). The downloaded compiler's Windows Authenticode signature and publisher, **Pyrsys B.V.**, were verified. ScreenshotBox's installer is currently unsigned.

## Visual C++ Runtime redistribution

Redistributing the Visual C++ Runtime requires a valid Visual Studio license. The build machine has Visual Studio Community 2022. Microsoft's [redistribution list](https://learn.microsoft.com/en-us/visualstudio/releases/2022/redistribution) and [deployment documentation](https://learn.microsoft.com/en-us/cpp/windows/choosing-a-deployment-method) describe the applicable terms for shipping unmodified CRT files alongside an application.

[`native/vc-runtime-x64/sources.json`](../../native/vc-runtime-x64/sources.json) pins the official download and hashes for the installer and each DLL. Packaging checks the Microsoft digital signature of each DLL. Only x64 DLLs are included; no system-wide CRT installer is run, so administrator rights are not required.

Microsoft recommends central deployment for system updates. With app-local deployment, ScreenshotBox must maintain these runtime files through application updates. The repository's MIT license does not replace Microsoft's CRT terms.
