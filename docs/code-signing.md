# Code signing policy

English · [简体中文](zh-CN/code-signing.md)

The published Windows 0.1.4 installer and application are **unsigned**. No certificate provider has approved ScreenshotBox. Release files and SHA-256 checksums are listed on the [release page](https://github.com/liugedragon/screenshot-box/releases/tag/v0.1.4); a matching checksum confirms file identity, not safety. See [release checks](distribution.md#release-checks-and-signing) for verification steps.

The repository owner maintains the release workflow and decides which versions to publish. For this single-maintainer project, [liugedragon](https://github.com/liugedragon) is the source committer, reviewer of outside contributions, and release/signing approver. Changes from other contributors require review before merging. Any future code-signing request must be approved by the repository owner. Signed releases, if introduced, will use GitHub-hosted Windows runners, a recorded source commit, locked dependencies, and verified OCR-model hashes. The [candidate workflow](../.github/workflows/windows-candidate.yml) currently produces **unsigned** build artifacts for inspection; it does not publish a release.

ScreenshotBox keeps screenshots, metadata, and recognized text in the selected local library. The application does not send them to a server or download OCR models during use. Users can explicitly export images or backups. The installer selects a per-user sign-in startup task by default; it can be cleared during installation or disabled in Task Manager. The installer creates uninstall entries, and [installation and data locations](usage.md) are documented in the user guide. Uninstalling leaves the library in place.

If a future release is signed, its release notes will name the certificate publisher and provide signature-check instructions. A valid signature identifies the publisher and protects file integrity; it does not guarantee that a new download will be free of a [Microsoft SmartScreen reputation warning](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation).
