using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using CodexUsageViewer;

internal static class UsageTests
{
    private static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
    private static string Event(string time, long input, long cached, long output, long reasoning)
    { return "{\"type\":\"event_msg\",\"timestamp\":\"" + time + "\",\"payload\":{\"type\":\"token_count\",\"info\":{\"total_token_usage\":{\"input_tokens\":" + input + ",\"cached_input_tokens\":" + cached + ",\"output_tokens\":" + output + ",\"reasoning_output_tokens\":" + reasoning + ",\"total_tokens\":" + (input+output) + "}}}}\n"; }
    public static void Main(string[] args)
    {
        string root = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(Path.Combine(root, "sessions")); Directory.CreateDirectory(Path.Combine(root, "archived_sessions"));
        string file = Path.Combine(root, "sessions", "a.jsonl");
        string meta = "{\"type\":\"session_meta\",\"payload\":{\"id\":\"task-a\",\"cwd\":\"F:/project\",\"source\":\"vscode\"}}\n";
        string log = meta + Event("2026-10-14T15:59:59Z", 100, 50, 20, 10) + Event("2026-10-14T16:00:01Z", 160, 70, 30, 15) + Event("2026-10-14T16:00:02Z", 160, 70, 30, 15) + "{\"type\":\"event_msg\",\"payload\":{\"type\":\"token_count\",\"info\":null}}\n";
        File.WriteAllText(file, log); File.WriteAllText(Path.Combine(root, "archived_sessions", "copy.jsonl"), log);
        File.WriteAllText(Path.Combine(root, "session_index.jsonl"), "{\"id\":\"task-a\",\"thread_name\":\"测试任务\"}\n");
        var store = new TaskUsageStore(); var result = store.Load(root); var task = result.Tasks.Single();
        Check(result.Tasks.Count == 1 && task.Counts.Total == 190, "deduplicate snapshots and active/archive copies");
        Check(task.Title == "测试任务", "resolve task title");
        Check(task.Counts.Input == 160 && task.Counts.Output == 30 && task.Counts.Cached == 70 && task.Counts.Reasoning == 15, "cached/reasoning subsets not double-counted");
        Check(task.Days[new DateTime(2026,10,14)].Total == 120 && task.Days[new DateTime(2026,10,15)].Total == 70, "China midnight daily deltas");
        Check(TaskUsageWindow.SelectCounts(task,new DateTime(2026,10,15),new DateTime(2026,10,15)).Total == 70, "date range selects increments not entire task");
        File.AppendAllText(file, Event("2026-10-15T02:00:00Z", 200, 80, 40, 20));
        Check(store.Load(root).Tasks.Single().Counts.Total == 240, "refresh invalidates changed file cache");
        File.AppendAllText(file, Event("2026-10-15T03:00:00Z", 100, 50, 20, 10) + "{\"type\":\"token_count\"");
        result = store.Load(root); Check(result.Tasks.Single().Counts.Total == 240 && result.Regressions == 1 && result.MalformedLines == 1, "regression and partial line keep known totals");
        File.WriteAllText(Path.Combine(root,"sessions","unknown.jsonl"), "{\"type\":\"session_meta\",\"payload\":{\"id\":\"unknown\"}}\n");
        File.WriteAllText(Path.Combine(root,"sessions","child.jsonl"), "{\"type\":\"session_meta\",\"payload\":{\"id\":\"child\",\"source\":{\"subagent\":{}}}}\n" + Event("2026-10-15T03:00:00Z", 10, 0, 1, 0));
        result = store.Load(root); Check(!result.Tasks.Single(t => t.Id == "unknown").HasUsage && result.Tasks.Single(t => t.Id == "child").Subagent, "unknown usage and subagent distinction");
        Check(store.Load(Path.Combine(root,"missing")).MissingSessions, "missing sessions is visible");
        Console.WriteLine("All 9 accounting checks passed.");
    }
}
