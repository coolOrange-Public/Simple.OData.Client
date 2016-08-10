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
			{
				var utf8FieldValue = Encoding.UTF8.GetString(Encoding.Default.GetBytes(fieldValue));
				value = Rfc3986.EscapeUriDataString(utf8FieldValue);
			}
			_fields[fieldKey] = value;
		}

		public override string ToString()
		{
			Func<string, string> getValue = value =>
			{
				var quoted = value.StartsWith("%22") && value.EndsWith("%22");
				if (quoted)
					value = "'" + value.Trim("%22".ToCharArray())+ "'";
				return value;
			};
			return string.Join(",", _fields.Select(kvp => kvp.Key + "="+getValue(kvp.Value)));
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
