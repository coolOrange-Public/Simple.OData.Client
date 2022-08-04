using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Simple.OData.Client.Adapter;

namespace Simple.OData.Client.V4.Adapter
{
	public class JsonWriter : IJsonWriter
	{
		static readonly Assembly MicrosoftOdataAssembly =
			Assembly.GetAssembly(typeof(Microsoft.OData.Json.IJsonWriter));

		private readonly object _jsonWriter;

		public JsonWriter(TextWriter textWriter, bool indent)
			: this(textWriter, indent, GetJsonLightFormat())
		{
		}

		internal JsonWriter(TextWriter textWriter, bool indent, object format)
		{
			_jsonWriter = MicrosoftOdataAssembly.CreateInstance("Microsoft.OData.Json.JsonWriter", true,
				BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { textWriter, indent, format, true }, null,
				null);
		}

		public void WriteJsonValue(object propertyValue)
		{
			var writeJsonValue = MicrosoftOdataAssembly.GetType("Microsoft.OData.Json.JsonWriterExtensions")
				.GetMethod("WriteJsonValue", BindingFlags.NonPublic | BindingFlags.Static,
					null, CallingConventions.Any,
					new[] { MicrosoftOdataAssembly.GetType("Microsoft.OData.Json.IJsonWriter"), typeof(object) }, null);

			writeJsonValue.Invoke(null, new[] { _jsonWriter, propertyValue });
		}

		static object GetJsonLightFormat()
		{
			return MicrosoftOdataAssembly.CreateInstance("Microsoft.OData.JsonLight.ODataJsonLightFormat", true,
				BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
				null, null, null, null);
		}
	}
}