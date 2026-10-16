using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace CodexUsageViewer.Usage;

internal static class UsageCache
{
	[DataContract]
	private sealed class CacheDocument
	{
		[DataMember(Name = "version")]
		public int Version { get; set; }

		[DataMember(Name = "fiveHourRemaining")]
		public int? ShortRemaining { get; set; }

		[DataMember(Name = "fiveHourResetAt")]
		public long? ShortResetUnixSeconds { get; set; }

		[DataMember(Name = "weekRemaining")]
		public int? LongRemaining { get; set; }

		[DataMember(Name = "weekResetAt")]
		public long? LongResetUnixSeconds { get; set; }

		[DataMember(Name = "lastSuccessAt")]
		public long SuccessUnixSeconds { get; set; }
	}

	private const int Version = 1;

	private static readonly string FilePath = Path.Combine(AppLogger.DirectoryPath, "usage-cache.json");

	public static CachedUsage Load()
	{
		try
		{
			if (!File.Exists(FilePath))
			{
				return null;
			}
			using FileStream stream = File.OpenRead(FilePath);
			CacheDocument cacheDocument = (CacheDocument)new DataContractJsonSerializer(typeof(CacheDocument)).ReadObject((Stream)stream);
			if (cacheDocument == null || cacheDocument.Version != 1 || cacheDocument.SuccessUnixSeconds <= 0)
			{
				return null;
			}
			if (!Valid(cacheDocument.ShortRemaining) || !Valid(cacheDocument.LongRemaining))
			{
				return null;
			}
			return new CachedUsage(cacheDocument.ShortRemaining, cacheDocument.ShortResetUnixSeconds, cacheDocument.LongRemaining, cacheDocument.LongResetUnixSeconds, DateTimeOffset.FromUnixTimeSeconds(cacheDocument.SuccessUnixSeconds));
		}
		catch (Exception exception)
		{
			AppLogger.Error("Cache read failed", exception);
			return null;
		}
	}

	public static void Save(CachedUsage value)
	{
		try
		{
			Directory.CreateDirectory(AppLogger.DirectoryPath);
			string text = FilePath + ".tmp";
			CacheDocument cacheDocument = new CacheDocument();
			cacheDocument.Version = 1;
			cacheDocument.ShortRemaining = value.ShortRemaining;
			cacheDocument.ShortResetUnixSeconds = value.ShortResetUnixSeconds;
			cacheDocument.LongRemaining = value.LongRemaining;
			cacheDocument.LongResetUnixSeconds = value.LongResetUnixSeconds;
			cacheDocument.SuccessUnixSeconds = value.SuccessAt.ToUnixTimeSeconds();
			CacheDocument graph = cacheDocument;
			using (FileStream stream = File.Create(text))
			{
				new DataContractJsonSerializer(typeof(CacheDocument)).WriteObject((Stream)stream, (object)graph);
			}
			if (File.Exists(FilePath))
			{
				File.Delete(FilePath);
			}
			File.Move(text, FilePath);
		}
		catch (Exception exception)
		{
			AppLogger.Error("Cache write failed", exception);
		}
	}

	private static bool Valid(int? value)
	{
		if (value.HasValue)
		{
			if (value.Value >= 0)
			{
				return value.Value <= 100;
			}
			return false;
		}
		return true;
	}
}
