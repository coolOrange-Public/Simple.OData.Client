using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Data.OData;
using Simple.OData.Client.Adapter;

namespace Simple.OData.Client.V3.Adapter
{
	public class JsonWriter : IJsonWriter
	{
		private readonly ODataVersion _oDataVersion;
		static readonly Assembly MicrosoftOdataAssembly = Assembly.GetAssembly(typeof(Microsoft.Data.OData.ODataEntry));
		private readonly object _jsonWriter;
		public JsonWriter(IODataAdapter adapter, TextWriter textWriter, bool indent)
			: this(adapter, textWriter, indent, GetJsonLightFormat())
		{
		}

		internal JsonWriter(IODataAdapter adapter, TextWriter textWriter, bool indent, object format)
		{
			_oDataVersion = (ODataVersion)Enum.Parse(typeof(ODataVersion),adapter.GetODataVersionString());

			_jsonWriter = MicrosoftOdataAssembly.CreateInstance("Microsoft.Data.OData.Json.JsonWriter", true,
					BindingFlags.Instance | BindingFlags.NonPublic, null, new [] { textWriter, indent, format }, null, null);
		}

		public void WriteJsonValue(object propertyValue)
		{
			var writeJsonValue = MicrosoftOdataAssembly.GetType("Microsoft.Data.OData.Json.JsonWriterExtensions")
					.GetMethod("WriteJsonValue", BindingFlags.NonPublic | BindingFlags.Static,
						null, CallingConventions.Any, 
						new [] { MicrosoftOdataAssembly.GetType("Microsoft.Data.OData.Json.IJsonWriter"), typeof(object), typeof(ODataVersion) }, null);

			writeJsonValue.Invoke(null, new [] { _jsonWriter,propertyValue, _oDataVersion });
		}

		static object GetJsonLightFormat()
		{	
			return MicrosoftOdataAssembly.CreateInstance("Microsoft.Data.OData.JsonLight.ODataJsonLightFormat", true,
				BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, 
				null, null, null, null);
		}
	}
}