using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace SilkJson
{
    /// <summary>
    /// Provides functionality for serializing objects to JSON and deserializing JSON to objects.
    /// </summary>
    public static class JsonSerializer
    {
        /// <summary>
        /// Binding flags for looking up instance members (public and non-public).
        /// </summary>
        public const BindingFlags InstanceLookup = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        #region Public Methods

        /// <summary>
        /// Creates a Json instance from an object using reflection.
        /// </summary>
        /// <param name="target">The object to convert.</param>
        /// <param name="bindingFlags">Binding flags for member lookup.</param>
        /// <returns>A Json representation of the object.</returns>
        public static Json FromObject(object target, BindingFlags bindingFlags = InstanceLookup)
        {
            if (target is Json json) return json;
            if (target == null) return new JsonValue(target, JsonValueType.Null);
            if (target is string || target is bool || target is int || target is long || target is short || target is float || target is double) return new JsonValue(target);
            if (target is decimal) return new JsonValue((double)(decimal)target);
            if (target is DateTime time) return new JsonValue(time.ToString("s"));
            if (target.GetType().IsEnum) return new JsonValue(Enum.GetName(target.GetType(), target));
            if (target is IDictionary) return SerializeDictionary(target, bindingFlags);
            if (target is IEnumerable) return SerializeIEnumerable(target, bindingFlags);
            return SerializeObject(target, bindingFlags);
        }
        
        /// <summary>
        /// Deserializes a JSON string into an object of type T.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <param name="throwOnError">Whether to throw exception on error. If false, the method will return null on error.</param>
        /// <returns>The deserialized object, or default(T) if throwOnError is false and an error occurs.</returns>
        public static T ToObject<T>(string json, bool throwOnError = true)
        {
            try
            {
                object obj = JsonParser.ParseDirect(json);
                if (obj is IDictionary) return (T)DeserializeObject(typeof(T), obj as Dictionary<string, object>);
                if (obj is IList) return (T)DeserializeArray(typeof(T), obj as List<object>);
                return (T)DeserializeValue(typeof(T), obj);
            }
            catch (Exception e)
            {
                if (throwOnError) throw new Exception("Failed to deserialize JSON string.", e);
                Console.WriteLine(e.Message + "\n" + e.StackTrace);
                return default(T);
            }
        }
        
        /// <summary>
        /// Deserializes a JSON string into an existing object instance.
        /// </summary>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <param name="target">The object to populate.</param>
        /// <param name="bindingFlags">Binding flags for member lookup.</param>
        public static void ToObject(string json, object target)
        {
            if (target == null) return;

            var table = JsonParser.ParseDirect(json) as Dictionary<string, object>;
            Type type = target.GetType();

            IEnumerable<MemberInfo> members = type.GetMembers(BindingFlags.Instance | BindingFlags.Public);

            foreach (MemberInfo member in members)
            {
                MemberTypes memberType = member.MemberType;
                if (memberType != MemberTypes.Field && memberType != MemberTypes.Property) continue;
                if (memberType == MemberTypes.Property && !((PropertyInfo)member).CanWrite) continue;
                
                object item;

                object[] attributes = member.GetCustomAttributes(typeof(AliasAttribute), true);
                AliasAttribute alias = attributes.Length > 0 ? attributes[0] as AliasAttribute : null;
                if (alias == null || !alias.IgnoreFieldName)
                {
                    if (table.TryGetValue(member.Name, out item))
                    {
                        DeserializeValue(memberType, member, item, target);
                        continue;
                    }
                }

                if (alias == null) continue;
                
                for (int j = 0; j < alias.Aliases.Length; j++)
                {
                    if (table.TryGetValue(alias.Aliases[j], out item))
                    {
                        DeserializeValue(memberType, member, item, target);
                        break;
                    }
                }
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Deserializes a JSON array to the specified type.
        /// </summary>
        /// <param name="type">The target type.</param>
        /// <param name="list">The list of parsed values.</param>
        /// <returns>An instance of the target type.</returns>
        private static object DeserializeArray(Type type, List<object> list)
        {
            if (list == null || list.Count == 0) return null;
            if (type.IsArray)
            {
                Type elementType = type.GetElementType();
                Array v = Array.CreateInstance(elementType, list.Count);
                for (int i = 0; i < list.Count; i++)
                {
                    object child = list[i];
                    object item;
                    if (child is IDictionary) item = DeserializeObject(elementType, child as Dictionary<string, object>);
                    else if (child is IList) item = DeserializeArray(elementType, child as List<object>);
                    else item = DeserializeValue(elementType, child);
                    v.SetValue(item, i);
                }

                return v;
            }

            if (type.IsGenericType)
            {
                return DeserializeGeneric(type, list);
            }

            return null;
        }

        /// <summary>
        /// Deserializes a JSON array to a generic collection type.
        /// </summary>
        /// <param name="type">The target generic type.</param>
        /// <param name="list">The list of parsed values.</param>
        /// <returns>An instance of the target type.</returns>
        private static object DeserializeGeneric(Type type, List<object> list)
        {
            Type listType = type.GetGenericArguments()[0];
            object v = Activator.CreateInstance(type);

            for (int i = 0; i < list.Count; i++)
            {
                object child = list[i];
                object item;
                if (child is IDictionary) item = DeserializeObject(listType, child as Dictionary<string, object>);
                else if (child is IList) item = DeserializeArray(listType, child as List<object>);
                else item = DeserializeValue(listType, child);
                try
                {
                    MethodInfo methodInfo = type.GetMethod("Add");
                    if (methodInfo != null) methodInfo.Invoke(v, new[] { item });
                }
                catch
                {
                }
            }

            return v;
        }

        /// <summary>
        /// Deserializes a JSON object to the specified type.
        /// </summary>
        /// <param name="type">The target type.</param>
        /// <param name="table">The dictionary of parsed key-value pairs.</param>
        /// <returns>An instance of the target type.</returns>
        private static object DeserializeObject(Type type, Dictionary<string, object> table)
        {
            IEnumerable<MemberInfo> members = type.GetMembers(InstanceLookup);

            object v = Activator.CreateInstance(type);

            foreach (MemberInfo member in members)
            {
                MemberTypes memberType = member.MemberType;
                if (memberType != MemberTypes.Field && memberType != MemberTypes.Property) continue;
                if (memberType == MemberTypes.Property && !((PropertyInfo)member).CanWrite) continue;
                object item;

                object[] attributes = member.GetCustomAttributes(typeof(AliasAttribute), true);
                AliasAttribute alias = attributes.Length > 0 ? attributes[0] as AliasAttribute : null;

                if (alias == null || !alias.IgnoreFieldName)
                {
                    if (table.TryGetValue(member.Name, out item))
                    {
                        DeserializeValue(memberType, member, item, v);
                        continue;
                    }
                }

                if (alias != null)
                {
                    for (int j = 0; j < alias.Aliases.Length; j++)
                    {
                        if (table.TryGetValue(alias.Aliases[j], out item))
                        {
                            DeserializeValue(memberType, member, item, v);
                            break;
                        }
                    }
                }
            }

            return v;
        }

        /// <summary>
        /// Deserializes a raw value to the specified type.
        /// </summary>
        /// <param name="type">The target type.</param>
        /// <param name="obj">The value to convert.</param>
        /// <returns>The converted value.</returns>
        private static object DeserializeValue(Type type, object obj)
        {
            if (obj == null) return null;
            try
            {
                return Convert.ChangeType(obj, type);
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message + "\n" + exception.StackTrace);
            }

            return null;
        }

        /// <summary>
        /// Deserializes a value and assigns it to a field or property.
        /// </summary>
        /// <param name="memberType">The type of member (field or property).</param>
        /// <param name="member">The member info.</param>
        /// <param name="item">The value to assign.</param>
        /// <param name="v">The object to assign to.</param>
        private static void DeserializeValue(MemberTypes memberType, MemberInfo member, object item, object v)
        {
            object cv;
            Type t = memberType == MemberTypes.Field ? ((FieldInfo)member).FieldType : ((PropertyInfo)member).PropertyType;
            if (t == typeof(object)) cv = item;
            else if (t.IsEnum) cv = Enum.Parse(t, item as string);
            else if (item is IDictionary) cv = DeserializeObject(t, item as Dictionary<string, object>);
            else if (item is IList) cv = DeserializeArray(t, item as List<object>);
            else cv = DeserializeValue(t, item);

            if (memberType == MemberTypes.Field) ((FieldInfo)member).SetValue(v, cv);
            else ((PropertyInfo)member).SetValue(v, cv, null);
        }

        /// <summary>
        /// Serializes an IEnumerable to a JsonArray.
        /// </summary>
        /// <param name="target">The enumerable to serialize.</param>
        /// <param name="bindingFlags">Binding flags for member lookup.</param>
        /// <returns>A JsonArray representation.</returns>
        private static Json SerializeIEnumerable(object target, BindingFlags bindingFlags)
        {
            JsonArray array = new JsonArray();
            foreach (var item in (IEnumerable)target)
            {
                array.Add(FromObject(item, bindingFlags));
            }

            return array;
        }

        /// <summary>
        /// Serializes a dictionary to a JsonObject.
        /// </summary>
        /// <param name="target">The dictionary to serialize.</param>
        /// <param name="bindingFlags">Binding flags for member lookup.</param>
        /// <returns>A JsonObject representation.</returns>
        private static Json SerializeDictionary(object target, BindingFlags bindingFlags)
        {
            JsonObject dv = new JsonObject();
            IDictionaryEnumerator en = (target as IDictionary).GetEnumerator();
            while (en.MoveNext())
            {
                object k = en.Key;
                object v = en.Value;

                dv.Set(k as string, FromObject(v, bindingFlags));
            }

            return dv;
        }

        /// <summary>
        /// Serializes an object to a JsonObject.
        /// </summary>
        /// <param name="target">The object to serialize.</param>
        /// <param name="bindingFlags">Binding flags for member lookup.</param>
        /// <returns>A JsonObject representation.</returns>
        private static Json SerializeObject(object target, BindingFlags bindingFlags)
        {
            JsonObject o = new JsonObject();
            Type type = target.GetType();

            foreach (FieldInfo field in type.GetFields(bindingFlags))
            {
                string fieldName = field.Name;
                if (field.IsDefined(typeof(NonSerializedAttribute), true)) continue;
                if (field.Attributes == (FieldAttributes.Private | FieldAttributes.InitOnly))
                {
                    int startIndex = fieldName.IndexOf('<') + 1;
                    int endIndex = fieldName.IndexOf('>', startIndex);
                    if (endIndex != -1 && startIndex != -1) fieldName = fieldName.Substring(startIndex, endIndex - startIndex);
                    else fieldName = fieldName.Trim('<', '>');
                }

                if (fieldName[0] == '<')
                {
                    int endIndex = fieldName.IndexOf('>');
                    if (endIndex != -1) fieldName = fieldName.Substring(1, endIndex - 1);
                }

                o.Set(fieldName, FromObject(field.GetValue(target)));
            }

            return o;
        }

        #endregion
    }
}
