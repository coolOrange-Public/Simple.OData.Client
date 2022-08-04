using System.Collections.Generic;
using System.Linq;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Simple.OData.Client.Extensions;

namespace Simple.OData.Client.V4.Adapter
{
	public class ResourceProperties
	{
		public ODataResource Resource { get; private set; }
		public string TypeName { get; set; }
		public IDictionary<string, ODataCollectionValue> CollectionProperties { get; set; }
		public IDictionary<string, ODataResource> StructuralProperties { get; set; }
		public IEnumerable<ODataProperty> PrimitiveProperties
		{
			get { return this.Resource.Properties; }
		}

		public ResourceProperties(ODataResource resource)
		{
			this.Resource = resource;
		}
	}
}