using System;

namespace CodexUsageViewer.Usage;

internal sealed class UsageException : Exception
{
	public UsageError Error { get; private set; }

	public string UserMessage => MessageFor(Error);

	public UsageException(UsageError error, string message)
		: base(message)
	{
		Error = error;
	}

	public UsageException(UsageError error, string message, Exception innerException)
		: base(message, innerException)
	{
		Error = error;
	}

	internal static string MessageFor(UsageError error)
	{
		switch (error)
		{
		case UsageError.NetworkUnavailable:
			return Localization.Get("NetworkFailed");
		case UsageError.Timeout:
			return Localization.Get("RequestTimeout");
		case UsageError.NotSignedIn:
			return Localization.Get("NotSignedIn");
		case UsageError.FormatError:
		case UsageError.InvalidData:
			return Localization.Get("InvalidData");
		default:
			return Localization.Get("CannotReadUsage");
		}
	}
}
