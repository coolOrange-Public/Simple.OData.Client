using System;

namespace Simple.OData.Client.V4.Adapter.Extensions
{
	public sealed class DynamicAggregationFunction
	{
		internal DynamicAggregationFunction()
		{
		}

		public Tuple<string, ODataExpression> Average(ODataExpression expression)
		{
			return new Tuple<string, ODataExpression>("Average", expression);
		}

		public Tuple<string, ODataExpression> Sum(ODataExpression expression)
		{
			return new Tuple<string, ODataExpression>("Sum", expression);
		}

		public Tuple<string, ODataExpression> Min(ODataExpression expression)
		{
			return new Tuple<string, ODataExpression>("Min", expression);
		}

		public Tuple<string, ODataExpression> Max(ODataExpression expression)
		{
			return new Tuple<string, ODataExpression>("Max", expression);
		}

		public Tuple<string, ODataExpression> Count()
		{
			return new Tuple<string, ODataExpression>("Count", null);
		}

		public Tuple<string, ODataExpression> CountDistinct(ODataExpression expression)
		{
			return new Tuple<string, ODataExpression>("CountDistinct", expression);
		}
	}
}