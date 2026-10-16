using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace CodexUsageViewer;

internal static class Localization
{
	public const string SystemLanguage = "system";

	public const string ChineseLanguage = "zh-CN";

	public const string EnglishLanguage = "en-US";

	private static readonly Dictionary<string, string> English = Load("CodexUsageViewer.Resources.Strings.en-US.txt");

	private static readonly Dictionary<string, string> Chinese = Load("CodexUsageViewer.Resources.Strings.zh-CN.txt");

	private static string preference = "system";

	public static string Preference => preference;

	public static string EffectiveLanguage => Resolve(preference, CultureInfo.CurrentUICulture);

	public static event EventHandler Changed;

	public static void SetPreference(string value)
	{
		string text = ((value == "zh-CN" || value == "en-US") ? value : "system");
		if (!(preference == text))
		{
			preference = text;
			Localization.Changed?.Invoke(null, EventArgs.Empty);
		}
	}

	public static string Get(string key)
	{
		if (EffectiveLanguage == "zh-CN" && Chinese.TryGetValue(key, out var value))
		{
			return value;
		}
		if (!English.TryGetValue(key, out value))
		{
			return "";
		}
		return value;
	}

	public static string Format(string key, params object[] values)
	{
		return string.Format(CultureInfo.CurrentCulture, Get(key), values);
	}

	internal static string Resolve(string value, CultureInfo systemCulture)
	{
		if (value == "zh-CN" || value == "en-US")
		{
			return value;
		}
		string text = ((systemCulture == null) ? "" : systemCulture.Name).ToLowerInvariant();
		if (!(text == "zh-cn") && !(text == "zh-sg") && !text.StartsWith("zh-hans"))
		{
			return "en-US";
		}
		return "zh-CN";
	}

	private static Dictionary<string, string> Load(string resourceName)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
		using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
		if (stream == null)
		{
			return dictionary;
		}
		using StreamReader streamReader = new StreamReader(stream, Encoding.UTF8);
		string text;
		while ((text = streamReader.ReadLine()) != null)
		{
			int num = text.IndexOf('=');
			if (num > 0 && !text.StartsWith("#"))
			{
				dictionary[text.Substring(0, num)] = text.Substring(num + 1);
			}
		}
		return dictionary;
	}
}
