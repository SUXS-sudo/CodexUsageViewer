using System;
using System.IO;
using System.Text;

namespace CodexUsageViewer;

internal static class AppLogger
{
	private const long MaxBytes = 524288L;

	private static readonly object Gate = new object();

	internal static readonly string DirectoryPath = ResolveDirectoryPath();

	internal static readonly string LogPath = Path.Combine(DirectoryPath, "CodexUsageViewer.log");

	private static string ResolveDirectoryPath()
	{
		string environmentVariable = Environment.GetEnvironmentVariable("CODEX_USAGE_VIEWER_DATA_DIR");
		if (!string.IsNullOrWhiteSpace(environmentVariable))
		{
			return environmentVariable;
		}
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexUsageViewer");
	}

	public static void Info(string message)
	{
		Write("INFO", message, null);
	}

	public static void Error(string message, Exception exception)
	{
		Write("ERROR", message, exception);
	}

	private static void Write(string level, string message, Exception exception)
	{
		string contents = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz") + " [" + level + "] " + message + ((exception == null) ? string.Empty : (" | " + exception.GetType().Name)) + Environment.NewLine;
		try
		{
			lock (Gate)
			{
				Directory.CreateDirectory(DirectoryPath);
				if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 524288)
				{
					string text = LogPath + ".1";
					if (File.Exists(text))
					{
						File.Delete(text);
					}
					File.Move(LogPath, text);
				}
				File.AppendAllText(LogPath, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			}
		}
		catch
		{
			try
			{
				File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CodexUsageViewer.runtime.log"), contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			}
			catch
			{
			}
		}
	}
}
