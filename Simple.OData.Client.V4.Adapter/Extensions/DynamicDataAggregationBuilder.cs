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
            var filterClause = (FilterClause) _underlyingDataAggregationBuilder.LastOrDefault(x => x is FilterClause);
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