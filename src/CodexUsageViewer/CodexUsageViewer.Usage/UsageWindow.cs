namespace CodexUsageViewer.Usage;

internal sealed class UsageWindow
{
	public int UsedPercent { get; private set; }

	public long ResetsAt { get; private set; }

	public long DurationMinutes { get; private set; }

	public UsageWindow(int usedPercent, long resetsAt, long durationMinutes)
	{
		UsedPercent = usedPercent;
		ResetsAt = resetsAt;
		DurationMinutes = durationMinutes;
	}
}
