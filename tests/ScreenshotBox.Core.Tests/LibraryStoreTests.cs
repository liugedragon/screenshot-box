using System.IO.Compression;
using ScreenshotBox.Core;
using Xunit;

namespace ScreenshotBox.Core.Tests;

public sealed class LibraryStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ScreenshotBox-tests-" + Guid.NewGuid().ToString("N"));

    private async Task<LibraryStore> CreateStoreAsync(string? suffix = null)
    {
        var store = new LibraryStore(suffix is null ? _root : Path.Combine(_root, suffix));
        await store.InitializeAsync();
        return store;
    }

    private static async Task<ScreenshotItem> AddImageAsync(LibraryStore store, string title = "截图", string text = "", DateTime? createdUtc = null, string tags = "")
    {
        var item = new ScreenshotItem
        {
            Id = Guid.NewGuid().ToString("N"), Title = title, Width = 3, Height = 2,
            OcrText = text, OcrStatus = string.IsNullOrEmpty(text) ? "Pending" : "Ready",
            CreatedUtc = createdUtc ?? DateTime.UtcNow, Tags = tags
        };
        item.ImagePath = "images/" + item.Id + ".png";
        item.ThumbnailPath = "thumbnails/" + item.Id + ".png";
        // The store is format-agnostic; these fixtures test byte preservation rather than PNG rendering.
        await File.WriteAllBytesAsync(store.ResolvePath(item.ImagePath), [1, 2, 3, 9]);
        await File.WriteAllBytesAsync(store.ResolvePath(item.ThumbnailPath), [4, 5]);
        await store.AddAsync(item);
        return item;
    }

    [Theory]
    [InlineData("保")]
    [InlineData("保修")]
    [InlineData("保修期")]
    [InlineData("50%")]
    [InlineData("a_b")]
    [InlineData("\"报价\"")]
    [InlineData("' OR 1=1 --")]
    public async Task SearchTreatsShortChineseAndSpecialCharactersLiterally(string query)
    {
        using var store = await CreateStoreAsync();
        var expected = await AddImageAsync(store, "收据", "保修期 50% a_b \"报价\" ' OR 1=1 --");
        await AddImageAsync(store, "其他截图", "其他内容 50元 axb 报价");
        var result = await store.QueryAsync(query);
        Assert.Single(result);
        Assert.Equal(expected.Id, result[0].Id);
    }

    [Fact]
    public async Task MetadataChangesSearchAndFavoriteImmediatelyAndSurviveRestart()
    {
        var store = await CreateStoreAsync();
        var item = await AddImageAsync(store, "旧标题");
        await store.UpdateMetadataAsync(item.Id, "新标题", "课程安排", "重要,学习", true);
        Assert.Empty(await store.QueryAsync("旧标题"));
        Assert.Single(await store.QueryAsync("课程", "favorites"));
        Assert.Single(await store.QueryAsync("重要"));
        store.Dispose();

        using var restarted = await CreateStoreAsync();
        var saved = await restarted.GetAsync(item.Id);
        Assert.NotNull(saved);
        Assert.Equal("新标题", saved.Title);
        Assert.Equal("课程安排", saved.Notes);
        Assert.True(saved.IsFavorite);
        Assert.Single(await restarted.QueryAsync("学习"));
    }

    [Fact]
    public async Task DeletedItemCannotBeResurrectedByAnOcrJobEvenAfterUndo()
    {
        using var store = await CreateStoreAsync();
        var item = await AddImageAsync(store);
        var staleGeneration = await store.BeginOcrAsync(item.Id);
        await store.SetDeletedAsync(item.Id, true);
        await store.CompleteOcrAsync(item.Id, staleGeneration, "不应该出现", [], "old-model");
        Assert.Empty(await store.QueryAsync(""));
        Assert.Single(await store.QueryAsync("", "trash"));
        Assert.Equal(-1, await store.BeginOcrAsync(item.Id));
        await store.SetDeletedAsync(item.Id, false);
        await store.CompleteOcrAsync(item.Id, staleGeneration, "不应该出现", [], "old-model");
        var restored = await store.GetAsync(item.Id);
        Assert.NotNull(restored);
        Assert.Equal("", restored.OcrText);
        Assert.Equal("Pending", restored.OcrStatus);
        Assert.True(File.Exists(store.ResolvePath(item.ImagePath)));
    }

    [Fact]
    public async Task OlderOcrResultDoesNotOverwriteANewerJob()
    {
        using var store = await CreateStoreAsync();
        var item = await AddImageAsync(store);
        var first = await store.BeginOcrAsync(item.Id);
        var second = await store.BeginOcrAsync(item.Id);
        await store.CompleteOcrAsync(item.Id, second, "正确结果", [new("正确结果", 1, 2, 3, 4, .9)], "v2");
        await store.CompleteOcrAsync(item.Id, first, "过期结果", [], "v1");
        var saved = await store.GetAsync(item.Id);
        Assert.NotNull(saved);
        Assert.Equal("正确结果", saved.OcrText);
        Assert.Equal("v2", saved.OcrModelVersion);
        Assert.Single(saved.OcrBlocks);
        Assert.Equal(1, saved.OcrBlocks[0].X);
    }

    [Fact]
    public async Task RestartRequeuesInterruptedOcrAndInvalidatesItsGeneration()
    {
        var store = await CreateStoreAsync();
        var item = await AddImageAsync(store);
        var interruptedGeneration = await store.BeginOcrAsync(item.Id);
        store.Dispose();
        using var restarted = await CreateStoreAsync();
        Assert.Single(await restarted.PendingAsync());
        await restarted.CompleteOcrAsync(item.Id, interruptedGeneration, "过期结果", [], "old");
        var saved = await restarted.GetAsync(item.Id);
        Assert.Equal("Pending", saved!.OcrStatus);
        Assert.True(saved.OcrGeneration > interruptedGeneration);
    }

    [Fact]
    public async Task BackupRestoresDatabaseOriginalsThumbnailsAndTrashWithoutWALLoss()
    {
        using var store = await CreateStoreAsync("source");
        var item = await AddImageAsync(store, "保修凭据", "订单编号 A123");
        var recycled = await AddImageAsync(store, "回收中的截图");
        await store.UpdateMetadataAsync(item.Id, "最新标题", "刚更新的备注", "凭据", true);
        await store.SetDeletedAsync(recycled.Id, true);
        var zip = Path.Combine(_root, "library-backup.zip");
        await store.BackupAsync(zip);
        var target = Path.Combine(_root, "restored");
        await LibraryStore.RestoreAsync(zip, target);
        using var restored = new LibraryStore(target);
        await restored.InitializeAsync();
        Assert.Single(await restored.QueryAsync("刚更新"));
        Assert.Single(await restored.QueryAsync("A123", "favorites"));
        Assert.Single(await restored.QueryAsync("", "trash"));
        var saved = await restored.GetAsync(item.Id);
        Assert.Equal("最新标题", saved!.Title);
        Assert.Equal(await File.ReadAllBytesAsync(store.ResolvePath(item.ImagePath)),
            await File.ReadAllBytesAsync(restored.ResolvePath(saved.ImagePath)));
        Assert.Equal(await File.ReadAllBytesAsync(store.ResolvePath(item.ThumbnailPath)),
            await File.ReadAllBytesAsync(restored.ResolvePath(saved.ThumbnailPath)));
        Assert.Empty(await restored.MissingFilesAsync());
    }

    [Fact]
    public async Task ConcurrentMetadataUpdatesLeaveAnInternallyConsistentBackup()
    {
        using var store = await CreateStoreAsync("source");
        var item = await AddImageAsync(store);
        var zip = Path.Combine(_root, "concurrent.zip");
        var updating = Task.Run(async () =>
        {
            for (var n = 0; n < 30; n++)
                await store.UpdateMetadataAsync(item.Id, "版本" + n, "版本" + n, "", n % 2 == 0);
        });
        await store.BackupAsync(zip);
        await updating;
        var target = Path.Combine(_root, "restored");
        await LibraryStore.RestoreAsync(zip, target);
        using var restored = new LibraryStore(target);
        await restored.InitializeAsync();
        var saved = await restored.GetAsync(item.Id);
        Assert.NotNull(saved);
        if (saved.Title.StartsWith("版本")) Assert.Equal(saved.Title, saved.Notes);
        Assert.Single(await restored.QueryAsync(saved.Title));
        Assert.Empty(await restored.MissingFilesAsync());
    }

    [Fact]
    public async Task MissingImagesAreReportedAndDoNotProduceAnApparentlySuccessfulBackup()
    {
        using var store = await CreateStoreAsync();
        var item = await AddImageAsync(store);
        File.Delete(store.ResolvePath(item.ImagePath));
        Assert.Single(await store.MissingFilesAsync());
        var zip = Path.Combine(_root, "missing.zip");
        await Assert.ThrowsAsync<FileNotFoundException>(() => store.BackupAsync(zip));
        Assert.False(File.Exists(zip));
    }

    [Theory]
    [InlineData("../escaped.png")]
    [InlineData("..\\escaped.png")]
    [InlineData("C:/escaped.png")]
    [InlineData("images/test.png:extra")]
    [InlineData("images/../escaped.png")]
    public async Task UnsafeArchivePathsAreRejectedWithoutWritingOutsideDestination(string name)
    {
        Directory.CreateDirectory(_root);
        var zip = Path.Combine(_root, "malicious.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            using var writer = new StreamWriter(archive.CreateEntry(name).Open());
            writer.Write("untrusted");
        }
        var destination = Path.Combine(_root, "restore");
        await Assert.ThrowsAsync<InvalidDataException>(() => LibraryStore.RestoreAsync(zip, destination));
        Assert.False(Directory.Exists(destination));
        Assert.False(File.Exists(Path.Combine(_root, "escaped.png")));
    }

    [Fact]
    public async Task RestoreNeverOverwritesAnExistingLibrary()
    {
        using var store = await CreateStoreAsync("source");
        await AddImageAsync(store);
        var zip = Path.Combine(_root, "backup.zip");
        await store.BackupAsync(zip);
        var target = Path.Combine(_root, "occupied");
        Directory.CreateDirectory(target);
        var marker = Path.Combine(target, "keep.txt");
        await File.WriteAllTextAsync(marker, "keep");
        await Assert.ThrowsAsync<IOException>(() => LibraryStore.RestoreAsync(zip, target));
        Assert.Equal("keep", await File.ReadAllTextAsync(marker));
    }

    [Fact]
    public async Task PaginationUsesStableOrderAndIncludesOnlyTheSelectedFilter()
    {
        using var store = await CreateStoreAsync();
        var items = new List<ScreenshotItem>();
        for (var n = 0; n < 5; n++) items.Add(await AddImageAsync(store));
        await store.SetDeletedAsync(items[0].Id, true);
        var all = await store.QueryAsync("");
        var first = await store.QueryAsync("", limit: 2);
        var second = await store.QueryAsync("", limit: 2, offset: 2);
        Assert.Equal(4, all.Count);
        Assert.Equal(all.Select(x => x.Id), first.Concat(second).Select(x => x.Id));
    }

    [Fact]
    public async Task OldestOrderingAndTagsUseTheWholeLibraryBeyondTheNewestPage()
    {
        using var store = await CreateStoreAsync();
        var start = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var items = new List<ScreenshotItem>();
        for (var n = 0; n < 205; n++)
            items.Add(await AddImageAsync(store, $"截图 {n}", createdUtc: start.AddMinutes(n),
                tags: n == 0 ? "古早, 通用,通用,, " : n == 204 ? "今日,通用" : ""));
        var deleted = await AddImageAsync(store, tags: "已删除标签");
        await store.SetDeletedAsync(deleted.Id, true);

        var newestPage = await store.QueryAsync("");
        Assert.Equal(200, newestPage.Count);
        Assert.DoesNotContain(newestPage, item => item.Id == items[0].Id);
        var first = await store.QueryAsync("", limit: 2, oldestFirst: true);
        var second = await store.QueryAsync("", limit: 2, offset: 2, oldestFirst: true);
        Assert.Equal(items.Take(4).Select(item => item.Id), first.Concat(second).Select(item => item.Id));
        Assert.Equal(items.Take(200).Select(item => item.Id), (await store.QueryAsync("", oldestFirst: true)).Select(item => item.Id));
        var tags = await store.GetTagsAsync();
        Assert.Equal(3, tags.Count);
        Assert.Contains("古早", tags);
        Assert.Contains("今日", tags);
        Assert.Contains("通用", tags);
        Assert.DoesNotContain("已删除标签", tags);
    }

    [Fact]
    public async Task RecentFilterIsAppliedBeforeOldestSortingAndPagination()
    {
        using var store = await CreateStoreAsync();
        var now = DateTime.UtcNow;
        for (var n = 0; n < 15; n++)
            await AddImageAsync(store, "旧资料", createdUtc: now.AddDays(-20).AddHours(n));
        var recent = new List<ScreenshotItem>();
        for (var n = 0; n < 6; n++)
            recent.Add(await AddImageAsync(store, "最近资料", createdUtc: now.AddDays(-6).AddHours(n)));
        var deleted = await AddImageAsync(store, "最近资料", createdUtc: now.AddDays(-6).AddMinutes(-1));
        await store.SetDeletedAsync(deleted.Id, true);

        Assert.All(await store.QueryAsync("", limit: 2, oldestFirst: true), item => Assert.Equal("旧资料", item.Title));
        var first = await store.QueryAsync("", "recent", limit: 2, oldestFirst: true);
        var second = await store.QueryAsync("", "recent", limit: 2, offset: 2, oldestFirst: true);
        Assert.Equal(recent.Take(4).Select(item => item.Id), first.Concat(second).Select(item => item.Id));
        Assert.Equal(recent.TakeLast(2).Reverse().Select(item => item.Id), (await store.QueryAsync("", "recent", limit: 2)).Select(item => item.Id));
        Assert.Empty(await store.QueryAsync("旧资料", "recent"));
        Assert.Equal(6, (await store.QueryAsync("最近", "recent")).Count);
    }

    [Theory]
    [InlineData("学习")]
    [InlineData("50%")]
    [InlineData("a_b")]
    [InlineData("\"报价\"")]
    [InlineData("' OR 1=1 --")]
    [InlineData("Learning")]
    [InlineData("école")]
    public async Task TagClassificationUsesLiteralWholeMembersRatherThanImageText(string tag)
    {
        using var store = await CreateStoreAsync();
        var matching = await AddImageAsync(store, "已分类", tags: $"其他,  {tag}  ，末尾");
        await AddImageAsync(store, tag, tag, tags: $"prefix{tag},其他");
        await AddImageAsync(store, "未分类", tag);

        var result = await store.QueryAsync("", requiredTag: "  " + tag.ToUpperInvariant() + "  ");
        Assert.Equal(matching.Id, Assert.Single(result).Id);
        Assert.Contains(tag, await store.GetTagsAsync());
        Assert.Empty(await store.QueryAsync("", requiredTag: tag + "额外"));
    }

    [Fact]
    public async Task ExactTagsIntersectSearchAndFavoritesBeforeStablePagination()
    {
        using var store = await CreateStoreAsync();
        var start = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var matching = new List<ScreenshotItem>();
        for (var n = 0; n < 12; n++)
        {
            var item = await AddImageAsync(store, "Learning " + n,
                n < 8 ? "保修订单" : "课程安排", start.AddMinutes(n),
                n % 2 == 0 ? "Learning， 凭据, learning, " : "Learning-extra");
            await store.UpdateMetadataAsync(item.Id, item.Title, "", item.Tags, n % 4 == 0);
            if (n % 2 == 0) matching.Add(item);
        }
        var deleted = await AddImageAsync(store, "Learning", "保修订单", start.AddDays(-1), "learning");
        await store.SetDeletedAsync(deleted.Id, true);

        var first = await store.QueryAsync("", limit: 2, oldestFirst: true, requiredTag: "LEARNING");
        var second = await store.QueryAsync("", limit: 2, offset: 2, oldestFirst: true, requiredTag: "learning");
        Assert.Equal(matching.Take(4).Select(i => i.Id), first.Concat(second).Select(i => i.Id));
        Assert.Equal(matching.TakeLast(2).Reverse().Select(i => i.Id),
            (await store.QueryAsync("", limit: 2, requiredTag: "Learning")).Select(i => i.Id));
        Assert.Equal(new[] { matching[0].Id, matching[2].Id },
            (await store.QueryAsync("保修", "favorites", oldestFirst: true, requiredTag: "learning")).Select(i => i.Id));
        Assert.Equal(matching[2].Id, Assert.Single(await store.QueryAsync("保修", "favorites", limit: 1, offset: 1,
            oldestFirst: true, requiredTag: "learning")).Id);
        Assert.Equal(deleted.Id, Assert.Single(await store.QueryAsync("", "trash", requiredTag: "Learning")).Id);
        Assert.Single(await store.GetTagsAsync(), tag => tag.Equals("learning", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UpdatingTagsImmediatelyChangesClassificationAndSurvivesReopening()
    {
        var store = await CreateStoreAsync();
        var item = await AddImageAsync(store, "标题仍然包含旧标签", "旧标签", tags: "旧标签");
        await store.UpdateMetadataAsync(item.Id, item.Title, "", " 新标签 ， 50%_\"报价\" , ", true);
        Assert.Empty(await store.QueryAsync("", requiredTag: "旧标签"));
        store.Dispose();

        using var reopened = await CreateStoreAsync();
        Assert.Equal(item.Id, Assert.Single(await reopened.QueryAsync("", "favorites", requiredTag: "50%_\"报价\"")).Id);
        Assert.Equal(item.Id, Assert.Single(await reopened.QueryAsync("旧标签", requiredTag: "新标签")).Id);
        Assert.DoesNotContain("旧标签", await reopened.GetTagsAsync());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
