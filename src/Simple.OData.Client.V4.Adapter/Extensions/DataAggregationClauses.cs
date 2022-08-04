using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Simple.OData.Client.V4.Adapter.Extensions
{
	internal interface IDataAggregationClause
	{
		string Format(ExpressionContext context);
	}

	internal class FilterClause : IDataAggregationClause
	{
		private ODataExpression _filterExpression;
		private string _filter;

		internal FilterClause(string filter)
		{
			_filter = filter;
		}

		internal FilterClause(ODataExpression expression)
		{
			_filterExpression = expression;
		}

		internal void Append(string filter)
		{
			_filter = string.Format("{0} and {1}", _filter, filter);
		}

		internal void Append(ODataExpression expression)
		{
			_filterExpression = _filterExpression && expression;
		}

		public string Format(ExpressionContext context)
		{
			if (string.IsNullOrEmpty(_filter) && !ReferenceEquals(_filterExpression, null))
			{
				_filter = _filterExpression.Format(context);
			}

			return string.IsNullOrEmpty(_filter)
				? string.Empty
				: string.Format("filter({0})", _filter);
		}
	}

	internal class GroupByClause<T> : IDataAggregationClause
	{
		private readonly IEnumerable<string> _columns;
		private readonly AggregationClauseCollection<T> _aggregation;

		internal GroupByClause(IEnumerable<string> columns, AggregationClauseCollection<T> aggregation = null)
		{
			_columns = columns;
			_aggregation = aggregation;
		}

		public string Format(ExpressionContext context)
		{
			var formattedAggregation = _aggregation != null ? _aggregation.Format(context) : null;
			var aggregation = string.IsNullOrEmpty(formattedAggregation)
				? string.Empty
				: string.Format(",{0}", formattedAggregation);

			return string.Format("groupby(({0}){1})", string.Join(",", _columns), aggregation);
		}
	}

	internal class AggregationClauseCollection<T> : IDataAggregationClause
	{
		private readonly ICollection<AggregationClause<T>> _clauses = new List<AggregationClause<T>>();

		internal void Add(AggregationClause<T> clause)
		{
			_clauses.Add(clause);
		}

		public string Format(ExpressionContext context)
		{
			return _clauses.Any()
				? string.Format("aggregate({0})", string.Join(",", _clauses.Select(c => c.Format(context))))
				: string.Empty;
		}
	}

	internal class AggregationClause<T>
	{
		private static readonly Dictionary<string, string> KnownFunctionTemplates = new Dictionary<string, string> {
			{ "Average", "{0} with average" },
			{ "Sum", "{0} with sum" },
			{ "Min", "{0} with min" },
			{ "Max", "{0} with max" },
			{ "CountDistinct", "{0} with countdistinct" },
			{ "Count", "$count" }
		};

		private readonly string _propertyName;
		private string _aggregatedColumnName;
		private string _aggregationMethodName;
		private readonly MethodCallExpression _expression;

		internal AggregationClause(string propertyName, Expression expression)
		{
			_propertyName = propertyName;
			if (!(expression is MethodCallExpression))
				throw new ArgumentException("Expression should be a method call.");
			_expression = (MethodCallExpression)expression;
		}

		internal AggregationClause(string propertyName, string aggregatedColumnName, string aggregationMethodName)
		{
			_propertyName = propertyName;
			_aggregatedColumnName = aggregatedColumnName;
			_aggregationMethodName = aggregationMethodName;
		}

		public string Format(ExpressionContext context)
		{
			var function = FormatFunction(context);
			return string.Format("{0} as {1}", function, _propertyName);
		}

		private string FormatFunction(ExpressionContext context)
		{
			if (_expression != null)
			{
				_aggregatedColumnName = string.Empty;
				if (_expression.Arguments.Any())
				{
					var aggregationMethodArgument = _expression.Arguments[0];
					_aggregatedColumnName = aggregationMethodArgument.ExtractColumnName(context.Session.TypeCache);
				}

				_aggregationMethodName = _expression.Method.Name;
			}

			string function;
			if (KnownFunctionTemplates.TryGetValue(_aggregationMethodName, out function))
				return string.Format(function, _aggregatedColumnName);
			throw new InvalidOperationException(string.Format("Unknown aggregation method '{0}'",
				_aggregationMethodName));
		}
	}
}