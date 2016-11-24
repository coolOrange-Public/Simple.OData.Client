using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Simple.OData.Client.Adapter;

namespace Simple.OData.Client.Http
{
	public class SlugHeader
	{
		private readonly Dictionary<string, string> _fields;

		public SlugHeader()
		{
			_fields = new Dictionary<string, string>();
		}

		public SlugHeader(IDictionary<string,string> fields) :
			this()
		{
			foreach (var field in fields)
				Add(field.Key,field.Value);
		}

		public void Add(string fieldKey, string fieldValue)
		{
			string value = null;
			if (fieldValue != null)
				value = ToSlugText(fieldValue);
			
			_fields[fieldKey] = value;
		}

		public override string ToString()
		{
			Func<string, string> getValue = value =>
			{
				var doubleQuote = "\"";
				var quoted = value.StartsWith(doubleQuote) && value.EndsWith(doubleQuote);
				if (quoted)
					value = "'" + value.Remove(value.Length - doubleQuote.Length).Remove(0, doubleQuote.Length) + "'";
				return value;
			};
			return string.Join(",", _fields.Select(kvp => kvp.Key + "="+getValue(kvp.Value)));
		}


		static readonly HashSet<int> Rfc3986CharsNotToEscape = new HashSet<int>(Enumerable.Range(32, 36 - 32 + 1)
			.Concat(Enumerable.Range(38, 126 - 38 + 1)));

		string ToSlugText(string text)
		{
			var utf8Encoded = ToUTF8(text);
			var stringBuilder = new StringBuilder();
			var toRfc3986 = new StringBuilder();
			Action<StringBuilder> escapeUriDataString = t =>
			{
				if(t.Length > 0)
					stringBuilder.Append(Rfc3986.EscapeUriDataString(t.ToString()));
			};
			foreach (var octet in utf8Encoded)
			{
				if (!Rfc3986CharsNotToEscape.Contains(octet))
					toRfc3986.Append(octet);
				else
				{
					escapeUriDataString(toRfc3986);
					toRfc3986.Clear();
					stringBuilder.Append(octet);
				}
			}
			escapeUriDataString(toRfc3986);
			return stringBuilder.ToString();
		}

		string ToUTF8(string text)
		{
			var utf8 = Encoding.UTF8;
			var encodedBytes = Encoding.Default.GetBytes(text);
			var convertedBytes = Encoding.Convert(Encoding.Default, utf8, encodedBytes);
			return utf8.GetString(convertedBytes);
		}
	}

	public static class SlugHeaderExtensions
	{
		public static SlugHeader ToSlugHeader(this Microsoft.Data.OData.ODataEntry entry, IODataAdapter adapter)
		{
			return ToSlugHeader(entry, writer => new V3.Adapter.JsonWriter(adapter,writer, true));
		}

		public static SlugHeader ToSlugHeader(this Microsoft.OData.Core.ODataEntry entry)
		{
			return ToSlugHeader(entry, writer => new V4.Adapter.JsonWriter(writer, true));
		}

		static SlugHeader ToSlugHeader(dynamic entry, Func<TextWriter,IJsonWriter> getJsonWriter)
		{
			var fields = new Dictionary<string, string>();
			foreach (var property in entry.Properties)
				using (var textWriter = new StringWriter())
				{
					var jsonWriter = getJsonWriter(textWriter);
					jsonWriter.WriteJsonValue(property.Value);
					fields[property.Name] = textWriter.ToString();
				}
			return new SlugHeader(fields);
		}
	}
}
