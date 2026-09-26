using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace ScreenshotBox.Core;

/// <summary>
/// A local library. Image files are immutable; removing an item only moves its
/// metadata to the recycle view. This also keeps concurrent backups consistent.
/// </summary>
public sealed class LibraryStore : IDisposable
{
    public const string DatabaseName = "library.db";
    private const int SchemaVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    // Explicitly reference the bundle. Reflection-based auto-discovery is unreliable
    // when a host or publish dependency graph omits a dynamically loaded assembly.
    private static readonly Lazy<bool> SqliteInitialized = new(() => { SQLitePCL.Batteries_V2.Init(); return true; });
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _connectionString;

    public string RootPath { get; }

    public LibraryStore(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _ = SqliteInitialized.Value;
        RootPath = Path.GetFullPath(root);
        _connectionString = ConnectionString(Path.Combine(RootPath, DatabaseName));
    }

    public async Task InitializeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            Directory.CreateDirectory(RootPath);
            Directory.CreateDirectory(Path.Combine(RootPath, "images"));
            Directory.CreateDirectory(Path.Combine(RootPath, "thumbnails"));
            using var connection = Open();
            using var version = connection.CreateCommand();
            version.CommandText = "PRAGMA user_version;";
            var existingVersion = Convert.ToInt32(version.ExecuteScalar(), CultureInfo.InvariantCulture);
            if (existingVersion > SchemaVersion)
                throw new InvalidDataException("资料库版本比当前软件新，请升级软件后打开。");
            using var command = connection.CreateCommand();
            command.CommandText = """
                PRAGMA journal_mode=WAL;
                PRAGMA synchronous=FULL;
                CREATE TABLE IF NOT EXISTS screenshots (
                    id TEXT PRIMARY KEY NOT NULL,
                    created_utc TEXT NOT NULL,
                    title TEXT NOT NULL,
                    notes TEXT NOT NULL,
                    tags TEXT NOT NULL,
                    ocr_text TEXT NOT NULL,
                    favorite INTEGER NOT NULL,
                    deleted INTEGER NOT NULL,
                    ocr_status TEXT NOT NULL,
                    generation INTEGER NOT NULL,
                    item_json TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS ix_screenshots_created ON screenshots(deleted, created_utc DESC, id);
                CREATE INDEX IF NOT EXISTS ix_screenshots_pending ON screenshots(deleted, ocr_status);
                PRAGMA user_version=1;
                """;
            command.ExecuteNonQuery();
            using var transaction = connection.BeginTransaction();
            var interrupted = ReadMany(connection, transaction,
                "SELECT item_json FROM screenshots WHERE ocr_status='Processing';");
            foreach (var item in interrupted)
            {
                item.OcrGeneration++;
                item.OcrStatus = "Pending";
                Save(connection, transaction, item);
            }
            transaction.Commit();
        }
        finally { _gate.Release(); }
    }

    public async Task AddAsync(ScreenshotItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(item.Id);
        ValidateFilePath(item.ImagePath);
        if (!File.Exists(ResolvePath(item.ImagePath)))
            throw new FileNotFoundException("请先保存截图原图，再加入资料库。", item.ImagePath);
        RejectReparsePoints(RootPath, ResolvePath(item.ImagePath));
        if (!string.IsNullOrEmpty(item.ThumbnailPath)) ValidateFilePath(item.ThumbnailPath);
        if (item.CreatedUtc == default) item.CreatedUtc = DateTime.UtcNow;
        item.CreatedUtc = item.CreatedUtc.ToUniversalTime();
        await _gate.WaitAsync();
        try
        {
            using var connection = Open();
            using var transaction = connection.BeginTransaction();
            Save(connection, transaction, item, insert: true);
            transaction.Commit();
        }
        finally { _gate.Release(); }
    }

    /// <summary>
    /// Literal, ASCII case-insensitive contains search. SQLite instr rather than LIKE
    /// deliberately treats quotes, percent, underscore, and one/two-character
    /// Chinese queries as normal text. This version does not claim an FTS index.
    /// </summary>
    public async Task<List<ScreenshotItem>> QueryAsync(string query, string filter = "all", int limit = 200, int offset = 0, bool oldestFirst = false, string? requiredTag = null)
    {
        if (limit is < 1 or > 1_000_000) throw new ArgumentOutOfRangeException(nameof(limit));
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
        var condition = filter switch
        {
            "all" => "deleted=0",
            "recent" => "deleted=0 AND created_utc >= $recentCutoff",
            "favorites" or "favorite" => "deleted=0 AND favorite=1",
            "deleted" or "trash" => "deleted=1",
            "pending" => "deleted=0 AND ocr_status IN ('Pending','Processing')",
            "failed" => "deleted=0 AND ocr_status='Failed'",
            _ => throw new ArgumentException("未知资料库筛选条件。", nameof(filter))
        };
        await _gate.WaitAsync();
        try
        {
            using var connection = Open();
            // Keep classification independent of free-text search, and filter in SQL
            // before ordering/paging. The same parser is used by the navigation list.
            if (requiredTag is not null)
                connection.CreateFunction<string, string, int>("sb_has_tag",
                    (raw, tag) => ParseTags(raw).Contains(tag, StringComparer.OrdinalIgnoreCase) ? 1 : 0,
                    isDeterministic: true);
            using var command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT item_json FROM screenshots WHERE {condition}
                {(requiredTag is null ? "" : "AND sb_has_tag(tags,$requiredTag)=1")}
                AND ($q='' OR instr(lower(title),lower($q))>0
                    OR instr(lower(notes),lower($q))>0
                    OR instr(lower(tags),lower($q))>0
                    OR instr(lower(ocr_text),lower($q))>0)
                ORDER BY created_utc {(oldestFirst ? "ASC" : "DESC")}, id ASC LIMIT $limit OFFSET $offset;
                """;
            command.Parameters.AddWithValue("$q", query?.Trim() ?? "");
            command.Parameters.AddWithValue("$limit", limit);
            command.Parameters.AddWithValue("$offset", offset);
            if (requiredTag is not null) command.Parameters.AddWithValue("$requiredTag", requiredTag.Trim());
            if (filter == "recent") command.Parameters.AddWithValue("$recentCutoff", DateTime.UtcNow.AddDays(-7).ToString("O", CultureInfo.InvariantCulture));
            return ReadMany(command);
        }
        finally { _gate.Release(); }
    }

    /// <summary>Lists distinct tags across the entire active library, independently of the current result page.</summary>
    public async Task<List<string>> GetTagsAsync()
    {
        await _gate.WaitAsync();
        try
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT tags FROM screenshots WHERE deleted=0 AND tags<>'';";
            using var reader = command.ExecuteReader();
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (reader.Read())
                foreach (var tag in ParseTags(reader.GetString(0)))
                    tags.Add(tag);
            return tags.OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase).ToList();
        }
        finally { _gate.Release(); }
    }

    // Preserve the original metadata text; normalize separators and whitespace only
    // when interpreting tags. English and Chinese commas are equivalent separators.
    private static IEnumerable<string> ParseTags(string tags) =>
        tags.Split([',', '，'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    public async Task<ScreenshotItem?> GetAsync(string id)
    {
        await _gate.WaitAsync();
        try
        {
            using var connection = Open();
            return ReadOne(connection, null, id);
        }
        finally { _gate.Release(); }
    }

    public Task UpdateMetadataAsync(string id, string title, string notes, string tags, bool favorite) =>
        MutateAsync(id, item =>
        {
            item.Title = title ?? "";
            item.Notes = notes ?? "";
            item.Tags = tags ?? "";
            item.IsFavorite = favorite;
        });

    public Task SetDeletedAsync(string id, bool deleted) => MutateAsync(id, item =>
    {
        if (item.IsDeleted != deleted)
        {
            item.OcrGeneration++;
            if (item.OcrStatus == "Processing") item.OcrStatus = "Pending";
        }
        item.IsDeleted = deleted;
    });

    public async Task<int> BeginOcrAsync(string id)
    {
        await _gate.WaitAsync();
        try
        {
            using var connection = Open();
            using var transaction = connection.BeginTransaction();
            var item = ReadOne(connection, transaction, id);
            if (item is null || item.IsDeleted) return -1;
            item.OcrGeneration++;
            item.OcrStatus = "Processing";
            item.OcrError = "";
            Save(connection, transaction, item);
            transaction.Commit();
            return item.OcrGeneration;
        }
        finally { _gate.Release(); }
    }

    public async Task CompleteOcrAsync(string id, int generation, string text, List<OcrBlock> blocks,
        string modelVersion, string? error = null)
    {
        await _gate.WaitAsync();
        try
        {
            using var connection = Open();
            using var transaction = connection.BeginTransaction();
            var item = ReadOne(connection, transaction, id);
            if (item is null || item.IsDeleted || item.OcrGeneration != generation || item.OcrStatus != "Processing")
                return;
            item.OcrStatus = string.IsNullOrEmpty(error) ? "Ready" : "Failed";
            item.OcrText = text ?? "";
            item.OcrBlocks = blocks ?? [];
            item.OcrModelVersion = modelVersion ?? "";
            item.OcrError = error ?? "";
            Save(connection, transaction, item);
            transaction.Commit();
        }
        finally { _gate.Release(); }
    }

    public async Task<List<ScreenshotItem>> PendingAsync()
    {
        await _gate.WaitAsync();
        try
        {
            using var connection = Open();
            return ReadMany(connection, null,
                "SELECT item_json FROM screenshots WHERE deleted=0 AND ocr_status='Pending' ORDER BY created_utc, id;");
        }
        finally { _gate.Release(); }
    }

    public async Task<List<ScreenshotItem>> MissingFilesAsync()
    {
        await _gate.WaitAsync();
        try
        {
            using var connection = Open();
            return ReadMany(connection, null, "SELECT item_json FROM screenshots WHERE deleted=0;")
                .Where(item => !File.Exists(ResolvePath(item.ImagePath))).ToList();
        }
        finally { _gate.Release(); }
    }

    /// <summary>Database snapshot and its referenced immutable images, including recycle items.</summary>
    public async Task BackupAsync(string zipPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zipPath);
        var destination = Path.GetFullPath(zipPath);
        if (File.Exists(destination)) throw new IOException("备份文件已存在，请选择新的文件名。");
        var stage = Path.Combine(Path.GetTempPath(), "ScreenshotBox-backup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        var snapshot = Path.Combine(stage, DatabaseName);
        try
        {
            await _gate.WaitAsync();
            try
            {
                // BackupDatabase understands WAL: copying library.db alone would omit recent writes.
                using var source = Open();
                using var target = new SqliteConnection(ConnectionString(snapshot));
                target.Open();
                source.BackupDatabase(target);
            }
            finally { _gate.Release(); }

            await Task.Run(() =>
            {
                List<ScreenshotItem> items;
                using (var snapshotConnection = new SqliteConnection(ConnectionString(snapshot)))
                {
                    snapshotConnection.Open();
                    items = ReadMany(snapshotConnection, null, "SELECT item_json FROM screenshots ORDER BY id;");
                }
                var references = items.SelectMany(i => new[] { i.ImagePath, i.ThumbnailPath })
                    .Where(p => !string.IsNullOrEmpty(p)).Select(ValidateFilePath)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var zipTemp = Path.Combine(stage, "backup.zip");
                using (var archive = ZipFile.Open(zipTemp, ZipArchiveMode.Create))
                {
                    archive.CreateEntryFromFile(snapshot, DatabaseName, CompressionLevel.Optimal);
                    var manifest = archive.CreateEntry("manifest.json");
                    using (var stream = manifest.Open())
                        JsonSerializer.Serialize(stream, new BackupManifest(SchemaVersion, DateTime.UtcNow, items.Count), JsonOptions);
                    foreach (var relativePath in references)
                    {
                        var file = ResolvePath(relativePath);
                        if (!File.Exists(file))
                            throw new FileNotFoundException("备份未完成：资料库中有图片文件缺失。", file);
                        RejectReparsePoints(RootPath, file);
                        archive.CreateEntryFromFile(file, relativePath, CompressionLevel.Optimal);
                    }
                }
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                var pendingDestination = destination + ".partial-" + Guid.NewGuid().ToString("N");
                try
                {
                    File.Copy(zipTemp, pendingDestination, overwrite: false);
                    File.Move(pendingDestination, destination, overwrite: false);
                }
                finally
                {
                    if (File.Exists(pendingDestination)) File.Delete(pendingDestination);
                }
            });
        }
        finally { Directory.Delete(stage, recursive: true); }
    }

    /// <summary>Restore into a new empty directory, validating paths before installing it.</summary>
    public static async Task RestoreAsync(string zipPath, string destination)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zipPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        var target = Path.GetFullPath(destination);
        EnsureEmptyDestination(target);
        var parent = Path.GetDirectoryName(target) ?? throw new ArgumentException("请选择资料库文件夹。");
        Directory.CreateDirectory(parent);
        var stage = Path.Combine(parent, ".ScreenshotBox-restore-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        try
        {
            await Task.Run(() =>
            {
                using var archive = ZipFile.OpenRead(zipPath);
                var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith('/')) continue;
                    var relative = NormalizeRelativePath(entry.FullName);
                    if (!entries.TryAdd(relative, entry))
                        throw new InvalidDataException("备份包含重复文件名。");
                }
                if (!entries.TryGetValue(DatabaseName, out var database) || !entries.TryGetValue("manifest.json", out var manifest))
                    throw new InvalidDataException("这不是有效的截图资料盒备份。");
                using (var stream = manifest.Open())
                {
                    var info = JsonSerializer.Deserialize<BackupManifest>(stream, JsonOptions);
                    if (info is null || info.FormatVersion != SchemaVersion)
                        throw new InvalidDataException("备份版本不受支持。");
                }
                database.ExtractToFile(Path.Combine(stage, DatabaseName));
                List<ScreenshotItem> items;
                using (var connection = new SqliteConnection(ConnectionString(Path.Combine(stage, DatabaseName))))
                {
                    connection.Open();
                    using var check = connection.CreateCommand();
                    check.CommandText = "PRAGMA user_version;";
                    if (Convert.ToInt32(check.ExecuteScalar(), CultureInfo.InvariantCulture) != SchemaVersion)
                        throw new InvalidDataException("备份数据库版本不受支持。");
                    check.CommandText = "PRAGMA integrity_check;";
                    if (!string.Equals(Convert.ToString(check.ExecuteScalar()), "ok", StringComparison.Ordinal))
                        throw new InvalidDataException("备份数据库损坏。");
                    items = ReadMany(connection, null, "SELECT item_json FROM screenshots;");
                }
                var references = items.SelectMany(i => new[] { i.ImagePath, i.ThumbnailPath })
                    .Where(p => !string.IsNullOrEmpty(p)).Select(ValidateFilePath)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var expected = references.Concat([DatabaseName, "manifest.json"]).ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (entries.Keys.Any(p => !expected.Contains(p)))
                    throw new InvalidDataException("备份含有资料库清单之外的文件。");
                foreach (var relative in references)
                {
                    if (!entries.TryGetValue(relative, out var entry))
                        throw new InvalidDataException($"备份中的图片缺失：{relative}");
                    var output = Path.Combine(stage, relative.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                    entry.ExtractToFile(output);
                }
            });
            EnsureEmptyDestination(target);
            if (Directory.Exists(target)) Directory.Delete(target, recursive: false);
            Directory.Move(stage, target);
        }
        finally { if (Directory.Exists(stage)) Directory.Delete(stage, recursive: true); }
    }

    public string ResolvePath(string relativePath) =>
        Path.Combine(RootPath, ValidateFilePath(relativePath).Replace('/', Path.DirectorySeparatorChar));

    private async Task MutateAsync(string id, Action<ScreenshotItem> action)
    {
        await _gate.WaitAsync();
        try
        {
            using var connection = Open();
            using var transaction = connection.BeginTransaction();
            var item = ReadOne(connection, transaction, id) ?? throw new KeyNotFoundException("截图不存在。");
            action(item);
            Save(connection, transaction, item);
            transaction.Commit();
        }
        finally { _gate.Release(); }
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        try { connection.Open(); return connection; }
        catch { connection.Dispose(); throw; }
    }

    private static string ConnectionString(string path) => new SqliteConnectionStringBuilder
    {
        DataSource = path,
        Mode = SqliteOpenMode.ReadWriteCreate,
        Pooling = false,
        DefaultTimeout = 10
    }.ToString();

    private static ScreenshotItem? ReadOne(SqliteConnection connection, SqliteTransaction? transaction, string id)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT item_json FROM screenshots WHERE id=$id;";
        command.Parameters.AddWithValue("$id", id);
        var json = command.ExecuteScalar() as string;
        return json is null ? null : Deserialize(json);
    }

    private static List<ScreenshotItem> ReadMany(SqliteConnection connection, SqliteTransaction? transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return ReadMany(command);
    }

    private static List<ScreenshotItem> ReadMany(SqliteCommand command)
    {
        using var reader = command.ExecuteReader();
        var items = new List<ScreenshotItem>();
        while (reader.Read()) items.Add(Deserialize(reader.GetString(0)));
        return items;
    }

    private static ScreenshotItem Deserialize(string json) =>
        JsonSerializer.Deserialize<ScreenshotItem>(json, JsonOptions) ?? throw new InvalidDataException("资料库记录损坏。");

    private static void Save(SqliteConnection connection, SqliteTransaction transaction, ScreenshotItem item, bool insert = false)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = insert
            ? """
              INSERT INTO screenshots(id,created_utc,title,notes,tags,ocr_text,favorite,deleted,ocr_status,generation,item_json)
              VALUES($id,$created,$title,$notes,$tags,$text,$favorite,$deleted,$status,$generation,$json);
              """
            : """
              UPDATE screenshots SET created_utc=$created,title=$title,notes=$notes,tags=$tags,ocr_text=$text,
              favorite=$favorite,deleted=$deleted,ocr_status=$status,generation=$generation,item_json=$json WHERE id=$id;
              """;
        command.Parameters.AddWithValue("$id", item.Id);
        command.Parameters.AddWithValue("$created", item.CreatedUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$title", item.Title ?? "");
        command.Parameters.AddWithValue("$notes", item.Notes ?? "");
        command.Parameters.AddWithValue("$tags", item.Tags ?? "");
        command.Parameters.AddWithValue("$text", item.OcrText ?? "");
        command.Parameters.AddWithValue("$favorite", item.IsFavorite ? 1 : 0);
        command.Parameters.AddWithValue("$deleted", item.IsDeleted ? 1 : 0);
        command.Parameters.AddWithValue("$status", item.OcrStatus ?? "Pending");
        command.Parameters.AddWithValue("$generation", item.OcrGeneration);
        command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(item, JsonOptions));
        command.ExecuteNonQuery();
    }

    private static string ValidateFilePath(string path)
    {
        var normalized = NormalizeRelativePath(path);
        if (normalized.Equals(DatabaseName, StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("manifest.json", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith("-wal", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith("-shm", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("图片路径不能指向资料库内部文件。");
        return normalized;
    }

    private static string NormalizeRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains(':') || path.Contains('\0'))
            throw new InvalidDataException("备份和图片路径必须位于资料库文件夹内。");
        var normalized = path.Replace('\\', '/');
        var parts = normalized.Split('/');
        if (parts.Any(p => string.IsNullOrWhiteSpace(p) || p is "." or ".." || p.EndsWith('.') || p.EndsWith(' ')))
            throw new InvalidDataException("备份和图片路径包含无效目录。");
        return normalized;
    }

    private static void EnsureEmptyDestination(string path)
    {
        if (File.Exists(path)) throw new IOException("恢复位置必须是新的空文件夹。");
        if (!Directory.Exists(path)) return;
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 || Directory.EnumerateFileSystemEntries(path).Any())
            throw new IOException("恢复位置必须是新的空文件夹，不能覆盖已有资料。");
    }

    private static void RejectReparsePoints(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        var current = root;
        foreach (var part in relative.Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, part);
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("备份不支持指向资料库外部的链接文件。");
        }
    }

    public void Dispose() => _gate.Dispose();
    private sealed record BackupManifest(int FormatVersion, DateTime CreatedUtc, int ItemCount);
}
