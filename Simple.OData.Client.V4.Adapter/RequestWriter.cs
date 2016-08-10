using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.OData.Core;
using Microsoft.OData.Edm;
using Microsoft.Spatial;
using Simple.OData.Client.Extensions;
using Simple.OData.Client.Http;

#pragma warning disable 1591

namespace Simple.OData.Client.V4.Adapter
{
	public class RequestWriter : RequestWriterBase
	{
		private readonly IEdmModel _model;
		readonly IEdmTypeMap _edmTypeMap = new EdmTypeMap();

		public RequestWriter(ISession session, IEdmModel model, Lazy<IBatchWriter> deferredBatchWriter)
			: base(session, deferredBatchWriter)
		{
			_model = model;
		}

		protected override async Task<Stream> WriteEntryContentAsync(string method, string collection, string commandText, IDictionary<string, object> entryData, bool resultRequired, bool deep)
		{
			IODataRequestMessageAsync message = IsBatch
				? await CreateBatchOperationMessageAsync(method, collection, entryData, commandText, resultRequired)
				: new ODataRequestMessage();

			if (method == RestVerbs.Get || method == RestVerbs.Delete)
				return null;

			var entityType = _model.FindDeclaredType(
				_session.Metadata.GetQualifiedTypeName(collection)) as IEdmEntityType;
			var model = method == RestVerbs.Patch ? new EdmDeltaModel(_model, entityType, entryData.Keys) : _model;

			using (var messageWriter = new ODataMessageWriter(message, GetWriterSettings(), model))
			{
				var contentId = _deferredBatchWriter != null ? _deferredBatchWriter.Value.GetContentId(entryData, null) : null;
				var entityCollection = _session.Metadata.NavigateToCollection(collection);
				var entryDetails = _session.Metadata.ParseEntryDetails(entityCollection.Name, entryData, contentId);

				var entryWriter = await messageWriter.CreateODataEntryWriterAsync();
				var entry = CreateODataEntry(entityType.FullName(), entryDetails.Properties);

				await entryWriter.WriteStartAsync(entry);
				await WriteNavigationLinks(entryDetails, entry, deep, entryWriter);
				await entryWriter.WriteEndAsync();
				return IsBatch ? null : await message.GetStreamAsync();
			}
		}

		protected override SlugHeader WriteEntrySlugHeader(string collection, IDictionary<string, object> entryData)
		{
			var entityType = _model.FindDeclaredType(
				_session.Metadata.GetQualifiedTypeName(collection)) as IEdmEntityType;
			var contentId = _deferredBatchWriter != null ? _deferredBatchWriter.Value.GetContentId(entryData, null) : null;
			var entityCollection = _session.Metadata.NavigateToCollection(collection);
			var entryDetails = _session.Metadata.ParseEntryDetails(entityCollection.Name, entryData, contentId);

			var entry = CreateODataEntry(entityType.FullName(), entryDetails.Properties);
			return entry.ToSlugHeader();
		}

		protected override async Task<Stream> WriteLinkContentAsync(string method, string commandText, string linkIdent)
		{
			var message = IsBatch
				? await CreateBatchOperationMessageAsync(method, null, null, commandText, false)
				: new ODataRequestMessage();

			using (var messageWriter = new ODataMessageWriter(message, GetWriterSettings(), _model))
			{
				var link = new ODataEntityReferenceLink
				{
					Url = Utils.CreateAbsoluteUri(_session.Settings.BaseUri.AbsoluteUri, linkIdent)
				};
				await messageWriter.WriteEntityReferenceLinkAsync(link);
				return IsBatch ? null : await message.GetStreamAsync();
			}
		}

		protected override async Task<Stream> WriteFunctionContentAsync(string method, string commandText)
		{
			if (IsBatch)
				await CreateBatchOperationMessageAsync(method, null, null, commandText, true);

			return null;
		}

		protected override async Task<Stream> WriteActionContentAsync(string method, string commandText, string actionName, IDictionary<string, object> parameters)
		{
			IODataRequestMessageAsync message = IsBatch
				? await CreateBatchOperationMessageAsync(method, null, null, commandText, true)
				: new ODataRequestMessage();

			using (var messageWriter = new ODataMessageWriter(message, GetWriterSettings(), _model))
			{
				var action = _model.SchemaElements.BestMatch(
					x => x.SchemaElementKind == EdmSchemaElementKind.Action,
					x => x.Name, actionName, _session.Pluralizer) as IEdmAction;
				var parameterWriter = await messageWriter.CreateODataParameterWriterAsync(action);

				await parameterWriter.WriteStartAsync();

				foreach (var parameter in parameters)
				{
					var operationParameter = action.Parameters.BestMatch(x => x.Name, parameter.Key, _session.Pluralizer);
					if (operationParameter == null)
						throw new UnresolvableObjectException(parameter.Key, string.Format("Parameter [{0}] not found for action [{1}]", parameter.Key, actionName));

					await WriteOperationParameterAsync(parameterWriter, operationParameter, parameter.Key, parameter.Value);
				}

				await parameterWriter.WriteEndAsync();
				return IsBatch ? null : await message.GetStreamAsync();
			}
		}

		private async Task WriteOperationParameterAsync(ODataParameterWriter parameterWriter, IEdmOperationParameter operationParameter, string paramName, object paramValue)
		{
			switch (operationParameter.Type.Definition.TypeKind)
			{
				case EdmTypeKind.Primitive:
				case EdmTypeKind.Complex:
					await parameterWriter.WriteValueAsync(paramName, paramValue);
					break;

				case EdmTypeKind.Enum:
					await parameterWriter.WriteValueAsync(paramName, new ODataEnumValue(paramValue.ToString()));
					break;

				case EdmTypeKind.Entity:
					var entryWriter = await parameterWriter.CreateEntryWriterAsync(paramName);
					var entry = CreateODataEntry(operationParameter.Type.Definition.FullTypeName(), paramValue.ToDictionary());
					await entryWriter.WriteStartAsync(entry);
					await entryWriter.WriteEndAsync();
					break;

				case EdmTypeKind.Collection:
					var collectionType = operationParameter.Type.Definition as IEdmCollectionType;
					var elementType = collectionType.ElementType;
					if (elementType.Definition.TypeKind == EdmTypeKind.Entity)
					{
						var feedWriter = await parameterWriter.CreateFeedWriterAsync(paramName);
						var feed = new ODataFeed();
						await feedWriter.WriteStartAsync(feed);
						foreach (var item in paramValue as IEnumerable)
						{
							var feedEntry = CreateODataEntry(elementType.Definition.FullTypeName(), item.ToDictionary());

							await feedWriter.WriteStartAsync(feedEntry);
							await feedWriter.WriteEndAsync();
						}
						await feedWriter.WriteEndAsync();
					}
					else
					{
						var collectionWriter = await parameterWriter.CreateCollectionWriterAsync(paramName);
						await collectionWriter.WriteStartAsync(new ODataCollectionStart());
						foreach (var item in paramValue as IEnumerable)
						{
							await collectionWriter.WriteItemAsync(item);
						}
						await collectionWriter.WriteEndAsync();
					}
					break;

				default:
					throw new NotSupportedException(string.Format("Unable to write action parameter of a type {0}", operationParameter.Type.Definition.TypeKind));
			}
		}

		protected override async Task<Stream> WriteStreamContentAsync(Stream stream, bool writeAsText)
		{
			var message = new ODataRequestMessage();
			using (var messageWriter = new ODataMessageWriter(message, GetWriterSettings(true), _model))
			{
				var value = writeAsText ? (object)Utils.StreamToString(stream) : Utils.StreamToByteArray(stream);
				await messageWriter.WriteValueAsync(value);
				return await message.GetStreamAsync();
			}
		}

		protected override string FormatLinkPath(string entryIdent, string navigationPropertyName, string linkIdent = null)
		{
			return linkIdent == null
				? string.Format("{0}/{1}/$ref", entryIdent, navigationPropertyName)
				: string.Format("{0}/{1}/$ref?$id={2}", entryIdent, navigationPropertyName, linkIdent);
		}

		protected override void AssignHeaders(ODataRequest request)
		{
			if (request.ResultRequired)
			{
				request.Headers.Add(HttpLiteral.Prefer, HttpLiteral.ReturnRepresentation);
			}
			else
			{
				request.Headers.Add(HttpLiteral.Prefer, HttpLiteral.ReturnMinimal);
			}
		}

		private async Task<IODataRequestMessageAsync> CreateBatchOperationMessageAsync(string method, string collection, IDictionary<string, object> entryData, string commandText, bool resultRequired)
		{
			var message = (await _deferredBatchWriter.Value.CreateOperationMessageAsync(
				Utils.CreateAbsoluteUri(_session.Settings.BaseUri.AbsoluteUri, commandText),
				method, collection, entryData, resultRequired)) as IODataRequestMessageAsync;

			return message;
		}

		async Task WriteNavigationLinks(EntryDetails entryDetails, Microsoft.OData.Core.ODataEntry entry, bool deep, ODataWriter entryWriter)
		{
			if (entryDetails.Links == null)
				return;

			foreach (var link in entryDetails.Links)
				if (link.Value.Any(x => x.LinkData != null))
				{

					if (deep)
						await WriteEntryAsync(entryWriter, entry, link.Key, link.Value);
					else
						await WriteLinkAsync(entryWriter, entry, link.Key, link.Value);
				}
		}

		async Task WriteEntryAsync(ODataWriter entryWriter, Microsoft.OData.Core.ODataEntry entry, string linkName, IEnumerable<ReferenceLink> links)
		{
			IEdmEntityType linkType;
			var navigationLink = CreateNavigationLink(entry, linkName, out linkType);
			await entryWriter.WriteStartAsync(navigationLink);
			if (navigationLink.IsCollection == true)
				await entryWriter.WriteStartAsync(new ODataFeed());

			foreach (var referenceLink in links)
			{
				var navigationTypeName = _session.Metadata.GetNavigationPropertyPartnerTypeName(entry.TypeName, referenceLink.LinkName);
				var navigationCollectionName = _session.Metadata.GetEntityCollectionExactName(navigationTypeName);
				var linkEntryDetails = _session.Metadata.ParseEntryDetails(navigationCollectionName, referenceLink.LinkData as Dictionary<string, object>, referenceLink.ContentId);
				var navigationEntityType = _model.FindDeclaredType(_session.Metadata.GetQualifiedTypeName(navigationCollectionName)) as IEdmEntityType;
				var linkEntry = CreateODataEntry(navigationEntityType.FullName(), linkEntryDetails.Properties);

				await entryWriter.WriteStartAsync(linkEntry);
				await WriteNavigationLinks(linkEntryDetails, linkEntry, true, entryWriter);
				await entryWriter.WriteEndAsync();
			}
			if (navigationLink.IsCollection == true)
				await entryWriter.WriteEndAsync();
			await entryWriter.WriteEndAsync();
		}

		private async Task WriteLinkAsync(ODataWriter entryWriter, Microsoft.OData.Core.ODataEntry entry, string linkName, IEnumerable<ReferenceLink> links)
		{
			IEdmEntityType linkType;
			var navigationLink = CreateNavigationLink(entry, linkName, out linkType);
			var linkTypeWithKey = linkType;
			while (linkTypeWithKey.DeclaredKey == null && linkTypeWithKey.BaseEntityType() != null)
				linkTypeWithKey = linkTypeWithKey.BaseEntityType();
			await entryWriter.WriteStartAsync(navigationLink);

			foreach (var referenceLink in links)
			{
				var linkKey = linkTypeWithKey.DeclaredKey;
				var linkEntry = referenceLink.LinkData.ToDictionary();
				var contentId = GetContentId(referenceLink);
				string linkUri;
				if (contentId != null)
				{
					linkUri = "$" + contentId;
				}
				else
				{
					bool isSingleton;
					var formattedKey = _session.Adapter.GetCommandFormatter().ConvertKeyValuesToUriLiteral(
						linkKey.ToDictionary(x => x.Name, x => linkEntry[x.Name]), true);
					var linkedCollectionName = _session.Metadata.GetLinkedCollectionName(
						referenceLink.LinkData.GetType().Name, linkTypeWithKey.Name, out isSingleton);
					linkUri = linkedCollectionName + (isSingleton ? string.Empty : formattedKey);
				}
				var link = new ODataEntityReferenceLink
				{
					Url = Utils.CreateAbsoluteUri(_session.Settings.BaseUri.AbsoluteUri, linkUri)
				};

				await entryWriter.WriteEntityReferenceLinkAsync(link);
			}

			await entryWriter.WriteEndAsync();
		}

		ODataNavigationLink CreateNavigationLink(Microsoft.OData.Core.ODataEntry entry, string linkName, out IEdmEntityType entityType)
		{
			var navigationProperty = (_model.FindDeclaredType(entry.TypeName) as IEdmEntityType).NavigationProperties()
				.BestMatch(x => x.Name, linkName, _session.Pluralizer);
			bool isCollection = navigationProperty.Type.Definition.TypeKind == EdmTypeKind.Collection;
			entityType = GetNavigationPropertyEntityType(navigationProperty);
			return new ODataNavigationLink
			{
				Name = linkName,
				IsCollection = isCollection,
				Url = new Uri(ODataNamespace.Related + entityType, UriKind.Absolute)
			};
		}

		private static IEdmEntityType GetNavigationPropertyEntityType(IEdmNavigationProperty navigationProperty)
		{
			if (navigationProperty.Type.Definition.TypeKind == EdmTypeKind.Collection)
				return (navigationProperty.Type.Definition as IEdmCollectionType).ElementType.Definition as IEdmEntityType;
			else
				return navigationProperty.Type.Definition as IEdmEntityType;
		}

		private ODataMessageWriterSettings GetWriterSettings(bool isRawValue = false)
		{
			var settings = new ODataMessageWriterSettings()
			{
				ODataUri = new ODataUri()
				{
					RequestUri = _session.Settings.BaseUri,
				},
				Indent = true,
				DisableMessageStreamDisposal = !IsBatch,
			};
			ODataFormat contentType;
			if (isRawValue)
			{
				contentType = ODataFormat.RawValue;
			}
			else
			{
				switch (_session.Settings.PayloadFormat)
				{
					case ODataPayloadFormat.Atom:
#pragma warning disable 0618
						contentType = ODataFormat.Atom;
#pragma warning restore 0618
						break;
					case ODataPayloadFormat.Json:
					default:
						contentType = ODataFormat.Json;
						break;
				}
			}
			settings.SetContentType(contentType);
			return settings;
		}

		private Microsoft.OData.Core.ODataEntry CreateODataEntry(string typeName, IDictionary<string, object> properties)
		{
			var entry = new Microsoft.OData.Core.ODataEntry() { TypeName = typeName };

			var typeProperties = (_model.FindDeclaredType(entry.TypeName) as IEdmEntityType).Properties();
			Func<string, string> findMatchingPropertyName = name =>
			{
				var property = typeProperties.BestMatch(y => y.Name, name, _session.Pluralizer);
				return property != null ? property.Name : name;
			};
			entry.Properties = properties.Select(x => new ODataProperty()
			{
				Name = findMatchingPropertyName(x.Key),
				Value = GetPropertyValue(typeProperties, x.Key, x.Value)
			}).ToList();

			return entry;
		}

		private object GetPropertyValue(IEnumerable<IEdmProperty> properties, string key, object value)
		{
			var property = properties.BestMatch(x => x.Name, key, _session.Pluralizer);
			return property != null ? GetPropertyValue(property.Type, value) : value;
		}

		private object GetPropertyValue(IEdmTypeReference propertyType, object value)
		{
			if (value == null)
				return value;

			switch (propertyType.TypeKind())
			{
				case EdmTypeKind.Complex:
					var complexTypeProperties = propertyType.AsComplex().StructuralProperties();
					return new ODataComplexValue
					{
						TypeName = propertyType.FullName(),
						Properties = value.ToDictionary()
							.Where(val => complexTypeProperties.Any(p => p.Name == val.Key))
							.Select(x => new ODataProperty
							{
								Name = x.Key,
								Value = GetPropertyValue(complexTypeProperties, x.Key, x.Value),
							})
					};

				case EdmTypeKind.Collection:
					var collection = propertyType.AsCollection();
					return new ODataCollectionValue()
					{
						TypeName = propertyType.FullName(),
						Items = ((IEnumerable)value).Cast<object>().Select(x => GetPropertyValue(collection.ElementType(), x)),
					};

				case EdmTypeKind.Primitive:
					var mappedTypes = _edmTypeMap.GetTypes(propertyType);
					if (mappedTypes.Any())
					{
						foreach (var mappedType in mappedTypes)
						{
							object result;
							if (Utils.TryConvert(value, mappedType, out result))
								return result;
						}
						throw new NotSupportedException(string.Format("Conversion is not supported from type {0} to OData type {1}", value.GetType(), propertyType));
					}
					return value;

				case EdmTypeKind.Enum:
					return new ODataEnumValue(value.ToString());

				case EdmTypeKind.None:
					if (CustomConverters.HasObjectConverter(value.GetType()))
					{
						return CustomConverters.Convert(value, value.GetType());
					}
					throw new NotSupportedException(string.Format("Conversion is not supported from type {0} to OData type {1}", value.GetType(), propertyType));

				default:
					return value;
			}
		}
	}
}