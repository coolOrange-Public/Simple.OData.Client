using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Simple.OData.Client.Extensions
{
	static class TypeExtensions
	{
		public static IEnumerable<PropertyInfo> GetAllProperties(this Type type)
		{
			return type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
				.Where(x => x.DeclaringType != typeof(object));
		}

		public static PropertyInfo GetNamedProperty(this Type type, string propertyName)
		{
			var currentType = type;
			while (currentType != null && currentType != typeof(object))
			{
				var property = currentType.GetTypeInfo().GetDeclaredProperty(propertyName);
				if (property != null)
					return property;

				currentType = currentType.GetTypeInfo().BaseType;
			}
			return null;
		}

		public static PropertyInfo GetAnyProperty(this Type type, string propertyName)
		{
			var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
			return property == null || property.DeclaringType == typeof(object) ? null : property;
		}

		public static IEnumerable<PropertyInfo> GetDeclaredProperties(this Type type)
		{
			return type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
		}

		public static PropertyInfo GetDeclaredProperty(this Type type, string propertyName)
		{
			return type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
		}

		public static IEnumerable<FieldInfo> GetAllFields(this Type type)
		{
			return type.GetFields(BindingFlags.Instance | BindingFlags.Public);
		}

		public static FieldInfo GetAnyField(this Type type, string fieldName, bool includeNonPublic = false)
		{
			var bindingFlags = BindingFlags.Instance | BindingFlags.Public;
			if (includeNonPublic)
				bindingFlags |= BindingFlags.NonPublic;
			var field = type.GetField(fieldName, bindingFlags);
			return field == null || field.DeclaringType == typeof(object) ? null : field;
		}

		public static IEnumerable<FieldInfo> GetDeclaredFields(this Type type)
		{
			return type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly);
		}

		public static FieldInfo GetDeclaredField(this Type type, string fieldName, bool includeNonPublic = false)
		{
			var bindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly;
			if (includeNonPublic)
				bindingFlags |= BindingFlags.NonPublic;
			return type.GetField(fieldName, bindingFlags);
		}

		public static MethodInfo GetDeclaredMethod(this Type type, string methodName)
		{
			return type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		}

		public static IEnumerable<ConstructorInfo> GetDeclaredConstructors(this Type type)
		{
			return type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		}

		public static ConstructorInfo GetDefaultConstructor(this Type type)
		{
			return type.GetConstructor(new Type[] { });
		}

		public static TypeAttributes GetTypeAttributes(this Type type)
		{
			return type.Attributes;
		}

		public static Type[] GetGenericTypeArguments(this Type type)
		{
			return type.GetGenericArguments();
		}

		public static bool IsTypeAssignableFrom(this Type type, Type otherType)
		{
			return type.IsAssignableFrom(otherType);
		}

		public static bool HasCustomAttribute(this Type type, Type attributeType, bool inherit)
		{
			return Attribute.IsDefined(type, attributeType, inherit);
		}

		public static bool IsGeneric(this Type type)
		{
			return type.IsGenericType;
		}

		public static bool IsValue(this Type type)
		{
			return type.IsValueType;
		}

		public static bool IsEnumType(this Type type)
		{
			return type.IsEnum;
		}

		public static Type GetBaseType(this Type type)
		{
			return type.BaseType;
		}

		public static string GetMappedName(this Type type)
		{
			var supportedAttributeNames = new[]
			{
				"DataContractAttribute",
				"TableAttribute",
			};

			var mappingAttribute = type.GetCustomAttributes()
				.FirstOrDefault(x => supportedAttributeNames.Any(y => x.GetType().Name == y));

			if (mappingAttribute != null)
			{
				var nameProperty = mappingAttribute.GetType().GetNamedProperty("Name");
				if (nameProperty != null)
				{
					var propertyValue = nameProperty.GetValueEx(mappingAttribute);
					if (propertyValue != null)
						return propertyValue.ToString();
				}
			}

			return type.Name;
		}
	}
}