using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.UIX;
using HarmonyLib;
using Newtonsoft.Json;
using ResoniteModLoader;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityFrooxEngineRunner;
using Camera = UnityEngine.Camera;
using Component = FrooxEngine.Component;

namespace PhotonicFreedom;

public class PhotonicFreedom : ResoniteMod
{
    private static readonly Dictionary<Type, ClassFieldHolder> FieldHolders = new();
    private static readonly Dictionary<Type, Dictionary<string, IField>> SettingFields = new();

    private static readonly JsonSerializerSettings? serializerSettings = new()
    {
        NullValueHandling = NullValueHandling.Include, ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        TypeNameHandling = TypeNameHandling.Auto
    };

    public override string Author => "Cyro, TheJebForge";
    public override string Name => "PhotonicFreedom";
    public override string Version => "2.2.1";
    private static string DynvarKey => "World/PhotonicFreedomPostProcessOption";

    private static List<Type> Types { get; } = new() { typeof(AmplifyOcclusionBase) };

    private static IEnumerable<Type> SupportedTypes => new[] { typeof(int), typeof(float), typeof(bool), typeof(Color) };

    private static void Bootstrap()
    {
        if (Userspace.UserspaceWorld.RootSlot.FindChild("PhotonicFreedom") is { } userspacePhotonicFreedom) userspacePhotonicFreedom.Destroy();
        Userspace.UserspaceWorld.RootSlot.AddSlot("PhotonicFreedom", false);
        var mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug("PhotonicFreedom: No main camera");
            return;
        }
        var layer = mainCam.GetComponent<PostProcessLayer>();
        if (layer == null)
        {
            Debug("PhotonicFreedom: No post process layer found");
            return;
        }

        if (layer.GetType().GetField("m_Bundles", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(layer) 
            is not Dictionary<Type, PostProcessBundle> settingsBundles)
        {
            Debug("PhotonicFreedom: No settings bundles");
            return;
        }

        foreach (var kvp in settingsBundles
                     .Where(kvp => !Types.Contains(kvp.Key)))
            Types.Add(kvp.Key);
        
        Types.ForEach(type =>
        {
            Debug($"PhotonicFreedom: Found -> {type.Name}\n");
            var comp = type.InheritsFrom(typeof(PostProcessEffectSettings))
                ? layer.GetBundle(type).settings
                : mainCam.GetComponent(type) ?? Activator.CreateInstance(type);
            WriteDefaultSettings(type, comp);
            ReadAllSettings(type, comp);
        });
    }

    private static void WriteDefaultSettings(Type type, object comp)
    {
        var c = new ClassFieldHolder(type, comp);
        var serializer = new ClassSerializer(type);
        FieldHolders[type] = c;
        foreach (var field in c.Fields)
        {
            Debug($"PhotonicFreedom: Found field -> {field.Name} with value {field.DefaultValue}\n");
            if (!SupportedTypes.Contains(field.RealType) && !field.RealType.IsEnum) continue;
            var pairType = typeof(FieldValuePair<>).MakeGenericType(field.RealType);
            serializer.defaultFieldValues.Add(
                (IFieldValuePair)Activator.CreateInstance(pairType, field.Name, field.DefaultValue));
        }

        foreach (var property in c.Properties)
        {
            Debug($"PhotonicFreedom: Found property -> {property.Name} with value {property.DefaultValue}\n");
            if (!SupportedTypes.Contains(property.BasePropertyType) && !property.BasePropertyType.IsEnum) continue;
            var pairType = typeof(FieldValuePair<>).MakeGenericType(property.BasePropertyType);
            serializer.defaultFieldValues.Add(
                (IFieldValuePair)Activator.CreateInstance(pairType, property.Name, property.DefaultValue));
        }

        if (!Directory.Exists("rml_mods/Photonic_Settings")) Directory.CreateDirectory("rml_mods/Photonic_Settings");
        if (File.Exists($"rml_mods/Photonic_Settings/{type.Name}.json")) return;
        Debug("PhotonicFreedom: Writing missing settings file for " + type.Name);
        File.WriteAllText($"rml_mods/Photonic_Settings/{type.Name}.json",
            JsonConvert.SerializeObject(serializer, Formatting.Indented, serializerSettings));
    }

    private static void WriteCurrentSettings(Type type, object comp)
    {
        var c = new ClassFieldHolder(type, comp);
        var serializer = new ClassSerializer(type);
        FieldHolders[type] = c;
        foreach (var field in c.Fields)
            //Debug($"PhotonicFreedom: Found field -> {field.Name} with value {field.DefaultValue}\n")
            if (SupportedTypes.Contains(field.RealType) || field.RealType.IsEnum)
            {
                var pairType = typeof(FieldValuePair<>).MakeGenericType(field.RealType);
                serializer.defaultFieldValues.Add(
                    (IFieldValuePair)Activator.CreateInstance(pairType, field.Name, field.GetValue(comp)));
            }

        foreach (var property in c.Properties)
            //Debug($"PhotonicFreedom: Found property -> {property.Name} with value {property.GetValue(comp)}\n")
            if (SupportedTypes.Contains(property.BasePropertyType) || property.BasePropertyType.IsEnum)
            {
                var pairType = typeof(FieldValuePair<>).MakeGenericType(property.BasePropertyType);
                serializer.defaultFieldValues.Add(
                    (IFieldValuePair)Activator.CreateInstance(pairType, property.Name, property.GetValue(comp)));
            }

        if (!Directory.Exists("rml_mods/Photonic_Settings")) Directory.CreateDirectory("rml_mods/Photonic_Settings");
        if (!File.Exists($"rml_mods/Photonic_Settings/{type.Name}.json"))
            Debug("PhotonicFreedom: Writing missing settings file for " + type.Name);
        File.WriteAllText($"rml_mods/Photonic_Settings/{type.Name}.json",
            JsonConvert.SerializeObject(serializer, Formatting.Indented, serializerSettings));
    }

    private static void ReadAllSettings(Type type, object comp)
    {
        var loaded =
            JsonConvert.DeserializeObject<ClassSerializer>(
                File.ReadAllText($"rml_mods/Photonic_Settings/{type.Name}.json"), serializerSettings);
        var userspacePhotonicFreedom = Userspace.UserspaceWorld.RootSlot.FindChild("PhotonicFreedom");
        foreach (var field in FieldHolders[type].Fields)
        {
            var pair = loaded.defaultFieldValues.FirstOrDefault(pair => pair.fieldName == field.Name);
            //Debug($"PhotonicFreedom: Found pair -> {pair.fieldName}\n");
            if (pair is not { BoxedValue: not null } || field.Name == "active") continue;
            field.SetRealValue(comp, pair.BoxedValue);
            var sanitizedValue = pair.BoxedValue is Color
                ? ((Color)pair.BoxedValue).ToEngine()
                : pair.BoxedValue;
            var dynValue =
                userspacePhotonicFreedom.AttachComponent(
                    typeof(DynamicValueVariable<>).MakeGenericType(sanitizedValue.GetType()));
            if (dynValue == null)
                return;

            dynValue.GetType().GetProperty("LocalValue", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(dynValue, sanitizedValue);

            var dynValueField = dynValue.GetType().GetField("Value").GetValue(dynValue) as IField;
            var dynFieldName = dynValue.GetType().BaseType.GetField("VariableName").GetValue(dynValue) as IField;

            if (dynValueField == null || dynFieldName == null)
                return;

            dynFieldName.BoxedValue = $"{DynvarKey}_{type.Name}_{field.Name}";
            if (sanitizedValue is color)
                dynValueField.Changed += c => { OnPhotonicSettingChanged_Color(c, field, comp); };
            else if (SupportedTypes.Contains(sanitizedValue.GetType()) || sanitizedValue.GetType().IsEnum)
                dynValueField.Changed += c => { OnPhotonicSettingChanged(c, field, comp); };
        }

        foreach (var property in FieldHolders[type].Properties)
        {
            var pair = loaded.defaultFieldValues.FirstOrDefault(pair => pair.fieldName == property.Name);
            if (pair is not { BoxedValue: not null } || property.Name != "enabled") continue;
            property.SetRealValue(comp, pair.BoxedValue);
            var sanitizedValue = pair.BoxedValue is Color color
                ? color.ToEngine()
                : pair.BoxedValue;
            var dynValue =
                userspacePhotonicFreedom.AttachComponent(
                    typeof(DynamicValueVariable<>).MakeGenericType(sanitizedValue.GetType()));
            if (dynValue == null) return;

            dynValue.GetType().GetProperty("LocalValue", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(dynValue, sanitizedValue);

            var dynValueField = dynValue.GetType().GetField("Value").GetValue(dynValue) as IField;
            var dynFieldName = dynValue.GetType().BaseType.GetField("VariableName").GetValue(dynValue) as IField;

            if (dynValueField == null || dynFieldName == null)
                return;

            dynFieldName.BoxedValue = $"{DynvarKey}_{type.Name}_{property.Name}";

            if (SupportedTypes.Contains(sanitizedValue.GetType()))
                dynValueField.Changed += c => { OnPhotonicSettingChanged(c, property, comp); };
        }
    }

    public override void OnEngineInit()
    {
        //Engine.Current.RunPostInit(Bootstrap);
        var harmony = new Harmony("net.Cyro.PhotonicFreedom");
        harmony.PatchAll();
    }

    private static void OnPhotonicSettingChanged(IChangeable c, ClassFieldHolder.FieldHolder field, object comp)
    {
        Debug("PhotonicFreedom: OnPhotonicSettingChanged");
        var realValue = c.GetType().GetProperty("Value")!.GetValue(c);
        Debug($"PhotonicFreedom: Changed {field.Name} to {realValue}");
        field.SetRealValue(comp, realValue);
    }

    private static void OnPhotonicSettingChanged_Color(IChangeable c, ClassFieldHolder.FieldHolder field, object comp)
    {
        var realValue = c.GetType().GetProperty("Value")!.GetValue(c);
        field.SetRealValue(comp, ((color)realValue).ToUnity());
    }

    private static void OnPhotonicSettingChanged(IChangeable c, ClassFieldHolder.PropertyHolder property, object comp)
    {
        property.SetRealValue(comp, c.GetType().GetProperty("Value")!.GetValue(c));
    }

    public static void TestForUIBuilder(UIBuilder builder)
    {
        var mainCam = Camera.main;
        var layer = mainCam!.GetComponent<PostProcessLayer>();
        var attachMethod = typeof(Slot).GetMethods()
            .Single(m => m.Name == "AttachComponent" && m.IsGenericMethodDefinition);

        builder.Text("<b>PhotonicFreedom Settings</b>");
        var resetButton = builder.Button("Reset all settings");
        resetButton.LocalPressed += (b, d) =>
        {
            foreach (var kvp in SettingFields)
            foreach (var kvp2 in kvp.Value)
            {
                object? value = null;
                if (FieldHolders[kvp.Key].Fields.Any(f => f.Name == kvp2.Key))
                    value = FieldHolders[kvp.Key].Fields.First(f => f.Name == kvp2.Key).DefaultValue;
                else if (FieldHolders[kvp.Key].Properties.Any(p => p.Name == kvp2.Key))
                    value = FieldHolders[kvp.Key].Properties.First(p => p.Name == kvp2.Key).DefaultValue;
                if (value != null)
                {
                    if (value is Color color) value = color.ToEngine();
                    kvp2.Value.BoxedValue = value;
                }
                else
                {
                    Debug("PhotonicFreedom: Could not find default value for " + kvp2.Key);
                }
            }
        };

        builder.Text("---------------------------------------------");

        foreach (var type in Types)
        {
            var holder = FieldHolders[type];
            var comp = type.InheritsFrom(typeof(PostProcessEffectSettings))
                ? layer.GetBundle(type).settings
                : mainCam.GetComponent(type) ?? Activator.CreateInstance(type);

            builder.Text("<b>" + type.Name + "</b>");

            foreach (var property in holder.Properties)
                if (property.BasePropertyType == typeof(bool) && property.Name == "enabled")
                {
                    builder.Panel();
                    var list = builder.SplitHorizontally(0.42f, 0.58f);
                    builder.NestInto(list[1]);
                    var checkbox = builder.Checkbox(property.PrettifiedName, (bool)property.GetValue(comp!), false);
                    builder.NestOut();
                    builder.NestOut();
                    checkbox.State.SyncWithVariable($"{DynvarKey}_{type.Name}_{property.Name}");
                }

            foreach (var field in holder.Fields)
                if (field.RealType == typeof(bool) && field.Name == "enabled")
                {
                    builder.Panel();
                    var list = builder.SplitHorizontally(0.42f, 0.58f);
                    builder.NestInto(list[1]);
                    var checkbox = builder.Checkbox(field.PrettifiedName, (bool)field.GetValue(comp!), false);
                    builder.NestOut();
                    builder.NestOut();
                    checkbox.State.SyncWithVariable($"{DynvarKey}_{type.Name}_{field.Name}");
                }

            builder.Text("---------------------------------------------");
            var individualComponentResetButton = builder.Button("Reset");
            individualComponentResetButton.LocalPressed += (b, d) =>
            {
                foreach (var kvp in SettingFields[type])
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

                    if (value != null)
                    {
                        if (value is Color color) value = color.ToEngine();
                        kvp.Value.BoxedValue = value;
                    }
                    else
                    {
                        Debug("PhotonicFreedom: Could not find default value for " + kvp.Key + " in " + type.Name);
                    }
                }
            };
            builder.Text("---------------------------------------------");
            foreach (var field in holder.Fields)
            {
                if (field.RealType == typeof(int))
                {
                    var parser =
                        builder.HorizontalElementWithLabel(field.PrettifiedName, 0.7f, () => builder.IntegerField());
                    parser.ParsedValue.Value = (int)field.GetValue(comp!);
                    parser.ParsedValue.SyncWithVariable($"{DynvarKey}_{type.Name}_{field.Name}");
                    var slider = builder.Slider<int>(builder.Style.MinHeight, 0, (int)field.Min, (int)field.Max, true);
                    slider.Value.DriveFrom(parser.ParsedValue, true);
                    //parser.ParsedValue.SyncWithVariable($"{DynvarKey}_{field.BaseFieldType.Name}_{field.Name}");
                    if (SettingFields.TryGetValue(type, out var settingField))
                        settingField.Add(field.Name, parser.ParsedValue);
                    else
                        SettingFields.Add(type, new Dictionary<string, IField> { { field.Name, parser.ParsedValue } });
                }

                if (field.RealType == typeof(float))
                {
                    var parser =
                        builder.HorizontalElementWithLabel(field.PrettifiedName, 0.7f, () => builder.FloatField());
                    parser.ParsedValue.Value = (float)field.GetValue(comp!);
                    parser.ParsedValue.SyncWithVariable($"{DynvarKey}_{type.Name}_{field.Name}");
                    var slider = builder.Slider<float>(builder.Style.MinHeight, 0, field.Min, field.Max);
                    slider.Value.DriveFrom(parser.ParsedValue, true);
                    //parser.ParsedValue.SyncWithVariable($"{DynvarKey}_{field.BaseFieldType.Name}_{field.Name}");
                    if (SettingFields.TryGetValue(type, out var settingField))
                        settingField.Add(field.Name, parser.ParsedValue);
                    else
                        SettingFields.Add(type, new Dictionary<string, IField> { { field.Name, parser.ParsedValue } });
                }

                if (field.RealType == typeof(bool) && field.Name != "active" && field.Name != "enabled")
                {
                    var checkbox = builder.Checkbox(field.PrettifiedName, (bool)field.GetValue(comp!));
                    checkbox.State.SyncWithVariable($"{DynvarKey}_{type.Name}_{field.Name}");
                    //checkbox.State.SyncWithVariable($"{DynvarKey}_{field.BaseFieldType.Name}_{field.Name}");
                    if (SettingFields.TryGetValue(type, out var settingField))
                        settingField.Add(field.Name, checkbox.State);
                    else
                        SettingFields.Add(type, new Dictionary<string, IField> { { field.Name, checkbox.State } });
                }

                if (field.RealType.IsEnum)
                {
                    var valuefield = typeof(ValueField<>).MakeGenericType(field.RealType);
                    var val = attachMethod.MakeGenericMethod(valuefield)
                        .Invoke(builder.Root, new object?[] { true, null });
                    var valField = valuefield.GetField("Value").GetValue(val) as IField;
                    if (val == null || valField == null)
                    {
                        Debug("Could not attach value field");
                        return;
                    }

                    var enumParser = builder.HorizontalElementWithLabel(field.PrettifiedName, 0.7f,
                        () => builder.EnumMemberEditor(valField));
                    valField.BoxedValue = field.GetValue(comp!);
                    typeof(DynamicFieldExtensions).GetMethod("SyncWithVariable")!.MakeGenericMethod(field.RealType)
                        .Invoke(null,
                            new object?[] { valField, $"{DynvarKey}_{type.Name}_{field.Name}", false, false });
                    if (SettingFields.TryGetValue(type, out var settingField))
                        settingField.Add(field.Name, valField);
                    else
                        SettingFields.Add(type, new Dictionary<string, IField> { { field.Name, valField } });
                }

                if (field.RealType == typeof(Color))
                {
                    var val = builder.Root.AttachComponent<ValueField<color>>();
                    val.Value.Value = ((Color)field.GetValue(comp!)).ToEngine();
                    var parser = builder.HorizontalElementWithLabel<Component>(field.PrettifiedName, 0.5f, () =>
                    {
                        SyncMemberEditorBuilder.Build(val.Value, "",
                            val.GetSyncMemberFieldInfo(val.IndexOfMember(val.GetSyncMember("Value"))), builder);
                        return val;
                    });
                    val.Value.SyncWithVariable($"{DynvarKey}_{type.Name}_{field.Name}");
                    if (SettingFields.TryGetValue(type, out var settingField))
                        settingField.Add(field.Name, val.Value);
                    else
                        SettingFields.Add(type, new Dictionary<string, IField> { { field.Name, val.Value } });
                }

                if (field.Tooltip.Length > 0 && SupportedTypes.Contains(field.RealType))
                    builder.Text("<size=25%>" + field.Tooltip + "</size>", false, Alignment.TopLeft).Slot
                        .GetComponent<LayoutElement>().Priority.Value = 0;
            }
        }
    }

    [HarmonyPatch]
    public static class SettingsDialog_GetUIBuilder
    {
        [HarmonyPatch(typeof(SettingsDialog), "OnAttach")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> OnAttach_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var uiBuilderIndex = codes.FindLastIndex(x =>
                x.opcode == OpCodes.Ldfld && ((FieldInfo)x.operand).FieldType == typeof(UIBuilder));
            Debug("PhotonicFreedom: UIBuilder index: " + uiBuilderIndex);

            // This might as well be a manual postfix, but it feels better than jankily getting the components in a hierarchy and duplicating the UI style myself
            codes.InsertRange(codes.Count - 2, new List<CodeInstruction>
            {
                new(OpCodes.Ldloc_0),
                new(OpCodes.Ldfld, codes[uiBuilderIndex].operand),
                new(OpCodes.Call,
                    typeof(PhotonicFreedom).GetMethod("TestForUIBuilder", BindingFlags.Static | BindingFlags.Public))
            });
            return codes.AsEnumerable();
        }
    }

    [HarmonyPatch]
    public static class Userspace_Patch
    {
        private static bool _outputDeviceChanged;
        private static Camera? _mainCam;

        [HarmonyPatch(typeof(Userspace), "SaveAllSettings")]
        [HarmonyPrefix]
        public static void SaveAllSettings_Prefix(Userspace __instance)
        {
            _mainCam = Camera.main;
            if (_mainCam == null) return;
            var layer = _mainCam.GetComponent<PostProcessLayer>();
            if (layer == null) return;

            Types.ForEach(type =>
            {
                Debug($"PhotonicFreedom: Found -> {type.Name}\n");
                var comp = type.InheritsFrom(typeof(PostProcessEffectSettings))
                    ? layer.GetBundle(type).settings
                    : _mainCam.GetComponent(type) ?? Activator.CreateInstance(type);
                WriteCurrentSettings(type, comp);
            });
        }

        private static void VRActiveChangedEvent(bool a)
        {
            _outputDeviceChanged = true;
        }

        [HarmonyPatch(typeof(Userspace), "OnAttach")]
        [HarmonyPostfix]
        public static void OnAttach_Postfix(Userspace __instance)
        {
            _mainCam = Camera.main;
            Bootstrap();
            __instance.InputInterface.VRActiveChanged += VRActiveChangedEvent;
        }

        [HarmonyPatch(typeof(ScreenModeController), "OnCommonUpdate")]
        [HarmonyPostfix]
        public static void OnCommonUpdate_Postfix(ScreenModeController __instance)
        {
            if (!_outputDeviceChanged) return;
            Msg("Waiting for changed camera");
            if (_mainCam == Camera.main || Camera.main == null) return;
            Msg("Resetting Camera");
            _mainCam = Camera.main;
            _outputDeviceChanged = false;
            Bootstrap();
        }
    }
}