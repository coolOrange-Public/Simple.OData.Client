using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Edm;
using Microsoft.Data.OData;
using Simple.OData.Client.Extensions;
using Simple.OData.Client.Http;

namespace Simple.OData.Client.V3.Adapter
{
	public class RequestWriter : RequestWriterBase
	{
		private readonly IEdmModel _model;

		public RequestWriter(ISession session, IEdmModel model, Lazy<IBatchWriter> deferredBatchWriter)
			: base(session, deferredBatchWriter)
		{
			_model = model;
		}

		protected override async Task<Stream> WriteEntryContentAsync(string method, string collection,
			string commandText, IDictionary<string, object> entryData, bool resultRequired, bool deep = false)
		{
			var message = IsBatch
				? await CreateBatchOperationMessageAsync(method, collection, entryData, commandText, resultRequired)
					
				: new ODataRequestMessage();

			if (method == RestVerbs.Get || method == RestVerbs.Delete)
			{
				return null;
			}

			var entityType = _model.FindDeclaredType(
				_session.Metadata.GetQualifiedTypeName(collection)) as IEdmEntityType;
			var model = (method == RestVerbs.Patch || method == RestVerbs.Merge)
				? new EdmDeltaModel(_model, entityType, entryData.Keys)
				: _model;

			using (var messageWriter = new ODataMessageWriter(message, GetWriterSettings(), model))
			{
				var contentId = _deferredBatchWriter != null
					? _deferredBatchWriter.Value.GetContentId(entryData, null)
					: null;
				//var entityCollection = _session.Metadata.GetEntityCollection(collection);
				var entityCollection = _session.Metadata.NavigateToCollection(collection);
				var entryDetails = _session.Metadata.ParseEntryDetails(entityCollection.Name, entryData, contentId);

				var entryWriter = messageWriter.CreateODataEntryWriter();
				var entry = CreateODataEntry(entityType.FullName(), entryDetails.Properties);

				entryWriter.WriteStart(entry);
				WriteNavigationLinks(entryDetails, entry, deep, entryWriter);
				entryWriter.WriteEnd();

				if (IsBatch)
				{
					return null;
				}

				return await message.GetStreamAsync();
			}
		}

		protected override SlugHeader WriteEntrySlugHeader(string collection, IDictionary<string, object> entryData)
		{
			var entityType = _model.FindDeclaredType(
				_session.Metadata.GetQualifiedTypeName(collection)) as IEdmEntityType;
			var contentId = _deferredBatchWriter != null
				? _deferredBatchWriter.Value.GetContentId(entryData, null)
				: null;
			var entityCollection = _session.Metadata.NavigateToCollection(collection);
			var entryDetails = _session.Metadata.ParseEntryDetails(entityCollection.Name, entryData, contentId);
			var entry = CreateODataEntry(entityType.FullName(), entryDetails.Properties);

			return entry.ToSlugHeader(_session.Adapter);
		}

#pragma warning disable 1998
		protected override async Task<Stream> WriteLinkContentAsync(string method, string commandText, string linkIdent)
		{
			IODataRequestMessageAsync message = IsBatch
				? await CreateBatchOperationMessageAsync(method, null, null, commandText, false)
				: new ODataRequestMessage();

			using (var messageWriter = new ODataMessageWriter(message, GetWriterSettings(), _model))
			{
				var link = new ODataEntityReferenceLink {
					Url = Utils.CreateAbsoluteUri(_session.Settings.BaseUri.AbsoluteUri, linkIdent)
				};
				messageWriter.WriteEntityReferenceLink(link);

				if (IsBatch)
				{
					return null;
				}

				return await message.GetStreamAsync();
			}
		}

		protected async override Task<Stream> WriteFunctionContentAsync(string method, string commandText)
		{
			if (IsBatch)
			{
				await CreateBatchOperationMessageAsync(method, null, null, commandText, true);
			}

			return null;
		}

		protected override async Task<Stream> WriteActionContentAsync(string method, string commandText,
			string actionName, string boundTypeName, IDictionary<string, object> parameters)
		{
			var message = IsBatch
				? await CreateBatchOperationMessageAsync(method, null, null, commandText, true)
				: new ODataRequestMessage();

			using (var messageWriter = new ODataMessageWriter(message, GetWriterSettings(ODataFormat.Json), _model))
			{
				var action = _model.SchemaElements
					.Where(x => x.SchemaElementKind == EdmSchemaElementKind.EntityContainer)
					.SelectMany(x => (x as IEdmEntityContainer).FunctionImports())
					.BestMatch(x => x.Name, actionName, _session.Settings.NameMatchResolver);
				var parameterWriter = await messageWriter.CreateODataParameterWriterAsync(action);
				await parameterWriter.WriteStartAsync();


				foreach (var parameter in parameters)
				{
					var operationParameter = action.Parameters.BestMatch(x => x.Name, parameter.Key,
						_session.Settings.NameMatchResolver);
					if (operationParameter == null)
						throw new UnresolvableObjectException(parameter.Key,
							string.Format("Parameter [{0}] not found for action [{1}]", parameter.Key, actionName));

					await WriteOperationParameterAsync(parameterWriter, operationParameter, parameter.Key,
						parameter.Value);
				}

				await parameterWriter.WriteEndAsync();

				if (IsBatch)
				{
					return null;
				}

				return await message.GetStreamAsync();
			}
		}

		private async Task WriteOperationParameterAsync(ODataParameterWriter parameterWriter,
			IEdmFunctionParameter operationParameter, string paramName, object paramValue)
		{
			switch (operationParameter.Type.Definition.TypeKind)
			{
				case EdmTypeKind.Primitive:
				case EdmTypeKind.Complex:
					var value = GetPropertyValue(operationParameter.Type, paramValue);
					await parameterWriter.WriteValueAsync(paramName, value);
					break;

				case EdmTypeKind.Collection:
					var collectionWriter =
						await parameterWriter.CreateCollectionWriterAsync(paramName);
					await collectionWriter.WriteStartAsync(new ODataCollectionStart());
					foreach (var item in (IEnumerable)paramValue)
					{
						await collectionWriter.WriteItemAsync(item);
					}

					await collectionWriter.WriteEndAsync();
					break;

				default:
					throw new NotSupportedException(string.Format("Unable to write action parameter of a type {0}",
						operationParameter.Type.Definition.TypeKind));
			}
		}

		protected async override Task<Stream> WriteStreamContentAsync(Stream stream, bool writeAsText)
		{
			var message = new ODataRequestMessage();
			using (var messageWriter = new ODataMessageWriter(message, GetWriterSettings(ODataFormat.RawValue), _model))
			{
				var value = writeAsText ? (object)Utils.StreamToString(stream) : Utils.StreamToByteArray(stream);
				await messageWriter.WriteValueAsync(value);
				return await message.GetStreamAsync();
			}
		}

		protected override string FormatLinkPath(string entryIdent, string navigationPropertyName,
			string linkIdent = null)
		{
			return linkIdent == null
				? string.Format("{0}/$links/{1}", entryIdent, navigationPropertyName)
				: string.Format("{0}/$links/{1}", entryIdent, linkIdent);
		}

		protected override void AssignHeaders(ODataRequest request)
		{
			request.Headers[HttpLiteral.Prefer] =
				request.ResultRequired ? HttpLiteral.ReturnContent : HttpLiteral.ReturnNoContent;
		}

		private ODataMessageWriterSettings GetWriterSettings(ODataFormat preferredContentType = null)
		{
			var settings = new ODataMessageWriterSettings() {
				BaseUri = _session.Settings.BaseUri,
				Indent = true,
				DisableMessageStreamDisposal = !IsBatch,
			};
			ODataFormat contentType;
			if (preferredContentType != null)
			{
				contentType = preferredContentType;
			}
			else
			{
				switch (_session.Settings.PayloadFormat)
				{
					case ODataPayloadFormat.Atom:
					default:
						contentType = ODataFormat.Atom;
						break;
					case ODataPayloadFormat.Json:
						switch (_session.Adapter.ProtocolVersion)
						{
							case ODataProtocolVersion.V1:
							case ODataProtocolVersion.V2:
								contentType = ODataFormat.VerboseJson;
								break;
							default:
								contentType = ODataFormat.Json;
								break;
						}

						break;
				}
			}

			settings.SetContentType(contentType);
			return settings;
		}

		private Microsoft.Data.OData.ODataEntry CreateODataEntry(string typeName,
			IDictionary<string, object> properties)
		{
			var entry = new Microsoft.Data.OData.ODataEntry() { TypeName = typeName };

			var typeProperties = (_model.FindDeclaredType(entry.TypeName) as IEdmEntityType).Properties();
			Func<string, string> findMatchingPropertyName = name =>
			{
				var property = typeProperties.BestMatch(y => y.Name, name, _session.Settings.NameMatchResolver);
				return property != null ? property.Name : name;
			};
			entry.Properties = properties.Select(x => new ODataProperty() {
				Name = findMatchingPropertyName(x.Key),
				Value = GetPropertyValue(typeProperties, x.Key, x.Value)
			}).ToList();

			return entry;
		}

		private async Task<IODataRequestMessageAsync> CreateBatchOperationMessageAsync(string method, string collection,
			IDictionary<string, object> entryData, string commandText, bool resultRequired)
		{
			var message = (await _deferredBatchWriter.Value.CreateOperationMessageAsync(
				Utils.CreateAbsoluteUri(_session.Settings.BaseUri.AbsoluteUri, commandText),
				method, collection, entryData, resultRequired)) as IODataRequestMessageAsync;

			return message;
		}

		void WriteNavigationLinks(EntryDetails entryDetails, Microsoft.Data.OData.ODataEntry entry, bool deep,
			ODataWriter entryWriter)
		{
			if (entryDetails.Links == null)
				return;

			foreach (var link in entryDetails.Links)
				if (link.Value.Any(x => x.LinkData != null))
				{
					if (deep)
						WriteEntry(entryWriter, entry, link.Key, link.Value);
					else
						WriteLink(entryWriter, entry, link.Key, link.Value);
				}
		}

		private void WriteEntry(ODataWriter entryWriter, Microsoft.Data.OData.ODataEntry entry, string linkName,
			IEnumerable<ReferenceLink> links)
		{
			IEdmEntityType linkType;
			var navigationLink = CreateNavigationLink(entry, linkName, out linkType);
			entryWriter.WriteStart(navigationLink);
			if (navigationLink.IsCollection == true)
				entryWriter.WriteStart(new ODataFeed());

			foreach (var referenceLink in links)
			{
				var navigationTypeName =
					_session.Metadata.GetNavigationPropertyPartnerTypeName(entry.TypeName, referenceLink.LinkName);
				var navigationCollectionName = _session.Metadata.GetEntityCollectionExactName(navigationTypeName);
				var linkEntryDetails = _session.Metadata.ParseEntryDetails(navigationCollectionName,
					referenceLink.LinkData as Dictionary<string, object>, referenceLink.ContentId);
				var navigationEntityType =
					_model.FindDeclaredType(_session.Metadata.GetQualifiedTypeName(navigationCollectionName)) as
						IEdmEntityType;
				var linkEntry = CreateODataEntry(navigationEntityType.FullName(), linkEntryDetails.Properties);

				entryWriter.WriteStart(linkEntry);
				WriteNavigationLinks(linkEntryDetails, linkEntry, true, entryWriter);
				entryWriter.WriteEnd();
			}

			if (navigationLink.IsCollection == true)
				entryWriter.WriteEnd();
			entryWriter.WriteEnd();
		}

		private void WriteLink(ODataWriter entryWriter, Microsoft.Data.OData.ODataEntry entry, string linkName,
			IEnumerable<ReferenceLink> links)
		{
			IEdmEntityType linkType;
			var navigationLink = CreateNavigationLink(entry, linkName, out linkType);
			var linkTypeWithKey = linkType;
			while (linkTypeWithKey.DeclaredKey == null && linkTypeWithKey.BaseEntityType() != null)
				linkTypeWithKey = linkTypeWithKey.BaseEntityType();
			entryWriter.WriteStart(navigationLink);

			foreach (var referenceLink in links)
			{
				var linkKey = linkTypeWithKey.DeclaredKey;
				var linkEntry = referenceLink.LinkData.ToDictionary(TypeCache);
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

				var link = new ODataEntityReferenceLink {
					Url = Utils.CreateAbsoluteUri(_session.Settings.BaseUri.AbsoluteUri, linkUri)
				};

				entryWriter.WriteEntityReferenceLink(link);
			}

			entryWriter.WriteEnd();
		}

		ODataNavigationLink CreateNavigationLink(Microsoft.Data.OData.ODataEntry entry, string linkName,
			out IEdmEntityType entityType)
		{
			var navigationProperty = (_model.FindDeclaredType(entry.TypeName) as IEdmEntityType).NavigationProperties()
				.BestMatch(x => x.Name, linkName, _session.Settings.NameMatchResolver);
			bool isCollection = navigationProperty.Type.Definition.TypeKind == EdmTypeKind.Collection;
			entityType = GetNavigationPropertyEntityType(navigationProperty);
			return new ODataNavigationLink {
				Name = linkName,
				IsCollection = isCollection,
				Url = new Uri(ODataNamespace.Related + entityType, UriKind.Absolute)
			};
		}

		private static IEdmEntityType GetNavigationPropertyEntityType(IEdmNavigationProperty navigationProperty)
		{
			if (navigationProperty.Type.Definition.TypeKind == EdmTypeKind.Collection)
			{
				return (navigationProperty.Type.Definition as IEdmCollectionType).ElementType.Definition as
					IEdmEntityType;
			}
			else
			{
				return navigationProperty.Type.Definition as IEdmEntityType;
			}
		}

		private object GetPropertyValue(IEnumerable<IEdmProperty> properties, string key, object value)
		{
			var property = properties.BestMatch(x => x.Name, key, _session.Settings.NameMatchResolver);
			return property != null ? GetPropertyValue(property.Type, value) : value;
		}

		private object GetPropertyValue(IEdmTypeReference propertyType, object value)
		{
			if (value == null)
			{
				return value;
			}

			switch (propertyType.TypeKind())
			{
				case EdmTypeKind.Complex:
					if (Converter.HasObjectConverter(value.GetType()))
					{
						return Converter.Convert(value, value.GetType());
					}

					var complexTypeProperties = propertyType.AsComplex().StructuralProperties();
					return new ODataComplexValue {
						TypeName = propertyType.FullName(),
						Properties = value.ToDictionary(TypeCache)
							.Where(val => complexTypeProperties.Any(p => p.Name == val.Key))
							.Select(x => new ODataProperty {
								Name = x.Key,
								Value = GetPropertyValue(complexTypeProperties, x.Key, x.Value),
							})
					};

				case EdmTypeKind.Collection:
					var collection = propertyType.AsCollection();
					return new ODataCollectionValue() {
						TypeName = propertyType.FullName(),
						Items = ((IEnumerable)value).Cast<object>()
							.Select(x => GetPropertyValue(collection.ElementType(), x)),
					};

				case EdmTypeKind.Primitive:
					var mappedTypes = EdmTypeMap.Map.Where(x =>
						x.Value == (propertyType.Definition as IEdmPrimitiveType).PrimitiveKind);
					if (mappedTypes.Any())
					{
						foreach (var mappedType in mappedTypes)
						{
							object result;
							if (TypeCache.TryConvert(value, mappedType.Key, out result))
								return result;
						}

						throw new NotSupportedException(string.Format(
							"Conversion is not supported from type {0} to OData type {1}", value.GetType(),
							propertyType));
					}

					return value;

				case EdmTypeKind.Enum:
					return value.ToString();

				case EdmTypeKind.None:
					if (Converter.HasObjectConverter(value.GetType()))
					{
						return Converter.Convert(value, value.GetType());
					}

					throw new NotSupportedException(string.Format(
						"Conversion is not supported from type {0} to OData type {1}", value.GetType(), propertyType));

				default:
					return value;
			}
		}
	}
}