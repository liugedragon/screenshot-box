using System.Security.Cryptography;
using System.Text.Json;
using ScreenshotBox.Core;
using ScreenshotBox.Linux;
using ScreenshotBox.Ocr;
using SkiaSharp;

if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Run this probe on Linux.");
if (args.Length != 1) throw new ArgumentException("Supply one new, empty output directory.");
var root = Path.GetFullPath(args[0]);
if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
    throw new IOException("The probe output directory must be empty.");
Directory.CreateDirectory(root);
var checks = new Dictionary<string, bool>();
void Check(string name, bool value)
{
    checks[name] = value;
    if (!value) throw new InvalidDataException("Failed check: " + name);
}
using var store = new LibraryStore(Path.Combine(root, "资料 library"));
await store.InitializeAsync();
var images = new LinuxImageLibrary(store);
var source = Path.Combine(root, "中文 mixed.png");
using (var bitmap = new SKBitmap(1000, 450))
using (var canvas = new SKCanvas(bitmap))
using (var face = SKTypeface.FromFile("/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc")
    ?? SKTypeface.FromFamilyName("Noto Sans CJK SC"))
using (var font = new SKFont(face, 42))
using (var paint = new SKPaint { IsAntialias = true, Color = new SKColor(0x21, 0x2B, 0x36) })
{
    canvas.Clear(new SKColor(0xF5, 0xF7, 0xFB));
    using var accent = new SKPaint { Color = new SKColor(0x17, 0x69, 0xC2), IsAntialias = true };
    canvas.DrawRoundRect(new SKRect(24, 20, 976, 90), 10, 10, accent);
    paint.Color = SKColors.White;
    canvas.DrawText("截图资料盒 ScreenshotBox", 42, 72, font, paint);
    paint.Color = new SKColor(0x21, 0x2B, 0x36);
    canvas.DrawText("课程安排：计算机网络 B204", 42, 156, font, paint);
    canvas.DrawText("订单编号 SB-20260927-001", 42, 232, font, paint);
    canvas.DrawText("保修日期 2026-09-27", 42, 308, font, paint);
    canvas.DrawText("英文 English / 中文识别", 42, 384, font, paint);
    LinuxImageLibrary.WritePngAtomic(bitmap, source);
}
var item = await images.AddFileAsync(source);
Check("originalBeforeDatabase", File.Exists(store.ResolvePath(item.ImagePath)));
Check("physicalImageSize", item.Width == 1000 && item.Height == 450);
using (var thumbnail = SKBitmap.Decode(store.ResolvePath(item.ThumbnailPath)))
{
    Check("thumbnailProportional", thumbnail is { Width: 320, Height: 144 });
}
var fromBytes = await images.AddPngAsync(await File.ReadAllBytesAsync(source), "Image bytes");
Check("pngByteImport", fromBytes.Width == 1000 && fromBytes.Height == 450);
int filesBefore = Directory.GetFiles(Path.Combine(store.RootPath, "images")).Length;
try { await images.AddPngAsync([1, 2, 3]); Check("invalidImageRejected", false); }
catch (InvalidDataException) { Check("invalidImageRejected", true); }
Check("invalidImportNoFiles", Directory.GetFiles(Path.Combine(store.RootPath, "images")).Length == filesBefore);
var export = Path.Combine(root, "export.png");
LinuxImageLibrary.ExportAtomic(store.ResolvePath(item.ImagePath), export);
Check("exportPreservesImage", SHA256.HashData(File.ReadAllBytes(export)).SequenceEqual(SHA256.HashData(File.ReadAllBytes(store.ResolvePath(item.ImagePath)))));
var beforeExport = SHA256.HashData(File.ReadAllBytes(export));
try { LinuxImageLibrary.ExportAtomic(Path.Combine(root, "missing.png"), export); Check("failedExportThrows", false); }
catch (FileNotFoundException) { Check("failedExportThrows", true); }
Check("failedExportPreservesDestination", SHA256.HashData(File.ReadAllBytes(export)).SequenceEqual(beforeExport));
Check("atomicTempsRemoved", !Directory.GetFiles(root, "*.tmp").Any());

await using (var queue = new OcrQueue(store))
{
    queue.Enqueue(item.Id);
    var deadline = DateTime.UtcNow.AddMinutes(3);
    ScreenshotItem? recognized;
    do
    {
        await Task.Delay(100);
        recognized = await store.GetAsync(item.Id);
    } while (recognized?.OcrStatus is "Pending" or "Processing" && DateTime.UtcNow < deadline);
    Check("cpuChineseOcrReady", recognized?.OcrStatus == "Ready");
    Check("chineseCourse", recognized!.OcrText.Contains("课程"));
    Check("chineseOrder", recognized.OcrText.Contains("订单"));
    Check("chineseWarranty", recognized.OcrText.Contains("保修"));
    Check("mixedEnglish", recognized.OcrText.Contains("English", StringComparison.OrdinalIgnoreCase));
    Check("lineBoxes", recognized.OcrBlocks.Count >= 5 && recognized.OcrBlocks.All(b => b.X >= 0 && b.Y >= 0 && b.Width > 0 && b.Height > 0 && b.X + b.Width <= 1001 && b.Y + b.Height <= 451));
    Check("confidence", recognized.OcrBlocks.All(b => b.Confidence >= 0 && b.Confidence <= 1));
    Check("modelVersion", recognized.OcrModelVersion == OcrQueue.ModelVersion);
    foreach (string query in new[] { "课程", "订单", "保修", "B204", "English", "2026-09-27" })
        Check("literalSearch:" + query, (await store.QueryAsync(query)).Any(i => i.Id == item.Id));
    await File.WriteAllTextAsync(Path.Combine(root, "ocr-result.json"), JsonSerializer.Serialize(recognized, new JsonSerializerOptions { WriteIndented = true }));
}
await store.UpdateMetadataAsync(item.Id, "课程资料", "literal % _ \" ' / 2026", "Study,保修", true);
foreach (var query in new[] { "%", "_", "\"", "'", "/", "课程", "Study" })
    Check("symbolAndMetadataSearch:" + query, (await store.QueryAsync(query)).Any(i => i.Id == item.Id));
Check("unmatchedSymbolNotWildcard", (await store.QueryAsync("%%")).Count == 0);
var oldGeneration = await store.BeginOcrAsync(item.Id);
await store.SetDeletedAsync(item.Id, true);
await store.CompleteOcrAsync(item.Id, oldGeneration, "stale-write", [], "old");
Check("deletedRejectsOldOcr", (await store.GetAsync(item.Id)) is { IsDeleted: true } deleted && deleted.OcrText != "stale-write");
await store.SetDeletedAsync(item.Id, false);
await store.CompleteOcrAsync(item.Id, oldGeneration, "stale-restore", [], "old");
Check("restoreRejectsOldOcr", (await store.GetAsync(item.Id))!.OcrText != "stale-restore");
var missing = await images.AddPngAsync(await File.ReadAllBytesAsync(source));
File.Delete(store.ResolvePath(missing.ImagePath));
await using (var queue = new OcrQueue(store))
{
    queue.Enqueue(missing.Id);
    var deadline = DateTime.UtcNow.AddSeconds(30);
    while ((await store.GetAsync(missing.Id))?.OcrStatus != "Failed" && DateTime.UtcNow < deadline) await Task.Delay(100);
    Check("missingOriginalOcrFails", (await store.GetAsync(missing.Id)) is { OcrStatus: "Failed" } failed && failed.OcrError.Length > 0);
}
LinuxImageLibrary.ExportAtomic(source, store.ResolvePath(missing.ImagePath));
var backup = Path.Combine(root, "备份 snapshot.zip");
await store.BackupAsync(backup);
var restoreRoot = Path.Combine(root, "新 restored library");
await LibraryStore.RestoreAsync(backup, restoreRoot);
using (var restored = new LibraryStore(restoreRoot))
{
    await restored.InitializeAsync();
    var loaded = await restored.GetAsync(item.Id);
    Check("backupRestoresOriginal", loaded is not null && File.Exists(restored.ResolvePath(loaded.ImagePath)));
    Check("backupRestoresTags", loaded is { Tags: "Study,保修", IsFavorite: true });
    Check("backupRestoresSearch", (await restored.QueryAsync("保修")).Any(i => i.Id == item.Id));
    Check("backupRestoresLineBoxes", loaded!.OcrBlocks.Count >= 5);
    Check("interruptedJobsRecovered", (await restored.PendingAsync()).Any(i => i.Id == item.Id));
}
try { await LibraryStore.RestoreAsync(backup, restoreRoot); Check("restoreNeverOverwrites", false); }
catch (IOException) { Check("restoreNeverOverwrites", true); }
var report = new
{
    platform = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
    framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
    cpuOnly = true, model = OcrQueue.ModelVersion, image = "Synthetic Chinese and English text; no user data",
    passed = checks.Count, checks,
    skia = System.Diagnostics.FileVersionInfo.GetVersionInfo(typeof(SKBitmap).Assembly.Location).ProductVersion,
    models = new[] { "models/v5/ch_PP-OCRv5_mobile_det.onnx", "models/v5/ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx",
        "models/chinese/ch_PP-OCRv5_rec_mobile.onnx", "models/chinese/ppocrv5_dict.txt" }.ToDictionary(
        path => path, path => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, path))))),
    nativeLibraries = Directory.EnumerateFiles(AppContext.BaseDirectory, "*.so*").ToDictionary(
        path => Path.GetFileName(path), path => new { bytes = new FileInfo(path).Length,
            sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))) }),
    limits = new[] { "This service probe does not check desktop capture, tray, shortcut, Wayland or clipboard UI." }
};
await File.WriteAllTextAsync(Path.Combine(root, "result.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

namespace ScreenshotBox.Linux
{
    internal static class L
    {
        public static string T(string chinese, string english) => english;
    }
}
