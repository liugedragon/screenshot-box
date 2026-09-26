# Windows构建

Windows11 x64，.NET SDK10.0.401。终端位于项目根目录，运行README中的构建脚本。`global.json`固定SDK补丁范围；每个项目的packages.lock.json锁定传递依赖。构建失败时不要删除锁文件盲目升版本。

`scripts/fetch-models.ps1`只在开发构建时联网。发行应用读取旁边models目录，不下载模型，不需要联网。模型URL、字节数与SHA256见models/chinese/sources.json。脚本核验后才替换文件。

App不启用单文件发布或裁剪，因为WPF资源、ONNX Runtime和SkiaSharp需完整依赖。仅发布win-x64 CPU资产。ZIP中包含.NET runtime、中文移动识别模型、检测和方向模型；不要搬走exe后删除其他文件。

自动测试：`scripts/build.ps1`执行全部Core/几何测试。

发行包集成自测（使用合成内容，不会导入真实截图）：

```powershell
.\artifacts\ScreenshotBox-0.1.2-win-x64\ScreenshotBox.exe --self-test --data-dir E:\temp\ScreenshotBox验收
```

完成后读取该目录self-test.json。包括真实中文OCR、源像素文字行框、两字词和符号查询、剪贴板原图以及备份到新目录再打开。注意这项自测不能代替常见外部应用实际粘贴、双屏拖动和物理断网测试。

安装程序和便携ZIP都会保留用户数据。卸载应用不自动删除资料库。工具与构建输出被.gitignore排除，不进入源码仓库。
