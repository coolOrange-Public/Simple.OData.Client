using Simple.OData.Client.V4.Adapter.Extensions;

namespace Simple.OData.Client
{
	public static class ODataDynamicDataAggregation
	{
		public static DynamicDataAggregation Builder
		{
			get { return new DynamicDataAggregation(); }
		}

		public static DynamicAggregationFunction AggregationFunction
		{
			get { return new DynamicAggregationFunction(); }
		}
	}
}