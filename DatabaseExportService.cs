using System.IO.Compression;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;

namespace AHS.Mobile.Services;

/// <summary>
/// Exports a consistent snapshot of the local SQLite database, bundles it with a
/// small diagnostic manifest, and hands it to the OS share sheet so the user can
/// send it through whatever channel they have available (email, Teams, cloud
/// storage, etc.) without needing a cable or a debug build.
/// </summary>
public interface IDatabaseExportService
{
    /// <summary>
    /// Creates the export bundle and invokes the share sheet. Completes once the
    /// share sheet is dismissed — MAUI's Share API does not report which target
    /// (if any) the user picked, or whether they cancelled, so this does not throw
    /// or return a failure value for "the user backed out." It throws only on a
    /// genuine failure to read/export the database itself.
    /// </summary>
    Task ExportAndShareAsync(CancellationToken cancellationToken = default);
}

/// <param name="databasePath">
/// Full path to the app's local SQLite file. Wire this to however the app already
/// resolves that path today (an existing constants class, connection-string
/// provider, etc.) — see the DI registration note below.
/// </param>
public sealed class DatabaseExportService(string databasePath) : IDatabaseExportService
{
    public async Task ExportAndShareAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(databasePath))
            throw new FileNotFoundException("Local database was not found.", databasePath);

        var workDir = Path.Combine(FileSystem.Current.CacheDirectory, "db-export");
        Directory.CreateDirectory(workDir);

        // Clean up whatever a previous export left behind. We deliberately do NOT
        // delete the zip right after handing it to the share sheet below — the
        // receiving app (Mail, Teams, etc.) may still be reading it asynchronously
        // for a moment after the sheet is dismissed, so an immediate delete can
        // race it. Cleaning up here, at the start of the *next* export, avoids that
        // race entirely while still not leaving exports piling up indefinitely.
        foreach (var existing in Directory.GetFiles(workDir))
        {
            File.Delete(existing);
        }

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var snapshotPath = Path.Combine(workDir, $"app-{timestamp}.db");
        var manifestPath = Path.Combine(workDir, "manifest.json");
        var zipPath = Path.Combine(workDir, $"AHSMobile-DbExport-{timestamp}.zip");

        // VACUUM INTO produces a clean, consistent, compacted snapshot even while
        // the app's live connection is open elsewhere (including WAL mode). A plain
        // File.Copy can grab a half-written file or miss WAL content that hasn't
        // been checkpointed yet — don't replace this with a raw file copy.
        await using (var connection = new SqliteConnection($"Data Source={databasePath}"))
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "VACUUM INTO $target;";
            command.Parameters.AddWithValue("$target", snapshotPath);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var manifest = new
        {
            ExportedAtUtc = DateTime.UtcNow,
            AppVersion = AppInfo.Current.VersionString,
            AppBuild = AppInfo.Current.BuildString,
            Platform = DeviceInfo.Current.Platform.ToString(),
            DeviceModel = DeviceInfo.Current.Model,
            OsVersion = DeviceInfo.Current.VersionString,
        };
        await File.WriteAllTextAsync(
            manifestPath,
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);

        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            zip.CreateEntryFromFile(snapshotPath, Path.GetFileName(snapshotPath));
            zip.CreateEntryFromFile(manifestPath, Path.GetFileName(manifestPath));
        }

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "Share diagnostic database export",
            File = new ShareFile(zipPath),
        });
    }
}
