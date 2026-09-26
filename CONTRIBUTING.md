# Contributing

English · [简体中文](CONTRIBUTING.zh-CN.md)

## Report a bug

Check the [known limitations](docs/limitations.md), then use an [issue template](https://github.com/liugedragon/screenshot-box/issues/new/choose). Include:

- App and Windows versions, and whether you used the installer or ZIP.
- Monitor count, resolution, scaling, and relative position of each display.
- Steps to reproduce, expected and actual behavior, and the exact error message.
- A synthetic or redacted test image where relevant.

Do not upload private screenshots, databases, recognized personal content, keys, or account details.

## Suggest a feature

Describe the use case, the problem, and your current workaround. Open an issue before starting a larger feature or adding a dependency. Existing plans are in [ROADMAP](ROADMAP.md).

## Submit a change

1. Fork the repository and create a branch for one focused change.
2. Follow the [build guide](docs/build.md) to prepare Windows x64 and .NET SDK 10.0.401.
3. Run `powershell -ExecutionPolicy Bypass -File scripts/build.ps1`. For capture or annotation changes, also run the [capture pixel tests](tests/ScreenshotBox.Capture.Probe/README.md).
4. Open a PR describing the problem, new behavior, checks performed, and untested conditions. Include window screenshots with synthetic data for UI changes.

Keep dependency lock files. New components or models need a source, version, license, and distribution method. Do not commit models, user data, or build outputs. Update both languages when changing documentation or UI text. New controls should follow the [design guide](docs/design.md).

Add regression coverage for storage, coordinate, search, or OCR state changes. Documentation-only changes need link and formatting checks.

## License

By contributing, you agree to release your contribution under this project's MIT license. Third-party material must retain its original license and notices.
