# 截图标注合成像素探针

简体中文 · [English](README.en.md)

仅在Windows运行的独立程序，把应用原生截图源码链接到单独项目。使用内存中的合成`Bgr32`图案验证真实截图会话及`Accept`导出路径。它不启动ScreenshotBox、不显示遮罩、不采集桌面、不写图片文件、不改变剪贴板、不注册热键，也不触发屏幕变化。

在Windows的仓库根目录使用.NET 10运行：

```powershell
dotnet run --project tests/ScreenshotBox.Capture.Probe/ScreenshotBox.Capture.Probe.csproj
```

使用仓库本地SDK时：

```powershell
.\.tools\dotnet\dotnet.exe run --project tests/ScreenshotBox.Capture.Probe/ScreenshotBox.Capture.Probe.csproj
```

退出码`0`表示全部通过，`1`表示至少一项失败。逐项打印结果并给出汇总，构建输出保留在此测试项目自己的`bin`和`obj`目录。

当前实现的实际运行结果：

```text
RESULT: 35 passed, 0 failed. Synthetic pixels only; no desktop capture, files, clipboard, hotkeys, or overlay windows.
```

这是记录过的Windows运行结果，不保证后续改动也通过；审查时请在确切源码提交上重新运行。

覆盖正负桌面原点、裁剪对齐、六种预设笔色、小数与最大线宽、箭头头部、反向矩形、6及32像素马赛克平均色（含边缘不完整块）、用橡皮恢复各类标注下的原图、撤销/重做、可撤销清空、历史分支和运行期间工具偏好的保持与边界。比较RGB前统一源图`Bgr32`和输出`Pbgra32`，源图不用的第四字节刻意变化，以发现错误的alpha假设。

反射只用于构造私有截图会话、选择私有模式枚举和读取完成任务。绘图、工具切换、历史和导出调用真实会话方法；每个中间导出都在新会话重放操作。预期原像素和马赛克块平均值直接来自合成图案，不依赖应用渲染器计算。

此探针验证标注像素和导出，不验证工具栏位置、鼠标输入路由、热键注册、物理插拔屏幕、混合DPI、真实桌面截图、OCR或文件/剪贴板持久化。
