﻿using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Collections.Concurrent;

namespace Simple.OData.Client
{
	internal static class MemberAccessor
	{
		static readonly ConcurrentDictionary<CacheType, Delegate> getterCache =
			new ConcurrentDictionary<CacheType, Delegate>();

		static readonly ConcurrentDictionary<CacheType, Delegate> setterCache =
			new ConcurrentDictionary<CacheType, Delegate>();

		static readonly ConcurrentDictionary<CacheType, Delegate> staticGetterCache =
			new ConcurrentDictionary<CacheType, Delegate>();

		static readonly ConcurrentDictionary<CacheType, Delegate> staticSetterCache =
			new ConcurrentDictionary<CacheType, Delegate>();

		public static Delegate BuildGetterAccessor(Type type, Type returnType, MemberInfo memberInfo)
		{
			var parameter = Expression.Parameter(type);

			var castedParameter =
				type != memberInfo.DeclaringType
					? Expression.Convert(parameter, memberInfo.DeclaringType)
					: (Expression)parameter;

			var delegateType = Expression.GetDelegateType(new[] { typeof(object), returnType });
			var body = (Expression)Expression.MakeMemberAccess(castedParameter, memberInfo);

			if (body.Type != returnType)
				body = Expression.Convert(body, returnType);

			return Expression.Lambda(delegateType, body, parameter).Compile();
		}

		public static Delegate BuildStaticGetterAccessor(Type returnType, MemberInfo memberInfo)
		{
			var delegateType = Expression.GetDelegateType(new[] { returnType });
			var body = (Expression)Expression.MakeMemberAccess(null, memberInfo);

			if (body.Type != returnType)
				body = Expression.Convert(body, returnType);

			return Expression.Lambda(delegateType, body).Compile();
		}

		public static Delegate BuildSetterAccessor(Type type, Type valueType, MemberInfo memberInfo)
		{
			var parameter = Expression.Parameter(type);
			var valueParameter = Expression.Parameter(valueType);

			var castedParameter =
				type != memberInfo.DeclaringType
					? Expression.Convert(parameter, memberInfo.DeclaringType)
					: (Expression)parameter;

			var memberType = GetMemberType(memberInfo);
			var castedValueParameter =
				valueType != memberType ? Expression.Convert(valueParameter, memberType) : (Expression)valueParameter;

			var delegateType = Expression.GetDelegateType(new[] { typeof(object), valueType, typeof(void) });
			return Expression.Lambda(delegateType,
				Expression.Assign(
					Expression.MakeMemberAccess(castedParameter, memberInfo),
					castedValueParameter),
				parameter, valueParameter).Compile();
		}

		public static Delegate BuildStaticSetterAccessor(Type valueType, MemberInfo memberInfo)
		{
			var valueParameter = Expression.Parameter(valueType);

			var memberType = GetMemberType(memberInfo);
			var castedValueParameter =
				valueType != memberType ? Expression.Convert(valueParameter, memberType) : (Expression)valueParameter;

			var delegateType = Expression.GetDelegateType(new[] { valueType, typeof(void) });
			return Expression.Lambda(delegateType,
				Expression.Assign(
					Expression.MakeMemberAccess(null, memberInfo),
					castedValueParameter),
				valueParameter).Compile();
		}

		private static Type GetMemberType(MemberInfo memberInfo)
		{
			if (memberInfo is PropertyInfo)
				return ((PropertyInfo)memberInfo).PropertyType;
			if (memberInfo is FieldInfo)
				return ((FieldInfo)memberInfo).FieldType;
			return null;
		}

		private static bool CanGet(MemberInfo memberInfo)
		{
			if (memberInfo is PropertyInfo)
				return ((PropertyInfo)memberInfo).CanRead;
			if (memberInfo is FieldInfo)
				return true;
			return false;
		}

		private static bool CanSet(MemberInfo memberInfo)
		{
			if (memberInfo is PropertyInfo)
				return ((PropertyInfo)memberInfo).CanWrite;
			if (memberInfo is FieldInfo)
				return true;
			return false;
		}

		private static bool IsStatic(MemberInfo memberInfo)
		{
			if (memberInfo is PropertyInfo)
				return (((PropertyInfo)memberInfo).CanRead && ((PropertyInfo)memberInfo).GetMethod != null &&
				        ((PropertyInfo)memberInfo).GetMethod.IsStatic)
				       || (((PropertyInfo)memberInfo).CanWrite && ((PropertyInfo)memberInfo).SetMethod != null &&
				           ((PropertyInfo)memberInfo).SetMethod.IsStatic);
			else if (memberInfo is FieldInfo)
				return ((FieldInfo)memberInfo).IsStatic;
			else
				return false;
		}

		private static MemberInfo GetMemberInfo(Type type, string memberName)
		{
			MemberInfo memberInfo;
			if (TryGetMemberInfo(type, memberName, out memberInfo))
				return memberInfo;

			throw new InvalidOperationException(string.Format("Property or field {0} not found in type {1}", memberName,
				type.FullName));
		}

		private static void AssertMemberInfoType(MemberInfo memberInfo)
		{
			if (memberInfo.MemberType == MemberTypes.Field || memberInfo.MemberType == MemberTypes.Property)
				return;

			throw new InvalidOperationException(string.Format(
				"Member {0} is of member type {1}. Only property or field members can be access for value.",
				memberInfo.Name, memberInfo.MemberType));
		}

		private static bool TryGetMemberInfo(Type type, string memberName, out MemberInfo memberInfo)
		{
			var propertyInfo = type.GetProperty(memberName,
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			var fieldInfo = (propertyInfo == null)
				? type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				: null;

			memberInfo = (MemberInfo)propertyInfo ?? fieldInfo;

			return !(memberInfo == null);
		}

		private static Func<object, TMember> GetGetAccessor<TMember>(object instance, MemberInfo memberInfo)
		{
			AssertMemberInfoType(memberInfo);

			var isStatic = IsStatic(memberInfo);

			if (!isStatic && instance == null)
				throw new ArgumentNullException("instance", "Instance cannot be null to access a non static member.");

			if (isStatic)
			{
				return (object _) => ((Func<TMember>)staticGetterCache.GetOrAdd(
					new CacheType(null, typeof(TMember), memberInfo),
					key => BuildStaticGetterAccessor(typeof(TMember), memberInfo)))();
			}
			else
			{
				return (Func<object, TMember>)getterCache.GetOrAdd(new CacheType
						(instance.GetType(), typeof(TMember), memberInfo),
					key => BuildGetterAccessor(typeof(object), typeof(TMember), memberInfo));
			}
		}

		struct CacheType
		{
			Type Instance { get; set; }
			Type Member { get; set; }
			MemberInfo MemberInfo { get; set; }

			public CacheType(Type instance, Type member, MemberInfo memberInfo)
				: this()
			{
				Instance = instance;
				Member = member;
				MemberInfo = memberInfo;
			}
		}

		public static TMember GetValue<TMember>(object instance, MemberInfo memberInfo)
		{
			var accessor = GetGetAccessor<TMember>(instance, memberInfo);

			return accessor(instance);
		}

		public static TMember GetValue<TMember>(object instance, string memberName)
		{
			if (instance == null) throw new ArgumentNullException("instance");

			var type = instance.GetType();
			var memberInfo = GetMemberInfo(type, memberName);

			return GetValue<TMember>(instance, memberInfo);
		}

		public static object GetValue(object instance, MemberInfo memberInfo)
		{
			return GetValue<object>(instance, memberInfo);
		}

		public static object GetValue(object instance, string memberName)
		{
			return GetValue<object>(instance, memberName);
		}

		public static bool TryGetValue<TMember>(object instance, MemberInfo memberInfo, out TMember value)
		{
			value = default(TMember);

			if (instance == null) return false;

			if (!(CanGet(memberInfo)))
				return false;

			var accessor = GetGetAccessor<TMember>(instance, memberInfo);

			try
			{
				value = accessor(instance);
			}
			catch
			{
				return false;
			}

			return true;
		}

		public static bool TryGetValue<TMember>(object instance, string memberName, out TMember value)
		{
			value = default(TMember);

			if (instance == null) return false;

			var type = instance.GetType();
			var memberInfo = GetMemberInfo(type, memberName);

			return TryGetValue(instance, memberInfo, out value);
		}

		public static bool TryGetValue(object instance, MemberInfo memberInfo, out object value)
		{
			return TryGetValue<object>(instance, memberInfo, out value);
		}

		public static bool TryGetValue(object instance, string memberName, out object value)
		{
			return TryGetValue<object>(instance, memberName, out value);
		}

		private static Action<object, TMember> GetSetAccessor<TMember>(object instance, MemberInfo memberInfo)
		{
			AssertMemberInfoType(memberInfo);

			var isStatic = IsStatic(memberInfo);

			if (!isStatic && instance == null)
				throw new ArgumentNullException("instance", "Instance cannot be null to access a non static member.");

			if (isStatic)
			{
				return (object _, TMember x) => ((Action<TMember>)staticSetterCache.GetOrAdd(
					new CacheType(null, typeof(TMember), memberInfo),
					key => BuildStaticSetterAccessor(typeof(TMember), memberInfo)))(x);
			}
			else
			{
				return (Action<object, TMember>)setterCache.GetOrAdd(
					new CacheType(instance.GetType(), typeof(TMember), memberInfo),
					key => BuildSetterAccessor(typeof(object), typeof(TMember), memberInfo));
			}
		}

		public static void SetValue<TMember>(object instance, MemberInfo memberInfo, TMember value)
		{
			AssertMemberInfoType(memberInfo);

			var accessor = GetSetAccessor<TMember>(instance, memberInfo);

			accessor(instance, value);
		}

		public static void SetValue<TMember>(object instance, string memberName, TMember value)
		{
			if (instance == null) throw new ArgumentNullException("instance");

			var type = instance.GetType();
			var memberInfo = GetMemberInfo(type, memberName);

			SetValue(instance, memberInfo, value);
		}

		public static void SetValue(object instance, MemberInfo memberInfo, object value)
		{
			SetValue<object>(instance, memberInfo, value);
		}

		public static void SetValue(object instance, string memberName, object value)
		{
			SetValue<object>(instance, memberName, value);
		}

		public static bool TrySetValue<TMember>(object instance, MemberInfo memberInfo, TMember value)
		{
			if (instance == null) return false;

			if (!(CanSet(memberInfo)))
				return false;

			var accessor = GetSetAccessor<TMember>(instance, memberInfo);

			try
			{
				accessor(instance, value);
			}
			catch
			{
				return false;
			}

			return true;
		}

		public static bool TrySetValue<TMember>(object instance, string memberName, TMember value)
		{
			if (instance == null) return false;

			var type = instance.GetType();
			var memberInfo = GetMemberInfo(type, memberName);

			return TrySetValue(instance, memberInfo, value);
		}

		public static bool TrySetValue(object instance, MemberInfo memberInfo, object value)
		{
			return TrySetValue<object>(instance, memberInfo, value);
		}

		public static bool TrySetValue(object instance, string memberName, object value)
		{
			return TrySetValue<object>(instance, memberName, value);
		}
	}
}