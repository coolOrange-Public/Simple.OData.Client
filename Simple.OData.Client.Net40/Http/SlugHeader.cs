using System.Collections.Generic;
using System.Linq;
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

		public SlugHeader(IDictionary<string,string> fields) : this()
		{
			foreach (var field in fields)
				Add(field.Key,field.Value);
		}

		public void Add(string fieldKey, string fieldValue)
		{
			var utf8FieldValue = Encoding.UTF8.GetString(Encoding.Default.GetBytes(fieldValue));
			_fields[fieldKey] = Rfc3986.EscapeUriDataString(utf8FieldValue);
		}

		public override string ToString()
		{
			return string.Join(",", _fields.Select(kvp => kvp.Key + "='" + kvp.Value + "'"));
		}
	}
}
