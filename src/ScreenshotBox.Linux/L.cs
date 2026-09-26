using System.Globalization;

namespace ScreenshotBox.Linux;

public static class L
{
    public static bool IsChinese { get; private set; }
    public static string Language => IsChinese ? "zh-CN" : "en-US";
    public static string T(string zh, string en) => IsChinese ? zh : en;
    public static string F(string zh, string en, params object[] args) => string.Format(CultureInfo.CurrentCulture, T(zh, en), args);
    public static string ErrorText(string message)
    {
        if (IsChinese) return message;
        foreach (var (zh, en) in Errors) message = message.Replace(zh, en, StringComparison.Ordinal);
        return message;
    }
    private static readonly (string Zh, string En)[] Errors =
    [
        ("资料库版本比当前软件新，请升级软件后打开。", "This library was created by a newer version. Update ScreenshotBox to open it."),
        ("请先保存截图原图，再加入资料库。", "Save the original image before adding it to the library."),
        ("备份文件已存在，请选择新的文件名。", "The backup file already exists. Choose a new file name."),
        ("备份未完成：资料库中有图片文件缺失。", "Backup incomplete: an image file is missing from the library."),
        ("请选择资料库文件夹。", "Select a library directory."),
        ("备份包含重复文件名。", "The backup contains duplicate file names."),
        ("这不是有效的截图资料盒备份。", "This is not a valid ScreenshotBox backup."),
        ("备份版本不受支持。", "This backup version is not supported."),
        ("备份数据库版本不受支持。", "The backup database version is not supported."),
        ("备份数据库损坏。", "The backup database is corrupt."),
        ("备份含有资料库清单之外的文件。", "The backup contains files outside its library manifest."),
        ("备份中的图片缺失：", "An image is missing from the backup: "),
        ("截图不存在。", "The screenshot does not exist."),
        ("资料库记录损坏。", "A library record is corrupt."),
        ("图片路径不能指向资料库内部文件。", "An image path cannot refer to an internal library file."),
        ("备份和图片路径必须位于资料库文件夹内。", "Backup and image paths must remain inside the library directory."),
        ("备份和图片路径包含无效目录。", "A backup or image path contains an invalid directory."),
        ("恢复位置必须是新的空文件夹，不能覆盖已有资料。", "Restore to a new empty directory; existing data cannot be overwritten."),
        ("恢复位置必须是新的空文件夹。", "Restore to a new empty directory."),
        ("备份不支持指向资料库外部的链接文件。", "Backups do not support links outside the library directory.")
    ];
    public static void Apply(string policy)
    {
        var system = Environment.GetEnvironmentVariable("LC_ALL");
        if (string.IsNullOrEmpty(system)) system = Environment.GetEnvironmentVariable("LC_MESSAGES");
        if (string.IsNullOrEmpty(system)) system = Environment.GetEnvironmentVariable("LANG") ?? CultureInfo.InstalledUICulture.Name;
        IsChinese = policy == "zh-CN" || policy == "System" && system.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
    }
}
