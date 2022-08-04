using System;
using System.Collections.Generic;
using System.Linq;

namespace Simple.OData.Client.Extensions
{
    internal static class TypeCacheExtensions
    {
        public static IDictionary<string, object> ToDictionary(this ITypeCache typeCache, object value)
        {
            var mpn = typeCache.GetMappedPropertiesWithNames(value.GetType());

            return mpn.Select(x => new KeyValuePair<string, object>(x.Item2, x.Item1.GetValueEx(value)))
                      .ToIDictionary();
        }

        public static T Convert<T>(this ITypeCache typeCache, object value)
        {
            return (T) typeCache.Convert(value, typeof(T));
        }

        public static object Convert(this ITypeCache typeCache, object value, Type targetType)
        {
            if (value == null && !typeCache.IsValue(targetType))
                return null;
            object result;
            if (typeCache.TryConvert(value, targetType, out result))
	            return result;

            throw new FormatException(string.Format("Unable to convert value from type {0} to type {1}", value.GetType(), targetType));
        }
    }
}
