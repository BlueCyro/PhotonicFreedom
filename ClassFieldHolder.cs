using HarmonyLib;
using NeosModLoader;
using FrooxEngine;
using FrooxEngine.UIX;
using BaseX;
using CodeX;
using System;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;
using MinAttribute = UnityEngine.Rendering.PostProcessing.MinAttribute;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;


namespace PhotonicFreedom;
public class ClassFieldHolder
{

    public Type componentType { get; private set; }
    public object? Instance;
    public ClassFieldHolder(Type type, object? instance = null)
    {
        componentType = type;
        Instance = instance;
        List<FieldHolder> fields = new List<FieldHolder>();
        foreach (FieldInfo field in componentType.GetFields())
        {
            if (Instance != null)
            {
                fields.Add(new FieldHolder(field, Instance));
            }
            else
            {
                fields.Add(new FieldHolder(field));
            }
        }
        List<PropertyHolder> properties = new List<PropertyHolder>();
        foreach (PropertyInfo property in componentType.GetProperties())
        {
            if (Instance != null)
            {
                properties.Add(new PropertyHolder(property, Instance));
            }
            else
            {
                properties.Add(new PropertyHolder(property));
            }
        }
        Properties = properties.ToArray();
        Fields = fields.ToArray();
    }
    public class FieldHolder
    {
        public FieldInfo FieldInfo { get; private set; }
        public FieldHolder(FieldInfo fieldInfo)
        {
            FieldInfo = fieldInfo;
        }
        public FieldHolder(FieldInfo fieldInfo, object obj) : this(fieldInfo)
        {
            _defaultValue = fieldInfo.GetValue(obj);
            Instance = obj;
        }
        public object? Instance;
        public string Name => FieldInfo.Name;
        public string PrettifiedName => Char.ToUpper(Name[0]) + Name.Substring(1);
        public Type ReflectedType => FieldInfo.ReflectedType;
        public Type BaseFieldType
        {
            get => FieldInfo.FieldType;
        }
        public Type RealType
        {
           get => IsParameter ? BaseFieldType.BaseType.GetGenericArguments()[0] : FieldInfo.FieldType;
        }
        public bool IsParameter
        {
            get => typeof(ParameterOverride).IsAssignableFrom(BaseFieldType);
        }
        public object[] Attributes
        {
            get => FieldInfo.GetCustomAttributes(false);
        }
        public bool HasAttribute<T>() where T : Attribute
        {
            return FieldInfo.GetCustomAttributes(typeof(T), false).Length > 0;
        }
        public T GetAttribute<T>() where T : Attribute
        {
            return (T)FieldInfo.GetCustomAttributes(typeof(T), false)[0];
        }
        public bool HasRangeAttribute
        {
            get => HasAttribute<UnityEngine.RangeAttribute>();
        }
        public bool HasTooltipAttribute
        {
            get => HasAttribute<TooltipAttribute>();
        }
        public bool HasMinAttribute
        {
            get => HasAttribute<MinAttribute>();
        }
        public bool HasMaxAttribute
        {
            get => HasAttribute<MaxAttribute>();
        }
        public float Min
        {
            get => HasRangeAttribute ? GetAttribute<UnityEngine.RangeAttribute>().min : HasMinAttribute ? GetAttribute<MinAttribute>().min : 0f;
        }
        public float Max
        {
            get => HasRangeAttribute ? GetAttribute<UnityEngine.RangeAttribute>().max : HasMaxAttribute ? GetAttribute<MaxAttribute>().max : 100f;
        }
        public string Tooltip
        {
            get => HasTooltipAttribute ? GetAttribute<TooltipAttribute>().tooltip : "";
        }
        public object DefaultValue
        {
            get
            {
                object field = _defaultValue ?? FieldInfo.GetValue(Activator.CreateInstance(FieldInfo.ReflectedType));
                return IsParameter ? field.GetType().GetField("value").GetValue(field) : field ?? Activator.CreateInstance(RealType);
            }
        }
        public void SetDefaultValue(object value)
        {
            _defaultValue = value;
        }
        public object GetValue(object instance)
        {
            object field = FieldInfo.GetValue(instance);
            return IsParameter ? field.GetType().GetField("value").GetValue(field) : field;
        }
        public void SetRealValue(object instance, object value)
        {
            if (IsParameter)
            {
                object param = Activator.CreateInstance(BaseFieldType);
                param.GetType().GetField("value").SetValue(param, value);
                FieldInfo.SetValue(instance, param);
            }
            else
            {
                FieldInfo.SetValue(instance, value);
            }
        }
        public object FieldValue
        {
            get => Instance != null ? GetValue(Instance) : DefaultValue;
            set => SetRealValue(Instance ?? Activator.CreateInstance(FieldInfo.ReflectedType), value);
        }
        private object? _defaultValue;
    }

    public class PropertyHolder
    {
        public PropertyInfo PropertyInfo { get; private set; }
        public PropertyHolder(PropertyInfo propertyInfo)
        {
            PropertyInfo = propertyInfo;
        }
        public PropertyHolder(PropertyInfo propertyInfo, object obj) : this(propertyInfo)
        {
            _defaultValue = propertyInfo.GetValue(obj);
            Instance = obj;
        }
        public object? Instance;
        public string Name => PropertyInfo.Name;
        public string PrettifiedName => Char.ToUpper(Name[0]) + Name.Substring(1);
        public Type ReflectedType => PropertyInfo.ReflectedType;
        public Type BasePropertyType
        {
            get => PropertyInfo.PropertyType;
        }
        public object[] Attributes
        {
            get => PropertyInfo.GetCustomAttributes(false);
        }
        public bool HasAttribute<T>() where T : Attribute
        {
            return PropertyInfo.GetCustomAttributes(typeof(T), false).Length > 0;
        }
        public T GetAttribute<T>() where T : Attribute
        {
            return (T)PropertyInfo.GetCustomAttributes(typeof(T), false)[0];
        }
        public object DefaultValue
        {
            get
            {
                object field = _defaultValue ?? PropertyInfo.GetValue(Activator.CreateInstance(PropertyInfo.ReflectedType));
                return field ?? Activator.CreateInstance(BasePropertyType);
            }
        }
        public void SetDefaultValue(object value)
        {
            _defaultValue = value;
        }
        public object GetValue(object instance)
        {
            object field = PropertyInfo.GetValue(instance);
            return field;
        }
        public void SetRealValue(object instance, object value)
        {
            if (!PropertyInfo.CanWrite)
                return;
            
            PropertyInfo.SetValue(instance, value);
        }
        public object PropertyValue
        {
            get => Instance != null ? GetValue(Instance) : DefaultValue;
            set => SetRealValue(Instance ?? Activator.CreateInstance(PropertyInfo.ReflectedType), value);
        }
        private object? _defaultValue;


    }
    public FieldHolder[] Fields;
    public PropertyHolder[] Properties;
}

public class ClassValueSerializer
{
    public string componentType { get; private set; }
    private Dictionary<string, object> defaultFieldValues = new Dictionary<string, object>();
    public Dictionary<string, object> ObjectValues
    {
        get => defaultFieldValues;
    }
    public ClassValueSerializer(Type type, object o)
    {
        componentType = type.Name;
        foreach (ClassFieldHolder.FieldHolder field in new ClassFieldHolder(type, o).Fields)
        {
            UniLog.Log(field.Name);
            defaultFieldValues[field.Name] = field.DefaultValue;
        }
    }
}
public struct FieldValuePair<T> : IFieldValuePair
{
    public string fieldName { get; private set; }
    public T fieldValue { get; }
    [JsonIgnore]
    public object? BoxedValue => fieldValue;
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
