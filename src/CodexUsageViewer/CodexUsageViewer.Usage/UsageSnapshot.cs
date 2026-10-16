namespace CodexUsageViewer.Usage;

internal sealed class UsageSnapshot
{
	public UsageWindow ShortWindow { get; private set; }

	public UsageWindow LongWindow { get; private set; }

	public UsageSnapshot(UsageWindow shortWindow, UsageWindow longWindow)
	{
		ShortWindow = shortWindow;
		LongWindow = longWindow;
	}
}
