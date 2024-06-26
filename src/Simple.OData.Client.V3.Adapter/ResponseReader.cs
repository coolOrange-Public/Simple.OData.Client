﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Data.Edm;
using Microsoft.Data.OData;

#pragma warning disable 1591

namespace Simple.OData.Client.V3.Adapter
{
	public class ResponseReader : ResponseReaderBase
	{
		private readonly IEdmModel _model;
		private bool _hasResponse = false;

		public ResponseReader(ISession session, IEdmModel model)
			: base(session)
		{
			_model = model;
		}

		public override async Task<ODataResponse> GetResponseAsync(HttpResponseMessage responseMessage)
		{
			var odataResponseMessage = new ODataResponseMessage(responseMessage);
			if (!responseMessage.IsSuccessStatusCode)
				return ODataResponse.FromErrorResponse(TypeCache, odataResponseMessage.StatusCode,
					ReadErrorDetails(odataResponseMessage));
			var response = await GetResponseAsync(odataResponseMessage);
			response.Headers = odataResponseMessage.Headers;
			return response;
		}

		public async Task<ODataResponse> GetResponseAsync(IODataResponseMessageAsync responseMessage)
		{
			if (responseMessage.StatusCode == (int)HttpStatusCode.NoContent)
				return ODataResponse.FromErrorResponse(TypeCache, responseMessage.StatusCode);

			var readerSettings = _session.ToReaderSettings();
			using (var messageReader = new ODataMessageReader(responseMessage, readerSettings, _model))
			{
				var payloadKind = messageReader.DetectPayloadKind();
				if (payloadKind.Any(x => x.PayloadKind != ODataPayloadKind.Property))
					_hasResponse = true;

				if (payloadKind.Any(x => x.PayloadKind == ODataPayloadKind.Error))
				{
					return ODataResponse.FromErrorResponse(TypeCache, responseMessage.StatusCode,
						ReadErrorDetails(responseMessage));
				}
				else if (payloadKind.Any(x => x.PayloadKind == ODataPayloadKind.Value))
				{
					if (payloadKind.Any(x => x.PayloadKind == ODataPayloadKind.Collection))
					{
						throw new NotImplementedException();
					}
					else
					{
						var stream = await responseMessage.GetStreamAsync();
						return ODataResponse.FromValueStream(TypeCache, stream,
							responseMessage is ODataBatchOperationResponseMessage);
					}
				}
				else if (payloadKind.Any(x => x.PayloadKind == ODataPayloadKind.Batch))
				{
					return await ReadResponse(messageReader.CreateODataBatchReader());
				}
				else if (payloadKind.Any(x => x.PayloadKind == ODataPayloadKind.Feed))
				{
					return ReadResponse(messageReader.CreateODataFeedReader(), responseMessage);
				}
				else if (payloadKind.Any(x => x.PayloadKind == ODataPayloadKind.Collection))
				{
					return ReadResponse(messageReader.CreateODataCollectionReader());
				}
				else if (payloadKind.Any(x => x.PayloadKind == ODataPayloadKind.Property))
				{
					var property = messageReader.ReadProperty();
					if (property.Value != null && (property.Value.GetType() != typeof(string) ||
					                               !string.IsNullOrEmpty(property.Value.ToString())))
						_hasResponse = true;

					if (_hasResponse)
					{
						return ODataResponse.FromProperty(TypeCache, property.Name, GetPropertyValue(property.Value));
					}
					else
					{
						return ODataResponse.EmptyFeeds(TypeCache);
					}
				}
				else
				{
					return ReadResponse(messageReader.CreateODataEntryReader(), responseMessage);
				}
			}
		}

		private async Task<ODataResponse> ReadResponse(ODataBatchReader odataReader)
		{
			var batch = new List<ODataResponse>();

			while (odataReader.Read())
			{
				switch (odataReader.State)
				{
					case ODataBatchReaderState.ChangesetStart:
						break;
					case ODataBatchReaderState.Operation:
						var operationMessage = odataReader.CreateOperationResponseMessage();
						if (operationMessage.StatusCode == (int)HttpStatusCode.NoContent)
							batch.Add(ODataResponse.FromErrorResponse(TypeCache, operationMessage.StatusCode));
						else if (operationMessage.StatusCode >= (int)HttpStatusCode.BadRequest)
						{
							var responseStream = await operationMessage.GetStreamAsync();
							var exception = WebRequestException.CreateFromBatchResponse((HttpStatusCode)operationMessage.StatusCode, responseStream);
							var errorResponse = ODataResponse.FromErrorResponse(TypeCache, operationMessage.StatusCode, null, exception);
							batch.Add(errorResponse);
						}
						else
							batch.Add(await GetResponseAsync(operationMessage));

						break;
					case ODataBatchReaderState.ChangesetEnd:
						break;
				}
			}

			return ODataResponse.FromBatch(TypeCache, batch);
		}

		private ODataResponse ReadResponse(ODataCollectionReader odataReader)
		{
			var collection = new List<object>();

			while (odataReader.Read())
			{
				if (odataReader.State == ODataCollectionReaderState.Completed)
					break;

				switch (odataReader.State)
				{
					case ODataCollectionReaderState.CollectionStart:
						break;

					case ODataCollectionReaderState.Value:
						collection.Add(GetPropertyValue(odataReader.Item));
						break;

					case ODataCollectionReaderState.CollectionEnd:
						break;
				}
			}

			return ODataResponse.FromCollection(TypeCache, collection);
		}

		private ODataResponse ReadResponse(ODataReader odataReader, IODataResponseMessageAsync responseMessage)
		{
			ResponseNode rootNode = null;
			var nodeStack = new Stack<ResponseNode>();

			while (odataReader.Read())
			{
				if (odataReader.State == ODataReaderState.Completed)
					break;

				switch (odataReader.State)
				{
					case ODataReaderState.FeedStart:
						StartFeed(nodeStack, CreateAnnotations(odataReader.Item as ODataFeed));
						break;

					case ODataReaderState.FeedEnd:
						EndFeed(nodeStack, CreateAnnotations(odataReader.Item as ODataFeed), ref rootNode);
						break;

					case ODataReaderState.EntryStart:
						StartEntry(nodeStack);
						break;

					case ODataReaderState.EntryEnd:
						EndEntry(nodeStack, ref rootNode, odataReader.Item);
						break;

					case ODataReaderState.NavigationLinkStart:
						StartNavigationLink(nodeStack, (odataReader.Item as ODataNavigationLink).Name);
						break;

					case ODataReaderState.NavigationLinkEnd:
						EndNavigationLink(nodeStack);
						break;
				}
			}

			return ODataResponse.FromNode(TypeCache, rootNode, responseMessage.Headers);
		}

		ODataErrorDetails ReadErrorDetails(IODataResponseMessageAsync responseMessage)
		{
			var readerSettings = new ODataMessageReaderSettings {
				UndeclaredPropertyBehaviorKinds = ODataUndeclaredPropertyBehaviorKinds.IgnoreUndeclaredValueProperty,
				DisableMessageStreamDisposal = true,
				
			};
			using (var messageReader = new ODataMessageReader(responseMessage, readerSettings))
			{
				var error = messageReader.ReadError();
				return new ODataErrorDetails(error);
			}
		}

		protected override void ConvertEntry(ResponseNode entryNode, object entry)
		{
			if (entry != null)
			{
				var odataEntry = entry as Microsoft.Data.OData.ODataEntry;
				foreach (var property in odataEntry.Properties)
				{
					entryNode.Entry.Data.Add(property.Name, GetPropertyValue(property.Value));
				}

				entryNode.Entry.SetAnnotations(CreateAnnotations(odataEntry));
			}
		}

		private ODataFeedAnnotations CreateAnnotations(ODataFeed feed)
		{
			return new ODataFeedAnnotations() {
				Id = feed.Id,
				Count = feed.Count,
				DeltaLink = feed.DeltaLink,
				NextPageLink = feed.NextPageLink,
				InstanceAnnotations = feed.InstanceAnnotations,
			};
		}

		private ODataEntryAnnotations CreateAnnotations(Microsoft.Data.OData.ODataEntry odataEntry)
		{
			string id = null;
			Uri readLink = null;
			Uri editLink = null;
			IEnumerable<ODataAssociationLink> associationLinks = null;
			if (_session.Adapter.GetMetadata().IsTypeWithId(odataEntry.TypeName))
			{
				try
				{
					id = odataEntry.Id;
					readLink = odataEntry.ReadLink;
					editLink = odataEntry.EditLink;
					associationLinks = odataEntry.AssociationLinks;
				}
				catch (ODataException)
				{
					// Ignored
				}
			}

			return new ODataEntryAnnotations {
				Id = id,
				TypeName = odataEntry.TypeName,
				ReadLink = readLink,
				EditLink = editLink,
				ETag = odataEntry.ETag,
				AssociationLinks = associationLinks == null
					? null
					: new List<ODataEntryAnnotations.AssociationLink>(
						odataEntry.AssociationLinks.Select(x => new ODataEntryAnnotations.AssociationLink {
							Name = x.Name,
							Uri = x.Url,
						})),
				MediaResource = CreateAnnotations(odataEntry.MediaResource),
				InstanceAnnotations = odataEntry.InstanceAnnotations,
			};
		}

		private ODataMediaAnnotations CreateAnnotations(ODataStreamReferenceValue value)
		{
			return value == null
				? null
				: new ODataMediaAnnotations {
					ContentType = value.ContentType,
					ReadLink = value.ReadLink,
					EditLink = value.EditLink,
					ETag = value.ETag,
				};
		}

		private object GetPropertyValue(object value)
		{
			if (value is ODataComplexValue)
			{
				return (value as ODataComplexValue).Properties.ToDictionary(
					x => x.Name, x => GetPropertyValue(x.Value));
			}
			else if (value is ODataCollectionValue)
			{
				return (value as ODataCollectionValue).Items.Cast<object>()
					.Select(GetPropertyValue).ToList();
			}
			else if (value is ODataStreamReferenceValue)
			{
				return CreateAnnotations(value as ODataStreamReferenceValue);
			}
			else
			{
				return value;
			}
		}
	}
}