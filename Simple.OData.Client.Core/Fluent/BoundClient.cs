using System.Collections.Generic;

namespace Simple.OData.Client
{
    // ALthough BoundClient is never instantiated directly (only via IBoundClient interface)
    // it's declared as public in order to resolve problem when it is used with dynamic C#
    // For the same reason FluentCommand is also declared as public
    // More: http://bloggingabout.net/blogs/vagif/archive/2013/08/05/we-need-better-interoperability-between-dynamic-and-statically-compiled-c.aspx

    /// <summary>
    /// Provides access to OData operations in a fluent style.
    /// </summary>
    /// <typeparam name="T">The entry type.</typeparam>
    public partial class BoundClient<T> : FluentClientBase<T, IBoundClient<T>>, IBoundClient<T>
        where T : class
    {
        internal BoundClient(ODataClient client, Session session, FluentCommand parentCommand = null, FluentCommand command = null, bool dynamicResults = false)
            : base(client, session, parentCommand, command, dynamicResults)
        {
        }

        #pragma warning disable 1591

        public IBoundClient<T> For(string collectionName = null)
        {
            this.Command.For(collectionName ?? typeof(T).Name);
            return this;
        }

        public IBoundClient<ODataEntry> For(ODataExpression expression)
        {
            this.Command.For(expression.Reference);
            return CreateClientForODataEntry();
        }

        public IBoundClient<IDictionary<string, object>> As(string derivedCollectionName)
        {
            this.Command.As(derivedCollectionName);
            return new BoundClient<IDictionary<string, object>>(_client, _session, _parentCommand, this.Command, _dynamicResults);
        }

        public IBoundClient<T> Set(object value)
        {
            this.Command.Set(value);
            return this;
        }

        public IBoundClient<T> Set(IDictionary<string, object> value)
        {
            this.Command.Set(value);
            return this;
        }

        public IBoundClient<T> Set(params ODataExpression[] value)
        {
            this.Command.Set(value);
            return this;
        }

	    public IBoundClient<T> Set(bool deep, params ODataExpression[] value)
	    {
			this.Command.Set(deep, value);
			return this;
		}

	    public IBoundClient<T> Set(T entry)
        {
            this.Command.Set(entry);
            return this;
        }

		public IBoundClient<T> Set(object value, bool deep)
		{
			this.Command.Set(value, deep);
			return this;
		}

		public IBoundClient<T> Set(IDictionary<string, object> value, bool deep)
		{
			this.Command.Set(value, deep);
			return this;
		}

		public IBoundClient<T> Set(T entry, bool deep)
		{
			this.Command.Set(entry, deep);
			return this;
		}

        public IBoundClient<U> As<U>(string derivedCollectionName = null)
        where U : class
        {
            this.Command.As(derivedCollectionName ?? typeof(U).Name);
            return new BoundClient<U>(_client, _session, _parentCommand, this.Command, _dynamicResults);
        }

        public IBoundClient<ODataEntry> As(ODataExpression expression)
        {
            this.Command.As(expression);
            return CreateClientForODataEntry();
        }

        public bool FilterIsKey
        {
            get { return this.Command.FilterIsKey; }
        }

        public IDictionary<string, object> FilterAsKey
        {
            get { return this.Command.FilterAsKey; }
        }

        #pragma warning restore 1591

        private BoundClient<ODataEntry> CreateClientForODataEntry() 
        {
            return new BoundClient<ODataEntry>(_client, _session, _parentCommand, this.Command, true); ;
        }
    }
}
