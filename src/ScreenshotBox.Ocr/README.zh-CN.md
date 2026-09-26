# ScreenshotBox.Ocr

简体中文 · [English](README.md)

基于 RapidOcrNet 4.2.0 和 PP-OCRv5 中文移动模型的共享 CPU OCR 队列。Windows 与 Linux 使用相同的字典、模型版本和数据库任务格式。

`Enqueue` 对待处理的图片 ID 去重。单个读取器复用同一引擎，使用两个推理线程，空闲 90 秒后释放模型。结果包含识别文字、原图行框、置信信息和模型版本。数据库只接受任务代数及有效图片状态仍匹配的结果。

退出或重启前调用 `DisposeAsync`。中断的识别任务在资料库重新打开时恢复为待处理。`RecognizeAsync` 是独立探针使用的底层入口，不应与活动工作线程并发调用。

桌面项目保留直接 RapidOcrNet 引用，因为模型复制目标不具备传递性。中文识别模型和字典记录在 [sources.json](../../models/chinese/sources.json)，可通过 `scripts/fetch-models.py` 或 Windows PowerShell 脚本下载。许可与上游职责见[第三方软件](../../docs/zh-CN/third-party.md)。
