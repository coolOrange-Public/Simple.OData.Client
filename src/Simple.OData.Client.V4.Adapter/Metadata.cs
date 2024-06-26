﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Vocabularies;

namespace Simple.OData.Client.V4.Adapter
{
	public class Metadata : MetadataBase
	{
		private readonly IEdmModel _model;

		public Metadata(IEdmModel model, INameMatchResolver nameMatchResolver, bool ignoreUnmappedProperties,
			bool unqualifiedNameCall)
			: base(nameMatchResolver, ignoreUnmappedProperties, unqualifiedNameCall)
		{
			_model = model;
		}

		public override bool HasStream(string collectionName)
		{
			return GetEntityType(collectionName).HasStream;
		}

		public override string GetNavigationPropertyMultiplicity(string collectionName, string propertyName)
		{
			return ExecuteOnNavigationProperty(collectionName, propertyName,
				navigationProperty => navigationProperty.TargetMultiplicity().ToString());
		}

		public override string GetNavigationPropertyPartnerMultiplicity(string collectionName, string propertyName)
		{
			return ExecuteOnNavigationProperty(collectionName, propertyName, navigationProperty =>
				(navigationProperty.Partner != null
					? navigationProperty.Partner.TargetMultiplicity()
					: navigationProperty.Type.TypeKind() == EdmTypeKind.Collection
						? EdmMultiplicity.Many
						: navigationProperty.Type.TypeKind() == EdmTypeKind.Entity
							? EdmMultiplicity.ZeroOrOne
							: EdmMultiplicity.Unknown).ToString());
		}

		T ExecuteOnNavigationProperty<T>(string collectionName, string propertyName,
			Func<IEdmNavigationProperty, T> execution)
		{
			var navigationProperty = GetNavigationProperty(GetEntityType(collectionName).Name, propertyName);
			return execution.Invoke(navigationProperty);
		}

		public override bool PropertyIsNullable(string collectionName, string propertyName)
		{
			var property = GetPropertyByName(collectionName, propertyName);
			return property.Type.IsNullable;
		}

		public override string GetPropertyDefaultValue(string collectionName, string propertyName)
		{
			var property = GetPropertyByName(collectionName, propertyName);
			if (property is IEdmStructuralProperty)
				return (property as IEdmStructuralProperty).DefaultValueString;
			return null;
		}

		public override Type GetPropertyType(string collectionName, string propertyName)
		{
			var property = GetPropertyByName(collectionName, propertyName);
			switch (property.Type.TypeKind())
			{
				case EdmTypeKind.Collection:
					var collectionElementType = property.Type.AsCollection().ElementType();
					var elementType = typeof(object);
					if (collectionElementType.TypeKind() == EdmTypeKind.Primitive)
						elementType = EdmTypeMap.GetTypes(collectionElementType).FirstOrDefault();
					return typeof(IEnumerable<>).MakeGenericType(elementType);
				case EdmTypeKind.Primitive:
					return EdmTypeMap.GetTypes(property.Type).FirstOrDefault();
				case EdmTypeKind.Enum:
					return typeof(Enum);
				default:
					return typeof(object);
			}
		}

		public override IEnumerable<string> GetEntitySetNames()
		{
			return GetEntitySets().Select(x => x.Name);
		}

		public override IEnumerable<string> GetEntityTypeNames()
		{
			return GetEntityTypes().Select(x => x.Name);
		}

		public override IEnumerable<string> GetNavigationPropertyNames(string collectionName)
		{
			return GetEntityType(collectionName).NavigationProperties().Select(x => x.Name);
		}

		/// <summary>
		/// Gets a collection of key name collections that represent the alternate keys of the given entity
		/// </summary>
		/// <see cref="https://github.com/OData/vocabularies/blob/master/OData.Community.Keys.V1.md"/>
		/// <param name="collectionName">The collection name of the entity</param>
		/// <returns>An enumeration of string enumerations representing the key names</returns>
		public override IEnumerable<IEnumerable<string>> GetAlternateKeyPropertyNames(string collectionName)
		{
			var entityType = GetEntityType(collectionName);
			return _model.GetAlternateKeysAnnotation(entityType)
				.Select(x => x.Select(y => y.Key));
		}

		public override IEnumerable<string> GetPropertyNames(string collectionName)
		{
			var keys = GetDeclaredKeyPropertyNames(collectionName);
			return GetEntityType(collectionName).StructuralProperties().Where(x => !keys.Contains(x.Name))
				.Select(x => x.Name);
		}

		public override string GetEntityTypeName(string entitySetName)
		{
			return GetEntitySet(entitySetName).EntityType().Name;
		}

		public override string GetEntityCollectionExactName(string collectionName)
		{
			IEdmEntitySet entitySet;
			IEdmSingleton singleton;
			IEdmEntityType entityType;
			if (TryGetEntitySet(collectionName, out entitySet))
			{
				return entitySet.Name;
			}
			else if (TryGetSingleton(collectionName, out singleton))
			{
				return singleton.Name;
			}
			else if (TryGetEntityType(collectionName, out entityType))
			{
				return entityType.Name;
			}

			throw new UnresolvableObjectException(collectionName,
				string.Format("Entity collection [{0}] not found", collectionName));
		}

		public override bool EntityCollectionRequiresOptimisticConcurrencyCheck(string collectionName)
		{
			IEdmEntitySet entitySet;
			IEdmSingleton singleton;
			IEdmEntityType entityType;
			IEdmVocabularyAnnotatable annotatable = null;
			if (TryGetEntitySet(collectionName, out entitySet))
			{
				annotatable = entitySet;
			}
			else if (TryGetSingleton(collectionName, out singleton))
			{
				annotatable = singleton;
			}
			else if (TryGetEntityType(collectionName, out entityType))
			{
				annotatable = entityType;
			}
			else
			{
				return false;
			}

			return annotatable.VocabularyAnnotations(_model).Any(x => x.Term.Name == "OptimisticConcurrency");
		}

		public override string GetDerivedEntityTypeExactName(string collectionName, string entityTypeName)
		{
			IEdmEntitySet entitySet;
			IEdmSingleton singleton;
			IEdmEntityType entityType;
			if (TryGetEntitySet(collectionName, out entitySet))
			{
				entityType = (_model.FindAllDerivedTypes(entitySet.EntityType())
					.BestMatch(x => (x as IEdmEntityType).Name, entityTypeName, NameMatchResolver)) as IEdmEntityType;
				if (entityType != null)
					return entityType.Name;
			}
			else if (TryGetSingleton(collectionName, out singleton))
			{
				entityType = (_model.FindDirectlyDerivedTypes(singleton.EntityType())
					.BestMatch(x => (x as IEdmEntityType).Name, entityTypeName, NameMatchResolver)) as IEdmEntityType;
				if (entityType != null)
					return entityType.Name;
			}
			else if (TryGetEntityType(entityTypeName, out entityType))
			{
				return entityType.Name;
			}

			throw new UnresolvableObjectException(entityTypeName,
				string.Format("Entity type [{0}] not found", entityTypeName));
		}

		public override string GetEntityTypeExactName(string collectionName)
		{
			var entityType = GetEntityTypes().BestMatch(x => x.Name, collectionName, NameMatchResolver);
			if (entityType != null)
				return entityType.Name;

			throw new UnresolvableObjectException(collectionName,
				string.Format("Entity type [{0}] not found", collectionName));
		}

		public override string GetLinkedCollectionName(string instanceTypeName, string typeName, out bool isSingleton)
		{
			isSingleton = false;
			IEdmEntitySet entitySet;
			IEdmSingleton singleton;
			IEdmEntityType entityType;

			if (TryGetEntitySet(instanceTypeName, out entitySet))
			{
				return entitySet.Name;
			}

			if (TryGetSingleton(instanceTypeName, out singleton))
			{
				isSingleton = true;
				return singleton.Name;
			}

			if (TryGetEntityType(instanceTypeName, out entityType))
			{
				return entityType.Name;
			}

			if (TryGetEntitySet(typeName, out entitySet))
			{
				return entitySet.Name;
			}

			if (TryGetSingleton(typeName, out singleton))
			{
				isSingleton = true;
				return singleton.Name;
			}

			if (TryGetEntityType(typeName, out entityType))
			{
				return entityType.Name;
			}

			throw new UnresolvableObjectException(typeName,
				string.Format("Linked collection for type [{0}] not found", instanceTypeName));
		}

		public override string GetQualifiedTypeName(string typeName)
		{
			IEdmEntityType entityType;
			if (TryGetEntityType(typeName, out entityType))
			{
				return string.Join(".", entityType.Namespace, entityType.Name);
			}

			IEdmComplexType complexType;
			if (TryGetComplexType(typeName, out complexType))
			{
				return string.Join(".", complexType.Namespace, complexType.Name);
			}

			IEdmEnumType enumType;
			if (TryGetEnumType(typeName, out enumType))
			{
				return string.Join(".", enumType.Namespace, enumType.Name);
			}

			throw new UnresolvableObjectException(typeName, string.Format("Type [{0}] not found", typeName));
		}

		public override bool IsOpenType(string collectionName)
		{
			return GetEntityType(collectionName).IsOpen;
		}

		public override bool IsTypeWithId(string collectionName)
		{
			IEdmEntityType entityType;
			if (TryGetEntityType(collectionName, out entityType))
				return entityType.DeclaredKey != null;
			else
				return false;
		}

		public override IEnumerable<string> GetStructuralPropertyNames(string collectionName)
		{
			return GetEntityType(collectionName).StructuralProperties().Select(x => x.Name);
		}

		public override bool HasStructuralProperty(string collectionName, string propertyName)
		{
			return GetEntityType(collectionName).StructuralProperties()
				.Any(x => NameMatchResolver.IsMatch(x.Name, propertyName));
		}

		public override string GetStructuralPropertyExactName(string collectionName, string propertyName)
		{
			return GetStructuralProperty(collectionName, propertyName).Name;
		}

		public override string GetStructuralPropertyPath(string collectionName, params string[] propertyNames)
		{
			if (propertyNames == null || propertyNames.Length == 0)
				throw new ArgumentNullException("propertyNames");
			var property = GetStructuralProperty(collectionName, propertyNames[0]);
			var exactNames = new List<string> { property.Name };

			for (var i = 1; i < propertyNames.Length; i++)
			{
				var entityType = GetComplexType(property.Type.FullName());
				property = GetStructuralProperty(entityType, propertyNames[i]);
				exactNames.Add(property.Name);

				if (property.Type.IsPrimitive())
					break;
			}

			return string.Join("/", exactNames.ToArray());
		}

		public override bool HasNavigationProperty(string collectionName, string propertyName)
		{
			return GetEntityType(collectionName).NavigationProperties()
				.Any(x => NameMatchResolver.IsMatch(x.Name, propertyName));
		}

		public override string GetNavigationPropertyExactName(string collectionName, string propertyName)
		{
			return GetNavigationProperty(collectionName, propertyName).Name;
		}

		public override string GetNavigationPropertyPartnerTypeName(string collectionName, string propertyName)
		{
			var navigationProperty = GetNavigationProperty(collectionName, propertyName);
			IEdmEntityType entityType;
			if (!TryGetEntityType(navigationProperty.Type, out entityType))
				throw new UnresolvableObjectException(propertyName,
					string.Format("No association found for [{0}].", propertyName));
			return entityType.Name;
		}

		public override bool IsNavigationPropertyCollection(string collectionName, string propertyName)
		{
			var property = GetNavigationProperty(collectionName, propertyName);
			return property.Type.Definition.TypeKind == EdmTypeKind.Collection;
		}

		public override IEnumerable<string> GetDeclaredKeyPropertyNames(string collectionName)
		{
			var entityType = GetEntityType(collectionName);
			while (entityType.DeclaredKey == null && entityType.BaseEntityType() != null)
			{
				entityType = entityType.BaseEntityType();
			}

			if (entityType.DeclaredKey == null)
				return Enumerable.Empty<string>();

			return entityType.DeclaredKey.Select(x => x.Name);
		}

		public override string GetFunctionFullName(string functionName)
		{
			var function = GetFunction(functionName);
			return function.IsBound && !UnqualifiedNameCall ? function.ShortQualifiedName() : function.Name;
		}

		public override EntityCollection GetFunctionReturnCollection(string functionName)
		{
			var function = GetFunction(functionName);

			if (function.ReturnType == null)
				return null;

			IEdmEntityType entityType;
			return !TryGetEntityType(function.ReturnType, out entityType)
				? null
				: new EntityCollection(entityType.Name);
		}

		public override string GetFunctionVerb(string functionName)
		{
			return RestVerbs.Get;
		}

		public override string GetActionFullName(string actionName)
		{
			var action = GetAction(actionName);
			return action.IsBound && !UnqualifiedNameCall ? action.ShortQualifiedName() : action.Name;
		}

		public override EntityCollection GetActionReturnCollection(string actionName)
		{
			var action = GetAction(actionName);

			if (action.ReturnType == null)
				return null;

			IEdmEntityType entityType;
			return !TryGetEntityType(action.ReturnType, out entityType) ? null : new EntityCollection(entityType.Name);
		}

		private IEnumerable<IEdmEntitySet> GetEntitySets()
		{
			return _model.SchemaElements
				.Where(x => x.SchemaElementKind == EdmSchemaElementKind.EntityContainer)
				.SelectMany(x => (x as IEdmEntityContainer).EntitySets());
		}

		private IEdmEntitySet GetEntitySet(string entitySetName)
		{
			IEdmEntitySet entitySet;
			if (TryGetEntitySet(entitySetName, out entitySet))
				return entitySet;

			throw new UnresolvableObjectException(entitySetName,
				string.Format("Entity set [{0}] not found", entitySetName));
		}

		private bool TryGetEntitySet(string entitySetName, out IEdmEntitySet entitySet)
		{
			if (entitySetName.Contains("/"))
				entitySetName = entitySetName.Split('/').First();

			entitySet = _model.SchemaElements
				.Where(x => x.SchemaElementKind == EdmSchemaElementKind.EntityContainer)
				.SelectMany(x => (x as IEdmEntityContainer).EntitySets())
				.BestMatch(x => x.Name, entitySetName, NameMatchResolver);

			return entitySet != null;
		}

		private IEnumerable<IEdmSingleton> GetSingletons()
		{
			return _model.SchemaElements
				.Where(x => x.SchemaElementKind == EdmSchemaElementKind.EntityContainer)
				.SelectMany(x => (x as IEdmEntityContainer).Singletons());
		}

		private IEdmSingleton GetSingleton(string singletonName)
		{
			IEdmSingleton singleton;
			if (TryGetSingleton(singletonName, out singleton))
				return singleton;

			throw new UnresolvableObjectException(singletonName,
				string.Format("Singleton [{0}] not found", singletonName));
		}

		private bool TryGetSingleton(string singletonName, out IEdmSingleton singleton)
		{
			if (singletonName.Contains("/"))
				singletonName = singletonName.Split('/').First();

			singleton = _model.SchemaElements
				.Where(x => x.SchemaElementKind == EdmSchemaElementKind.EntityContainer)
				.SelectMany(x => (x as IEdmEntityContainer).Singletons())
				.BestMatch(x => x.Name, singletonName, NameMatchResolver);

			return singleton != null;
		}

		private IEnumerable<IEdmEntityType> GetEntityTypes()
		{
			return _model.SchemaElements
				.Where(x => x.SchemaElementKind == EdmSchemaElementKind.TypeDefinition &&
				            (x as IEdmType).TypeKind == EdmTypeKind.Entity)
				.Select(x => x as IEdmEntityType);
		}

		private IEdmEntityType GetEntityType(string collectionName)
		{
			IEdmEntityType entityType;
			if (TryGetEntityType(collectionName, out entityType))
				return entityType;

			throw new UnresolvableObjectException(collectionName,
				string.Format("Entity type [{0}] not found", collectionName));
		}

		private bool TryGetEntityType(string collectionName, out IEdmEntityType entityType)
		{
			entityType = null;
			if (collectionName.Contains("/"))
			{
				var segments = GetCollectionPathSegments(collectionName).ToList();

				if (SegmentsIncludeTypeSpecification(segments))
				{
					var derivedTypeName = segments.Last();
					var derivedType = GetEntityTypes().SingleOrDefault(x => x.FullName() == derivedTypeName);
					if (derivedType != null)
					{
						entityType = derivedType;
						return true;
					}
				}
				else
				{
					var collection = NavigateToCollection(collectionName);
					var entityTypes = GetEntityTypes();
					entityType = entityTypes.SingleOrDefault(x => x.Name == collection.Name);
					if (entityType != null)
					{
						return true;
					}
					else
					{
						entityType =
							entityTypes.BestMatch(x => (x as IEdmEntityType).Name, collection.Name,
								NameMatchResolver) as IEdmEntityType;
						if (entityType != null)
							return true;
					}
				}
			}
			else
			{
				var entitySet = GetEntitySets()
					.BestMatch(x => x.Name, collectionName, NameMatchResolver);
				if (entitySet != null)
				{
					entityType = entitySet.EntityType();
					return true;
				}

				var singleton = GetSingletons()
					.BestMatch(x => x.Name, collectionName, NameMatchResolver);
				if (singleton != null)
				{
					entityType = singleton.EntityType();
					return true;
				}

				var derivedType = GetEntityTypes().BestMatch(x => x.Name, collectionName, NameMatchResolver);
				if (derivedType != null)
				{
					var baseType = GetEntityTypes()
						.SingleOrDefault(x => _model.FindDirectlyDerivedTypes(x).Contains(derivedType));
					if (baseType != null && GetEntitySets().Any(x => x.EntityType() == baseType))
					{
						entityType = derivedType;
						return true;
					}

					// Check if we can return it anyway
					entityType = derivedType;
					return true;
				}
			}

			return false;
		}

		private bool TryGetEntityType(IEdmTypeReference typeReference, out IEdmEntityType entityType)
		{
			entityType = typeReference.Definition.TypeKind == EdmTypeKind.Collection
				? (typeReference.Definition as IEdmCollectionType).ElementType.Definition as IEdmEntityType
				: typeReference.Definition.TypeKind == EdmTypeKind.Entity
					? typeReference.Definition as IEdmEntityType
					: null;
			return entityType != null;
		}

		private IEdmComplexType GetComplexType(string typeName)
		{
			IEdmComplexType complexType;
			if (TryGetComplexType(typeName, out complexType))
				return complexType;

			throw new UnresolvableObjectException(typeName, string.Format("Enum [{0}] not found", typeName));
		}

		private bool TryGetComplexType(string typeName, out IEdmComplexType complexType)
		{
			complexType = _model.SchemaElements
				.Where(x => x.SchemaElementKind == EdmSchemaElementKind.TypeDefinition &&
				            (x as IEdmType).TypeKind == EdmTypeKind.Complex)
				.Select(x => x as IEdmComplexType)
				.BestMatch(x => x.Name, typeName, NameMatchResolver);

			return complexType != null;
		}

		private IEdmEnumType GetEnumType(string typeName)
		{
			IEdmEnumType enumType;
			if (TryGetEnumType(typeName, out enumType))
				return enumType;

			throw new UnresolvableObjectException(typeName, string.Format("Enum [{0}] not found", typeName));
		}

		private bool TryGetEnumType(string typeName, out IEdmEnumType enumType)
		{
			enumType = _model.SchemaElements
				.Where(x => x.SchemaElementKind == EdmSchemaElementKind.TypeDefinition &&
				            (x as IEdmType).TypeKind == EdmTypeKind.Enum)
				.Select(x => x as IEdmEnumType)
				.BestMatch(x => x.Name, typeName, NameMatchResolver);

			return enumType != null;
		}

		private IEdmStructuralProperty GetStructuralProperty(string collectionName, string propertyName)
		{
			var edmType = GetEntityType(collectionName);
			return GetStructuralProperty(edmType, propertyName);
		}

		private IEdmStructuralProperty GetStructuralProperty(IEdmStructuredType edmType, string propertyName)
		{
			var property = edmType.StructuralProperties().BestMatch(
				x => x.Name, propertyName, NameMatchResolver);

			if (property == null)
				throw new UnresolvableObjectException(propertyName,
					string.Format("Structural property [{0}] not found", propertyName));

			return property;
		}


		IEdmProperty GetPropertyByName(string collectionName, string propertyName)
		{
			var property = GetEntityType(collectionName).FindProperty(propertyName);
			if (property == default(IEdmProperty))
				throw new UnresolvableObjectException(propertyName,
					string.Format("Property [{0}] not found", propertyName));
			return property;
		}

		private IEdmNavigationProperty GetNavigationProperty(string collectionName, string propertyName)
		{
			var property = GetEntityType(collectionName).NavigationProperties()
				.BestMatch(x => x.Name, propertyName, NameMatchResolver);

			if (property == null)
				throw new UnresolvableObjectException(propertyName,
					string.Format("Association [{0}] not found", propertyName));

			return property;
		}

		private IEdmFunction GetFunction(string functionName)
		{
			IEdmFunction function = null;
			var functionImport = _model.SchemaElements
				.Where(x => x.SchemaElementKind == EdmSchemaElementKind.EntityContainer)
				.SelectMany(x => (x as IEdmEntityContainer).Elements
					.Where(y => y.ContainerElementKind == EdmContainerElementKind.FunctionImport))
				.BestMatch(x => x.Name, functionName, NameMatchResolver) as IEdmFunctionImport;
			if (functionImport != null)
			{
				function = functionImport.Function;
			}

			if (function == null)
			{
				function = _model.SchemaElements
					.BestMatch(x => x.SchemaElementKind == EdmSchemaElementKind.Function,
						x => x.Name, functionName, NameMatchResolver) as IEdmFunction;
			}

			if (function == null)
				throw new UnresolvableObjectException(functionName,
					string.Format("Function [{0}] not found", functionName));

			return function;
		}

		private IEdmAction GetAction(string actionName)
		{
			IEdmAction action = null;
			var actionImport = _model.SchemaElements
				.Where(x => x.SchemaElementKind == EdmSchemaElementKind.EntityContainer)
				.SelectMany(x => (x as IEdmEntityContainer).Elements
					.Where(y => y.ContainerElementKind == EdmContainerElementKind.ActionImport))
				.BestMatch(x => x.Name, actionName, NameMatchResolver) as IEdmActionImport;
			if (actionImport != null)
			{
				action = actionImport.Action;
			}

			if (action == null)
			{
				action = _model.SchemaElements
					.BestMatch(x => x.SchemaElementKind == EdmSchemaElementKind.Action,
						x => x.Name, actionName, NameMatchResolver) as IEdmAction;
			}

			if (action == null)
				throw new UnresolvableObjectException(actionName, string.Format("Action [{0}] not found", actionName));

			return action;
		}
	}
}