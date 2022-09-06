using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using HarmonyLib;
using Newtonsoft.Json;
using System.Runtime.InteropServices;
using System.Reflection.Emit;

namespace Misc_Mods
{
    internal class ArbitraryGODumper
    {
        internal static BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        internal static BindingFlags PublicInstanceFlags = BindingFlags.Instance | BindingFlags.Public;

        internal static Logger logger;

        internal static void ConfigureLogger()
        {
            logger = new Logger("GODumperV2");
        }

        private static string CombinePath(string path1, string path2)
        {
            return $"{path1}/{path2}";
        }

        private struct ComponentRef {
            public Type type;
            public Component component;
            public GameObjectRef objectRef;
            public JObject jObj;
            public int index;

            public override string ToString()
            {
                return (index >= 0 ? $"[{index}]" : "") + type.FullName;
            }

            public string GetReferenceString()
            {
                return this.objectRef.GetReferenceString() + "~>" + this.ToString();
            }
        }

        private struct UnityObjectRef
        {
            public Type type;
            public UnityEngine.Object unityObject;
            public string path;

            public override string ToString()
            {
                return this.unityObject.name;
            }

            public string GetReferenceString()
            {
                return this.path + "." + this.ToString();
            }
        }

        private struct GameObjectRef
        {
            public GameObject gameObject;
            public string path;
            public int depth;
            public int index;
            public JObject jObj;

            public override string ToString()
            {
                return (index >= 0 ? $"[{index}]" : "") + gameObject.name;
            }

            public string GetReferenceString()
            {
                return CombinePath(path, this.ToString());
            }

            public string GetTransformString()
            {
                return this.GetReferenceString() + "~>transform";
            }
        }

        private struct MissingTargetRef
        {
            public JToken source;
            public object missingTarget;
            public string jProperty;
            public int index;
        }

        private GameObject root;
        private int maxLayers;

        private JToken output = null;

        public bool showTypeInformation;

        public ArbitraryGODumper(GameObject gameObject, int maxLayers = 16)
        {
            this.root = gameObject;
            this.maxLayers = maxLayers;
            logger.Debug($"Dumping details for GO {gameObject.name}");
        }

        private Dictionary<Component, ComponentRef> ComponentMap = new Dictionary<Component, ComponentRef>();
        private Dictionary<GameObject, GameObjectRef> GameObjectMap = new Dictionary<GameObject, GameObjectRef>();
        private Dictionary<UnityEngine.Object, UnityObjectRef> UnityObjectMap = new Dictionary<UnityEngine.Object, UnityObjectRef>();

        private List<MissingTargetRef> externalRefs = new List<MissingTargetRef>();
        private HashSet<GameObject> missingGOs = new HashSet<GameObject>();
        private HashSet<Component> components = new HashSet<Component>();
        private HashSet<GameObject> gameObjects = new HashSet<GameObject>();

        private JToken DumpTransform(GameObjectRef objectRef)
        {
            Transform transform = objectRef.gameObject.transform;
            JObject OBJ = new JObject();
            OBJ.Add("localPosition", DumpArbitraryObject_Internal(OBJ, "localPosition", $"{objectRef.GetTransformString()}=>localPosition", transform.localPosition));
            OBJ.Add("localScale", DumpArbitraryObject_Internal(OBJ, "localScale", $"{objectRef.GetTransformString()}=>localScale", transform.localScale));
            OBJ.Add("localEulerAngles", DumpArbitraryObject_Internal(OBJ, "localEulerAngles", $"{objectRef.GetTransformString()}=>localEulerAngles", transform.localEulerAngles));
            OBJ.Add("position", DumpArbitraryObject_Internal(OBJ, "position", $"{objectRef.GetTransformString()}=>position", transform.position));
            OBJ.Add("eulerAngles", DumpArbitraryObject_Internal(OBJ, "eulerAngles", $"{objectRef.GetTransformString()}=>eulerAngles", transform.eulerAngles));
            OBJ.Add("forward", DumpArbitraryObject_Internal(OBJ, "forward", $"{objectRef.GetTransformString()}=>forward", transform.forward));
            OBJ.Add("up", DumpArbitraryObject_Internal(OBJ, "up", $"{objectRef.GetTransformString()}=>up", transform.up));
            OBJ.Add("right", DumpArbitraryObject_Internal(OBJ, "right", $"{objectRef.GetTransformString()}=>right", transform.right));
            OBJ.Add("localToWorldMatrix", DumpArbitraryObject_Internal(OBJ, "localToWorldMatrix", $"{objectRef.GetTransformString()}=>localToWorldMatrix", transform.localToWorldMatrix));
            OBJ.Add("worldToLocalMatrix", DumpArbitraryObject_Internal(OBJ, "worldToLocalMatrix", $"{objectRef.GetTransformString()}=>worldToLocalMatrix", transform.worldToLocalMatrix));
            return OBJ;
        }

        private GameObjectRef GetGameObjectRef(GameObject gameObject, string currentPath, int depth, int index = -1)
        {
            if (!GameObjectMap.TryGetValue(gameObject, out GameObjectRef objectRef))
            {
                objectRef = new GameObjectRef
                {
                    gameObject = gameObject,
                    path = currentPath,
                    depth = depth,
                    index = index,
                    jObj = new JObject()
                };
                GameObjectMap.Add(gameObject, objectRef);
            }
            return objectRef;
        }

        private ComponentRef GetComponentRef(Component component, GameObjectRef objectRef, int index = -1)
        {
            if (!ComponentMap.TryGetValue(component, out ComponentRef componentRef))
            {
                componentRef = new ComponentRef
                {
                    component = component,
                    type = component.GetType(),
                    index = index,
                    jObj = new JObject(),
                    objectRef = objectRef
                };
                ComponentMap.Add(component, componentRef);
            }
            return componentRef;
        }

        private void DumpGameObjectRecursive(GameObjectRef objectRef, string currentPath, int depth)
        {
            GameObject currentObject = objectRef.gameObject;
            objectRef.jObj.Add("activeSelf", currentObject.activeSelf);
            gameObjects.Add(currentObject);
            string spacing = new String(' ', depth * 4);
            logger.Trace($"{spacing}Going through children of {currentPath}/{currentObject.name}");
            Dictionary<string, int> nameCount = new Dictionary<string, int>();
            for (int i = 0; i < currentObject.transform.childCount; i++)
            {
                Transform child = currentObject.transform.GetChild(i);
                string name = child.name;
                if (nameCount.TryGetValue(name, out int count))
                {
                    logger.Trace($"{spacing}|-Found duplicate name " + name);
                    nameCount[name] = count + 1;
                }
                else
                {
                    logger.Trace($"{spacing}|-Found new name " + name);
                    nameCount.Add(name, 1);
                }
            }

            Dictionary<string, int> currIndCount = new Dictionary<string, int>();
            for (int i = 0; i < currentObject.transform.childCount; i++)
            {
                Transform child = currentObject.transform.GetChild(i);
                int totalCount = nameCount[child.name];
                int index = -1;
                if (totalCount > 1)
                {
                    if (currIndCount.TryGetValue(child.name, out int currIndex))
                    {
                        index = currIndex;
                        currIndCount[child.name] = currIndex + 1;
                    }
                    else
                    {
                        index = 0;
                        currIndCount.Add(child.name, 1);
                    }
                }
                string indexString = index >= 0 ? $"[{index}]" : "";
                if (child.gameObject != this.root)
                {
                    GameObjectRef childRef = GetGameObjectRef(child.gameObject, CombinePath(currentPath, objectRef.ToString()), depth + 1, index);
                    DumpGameObjectRecursive(childRef, CombinePath(currentPath, objectRef.ToString()), depth + 1);
                    logger.Trace($"{spacing}|-Adding GO with name {child.name}{indexString} to parent of {currentPath}/{currentObject.name}");
                    objectRef.jObj.Add($"GameObject{indexString}|{child.name}", childRef.jObj);
                }
                else
                {
                    logger.Trace($"FOUND ROOT!! MUST BE FROM EXTERNAL");
                    objectRef.jObj.Add($"GameObject{indexString}|{child.name}", "<ROOT>");
                }
            }
            return;
        }

        private JToken DumpArbitraryDictionary_Internal(JObject source, Array keys, Array values, string currentPath)
        {
            int errorKeyCount = 0;
            int emptyKeyCount = 0;
            string[] keyNames = new string[keys.Length];
            Dictionary<string, int> keyOccurenceCount = new Dictionary<string, int>();
            for (int i = 0; i < keys.Length; i++)
            {
                object key = keys.GetValue(i);
                try
                {
                    string keyName = key.ToString();
                    if (keyName == null || keyName.Length == 1)
                    {
                        keyNames[i] = $"EMPTY<{emptyKeyCount}>";
                        emptyKeyCount++;
                    }
                    else
                    {
                        if (keyOccurenceCount.TryGetValue(keyName, out int count))
                        {
                            keyNames[i] = $"{keyName}<{count}>";
                            keyOccurenceCount[keyName] = count + 1;
                        }
                        else
                        {
                            keyOccurenceCount[keyName] = 1;
                            keyNames[i] = keyName;
                        }
                    }
                }
                catch (Exception e)
                {
                    logger.Error("ERROR CONVERTING KEY TO STRING");
                    logger.Error(e);
                    keyNames[i] = $"ERROR<{errorKeyCount}>";
                    errorKeyCount++;
                }
            }

            for (int i = 0; i < keys.Length; i++)
            {
                JToken valueToken = DumpArbitraryObject_Internal(source, keyNames[i], currentPath + $"{{{keyNames[i]}}}", values.GetValue(i));
                source.Add(keyNames[i], valueToken);
            }
            return source;
        }

        private JToken DumpArbitraryDictionary(object objectToDump, string currentPath)
        {
            Type[] interfaces = objectToDump.GetType().GetInterfaces();

            JObject obj = new JObject();
            Type idictionaryInterface = interfaces.First(i => i.IsGenericType && typeof(IDictionary<,>).IsAssignableFrom(i.GetGenericTypeDefinition()));
            Type[] genericTypes = idictionaryInterface.GetGenericArguments();

            PropertyInfo Keys = AccessTools.Property(idictionaryInterface, "Keys");
            PropertyInfo Values = AccessTools.Property(idictionaryInterface, "Values");

            var keys = Keys.GetValue(objectToDump);
            var values = Values.GetValue(objectToDump);

            Type keyType = keys.GetType();
            Type keyInterface = keyType.GetInterfaces().First(i => i.IsGenericType && typeof(IEnumerable<>).IsAssignableFrom(i.GetGenericTypeDefinition()));
            MethodInfo KeysToArray = AccessTools.Method(typeof(Enumerable), "ToArray", generics: keyInterface.GenericTypeArguments);
            logger.Trace(KeysToArray == null ? "FAILED to get Keys ToArray method" : "GOT Keys ToArray method");
            var keysObj = KeysToArray.Invoke(null, new object[] { keys });
            Type keysArrayType = keysObj.GetType();
            logger.Trace($"KEYS HAS ACTUAL TYPE OF {keysArrayType.FullName}, with element type of {keysArrayType.GetElementType()}");

            Type valuesType = values.GetType();
            Type valuesInterface = valuesType.GetInterfaces().First(i => i.IsGenericType && typeof(IEnumerable<>).IsAssignableFrom(i.GetGenericTypeDefinition()));
            MethodInfo ValuesToArray = AccessTools.Method(typeof(Enumerable), "ToArray", generics: valuesInterface.GenericTypeArguments);
            logger.Trace(ValuesToArray == null ? "FAILED to get Values ToArray method" : "GOT Values ToArray method");
            var valuesObj = ValuesToArray.Invoke(null, new object[] { values });
            Type valuesArrayType = valuesObj.GetType();
            logger.Trace($"VALUES HAS ACTUAL TYPE OF {valuesArrayType.FullName}, with element type of {valuesArrayType.GetElementType()}");

            return DumpArbitraryDictionary_Internal(obj, (Array)keysObj, (Array)valuesObj, currentPath);
        }

        private JToken DumpArbitraryCollection_Internal(JArray source, Array objects, string currentPath)
        {
            for (int i = 0; i < objects.Length; i++)
            {
                try
                {
                    source[i] = DumpArbitraryObject_Internal(source, null, currentPath + $"[{i}]", objects.GetValue(i), i);
                }
                catch (Exception e)
                {
                    logger.Error($"FAILED to write out array element at index {i}");
                    logger.Error(e);
                }
            }
            return source;
        }

        private JToken DumpArbitraryCollection(object objectToDump, string currentPath)
        {

            Type[] interfaces = objectToDump.GetType().GetInterfaces();
            JArray array = new JArray();

            Type ienumerableInterface = interfaces.First(i => i.IsGenericType && typeof(IEnumerable<>).IsAssignableFrom(i.GetGenericTypeDefinition()));
            Type[] genericTypes = ienumerableInterface.GetGenericArguments();

            MethodInfo ToArray = AccessTools.Method(typeof(Enumerable), "ToArray", generics: ienumerableInterface.GenericTypeArguments);
            logger.Trace(ToArray == null ? "FAILED to get ToArray method" : "GOT ToArray method");
            var objects = ToArray.Invoke(null, new object[] { objectToDump });
            Type arrayType = objects.GetType();
            logger.Trace($"OBJECT[] HAS ACTUAL TYPE OF {arrayType.FullName}, with element type of {arrayType.GetElementType()}");
            Array castObjects = (Array)objects;

            for (int i = 0; i < castObjects.Length; i++)
            {
                array.Add("<ERROR>");
            }

            return DumpArbitraryCollection_Internal(array, castObjects, currentPath);
        }

        private static bool IsNumericType(Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    return true;
                default:
                    return false;
            }
        }

        private void AddMissingGO(GameObject missingObject)
        {
            HashSet<GameObject> itemsToRemove = new HashSet<GameObject>();
            foreach (GameObject gameObject in missingGOs)
            {
                if (gameObject.transform.IsChildOf(missingObject.transform))
                {
                    if (gameObject == missingObject)
                    {
                        return;
                    }
                    itemsToRemove.Add(gameObject);
                }
                else if (missingObject.transform.IsChildOf(gameObject.transform))
                {
                    return;
                }
            }

            // Remove all old GOs that are children of the new GO
            foreach (GameObject gameObject in itemsToRemove)
            {
                missingGOs.Remove(gameObject);
            }

            // If we got here, that means this is not the child of any existing GO, so add it
            missingGOs.Add(missingObject);
        }

        private JToken DumpPrimitive(object primitive)
        {
            if (primitive is Boolean boolean)
            {
                return (bool)boolean;
            }
            if (primitive is Byte byteObj)
            {
                return (byte)byteObj;
            }
            if (primitive is SByte sByteObj)
            {
                return (sbyte)sByteObj;
            }
            if (primitive is Int16 int16)
            {
                return (short)int16;
            }
            if (primitive is UInt16 uint16)
            {
                return (ushort)uint16;
            }
            if (primitive is Int32 int32)
            {
                return (int)int32;
            }
            if (primitive is UInt32 uint32)
            {
                return (uint)uint32;
            }
            if (primitive is Int64 int64)
            {
                return (long)int64;
            }
            if (primitive is UInt64 uint64)
            {
                return (ulong)uint64;
            }
            if (primitive is Single single)
            {
                return (float)single;
            }
            if (primitive is Double doubleObj) {
                return (double)doubleObj;
            }

            Type pointerType = typeof(int).MakePointerType();
            int pointerSize = Marshal.SizeOf(pointerType);
            if (primitive is IntPtr intptr)
            {
                switch(pointerSize)
                {
                    case 1:
                        return (sbyte)intptr;
                    case 2:
                        return (short)intptr;
                    case 4:
                        return (int)intptr;
                    case 8:
                        return (long)intptr;
                    default:
                        return (int)intptr;
                }
            }
            if (primitive is UIntPtr uintptr)
            {
                switch (pointerSize)
                {
                    case 1:
                        return (byte)uintptr;
                    case 2:
                        return (ushort)uintptr;
                    case 4:
                        return (uint)uintptr;
                    case 8:
                        return (ulong)uintptr;
                    default:
                        return (int)uintptr;
                }
            }
            return primitive.ToString();
        }

        private JToken DumpArbitraryObject_Internal(JToken sourceObj, string jPropertyName, string currentPath, object objectToDump, int index = -1)
        {
            if (objectToDump is null)
            {
                return null;
            }
            Type objectType = objectToDump.GetType();
            logger.Trace($"TRYING TO DUMP OBJECT OF TYPE {objectType.FullName} for property {jPropertyName}");
            if (objectToDump is GameObject gameObject)
            {
                // Remove Rigidbody refs since that always goes to tank
                /* if (currentPath.ToLower().EndsWith("gameobject"))
                {
                    return $"<{gameObject.name}>";
                } */

                if (GameObjectMap.TryGetValue(gameObject, out GameObjectRef objectRef))
                {
                    return objectRef.GetReferenceString();
                }
                else
                {
                    MissingTargetRef targetRef = new MissingTargetRef
                    {
                        source = sourceObj,
                        missingTarget = objectToDump,
                        jProperty = jPropertyName,
                        index = index
                    };
                    AddMissingGO(gameObject);
                    externalRefs.Add(targetRef);
                    return null;
                }
            }
            else if (objectToDump is Tank tank)
            {
                return $"<TANK: {tank.name}>";
            }
            else if (objectToDump is Transform transform)
            {
                // Remove Rigidbody refs since that always goes to tank
                /* if (currentPath.ToLower().EndsWith("transform"))
                {
                    return $"<{transform.name}>";
                } */

                if (GameObjectMap.TryGetValue(transform.gameObject, out GameObjectRef objectRef))
                {
                    return objectRef.GetReferenceString() + "~>transform";
                }
                else
                {
                    MissingTargetRef targetRef = new MissingTargetRef
                    {
                        source = sourceObj,
                        missingTarget = objectToDump,
                        jProperty = jPropertyName,
                        index = index
                    };
                    AddMissingGO(transform.gameObject);
                    externalRefs.Add(targetRef);
                    return null;
                }
            }
            else if (objectToDump is TankBlock tankBlock)
            {
                JObject tankObj = new JObject();
                DumpTankBlock(tankObj, tankBlock, currentPath);
                return tankObj;
            }
            else if (objectToDump is Visible visible)
            {
                string visibleName = visible != null ? visible.name : "#NULL";
                return $"<VISIBLE {visibleName}>";
            }
            else if (objectToDump is Component component)
            {
                if (ComponentMap.TryGetValue(component, out ComponentRef targetComponentRef))
                {
                    return targetComponentRef.GetReferenceString();
                }
                else
                {
                    MissingTargetRef targetRef = new MissingTargetRef
                    {
                        source = sourceObj,
                        missingTarget = objectToDump,
                        jProperty = jPropertyName,
                        index = index
                    };
                    AddMissingGO(component.gameObject);
                    externalRefs.Add(targetRef);
                    return null;
                }
            }
            else if (objectToDump is Vector2 vector2)
            {
                return new JArray(vector2.x, vector2.y);
            }
            else if (objectToDump is Vector3 vector3)
            {
                return new JArray(vector3.x, vector3.y, vector3.z);
            }
            else if (objectToDump is Vector4 vector4)
            {
                return new JArray(vector4.x, vector4.y, vector4.z, vector4.w);
            }
            else if (objectToDump is IntVector2 intVector2)
            {
                return new JArray(intVector2.x, intVector2.y);
            }
            else if (objectToDump is IntVector3 intVector3)
            {
                return new JArray(intVector3.x, intVector3.y, intVector3.z);
            }
            else if (objectToDump is Matrix4x4 matrix)
            {
                Vector4 row0 = matrix.GetRow(0);
                Vector4 row1 = matrix.GetRow(1);
                Vector4 row2 = matrix.GetRow(2);
                Vector4 row3 = matrix.GetRow(3);
                JArray matrixOuter = new JArray
                {
                    new JArray(row0.x, row0.y, row0.z, row0.w),
                    new JArray(row1.x, row1.y, row1.z, row1.w),
                    new JArray(row2.x, row2.y, row2.z, row2.w),
                    new JArray(row3.x, row3.y, row3.z, row3.w),
                };
                return matrixOuter;
            }
            else if (objectToDump is string text)
            {
                return text;
            }
            else if (IsNumericType(objectType))
            {
                return new JValue(objectToDump);
            }
            else if (objectType.IsPrimitive)
            {
                return DumpPrimitive(objectToDump);
            }
            else if (objectType.IsEnum)
            {
                return objectToDump.ToString();
            }
            else if (objectToDump is Type type)
            {
                return type.FullName;
            }
            else if (objectToDump is Delegate _delegate)
            {
                return _delegate.ToString();
            }
            else if (objectToDump is Color color)
            {
                JObject OBJ = new JObject
                {
                    { "r", color.r },
                    { "g", color.g },
                    { "b", color.b }
                };
                return OBJ;
            }
            else if (objectToDump is Font font)
            {
                return new JObject {
                    { "InstanceId", font.GetInstanceID() },
                    { "FontName", font.name }
                };
            }
            else if (objectToDump is Mesh mesh)
            {
                return new JObject {
                    { "InstanceId", mesh.GetInstanceID() },
                    { "MeshName", mesh.name }
                };
            }
            else if (objectToDump is TextGenerator textGenerator)
            {
                return "<TEXTGENERATOR>";
            }
            else if (objectToDump is WorldTile worldTile)
            {
                return $"<WorldTile ({worldTile.Coord.x},{worldTile.Coord.y})>";
            }
            else if (objectToDump is TileManager.TileCache tileCache)
            {
                return "<TILE_CACHE>";
            }
            else if (typeof(IEvent).IsAssignableFrom(objectType))
            {
                if (objectToDump is EventNoParams eventNoParams)
                {
                    return "{EventNoParams}";
                }
                else if (objectType.IsGenericType)
                {
                    Type[] types = objectType.GetGenericArguments();
                    return $"{{Event<{string.Join(",", types.Select((Type paramType) => paramType.Name))}>}}";
                }
                else
                {
                    return $"{{UNKNOWN EVENT: {objectType.FullName}}}";
                }
            }
            else if (objectToDump is Delegate delegateInstance) {
                MethodInfo delegateMethod = delegateInstance.GetMethodInfo();
                return $"{{Delegate ({string.Join(",", delegateMethod.GetParameters().Select((ParameterInfo param) => param.ParameterType))}) => {delegateMethod.ReturnType}}}";
            }
            /* else if (objectToDump is Visible visible)
            {
            } */
            /* else if (objectToDump is Sprite sprite)
            {
                return sprite.name;
            } */
            else
            {
                try
                {
                    bool isUnityObject = objectToDump is UnityEngine.Object;
                    if (isUnityObject)
                    {
                        UnityEngine.Object unityObject = objectToDump as UnityEngine.Object;
                        if (UnityObjectMap.TryGetValue(unityObject, out UnityObjectRef unityRef))
                        {
                            JObject jObj = new JObject
                            {
                                { "InstanceId", unityRef.unityObject.GetInstanceID() },
                                { "Path", unityRef.GetReferenceString() }
                            };
                            if (showTypeInformation)
                            {
                                jObj.Add("<AGOD>__ActualType", objectType.FullName);
                            }
                            return jObj;
                        }
                        else
                        {
                            unityRef = new UnityObjectRef {
                                type = unityObject.GetType(),
                                unityObject = unityObject,
                                path = currentPath
                            };
                            UnityObjectMap.Add(unityObject, unityRef);
                        }
                    }

                    Type[] interfaces = objectType.GetInterfaces();
                    bool isGenericCollection = interfaces.Any(i => i.IsGenericType && typeof(IEnumerable<>).IsAssignableFrom(i.GetGenericTypeDefinition()));
                    bool isGenericDictionary = interfaces.Any(i => i.IsGenericType && typeof(IDictionary<,>).IsAssignableFrom(i.GetGenericTypeDefinition()));
                    bool isCollection = interfaces.Any(i => i is IEnumerable);
                    bool isDictionary = interfaces.Any(i => i is IDictionary);
                    bool isArray = objectType.IsArray;

                    if (isGenericDictionary)
                    {
                        logger.Trace("Property is a generic dictionary");
                        return DumpArbitraryDictionary(objectToDump, currentPath);
                    }
                    else if (isGenericCollection)
                    {
                        logger.Trace("Property is a generic collection");
                        return DumpArbitraryCollection(objectToDump, currentPath);
                    }
                    else if (isDictionary)
                    {
                        logger.Trace("Property is a dictionary");
                        JObject obj = new JObject();
                        IDictionary dict = objectToDump as IDictionary;
   
                        object[] keys = new object[dict.Count];
                        int i = 0;
                        foreach (object key in dict.Keys)
                        {
                            keys[i] = key;
                            i++;
                        }
                        object[] values = new object[keys.Length];
                        for (i = 0; i < keys.Length; i++)
                        {
                            values[i] = dict[keys[i]];
                        }
                        return DumpArbitraryDictionary_Internal(obj, keys, values, currentPath);
                    }
                    else if (isCollection)
                    {
                        logger.Trace("Property is a collection");
                        JArray array = new JArray();
                        ICollection collection = objectToDump as ICollection;
                        object[] objects = new object[collection.Count];
                        int i = 0;
                        foreach (object obj in collection)
                        {
                            objects[i] = obj;
                            array.Add(null);
                            i++;
                        }
                        return DumpArbitraryCollection_Internal(array, objects, currentPath);
                    }
                    else
                    {
                        logger.Trace($"TRYING TO DUMP OBJECT OF TYPE {objectType}");
                        bool relevant = objectType.Assembly.FullName.Contains("Assembly-CSharp");
                        BindingFlags correctFlags = objectToDump is UnityEngine.Object || !relevant ? PublicInstanceFlags : InstanceFlags;
                        if (PublicOnly.Contains(objectType))
                        {
                            correctFlags = PublicInstanceFlags;
                        }

                        JObject classObj = new JObject();
                        if (showTypeInformation)
                        {
                            classObj.Add("<AGOD>__ActualType", objectType.FullName);
                        }
                        if (relevant || objectToDump is UnityEngine.Object)
                        {
                            FieldInfo[] fields = objectType.GetFields(correctFlags);
                            foreach (FieldInfo field in fields)
                            {
                                string typeDetail = showTypeInformation ? $"({field.FieldType}) " : "";
                                string fieldName = $"{typeDetail}{field.Name}";
                                try
                                {
                                    object value = field.GetValue(objectToDump);
                                    JToken outputToken = field.FieldType != objectType ? DumpArbitraryObject_Internal(classObj, fieldName, $"{currentPath}->{field.Name}", value) : value?.ToString();
                                    classObj.Add(fieldName, outputToken);
                                }
                                catch (Exception e)
                                {
                                    logger.Error($"FAILED to write field {field.Name}");
                                    logger.Error(e);
                                    classObj.Add(jPropertyName, "<ERROR>");
                                }
                            }
                            PropertyInfo[] properties = objectType.GetProperties(correctFlags);
                            foreach (PropertyInfo property in properties)
                            {
                                MethodInfo getMethod = property.GetGetMethod(true);
                                MethodInfo setMethod = property.GetSetMethod(true);
                                bool isReadonly = setMethod == null;
                                try
                                {
                                    if (getMethod == null)
                                    {
                                        logger.Trace($"DETECTED WRITE-ONLY PROPERTY");
                                    }
                                    else
                                    {
                                        object value = getMethod.Invoke(objectToDump, null);
                                        string typeDetail = showTypeInformation ? $"({property.PropertyType}) " : "";
                                        string propertyName = $"{typeDetail}{property.Name}" + (isReadonly ? " (readonly)" : "");
                                        JToken outputToken = property.PropertyType != objectType ? DumpArbitraryObject_Internal(classObj, propertyName, $"{currentPath}=>{property.Name}", value) : value?.ToString();
                                        classObj.Add(propertyName, outputToken);
                                    }
                                }
                                catch (Exception e)
                                {
                                    logger.Error($"FAILED to write property {property.Name}");
                                    logger.Error(e);
                                    classObj.Add(jPropertyName, "<ERROR>");
                                }
                            }
                            if (objectToDump is UnityEngine.Object unityObject)
                            {
                                classObj.Add("InstanceId", unityObject.GetInstanceID());
                                if (unityObject is Material material)
                                {
                                    string[] propertyNames = material.GetTexturePropertyNames();
                                    classObj.Add("ShaderProperties", new JArray(propertyNames));
                                    DumpMaterialProperties(classObj, material, propertyNames, currentPath);
                                }
                            }
                        }
                        else
                        {
                            try
                            {
                                classObj.Add("toString", objectToDump.ToString());
                            }
                            catch (Exception e)
                            {
                                classObj.Add("toString ERROR", e.ToString());
                            }
                        }
                        logger.Trace("OBJECT DUMP COMPLETE");
                        return classObj;
                    }
                }
                catch (Exception e)
                {
                    logger.Error($"ERROR while dumping output");
                    logger.Error(e);
                    return "<ERROR>";
                }
            }
        }

        private static bool ContainsIgnoreCase(string source, string search)
        {
            int sourceInd = 0;

            int sourceLength = source.Length;
            int searchLength = search.Length;

            while (sourceLength >= searchLength)
            {
                bool failed = false;
                for (int i = 0; i < searchLength; i++)
                {
                    if (Char.ToUpper(source[sourceInd + i]) != Char.ToUpper(search[i])) {
                        failed = true;
                        break;
                    }
                }
                if (!failed)
                {
                    return true;
                }

                sourceLength--;
                sourceInd++;
            }

            return false;
        }

        private void DumpMaterialProperties(JObject jObject, Material material, string[] propertyNames, string currentPath)
        {
            JObject shaderProps = new JObject();
            foreach (string property in propertyNames)
            {
                logger.Trace("Testing property " + property);
                try
                {
                    JToken output = "<UNKNOWN>";
                    if (ContainsIgnoreCase(property, "color") || ContainsIgnoreCase(property, "colour"))
                    {
                        if (ContainsIgnoreCase(property, "arr"))
                        {
                            logger.Trace("Trying to fetch color array");
                            Color[] colors = material.GetColorArray(property);
                            output = DumpArbitraryObject_Internal(shaderProps, property, $"{currentPath}.{property}", colors);
                        }
                        else
                        {
                            logger.Trace("Trying to fetch color");
                            Color color = material.GetColor(property);
                            output = DumpArbitraryObject_Internal(shaderProps, property, $"{currentPath}.{property}", color);
                        }
                    }
                    else if (ContainsIgnoreCase(property, "float"))
                    {
                        if (ContainsIgnoreCase(property, "arr"))
                        {
                            logger.Trace("Trying to fetch float array");
                            float[] floats = material.GetFloatArray(property);
                            output = DumpArbitraryObject_Internal(shaderProps, property, $"{currentPath}.{property}", floats);
                        }
                        else
                        {
                            logger.Trace("Trying to fetch float");
                            output = material.GetFloat(property);
                        }
                    }
                    else if (ContainsIgnoreCase(property, "tex"))
                    {
                        if (ContainsIgnoreCase(property, "off"))
                        {
                            logger.Trace("Trying to fetch texture offset");
                            Vector2 obj = material.GetTextureOffset(property);
                            output = DumpArbitraryObject_Internal(shaderProps, property, $"{currentPath}.{property}", obj);
                        }
                        else if (ContainsIgnoreCase(property, "scal"))
                        {
                            logger.Trace("Trying to fetch texture scale");
                            Vector2 obj = material.GetTextureScale(property);
                            output = DumpArbitraryObject_Internal(shaderProps, property, $"{currentPath}.{property}", obj);
                        }
                        else
                        {
                            logger.Trace("Trying to fetch texture");
                            Texture tex = material.GetTexture(property);
                            output = DumpArbitraryObject_Internal(shaderProps, property, $"{currentPath}.{property}", tex);
                        }
                    }
                    else if (ContainsIgnoreCase(property, "vector"))
                    {
                        if (ContainsIgnoreCase(property, "arr"))
                        {
                            logger.Trace("Trying to fetch vector4 array");
                            Vector4[] obj = material.GetVectorArray(property);
                            output = DumpArbitraryObject_Internal(shaderProps, property, $"{currentPath}.{property}", obj);
                        }
                        else
                        {
                            logger.Trace("Trying to fetch vector4");
                            Vector4 obj = material.GetVector(property);
                            output = DumpArbitraryObject_Internal(shaderProps, property, $"{currentPath}.{property}", obj);
                        }
                    }
                    else if (ContainsIgnoreCase(property, "matrix"))
                    {
                        if (ContainsIgnoreCase(property, "arr"))
                        {
                            logger.Trace("Trying to fetch matrix array");
                            Matrix4x4[] obj = material.GetMatrixArray(property);
                            output = DumpArbitraryObject_Internal(shaderProps, property, $"{currentPath}.{property}", obj);
                        }
                        else
                        {
                            logger.Trace("Trying to fetch matrix");
                            Matrix4x4 obj = material.GetMatrix(property);
                            output = DumpArbitraryObject_Internal(shaderProps, property, $"{currentPath}.{property}", obj);
                        }
                    }
                    else if (ContainsIgnoreCase(property, "int"))
                    {
                        logger.Trace("Trying to fetch int");
                        output = material.GetInt(property);
                    }
                    else
                    {
                        logger.Trace("UNKNOWN TYPE");
                    }
                    shaderProps.Add(property, output);
                }
                catch (Exception e)
                {
                    logger.Error(e);
                }
            }
            jObject.Add("ShaderPropertiesValues", shaderProps);
        }

        public bool DumpCachedProperties = true;

        private void DumpTankBlock(JObject tankBlockObj, TankBlock tankBlock, string currentPath)
        {
            tankBlockObj.Add("m_DefaultMass", tankBlock.m_DefaultMass);
            tankBlockObj.Add("m_Tier", tankBlock.m_Tier);
            tankBlockObj.Add("m_BlockCategory", (int)tankBlock.m_BlockCategory);
            tankBlockObj.Add("m_BlockRarity", (int)tankBlock.m_BlockRarity);
            tankBlockObj.Add("m_BlockLinkAudioType", (int)tankBlock.m_BlockLinkAudioType);
            tankBlockObj.Add("attachPoints", DumpArbitraryObject_Internal(tankBlockObj, "attachPoints", $"{currentPath}->attachPoints", tankBlock.filledCells));
            tankBlockObj.Add("filledCells", DumpArbitraryObject_Internal(tankBlockObj, "filledCells", $"{currentPath}->filledCells", tankBlock.filledCells));
            tankBlockObj.Add("CentreOfMass", DumpArbitraryObject_Internal(tankBlockObj, "CentreOfMass", $"{currentPath}=>CentreOfMass", tankBlock.CentreOfMass));
            tankBlockObj.Add("CentreOfGravity", DumpArbitraryObject_Internal(tankBlockObj, "CentreOfGravity", $"{currentPath}=>CentreOfGravity", tankBlock.CentreOfGravity));
            if (tankBlock.trans != null)
            {
                tankBlockObj.Add("centreOfMassWorld", DumpArbitraryObject_Internal(tankBlockObj, "centreOfMassWorld", $"{currentPath}=>centreOfMassWorld", tankBlock.centreOfMassWorld));
            }
            tankBlockObj.Add("cachedLocalPosition", DumpArbitraryObject_Internal(tankBlockObj, "cachedLocalPositation", $"{currentPath}=>cachedLocalPosition", tankBlock.cachedLocalPosition));
            tankBlockObj.Add("cachedLocalRotation", DumpArbitraryObject_Internal(tankBlockObj, "cachedLocalRotationn", $"{currentPath}=>cachedLocalRotation", tankBlock.cachedLocalRotation));
            tankBlockObj.Add("BlockCellBounds", DumpArbitraryObject_Internal(tankBlockObj, "BlockCellBounds", $"{currentPath}=>BlockCellBounds", tankBlock.BlockCellBounds));
            return;
        }

        private static Type[] PublicOnly = new Type[] { typeof(BlockManager) };

        private void DumpComponent(ComponentRef componentRef)
        {
            Type componentType = componentRef.type;
            logger.Trace($"TRYING TO DUMP COMPONENT OF TYPE {componentType.FullName} on GO {componentRef.component?.gameObject}");
            // don't fully dump TankBlocks
            if (componentRef.component is TankBlock tankBlock)
            {
                DumpTankBlock(componentRef.jObj, tankBlock, componentRef.GetReferenceString());
                return;
            }
            BindingFlags correctFlags = (componentRef.component is Module || componentType.Assembly.FullName.Contains("Assembly-CSharp")) && !PublicOnly.Contains(componentRef.type) ? InstanceFlags : PublicInstanceFlags;
            JObject componentObj = componentRef.jObj;
            FieldInfo[] fields = componentRef.type.GetFields(correctFlags);
            foreach(FieldInfo field in fields)
            {
                string typeDetail = showTypeInformation ? $"({field.FieldType}) " : "";
                string jPropertyName = $"{typeDetail}{field.Name}";
                try
                {
                    if (!DumpCachedProperties && field.Name.Contains("cached"))
                    {
                        logger.Debug("Cached field detected, SKIPPING");
                    }
                    else
                    {
                        object value = field.GetValue(componentRef.component);
                        JToken outputToken = field.FieldType != componentRef.type ?
                            DumpArbitraryObject_Internal(componentRef.jObj, jPropertyName, $"{componentRef.GetReferenceString()}->{field.Name}", value) :
                            value?.ToString();
                        componentObj.Add(jPropertyName, outputToken);
                    }
                }
                catch (Exception e)
                {
                    logger.Error($"FAILED to write field {field.Name}");
                    logger.Error(e);
                    componentObj.Add(jPropertyName, "<ERROR>");
                }
            }
            PropertyInfo[] properties = componentRef.type.GetProperties(correctFlags);
            foreach (PropertyInfo property in properties)
            {
                string typeDetail = showTypeInformation ? $"({property.PropertyType}) " : "";
                MethodInfo getMethod = property.GetGetMethod(true);
                MethodInfo setMethod = property.GetSetMethod(true);
                bool isReadonly = setMethod == null;
                string jPropertyName = $"{typeDetail}{property.Name}" + (isReadonly ? " (readonly)" : "");
                try
                {
                    if (getMethod == null)
                    {
                        logger.Trace($"DETECTED WRITE-ONLY PROPERTY");
                    }
                    else if (!DumpCachedProperties && property.Name.Contains("cached"))
                    {
                        logger.Debug("Cached property detected, SKIPPING");
                    }
                    else
                    {
                        object value = getMethod.Invoke(componentRef.component, null);
                        JToken outputToken = property.PropertyType != componentRef.type ?
                            DumpArbitraryObject_Internal(componentRef.jObj, jPropertyName, $"{componentRef.GetReferenceString()}=>{property.Name}", value) :
                            value?.ToString();
                        componentObj.Add(jPropertyName, outputToken);
                    }
                }
                catch (Exception e)
                {
                    logger.Error($"FAILED to write property {property.Name}");
                    logger.Error(e);
                    componentObj.Add(jPropertyName, "<ERROR>");
                }
            }
            logger.Trace($"COMPONENT DUMP COMPLETE FOR {componentType.FullName} on GO {componentRef.component?.gameObject}");
        }

        private void SetTargetRef(MissingTargetRef targetRef, string targetValue)
        {
            if (targetRef.jProperty is null)
            {
                // is array
                JArray array = (JArray) targetRef.source;
                array[targetRef.index] = targetValue;
            }
            else
            {
                JObject jObject = (JObject) targetRef.source;
                jObject[targetRef.jProperty] = targetValue;
            }
        }

        private void SetupComponentStructure()
        {
            foreach (GameObject gameObject in gameObjects)
            {
                GameObjectRef objectRef = GameObjectMap[gameObject];

                objectRef.jObj.Add("UnityEngine.Transform", DumpTransform(objectRef));

                Component[] objectComponents = gameObject.GetComponents<Component>().Where(c => !(c is Transform)).ToArray();

                Dictionary<string, int> nameCount = new Dictionary<string, int>();
                foreach (Component component in objectComponents)
                {
                    string name = component.GetType().FullName;
                    if (nameCount.TryGetValue(name, out int count))
                    {
                        nameCount[name] = count + 1;
                    }
                    else
                    {
                        nameCount.Add(name, 1);
                    }
                }

                Dictionary<string, int> currIndCount = new Dictionary<string, int>();
                foreach (Component component in objectComponents)
                {
                    components.Add(component);
                    string name = component.GetType().FullName;
                    int totalCount = nameCount[name];
                    int index = -1;
                    if (totalCount > 1)
                    {
                        if (currIndCount.TryGetValue(name, out int currIndex))
                        {
                            index = currIndex;
                            currIndCount[name] = currIndex + 1;
                        }
                        else
                        {
                            index = 0;
                            currIndCount.Add(name, 1);
                        }
                    }

                    ComponentRef componentRef = GetComponentRef(component, objectRef, index);
                    objectRef.jObj.Add(componentRef.ToString(), componentRef.jObj);
                }
            }
        }

        private void DumpComponentDetails()
        {
            foreach (Component component in components)
            {
                ComponentRef componentRef = ComponentMap[component];
                DumpComponent(componentRef);
            }
        }

        public bool DumpExternal = false;

        public string Dump()
        {
            if (output == null)
            {
                // Go through gameObjects first
                GameObjectRef rootRef = new GameObjectRef
                {
                    gameObject = this.root,
                    path = "ROOT:",
                    index = -1,
                    jObj = new JObject()
                };
                GameObjectMap.Add(this.root, rootRef);
                DumpGameObjectRecursive(rootRef, "ROOT:", 0);
                JObject actualOutput = new JObject();
                actualOutput.Add($"ROOT|{this.root.name}", rootRef.jObj);
                output = actualOutput;

                // Setup the component structure
                SetupComponentStructure();
                gameObjects.Clear();

                // Dump components, determine islands
                DumpComponentDetails();
                components.Clear();

                if (DumpExternal)
                {
                    // Fillup graph
                    JObject external = new JObject();
                    int i = 0;
                    HashSet<GameObject> missingObjects = new HashSet<GameObject>(missingGOs);
                    missingGOs.Clear();

                    string playerTankPropName = null;
                    GameObjectRef playerTankRef;
                    foreach (GameObject island in missingObjects)
                    {
                        GameObjectRef islandRef = new GameObjectRef
                        {
                            gameObject = island,
                            path = "EXT:",
                            index = -1,
                            jObj = new JObject()
                        };
                        GameObjectMap.Add(island, islandRef);

                        string islandName = $"External_GameObject_{i}|{island.name}";
                        string path = "EXT:";
                        if (island.GetComponent<Tank>() == Singleton.playerTank)
                        {
                            playerTankRef = islandRef;
                            playerTankPropName = islandName;
                            path = "EXT (PLAYER TANK):";
                        }

                        DumpGameObjectRecursive(islandRef, path, 0);
                        external.Add(islandName, islandRef.jObj);
                        i++;
                    }
                    actualOutput.Add("EXTERNAL", external);

                    // Dump the rest of the stuff
                    // Setup the component structure
                    SetupComponentStructure();
                    gameObjects.Clear();

                    // Dump components, determine islands
                    DumpComponentDetails();
                    components.Clear();

                    // Remove tank from externaloutput, but keep references to it (with modified name)
                    if (playerTankPropName != null)
                    {
                        external.Remove(playerTankPropName);
                    }

                    // Fixup references
                    foreach (MissingTargetRef missingTarget in externalRefs)
                    {
                        JToken source = missingTarget.source;
                        object target = missingTarget.missingTarget;

                        string targetValue = "<MISSING>";
                        if (target is GameObject missingObject)
                        {
                            if (GameObjectMap.TryGetValue(missingObject, out GameObjectRef goRef))
                            {
                                targetValue = goRef.GetReferenceString();
                            }
                        }
                        else if (target is Transform missingTransform)
                        {
                            if (GameObjectMap.TryGetValue(missingTransform.gameObject, out GameObjectRef goRef))
                            {
                                targetValue = goRef.GetTransformString();
                            }
                        }
                        else if (target is Component missingComponent)
                        {
                            if (ComponentMap.TryGetValue(missingComponent, out ComponentRef componentRef))
                            {
                                targetValue = componentRef.GetReferenceString();
                            }
                        }
                        SetTargetRef(missingTarget, targetValue);
                    }
                }
                Cleanup();
            }
            return JsonConvert.SerializeObject(output, Formatting.Indented);
        }
        private void Cleanup()
        {
            UnityObjectMap.Clear();
            ComponentMap.Clear();
            GameObjectMap.Clear();
            externalRefs.Clear();
            missingGOs.Clear();
            components.Clear();
            gameObjects.Clear();

            UnityObjectMap = null;
            ComponentMap = null;
            GameObjectMap = null;
            externalRefs = null;
            missingGOs = null;
            components = null;
            gameObjects = null;
            root = null;
        }
    }
}
