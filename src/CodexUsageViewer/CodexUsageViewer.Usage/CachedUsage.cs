using System;

namespace CodexUsageViewer.Usage;

internal sealed class CachedUsage
{
	public int? ShortRemaining { get; private set; }

	public long? ShortResetUnixSeconds { get; private set; }

	public int? LongRemaining { get; private set; }

	public long? LongResetUnixSeconds { get; private set; }

	public DateTimeOffset SuccessAt { get; private set; }

	public CachedUsage(int? shortRemaining, long? shortReset, int? longRemaining, long? longReset, DateTimeOffset successAt)
	{
		ShortRemaining = shortRemaining;
		ShortResetUnixSeconds = shortReset;
		LongRemaining = longRemaining;
		LongResetUnixSeconds = longReset;
		SuccessAt = successAt;
	}
}
