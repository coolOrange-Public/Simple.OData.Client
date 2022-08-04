using System;
using System.Collections.Generic;
using System.Linq;
using Simple.OData.Client.Extensions;

namespace Simple.OData.Client.V4.Adapter.Extensions
{
	public class DynamicDataAggregation
	{
		private readonly DataAggregationBuilderHolder _underlyingDataAggregationBuilder;

		internal DynamicDataAggregation()
		{
			_underlyingDataAggregationBuilder = new DataAggregationBuilderHolder();
		}

		public DynamicDataAggregation Filter(string filter)
		{
			var filterClause = (FilterClause)_underlyingDataAggregationBuilder.LastOrDefault(x => x is FilterClause);
			if (filterClause != null)
			{
				filterClause.Append(filter);
			}
			else
			{
				filterClause = new FilterClause(filter);
				_underlyingDataAggregationBuilder.Add(filterClause);
			}

			return this;
		}

		public DynamicDataAggregation Filter(ODataExpression filter)
		{
			var filterClause = _underlyingDataAggregationBuilder.LastOrDefault() as FilterClause;
			if (filterClause != null)
			{
				filterClause.Append(filter);
			}
			else
			{
				filterClause = new FilterClause(filter);
				_underlyingDataAggregationBuilder.Add(filterClause);
			}

			return this;
		}

		public DynamicDataAggregation Aggregate(object aggregation)
		{
			var aggregationClauses = new AggregationClauseCollection<object>();
			var objectType = aggregation.GetType();
			var declaredProperties = objectType.GetDeclaredProperties();
			foreach (var property in declaredProperties)
			{
				var propertyValue = property.GetValueEx(aggregation);
				var aggregatedProperty = propertyValue as Tuple<string, ODataExpression>;
				if (aggregatedProperty != null)
				{
					aggregationClauses.Add(new AggregationClause<object>(property.Name,
						aggregatedProperty.Item2 != null ? aggregatedProperty.Item2.Reference : null,
						aggregatedProperty.Item1));
				}
			}

			_underlyingDataAggregationBuilder.Add(aggregationClauses);
			return this;
		}

		public DynamicDataAggregation GroupBy(object groupBy)
		{
			var groupByColumns = new List<string>();
			var aggregationClauses = new AggregationClauseCollection<object>();
			var groupByExpression = groupBy as ODataExpression;
			if (!ReferenceEquals(groupByExpression, null))
			{
				groupByColumns.Add(groupByExpression.Reference);
			}
			else
			{
				var objectType = groupBy.GetType();
				var declaredProperties = objectType.GetDeclaredProperties();
				foreach (var property in declaredProperties)
				{
					var propertyValue = property.GetValueEx(groupBy);
					var oDataExpression = propertyValue as ODataExpression;
					if (!ReferenceEquals(oDataExpression, null))
					{
						groupByColumns.Add(oDataExpression.Reference);
					}
					else
					{
						var aggregatedProperty = propertyValue as Tuple<string, ODataExpression>;
						if (aggregatedProperty != null)
						{
							aggregationClauses.Add(new AggregationClause<object>(property.Name,
								aggregatedProperty.Item2 != null ? aggregatedProperty.Item2.Reference : null,
								aggregatedProperty.Item1));
						}
					}
				}
			}

			_underlyingDataAggregationBuilder.Add(new GroupByClause<object>(groupByColumns, aggregationClauses));
			return this;
		}

		internal DataAggregationBuilder CreateBuilder()
		{
			return _underlyingDataAggregationBuilder;
		}

		private class DataAggregationBuilderHolder : DataAggregationBuilder
		{
			internal void Add(IDataAggregationClause dataAggregationClause)
			{
				DataAggregationClauses.Add(dataAggregationClause);
			}

			internal IDataAggregationClause LastOrDefault(Func<IDataAggregationClause, bool> predicate = null)
			{
				return DataAggregationClauses.LastOrDefault(predicate ?? (x => true));
			}
		}
	}
}