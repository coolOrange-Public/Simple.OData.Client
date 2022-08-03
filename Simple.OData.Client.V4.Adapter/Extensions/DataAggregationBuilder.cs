using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Simple.OData.Client.V4.Adapter.Extensions
{
	internal abstract class DataAggregationBuilder
	{
		protected readonly List<IDataAggregationClause> DataAggregationClauses;
		private DataAggregationBuilder _nextDataAggregationBuilder;

		protected DataAggregationBuilder()
		{
			DataAggregationClauses = new List<IDataAggregationClause>();
		}

		internal string Build(ResolvedCommand command, ISession session)
		{
			var context = new ExpressionContext(session, null, null, command.DynamicPropertiesContainerName);
			var commandText = string.Empty;
			foreach (var applyClause in DataAggregationClauses)
			{
				var formattedApplyClause = applyClause.Format(context);
				if (string.IsNullOrEmpty(formattedApplyClause))
					continue;
				if (commandText.Length > 0)
					commandText += "/";
				commandText += formattedApplyClause;
			}

			return AddNextCommand(commandText, command, session);
		}

		internal void Append(DataAggregationBuilder nextDataAggregationBuilder)
		{
			if (_nextDataAggregationBuilder != null)
			{
				_nextDataAggregationBuilder.Append(nextDataAggregationBuilder);
				return;
			}
			_nextDataAggregationBuilder = nextDataAggregationBuilder;
		}

		private string AddNextCommand(string commandText, ResolvedCommand command, ISession session)
		{
			if (_nextDataAggregationBuilder == null) return commandText;

			var nestedCommand = _nextDataAggregationBuilder.Build(command, session);
			if (string.IsNullOrEmpty(nestedCommand)) return commandText;

			if (commandText.Length > 0)
				commandText += "/";
			commandText += nestedCommand;

			return commandText;
		}
	}

	/// <inheritdoc cref="IDataAggregation{T}"/>
	internal class DataAggregationBuilder<T> : DataAggregationBuilder, IDataAggregation<T>
		where T : class
	{
		private readonly ISession _session;

		internal DataAggregationBuilder(ISession session) : base()
		{
			_session = session;
		}

		public IDataAggregation<T> Filter(Expression<Func<T, bool>> filter)
		{
			var filterClause = DataAggregationClauses.LastOrDefault() as FilterClause;
			if (filterClause != null)
				filterClause.Append(ODataExpression.FromLinqExpression(filter));
			else
			{
				filterClause = new FilterClause(ODataExpression.FromLinqExpression(filter));
				DataAggregationClauses.Add(filterClause);
			}
			return this;
		}

		public IDataAggregation<TR> Aggregate<TR>(Expression<Func<T, IAggregationFunction<T>, TR>> aggregation) where TR : class
		{
			var aggregationClauses = ExtractAggregationClauses(aggregation);
			DataAggregationClauses.Add(aggregationClauses);
			var nextDataAggregationBuilder = new DataAggregationBuilder<TR>(_session);
			Append(nextDataAggregationBuilder);
			return nextDataAggregationBuilder;
		}

		private static AggregationClauseCollection<T> ExtractAggregationClauses<TR>(Expression<Func<T, IAggregationFunction<T>, TR>> expression) where TR : class
		{
			var aggregationClauses = new AggregationClauseCollection<T>();
			var newExpression = expression.Body as NewExpression;
			if (newExpression != null)
			{
				var membersCount = Math.Min(newExpression.Members.Count, newExpression.Arguments.Count);
				for (var index = 0; index < membersCount; index++)
				{
					var methodCallExpression = newExpression.Arguments[index] as MethodCallExpression;
					if (methodCallExpression != null &&
						methodCallExpression.Method.DeclaringType == typeof(IAggregationFunction<T>))
						aggregationClauses.Add(new AggregationClause<T>(newExpression.Members[index].Name,
							newExpression.Arguments[index]));
				}
			}
			else
			{
				var memberInitExpression = expression.Body as MemberInitExpression;
				if (memberInitExpression != null)
				{
					foreach (var assignment in memberInitExpression.Bindings.OfType<MemberAssignment>())
					{
						var methodCallExpression = assignment.Expression as MethodCallExpression;
						if (methodCallExpression != null &&
							methodCallExpression.Method.DeclaringType == typeof(IAggregationFunction<T>))
							aggregationClauses.Add(new AggregationClause<T>(assignment.Member.Name, assignment.Expression));
					}
				}
				else
				{
					throw new AggregateException("Expression should be a NewExpression or MemberInitExpression");
				}
			}

			return aggregationClauses;
		}

		public IDataAggregation<TR> GroupBy<TR>(Expression<Func<T, IAggregationFunction<T>, TR>> groupBy) where TR : class
		{
			var groupByColumns = new List<string>();
			AggregationClauseCollection<T> aggregationClauses = null;
			if (groupBy.Body is MemberExpression)
				groupByColumns.Add(((MemberExpression)groupBy.Body).ExtractColumnName(_session.TypeCache));
			else
			{
				aggregationClauses = ExtractAggregationClauses(groupBy);
				groupByColumns.AddRange(ExtractGroupByColumns(groupBy));
			}

			var groupByClause = new GroupByClause<T>(groupByColumns, aggregationClauses);
			DataAggregationClauses.Add(groupByClause);
			var nextDataAggregationBuilder = new DataAggregationBuilder<TR>(_session);
			Append(nextDataAggregationBuilder);
			return nextDataAggregationBuilder;
		}

		private IEnumerable<string> ExtractGroupByColumns<TR>(Expression<Func<T, IAggregationFunction<T>, TR>> expression) where TR : class
		{
			var newExpression = expression.Body as NewExpression;
			if (newExpression != null)
			{
				var membersCount = Math.Min(newExpression.Members.Count, newExpression.Arguments.Count);
				for (var index = 0; index < membersCount; index++)
				{
					var memberExpression = newExpression.Arguments[index] as MemberExpression;
					if (memberExpression != null)
						yield return newExpression.Arguments[index].ExtractColumnName(_session.TypeCache);
					else
					{
						var memberInitExpression = newExpression.Arguments[index] as MemberInitExpression;
						if (memberInitExpression != null)
							foreach (var columnName in ExtractColumnNames(memberInitExpression))
								yield return columnName;
					}
				}
			}
			else if (expression.Body is MemberInitExpression)
			{
				foreach (var columnName in ExtractColumnNames((MemberInitExpression)expression.Body))
					yield return columnName;
			}
			else
			{
				throw new AggregateException("Expression should be a NewExpression or MemberInitExpression");
			}
		}

		private IEnumerable<string> ExtractColumnNames(MemberInitExpression expression)
		{
			var columnNames = new List<string>();
			foreach (var assignment in expression.Bindings.OfType<MemberAssignment>())
			{
				var memberExpression = assignment.Expression as MemberExpression;
				if (memberExpression != null)
					columnNames.Add(memberExpression.ExtractColumnName(_session.TypeCache));
				else
				{
					var memberInitExpression = assignment.Expression as MemberInitExpression;
					if (memberInitExpression != null) columnNames.AddRange(ExtractColumnNames(memberInitExpression));
				}
			}

			return columnNames;
		}
	}
}