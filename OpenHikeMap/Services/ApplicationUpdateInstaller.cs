using System.Diagnostics;
using System.Text.Json;

namespace OpenHikeMap.Services;

public sealed record UpdateJob(string Token, string Target, string Payload, string Backup, string OldSha256, string NewSha256, string Version, string Language, int ParentId, long ParentStartedTicks);
public sealed record UpdateHelperResult(bool Success, string Language);

public static class ApplicationUpdateInstaller
{
    public static string Root => Path.Combine(Environment.GetEnvironmentVariable("OPENHIKEMAP_DATA_DIRECTORY") is { Length: > 0 } testDirectory
        ? testDirectory : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenHikeMap"), "Updates");

    public static string CreateStage()
    {
        var directory = Path.Combine(Root, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory); return directory;
    }
    public static string RunningExecutable()
    {
        var target = Environment.ProcessPath ?? throw new InvalidOperationException("Application executable not found.");
        ApplicationUpdateService.ValidateExecutable(target);
        var probe = Path.Combine(Path.GetDirectoryName(target)!, ".openhikemap-write-" + Guid.NewGuid().ToString("N"));
        using (File.Create(probe)) { } File.Delete(probe);
        return Path.GetFullPath(target);
    }
    public static async Task<string> PrepareAsync(string stage, string target, DownloadedUpdate download, Version version, string language, CancellationToken token)
    {
        ValidateStage(stage);
        var nonce = Path.GetFileName(stage);
        if (!SamePath(download.ExecutablePath, Path.Combine(stage, "OpenHikeMap.new.exe"))) throw new InvalidDataException("Unexpected update payload path.");
        ApplicationUpdateService.ValidateExecutable(target); ApplicationUpdateService.ValidateExecutable(download.ExecutablePath, version);
        var oldHash = await ApplicationUpdateService.HashAsync(target, token).ConfigureAwait(false);
        var backup = Path.Combine(Path.GetDirectoryName(target)!, "." + Path.GetFileNameWithoutExtension(target) + "." + nonce + ".previous.exe");
        using var parent = Process.GetCurrentProcess();
        var job = new UpdateJob(nonce, target, download.ExecutablePath, backup, oldHash, download.Sha256, version.ToString(4), language, parent.Id, parent.StartTime.ToUniversalTime().Ticks);
        File.Copy(target, Path.Combine(stage, "OpenHikeMap.Update.exe"));
        var path = Path.Combine(stage, "job.json"); await File.WriteAllTextAsync(path, JsonSerializer.Serialize(job), token).ConfigureAwait(false); return path;
    }
    public static async Task LaunchAsync(string jobPath, CancellationToken token)
    {
        var job = LoadJob(jobPath); var stage = Path.GetDirectoryName(jobPath)!;
        var start = new ProcessStartInfo(Path.Combine(stage, "OpenHikeMap.Update.exe")) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = stage };
        start.ArgumentList.Add("--apply-update"); start.ArgumentList.Add(jobPath); start.ArgumentList.Add(job.Token);
        using var helper = Process.Start(start) ?? throw new IOException("Unable to start the updater.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            await File.WriteAllTextAsync(Path.Combine(stage, "helper.pid"), helper.Id.ToString(), timeout.Token).ConfigureAwait(false);
            var ready = Path.Combine(stage, "ready");
            while (!File.Exists(ready)) { if (helper.HasExited) throw new IOException("Updater exited before it was ready."); await Task.Delay(100, timeout.Token).ConfigureAwait(false); }
            if (await File.ReadAllTextAsync(ready, timeout.Token).ConfigureAwait(false) != job.Token) throw new InvalidDataException("Updater handshake failed.");
        }
        catch { await File.WriteAllTextAsync(Path.Combine(stage, "cancelled"), job.Token, CancellationToken.None).ConfigureAwait(false); throw; }
    }
    public static void Commit(string jobPath)
    {
        var job = LoadJob(jobPath); File.WriteAllText(Path.Combine(Path.GetDirectoryName(jobPath)!, "commit"), job.Token);
    }

    public static async Task<UpdateHelperResult> RunHelperAsync(string[] args)
    {
        UpdateJob? job = null; var installed = false;
        try
        {
            if (args.Length != 3) throw new InvalidDataException("Invalid updater arguments.");
            job = LoadJob(args[1]); var stage = Path.GetDirectoryName(args[1])!;
            if (args[2] != job.Token || !SamePath(Environment.ProcessPath!, Path.Combine(stage, "OpenHikeMap.Update.exe"))) throw new InvalidDataException("Updater identity mismatch.");
            ApplicationUpdateService.ValidateExecutable(job.Payload, ParseVersion(job.Version));
            if (!string.Equals(await ApplicationUpdateService.HashAsync(job.Payload).ConfigureAwait(false), job.NewSha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update payload changed.");
            await File.WriteAllTextAsync(Path.Combine(stage, "ready"), job.Token).ConfigureAwait(false);
            using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            try
            {
                using var parent = Process.GetProcessById(job.ParentId);
                if (parent.StartTime.ToUniversalTime().Ticks == job.ParentStartedTicks)
                {
                    if (!SamePath(parent.MainModule?.FileName ?? "", job.Target)) throw new InvalidDataException("Original application identity mismatch.");
                    while (!parent.HasExited)
                    {
                        if (File.Exists(Path.Combine(stage, "cancelled"))) return new(true, job.Language);
                        await Task.Delay(100, deadline.Token).ConfigureAwait(false);
                    }
                }
            }
            catch (ArgumentException) { /* The original process already exited. */ }
            if (File.Exists(Path.Combine(stage, "cancelled"))) return new(true, job.Language);
            var commit = Path.Combine(stage, "commit");
            if (!File.Exists(commit) || await File.ReadAllTextAsync(commit).ConfigureAwait(false) != job.Token) return new(true, job.Language);
            await ApplyFilesAsync(args[1], job.Token, deadline.Token).ConfigureAwait(false); installed = true;
            var start = new ProcessStartInfo(job.Target) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(job.Target)! };
            start.ArgumentList.Add("--update-finished"); start.ArgumentList.Add(args[1]); start.ArgumentList.Add(job.Token); start.ArgumentList.Add(Environment.ProcessId.ToString());
            using var restarted = Process.Start(start) ?? throw new IOException("Unable to restart OpenHikeMap.");
            await Task.Delay(2500).ConfigureAwait(false);
            if (restarted.HasExited && restarted.ExitCode != 0) throw new IOException("Updated application could not start.");
            TryDelete(job.Backup);
            return new(true, job.Language);
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Write("Application update failed.", ex);
            if (job is not null)
            {
                try
                {
                    if (installed)
                    {
                        if (!string.Equals(await ApplicationUpdateService.HashAsync(job.Backup).ConfigureAwait(false), job.OldSha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Previous executable checksum mismatch.");
                        File.Replace(job.Backup, job.Target, null, true);
                    }
                    var parentAlive = false;
                    try { using var parent = Process.GetProcessById(job.ParentId); parentAlive = !parent.HasExited && parent.StartTime.ToUniversalTime().Ticks == job.ParentStartedTicks; } catch (ArgumentException) { }
                    if (!parentAlive && string.Equals(await ApplicationUpdateService.HashAsync(job.Target).ConfigureAwait(false), job.OldSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        var restore = new ProcessStartInfo(job.Target) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(job.Target)! }; restore.ArgumentList.Add("--skip-update-check"); Process.Start(restore);
                    }
                }
                catch (Exception restoreError) { StartupDiagnostics.Write("Unable to restore the previous executable; backup retained at " + job.Backup, restoreError); }
            }
            return new(false, job?.Language ?? "en-US");
        }
    }

    public static async Task ApplyFilesAsync(string jobPath, string token, CancellationToken cancellationToken = default)
    {
        var job = LoadJob(jobPath);
        ApplicationUpdateService.ValidateExecutable(job.Target);
        if (token != job.Token || !string.Equals(await ApplicationUpdateService.HashAsync(job.Target, cancellationToken).ConfigureAwait(false), job.OldSha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The original executable changed; update cancelled.");
        ApplicationUpdateService.ValidateExecutable(job.Payload, ParseVersion(job.Version));
        var temporary = Path.Combine(Path.GetDirectoryName(job.Target)!, "." + Path.GetFileName(job.Target) + "." + job.Token + ".update");
        try
        {
            await using (var input = File.OpenRead(job.Payload))
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            { await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false); await output.FlushAsync(cancellationToken).ConfigureAwait(false); output.Flush(true); }
            if (!string.Equals(await ApplicationUpdateService.HashAsync(temporary, cancellationToken).ConfigureAwait(false), job.NewSha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Executable checksum mismatch.");
            if (File.Exists(job.Backup)) throw new IOException("Update backup already exists.");
            File.Replace(temporary, job.Target, job.Backup, true);
        }
        finally { TryDelete(temporary); }
    }

    public static void CompleteRestart(string[] args)
    {
        if (args.Length != 4 || args[0] != "--update-finished") return;
        _ = Task.Run(async () =>
        {
            try
            {
                var job = LoadJob(args[1]); if (args[2] != job.Token || !SamePath(job.Target, Environment.ProcessPath!)) return;
                if (int.TryParse(args[3], out var id))
                    try { using var helper = Process.GetProcessById(id); using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30)); await helper.WaitForExitAsync(timeout.Token).ConfigureAwait(false); } catch (ArgumentException) { }
                TryDelete(job.Backup); CleanupStage(Path.GetDirectoryName(args[1])!);
            }
            catch (Exception ex) { StartupDiagnostics.Write("Update cache cleanup deferred.", ex); }
        });
    }
    public static void CleanupStage(string directory)
    {
        try { ValidateStage(directory); if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { StartupDiagnostics.Write("Update staging cleanup deferred.", ex); }
    }
    public static async Task CleanupStageAsync(string directory)
    {
        try
        {
            ValidateStage(directory);
            var pidFile = Path.Combine(directory, "helper.pid");
            if (File.Exists(pidFile) && int.TryParse(await File.ReadAllTextAsync(pidFile).ConfigureAwait(false), out var pid))
            {
                try
                {
                    using var helper = Process.GetProcessById(pid);
                    if (!helper.HasExited && SamePath(helper.MainModule?.FileName ?? "", Path.Combine(directory, "OpenHikeMap.Update.exe")))
                    { using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3)); await helper.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
                }
                catch (ArgumentException) { /* The helper already exited. */ }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or OperationCanceledException or System.ComponentModel.Win32Exception)
        { StartupDiagnostics.Write("Update staging cleanup deferred.", ex); return; }
        for (var attempt = 0; attempt < 15; attempt++)
        {
            try { ValidateStage(directory); if (Directory.Exists(directory)) Directory.Delete(directory, true); return; }
            catch (IOException) when (attempt < 14) { await Task.Delay(200).ConfigureAwait(false); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { StartupDiagnostics.Write("Update staging cleanup deferred.", ex); return; }
        }
    }
    private static UpdateJob LoadJob(string path)
    {
        var stage = Path.GetDirectoryName(Path.GetFullPath(path))!; ValidateStage(stage);
        if (Path.GetFileName(path) != "job.json") throw new InvalidDataException("Unexpected updater job file.");
        var job = JsonSerializer.Deserialize<UpdateJob>(File.ReadAllText(path)) ?? throw new InvalidDataException("Invalid update job.");
        var backup = Path.Combine(Path.GetDirectoryName(job.Target)!, "." + Path.GetFileNameWithoutExtension(job.Target) + "." + job.Token + ".previous.exe");
        if (job.Token != Path.GetFileName(stage) || !Path.IsPathFullyQualified(job.Target) || !SamePath(job.Payload, Path.Combine(stage, "OpenHikeMap.new.exe")) || !SamePath(job.Backup, backup) || SamePath(job.Target, job.Payload)) throw new InvalidDataException("Invalid updater paths.");
        return job;
    }
    private static void ValidateStage(string path)
    {
        var full = Path.GetFullPath(path);
        if (!SamePath(Path.GetDirectoryName(full)!, Root) || !Guid.TryParseExact(Path.GetFileName(full), "N", out _) || Directory.Exists(full) && (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Invalid update staging directory.");
    }
    private static Version ParseVersion(string value) => ApplicationUpdateService.TryVersion(value, out var version) ? version : throw new InvalidDataException("Invalid update version.");
    private static bool SamePath(string first, string second) => Path.GetFullPath(first).Equals(Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);
    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
}
