using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.OData;
using Microsoft.OData.Edm;

namespace Simple.OData.Client.V4.Adapter
{
	public class ResponseReader : ResponseReaderBase
	{
		private readonly IEdmModel _model;

		public ResponseReader(ISession session, IEdmModel model)
			: base(session)
		{
			_model = model;
		}

		public override async Task<ODataResponse> GetResponseAsync(HttpResponseMessage responseMessage)
		{
			var odataResponseMessage = new ODataResponseMessage(responseMessage);
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
				var payloadKind = messageReader.DetectPayloadKind().ToList();
				if (payloadKind.Any(x => x.PayloadKind == ODataPayloadKind.Error))
				{
					return ODataResponse.FromErrorResponse(TypeCache, responseMessage.StatusCode,
						ReadErrorDetails(messageReader, readerSettings));
				}
				else if (payloadKind.Any(x => x.PayloadKind == ODataPayloadKind.Value))
				{
					if (payloadKind.Any(x => x.PayloadKind == ODataPayloadKind.Collection))
					{
						throw new NotImplementedException();
					}
					else
					{
						return ODataResponse.FromValueStream(TypeCache, await responseMessage.GetStreamAsync(),
							responseMessage is ODataBatchOperationResponseMessage);
					}
				}
				else if (payloadKind.Any(x => x.PayloadKind == ODataPayloadKind.Batch))
				{
					return await ReadResponse(messageReader.CreateODataBatchReader()).ConfigureAwait(false);
				}
				else if (payloadKind.Any(x => x.PayloadKind == ODataPayloadKind.ResourceSet))
				{
					return ReadResponse(messageReader.CreateODataResourceSetReader(), responseMessage);
				}
				else if (payloadKind.Any(x => x.PayloadKind == ODataPayloadKind.Collection))
				{
					return ReadResponse(messageReader.CreateODataCollectionReader());
				}
				else if (payloadKind.Any(x => x.PayloadKind == ODataPayloadKind.Property))
				{
					if (payloadKind.Any(x => x.PayloadKind == ODataPayloadKind.Resource))
					{
						return ReadResponse(messageReader.CreateODataResourceReader(), responseMessage);
					}
					else
					{
						var property = messageReader.ReadProperty();
						return ODataResponse.FromProperty(TypeCache, property.Name, GetPropertyValue(property.Value));
					}
				}
				else if (payloadKind.Any(x => x.PayloadKind == ODataPayloadKind.Delta))
				{
					if (payloadKind.Any(x => x.PayloadKind == ODataPayloadKind.Resource))
					{
						return ReadResponse(messageReader.CreateODataResourceReader(), responseMessage);
					}
					else
					{
						return ReadResponse(messageReader.CreateODataDeltaResourceSetReader(), responseMessage);
					}
				}
				else
				{
					return ReadResponse(messageReader.CreateODataResourceReader(), responseMessage);
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
							var exception =
								WebRequestException.CreateFromFromBatchResponse(
									(HttpStatusCode)operationMessage.StatusCode, responseStream);
							var errorResponse = ODataResponse.FromErrorResponse(TypeCache, operationMessage.StatusCode,
								ReadErrorDetails(operationMessage), exception);

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
					case ODataReaderState.ResourceSetStart:
					case ODataReaderState.DeltaResourceSetStart:
						StartFeed(nodeStack, CreateAnnotations(odataReader.Item as ODataResourceSetBase));
						break;

					case ODataReaderState.ResourceSetEnd:
					case ODataReaderState.DeltaResourceSetEnd:
						EndFeed(nodeStack, CreateAnnotations(odataReader.Item as ODataResourceSetBase), ref rootNode);
						break;

					case ODataReaderState.ResourceStart:
						StartEntry(nodeStack);
						break;

					case ODataReaderState.ResourceEnd:
						EndEntry(nodeStack, ref rootNode, odataReader.Item);
						break;

					case ODataReaderState.NestedResourceInfoStart:
						StartNavigationLink(nodeStack, (odataReader.Item as ODataNestedResourceInfo).Name);
						break;

					case ODataReaderState.NestedResourceInfoEnd:
						EndNavigationLink(nodeStack);
						break;
				}
			}

			return ODataResponse.FromNode(TypeCache, rootNode, responseMessage.Headers);
		}

		ODataErrorDetails ReadErrorDetails(ODataBatchOperationResponseMessage operationMessage)
		{
			var readerSettings = new ODataMessageReaderSettings();
			using (var messageReader = new ODataMessageReader(operationMessage, readerSettings))
				return ReadErrorDetails(messageReader, readerSettings);
		}

		ODataErrorDetails ReadErrorDetails(ODataMessageReader responseMessageReader,
			ODataMessageReaderSettings readerSettings)
		{
			readerSettings.Validations = ValidationKinds.None;
			var error = responseMessageReader.ReadError();
			return new ODataErrorDetails(error);
		}

		protected override void ConvertEntry(ResponseNode entryNode, object entry)
		{
			if (entry != null)
			{
				var odataEntry = entry as ODataResource;
				foreach (var property in odataEntry.Properties)
				{
					entryNode.Entry.Data.Add(property.Name, GetPropertyValue(property.Value));
				}

				entryNode.Entry.SetAnnotations(CreateAnnotations(odataEntry));
			}
		}

		private ODataFeedAnnotations CreateAnnotations(ODataResourceSetBase feed)
		{
			return new ODataFeedAnnotations() {
				Id = feed.Id == null ? null : feed.Id.AbsoluteUri,
				Count = feed.Count,
				DeltaLink = feed.DeltaLink,
				NextPageLink = feed.NextPageLink,
				InstanceAnnotations = feed.InstanceAnnotations,
			};
		}

		private ODataEntryAnnotations CreateAnnotations(ODataResource odataEntry)
		{
			string id = null;
			Uri readLink = null;
			Uri editLink = null;
			string etag = null;
			if (_session.Adapter.GetMetadata().IsTypeWithId(odataEntry.TypeName))
			{
				try
				{
					// odataEntry.Id is null for transient entities (s. http://docs.oasis-open.org/odata/odata-json-format/v4.0/errata03/os/odata-json-format-v4.0-errata03-os-complete.html#_Toc453766634)
					id = odataEntry.Id != null ? odataEntry.Id.AbsoluteUri : null;
					readLink = odataEntry.ReadLink;
					editLink = odataEntry.EditLink;
					etag = odataEntry.ETag;
				}
				catch (ODataException)
				{
					// Ingored
				}
			}

			return new ODataEntryAnnotations {
				Id = id,
				TypeName = odataEntry.TypeName,
				ReadLink = readLink,
				EditLink = editLink,
				ETag = etag,
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
			if (value is ODataResource)
				return (value as ODataResource).Properties.ToDictionary(x => x.Name, x => GetPropertyValue(x.Value));
			if (value is ODataCollectionValue)
				return (value as ODataCollectionValue).Items.Select(GetPropertyValue).ToList();
			if (value is ODataEnumValue)
				return (value as ODataEnumValue).Value;
			if (value is ODataUntypedValue)
			{
				var result = (value as ODataUntypedValue).RawValue;
				if (!string.IsNullOrEmpty(result))
				{
					// Remove extra quoting as has been read as a string
					// Don't just replace \" in case we have embedded quotes
					if (result.StartsWith("\"") && result.EndsWith("\""))
					{
						result = result.Substring(1, result.Length - 2);
					}
				}

				return result;
			}

			if (value is ODataStreamReferenceValue)
				return CreateAnnotations(value as ODataStreamReferenceValue);
			return value;
		}
	}
}