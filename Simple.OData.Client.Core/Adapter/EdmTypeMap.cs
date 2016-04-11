using System;
using System.Collections.Generic;

namespace Simple.OData.Client
{
	public interface IEdmTypeMap
	{
		IEnumerable<Type> GetTypes(object propertyType);
	}
}