using System;
using System.Collections.Concurrent;

namespace Simple.OData.Client
{
	public class EdmMetadataCache
	{
		readonly ITypeCache _typeCache;
		public static readonly ConcurrentDictionary<string, EdmMetadataCache> Instances = new ConcurrentDictionary<string, EdmMetadataCache>();

		string _metadataDocument;

		public EdmMetadataCache(ITypeCache typeCache)
		{
			_typeCache = typeCache;
		}

		public bool IsResolved()
		{
			return _metadataDocument != null;
		}

		public string MetadataDocument
		{
			get
			{
				if (_metadataDocument == null)
					throw new InvalidOperationException("Service metadata is not resolved");

				return _metadataDocument;
			}
		}

		public void SetMetadataDocument(string metadataString)
		{
			_metadataDocument = metadataString;
		}

		public IODataAdapter GetODataAdapter(ISession session)
		{
			return session.Settings.AdapterFactory.CreateAdapterLoader(MetadataDocument, _typeCache)(session);
		}

		public static void Clear()
		{
		}
	}
}
