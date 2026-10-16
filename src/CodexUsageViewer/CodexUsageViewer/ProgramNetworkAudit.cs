using System.IO;
using System.Text;
namespace CodexUsageViewer;
internal static class ProgramNetworkAudit
{
    public static string Write()
    {
        string path = Path.Combine(AppLogger.DirectoryPath, "Program Network Audit.txt");
        Directory.CreateDirectory(AppLogger.DirectoryPath);
        File.WriteAllText(path, @"Codex Usage Viewer 2.1.1
Quota: fixed initialize and account/rateLimits/read JSON-RPC requests to the official local codex app-server. Authentication remains inside that process.
Task statistics: reads local .codex/sessions, archived_sessions and session_index.jsonl. Reads metadata, model name and token_count events; other history is not deserialized for statistics.
Network: no added HTTP client, upload, download, analytics, or third-party service. Starts the official app-server with analytics.enabled=false, as in version 2.0.0.
Credentials: does not read auth.json, API keys, access tokens, cookies or browser data.
Storage: existing display settings, quota cache, rotating logs and this report remain in the application data directory. Task statistics are cached in memory only. CSV is written only to the location chosen by the user and includes task titles, IDs, directories and token counts.
Accounting: highest cumulative token snapshot per thread; duplicates are not summed. Missing usage is unknown. Cached input and reasoning output are subsets, not additional totals. Daily increments use China Standard Time. Subagents appear separately. Counts are not plan quota or a bill.", new UTF8Encoding(false));
        return path;
    }
}
