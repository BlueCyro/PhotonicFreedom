using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using FrooxEngine;
using FrooxEngine.UIX;
using BaseX;
using System.Reflection;
using UnityEngine.Rendering.PostProcessing;

/*
Create a slider

var BlurQualityText = Builder.HorizontalElementWithLabel<IntTextEditorParser>("Motion Blur Quality", 0.7f, () => Builder.IntegerField(0, 100, 1, true));
var BlurQualitySlider = Builder.Slider<int>(Builder.Style.MinHeight, 10, 0, 100, true);
BlurQualitySlider.Value.SyncWithSetting(BlurQualityPath, SettingSync.LocalChange.UpdateSetting);
*/
namespace PhotonicFreedom
{
    public static class SettingsMenuHelper
    {
        //Convert the commented code snippet to a method
        public static void CreateSyncedCheckbox(
            UIBuilder builder,
            string checkLabel,
            out Checkbox checkOut,
            out SettingSync<bool> syncOut,
            string settingPath,
            SettingSync.LocalChange localChange = SettingSync.LocalChange.UpdateSetting,
            bool defaultState = false,
            bool labelFirst = true
        )
        {
            //Create a checkbox
            checkOut = builder.Checkbox(checkLabel, defaultState, labelFirst, 4f);
            //Create a settingsync for the checkbox
            syncOut = checkOut.State.SyncWithSetting(settingPath, localChange);
        }

        //Add arguments for all undefined variables in the method
        public static void CreateSyncedIntSlider(
            UIBuilder builder,
            string elementLabel,
            string settingPath,
            out IntTextEditorParser parserOut,
            out Slider<int> sliderOut,
            out SettingSync<int> sliderSyncOut,
            out SettingSync<int> parserSyncOut,
            float separation = 0.7f,
            int def = 0, int min = 0,
            int max = 1, int incrementStep = 1,
            bool parseCont = true,
            SettingSync.LocalChange localChange = SettingSync.LocalChange.UpdateSetting
        )
        {
            //Create a field so that the value can be edited manually
            parserOut = builder.HorizontalElementWithLabel<IntTextEditorParser>(elementLabel, separation, () => builder.IntegerField(min, max, incrementStep, parseCont));
            //Create a slider so that the value can be changed quickly
            sliderOut = builder.Slider<int>(builder.Style.MinHeight, def, min, max, true);
            //Sync with the specified setting
            sliderSyncOut = sliderOut.Value.SyncWithSetting(settingPath, localChange);
            //Sync the value of the field with the slider
            parserSyncOut = parserOut.ParsedValue.SyncWithSetting(settingPath, localChange);
        }
        public static void CreateSyncedFloatSlider(
            UIBuilder builder,
            string elementLabel,
            string settingPath,
            out FloatTextEditorParser parserOut,
            out Slider<float> sliderOut,
            out SettingSync<float> sliderSyncOut,
            out SettingSync<float> parserSyncOut,
            float separation = 0.7f,
            float def = 0f, float min = 0f,
            float max = 1f, int decimalPlaces = 2,
            SettingSync.LocalChange localChange = SettingSync.LocalChange.UpdateSetting
        )
        {
            //Create a field so that the value can be edited manually
            parserOut = builder.HorizontalElementWithLabel<FloatTextEditorParser>(elementLabel, separation, () => builder.FloatField(min, max, decimalPlaces));
            //Create a slider so that the value can be changed quickly
            sliderOut = builder.Slider<float>(builder.Style.MinHeight, def, min, max, false);
            //Sync with the specified setting
            sliderSyncOut = sliderOut.Value.SyncWithSetting(settingPath, localChange);
            //Sync the value of the field with the slider
            parserSyncOut = parserOut.ParsedValue.SyncWithSetting(settingPath, localChange);
        }

        public static void CreateSettingForType(FieldInfo field, UIBuilder builder, object Obj, string settingPath)
        {
            //Get the type of the field
            Type fieldType = field.FieldType;
            //Get the name of the field
            string fieldName = field.Name;
            //Get the value of the field
            object fieldValue = field.GetValue(Obj);

            UniLog.Log("Field type: " + fieldType.ToString());
            UniLog.Log("Field name: " + fieldName);

            //Create a synced checkbox for the "enabled" field


            //Check if the fieldType is a boolean
            switch(fieldType.Name)
            {
                case "Boolean":
                    //Create a checkbox
                    Checkbox checkOut;
                    SettingSync<bool> syncOut;
                    CreateSyncedCheckbox(builder, fieldName, out checkOut, out syncOut, settingPath, default, (bool)fieldValue);
                    syncOut.Changed += (IChangeable c) =>
                    {
                        //Find all objects of the same type as Obj and set the corresponding field on each object to the value of the checkbox
                        foreach (object o in Resources.FindObjectsOfTypeAll(Obj.GetType()))
                        {
                            field.SetValue(o, Settings.ReadValue(settingPath, field.GetValue(o)));
                        }

                    };
                    break;
                case "Single":
                    //Create a slider
                    FloatTextEditorParser parserOut;
                    Slider<float> sliderOut;
                    SettingSync<float> sliderSyncOut;
                    SettingSync<float> parserSyncOut;

                    //Get the range attributes of the field
                    var range = field.GetCustomAttribute<UnityEngine.RangeAttribute>();
                    float min = range != null ? (float)range.min : 0f;
                    float max = range != null? (float)range.max : 10f;

                    CreateSyncedFloatSlider(builder, fieldName, settingPath, out parserOut, out sliderOut, out sliderSyncOut, out parserSyncOut, 0.7f, (float)fieldValue, min, max, 2, default);
                    sliderSyncOut.TargetField.Target.Value = (float)fieldValue;
                    sliderSyncOut.Changed += (IChangeable c) =>
                    {
                        //Find all objects of the same type as Obj and set the corresponding field on each object to the value of the slider
                        foreach (object o in Resources.FindObjectsOfTypeAll(Obj.GetType()))
                        {
                            field.SetValue(o, Settings.ReadValue(settingPath, field.GetValue(o)));
                        }
                    };
                    break;
                default:
                    break;
            }
        }
    }
}
