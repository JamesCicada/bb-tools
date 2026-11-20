using System.IO;
using System.IO.Compression;
using System.Text.Json;
using BBTools;
internal static partial class Program
{
    private static void LoadoutChecks()
    {
        var vial = new LoadoutItem("Blood Vial", 1000, 0x40000000, 20);
        var cleaver = new LoadoutItem("Saw Cleaver +10", 7000010, 0, 1);
        var kit = new ItemLoadout("Boss kit", [vial, cleaver]);
        kit.Validate();
        Reject<ArgumentException>(() => new ItemLoadout("Empty", []).Validate(), "loadouts reject an empty item list");
        Reject<ArgumentException>(() => new ItemLoadout("Too many", Enumerable.Repeat(vial, ItemLoadout.MaxItems + 1).ToArray()).Validate(), "loadouts are capped at the documented item count");
        Reject<ArgumentException>(() => new ItemLoadout("Bad", [vial with { Quantity = 2, Category = 0 }]).Validate(), "loadout items reuse single-spawn rules (equipment quantity 1)");
        Reject<ArgumentException>(() => new ItemLoadout(" padded", [vial]).Validate(), "loadout names follow library naming rules");
        var edited = kit.With(vial with { Name = "Antidote", Id = 1100, Quantity = 3 }).Without(0);
        Check(edited.Items.Select(x => x.Name).SequenceEqual(new[] { "Saw Cleaver +10", "Antidote" }) && kit.Items.Length == 2, "loadout edits return new copies in order without changing the original");
        Reject<ArgumentException>(() => kit.Without(5), "removing a missing loadout item is rejected");

        var spawned = new List<int>();
        var count = LoadoutSpawner.SpawnAsync(kit, item => { spawned.Add(item.Id); return Task.CompletedTask; }).GetAwaiter().GetResult();
        Check(count == 2 && spawned.SequenceEqual(new[] { 1000, 7000010 }), "loadout spawns every item once, in order");
        spawned.Clear();
        var three = kit.With(vial with { Name = "Antidote", Id = 1100 });
        try
        {
            LoadoutSpawner.SpawnAsync(three, item => { spawned.Add(item.Id); return item.Id == 7000010 ? Task.FromException(new IOException("Game request timed out.")) : Task.CompletedTask; }).GetAwaiter().GetResult();
            throw new Exception("failed loadout reported success");
        }
        catch (LoadoutSpawnException ex)
        {
            Check(ex.Spawned == 1 && spawned.SequenceEqual(new[] { 1000, 7000010 }) && ex.Message.Contains("1 of 3"), "a failed loadout stops at the failing item, never retries it and reports progress");
        }

        var folder = Path.Combine(Path.GetTempPath(), "BBTools-loadouts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var library = new UserLibrary(Path.Combine(folder, "library.db"), Path.Combine(folder, "none.json"));
            library.Save(kit);
            Check(library.Loadouts().Single().Items.SequenceEqual(kit.Items), "loadouts persist in the SQLite library");
            var export = Path.Combine(folder, "export.json"); library.Export(export);
            var imported = new UserLibrary(Path.Combine(folder, "target.db"), Path.Combine(folder, "none.json")); imported.Import(export);
            Check(imported.Loadouts().Single().Items.SequenceEqual(kit.Items), "library export and import carry loadouts");
            var legacy = Path.Combine(folder, "legacy.json");
            File.WriteAllText(legacy, JsonSerializer.Serialize(new { Version = 1, Presets = Array.Empty<object>(), Locations = Array.Empty<object>(), Favorites = Array.Empty<object>() }));
            imported.Import(legacy);
            Check(imported.Loadouts().Length == 1, "older library exports without loadouts still import");
            library.Delete("loadout", kit.Name);
            Check(library.Loadouts().Length == 0, "deleted loadouts are removed");
        }
        finally { Directory.Delete(folder, true); }
    }

    private static void BugReportChecks()
    {
        Check(BugReport.Redact(@"Log at C:\Users\Hunter\AppData\x.log by HUNTER; Hunters stay", @"C:\Users\Hunter", "Hunter") == "Log at ~\\AppData\\x.log by [user]; Hunters stay",
            "bug reports replace the home folder and whole-word account name only");
        Check(BugReport.Redact("/home/hunter/.local/bbtools", "/home/hunter", "hunter") == "~/.local/bbtools", "bug reports redact Linux home folders");
        var folder = Path.Combine(Path.GetTempPath(), "BBTools-report-" + Guid.NewGuid().ToString("N"));
        var logs = Path.Combine(folder, "logs"); Directory.CreateDirectory(logs);
        try
        {
            string Log(string stamp) => Path.Combine(logs, $"BBTools-{stamp}Z-{new string('a', 32)}.log");
            var older = Log("20261101T0000000000000"); var newer = Log("20261102T0000000000000"); var newest = Log("20261103T0000000000000"); var current = Log("20261001T0000000000000");
            foreach (var path in new[] { older, newer, newest }) File.WriteAllText(path, "old session");
            File.WriteAllText(Path.Combine(logs, "notes.log"), "unrelated");
            File.WriteAllText(current, new string('x', BugReport.MaxLogBytes) + "\nlast line by " + Environment.UserName);
            Check(BugReport.RecentLogs(current).SequenceEqual(new[] { current, newest, newer }), "bug reports include the current log first, then the newest earlier session logs");
            Check(BugReport.RecentLogs(null).Length == 0, "bug reports work without session logging");
            using (new FileStream(current, FileMode.Open, FileAccess.Write, FileShare.Read | FileShare.Delete))
            {
                var tail = BugReport.ReadTail(current);
                Check(tail.StartsWith("[") && tail.Contains("earlier bytes omitted") && tail.EndsWith("last line by " + Environment.UserName) && tail.Length < BugReport.MaxLogBytes, "the active session log is read while open and capped to its newest bytes");
            }
            var zipPath = Path.Combine(folder, "report.zip");
            var included = BugReport.Create(zipPath, new BugReportContext("Windows", current, true, false, "bbprobe", "Status from " + Environment.UserName, "en",
                new Dictionary<string, string> { ["Desktop"] = "test" }), new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc));
            using var zip = ZipFile.OpenRead(zipPath);
            var names = zip.Entries.Select(x => x.FullName).OrderBy(x => x).ToArray();
            var expected = new[] { current, newest, newer }.Select(x => "logs/" + Path.GetFileName(x)).Append("report.json").OrderBy(x => x).ToArray();
            Check(included == 3 && names.SequenceEqual(expected), "bug report ZIP holds report.json and only the selected session logs");
            using var reader = new StreamReader(zip.GetEntry("report.json")!.Open());
            var report = JsonDocument.Parse(reader.ReadToEnd()).RootElement;
            Check(report.GetProperty("AppVersion").GetString() == AppInfo.DisplayVersion && report.GetProperty("ProcessName").GetString() == "bbprobe" &&
                report.GetProperty("LastStatus").GetString() == "Status from [user]" && report.GetProperty("IncludedLogs").GetArrayLength() == 3,
                "bug report metadata names the version, process and redacted status");
            using var log = new StreamReader(zip.GetEntry("logs/" + Path.GetFileName(current))!.Open());
            Check(log.ReadToEnd().EndsWith("last line by [user]"), "included session logs are redacted");
            Check(!Directory.EnumerateFiles(folder, "*.tmp").Any(), "bug report export leaves no temporary files");
        }
        finally { Directory.Delete(folder, true); }
    }
}
