using BaseX;
using FrooxEngine;
using FrooxEngine.UIX;
using HarmonyLib;
using NeosModLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using System.Reflection;
using PhotonicFreedom;
using Newtonsoft.Json;
using System.IO;
using System.Threading.Tasks;

namespace PhotonicFreedom
{
    public static class SettingsHelper
    {
        public static Type[] GetTargettedTypes()
        {
            return new Type[]
            {
                typeof(MotionBlur),
                typeof(Bloom),
                typeof(AmplifyOcclusionEffect)
            };
        }
        public static string DefaultFilePath => "nml_mods/photonic_freedom/";
        public static List<FieldInfo[]> RetrieveTypeFields()
        {
            return GetTargettedTypes().Select(x => x.GetFields(BindingFlags.Public | BindingFlags.Instance)).ToList();
        }

        public static List<PropertyInfo[]> RetrieveProperties()
        {
            return GetTargettedTypes().Select(x => x.GetProperties(BindingFlags.Public | BindingFlags.Instance)).ToList();
        }

        public static List<ClassFieldHolder> RetrieveClassSettings()
        {
            //For each json file in the settings directory, deserialize it into a ClassFieldHolder
            List<ClassFieldHolder> settings = new List<ClassFieldHolder>();
            foreach (string file in Directory.GetFiles(DefaultFilePath, "*.json"))
            {
                ClassFieldHolder holder = JsonConvert.DeserializeObject<ClassFieldHolder>(File.ReadAllText(file));
                settings.Add(holder);
            }
            return settings;
        }

        public static void UpdateSettings(string type, string fieldname, string value)
        {
            //Check if the settings file exists and if it doesn't, throw an exception
            if (!File.Exists(DefaultFilePath + type + ".json"))
            {
                throw new Exception("Settings file for " + type + " does not exist");
            }
            //Deserialize the settings file into a ClassFieldHolder
            ClassFieldHolder holder = JsonConvert.DeserializeObject<ClassFieldHolder>(File.ReadAllText(DefaultFilePath + type + ".json"));
            //Check if the field exists in the ClassFieldHolder
            if (!holder.fields.ContainsKey(fieldname))
            {
                throw new Exception("Field " + fieldname + " does not exist in " + type);
            }
            //Update the value of the field
            holder.fields[fieldname] = value;
            //Serialize the ClassFieldHolder into a json string
            string json = JsonConvert.SerializeObject(holder, Formatting.Indented);
            //Write the json string to the settings file
            File.WriteAllText(DefaultFilePath + type + ".json", json);
        }

        public static void UpdateDefaultSettings()
        {
            //If the settings directory is there, delete all of the json files in it
            foreach (string file in Directory.GetFiles(DefaultFilePath + "Defaults/", "*.json"))
            {
                File.Delete(file);
            }

            foreach(FieldInfo[] fields in RetrieveTypeFields())
            {
                ClassFieldHolder holder = new ClassFieldHolder();
                holder.type = fields[0].DeclaringType.AssemblyQualifiedName;
                holder.fields = new Dictionary<string, string>();
                foreach(FieldInfo field in fields)
                {
                    Type SanitizedType = FieldSanitizer(field.FieldType);
                    bool isCorrectType = SanitizedType == typeof(int) || SanitizedType == typeof(float) || SanitizedType == typeof(bool);
                    if(isCorrectType)
                    {
                        holder.fields.Add(field.Name, GetValueFromField(field, GameObject.FindObjectOfType(fields[0].DeclaringType)).ToString());
                    }
                }
                string json = JsonConvert.SerializeObject(holder, Formatting.Indented);
                File.WriteAllText(DefaultFilePath + "Defaults/" + fields[0].DeclaringType.Name + ".json", json);
            }
        }

        public static void InstantiateSettings(bool ResetAll = false, bool WriteDefaults = true, bool UpdateDefaults = false)
        {
            //If the settings directory isn't there, create it
            if (!Directory.Exists(DefaultFilePath))
            {
                Directory.CreateDirectory(DefaultFilePath);
                Directory.CreateDirectory(DefaultFilePath + "Defaults");
            }

            //If the default settings files don't exist, create them
            bool flag = false;
            foreach(FieldInfo[] f in RetrieveTypeFields())
            {
                if (!File.Exists(DefaultFilePath + "Defaults/" + f[0].DeclaringType.Name + ".json"))
                {
                    flag = true;
                    break;
                }
            }

            if (flag || UpdateDefaults)
            {
                UpdateDefaultSettings();
            }

            //If the settings files don't exist, create them from the default settings files
            foreach (FieldInfo[] f in RetrieveTypeFields())
            {
                if (!File.Exists(DefaultFilePath + f[0].DeclaringType.Name + ".json"))
                {
                    File.Copy(DefaultFilePath + "Defaults/" + f[0].DeclaringType.Name + ".json", DefaultFilePath + f[0].DeclaringType.Name + ".json");
                }
            }


            foreach(ClassFieldHolder hold in RetrieveClassSettings())
            {
                Type declType = Type.GetType(hold.type);

                object[] correspondingWorldObjs = GameObject.FindObjectsOfType(declType);


                foreach(KeyValuePair<string, string> entry in hold.fields)
                {
                    FieldInfo field = declType.GetField(entry.Key);

                    Type SanitizedType = FieldSanitizer(field.FieldType);
                    bool isCorrectType = SanitizedType == typeof(int) || SanitizedType == typeof(float) || SanitizedType == typeof(bool);

                    object val = null;

                    if(isCorrectType)
                    {
                        //Interpret the string as the correct type, even if it's a bool
                        try
                        {
                            val = Convert.ToBoolean(entry.Value);
                        } 
                        catch
                        {
                            val = Convert.ChangeType(entry.Value, SanitizedType);
                        }
                        UniLog.Log("[SettingsHelper] " + field.Name + " is a " + SanitizedType.Name + " and has the value " + val);
                        SetValueToAllFields(field, correspondingWorldObjs, val);
                    }
                    else
                    {
                        UniLog.Log("[SettingsHelper] Field type not supported: " + SanitizedType.Name);
                    }
                    
                }
                
            }

            /*
            RetrieveProperties().ForEach(x => {
                UniLog.Log(x.GetFirst().ReflectedType.Name + ":\n");
                x.ToList().ForEach(y => UniLog.Log(y.Name + " is of type: " + y.PropertyType));
            });*/
        }

        public static Type FieldSanitizer(Type t)
        {
            if (t.Name == "FloatParameter")
            {
                return typeof(float);
            }
            else if (t.Name == "IntParameter")
            {
                return typeof(int);
            }
            else if (t.Name == "BoolParameter")
            {
                return typeof(bool);
            }
            else
            {
                return t;
            }
        }
        public static string GetSettingPath(FieldInfo field)
        {
            return "Settings.PostProcessing." + field.DeclaringType.Name + "." + field.Name;
        }


        public static void SetValueToField(FieldInfo field, object obj, object value)
        {
            Type type = field.FieldType;
            bool isValue = type.IsValueType;

            if (isValue)
            {
                field.SetValue(obj, value);
                UniLog.Log("Set " + field.Name + " to " + value + "\n");
            }
            else if (type.GetField("value") != null)
            {
                UniLog.Log("Setting value of " + field.Name + " to " + value);
                AccessTools.Field(type, "value").SetValue(field.GetValue(obj), Convert.ChangeType(value, type.GetField("value").FieldType));
            }
        }

        public static void SetValueToAllFields(FieldInfo field, object[] obj, object value)
        {
            foreach (object o in obj)
            {
                SetValueToField(field, o, value);
            }
        }

        public static object GetValueFromField(FieldInfo field, object obj)
        {
            UniLog.Log(field.Name + " is of type: " + field.FieldType);
            object val = null;
            Type type = field.FieldType;
            bool isValue = type.IsValueType;

            if (isValue)
            {
                val = field.GetValue(obj);
                UniLog.Log(val);
                return val;
            }
            else if (type.GetField("value") != null)
            {
                val = AccessTools.Field(type, "value").GetValue(field.GetValue(obj));
                UniLog.Log(val == null ? "NULL" : val);
                return val;
            }
            else
            {
                UniLog.Log("No value found");
                val = null;
                return val;
            }
        }

        public static void GetValueFromProperty(PropertyInfo property, object obj)
        {
            UniLog.Log(property.Name + " is of type: " + property.PropertyType);
            UniLog.Log(property.GetValue(obj));
        }


    }
}
