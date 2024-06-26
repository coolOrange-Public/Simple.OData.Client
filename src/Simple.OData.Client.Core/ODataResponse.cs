﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Simple.OData.Client.Extensions;

namespace Simple.OData.Client
{
	public class AnnotatedFeed
	{
		public IList<AnnotatedEntry> Entries { get; private set; }
		public ODataFeedAnnotations Annotations { get; private set; }

		public AnnotatedFeed(IEnumerable<AnnotatedEntry> entries, ODataFeedAnnotations annotations = null)
		{
			this.Entries = entries.ToList();
			this.Annotations = annotations;
		}

		public void SetAnnotations(ODataFeedAnnotations annotations)
		{
			if (this.Annotations == null)
			{
				this.Annotations = annotations;
			}
			else
			{
				this.Annotations.Merge(annotations);
			}
		}
	}

	public class AnnotatedEntry
	{
		public IDictionary<string, object> Data { get; private set; }
		public ODataEntryAnnotations Annotations { get; private set; }
		public ODataFeedAnnotations LinkAnnotations { get; private set; }

		public AnnotatedEntry(IDictionary<string, object> data, ODataEntryAnnotations annotations = null)
		{
			this.Data = data;
			this.Annotations = annotations;
		}

		public void SetAnnotations(ODataEntryAnnotations annotations)
		{
			if (this.Annotations == null)
			{
				this.Annotations = annotations;
			}
			else
			{
				this.Annotations.Merge(annotations);
			}
		}

		public void SetLinkAnnotations(ODataFeedAnnotations annotations)
		{
			if (this.LinkAnnotations == null)
			{
				this.LinkAnnotations = annotations;
			}
			else
			{
				this.LinkAnnotations.Merge(annotations);
			}
		}

		public IDictionary<string, object> GetData(bool includeAnnotations)
		{
			if (includeAnnotations && this.Annotations != null)
			{
				var dataWithAnnotations = new Dictionary<string, object>(this.Data);
				dataWithAnnotations.Add(FluentCommand.AnnotationsLiteral, this.Annotations);
				return dataWithAnnotations;
			}
			else
			{
				return this.Data;
			}
		}
	}

	public class ODataErrorDetails
	{
		public string ErrorCode { get; private set; }
		public string Message { get; private set; }
		public ODataInnerErrorDetails InnerError { get; private set; }

		internal ODataErrorDetails(dynamic error)
		{
			ErrorCode = error.ErrorCode;
			Message = error.Message;
			if (error.InnerError != null)
				InnerError = new ODataInnerErrorDetails(error.InnerError);
		}
	}

	public class ODataInnerErrorDetails
	{
		public string Message { get; private set; }
		public string TypeName { get; private set; }
		public string StackTrace { get; private set; }
		public ODataInnerErrorDetails InnerError { get; private set; }

		internal ODataInnerErrorDetails(dynamic innerError)
		{
			Message = innerError.Message;
			StackTrace = innerError.StackTrace;
			TypeName = innerError.TypeName;
			if (innerError.InnerError != null)
				InnerError = new ODataInnerErrorDetails(innerError.InnerError);
		}
	}

	public class ODataResponse
	{
		public int StatusCode { get; private set; }
		public AnnotatedFeed Feed { get; private set; }
		public IList<ODataResponse> Batch { get; private set; }
		public ODataErrorDetails ErrorDetails { get; private set; }
		public Exception Exception { get; private set; }
		public IEnumerable<KeyValuePair<string, string>> Headers { get; internal set; }

		public string Location
		{
			get
			{
				return Headers != null ? Headers.FirstOrDefault(x => x.Key == "Location").Value : null;
			}
		}

		public string ODataEntityId
		{
			get
			{
				return Headers != null ? Headers.FirstOrDefault(x => x.Key == "OData-EntityId").Value : null;
			}
		}

		internal ITypeCache TypeCache { get; set; }

		private ODataResponse(ITypeCache typeCache)
		{
			TypeCache = typeCache;
		}

		public IEnumerable<IDictionary<string, object>> AsEntries(bool includeAnnotations)
		{
			if (this.Feed != null)
			{
				var data = this.Feed.Entries;
				return data.Select(x =>
					data.Any() && data.First().Data.ContainsKey(FluentCommand.ResultLiteral)
						? ExtractDictionary(x, includeAnnotations)
						: ExtractData(x, includeAnnotations));
			}
			else
			{
				return Enumerable.Empty<IDictionary<string, object>>();
			}
		}

		public IDictionary<string, object> AsEntry(bool includeAnnotations)
		{
			var result = AsEntries(includeAnnotations);

			return result != null
				? result.FirstOrDefault()
				: null;
		}

		public T AsScalar<T>()
		{
			Func<IDictionary<string, object>, object> extractScalar = x =>
				(x == null) || !x.Any() ? null : x.Values.First();
			var result = this.AsEntry(false);
			var value = result == null ? null : extractScalar(result);

			return value == null
				? default(T)
				: TypeCache.Convert<T>(value);
		}

		public T[] AsArray<T>()
		{
			return this.AsEntries(false)
				.SelectMany(x => x.Values)
				.Select(x => TypeCache.Convert<T>(x))
				.ToArray();
		}

		internal static ODataResponse FromNode(ITypeCache typeCache, ResponseNode node,
			IEnumerable<KeyValuePair<string, string>> headers)
		{
			return new ODataResponse(typeCache) {
				Feed = node.Feed ?? new AnnotatedFeed(node.Entry != null ? new[] { node.Entry } : null),
				Headers = headers
			};
		}

		internal static ODataResponse FromProperty(ITypeCache typeCache, string propertyName, object propertyValue)
		{
			return FromFeed(typeCache, new[] {
				new Dictionary<string, object> { { propertyName ?? FluentCommand.ResultLiteral, propertyValue } }
			});
		}

		internal static ODataResponse FromValueStream(ITypeCache typeCache, Stream stream, bool disposeStream = false)
		{
			return FromFeed(typeCache, new[] {
				new Dictionary<string, object>
					{ { FluentCommand.ResultLiteral, Utils.StreamToString(stream, disposeStream) } }
			});
		}

		internal static ODataResponse FromCollection(ITypeCache typeCache, IList<object> collection)
		{
			return new ODataResponse(typeCache) {
				Feed = new AnnotatedFeed(collection.Select(
					x => new AnnotatedEntry(new Dictionary<string, object>() {
						{ FluentCommand.ResultLiteral, x }
					})))
			};
		}

		internal static ODataResponse FromBatch(ITypeCache typeCache, IList<ODataResponse> batch)
		{
			return new ODataResponse(typeCache) {
				Batch = batch,
			};
		}

		public static ODataResponse FromErrorResponse(ITypeCache typeCache, int statusCode, ODataErrorDetails errorDetails = null, Exception e = null)
		{
			return new ODataResponse(typeCache) {
				StatusCode = statusCode,
				ErrorDetails = errorDetails,
				Exception = e
			};
		}

		internal static ODataResponse EmptyFeeds(ITypeCache typeCache)
		{
			return FromFeed(typeCache, Enumerable.Empty<Dictionary<string, object>>());
		}

		private static ODataResponse FromFeed(ITypeCache typeCache, IEnumerable<IDictionary<string, object>> entries,
			ODataFeedAnnotations feedAnnotations = null)
		{
			return new ODataResponse(typeCache) {
				Feed = new AnnotatedFeed(entries.Select(x => new AnnotatedEntry(x)), feedAnnotations)
			};
		}

		private IDictionary<string, object> ExtractData(AnnotatedEntry entry, bool includeAnnotations)
		{
			if (entry == null || entry.Data == null)
			{
				return null;
			}

			return includeAnnotations ? DataWithAnnotations(entry.Data, entry.Annotations) : entry.Data;
		}

		private IDictionary<string, object> ExtractDictionary(AnnotatedEntry entry, bool includeAnnotations)
		{
			if (entry == null || entry.Data == null)
			{
				return null;
			}

			var data = entry.Data;
			if (data.Keys.Count == 1 && data.ContainsKey(FluentCommand.ResultLiteral) &&
			    data.Values.First() is IDictionary<string, object>)
			{
				return data.Values.First() as IDictionary<string, object>;
			}
			else if (includeAnnotations)
			{
				return DataWithAnnotations(data, entry.Annotations);
			}
			else
			{
				return data;
			}
		}

		private IDictionary<string, object> DataWithAnnotations(IDictionary<string, object> data,
			ODataEntryAnnotations annotations)
		{
			var dataWithAnnotations = new Dictionary<string, object>(data) {
				{ FluentCommand.AnnotationsLiteral, annotations }
			};
			return dataWithAnnotations;
		}
	}
}