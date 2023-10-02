using System;
using System.Collections.Generic;
using System.Reflection;
using Elements.Core;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using MinAttribute = UnityEngine.Rendering.PostProcessing.MinAttribute;

namespace PhotonicFreedom;

public class ClassFieldHolder
{
    public FieldHolder[] Fields;
    public object? Instance;
    public PropertyHolder[] Properties;

    public ClassFieldHolder(Type type, object? instance = null)
    {
        componentType = type;
        Instance = instance;
        var fields = new List<FieldHolder>();
        foreach (var field in componentType.GetFields())
            if (Instance != null)
                fields.Add(new FieldHolder(field, Instance));
            else
                fields.Add(new FieldHolder(field));
        var properties = new List<PropertyHolder>();
        foreach (var property in componentType.GetProperties())
            if (Instance != null)
                properties.Add(new PropertyHolder(property, Instance));
            else
                properties.Add(new PropertyHolder(property));
        Properties = properties.ToArray();
        Fields = fields.ToArray();
    }

    public Type componentType { get; }

    public class FieldHolder
    {
        private object? _defaultValue;
        public object? Instance;

        public FieldHolder(FieldInfo fieldInfo)
        {
            FieldInfo = fieldInfo;
        }

        public FieldHolder(FieldInfo fieldInfo, object obj) : this(fieldInfo)
        {
            _defaultValue = fieldInfo.GetValue(obj);
            Instance = obj;
        }

        public FieldInfo FieldInfo { get; }
        public string Name => FieldInfo.Name;
        public string PrettifiedName => char.ToUpper(Name[0]) + Name.Substring(1);
        public Type ReflectedType => FieldInfo.ReflectedType;

        public Type BaseFieldType => FieldInfo.FieldType;

        public Type RealType => IsParameter ? BaseFieldType.BaseType.GetGenericArguments()[0] : FieldInfo.FieldType;

        public bool IsParameter => typeof(ParameterOverride).IsAssignableFrom(BaseFieldType);

        public object[] Attributes => FieldInfo.GetCustomAttributes(false);

        public bool HasRangeAttribute => HasAttribute<RangeAttribute>();

        public bool HasTooltipAttribute => HasAttribute<TooltipAttribute>();

        public bool HasMinAttribute => HasAttribute<MinAttribute>();

        public bool HasMaxAttribute => HasAttribute<MaxAttribute>();

        public float Min => HasRangeAttribute ? GetAttribute<RangeAttribute>().min :
            HasMinAttribute ? GetAttribute<MinAttribute>().min : 0f;

        public float Max => HasRangeAttribute ? GetAttribute<RangeAttribute>().max :
            HasMaxAttribute ? GetAttribute<MaxAttribute>().max : 100f;

        public string Tooltip => HasTooltipAttribute ? GetAttribute<TooltipAttribute>().tooltip : "";

        public object DefaultValue
        {
            get
            {
                var field = _defaultValue ?? FieldInfo.GetValue(Activator.CreateInstance(FieldInfo.ReflectedType));
                return IsParameter
                    ? field.GetType().GetField("value").GetValue(field)
                    : field ?? Activator.CreateInstance(RealType);
            }
        }

        public object FieldValue
        {
            get => Instance != null ? GetValue(Instance) : DefaultValue;
            set => SetRealValue(Instance ?? Activator.CreateInstance(FieldInfo.ReflectedType), value);
        }

        public bool HasAttribute<T>() where T : Attribute
        {
            return FieldInfo.GetCustomAttributes(typeof(T), false).Length > 0;
        }

        public T GetAttribute<T>() where T : Attribute
        {
            return (T)FieldInfo.GetCustomAttributes(typeof(T), false)[0];
        }

        public void SetDefaultValue(object value)
        {
            _defaultValue = value;
        }

        public object GetValue(object instance)
        {
            var field = FieldInfo.GetValue(instance);
            return IsParameter ? field.GetType().GetField("value").GetValue(field) : field;
        }

        public void SetRealValue(object instance, object value)
        {
            if (IsParameter)
            {
                var param = Activator.CreateInstance(BaseFieldType);
                param.GetType().GetField("value").SetValue(param, value);
                FieldInfo.SetValue(instance, param);
            }
            else
            {
                FieldInfo.SetValue(instance, value);
            }
        }
    }

    public class PropertyHolder
    {
        private object? _defaultValue;
        public object? Instance;

        public PropertyHolder(PropertyInfo propertyInfo)
        {
            PropertyInfo = propertyInfo;
        }

        public PropertyHolder(PropertyInfo propertyInfo, object obj) : this(propertyInfo)
        {
            _defaultValue = propertyInfo.GetValue(obj);
            Instance = obj;
        }

        public PropertyInfo PropertyInfo { get; }
        public string Name => PropertyInfo.Name;
        public string PrettifiedName => char.ToUpper(Name[0]) + Name.Substring(1);
        public Type ReflectedType => PropertyInfo.ReflectedType;

        public Type BasePropertyType => PropertyInfo.PropertyType;

        public object[] Attributes => PropertyInfo.GetCustomAttributes(false);

        public object DefaultValue
        {
            get
            {
                var field = _defaultValue ??
                            PropertyInfo.GetValue(Activator.CreateInstance(PropertyInfo.ReflectedType));
                return field ?? Activator.CreateInstance(BasePropertyType);
            }
        }

        public object PropertyValue
        {
            get => Instance != null ? GetValue(Instance) : DefaultValue;
            set => SetRealValue(Instance ?? Activator.CreateInstance(PropertyInfo.ReflectedType), value);
        }

        public bool HasAttribute<T>() where T : Attribute
        {
            return PropertyInfo.GetCustomAttributes(typeof(T), false).Length > 0;
        }

        public T GetAttribute<T>() where T : Attribute
        {
            return (T)PropertyInfo.GetCustomAttributes(typeof(T), false)[0];
        }

        public void SetDefaultValue(object value)
        {
            _defaultValue = value;
        }

        public object GetValue(object instance)
        {
            var field = PropertyInfo.GetValue(instance);
            return field;
        }

        public void SetRealValue(object instance, object value)
        {
            if (!PropertyInfo.CanWrite)
                return;

            PropertyInfo.SetValue(instance, value);
        }
    }
}

public class ClassValueSerializer
{
    public ClassValueSerializer(Type type, object o)
    {
        componentType = type.Name;
        foreach (var field in new ClassFieldHolder(type, o).Fields)
        {
            UniLog.Log(field.Name);
            ObjectValues[field.Name] = field.DefaultValue;
        }
    }

    public string componentType { get; private set; }

    public Dictionary<string, object> ObjectValues { get; } = new();
}

public struct FieldValuePair<T> : IFieldValuePair
{
    public string fieldName { get; }
    public T fieldValue { get; }
    [JsonIgnore] public object? BoxedValue => fieldValue;

    public FieldValuePair(string fieldName, T fieldValue)
    {
        this.fieldName = fieldName;
        this.fieldValue = fieldValue;
    }
}

public interface IFieldValuePair
{
    string fieldName { get; }
    object? BoxedValue { get; }
}

public struct ClassSerializer
{
    public string componentType;
    public List<IFieldValuePair> defaultFieldValues;

    public ClassSerializer(Type t)
    {
        componentType = t.FullName;
        defaultFieldValues = new List<IFieldValuePair>();
    }
}