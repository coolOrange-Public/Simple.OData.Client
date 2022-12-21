using System.IO;
using System.Reflection;
using Simple.OData.Client.Adapter;

namespace Simple.OData.Client.V4.Adapter
{
	public class JsonWriter : IJsonWriter
	{
		private static readonly Assembly MicrosoftOdataAssembly = Assembly.GetAssembly(typeof(Microsoft.OData.ODataResource));
		private readonly object _jsonWriter;

		public JsonWriter(TextWriter textWriter)
		{
			var factory = new Microsoft.OData.Json.DefaultJsonWriterFactory();
			_jsonWriter = factory.CreateJsonWriter(textWriter, true);
		}

		public void WriteJsonValue(object propertyValue)
		{
			var writeJsonValue = MicrosoftOdataAssembly.GetType("Microsoft.OData.Json.JsonWriterExtensions")
				.GetMethod("WriteJsonValue", BindingFlags.NonPublic | BindingFlags.Static,
					null, CallingConventions.Any,
					new[] { MicrosoftOdataAssembly.GetType("Microsoft.OData.Json.IJsonWriter"), typeof(object) }, null);

			writeJsonValue.Invoke(null, new[] { _jsonWriter, propertyValue });
		}
	}
}