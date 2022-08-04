﻿using System;
using System.Collections.Generic;
using System.Linq;
using Simple.OData.Client.Extensions;

namespace Simple.OData.Client
{
	public class ResolvedCommand
	{
		private readonly ISession _sesson;

		internal ResolvedCommand(FluentCommand command, ISession session)
		{
			_sesson = session;
			Details = new FluentCommandDetails(command.Details);

			ResolveCollectionName(command.Details);
			ResolveDerivedCollectionName(command.Details);
			ResolveLinkName(command.Details);
			ResolveEntityCollection(command.Details);
			ResolveKeys(command.Details);
			ResolveFilter(command.Details);
			ResolveEntryData(command.Details);
		}

		internal FluentCommandDetails Details { get; private set; }

		internal EntityCollection EntityCollection { get; private set; }

		public string QualifiedEntityCollectionName
		{
			get
			{
				var entityCollection = this.EntityCollection;
				return entityCollection.BaseEntityCollection == null
					? entityCollection.Name
					: string.Format("{0}/{1}", entityCollection.BaseEntityCollection.Name,
						_sesson.Metadata.GetQualifiedTypeName(entityCollection.Name));
			}
		}

		public string DynamicPropertiesContainerName
		{
			get { return Details.DynamicPropertiesContainerName; }
		}

		private void ResolveCollectionName(FluentCommandDetails details)
		{
			if (Details.CollectionName == null && !ReferenceEquals(details.CollectionExpression, null))
			{
				var collectionName = details.CollectionExpression.AsString(_sesson);
				var items = collectionName.Split('/');
				if (items.Count() > 1)
				{
					Details.CollectionName = items[0];
					Details.DerivedCollectionName = items[1];
				}
				else
				{
					Details.CollectionName = collectionName;
				}
			}
		}

		private void ResolveDerivedCollectionName(FluentCommandDetails details)
		{
			if (Details.DerivedCollectionName == null && !ReferenceEquals(details.DerivedCollectionExpression, null))
			{
				var derivedCollectionName = details.DerivedCollectionExpression.AsString(_sesson);
				Details.DerivedCollectionName = derivedCollectionName;
			}
		}

		private void ResolveLinkName(FluentCommandDetails details)
		{
			if (Details.LinkName == null && !ReferenceEquals(details.LinkExpression, null))
			{
				Details.LinkName = details.LinkExpression.AsString(_sesson);
			}
		}

		private void ResolveEntityCollection(FluentCommandDetails details)
		{
			if (string.IsNullOrEmpty(Details.CollectionName) && string.IsNullOrEmpty(Details.LinkName))
			{
				this.EntityCollection = null;
			}
			else
			{
				EntityCollection entityCollection;
				if (!string.IsNullOrEmpty(Details.LinkName))
				{
					var parent = new FluentCommand(Details.Parent).Resolve(_sesson);
					var collectionName = _sesson.Metadata.GetNavigationPropertyPartnerTypeName(
						parent.EntityCollection.Name, Details.LinkName);
					entityCollection = _sesson.Metadata.GetEntityCollection(collectionName);
				}
				else
				{
					entityCollection = _sesson.Metadata.GetEntityCollection(Details.CollectionName);
				}

				this.EntityCollection =
					string.IsNullOrEmpty(Details.DerivedCollectionName)
						? entityCollection
						: _sesson.Metadata.GetDerivedEntityCollection(entityCollection, Details.DerivedCollectionName);
			}
		}

		private void ResolveKeys(FluentCommandDetails details)
		{
			var namedKeyValues =
				details.KeyValues != null && details.KeyValues.Count == 1 &&
				_sesson.TypeCache.IsAnonymousType(details.KeyValues.First().GetType())
					? _sesson.TypeCache.ToDictionary(details.KeyValues.First())
					: details.NamedKeyValues;

			if (namedKeyValues != null)
			{
				IEnumerable<KeyValuePair<string, object>> matchingKey;
				bool isAlternateKey;
				if (NamedKeyValuesMatchAnyKey(namedKeyValues, out matchingKey, out isAlternateKey))
				{
					Details.NamedKeyValues = matchingKey.ToDictionary();
					Details.IsAlternateKey = isAlternateKey;
				}
				else
				{
					IEnumerable<KeyValuePair<string, object>> containedKey;
					if (TryExtractKeyFromNamedValues(namedKeyValues, out containedKey))
					{
						Details.NamedKeyValues = containedKey.ToDictionary();
					}
					else
					{
						Details.NamedKeyValues = null;
					}
				}

				Details.KeyValues = null;
			}
		}

		private void ResolveFilter(FluentCommandDetails details)
		{
			if (Details.Filter == null && !ReferenceEquals(details.FilterExpression, null))
			{
				bool isAlternateKey;
				Details.NamedKeyValues =
					TryInterpretFilterExpressionAsKey(details.FilterExpression, out isAlternateKey);
				Details.IsAlternateKey = isAlternateKey;

				if (Details.NamedKeyValues == null)
				{
					var entityCollection = this.EntityCollection;
					if (details.HasFunction)
					{
						var collection = _sesson.Metadata.GetFunctionReturnCollection(details.FunctionName);
						if (collection != null)
						{
							entityCollection = collection;
						}
					}

					Details.Filter = details.FilterExpression.Format(
						new ExpressionContext(_sesson, entityCollection, null, this.DynamicPropertiesContainerName));
				}
				else
				{
					Details.KeyValues = null;
					Details.TopCount = -1;
				}

				if (details.FilterExpression.HasTypeConstraint(details.DerivedCollectionName))
				{
					Details.DerivedCollectionName = null;
				}
			}
		}

		private void ResolveEntryData(FluentCommandDetails details)
		{
			if (details.EntryValue != null)
			{
				var value = details.EntryValue as IDictionary<string, object>;
				Details.EntryData = value ?? _sesson.TypeCache.ToDictionary(details.EntryValue);
				if (Details.BatchEntries != null)
					Details.BatchEntries.GetOrAdd(details.EntryValue, Details.EntryData);
			}
		}

		public IDictionary<string, object> FilterAsKey
		{
			get { return Details.NamedKeyValues; }
		}

		public ResolvedCommand WithCount()
		{
			Details.IncludeCount = true;
			return this;
		}

		public override string ToString()
		{
			return Format();
		}

		public string Format()
		{
			return _sesson.Adapter.GetCommandFormatter().FormatCommand(this);
		}

		internal IDictionary<string, object> KeyValues
		{
			get
			{
				if (!Details.HasKey)
					return null;

				var keyNames = _sesson.Metadata.GetDeclaredKeyPropertyNames(this.EntityCollection.Name).ToList();
				var namedKeyValues = new Dictionary<string, object>();
				for (int index = 0; index < keyNames.Count; index++)
				{
					bool found = false;
					object keyValue = null;
					if (Details.NamedKeyValues != null && Details.NamedKeyValues.Count > 0)
					{
						found = Details.NamedKeyValues.TryGetValue(keyNames[index], out keyValue);
						if (found && !(keyValue is ODataExpression))
						{
							var type = _sesson.Metadata.GetPropertyType(EntityCollection.Name, keyNames[index]);
							keyValue = _sesson.TypeCache.Convert(keyValue, type);
						}
					}
					else if (Details.KeyValues != null && Details.KeyValues.Count >= index)
					{
						keyValue = Details.KeyValues[index];
						found = true;
					}

					if (found)
					{
						var value = keyValue is ODataExpression ? (keyValue as ODataExpression).Value : keyValue;
						namedKeyValues.Add(keyNames[index], value);
					}
				}

				return namedKeyValues;
			}
		}

		internal IDictionary<string, object> CommandData
		{
			get
			{
				if (Details.EntryData == null)
					return new Dictionary<string, object>();
				if (string.IsNullOrEmpty(Details.DynamicPropertiesContainerName))
					return Details.EntryData;

				var entryData = new Dictionary<string, object>();
				foreach (var key in Details.EntryData.Keys.Where(x =>
					         !string.Equals(x, Details.DynamicPropertiesContainerName,
						         StringComparison.OrdinalIgnoreCase)))
				{
					entryData.Add(key, Details.EntryData[key]);
				}

				object dynamicProperties;
				if (Details.EntryData.TryGetValue(Details.DynamicPropertiesContainerName, out dynamicProperties) &&
				    dynamicProperties != null)
				{
					var kv = dynamicProperties as IDictionary<string, object>;
					if (kv != null)
					{
						foreach (var key in kv.Keys)
							entryData.Add(key, kv[key]);
					}
					else
					{
						throw new InvalidOperationException(string.Format(
							"Property {0} must implement IDictionary<string,object> interface",
							Details.DynamicPropertiesContainerName));
					}
				}

				return entryData;
			}
		}

		private IDictionary<string, object> TryInterpretFilterExpressionAsKey(ODataExpression expression,
			out bool isAlternateKey)
		{
			isAlternateKey = false;
			var ok = false;

			IDictionary<string, object> namedKeyValues = new Dictionary<string, object>();
			if (!ReferenceEquals(expression, null))
			{
				ok = expression.ExtractLookupColumns(namedKeyValues);
			}

			if (!ok)
				return null;

			IEnumerable<KeyValuePair<string, object>> matchingNamedKeyValues;
			if (NamedKeyValuesMatchAnyKey(namedKeyValues, out matchingNamedKeyValues, out isAlternateKey))
				return matchingNamedKeyValues.ToIDictionary();

			return null;
		}

		private bool NamedKeyValuesMatchAnyKey(IDictionary<string, object> namedKeyValues,
			out IEnumerable<KeyValuePair<string, object>> matchingNamedKeyValues, out bool isAlternateKey)
		{
			isAlternateKey = false;

			if (NamedKeyValuesMatchPrimaryKey(namedKeyValues, out matchingNamedKeyValues))
				return true;

			if (NamedKeyValuesMatchAlternateKey(namedKeyValues, out matchingNamedKeyValues))
			{
				isAlternateKey = true;
				return true;
			}

			return false;
		}

		private bool NamedKeyValuesMatchPrimaryKey(IDictionary<string, object> namedKeyValues,
			out IEnumerable<KeyValuePair<string, object>> matchingNamedKeyValues)
		{
			var keyNames = _sesson.Metadata.GetDeclaredKeyPropertyNames(this.EntityCollection.Name).ToList();

			return Utils.NamedKeyValuesMatchKeyNames(namedKeyValues, _sesson.Settings.NameMatchResolver, keyNames,
				out matchingNamedKeyValues);
		}

		private bool NamedKeyValuesMatchAlternateKey(IDictionary<string, object> namedKeyValues,
			out IEnumerable<KeyValuePair<string, object>> alternateKeyNamedValues)
		{
			alternateKeyNamedValues = null;

			var alternateKeys = _sesson.Metadata.GetAlternateKeyPropertyNames(this.EntityCollection.Name).ToList();

			foreach (var alternateKey in alternateKeys)
			{
				if (Utils.NamedKeyValuesMatchKeyNames(namedKeyValues, _sesson.Settings.NameMatchResolver, alternateKey,
					    out alternateKeyNamedValues))
					return true;
			}

			return false;
		}

		private bool TryExtractKeyFromNamedValues(IDictionary<string, object> namedValues,
			out IEnumerable<KeyValuePair<string, object>> matchingNamedKeyValues)
		{
			return Utils.NamedKeyValuesContainKeyNames(namedValues,
				_sesson.Settings.NameMatchResolver,
				_sesson.Metadata.GetDeclaredKeyPropertyNames(this.EntityCollection.Name),
				out matchingNamedKeyValues);
		}
	}
}