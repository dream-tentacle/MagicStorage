using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HaulersDream;

internal static class MiniJson
{
	public static object Parse(string json)
	{
		if (string.IsNullOrEmpty(json))
		{
			return null;
		}
		try
		{
			int i = 0;
			return ParseValue(json, ref i);
		}
		catch
		{
			return null;
		}
	}

	private static object ParseValue(string s, ref int i)
	{
		SkipWs(s, ref i);
		switch (s[i])
		{
		case '{':
			return ParseObject(s, ref i);
		case '[':
			return ParseArray(s, ref i);
		case '"':
			return ParseString(s, ref i);
		case 't':
			i += 4;
			return true;
		case 'f':
			i += 5;
			return false;
		case 'n':
			i += 4;
			return null;
		default:
			return ParseNumber(s, ref i);
		}
	}

	private static Dictionary<string, object> ParseObject(string s, ref int i)
	{
		Dictionary<string, object> dictionary = new Dictionary<string, object>();
		i++;
		SkipWs(s, ref i);
		if (s[i] == '}')
		{
			i++;
			return dictionary;
		}
		char c;
		do
		{
			SkipWs(s, ref i);
			string key = ParseString(s, ref i);
			SkipWs(s, ref i);
			i++;
			dictionary[key] = ParseValue(s, ref i);
			SkipWs(s, ref i);
			c = s[i++];
		}
		while (c == ',' || c != '}');
		return dictionary;
	}

	private static List<object> ParseArray(string s, ref int i)
	{
		List<object> list = new List<object>();
		i++;
		SkipWs(s, ref i);
		if (s[i] == ']')
		{
			i++;
			return list;
		}
		char c;
		do
		{
			list.Add(ParseValue(s, ref i));
			SkipWs(s, ref i);
			c = s[i++];
		}
		while (c == ',' || c != ']');
		return list;
	}

	private static string ParseString(string s, ref int i)
	{
		StringBuilder stringBuilder = new StringBuilder();
		i++;
		while (true)
		{
			char c = s[i++];
			switch (c)
			{
			case '\\':
			{
				char c2 = s[i++];
				switch (c2)
				{
				case '"':
					stringBuilder.Append('"');
					break;
				case '\\':
					stringBuilder.Append('\\');
					break;
				case '/':
					stringBuilder.Append('/');
					break;
				case 'n':
					stringBuilder.Append('\n');
					break;
				case 'r':
					stringBuilder.Append('\r');
					break;
				case 't':
					stringBuilder.Append('\t');
					break;
				case 'b':
					stringBuilder.Append('\b');
					break;
				case 'f':
					stringBuilder.Append('\f');
					break;
				case 'u':
				{
					int num = int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
					stringBuilder.Append((char)num);
					i += 4;
					break;
				}
				default:
					stringBuilder.Append(c2);
					break;
				}
				break;
			}
			default:
				stringBuilder.Append(c);
				break;
			case '"':
				return stringBuilder.ToString();
			}
		}
	}

	private static object ParseNumber(string s, ref int i)
	{
		int num = i;
		while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0)
		{
			i++;
		}
		double.TryParse(s.Substring(num, i - num), NumberStyles.Float, CultureInfo.InvariantCulture, out var result);
		return result;
	}

	private static void SkipWs(string s, ref int i)
	{
		while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r'))
		{
			i++;
		}
	}
}
