using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

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

		internal SlugHeader(Microsoft.Data.OData.ODataEntry entry, IODataAdapter adapter) :
			this(GetJsonFieldValues(entry, adapter))
		{
		}
		internal SlugHeader(Microsoft.OData.Core.ODataEntry entry, IODataAdapter adapter) :
			this(GetJsonFieldValues(entry, adapter))
		{
		}

		static IDictionary<string, string> GetJsonFieldValues(dynamic entry, IODataAdapter adapter)
		{
			var @namepsace = ((Type) entry.GetType()).Namespace;
			var fields = new Dictionary<string, string>();
			Assembly microsoftOdataAssembly = Assembly.GetAssembly(entry.GetType());
			var jsonFormat = microsoftOdataAssembly.CreateInstance(@namepsace+".JsonLight.ODataJsonLightFormat", true,
				BindingFlags.CreateInstance |BindingFlags.Instance | BindingFlags.NonPublic |BindingFlags.Public,null, null, null, null);
			
			var odataVersion = Enum.Parse(microsoftOdataAssembly.GetType(@namepsace+ ".ODataVersion"), adapter.GetODataVersionString(), false);

			foreach (var property in entry.Properties)
				fields[property.Name] = GetJsonFieldValue(property, odataVersion, microsoftOdataAssembly, namepsace, jsonFormat);
			return fields;
		}

		static string GetJsonFieldValue(dynamic property, object odataVersion, Assembly microsoftOdataAssembly, string namepsace, object jsonFormat)
		{
			using (var textWriter = new StringWriter())
			{
				var jsonWriter = microsoftOdataAssembly.CreateInstance(@namepsace + ".Json.JsonWriter", true,
					BindingFlags.Instance | BindingFlags.NonPublic, null, new[] {textWriter, true, jsonFormat}, null, null);

				var writeJsonValueArguments = new Dictionary<Type, object> {
					{microsoftOdataAssembly.GetType(@namepsace + ".Json.IJsonWriter"), jsonWriter},
					{typeof(object), property.Value},
					{odataVersion.GetType(), odataVersion}
				};
				if (@namepsace.Equals("Microsoft.OData.Core", StringComparison.InvariantCulture))
					writeJsonValueArguments.Add(typeof(bool), true);

				var writeJsonValue = microsoftOdataAssembly.GetType(@namepsace + ".Json.JsonWriterExtensions")
					.GetMethod("WriteJsonValue", BindingFlags.NonPublic | BindingFlags.Static,
						null, CallingConventions.Any, writeJsonValueArguments.Keys.ToArray(), null);

				writeJsonValue.Invoke(null, writeJsonValueArguments.Values.ToArray());
				return textWriter.ToString();
			}
		}
	}
}
