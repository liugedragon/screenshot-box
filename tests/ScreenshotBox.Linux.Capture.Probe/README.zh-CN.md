# Linux 截图检查

[English](README.md)

在 Linux x64 的 X11 显示环境中使用 .NET 10 SDK 运行：

```bash
DISPLAY=:0 dotnet run --project tests/ScreenshotBox.Linux.Capture.Probe
```

测试短暂显示自己的合成标记窗口和截图遮罩，采集真实 X11 root 像素，查询标记窗口的实际位置，并验证截图前隐藏应用窗口。测试期间不要在同一显示服务上运行其他界面测试。

检查包括反向框选、八点调整、移动边界、逐像素裁剪、可调画笔宽度、箭头、矩形、马赛克、整条标注擦除和撤销。两个独立 X11 连接检查快捷键占用及修改失败后保留原注册。真实 Avalonia 控件检查 Enter、Esc、完成操作和工具条位置。

结果和合成英文界面图片保存在 `artifacts/linux-capture-validation/`。可通过 `SCREENSHOTBOX_CAPTURE_PROBE_OUTPUT` 指定其他目录。测试不创建资料记录，也不将真实桌面画面保存为文件。

这些检查覆盖组件和原生接口，不发送实际键盘组合，不验证双屏混合缩放或 Wayland。在 WSL 配合外部 X11 服务器时，截图与快捷键仅覆盖该服务器中的 X11 客户端。
