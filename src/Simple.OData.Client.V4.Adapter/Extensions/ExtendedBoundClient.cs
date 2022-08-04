using System;

namespace Simple.OData.Client.V4.Adapter.Extensions
{
    /// <summary>
    /// Provides access to extended OData operations e.g. data aggregation extensions in a fluent style.
    /// </summary>
    /// <typeparam name="T">The entry type.</typeparam>
    /// <inheritdoc cref="IExtendedBoundClient{T}"/>
    public class ExtendedBoundClient<T> : BoundClient<T>, IExtendedBoundClient<T> where T : class
    {
        private ExtendedBoundClient(Session session, FluentCommand command) 
            : base(new ODataClient(session.Settings), session, null, command)
        {
        }

        internal ExtendedBoundClient(ODataClient oDataClient, Session session, bool dynamicResults = false)
            : base(oDataClient, session, dynamicResults: dynamicResults)
        {
        }

        public IExtendedBoundClient<TR> Apply<TR>(Func<IDataAggregation<T>, IDataAggregation<TR>> dataAggregation) where TR : class
        {
            var dataAggregationBuilder = new DataAggregationBuilder<T>(Session);
            dataAggregation(dataAggregationBuilder);
            AppendDataAggregationBuilder(dataAggregationBuilder);
            return new ExtendedBoundClient<TR>(Session, Command);
        }

        public IExtendedBoundClient<T> Apply(string dataAggregationCommand)
        {
            AppendDataAggregationCommand(dataAggregationCommand);
            return this;
        }

        public IExtendedBoundClient<TR> Apply<TR>(string dataAggregationCommand) where TR : class
        {
            AppendDataAggregationCommand(dataAggregationCommand);
            return new ExtendedBoundClient<TR>(Session, Command);
        }

        public IExtendedBoundClient<T> Apply(DynamicDataAggregation dataAggregation)
        {
            Command.Details.Extensions[ODataLiteral.Apply] = dataAggregation.CreateBuilder();
            return this;
        }

        private void AppendDataAggregationBuilder(DataAggregationBuilder dataAggregationBuilder)
        {
	        object applyExtension;
	        if (Command.Details.Extensions.TryGetValue(ODataLiteral.Apply, out applyExtension) && 
	            applyExtension is DataAggregationBuilder)
            {
	            ((DataAggregationBuilder)applyExtension).Append(dataAggregationBuilder);
            }
            else
            {
                Command.Details.Extensions[ODataLiteral.Apply] = dataAggregationBuilder;
            }
        }

        private void AppendDataAggregationCommand(string dataAggregationCommand)
        {
	        object applyExtension;
	        if (Command.Details.Extensions.TryGetValue(ODataLiteral.Apply, out applyExtension) && 
	            applyExtension is string  && !string.IsNullOrEmpty((string)applyExtension))
            {
                Command.Details.Extensions[ODataLiteral.Apply] = applyExtension + "/" + dataAggregationCommand;
            }
            else
            {
                Command.Details.Extensions[ODataLiteral.Apply] = dataAggregationCommand;
            }
        }
    }
}