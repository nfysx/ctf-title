using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MoeCTF.RE.Tutorial.Host.I18n;

public sealed class I18nService
{
	private readonly Dictionary<string, string> _map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	public string Locale { get; private set; } = "zh-CN";

	public void Load(string locale, string i18nDir, string? fallbackDir = null)
	{
		Locale = locale;
		_map.Clear();
		if (!string.IsNullOrWhiteSpace(fallbackDir))
		{
			LoadDirectory(locale, fallbackDir);
		}
		LoadDirectory(locale, i18nDir);
	}

	private void LoadDirectory(string locale, string i18nDir)
	{
		string path = Path.Combine(i18nDir, locale + ".json");
		if (!File.Exists(path))
		{
			path = Path.Combine(i18nDir, "zh-CN.json");
		}
		if (!File.Exists(path))
		{
			return;
		}
		using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(path));
		foreach (JsonProperty item in jsonDocument.RootElement.EnumerateObject())
		{
			_map[item.Name] = item.Value.GetString() ?? item.Name;
		}
	}

	public string T(string key, IReadOnlyDictionary<string, string?>? args = null)
	{
		if (!_map.TryGetValue(key, out string value))
		{
			value = key;
		}
		if (args == null || args.Count == 0)
		{
			return value;
		}
		return Regex.Replace(value, "\\{(\\w+)\\}", delegate(Match m)
		{
			string value2 = m.Groups[1].Value;
			string value3;
			return (!args.TryGetValue(value2, out value3) || value3 == null) ? m.Value : value3;
		});
	}

	public string? GetRaw(string key)
	{
		if (!_map.TryGetValue(key, out string value))
		{
			return null;
		}
		return value;
	}
}
