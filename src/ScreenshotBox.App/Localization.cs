using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Runtime.InteropServices;

namespace ScreenshotBox.App;

/// <summary>Application text and culture. Language changes take effect on restart.</summary>
public static class L
{
    public static string Language { get; private set; } = "zh-CN";
    public static bool IsEnglish => Language == "en-US";
    public static string Normalize(string? language) => language switch { "en" or "en-US"=>"en-US", "zh" or "zh-CN"=>"zh-CN", _=>"System" };
    public static string Resolve(string? preference,string? systemLanguage=null)
    {
        var policy=Normalize(preference);
        if(policy!="System")return policy;
        return (systemLanguage??WindowsDisplayLanguage()).StartsWith("zh",StringComparison.OrdinalIgnoreCase)?"zh-CN":"en-US";
    }
    private static string WindowsDisplayLanguage()
    {
        const uint languageName=8;
        try {
            uint count=0,length=0;
            if(GetUserPreferredUILanguages(languageName,out count,IntPtr.Zero,ref length)&&length is >0 and <65536){
                var buffer=Marshal.AllocHGlobal(checked((int)length*2));
                try{if(GetUserPreferredUILanguages(languageName,out count,buffer,ref length)&&Marshal.PtrToStringUni(buffer) is {Length:>0} name)return name;}
                finally{Marshal.FreeHGlobal(buffer);}
            }
            return CultureInfo.GetCultureInfo((int)GetUserDefaultUILanguage()).Name;
        }catch{return "en-US";}
    }
    [DllImport("kernel32.dll",ExactSpelling=true,SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetUserPreferredUILanguages(uint flags,out uint count,IntPtr languages,ref uint length);
    [DllImport("kernel32.dll",ExactSpelling=true)]private static extern ushort GetUserDefaultUILanguage();
    public static string T(string chinese, string english) => IsEnglish ? english : chinese;
    public static string F(string chinese, string english, params object?[] args) => string.Format(CultureInfo.CurrentCulture,T(chinese,english),args);
    public static string Get(string key)
    {
        if (!Texts.TryGetValue(key,out var text)) throw new ArgumentException("Unknown UI text key: " + key,nameof(key));
        return T(text.Chinese,text.English);
    }
    public static void Apply(string? language)
    {
        Language=Resolve(language);
        var culture=CultureInfo.GetCultureInfo(Language);
        CultureInfo.CurrentCulture=culture;CultureInfo.CurrentUICulture=culture;
        CultureInfo.DefaultThreadCurrentCulture=culture;CultureInfo.DefaultThreadCurrentUICulture=culture;
        if(Application.Current is { } app)
            foreach(var key in Texts.Keys)app.Resources["Ui."+key]=Get(key);
    }
    public static string Error(Exception exception) => Error(exception.Message);
    public static string Error(string message)
    {
        if(!IsEnglish)return message;
        foreach(var pair in CoreErrors)
            if(message.StartsWith(pair.Key,StringComparison.Ordinal))return pair.Value+message[pair.Key.Length..];
        return message;
    }
    private static readonly Dictionary<string,(string Chinese,string English)> Texts = new(StringComparer.Ordinal)
    {
        ["AppName"]=("截图资料盒","ScreenshotBox"),
        ["WindowTitle"]=("截图资料盒 · ScreenshotBox","ScreenshotBox"),
        ["All"]=("全部截图","All screenshots"),
        ["Recent"]=("最近保存","Recent"),
        ["Starred"]=("星标","Starred"),
        ["Tags"]=("标签","Tags"),
        ["Trash"]=("回收站","Recycle bin"),
        ["Settings"]=("设置","Settings"),
        ["Search"]=("搜索标题、备注、标签和图片文字","Search titles, notes, tags, and image text"),
        ["Capture"]=("新截图","Capture"),
        ["Import"]=("导入","Import"),
        ["Newest"]=("最新优先","Newest first"),
        ["Oldest"]=("最早优先","Oldest first"),
        ["ThumbnailSize"]=("缩略图大小","Thumbnail size"),
        ["Details"]=("详情","Details"),
        ["Empty"]=("按快捷键框选截图，或拖入图片。","Use the capture shortcut or drop images here."),
        ["LocalImages"]=("PNG / JPEG · 本地保存","PNG / JPEG · Stored locally"),
        ["LoadMore"]=("加载更多","Load more"),
        ["SelectImage"]=("选择一张截图查看资料","Select a screenshot to view its details"),
        ["ItemDetails"]=("资料详情","Image details"),
        ["Title"]=("标题","Title"),
        ["Notes"]=("备注","Notes"),
        ["TagLabel"]=("标签（逗号分隔）","Tags (comma-separated)"),
        ["SaveChanges"]=("保存修改","Save changes"),
        ["Preview"]=("预览","Preview"),
        ["ExportImage"]=("导出图片","Export image"),
        ["Delete"]=("移入回收站","Move to recycle bin"),
        ["CopyText"]=("复制文字","Copy text"),
        ["Retry"]=("重新识别","Retry OCR")
    };
    private static readonly Dictionary<string,string> CoreErrors = new(StringComparer.Ordinal)
    {
        ["资料库版本比当前软件新，请升级软件后打开。"]="This library requires a newer version of ScreenshotBox.",
        ["请先保存截图原图，再加入资料库。"]="Save the original image before adding it to the library.",
        ["未知资料库筛选条件。"]="Unknown library filter.",
        ["备份文件已存在，请选择新的文件名。"]="The backup file already exists. Choose a new filename.",
        ["备份未完成：资料库中有图片文件缺失。"]="Backup failed because an image is missing from the library.",
        ["请选择资料库文件夹。"]="Select a library directory.",
        ["备份包含重复文件名。"]="The backup contains duplicate filenames.",
        ["这不是有效的截图资料盒备份。"]="This is not a valid ScreenshotBox backup.",
        ["备份版本不受支持。"]="Unsupported backup version.",
        ["备份数据库版本不受支持。"]="Unsupported backup database version.",
        ["备份数据库损坏。"]="The backup database is damaged.",
        ["备份含有资料库清单之外的文件。"]="The backup contains files outside the library manifest.",
        ["备份中的图片缺失："]="An image is missing from the backup: ",
        ["截图不存在。"]="The screenshot no longer exists.",
        ["资料库记录损坏。"]="A library record is damaged.",
        ["图片路径不能指向资料库内部文件。"]="The image path cannot point to internal library files.",
        ["备份和图片路径必须位于资料库文件夹内。"]="Backup and image paths must stay within the library directory.",
        ["备份和图片路径包含无效目录。"]="The backup or image path contains an invalid directory.",
        ["恢复位置必须是新的空文件夹，不能覆盖已有资料。"]="Restore into a new empty directory. Existing data cannot be overwritten.",
        ["恢复位置必须是新的空文件夹。"]="Restore into a new empty directory.",
        ["备份不支持指向资料库外部的链接文件。"]="The backup cannot contain links outside the library directory."
    };
}
