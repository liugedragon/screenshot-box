# README references

[中文](../readme-references.md) · English

Reviewed on 2026-09-26. We studied the structure and writing of the official READMEs below. We did not copy their descriptions, screenshots, icons, or branding. Clear documentation can make an application easier to try; it does not guarantee GitHub stars.

## Projects reviewed

| Official README | What we observed | What ScreenshotBox adopts |
| --- | --- | --- |
| [ShareX](https://github.com/ShareX/ShareX/blob/develop/README.md) | Product screenshots, release/build status, and sections organized around capture, editing, and subsequent actions. | Explain the capture → save → recognize → retrieve workflow and list shipped features. |
| [Flameshot](https://github.com/flameshot-org/flameshot/blob/master/README.md) | An early preview, shortcut tables, and separate usage, installation, and compilation sections. | Show capture editing, distinguish global from window shortcuts, and put downloads before build instructions. |
| [PowerToys](https://github.com/microsoft/PowerToys/blob/main/README.md) | Direct installation/documentation/release links, package-selection guidance, and separate developer documentation. | Offer the Windows x64 installer and ZIP upfront; link detailed engineering material from a documentation index. |
| [Flow Launcher](https://github.com/Flow-Launcher/Flow.Launcher/blob/master/README.md) | Installer and portable downloads together, illustrated features, and explicit feedback/contribution routes. | Make the application understandable before asking readers to install it; make useful feedback easy to submit. |

## Rules for our landing page

- Chinese is the default; English has its own complete page. English documentation does not imply an English application interface.
- Start with a concrete need: finding a saved screenshot by its text a few days later. Avoid generic superlatives.
- Put downloads, real application screenshots, and the shortest workflow first. Demo contents are synthetic.
- Separate shipped features, future work, and unverified conditions.
- Use verifiable release, build, and license badges. No fabricated download counts, sponsors, testimonials, or star-history decoration.

## Helping interested users find the project

Use an accurate repository description and relevant topics such as Windows, screenshot, OCR, image-annotation, and local-first. Describe a local screenshot library with Chinese OCR rather than a general-purpose AI platform.

One modest invitation is enough: if the application helps someone find a screenshot, a star is welcome; issues are welcome too. Contributions can include documentation, translations, reproducible bug reports, and permitted, sanitized test samples. Sharing real user materials requires permission.

These are our editorial choices, not endorsements from the referenced projects. Posting to external communities requires a separate choice of channels and prepared content; this work does not send promotional messages.

## What we deliberately leave out

ScreenshotBox does not currently ship cloud uploads, recording, a plugin store, cross-platform packages, a multilingual application interface, or an established sponsorship program. Those sections do not belong in its README. We link existing GitHub downloads rather than inventing store or package-manager installation commands. Upstream branding and feature screenshots are not project assets.

Prepared introductions and sharing notes are in [the project introduction](community.md). This documentation update also adjusts the repository description, download homepage, and relevant topics.
