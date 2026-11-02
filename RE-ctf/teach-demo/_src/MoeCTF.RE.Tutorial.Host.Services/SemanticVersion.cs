using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace MoeCTF.RE.Tutorial.Host.Services;

internal sealed class SemanticVersion : IComparable<SemanticVersion>
{
	private static readonly Regex Pattern = new Regex("^(0|[1-9]\\d*)\\.(0|[1-9]\\d*)\\.(0|[1-9]\\d*)(?:-([0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*))?(?:\\+[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*)?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private readonly string[] _prerelease;

	public int Major { get; }

	public int Minor { get; }

	public int Patch { get; }

	private SemanticVersion(int major, int minor, int patch, string[] prerelease)
	{
		Major = major;
		Minor = minor;
		Patch = patch;
		_prerelease = prerelease;
	}

	public static bool TryParse(string? value, out SemanticVersion version)
	{
		version = null;
		if (string.IsNullOrWhiteSpace(value))
		{
			return false;
		}
		Match match = Pattern.Match(value.Trim());
		if (!match.Success || !int.TryParse(match.Groups[1].Value, out var result) || !int.TryParse(match.Groups[2].Value, out var result2) || !int.TryParse(match.Groups[3].Value, out var result3))
		{
			return false;
		}
		string[] array = (match.Groups[4].Success ? match.Groups[4].Value.Split('.') : Array.Empty<string>());
		if (array.Any((string part) => part.Length == 0 || (part.All(char.IsDigit) && part.Length > 1 && part[0] == '0')))
		{
			return false;
		}
		version = new SemanticVersion(result, result2, result3, array);
		return true;
	}

	public int CompareTo(SemanticVersion? other)
	{
		if (other == null)
		{
			return 1;
		}
		int num = Major.CompareTo(other.Major);
		if (num == 0)
		{
			num = Minor.CompareTo(other.Minor);
		}
		if (num == 0)
		{
			num = Patch.CompareTo(other.Patch);
		}
		if (num != 0)
		{
			return num;
		}
		if (_prerelease.Length == 0)
		{
			return (other._prerelease.Length != 0) ? 1 : 0;
		}
		if (other._prerelease.Length == 0)
		{
			return -1;
		}
		for (int i = 0; i < Math.Min(_prerelease.Length, other._prerelease.Length); i++)
		{
			string text = _prerelease[i];
			string text2 = other._prerelease[i];
			int result;
			bool flag = int.TryParse(text, out result);
			int result2;
			bool flag2 = int.TryParse(text2, out result2);
			int num2 = ((flag && flag2) ? result.CompareTo(result2) : (flag ? (-1) : (flag2 ? 1 : string.CompareOrdinal(text, text2))));
			if (num2 != 0)
			{
				return num2;
			}
		}
		return _prerelease.Length.CompareTo(other._prerelease.Length);
	}

	public static int Compare(string? left, string? right)
	{
		if (!TryParse(left, out SemanticVersion version))
		{
			throw new FormatException("Invalid semantic version: " + left);
		}
		if (!TryParse(right, out SemanticVersion version2))
		{
			throw new FormatException("Invalid semantic version: " + right);
		}
		return version.CompareTo(version2);
	}
}
