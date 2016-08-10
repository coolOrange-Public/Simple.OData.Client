using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.OData.Core;
using Simple.OData.Client.Adapter;

namespace Simple.OData.Client.V4.Adapter
{
	public class JsonWriter : IJsonWriter
	{
		static readonly Assembly MicrosoftOdataAssembly = Assembly.GetAssembly(typeof(Microsoft.OData.Core.ODataEntry));
		private readonly object _jsonWriter;
		public JsonWriter(TextWriter textWriter, bool indent)
			: this(textWriter, indent, GetJsonLightFormat())
		{
		}

		internal JsonWriter(TextWriter textWriter, bool indent, object format)
		{
			_jsonWriter = MicrosoftOdataAssembly.CreateInstance("Microsoft.OData.Core.Json.JsonWriter", true,
					BindingFlags.Instance | BindingFlags.NonPublic, null, new [] { textWriter, indent, format,true },null, null);
		}

		public void WriteJsonValue(object propertyValue)
		{
			var writeJsonValue = MicrosoftOdataAssembly.GetType("Microsoft.OData.Core.Json.JsonWriterExtensions")
					.GetMethod("WriteJsonValue", BindingFlags.NonPublic | BindingFlags.Static,
						null, CallingConventions.Any, 
						new [] { MicrosoftOdataAssembly.GetType("Microsoft.OData.Core.Json.IJsonWriter"), typeof(object) }, null);

			writeJsonValue.Invoke(null, new [] { _jsonWriter , propertyValue });
		}

		static object GetJsonLightFormat()
		{	
			return MicrosoftOdataAssembly.CreateInstance("Microsoft.OData.Core.JsonLight.ODataJsonLightFormat", true,
				BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, 
				null, null, null, null);
		}
	}
}