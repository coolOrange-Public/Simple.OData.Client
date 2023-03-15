using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Simple.OData.Client
{
	class Session : ISession
	{
		private IODataAdapter _adapter;
		private HttpConnection _httpConnection;
		private EdmMetadataCache _metadataCache;

		private Session(Uri baseUri, string metadataString) : this(new ODataClientSettings {
			BaseUri = baseUri,
			MetadataDocument = metadataString
		})
		{
		}

		private Session(ODataClientSettings settings)
		{
			if (settings.BaseUri == null || string.IsNullOrEmpty(settings.BaseUri.AbsoluteUri))
			{
				throw new InvalidOperationException("Unable to create client session with no URI specified.");
			}

			Settings = settings;
			MetadataCache =
				EdmMetadataCache.Instances.GetOrAdd(this.Settings.BaseUri.AbsoluteUri, new EdmMetadataCache(TypeCache));
			MetadataCache.SetMetadataDocument(settings.MetadataDocument);
		}

		public IODataAdapter Adapter
		{
			get
			{
				if (_adapter == null)
				{
					lock (this)
					{
						if (_adapter == null)
						{
							_adapter = MetadataCache.GetODataAdapter(this);
						}
					}
				}

				return _adapter;
			}
		}

		public IMetadata Metadata
		{
			get { return Adapter.GetMetadata(); }
		}

		public EdmMetadataCache MetadataCache
		{
			get { return _metadataCache; }
			set { _metadataCache = value; }
		}

		public ODataClientSettings Settings { get; private set; }

		public ITypeCache TypeCache
		{
			get { return TypeCaches.TypeCache(Settings.BaseUri.AbsoluteUri, Settings.NameMatchResolver); }
		}

		public void Dispose()
		{
			lock (this)
			{
				if (_httpConnection != null)
				{
					_httpConnection.Dispose();
					_httpConnection = null;
				}
			}
		}

		private readonly SemaphoreSlim _initializeSemaphore = new SemaphoreSlim(1);

		public async Task Initialize(CancellationToken cancellationToken)
		{
			// Just allow one schema request at a time, unlikely to be much contention but avoids multiple requests for same endpoint.
			await _initializeSemaphore.WaitAsync(cancellationToken);

			try
			{
				if (!this.MetadataCache.IsResolved())
				{
					if (string.IsNullOrEmpty(this.Settings.MetadataDocument))
					{
						var response = await SendMetadataRequestAsync(cancellationToken);
						this.MetadataCache.SetMetadataDocument(await response.Content.ReadAsStringAsync());
					}
					else
					{
						this.MetadataCache.SetMetadataDocument(this.Settings.MetadataDocument);
					}
				}

				if (_adapter == null)
					_adapter = _metadataCache.GetODataAdapter(this);
			}
			finally
			{
				_initializeSemaphore.Release();
			}
		}

		public void Trace(string message, params object[] messageParams)
		{
			if (Settings.OnTrace != null)
				Settings.OnTrace.Invoke(message, messageParams);
		}

		public async Task<IODataAdapter> ResolveAdapterAsync(CancellationToken cancellationToken)
		{
			await Initialize(cancellationToken);

			if (Settings.PayloadFormat == ODataPayloadFormat.Unspecified)
				Settings.PayloadFormat = Adapter.DefaultPayloadFormat;

			return Adapter;
		}

		public HttpConnection GetHttpConnection()
		{
			if (_httpConnection == null)
			{
				lock (this)
				{
					if (_httpConnection == null)
					{
						_httpConnection = new HttpConnection(Settings);
					}
				}
			}

			return _httpConnection;
		}

		internal static Session FromSettings(ODataClientSettings settings)
		{
			return new Session(settings);
		}

		internal static Session FromMetadata(Uri baseUri, string metadataString)
		{
			return new Session(baseUri, metadataString);
		}

		private async Task<HttpResponseMessage> SendMetadataRequestAsync(CancellationToken cancellationToken)
		{
			var request = new ODataRequest(RestVerbs.Get, this, ODataLiteral.Metadata);
			return await new RequestRunner(this).ExecuteRequestAsync(request, cancellationToken);
		}
	}
}