using HarmonyLib;
using NeosModLoader;
using FrooxEngine;
using FrooxEngine.UIX;
using BaseX;
using CodeX;
using System;
using System.Reflection;
using System.Reflection.Emit;
using Newtonsoft.Json;
using System.IO;
using UnityEngine;
using MinAttribute = UnityEngine.Rendering.PostProcessing.MinAttribute;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

namespace PhotonicFreedom;

public class PhotonicFreedom : NeosMod
{
    public override string Author => "Cyro";
    public override string Name => "PhotonicFreedom";
    public override string Version => "2.0.0";
    private static List<Type> TypeList = new List<Type>() { typeof(AmplifyOcclusionBase) };
    public static List<Type> Types
    {
        get => TypeList;
    }
    public static Dictionary<Type, ClassFieldHolder> FieldHolders = new Dictionary<Type, ClassFieldHolder>();
    public static Dictionary<Type, Dictionary<string, IField>> SettingFields = new Dictionary<Type, Dictionary<string, IField>>();
    private static JsonSerializerSettings? serializerSettings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include, ReferenceLoopHandling = ReferenceLoopHandling.Ignore, TypeNameHandling = TypeNameHandling.Auto };
    public static void Bootstrap()
    {
        UnityEngine.Camera mainCam = UnityEngine.Camera.main;
        PostProcessLayer layer = mainCam.GetComponent<PostProcessLayer>();
        if(layer == null)
        {
            Debug("PhotonicFreedom: No post process layer found");
            return;
        }
        Dictionary<Type, PostProcessBundle>? settingsBundles = layer.GetType().GetField("m_Bundles", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(layer) as Dictionary<Type, PostProcessBundle>;
        if (settingsBundles == null)
        {
            Debug("PhotonicFreedom: No settings bundles");
            return;
        }
        foreach (KeyValuePair<Type, PostProcessBundle> kvp in settingsBundles)
        {
            if (!Types.Contains(kvp.Key))
            {
                Types.Add(kvp.Key);
            }
        }
        Types.ForEach(type => {
            Debug($"PhotonicFreedom: Found -> {type.Name}\n");
            object comp = type.InheritsFrom(typeof(PostProcessEffectSettings)) ? layer.GetBundle(type).settings : mainCam.GetComponent(type) ?? Activator.CreateInstance(type);
            WriteDefaultSettings(type, comp);
            ReadAllSettings(type, comp);
        });
    }
    public static void WriteDefaultSettings(Type type, object comp)
    {
        ClassFieldHolder c = new ClassFieldHolder(type, comp);
        ClassSerializer serializer = new ClassSerializer(type);
        FieldHolders[type] = c;
        foreach (var field in c.Fields)
        {
            Debug($"PhotonicFreedom: Found field -> {field.Name} with value {field.DefaultValue}\n");
            if (SupportedTypes.Contains(field.RealType) || field.RealType.IsEnum)
            {
                Type pairType = typeof(FieldValuePair<>).MakeGenericType(field.RealType);
                serializer.defaultFieldValues.Add((IFieldValuePair)Activator.CreateInstance(pairType, new object[] { field.Name, field.DefaultValue }));
            }
        }
        foreach (var property in c.Properties)
        {
            Debug($"PhotonicFreedom: Found property -> {property.Name} with value {property.DefaultValue}\n");
            if (SupportedTypes.Contains(property.BasePropertyType) || property.BasePropertyType.IsEnum)
            {
                Type pairType = typeof(FieldValuePair<>).MakeGenericType(property.BasePropertyType);
                serializer.defaultFieldValues.Add((IFieldValuePair)Activator.CreateInstance(pairType, new object[] { property.Name, property.DefaultValue }));
            }
        }
        if (!Directory.Exists("nml_mods/Photonic_Settings"))
        {
            Directory.CreateDirectory("nml_mods/Photonic_Settings");
        }
        if (!File.Exists($"nml_mods/Photonic_Settings/{type.Name}.json"))
        {
            Debug("PhotonicFreedom: Writing missing settings file for " + type.Name);
            File.WriteAllText($"nml_mods/Photonic_Settings/{type.Name}.json", JsonConvert.SerializeObject(serializer, Formatting.Indented, serializerSettings));
        }
    }
    public static void WriteCurrentSettings(Type type, object comp)
    {
        ClassFieldHolder c = new ClassFieldHolder(type, comp);
        ClassSerializer serializer = new ClassSerializer(type);
        FieldHolders[type] = c;
        foreach (var field in c.Fields)
        {
            //Debug($"PhotonicFreedom: Found field -> {field.Name} with value {field.DefaultValue}\n")
            if (SupportedTypes.Contains(field.RealType) || field.RealType.IsEnum)
            {
                Type pairType = typeof(FieldValuePair<>).MakeGenericType(field.RealType);
                serializer.defaultFieldValues.Add((IFieldValuePair)Activator.CreateInstance(pairType, new object[] { field.Name, field.GetValue(comp) }));
            }
        }
        foreach (var property in c.Properties)
        {
            //Debug($"PhotonicFreedom: Found property -> {property.Name} with value {property.GetValue(comp)}\n")
            if (SupportedTypes.Contains(property.BasePropertyType) || property.BasePropertyType.IsEnum)
            {
                Type pairType = typeof(FieldValuePair<>).MakeGenericType(property.BasePropertyType);
                serializer.defaultFieldValues.Add((IFieldValuePair)Activator.CreateInstance(pairType, new object[] { property.Name, property.GetValue(comp) }));
            }
        }
        if (!Directory.Exists("nml_mods/Photonic_Settings"))
        {
            Directory.CreateDirectory("nml_mods/Photonic_Settings");
        }
        if (!File.Exists($"nml_mods/Photonic_Settings/{type.Name}.json"))
        {
            Debug("PhotonicFreedom: Writing missing settings file for " + type.Name);
        }
        File.WriteAllText($"nml_mods/Photonic_Settings/{type.Name}.json", JsonConvert.SerializeObject(serializer, Formatting.Indented, serializerSettings));
    }
    public static void ReadAllSettings(Type type, object comp)
    {
        ClassSerializer loaded = JsonConvert.DeserializeObject<ClassSerializer>(File.ReadAllText($"nml_mods/Photonic_Settings/{type.Name}.json"), serializerSettings);
        Debug("PhotonicFreedom: Loaded settings has " + loaded.defaultFieldValues + "\n");
        foreach (var field in FieldHolders[type].Fields)
        {
            IFieldValuePair pair = loaded.defaultFieldValues.FirstOrDefault(pair => pair.fieldName == field.Name);
            if (pair != null && pair.BoxedValue != null)
            {
                Debug($"PhotonicFreedom: Found field -> {field.Name} with value {pair.BoxedValue} and a type of {pair.BoxedValue.GetType()}\n");
                field.SetRealValue(comp, pair.BoxedValue);
            }
        }
        foreach (var property in FieldHolders[type].Properties)
        {
            IFieldValuePair pair = loaded.defaultFieldValues.FirstOrDefault(pair => pair.fieldName == property.Name);
            if (pair != null && pair.BoxedValue != null)
            {
                Debug($"PhotonicFreedom: Found property -> {property.Name} with value {pair.BoxedValue} and a type of {pair.BoxedValue.GetType()}\n");
                Debug("PhotonicFreedom: Setting property " + property.Name + " to " + pair.BoxedValue + "\n");
                property.SetRealValue(comp, pair.BoxedValue);
            }
        }
    }
    public override void OnEngineInit()
    {
        //Engine.Current.RunPostInit(Bootstrap);
        Harmony harmony = new Harmony("net.Cyro.PhotonicFreedom");
        harmony.PatchAll();
    }
    public static Type[] SupportedTypes
    {
        get => new Type[] { typeof(int), typeof(float), typeof(bool), typeof(Color) };
    }
    static void OnPhotonicSettingChanged(IChangeable c, ClassFieldHolder.FieldHolder field, object comp)
    {
        var realValue = c.GetType().GetProperty("Value").GetValue(c);
        field.SetRealValue(comp, realValue);
    }
    static void OnPhotonicSettingChanged_Color(IChangeable c, ClassFieldHolder.FieldHolder field, object comp)
    {
        var realValue = c.GetType().GetProperty("Value").GetValue(c);
        field.SetRealValue(comp, UnityNeos.Conversions.ToUnity((color)realValue));
    }
    static void OnPhotonicSettingChanged(IChangeable c, ClassFieldHolder.PropertyHolder property, object comp)
    {
        property.SetRealValue(comp, c.GetType().GetProperty("Value").GetValue(c));
    }
    public static void TestForUIBuilder(UIBuilder builder)
    {
        UnityEngine.Camera mainCam = UnityEngine.Camera.main;
        PostProcessLayer layer = mainCam.GetComponent<PostProcessLayer>();
        MethodInfo AttachMethod = typeof(Slot).GetMethods().Single(m => m.Name == "AttachComponent" && m.IsGenericMethodDefinition);
        
        builder.Text("<b>PhotonicFreedom Settings</b>", true, null, true, null);
        Button ResetButton = builder.Button("Reset all settings");
        ResetButton.LocalPressed += (IButton b, ButtonEventData d) => {
            foreach (KeyValuePair<Type, Dictionary<string, IField>> kvp in SettingFields)
            {
                foreach (KeyValuePair<string, IField> kvp2 in kvp.Value)
                {
                    object? value = null;
                    if (FieldHolders[kvp.Key].Fields.Any(f => f.Name == kvp2.Key))
                    {
                        value = FieldHolders[kvp.Key].Fields.First(f => f.Name == kvp2.Key).DefaultValue;
                    }
                    else if (FieldHolders[kvp.Key].Properties.Any(p => p.Name == kvp2.Key))
                    {
                        value = FieldHolders[kvp.Key].Properties.First(p => p.Name == kvp2.Key).DefaultValue;
                    }
                    if (value == null)
                    {
                        Debug("PhotonicFreedom: Could not find default value for " + kvp2.Key);
                        continue;
                    }
                    if (value.GetType() == typeof(Color))
                    {
                        value = UnityNeos.Conversions.ToNeos((Color)value);
                    }
                    kvp2.Value.BoxedValue = value;
                }
            }
        };
        
        builder.Text("---------------------------------------------", true, null, true, null);
        
        foreach (var type in Types)
        {
            ClassFieldHolder holder = FieldHolders[type];
            object comp = type.InheritsFrom(typeof(PostProcessEffectSettings)) ? layer.GetBundle(type).settings : mainCam.GetComponent(type) ?? Activator.CreateInstance(type);

            builder.Text("<b>" + type.Name + "</b>", true, null, true, null);

            foreach (var property in holder.Properties)
            {
                if (property.BasePropertyType == typeof(bool) && property.Name == "enabled")
                {
                    builder.Panel();
                    var list = builder.SplitHorizontally(new float[] { 0.42f, 0.58f });
                    builder.NestInto(list[1]);
                    var checkbox = builder.Checkbox(property.PrettifiedName, (bool)property.GetValue(comp!), false);
                    builder.NestOut();
                    builder.NestOut();
                    checkbox.State.Changed += (IChangeable c) => OnPhotonicSettingChanged(c, property, comp!);
                    if (SettingFields.ContainsKey(type))
                    {
                        SettingFields[type].Add(property.Name, checkbox.State);
                    }
                    else
                    {
                        SettingFields.Add(type, new Dictionary<string, IField>() { { property.Name, checkbox.State } });
                    }
                }
            }
            foreach (var field in holder.Fields)
            {
                if (field.RealType == typeof(bool) && field.Name == "enabled")
                {
                    builder.Panel();
                    var list = builder.SplitHorizontally(new float[] { 0.42f, 0.58f });
                    builder.NestInto(list[1]);
                    var checkbox = builder.Checkbox(field.PrettifiedName, (bool)field.GetValue(comp!), false);
                    builder.NestOut();
                    builder.NestOut();
                    checkbox.State.Changed += (IChangeable c) => OnPhotonicSettingChanged(c, field, comp!);
                    if (SettingFields.ContainsKey(type))
                    {
                        SettingFields[type].Add(field.Name, checkbox.State);
                    }
                    else
                    {
                        SettingFields.Add(type, new Dictionary<string, IField>() { { field.Name, checkbox.State } });
                    }
                }
            }
            builder.Text("---------------------------------------------", true, null, true, null);
            Button IndividualComponentResetButton = builder.Button("Reset");
            IndividualComponentResetButton.LocalPressed += (IButton b, ButtonEventData d) => {
                foreach (KeyValuePair<string, IField> kvp in SettingFields[type])
                {
                    object? value = null;
                    if (FieldHolders[type].Fields.Any(f => f.Name == kvp.Key))
                    {
                        value = FieldHolders[type].Fields.First(f => f.Name == kvp.Key).DefaultValue;
                        Debug("PhotonicFreedom: Setting field " + kvp.Key + " to " + value + "\n");
                    }
                    else if (FieldHolders[type].Properties.Any(p => p.Name == kvp.Key))
                    {
                        value = FieldHolders[type].Properties.First(p => p.Name == kvp.Key).DefaultValue;
                        Debug("PhotonicFreedom: Setting property " + kvp.Key + " to " + value + "\n");
                    }
                    if (value == null)
                    {
                        Debug("PhotonicFreedom: Could not find default value for " + kvp.Key + " in " + type.Name);
                        continue;
                    }
                    if (value.GetType() == typeof(Color))
                    {
                        value = UnityNeos.Conversions.ToNeos((Color)value);
                    }
                    kvp.Value.BoxedValue = value;
                }
            };
            builder.Text("---------------------------------------------", true, null, true, null);
            foreach (var field in holder.Fields)
            {
                if (field.RealType == typeof(int))
                {
                    var parser = builder.HorizontalElementWithLabel<IntTextEditorParser>(field.PrettifiedName, 0.7f, () => builder.IntegerField((int)field.Min, (int)field.Max, 1));
                    parser.ParsedValue.Value = (int)field.GetValue(comp!);
                    parser.ParsedValue.Changed += (IChangeable c) => OnPhotonicSettingChanged(c, field, comp!);
                    var slider = builder.Slider<int>(builder.Style.MinHeight, 0, (int)field.Min, (int)field.Max, true);
                    slider.Value.DriveFrom(parser.ParsedValue, true);
                    if (SettingFields.ContainsKey(type))
                    {
                        SettingFields[type].Add(field.Name, parser.ParsedValue);
                    }
                    else
                    {
                        SettingFields.Add(type, new Dictionary<string, IField>() { { field.Name, parser.ParsedValue } });
                    }
                }
                if (field.RealType == typeof(float))
                {
                    var parser = builder.HorizontalElementWithLabel<FloatTextEditorParser>(field.PrettifiedName, 0.7f, () => builder.FloatField(field.Min, field.Max));
                    parser.ParsedValue.Value = (float)field.GetValue(comp!);
                    parser.ParsedValue.Changed += (IChangeable c) => OnPhotonicSettingChanged(c, field, comp!);
                    var slider = builder.Slider<float>(builder.Style.MinHeight, 0, field.Min, field.Max, false);
                    slider.Value.DriveFrom(parser.ParsedValue, true);
                    if (SettingFields.ContainsKey(type))
                    {
                        SettingFields[type].Add(field.Name, parser.ParsedValue);
                    }
                    else
                    {
                        SettingFields.Add(type, new Dictionary<string, IField>() { { field.Name, parser.ParsedValue } });
                    }
                }
                if (field.RealType == typeof(bool) && field.Name != "active" && field.Name != "enabled")
                {
                    var checkbox = builder.Checkbox(field.PrettifiedName, (bool)field.GetValue(comp!));
                    checkbox.State.Changed += (IChangeable c) => OnPhotonicSettingChanged(c, field, comp!);
                    if (SettingFields.ContainsKey(type))
                    {
                        SettingFields[type].Add(field.Name, checkbox.State);
                    }
                    else
                    {
                        SettingFields.Add(type, new Dictionary<string, IField>() { { field.Name, checkbox.State } });
                    }
                }
                if (field.RealType.IsEnum)
                {
                    var valuefield = typeof(ValueField<>).MakeGenericType(field.RealType);
                    object? val = AttachMethod.MakeGenericMethod(valuefield).Invoke(builder.Root, new object?[] { true, null });
                    IField? valField = valuefield.GetField("Value").GetValue(val) as IField;
                    if (val == null || valField == null)
                    {
                        Debug("Could not attach value field");
                        return;
                    }
                    EnumMemberEditor enumParser = builder.HorizontalElementWithLabel<EnumMemberEditor>(field.PrettifiedName, 0.7f, () => builder.EnumMemberEditor(valField));
                    valField.BoxedValue = field.GetValue(comp!);
                    valField.Changed += (IChangeable c) => OnPhotonicSettingChanged(c, field, comp!);
                    if (SettingFields.ContainsKey(type))
                    {
                        SettingFields[type].Add(field.Name, valField);
                    }
                    else
                    {
                        SettingFields.Add(type, new Dictionary<string, IField>() { { field.Name, valField } });
                    }
                }
                if (field.RealType == typeof(UnityEngine.Color))
                {
                    ValueField<color> val = builder.Root.AttachComponent<ValueField<color>>();
                    val.Value.Value = UnityNeos.Conversions.ToNeos((UnityEngine.Color)field.GetValue(comp!));
                    var parser = builder.HorizontalElementWithLabel<FrooxEngine.Component>(field.PrettifiedName, 0.5f, () => {
                        SyncMemberEditorBuilder.Build(val.Value, "", val.GetSyncMemberFieldInfo(val.IndexOfMember(val.GetSyncMember("Value"))), builder);
                        return val;
                    });
                    val.Value.Changed += (IChangeable c) => OnPhotonicSettingChanged_Color(c, field, comp!);
                    if (SettingFields.ContainsKey(type))
                    {
                        SettingFields[type].Add(field.Name, val.Value);
                    }
                    else
                    {
                        SettingFields.Add(type, new Dictionary<string, IField>() { { field.Name, val.Value } });
                    }
                }
                if (field.Tooltip != null && field.Tooltip.Length > 0 && SupportedTypes.Contains(field.RealType))
                {
                    builder.Text("<size=25%>" + field.Tooltip + "</size>", false, Alignment.TopLeft, true, null).Slot.GetComponent<LayoutElement>().Priority.Value = 0;
                }
            }
        }
    }

    [HarmonyPatch]
    public static class SettingsDialog_GetUIBuilder
    {

        private static UIBuilder? _builder { get; set; }

        [HarmonyPatch(typeof(SettingsDialog), "OnAttach")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> OnAttach_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            int UIBuilderIndex = codes.FindLastIndex(x => x.opcode == OpCodes.Ldfld && ((FieldInfo)x.operand).FieldType == typeof(UIBuilder));
            Debug("PhotonicFreedom: UIBuilder index: " + UIBuilderIndex);
            
            // This might as well be a manual postfix, but it feels better than jankily getting the components in a hierarchy and duplicating the UI style myself
            codes.InsertRange(codes.Count - 2, new List<CodeInstruction>
            {
                new CodeInstruction(OpCodes.Ldloc_0),
                new CodeInstruction(OpCodes.Ldfld, codes[UIBuilderIndex].operand),
                new CodeInstruction(OpCodes.Call, typeof(PhotonicFreedom).GetMethod("TestForUIBuilder", BindingFlags.Static | BindingFlags.Public))
            });
            return codes.AsEnumerable();
        }
    }

    [HarmonyPatch]
    public static class Userspace_Patch
    {
        [HarmonyPatch(typeof(Userspace), "SaveAllSettings")]
        [HarmonyPrefix]
        public static void SaveAllSettings_Prefix(Userspace __instance)
        {
            UnityEngine.Camera mainCam = UnityEngine.Camera.main;
            if (mainCam == null)
            {
                return;
            }
            PostProcessLayer layer = mainCam.GetComponent<PostProcessLayer>();
            if (layer == null)
            {
                return;
            }

            Types.ForEach(type => {
                Debug($"PhotonicFreedom: Found -> {type.Name}\n");
                object comp = type.InheritsFrom(typeof(PostProcessEffectSettings)) ? layer.GetBundle(type).settings : mainCam.GetComponent(type) ?? Activator.CreateInstance(type);
                WriteCurrentSettings(type, comp);
            });
        }
        [HarmonyPatch(typeof(Userspace), "OnAttach")]
        [HarmonyPostfix]
        public static void OnAttach_Postfix(Userspace __instance)
        {
            Bootstrap();
        }
    }
}
