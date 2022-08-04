﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Simple.OData.Client.Extensions
{
	static class DictionaryExtensions
	{
		private static ConcurrentDictionary<Type, ActivatorDelegate> _defaultActivators =
			new ConcurrentDictionary<Type, ActivatorDelegate>();

		private static ConcurrentDictionary<Tuple<Type, Type>, ActivatorDelegate> _collectionActivators =
			new ConcurrentDictionary<Tuple<Type, Type>, ActivatorDelegate>();

		internal static Func<IDictionary<string, object>, ITypeCache, ODataEntry> CreateDynamicODataEntry { get; set; }

		internal static void ClearCache()
		{
			_defaultActivators = new ConcurrentDictionary<Type, ActivatorDelegate>();
			_collectionActivators = new ConcurrentDictionary<Tuple<Type, Type>, ActivatorDelegate>();
		}

		public static T ToObject<T>(this IDictionary<string, object> source, ITypeCache typeCache,
			bool dynamicObject = false)
			where T : class
		{
			if (typeCache == null)
			{
				throw new ArgumentNullException("typeCache");
			}

			if (source == null)
				return default(T);

			if (typeCache.IsTypeAssignableFrom(typeof(IDictionary<string, object>), typeof(T)))
				return source as T;

			if (typeof(T) == typeof(ODataEntry))
				return CreateODataEntry(source, typeCache, dynamicObject) as T;

			if (typeof(T) == typeof(string) || typeCache.IsValue(typeof(T)))
				throw new InvalidOperationException(string.Format("Unable to convert structural data to {0}.",
					typeof(T).Name));

			return (T)ToObject(source, typeCache, typeof(T), dynamicObject);
		}

		public static object ToObject(this IDictionary<string, object> source, ITypeCache typeCache, Type type,
			bool dynamicObject = false)
		{
			if (typeCache == null)
			{
				throw new ArgumentNullException("typeCache");
			}

			if (source == null)
				return null;

			if (typeCache.IsTypeAssignableFrom(typeof(IDictionary<string, object>), type))
				return source;

			if (type == typeof(ODataEntry))
				return CreateODataEntry(source, typeCache, dynamicObject);

			// Check before custom converter so we use the most appropriate type.
			if (source.ContainsKey(FluentCommand.AnnotationsLiteral))
			{
				type = GetTypeFromAnnotation(source, typeCache, type);
			}

			if (typeCache.Converter.HasDictionaryConverter(type))
			{
				return typeCache.Converter.Convert(source, type);
			}

			if (type.HasCustomAttribute(typeof(CompilerGeneratedAttribute), false))
			{
				return CreateInstanceOfAnonymousType(source, type, typeCache);
			}

			var instance = CreateInstance(type);

			IDictionary<string, object> dynamicProperties = null;
			var dynamicPropertiesContainerName = typeCache.DynamicContainerName(type);
			if (!string.IsNullOrEmpty(dynamicPropertiesContainerName))
			{
				dynamicProperties =
					CreateDynamicPropertiesContainer(type, typeCache, instance, dynamicPropertiesContainerName);
			}

			foreach (var item in source)
			{
				var property = FindMatchingProperty(type, typeCache, item);

				if (property != null && property.CanWrite)
				{
					if (item.Value != null)
					{
						property.SetValue(instance, ConvertValue(property.PropertyType, typeCache, item.Value), null);
					}
				}
				else
				{
					if (dynamicProperties != null)
						dynamicProperties.Add(item.Key, item.Value);
				}
			}

			return instance;
		}

		private static Type GetTypeFromAnnotation(IDictionary<string, object> source, ITypeCache typeCache, Type type)
		{
			var annotations = source[FluentCommand.AnnotationsLiteral] as ODataEntryAnnotations;

			var odataType = annotations != null ? annotations.TypeName : null;

			if (string.IsNullOrEmpty(odataType))
			{
				return type;
			}

			// TODO: We could cast the ITypeCatcher to TypeCache and use it's property but it's a bit naughty - conditional?
			var resolver = ODataNameMatchResolver.NotStrict;

			if (!resolver.IsMatch(odataType, type.Name))
			{
				// Ok, something other than the base type, see if we can match it
				var derived = typeCache.GetDerivedTypes(type)
					.FirstOrDefault(x => resolver.IsMatch(odataType, typeCache.GetMappedName(x)));
				if (derived != null)
				{
					return derived;
				}

				foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
				{
					var typeFound = assembly.GetType(odataType);
					if (typeFound != null)
						return typeFound;
				}

				// TODO: Should we throw an exception here or log a warning here as we don't understand the data
			}

			return type;
		}

		private static PropertyInfo FindMatchingProperty(Type type, ITypeCache typeCache,
			KeyValuePair<string, object> item)
		{
			var property = typeCache.GetMappedProperty(type, item.Key);

			if (property == null && item.Key == FluentCommand.AnnotationsLiteral)
			{
				property = typeCache.GetAnnotationsProperty(type);
			}

			return property;
		}

		private static object ConvertValue(Type type, ITypeCache typeCache, object itemValue)
		{
			return IsCollectionType(type, typeCache, itemValue)
				? ConvertCollection(type, typeCache, itemValue)
				: ConvertSingle(type, typeCache, itemValue);
		}

		private static bool IsCollectionType(Type type, ITypeCache typeCache, object itemValue)
		{
			return
				(type.IsArray || typeCache.IsGeneric(type) &&
					typeCache.IsTypeAssignableFrom(typeof(System.Collections.IEnumerable), type)) &&
				(itemValue as System.Collections.IEnumerable) != null;
		}

		private static bool IsCompoundType(Type type, ITypeCache typeCache)
		{
			return !typeCache.IsValue(type) && !type.IsArray && type != typeof(string);
		}

		private static object ConvertEnum(Type type, ITypeCache typeCache, object itemValue)
		{
			if (itemValue == null)
				return null;

			var stringValue = itemValue.ToString();
			int intValue;
			if (int.TryParse(stringValue, out intValue))
			{
				object result;
				typeCache.TryConvert(intValue, type, out result);
				return result;
			}
			else
			{
				return Enum.Parse(type, stringValue, false);
			}
		}

		private static object ConvertSingle(Type type, ITypeCache typeCache, object itemValue)
		{
			return type == typeof(ODataEntryAnnotations)
				? itemValue
				: IsCompoundType(type, typeCache)
					? itemValue.ToDictionary(typeCache).ToObject(typeCache, type)
					: type.IsEnumType()
						? ConvertEnum(type, typeCache, itemValue)
						: TryConvert(itemValue, type, typeCache);
		}

		static object TryConvert(object v, Type t, ITypeCache typeCache)
		{
			object result;
			return typeCache.TryConvert(v, t, out result) ? result : v;
		}

		private static object ConvertCollection(Type type, ITypeCache typeCache, object itemValue)
		{
			var elementType = type.IsArray
				? type.GetElementType()
				: typeCache.IsGeneric(type) && typeCache.GetGenericTypeArguments(type).Length == 1
					? typeCache.GetGenericTypeArguments(type)[0]
					: null;

			if (elementType == null)
				return null;

			var count = GetCollectionCount(itemValue);
			var arrayValue = Array.CreateInstance(elementType, count);

			count = 0;
			foreach (var item in (itemValue as System.Collections.IEnumerable))
			{
				arrayValue.SetValue(ConvertSingle(elementType, typeCache, item), count++);
			}

			if (type.IsArray || typeCache.IsTypeAssignableFrom(type, arrayValue.GetType()))
			{
				return arrayValue;
			}
			else
			{
				var collectionTypes = new[] {
					typeof(IList<>).MakeGenericType(elementType),
					typeof(IEnumerable<>).MakeGenericType(elementType)
				};
				var collectionType = type.GetConstructor(new[] { collectionTypes[0] }) != null
					? collectionTypes[0]
					: collectionTypes[1];

				var activator = _collectionActivators.GetOrAdd(new Tuple<Type, Type>(type, collectionType),
					type.CreateActivator(collectionType));
				return activator != null ? activator.Invoke(arrayValue) : null;
			}
		}

		private static int GetCollectionCount(object collection)
		{
			if (collection is System.Collections.IList)
			{
				return ((System.Collections.IList)collection).Count;
			}
			else if (collection is System.Collections.IDictionary)
			{
				return ((System.Collections.IDictionary)collection).Count;
			}
			else
			{
				int count = 0;
				var e = ((System.Collections.IEnumerable)collection).GetEnumerator();
				using (e as IDisposable)
				{
					while (e.MoveNext()) count++;
				}

				return count;
			}
		}

		public static IDictionary<string, object> ToDictionary(this object source, ITypeCache typeCache)
		{
			if (source == null)
				return new Dictionary<string, object>();
			var objects = source as IDictionary<string, object>;
			if (objects != null)
				return objects;
			var entry = source as ODataEntry;
			if (entry != null)
				return (Dictionary<string, object>)entry;

			return typeCache.ToDictionary(source);
		}

		private static object CreateInstance(Type type)
		{
			if (type == typeof(IDictionary<string, object>))
			{
				return new Dictionary<string, object>();
			}
			else
			{
				var ctor = _defaultActivators.GetOrAdd(type, t => t.CreateActivator());
				return ctor.Invoke();
			}
		}

		private static ODataEntry CreateODataEntry(IDictionary<string, object> source, ITypeCache typeCache,
			bool dynamicObject = false)
		{
			return dynamicObject && CreateDynamicODataEntry != null
				? CreateDynamicODataEntry(source, typeCache)
				: new ODataEntry(source);
		}

		private static IDictionary<string, object> CreateDynamicPropertiesContainer(Type type, ITypeCache typeCache,
			object instance, string dynamicPropertiesContainerName)
		{
			var property = typeCache.GetNamedProperty(type, dynamicPropertiesContainerName);

			if (property == null)
				throw new ArgumentException(string.Format("Type {0} does not have property {1} ", type,
					dynamicPropertiesContainerName));

			if (!typeCache.IsTypeAssignableFrom(typeof(IDictionary<string, object>), property.PropertyType))
				throw new InvalidOperationException(string.Format(
					"Property {0} must implement IDictionary<string,object> interface",
					dynamicPropertiesContainerName));

			var dynamicProperties = new Dictionary<string, object>();
			property.SetValue(instance, dynamicProperties, null);
			return dynamicProperties;
		}

		private static object CreateInstanceOfAnonymousType(IDictionary<string, object> source, Type type,
			ITypeCache typeCache)
		{
			var constructor = FindConstructorOfAnonymousType(type, source);
			if (constructor == null)
			{
				throw new ConstructorNotFoundException(type, source.Values.Select(v => v.GetType()));
			}

			var parameterInfos = constructor.GetParameters();
			var constructorParameters = new object[parameterInfos.Length];
			for (var parameterIndex = 0; parameterIndex < parameterInfos.Length; parameterIndex++)
			{
				var parameterInfo = parameterInfos[parameterIndex];
				constructorParameters[parameterIndex] =
					ConvertValue(parameterInfo.ParameterType, typeCache, source[parameterInfo.Name]);
			}

			return constructor.Invoke(constructorParameters);
		}

		private static ConstructorInfo FindConstructorOfAnonymousType(Type type, IDictionary<string, object> source)
		{
			return type.GetDeclaredConstructors().FirstOrDefault(c =>
			{
				var parameters = c.GetParameters();
				return parameters.Length == source.Count &&
				       parameters.All(p => source.ContainsKey(p.Name));
			});
		}
	}
}