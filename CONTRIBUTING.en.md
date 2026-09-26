# Contributing to ScreenshotBox

[简体中文](CONTRIBUTING.md) · English

You do not have to write code. Reproducing a bug, trying a different display configuration, or fixing an unclear instruction all help.

## Report a problem

Check the [known limitations](docs/en/limitations.md), then choose a template in [Issues](https://github.com/liugedragon/screenshot-box/issues/new/choose). Include:

- App and Windows versions; installer or ZIP.
- Monitor count, resolutions, and scaling for each display.
- Steps, expected behavior, and what actually happened.
- A synthetic or redacted image that reproduces it, plus the exact error message if any.

Do not submit real user screenshots, databases, recognized personal content, keys, or account details. A few lines of invented text make a useful test image. Passing single-monitor automation does not rule out a real multiple-monitor bug.

## Suggest a change

Describe what you were doing, where it failed, and your current workaround before proposing a solution. Discuss larger features or new dependencies in an issue first.

Useful work includes mixed-DPI monitor checks, missing originals, Chinese directory names and backup recovery, difficult OCR samples, and documentation fixes. The [roadmap](ROADMAP.en.md) has no promised dates.

## Change the code

1. Fork the repository and create a branch for one focused change.
2. Follow the [build guide](docs/en/build.md): Windows x64 and .NET SDK 10.0.401.
3. Run `powershell -ExecutionPolicy Bypass -File scripts/build.ps1`. For capture coordinates or annotation changes, also run the [capture pixel probe](tests/ScreenshotBox.Capture.Probe/README.en.md).
4. Open a PR explaining the trigger, new behavior, checks performed, and conditions you could not test. Use actual window images with synthetic data for UI changes.

Keep dependency lock files. New components or models need a source, version, license, and distribution plan. Do not commit models, user data, or build outputs. Keep Chinese and English documentation in sync; flag technical translations you cannot confirm.

Tests should check real constraints: coordinates, literal Chinese queries, stale OCR tasks, and restored data. Avoid tests that repeat the implementation for a minor wording change. Do not describe programmatic checks as manual acceptance tests.

## Documentation and UI

Explain the task first, then the operation. Prefer “The image is saved first; text recognition follows” to claims about an ultimate all-in-one tool. Put unimplemented features in the roadmap and mark untested conditions explicitly.

Use one accent color, system fonts, and clear states. New tools should not interrupt the main capture-and-save flow. See the [design notes](docs/en/design.md).

By contributing, you agree to license your contribution under this project's MIT license. Third-party material must retain its own terms.
