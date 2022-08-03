using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.OData;
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
		private readonly Dictionary<ODataResource, ResourceProperties> _resourceEntryMap;
		private readonly Dictionary<ODataResource, List<ODataResource>> _resourceEntries;

		public RequestWriter(ISession session, IEdmModel model, Lazy<IBatchWriter> deferredBatchWriter)
			: base(session, deferredBatchWriter)
		{
			_model = model;
			_resourceEntryMap = new Dictionary<ODataResource, ResourceProperties>();
			_resourceEntries = new Dictionary<ODataResource, List<ODataResource>>();
		}

		private void RegisterRootEntry(ODataResource root)
		{
			_resourceEntries.Add(root, new List<ODataResource>());
		}

		private void UnregisterRootEntry(ODataResource root)
		{
			List<ODataResource> entries;
			if (_resourceEntries.TryGetValue(root, out entries))
			{
				foreach (var entry in entries)
					_resourceEntryMap.Remove(entry);
				_resourceEntries.Remove(root);
			}
		}
		private async Task WriteEntryPropertiesAsync(ODataWriter entryWriter, ODataResource entry, IDictionary<string, List<ReferenceLink>> links)
		{
			await entryWriter.WriteStartAsync(entry).ConfigureAwait(false);
			ResourceProperties resourceEntry;
			if (_resourceEntryMap.TryGetValue(entry, out resourceEntry))
			{
				if (resourceEntry.CollectionProperties != null)
				{
					foreach (var prop in resourceEntry.CollectionProperties)
					{
						if (prop.Value != null)
						{
							await WriteNestedCollectionAsync(entryWriter, prop.Key, prop.Value).ConfigureAwait(false);
						}
					}
				}
				if (resourceEntry.StructuralProperties != null)
				{
					foreach (var prop in resourceEntry.StructuralProperties)
					{
						if (prop.Value != null)
						{
							await WriteNestedEntryAsync(entryWriter, prop.Key, prop.Value).ConfigureAwait(false);
						}
					}
				}
			}

			if (links != null)
			{
				foreach (var link in links)
				{
					if (link.Value.Any(x => x.LinkData != null))
					{
						await WriteLinkAsync(entryWriter, entry.TypeName, link.Key, link.Value).ConfigureAwait(false);
					}
				}
			}

			await entryWriter.WriteEndAsync().ConfigureAwait(false);
		}

		private async Task WriteNestedCollectionAsync(ODataWriter entryWriter, string entryName, ODataCollectionValue collection)
		{
			await entryWriter.WriteStartAsync(new ODataNestedResourceInfo()
			{
				Name = entryName,
				IsCollection = true,
			}).ConfigureAwait(false);

			await entryWriter.WriteStartAsync(new ODataResourceSet());
			foreach (var item in collection.Items)
			{
				await WriteEntryPropertiesAsync(entryWriter, item as ODataResource, null);
			}
			await entryWriter.WriteEndAsync().ConfigureAwait(false);

			await entryWriter.WriteEndAsync().ConfigureAwait(false);
		}

		private async Task WriteNestedEntryAsync(ODataWriter entryWriter, string entryName, ODataResource entry)
		{
			await entryWriter.WriteStartAsync(new ODataNestedResourceInfo()
			{
				Name = entryName,
				IsCollection = false,
			}).ConfigureAwait(false);

			await WriteEntryPropertiesAsync(entryWriter, entry, null).ConfigureAwait(false);

			await entryWriter.WriteEndAsync().ConfigureAwait(false);
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

				var entryWriter = await messageWriter.CreateODataResourceWriterAsync().ConfigureAwait(false);
				var entry = CreateODataEntry(entityType.FullName(), entryDetails.Properties, null);

				await entryWriter.WriteStartAsync(entry).ConfigureAwait(false);
				await WriteNavigationLinks(entryDetails, entry, deep, entryWriter).ConfigureAwait(false);
				await entryWriter.WriteEndAsync().ConfigureAwait(false);

				return IsBatch ? null : await message.GetStreamAsync().ConfigureAwait(false);
			}
		}

		protected override SlugHeader WriteEntrySlugHeader(string collection, IDictionary<string, object> entryData)
		{
			var entityType = _model.FindDeclaredType(
				_session.Metadata.GetQualifiedTypeName(collection)) as IEdmEntityType;
			var contentId = _deferredBatchWriter != null ? _deferredBatchWriter.Value.GetContentId(entryData, null) : null;
			var entityCollection = _session.Metadata.NavigateToCollection(collection);
			var entryDetails = _session.Metadata.ParseEntryDetails(entityCollection.Name, entryData, contentId);

			var entry = CreateODataEntry(entityType.FullName(), entryDetails.Properties, null);
			return entry.ToSlugHeader();
		}

		protected override async Task<Stream> WriteLinkContentAsync(string method, string commandText, string linkIdent)
		{
			var message = IsBatch
				? await CreateBatchOperationMessageAsync(method, null, null, commandText, false).ConfigureAwait(false)
				: new ODataRequestMessage();

			using (var messageWriter = new ODataMessageWriter(message, GetWriterSettings(), _model))
			{
				var link = new ODataEntityReferenceLink
				{
					Url = Utils.CreateAbsoluteUri(_session.Settings.BaseUri.AbsoluteUri, linkIdent)
				};
				await messageWriter.WriteEntityReferenceLinkAsync(link).ConfigureAwait(false);
				return IsBatch ? null : await message.GetStreamAsync().ConfigureAwait(false);
			}
		}

		protected override async Task<Stream> WriteFunctionContentAsync(string method, string commandText)
		{
			if (IsBatch)
				await CreateBatchOperationMessageAsync(method, null, null, commandText, true).ConfigureAwait(false);

			return null;
		}

		protected override async Task<Stream> WriteActionContentAsync(string method, string commandText, string actionName, string boundTypeName, IDictionary<string, object> parameters)
		{
			var message = IsBatch
				? await CreateBatchOperationMessageAsync(method, null, null, commandText, true).ConfigureAwait(false)
				: new ODataRequestMessage();

			using (var messageWriter = new ODataMessageWriter(message, GetWriterSettings(), _model))
			{
				Func<IEdmOperationParameter, IEdmType, bool> typeMatch = (parameter, baseType) =>
					parameter == null ||
					parameter.Type.Definition == baseType ||
					parameter.Type.Definition.TypeKind == EdmTypeKind.Collection &&
						(parameter.Type.Definition as IEdmCollectionType).ElementType.Definition == baseType;

				var action = boundTypeName == null
					? _model.SchemaElements.BestMatch(
						x => x.SchemaElementKind == EdmSchemaElementKind.Action,
						x => x.Name, actionName, _session.Settings.NameMatchResolver) as IEdmAction
					: _model.SchemaElements.BestMatch(
						x => x.SchemaElementKind == EdmSchemaElementKind.Action
							 && typeMatch(
								 ((IEdmAction)x).Parameters.FirstOrDefault(p => p.Name == "bindingParameter"),
								 _model.FindDeclaredType(boundTypeName)),
						x => x.Name, actionName, _session.Settings.NameMatchResolver) as IEdmAction;
				var parameterWriter = await messageWriter.CreateODataParameterWriterAsync(action).ConfigureAwait(false);

				await parameterWriter.WriteStartAsync().ConfigureAwait(false);

				foreach (var parameter in parameters)
				{
					var operationParameter = action.Parameters.BestMatch(x => x.Name, parameter.Key, _session.Settings.NameMatchResolver);
					if (operationParameter == null)
						throw new UnresolvableObjectException(parameter.Key, string.Format("Parameter [{0}] not found for action [{1}]", parameter.Key, actionName));
					await WriteOperationParameterAsync(parameterWriter, operationParameter, parameter.Key, parameter.Value).ConfigureAwait(false);
				}

				await parameterWriter.WriteEndAsync().ConfigureAwait(false);
				return IsBatch ? null : await message.GetStreamAsync().ConfigureAwait(false);
			}
		}
		private async Task WriteOperationParameterAsync(ODataParameterWriter parameterWriter, IEdmOperationParameter operationParameter, string paramName, object paramValue)
		{
			switch (operationParameter.Type.Definition.TypeKind)
			{
				case EdmTypeKind.Primitive:
					var value = GetPropertyValue(operationParameter.Type, paramValue, null);
					await parameterWriter.WriteValueAsync(paramName, value).ConfigureAwait(false);
					break;

				case EdmTypeKind.Enum:
					await parameterWriter.WriteValueAsync(paramName, new ODataEnumValue(paramValue.ToString())).ConfigureAwait(false);
					break;

				case EdmTypeKind.Untyped:
					await parameterWriter.WriteValueAsync(paramName, new ODataUntypedValue { RawValue = paramValue.ToString() }).ConfigureAwait(false);
					break;

				case EdmTypeKind.Entity:
					{
						var entryWriter = await parameterWriter.CreateResourceWriterAsync(paramName).ConfigureAwait(false);
						var paramValueDict = paramValue.ToDictionary(TypeCache);
						var contentId = _deferredBatchWriter != null ? _deferredBatchWriter.Value.GetContentId(paramValueDict, null) : null;

						var typeName = operationParameter.Type.Definition.FullTypeName();
						if (paramValueDict.ContainsKey("@odata.type") && paramValueDict["@odata.type"] is string)
						{
							typeName = paramValueDict["@odata.type"] as string;
							paramValueDict.Remove("@odata.type");
						}

						var entryDetails = _session.Metadata.ParseEntryDetails(typeName, paramValueDict, contentId);
						var entry = CreateODataEntry(typeName, entryDetails.Properties, null);

						RegisterRootEntry(entry);
						await WriteEntryPropertiesAsync(entryWriter, entry, entryDetails.Links).ConfigureAwait(false);
						UnregisterRootEntry(entry);
					}
					break;
				case EdmTypeKind.Complex:
					{
						var entryWriter = await parameterWriter.CreateResourceWriterAsync(paramName).ConfigureAwait(false);
						var paramValueDict = paramValue.ToDictionary(TypeCache);

						var typeName = operationParameter.Type.Definition.FullTypeName();
						if (paramValueDict.ContainsKey("@odata.type") && paramValueDict["@odata.type"] is string)
						{
							typeName = paramValueDict["@odata.type"] as string;
							paramValueDict.Remove("@odata.type");
						}

						var entry = CreateODataEntry(typeName, paramValueDict, null);

						RegisterRootEntry(entry);
						await WriteEntryPropertiesAsync(entryWriter, entry, new Dictionary<string, List<ReferenceLink>>()).ConfigureAwait(false);
						UnregisterRootEntry(entry);
					}
					break;

				case EdmTypeKind.Collection:
					var collectionType = operationParameter.Type.Definition as IEdmCollectionType;
					var elementType = collectionType.ElementType;
					if (elementType.Definition.TypeKind == EdmTypeKind.Entity)
					{
						var feedWriter = await parameterWriter.CreateResourceSetWriterAsync(paramName).ConfigureAwait(false);
						var feed = new ODataResourceSet();
						await feedWriter.WriteStartAsync(feed).ConfigureAwait(false);
						foreach (var item in (IEnumerable)paramValue)
						{
							var feedEntry = CreateODataEntry(elementType.Definition.FullTypeName(), item.ToDictionary(TypeCache), null);

							RegisterRootEntry(feedEntry);
							await feedWriter.WriteStartAsync(feedEntry).ConfigureAwait(false);
							await feedWriter.WriteEndAsync().ConfigureAwait(false);
							UnregisterRootEntry(feedEntry);
						}
						await feedWriter.WriteEndAsync().ConfigureAwait(false);
					}
					else
					{
						var collectionWriter = await parameterWriter.CreateCollectionWriterAsync(paramName).ConfigureAwait(false);
						await collectionWriter.WriteStartAsync(new ODataCollectionStart()).ConfigureAwait(false);
						foreach (var item in (IEnumerable)paramValue)
						{
							await collectionWriter.WriteItemAsync(item).ConfigureAwait(false);
						}
						await collectionWriter.WriteEndAsync().ConfigureAwait(false);
					}
					break;

				default:
					throw new NotSupportedException(string.Format("Unable to write action parameter of a type {0}", operationParameter.Type.Definition.TypeKind));
			}
		}

		protected override async Task<Stream> WriteStreamContentAsync(Stream stream, bool writeAsText)
		{
			var message = new ODataRequestMessage();
			using (var messageWriter = new ODataMessageWriter(message, GetWriterSettings(ODataFormat.RawValue), _model))
			{
				var value = writeAsText ? (object)Utils.StreamToString(stream) : Utils.StreamToByteArray(stream);
				await messageWriter.WriteValueAsync(value).ConfigureAwait(false);
				return await message.GetStreamAsync().ConfigureAwait(false);
			}
		}

		protected override string FormatLinkPath(string entryIdent, string navigationPropertyName, string linkIdent = null)
		{
			var linkPath = string.Format("{0}/{1}/$ref", entryIdent, navigationPropertyName);
			if (linkIdent != null)
			{
				var link = _session.Settings.UseAbsoluteReferenceUris
					? Utils.CreateAbsoluteUri(_session.Settings.BaseUri.AbsoluteUri, linkIdent).AbsoluteUri
					: linkIdent;
				linkPath += string.Format("$id={0}", link);
			}

			return linkPath;
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
				method, collection, entryData, resultRequired).ConfigureAwait(false)) as IODataRequestMessageAsync;

			return message;
		}

		async Task WriteNavigationLinks(EntryDetails entryDetails, ODataResource entry, bool deep, ODataWriter entryWriter)
		{
			if (entryDetails.Links == null)
				return;

			foreach (var link in entryDetails.Links)
				if (link.Value.Any(x => x.LinkData != null))
				{

					if (deep)
						await WriteEntryAsync(entryWriter, entry, link.Key, link.Value).ConfigureAwait(false);
					else
						await WriteLinkAsync(entryWriter, entry.TypeName, link.Key, link.Value).ConfigureAwait(false);
				}
		}

		async Task WriteEntryAsync(ODataWriter entryWriter, ODataResource entry, string linkName, IEnumerable<ReferenceLink> links)
		{
			IEdmEntityType linkType;
			var navigationLink = CreateNavigationLink(entry, linkName, out linkType);
			await entryWriter.WriteStartAsync(navigationLink).ConfigureAwait(false);
			if (navigationLink.IsCollection != null ? navigationLink.IsCollection.Value : false)
				await entryWriter.WriteStartAsync(new ODataResourceSet()).ConfigureAwait(false);

			foreach (var referenceLink in links)
			{
				var navigationTypeName = _session.Metadata.GetNavigationPropertyPartnerTypeName(entry.TypeName, referenceLink.LinkName);
				var navigationCollectionName = _session.Metadata.GetEntityCollectionExactName(navigationTypeName);
				var linkEntryDetails = _session.Metadata.ParseEntryDetails(navigationCollectionName, referenceLink.LinkData as Dictionary<string, object>, referenceLink.ContentId);
				var navigationEntityType = _model.FindDeclaredType(_session.Metadata.GetQualifiedTypeName(navigationCollectionName)) as IEdmEntityType;
				var linkEntry = CreateODataEntry(navigationEntityType.FullName(), linkEntryDetails.Properties, null);

				await entryWriter.WriteStartAsync(linkEntry).ConfigureAwait(false);
				await WriteNavigationLinks(linkEntryDetails, linkEntry, true, entryWriter).ConfigureAwait(false);
				await entryWriter.WriteEndAsync().ConfigureAwait(false);
			}
			if (navigationLink.IsCollection != null ? navigationLink.IsCollection.Value : false)
				await entryWriter.WriteEndAsync().ConfigureAwait(false);
			await entryWriter.WriteEndAsync().ConfigureAwait(false);
		}

		private async Task WriteLinkAsync(ODataWriter entryWriter, string typeName, string linkName, IEnumerable<ReferenceLink> links)
		{
			var navigationProperty = (_model.FindDeclaredType(typeName) as IEdmEntityType).NavigationProperties()
				.BestMatch(x => x.Name, linkName, _session.Settings.NameMatchResolver);
			var isCollection = navigationProperty.Type.Definition.TypeKind == EdmTypeKind.Collection;

			var linkType = GetNavigationPropertyEntityType(navigationProperty);
			var linkTypeWithKey = linkType;
			while (linkTypeWithKey.DeclaredKey == null && linkTypeWithKey.BaseEntityType() != null)
			{
				linkTypeWithKey = linkTypeWithKey.BaseEntityType();
			}

			await entryWriter.WriteStartAsync(new ODataNestedResourceInfo()
			{
				Name = linkName,
				IsCollection = isCollection,
				Url = new Uri(ODataNamespace.Related + linkType, UriKind.Absolute),
			}).ConfigureAwait(false);

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
					var formattedKey = _session.Adapter.GetCommandFormatter().ConvertKeyValuesToUriLiteral(
						linkKey.ToDictionary(x => x.Name, x => linkEntry[x.Name]), true);
					bool isSingleton;
					var linkedCollectionName = _session.Metadata.GetLinkedCollectionName(
						referenceLink.LinkData.GetType().Name, linkTypeWithKey.Name, out isSingleton);
					linkUri = linkedCollectionName + (isSingleton ? string.Empty : formattedKey);
				}
				var link = new ODataEntityReferenceLink
				{
					Url = Utils.CreateAbsoluteUri(_session.Settings.BaseUri.AbsoluteUri, linkUri)
				};

				await entryWriter.WriteEntityReferenceLinkAsync(link).ConfigureAwait(false);
			}

			await entryWriter.WriteEndAsync().ConfigureAwait(false);
		}

		ODataNestedResourceInfo CreateNavigationLink(ODataResource entry, string linkName, out IEdmEntityType entityType)
		{
			var navigationProperty = (_model.FindDeclaredType(entry.TypeName) as IEdmEntityType).NavigationProperties()
				.BestMatch(x => x.Name, linkName, _session.Settings.NameMatchResolver);
			bool isCollection = navigationProperty.Type.Definition.TypeKind == EdmTypeKind.Collection;
			entityType = GetNavigationPropertyEntityType(navigationProperty);
			return new ODataNestedResourceInfo
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

		private ODataMessageWriterSettings GetWriterSettings(ODataFormat preferredContentType = null)
		{
			var settings = new ODataMessageWriterSettings()
			{
				ODataUri = new ODataUri()
				{
					RequestUri = _session.Settings.BaseUri,
				},
				EnableMessageStreamDisposal = IsBatch,
				Validations = (Microsoft.OData.ValidationKinds)_session.Settings.Validations
			};
			var contentType = preferredContentType != null ? preferredContentType : ODataFormat.Json;
			settings.SetContentType(contentType);
			return settings;
		}

		private ODataResource CreateODataEntry(string typeName, IDictionary<string, object> properties, ODataResource root)
		{
			var entry = new ODataResource { TypeName = typeName };
			root = root != null ? root : entry;

			var entryType = _model.FindDeclaredType(entry.TypeName);
			var typeProperties = typeof(IEdmEntityType).IsTypeAssignableFrom(entryType.GetType())
				? (entryType as IEdmEntityType).Properties().ToList()
				: (entryType as IEdmComplexType).Properties().ToList();

			var resourceEntry = new ResourceProperties(entry);
			entry.Properties = properties
				.Where(x => IsPrimitive(FindMatchingPropertyType(x.Key, typeProperties)))
				.Select(x => new ODataProperty
				{
					Name = FindMatchingPropertyName(x.Key, typeProperties),
					Value = GetPropertyValue(typeProperties, x.Key, x.Value, root)
				}).ToList();
			resourceEntry.CollectionProperties = properties
				.Where(x => IsStructuralCollection(FindMatchingPropertyType(x.Key, typeProperties)))
				.Select(x => new KeyValuePair<string, ODataCollectionValue>(
					FindMatchingPropertyName(x.Key, typeProperties),
					GetPropertyValue(typeProperties, x.Key, x.Value, root) as ODataCollectionValue))
				.ToDictionary();
			resourceEntry.StructuralProperties = properties
				.Where(x => IsStructural(FindMatchingPropertyType(x.Key, typeProperties)))
				.Select(x => new KeyValuePair<string, ODataResource>(
					FindMatchingPropertyName(x.Key, typeProperties),
					GetPropertyValue(typeProperties, x.Key, x.Value, root) as ODataResource))
				.ToDictionary();
			_resourceEntryMap.Add(entry, resourceEntry);
			List<ODataResource> entries;
			if (root != null && _resourceEntries.TryGetValue(root, out entries))
				entries.Add(entry);

			return entry;
		}

		IEdmTypeReference FindMatchingPropertyType(string name, List<IEdmProperty> typeProperties)
		{
			var property = typeProperties.BestMatch(y => y.Name, name, _session.Settings.NameMatchResolver);
			return property != null ? property.Type : null;
		}

		bool IsPrimitive(IEdmTypeReference type)
		{
			return !IsStructural(type) && !IsStructuralCollection(type);
		}

		bool IsStructuralCollection(IEdmTypeReference type)
		{
			return type != null && type.TypeKind() == EdmTypeKind.Collection &&
				   type.AsCollection().ElementType().TypeKind() == EdmTypeKind.Complex;
		}

		bool IsStructural(IEdmTypeReference type)
		{
			return type != null && type.TypeKind() == EdmTypeKind.Complex;
		}

		string FindMatchingPropertyName(string name, List<IEdmProperty> typeProperties)
		{
			var property = typeProperties.BestMatch(y => y.Name, name, _session.Settings.NameMatchResolver);
			return property != null ? property.Name : name;
		}

		object GetPropertyValue(IEnumerable<IEdmProperty> properties, string key, object value, ODataResource root)
		{
			var property = properties.BestMatch(x => x.Name, key, _session.Settings.NameMatchResolver);
			return property != null ? GetPropertyValue(property.Type, value, root) : value;
		}

		object GetPropertyValue(IEdmTypeReference propertyType, object value, ODataResource root)
		{
			if (value == null)
				return value;

			switch (propertyType.TypeKind())
			{
				case EdmTypeKind.Complex:
					if (Converter.HasObjectConverter(value.GetType()))
					{
						return Converter.Convert(value, value.GetType());
					}
					return CreateODataEntry(propertyType.FullName(), value.ToDictionary(TypeCache), root);

				case EdmTypeKind.Collection:
					var collection = propertyType.AsCollection();
					return new ODataCollectionValue
					{
						TypeName = propertyType.FullName(),
						Items = ((IEnumerable)value).Cast<object>().Select(x => GetPropertyValue(collection.ElementType(), x, root)),
					};

				case EdmTypeKind.Primitive:
					var mappedTypes = EdmTypeMap.Map.Where(x => x.Value == ((IEdmPrimitiveType)propertyType.Definition).PrimitiveKind);
					if (mappedTypes.Any())
					{
						foreach (var mappedType in mappedTypes)
						{
							object result;
							if (TryConvert(value, mappedType.Key, out result))
								return result;
							else if (TypeCache.TryConvert(value, mappedType.Key, out result))
								return result;
						}
						throw new NotSupportedException(string.Format("Conversion is not supported from type {0} to OData type {1}", value.GetType(), propertyType));
					}
					return value;

				case EdmTypeKind.Enum:
					return new ODataEnumValue(value.ToString());

				case EdmTypeKind.None:
					if (Converter.HasObjectConverter(value.GetType()))
					{
						return Converter.Convert(value, value.GetType());
					}
					throw new NotSupportedException(string.Format("Conversion is not supported from type {0} to OData type {1}", value.GetType(), propertyType));

				default:
					return value;
			}
		}

		public static bool TryConvert(object value, Type targetType, out object result)
		{
			try
			{
				if ((targetType == typeof(Date) || targetType == typeof(Date?)) && value is DateTimeOffset)
				{
					var dto = (DateTimeOffset)value;
					result = new Date(dto.Year, dto.Month, dto.Day);
					return true;
				}
				else if ((targetType == typeof(Date) || targetType == typeof(Date?)) && value is DateTime)
				{
					var dt = (DateTime)value;
					result = new Date(dt.Year, dt.Month, dt.Day);
					return true;
				}
				result = null;
				return false;
			}
			catch (Exception)
			{
				result = null;
				return false;
			}
		}
	}
}