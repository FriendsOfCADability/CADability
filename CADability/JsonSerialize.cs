using CADability.GeoObject;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace CADability
{
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
    public class JsonVersion : System.Attribute
    {
        public bool serializeAsStruct = false;
        public int version = 0;

        public JsonVersion()
        {
        }
        public JsonVersion(int version)
        {
            this.version = version;
            this.serializeAsStruct = false;
        }
        public JsonVersion(int version, bool serializeAsStruct)
        {
            this.version = version;
            this.serializeAsStruct = serializeAsStruct;
        }
    }
    public interface IJsonVersion
    {
        string GetAssemblyName();
        int GetTypeVersion(Type type);
    }
    public interface IJsonSerialize
    {
        void GetObjectData(IJsonWriteData data);
        void SetObjectData(IJsonReadData data);
    }
    /// <summary>
    /// Implement this interface if your object needs a callback after the first pass of the serialisation/deserialisation.
    /// To ensure this interface of your object is called, you must call <see cref="JsonSerialize.RegisterForSerializationDoneCallback(IJsonSerializeDone)"/>,
    /// in the implementation of <see cref="IJsonSerialize.GetObjectData(IJsonWriteData)"/> or <see cref="IJsonSerialize.SetObjectData(IJsonWriteData)"/>.
    /// When the object is beeing read, the properties might be empty objects in the first pass. When <see cref="SerializationDone(JsonSerialize)"/> is called
    /// all properties had their first pass, i.e. <see cref="IJsonSerialize.SetObjectData(IJsonReadData)"/> has been called on those objects.
    /// When the object is beeing written, this mechanism provides the opportunity to modify the object in <see cref="IJsonSerialize.GetObjectData(IJsonWriteData)"/> and
    /// restore it in <see cref="SerializationDone(JsonSerialize)"/>
    /// </summary>
    public interface IJsonSerializeDone
    {
        /// <summary>
        /// Will be called, when all objects are written or read.
        /// </summary>
        /// <param name="jsonSerialize"></param>
        void SerializationDone(JsonSerialize jsonSerialize);
    }
    public interface IJsonReadData
    {
        bool HasProperty(string name);
        object GetProperty(string name);
        string GetStringProperty(string name);
        int GetIntProperty(string name);
        double GetDoubleProperty(string name); // and more to come
        object GetProperty(string name, Type type);
        T GetProperty<T>(string name);
        bool TryGetProperty<T>(string name, out T val) where T : class;
        T GetPropertyOrDefault<T>(string name);
        int Version { get; }
        void RegisterForSerializationDoneCallback(IJsonSerializeDone toCall);
        Dictionary<string, object>.Enumerator GetEnumerator();
    }
    public interface IJsonReadStruct
    {
        T GetValue<T>() where T : struct;
    }
    public interface IJsonWriteData
    {
        void AddProperty(string name, object value);
        void AddValue(object value);
        void AddValues(params object[] value);
        void AddHashTable(string v, Hashtable attributeLists);
        void RegisterForSerializationDoneCallback(IJsonSerializeDone toCall);
    }
    public class JsonSerialize : IJsonWriteData
    {
        Dictionary<string, Assembly> loadedAssemblies = null;
        public delegate Type ResolveTypeDelegate(string typeName, string assemblyName);
        /// <summary>
        /// If a type cannot be resolved (maybe you changed the class name) on deserialization, you can return a type here.
        /// Return null, if you don't handle this type name (but another event consumer does)
        /// </summary>
        public static event ResolveTypeDelegate ResolveType;
        /// <summary>
        /// Force the StreamWriter to write numbers with InvariantInfo
        /// </summary>
        class FormattingStreamWriter : StreamWriter
        {
            public FormattingStreamWriter(Stream stream)
                : base(stream)
            {
            }
            public override IFormatProvider FormatProvider
            {
                get
                {
                    return NumberFormatInfo.InvariantInfo;
                }
            }
        }

        Tokenizer tk;
        /// <summary>
        /// Read JSON files token by token
        /// </summary>
        class Tokenizer : IDisposable
        {
            public enum etoken { delimited, beginObject, endObject, beginArray, endArray, nnull, number, comma, ttrue, ffalse, colon, eof, error }
            StreamReader sr;
            string currentline;
            int actind;
            public Tokenizer(Stream stream)
            {
                sr = new StreamReader(stream);
                currentline = sr.ReadLine().Trim();
                actind = 0;
            }

            public bool EndOfFile
            {
                get
                {
                    return sr.EndOfStream;
                }
            }

            public etoken NextToken(out string line, out int start, out int length)
            {
                line = null;
                start = length = 0;
                while (actind >= currentline.Length)
                {
                    if (sr.EndOfStream) return etoken.eof;
                    currentline = sr.ReadLine().Trim();
                    actind = 0;
                }
                while (char.IsWhiteSpace(currentline[actind]))
                {   // skip whitespace
                    ++actind;
                    if (actind >= currentline.Length)
                    {
                        if (sr.EndOfStream) return etoken.eof;
                        currentline = sr.ReadLine().Trim();
                        actind = 0;
                    }
                }
                switch (currentline[actind])
                {
                    case '"':
                        {   // return string in quotes
                            int ind = currentline.IndexOf('"', actind + 1);
                            while (ind >= 0 && ind < currentline.Length - 1 && currentline[ind - 1] == '\\') ind = currentline.IndexOf('"', ind + 1); // skip \" in a string
                            if (ind < 0)
                            {   // string spans multiple lines
                                string res = currentline.Substring(actind);
                                while (ind < 0)
                                {
                                    if (sr.EndOfStream) return etoken.eof;
                                    currentline = sr.ReadLine().Trim();
                                    ind = currentline.IndexOf('\'');
                                    while (ind >= 0 && ind < currentline.Length - 1 && currentline[ind - 1] == '\\') ind = currentline.IndexOf('"', ind + 1); // skip \" in a string
                                    if (ind >= 0)
                                    {
                                        res += currentline.Substring(0, ind + 1);
                                        actind = ind + 1;
                                        line = res;
                                        start = 1;
                                        length = res.Length - 2;
                                        return etoken.delimited;
                                    }
                                    else res += currentline;
                                }
                            }
                            start = actind + 1;
                            length = ind - start + 1 - 1;
                            line = currentline;
                            actind = ind + 1;
                            return etoken.delimited;
                        }
                    case '.':
                    case '-':
                    case '+':
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        {   // return a number
                            start = actind;
                            ++actind;
                            while (actind < currentline.Length && (char.IsDigit(currentline[actind]) || currentline[actind] == 'E' || currentline[actind] == 'e'
                                || currentline[actind] == '+' || currentline[actind] == '-' || currentline[actind] == '.')) ++actind;
                            length = actind - start;
                            line = currentline;
                            return etoken.number;
                        }
                    case 'n':
                        if (currentline.Substring(actind, 4) == "null")
                        {
                            start = actind;
                            actind += 4;
                            length = 4;
                            line = currentline;
                            return etoken.nnull;
                        }
                        break;
                    case 't':
                        if (currentline.Substring(actind, 4) == "true")
                        {
                            start = actind;
                            actind += 4;
                            length = 4;
                            line = currentline;
                            return etoken.ttrue;
                        }
                        break;
                    case 'f':
                        if (currentline.Substring(actind, 5) == "false")
                        {
                            start = actind;
                            actind += 5;
                            length = 5;
                            line = currentline;
                            return etoken.ffalse;
                        }
                        break;
                    case '[':
                        start = actind;
                        ++actind;
                        length = 1;
                        line = currentline;
                        return etoken.beginArray;
                    case ']':
                        start = actind;
                        ++actind;
                        length = 1;
                        line = currentline;
                        return etoken.endArray;
                    case '{':
                        start = actind;
                        ++actind;
                        length = 1;
                        line = currentline;
                        return etoken.beginObject;
                    case '}':
                        start = actind;
                        ++actind;
                        length = 1;
                        line = currentline;
                        return etoken.endObject;
                    case ',':
                        start = actind;
                        ++actind;
                        length = 1;
                        line = currentline;
                        return etoken.comma;
                    case ':':
                        start = actind;
                        ++actind;
                        length = 1;
                        line = currentline;
                        return etoken.colon;
                }
                return etoken.error;
            }

            #region IDisposable Support
            private bool disposedValue = false; // To detect redundant calls

            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {
                        if (sr != null) sr.Dispose();
                    }
                    disposedValue = true;
                }
            }

            // This code added to correctly implement the disposable pattern.
            void IDisposable.Dispose()
            {
                // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
                Dispose(true);
            }
            #endregion
        }

        /// <summary>
        /// Actually a Json object: a dictionary of string->value pairs, where the value is either a string, boolean, null or List&lt;object&gt; (list of objects).
        /// Numbers are kept as strings, because it depends on their usage, whether they will be parsed as double, int, long, ulong, byte etc.
        /// </summary>
        class JsonDict : Dictionary<string, object>, IJsonReadData
        {
            private JsonSerialize root;
            public JsonDict(JsonSerialize root) : base()
            {
                this.root = root;
            }
            object IJsonReadData.GetProperty(string name)
            {
                return this[name];
            }
            bool IJsonReadData.HasProperty(string name)
            {
                return ContainsKey(name);
            }
            string IJsonReadData.GetStringProperty(string name)
            {
                return this[name] as string;
            }
            int IJsonReadData.GetIntProperty(string name)
            {
                return Convert.ToInt32(this[name]);
            }
            double IJsonReadData.GetDoubleProperty(string name)
            {
                return (double)this[name];
            }
            object IJsonReadData.GetProperty(string name, Type type)
            {
                object val = this[name];
                if (root.typeVersions.TryGetValue(type.FullName, out int tv) && tv == -1) return val; // maybe it was ISerialize in an old version and is now SerializeAsStruct: this would fail
                if (SerializeAsStruct(type))
                {
                    ConstructorInfo cie = type.GetConstructor(BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.DefaultBinder, new Type[] { typeof(IJsonReadStruct) }, null);
                    return cie.Invoke(new object[] { new JsonArray(val as List<object>, root) });
                }
                if ((type.IsPrimitive || type.IsEnum) && val is string)
                {
                    return Parse(val, type);
                }
                if (type.IsPrimitive && val is double) // all numbers are read as double
                {
                    return Convert.ChangeType(val, type);
                }
                if (type.IsArray)
                {
                    ConstructorInfo cie = type.GetConstructor(BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.DefaultBinder, new Type[] { typeof(int) }, null);
                    Type eltp = type.GetElementType();
                    Array sar = cie.Invoke(new object[] { (val as List<object>).Count }) as Array;
                    if (eltp.IsArray)
                    {
                        ConstructorInfo subci = eltp.GetConstructor(BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.DefaultBinder, new Type[] { typeof(int) }, null);
                        List<object> kve = val as List<object>;
                        for (int i = 0; i < kve.Count; i++)
                        {
                            List<object> sublist = kve[i] as List<object>;
                            Array ssar = subci.Invoke(new object[] { sublist.Count }) as Array;
                            for (int j = 0; j < sublist.Count; j++)
                            {
                                ssar.SetValue(sublist[j], j);
                            }
                            sar.SetValue(ssar, i);
                        }
                        return sar;
                    }
                    else if (SerializeAsStruct(eltp))
                    {
                        ConstructorInfo cieltp = eltp.GetConstructor(BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.DefaultBinder, new Type[] { typeof(IJsonReadStruct) }, null);

                        List<object> kve = val as List<object>;
                        for (int i = 0; i < kve.Count; i++)
                        {
                            sar.SetValue(cieltp.Invoke(new object[] { new JsonArray(kve[i] as List<object>, root) }), i);
                        }
                        return sar;
                    }
                    else
                    {
                        List<object> kve = val as List<object>;
                        for (int i = 0; i < kve.Count; i++)
                        {
                            sar.SetValue(kve[i], i);
                        }
                        return sar;
                    }
                }
                else if (type.GetInterface("IList") != null)
                {

                    ConstructorInfo cie = type.GetConstructor(BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.DefaultBinder, new Type[0], null);
                    IList ar = cie.Invoke(new object[0]) as IList;
                    List<object> kve = val as List<object>;
                    for (int i = 0; i < kve.Count; i++)
                    {

                        try { ar.Add(kve[i]); } catch { }; // could fail when kve[i] is a JsonUnknownType
                    }
                    return ar;
                }

                return val;
            }
            T IJsonReadData.GetProperty<T>(string name)
            {
                if (!ContainsKey(name)) return default(T);
                return (T)(this as IJsonReadData).GetProperty(name, typeof(T));
            }
            bool IJsonReadData.TryGetProperty<T>(string name, out T val)
            {
                if (ContainsKey(name))
                {
                    val = (this as IJsonReadData).GetProperty(name, typeof(T)) as T;
                    return true;
                }
                val = default(T);
                return false;
            }
            T IJsonReadData.GetPropertyOrDefault<T>(string name)
            {
                if (ContainsKey(name))
                {
                    object o = (this as IJsonReadData).GetProperty(name, typeof(T));
                    if (o != null && o is T) return (T)o;
                }
                return default(T);
            }
            void IJsonReadData.RegisterForSerializationDoneCallback(IJsonSerializeDone toCall)
            {
                root.RegisterForSerializationDoneCallback(toCall);
            }
            Dictionary<string, object>.Enumerator IJsonReadData.GetEnumerator()
            {
                return this.GetEnumerator();
            }

            public int Version { get; set; }

            int IJsonReadData.Version => Version;

        }

        private void RegisterForSerializationDoneCallback(IJsonSerializeDone toCall)
        {
            SerializationDoneCallback.Add(toCall);
        }

        internal class JsonArray : IJsonReadStruct
        {
            private JsonSerialize root;
            List<object> list;
            int currentIndex;
            public JsonArray(List<object> list, JsonSerialize root)
            {
                this.list = list;
                currentIndex = 0;
                this.root = root;
            }
            T IJsonReadStruct.GetValue<T>()
            {
                if (SerializeAsStruct(typeof(T)) && list[currentIndex] is List<object>)
                {
                    ConstructorInfo ci = typeof(T).GetConstructor(BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.DefaultBinder, new Type[] { typeof(IJsonReadStruct) }, null);
                    return (T)ci.Invoke(new object[] { new JsonArray(list[currentIndex++] as List<object>, root) });
                }
                else return (T)Parse(list[currentIndex++], typeof(T));
            }

        }

        // for serialization:
        Queue<object> queue;
        int objectCount;
        FormattingStreamWriter outStream;
        Dictionary<Type, int> serializedTypes;
        Stack<bool> firstEntry;
        Dictionary<object, int> objectToIndex;
        // for deserialization:
        delegate object EntityCreationDelegate(JsonDict data, List<object> entities, ulong index, HashSet<ulong> underConstruction); // maybe more parameters
        HashSet<IJsonSerializeDone> SerializationDoneCallback = new HashSet<IJsonSerializeDone>();
        List<EntityCreationDelegate> createEntity;
        Dictionary<string, int> typeVersions;
        Dictionary<int, int> typeIndexToVersion;
        Queue<Tuple<IJsonSerialize, JsonDict>> deferred;
        private bool verbose;

        public JsonSerialize()
        {
        }
        private static object Parse(object val, Type tp)
        {
            if (!tp.IsEnum && val is string s) val = double.Parse(s, NumberStyles.Any, NumberFormatInfo.InvariantInfo);
            switch (tp.Name)
            {
                case "Byte":
                    return Convert.ToByte(val);
                case "Int16":
                    return Convert.ToInt16(val);
                case "Int32":
                    return Convert.ToInt32(val);
                case "Int64":
                    return Convert.ToInt64(val);
                case "SByte":
                    return Convert.ToSByte(val);
                case "UInt16":
                    return Convert.ToUInt16(val);
                case "UInt32":
                    return Convert.ToUInt32(val);
                case "UInt64":
                    return Convert.ToUInt64(val);
                case "Double":
                    return Convert.ToDouble(val); // min and maxvalue don't work???
                case "Single":
                    return Convert.ToSingle(val);
                default:
                    if (tp.IsEnum)
                    {
                        string[] names = Enum.GetNames(tp);
                        object[] attrs = tp.GetCustomAttributes(typeof(FlagsAttribute), true);
                        if (attrs.Length > 0)
                        {
                            // a Flags attribut, the val string may be composed of multiple values
                            string[] vals = (val as string).Split(',');
                            int res = 0;
                            for (int i = 0; i < vals.Length; i++)
                            {
                                try
                                {
                                    res |= (int)Enum.Parse(tp, vals[i]);
                                }
                                catch (ArgumentException) { } // enum value probably not existant
                            }
                            return Enum.ToObject(tp, res);
                        }
                        try
                        {
                            return Enum.ToObject(tp, Enum.Parse(tp, val as string));
                        }
                        catch (ArgumentException)
                        {
                            return Enum.ToObject(tp, 0); // enum value probably not existant
                        }
                    }
                    break;
            }
            return val;
        }
        private List<object> GetArray(Tokenizer tk)
        {
            string line;
            int start, length;
            List<object> res = new List<object>();
            Tokenizer.etoken token = tk.NextToken(out line, out start, out length);
            while (token != Tokenizer.etoken.endArray)
            {
                if (token == Tokenizer.etoken.beginObject) res.Add(GetObject(tk));
                else if (token == Tokenizer.etoken.beginArray) res.Add(GetArray(tk));
                else if (token == Tokenizer.etoken.number) res.Add(ParseDouble(line.Substring(start, length)));
                else if (token == Tokenizer.etoken.nnull) res.Add(null);
                else if (token == Tokenizer.etoken.ttrue) res.Add(true);
                else if (token == Tokenizer.etoken.ffalse) res.Add(false);
                else if (token == Tokenizer.etoken.delimited) res.Add(unescape(line, start, length, false));
                else throw new ApplicationException("Syntax error in json file");
                token = tk.NextToken(out line, out start, out length);
                if (token == Tokenizer.etoken.comma) token = tk.NextToken(out line, out start, out length);
            }
            return res;
        }
        private static double ParseDouble(string toParse)
        {
            double d;
            if (!double.TryParse(toParse, NumberStyles.Any, NumberFormatInfo.InvariantInfo, out d))
            {
                if (toParse.Equals(double.MaxValue.ToString(NumberFormatInfo.InvariantInfo))) return double.MaxValue;
                else if (toParse.Equals(double.MinValue.ToString(NumberFormatInfo.InvariantInfo))) return double.MinValue;
                else return double.NaN;
            }
            return d;
        }
        private static float ParseFloat(string toParse)
        {
            float d;
            if (!float.TryParse(toParse, NumberStyles.Any, NumberFormatInfo.InvariantInfo, out d))
            {
                if (toParse.Equals(float.MaxValue)) return float.MaxValue;
                else if (toParse.Equals(float.MinValue)) return float.MinValue;
                else return float.NaN;
            }
            return d;
        }
        private object unescape(string line, int start, int length, bool propName)
        {
            string res = line.Substring(start, length).Replace("\\\"", "\"").Replace("\\n", "\n").Replace("\\r", "\r");
            if (!propName)
            {
                if (res.StartsWith("##")) return res.Substring(1);
                else if (res.StartsWith("#")) return ulong.Parse(res.Substring(1)); // unsigned long is used as reference. All number values are double values
                if (res.StartsWith("$$")) return res.Substring(1);
            }
            return res;
        }
        private string propName(string line, int start, int length)
        {
            return line.Substring(start, length);
        }
        private object GetValue(Tokenizer tk)
        {
            string line;
            int start, length;
            Tokenizer.etoken token = tk.NextToken(out line, out start, out length);
            if (token == Tokenizer.etoken.beginObject) return GetObject(tk);
            else if (token == Tokenizer.etoken.beginArray) return GetArray(tk);
            else if (token == Tokenizer.etoken.number) return double.Parse(line.Substring(start, length), NumberStyles.Any, NumberFormatInfo.InvariantInfo); // only here we know, it is some kind of a number, double may be converted later
            else if (token == Tokenizer.etoken.nnull) return null;
            else if (token == Tokenizer.etoken.ttrue) return true;
            else if (token == Tokenizer.etoken.ffalse) return false;
            else if (token == Tokenizer.etoken.delimited) return unescape(line, start, length, false); // unescape!
            else throw new ApplicationException("Syntax error in json file");
        }
        private JsonDict GetObject(Tokenizer tk)
        {   // tk is position after an "{".  read up to and including matching "}"
            JsonDict res = new JsonDict(this);
            string line;
            int start, length;
            string typename = null;
            string assemblyName = null;
            int typeindex = -1;
            int typeversion = -1;
            Tokenizer.etoken token = tk.NextToken(out line, out start, out length);
            while (token == Tokenizer.etoken.delimited)
            {
                string name = propName(line, start, length);
                token = tk.NextToken(out line, out start, out length);
                if (token != Tokenizer.etoken.colon) throw new ApplicationException("Syntax error in json file");
                res[name] = GetValue(tk);
                if (name == "$TypeIndex") typeindex = Convert.ToInt32(res[name]);
                if (name == "$Type") typename = res[name] as string;
                if (name == "$TypeVersion") typeversion = Convert.ToInt32(res[name]);
                if (name == "$Assembly") assemblyName = res[name] as string;
                token = tk.NextToken(out line, out start, out length);
                if (token == Tokenizer.etoken.comma) token = tk.NextToken(out line, out start, out length);
                else if (token == Tokenizer.etoken.endObject) break;
            }
            if (typeindex >= 0 && typename != null)
            {
                typeVersions[typename] = typeversion;
                typeIndexToVersion[typeindex] = typeversion;
                CreateDeserializer(typename, assemblyName, typeindex);
            }
            if (typeversion == -1 && typeindex >= 0) res.Version = typeIndexToVersion[typeindex];
            else res.Version = typeversion;
            return res;
        }
        public object FromStream(Stream stream)
        {
            tk = new Tokenizer(stream);
            string line;
            int start, length;
            createEntity = new List<EntityCreationDelegate>();
            typeVersions = new Dictionary<string, int>();
            typeIndexToVersion = new Dictionary<int, int>();
            Tokenizer.etoken token = tk.NextToken(out line, out start, out length);
            if (token == Tokenizer.etoken.beginObject)
            {
                JsonDict allObjects = GetObject(tk);
                if (!allObjects.ContainsKey("CADability")) return null;
                JsonDict cdb = allObjects["CADability"] as JsonDict;
                if (cdb != null)
                {
                    string versionstring = cdb["Version"] as string;
                }
                if (!allObjects.ContainsKey("Entities")) return null;
                List<object> entities = allObjects["Entities"] as List<object>;
                if (entities != null)
                {
#if DEBUG
                    for (int i = 0; i < entities.Count; i++)
                    {
                        if (entities[i] is JsonDict jd) jd["§Index"] = i;
                    }
#endif
                    CreateEntities(entities);
                    for (int i = 0; i < entities.Count; i++)
                    {
                        if (entities[i] != null && !(entities[i] is JsonDict) && !(entities[i] is JsonArray) && entities[i] is IDeserializationCallback && typeVersions.TryGetValue(entities[i].GetType().FullName, out int typeVersion))
                            if (typeVersion == -1) (entities[i] as IDeserializationCallback).OnDeserialization(this);
                    }
                    IJsonSerializeDone item = SerializationDoneCallback.FirstOrDefault();
                    while (item != null)
                    {
                        SerializationDoneCallback.Remove(item);
                        item.SerializationDone(this);
                        item = SerializationDoneCallback.FirstOrDefault();
                    }
                    //foreach (IJsonSerializeDone item in SerializationDoneCallback)
                    //{
                    //    item.SerializationDone();
                    //}
                    return entities[0];
                }
            }
            return null;
        }
        public void InvokeSerializationDoneCallback(object item)
        {
            if (item is IJsonSerializeDone done && SerializationDoneCallback.Contains(done))
            {
                SerializationDoneCallback.Remove(done);
                done.SerializationDone(this);
            }
        }
        public bool IsWriting
        {
            get { return outStream!=null; }   
        }
        private SerializationInfo SerializationInfoFromJsonData(JsonDict data, Type tp, List<object> entities, HashSet<ulong> underConstruction)
        {
            SerializationInfo si = new SerializationInfo(tp, new FormatterConverter());
            foreach (string key in data.Keys)
            {
                if (key.StartsWith("$")) continue;
                if (data[key] is ulong)
                {   // this is a reference to an object
                    ulong index = (ulong)data[key];
                    if (entities[(int)index] is JsonDict) CreateEntity(entities, (ulong)data[key], underConstruction);
                    object created = entities[(int)index];
                    si.AddValue(key, created, created.GetType());
                }
                else if (data[key] is JsonDict && (data[key] as JsonDict).Count == 2 && (data[key] as JsonDict).ContainsKey("$Type") && (data[key] as JsonDict).ContainsKey("$Value"))
                {   // this is a typed generated from ISerialize
                    string typename = (data[key] as JsonDict)["$Type"] as string;
                    object val = (data[key] as JsonDict)["$Value"];
                    if (!typename.Contains("."))
                    {   // a primitive type
                        switch (typename)
                        {
                            case "Byte":
                                si.AddValue(key, Byte.Parse(val as string));
                                break;
                            case "Int16":
                                si.AddValue(key, Int16.Parse(val as string));
                                break;
                            case "Int32":
                                si.AddValue(key, Int32.Parse(val as string));
                                break;
                            case "Int64":
                                si.AddValue(key, Int64.Parse(val as string));
                                break;
                            case "SByte":
                                si.AddValue(key, SByte.Parse(val as string));
                                break;
                            case "UInt16":
                                si.AddValue(key, UInt16.Parse(val as string));
                                break;
                            case "UInt32":
                                si.AddValue(key, UInt32.Parse(val as string));
                                break;
                            case "UInt64":
                                si.AddValue(key, UInt64.Parse(val as string));
                                break;
                            case "Double":
                                si.AddValue(key, ParseDouble(val as string));
                                break;
                            case "Single":
                                si.AddValue(key, ParseFloat(val as string));
                                break;
                        }
                    }
                    else
                    {
                        Type tpe = Type.GetType(typename);
                        if (tpe.IsEnum)
                        {
                            string[] names = Enum.GetNames(tpe);
                            bool isEmpty = true;
                            try
                            {
                                si.AddValue(key, Enum.Parse(tpe, val as string));
                                isEmpty = false;
                            }
                            catch { }
                            // was this before, doesn't work when Enum types have non standard numbers
                            //for (int i = 0; i < names.Length; i++)
                            //{
                            //    if (names[i] == (val as string))
                            //    {
                            //        //si.AddValue(key, Enum.ToObject(tpe, i));
                            //        isEmpty = false;
                            //        break;
                            //    }
                            //}
                            if (isEmpty) si.AddValue(key, Enum.ToObject(tpe, int.Parse(val as string)));
                        }
                        else if (tpe.IsArray)
                        {
                            // enumerableName = enumerableName.Substring(0, enumerableName.Length - 2);
                            // create an array of that type, only one-dimensional, two-dimensional and two-dimensional jagged arrays are supported, otherwise we would have to do it recursively
                            if (tpe.GetArrayRank() == 2)
                            {
                                List<object> lval = val as List<object>;
                                ConstructorInfo cie = tpe.GetConstructor(BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.DefaultBinder, new Type[] { typeof(int), typeof(int) }, null);
                                int l1 = lval.Count;
                                int l2 = 0;
                                if (lval.Count > 0) l2 = (lval[0] as List<object>).Count;
                                Array sar = cie.Invoke(new object[] { l1, l2 }) as Array;
                                for (int i = 0; i < lval.Count; i++)
                                {
                                    ConstructorInfo cielt = null;
                                    List<object> ll = lval[i] as List<object>;
                                    if (SerializeAsStruct(tpe.GetElementType()))
                                    {
                                        cielt = tpe.GetElementType().GetConstructor(BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.DefaultBinder, new Type[] { typeof(IJsonReadStruct) }, null);
                                    }
                                    for (int j = 0; j < ll.Count; j++)
                                    {
                                        if (ll[j] is ulong) sar.SetValue(CreateEntity(entities, (ulong)ll[j], underConstruction), i, j);
                                        else if (ll[j] is string && tpe.GetElementType().IsPrimitive) sar.SetValue(Parse(ll[j], tpe.GetElementType()), i, j);
                                        else if (cielt != null) sar.SetValue(cielt.Invoke(new object[] { new JsonArray(ll[j] as List<object>, this) }), i, j);
                                        else sar.SetValue(ll[j], i, j);
                                    }
                                }
                                si.AddValue(key, sar, sar.GetType());
                            }
                            else
                            {
                                ConstructorInfo cie = tpe.GetConstructor(BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.DefaultBinder, new Type[] { typeof(int) }, null);
                                Type eltp = tpe.GetElementType();
                                Array sar = cie.Invoke(new object[] { (val as List<object>).Count }) as Array;
                                if (eltp.IsArray)
                                {
                                    ConstructorInfo subci = eltp.GetConstructor(BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.DefaultBinder, new Type[] { typeof(int) }, null);
                                    ConstructorInfo cielt = null;
                                    if (SerializeAsStruct(eltp.GetElementType()))
                                    {
                                        cielt = eltp.GetElementType().GetConstructor(BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.DefaultBinder, new Type[] { typeof(IJsonReadStruct) }, null);
                                    }
                                    List<object> kve = val as List<object>;
                                    for (int i = 0; i < kve.Count; i++)
                                    {
                                        List<object> sublist = kve[i] as List<object>;
                                        Array ssar = subci.Invoke(new object[] { sublist.Count }) as Array;
                                        for (int j = 0; j < sublist.Count; j++)
                                        {
                                            if (sublist[j] is ulong) ssar.SetValue(CreateEntity(entities, (ulong)sublist[j], underConstruction), j);
                                            else if (sublist[j] is string && eltp.GetElementType().IsPrimitive) sar.SetValue(Parse(sublist[j], eltp.GetElementType()), i);
                                            else if (cielt != null) ssar.SetValue(cielt.Invoke(new object[] { new JsonArray(sublist[j] as List<object>, this) }), j);
                                            else ssar.SetValue(sublist[j], j);
                                        }
                                        sar.SetValue(ssar, i);
                                    }
                                    si.AddValue(key, sar, sar.GetType());
                                }
                                else
                                {
                                    ConstructorInfo cielt = null;
                                    if (SerializeAsStruct(eltp))
                                    {
                                        cielt = eltp.GetConstructor(BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.DefaultBinder, new Type[] { typeof(IJsonReadStruct) }, null);
                                    }
                                    List<object> kve = val as List<object>;
                                    for (int i = 0; i < kve.Count; i++)
                                    {
                                        if (kve[i] is ulong) sar.SetValue(CreateEntity(entities, (ulong)kve[i], underConstruction), i);
                                        else if (kve[i] is string && eltp.IsPrimitive) sar.SetValue(Parse(kve[i], eltp), i);
                                        else if (cielt != null) sar.SetValue(cielt.Invoke(new object[] { new JsonArray(kve[i] as List<object>, this) }), i);
                                        else sar.SetValue(Convert.ChangeType(kve[i], sar.GetType().GetElementType()), i); // das sollte kein double sein
                                    }
                                    si.AddValue(key, sar, sar.GetType());
                                }
                            }
                        }
                        else if (tpe.GetInterface("IList") != null)
                        {

                            ConstructorInfo cie = tpe.GetConstructor(BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.DefaultBinder, new Type[0], null);
                            IList ar = cie.Invoke(new object[0]) as IList;
                            List<object> kve = val as List<object>;
                            for (int i = 0; i < kve.Count; i++)
                            {
                                if (kve[i] is ulong) ar.Add(CreateEntity(entities, (ulong)kve[i], underConstruction));
                                else ar.Add(kve[i]);
                            }
                            si.AddValue(key, ar, ar.GetType());
                        }
                        else if (SerializeAsStruct(tpe))
                        {
                            ConstructorInfo cie = tpe.GetConstructor(BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.DefaultBinder, new Type[] { typeof(IJsonReadStruct) }, null);
                            IJsonSerialize str = cie.Invoke(new object[] { new JsonArray((data[key] as JsonDict)["$Value"] as List<object>, this) }) as IJsonSerialize;
                            si.AddValue(key, str);
                        }
                        else
                        {
                            throw new ApplicationException("unable to deserialize: " + tpe.FullName);
                        }
                    }
                }
                else
                {
                    if (data[key] != null) si.AddValue(key, data[key], data[key].GetType());
                    else si.AddValue(key, null);
                }
            }
            return si;
        }

        internal static bool SerializeAsStruct(Type type)
        {
            object[] cas = type.GetCustomAttributes(typeof(JsonVersion), true);
            if (cas != null && cas.Length > 0) return (cas[0] as JsonVersion).serializeAsStruct;
            return false;
        }
        private static int SerializeVersion(Type type)
        {
            object[] cas = type.GetCustomAttributes(typeof(JsonVersion), true);
            if (cas != null && cas.Length > 0) return (cas[0] as JsonVersion).version;
            return 0;
        }
        private void CreateDeserializer(string typeName, string assemblyName, int ti)
        {
            while (ti >= createEntity.Count) createEntity.Add(null);
            if (createEntity[ti] == null)
            {
                if (loadedAssemblies == null)
                {   // one time initialisation of loaded assemblies dictionary
                    Assembly[] la = AppDomain.CurrentDomain.GetAssemblies();
                    loadedAssemblies = new Dictionary<string, Assembly>();
                    for (int i = 0; i < la.Length; i++) loadedAssemblies[la[i].FullName.Split(',')[0]] = la[i];
                }

                Type tp = null;
                if (assemblyName == null && typeName.StartsWith("System.Drawing.")) assemblyName = "System.Drawing"; // this is for old files, where System.Drawing objects where handled different
                if (assemblyName == null) tp = Type.GetType(typeName);
                if (tp == null)
                {
                    if (loadedAssemblies.TryGetValue(assemblyName, out Assembly assembly)) tp = assembly.GetType(typeName);
                }
                if (tp == null)
                {
                    if (ResolveType != null)
                    {
                        Delegate[] ds = ResolveType.GetInvocationList();
                        foreach (Delegate d in ds)
                        {
                            tp = (d as ResolveTypeDelegate)(typeName, assemblyName);
                            if (tp != null) break;
                        }
                    }
                }
                if (tp == null)
                {
                    tp = typeof(JsonProxyType);
                }
                if (tp != null)
                {
                    if (tp.GetInterface("IJsonSerialize") != null && typeVersions[typeName] != -1) // typeVersions[typeName]==-1 means: it has been serialized via ISerializable
                    {
                        ConstructorInfo ci = tp.GetConstructor(BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.DefaultBinder, new Type[0], null);
                        createEntity[ti] = delegate (JsonDict data, List<object> entities, ulong index, HashSet<ulong> underConstruction)
                        {
                            object created = ci.Invoke(new object[0]);
                            if (created is IJsonConvert cnvt)
                            {
                                // "created" is not the final object, it must be converted first
                                // so we have to construct the whole object and cannot derfer its construction
                                List<string> keys = new List<string>(data.Keys);
                                foreach (string key in keys)
                                {
                                    if (data[key] is ulong) data[key] = CreateEntity(entities, (ulong)data[key], underConstruction);
                                    else if (data[key] is List<object>)
                                    {
                                        List<object> lo = data[key] as List<object>;
                                        for (int i = 0; i < lo.Count; i++)
                                        {
                                            if (lo[i] is ulong) lo[i] = CreateEntity(entities, (ulong)lo[i], underConstruction);
                                            if (lo[i] is List<object>) // only max. twodimensional arrays implemented
                                            {
                                                List<object> llo = lo[i] as List<object>;
                                                for (int j = 0; j < llo.Count; j++)
                                                {
                                                    if (llo[j] is ulong) llo[j] = CreateEntity(entities, (ulong)llo[j], underConstruction);
                                                }
                                            }
                                        }
                                    }
                                }
                                (created as IJsonSerialize).SetObjectData(data);
                                created = cnvt.Convert(); // convert from JsonDictinary to Hashable or similar
                                entities[(int)index] = created;
                                underConstruction.Remove(index);
                                return created;
                            }
                            else
                            {
                                entities[(int)index] = created;
                                underConstruction.Remove(index); // here we allow cycles in the object graph, which is not possible when objects are deserialized with ISerializable interface
                                                                 // we defer the creation of the subentities to avoid stack overflow
                                deferred.Enqueue(new Tuple<IJsonSerialize, JsonDict>(created as IJsonSerialize, data));
                                return created;
                            }
                        };

                    }
                    else if (tp.GetInterface("ISerializable") != null)
                    {
                        ConstructorInfo ci = tp.GetConstructor(BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.DefaultBinder, new Type[] { typeof(SerializationInfo), typeof(StreamingContext) }, null);
                        createEntity[ti] = delegate (JsonDict data, List<object> entities, ulong index, HashSet<ulong> underConstruction)
                        {
                            SerializationInfo si = SerializationInfoFromJsonData(data, tp, entities, underConstruction);
                            try
                            {
                                return ci.Invoke(new object[] { si, new StreamingContext() });
                            }
                            catch { return null; }
                        };
                    }
                }
            }
        }
        private void CreateDeferred(IJsonSerialize obj, JsonDict data, List<object> entities, HashSet<ulong> underConstruction)
        {
            List<string> keys = new List<string>(data.Keys);
            foreach (string key in keys)
            {
                if (data[key] is ulong) data[key] = CreateEntity(entities, (ulong)data[key], underConstruction);
                else if (data[key] is List<object>)
                {
                    List<object> lo = data[key] as List<object>;
                    for (int i = 0; i < lo.Count; i++)
                    {
                        if (lo[i] is ulong) lo[i] = CreateEntity(entities, (ulong)lo[i], underConstruction);
                        if (lo[i] is List<object>) // only max. twodimensional arrays implemented
                        {
                            List<object> llo = lo[i] as List<object>;
                            for (int j = 0; j < llo.Count; j++)
                            {
                                if (llo[j] is ulong) llo[j] = CreateEntity(entities, (ulong)llo[j], underConstruction);
                            }
                        }
                    }
                }
            }
            obj.SetObjectData(data);
        }
        private void CreateEntities(List<object> entities)
        {
            deferred = new Queue<Tuple<IJsonSerialize, JsonDict>>();
            HashSet<ulong> underConstruction = new HashSet<ulong>();
            CreateEntity(entities, 0, underConstruction);
            while (deferred.Count > 0)
            {
                Tuple<IJsonSerialize, JsonDict> deq = deferred.Dequeue();
                CreateDeferred(deq.Item1, deq.Item2, entities, underConstruction);
            }
        }

        private object CreateEntity(List<object> entities, ulong index, HashSet<ulong> underConstruction)
        {
            if (!(entities[(int)index] is JsonDict)) return entities[(int)index];
            JsonDict jd = entities[(int)index] as JsonDict;
            int ti = Convert.ToInt32(jd["$TypeIndex"]);
            if (underConstruction.Contains(index)) throw new ApplicationException("Json stream contains cyclical reference");
            if (createEntity[ti] != null)
            {
                underConstruction.Add(index);
                entities[(int)index] = createEntity[ti](jd, entities, index, underConstruction);
                underConstruction.Remove(index);
            }
            else
            {
                entities[(int)index] = new Dictionary<string, object>(); // better: a class "UnknownObject" with a name and a Dictionary
            }
            return entities[(int)index];
        }

        public bool ToStream(Stream stream, object toSerialize, bool closeStream = true)
        {
            verbose = Settings.GlobalSettings.GetBoolValue("Json.Verbose", false);
#if DEBUG
            verbose = true;
#endif
            outStream = new FormattingStreamWriter(stream);
            firstEntry = new Stack<bool>();
            //using (new PerformanceTick("Json"))
            //{
            BeginObject();
            WriteProperty("CADability");
            BeginObject();
            WriteProperty("URL");
            WriteString("http://www.cadability.de");
            WriteProperty("Version");
            Assembly ass = Assembly.GetExecutingAssembly();
            AssemblyName an = ass.GetName();
            int mv = an.Version.Build;
            WriteString(an.Version.ToString());
            WriteProperty("Assembly");
            WriteString(ass.GetName().Name);
            EndObject();
            queue = new Queue<object>();
            serializedTypes = new Dictionary<Type, int>();
            queue.Enqueue(toSerialize);
            objectToIndex = new Dictionary<object, int>();
            objectCount = 0;
            WriteProperty("Entities");
            BeginArray();
            while (queue.Count > 0)
            {
                if (objectCount > 0)
                {
                    Seperator();
                    // outStream.Write("\n"); // only very small difference in size, but much better readable
                }
                WriteObject(queue.Dequeue());
                ++objectCount;
            }
            EndArray();
            EndObject();
            //}
            outStream.Flush();
            if (closeStream) outStream.Close();
            IJsonSerializeDone item = SerializationDoneCallback.FirstOrDefault();
            while (item != null)
            {
                SerializationDoneCallback.Remove(item);
                item.SerializationDone(this);
                item = SerializationDoneCallback.FirstOrDefault();
            }

            return true;
        }
        private void BeginObject()
        {
            outStream.Write("{");
            firstEntry.Push(true);
        }
        private void EndObject()
        {
            outStream.Write("}");
            firstEntry.Pop();
        }
        private void BeginArray()
        {
            outStream.Write("[");
            firstEntry.Push(true);
        }
        private void EndArray()
        {
            outStream.Write("]");
            firstEntry.Pop();
        }
        private void Seperator()
        {
            outStream.Write(",");
        }
        private void Colon()
        {
            outStream.Write(":");
        }
        private void WriteNull()
        {
            outStream.Write("null");
        }
        private void WriteString(string val, bool escapePound = true)
        {
            if (val == null)
            {
                outStream.Write("null");
            }
            else
            {
                string escaped = val.Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r"); // escape delimiters and \n
                if (escapePound && escaped.StartsWith("#")) escaped = "#" + escaped; // escape starting #
                if (escapePound && escaped.StartsWith("$")) escaped = "$" + escaped; // escape starting $
                outStream.Write("\"" + escaped + "\"");
            }
        }
        private void WriteRefIndex(int index)
        {
            outStream.Write("\"#" + index.ToString() + "\"");
        }
        private void WriteObject(object val)
        {
            BeginObject();

            if (verbose) (this as IJsonWriteData).AddProperty("$Index(Debug)", objectCount);
            if (val is IDictionary ht && !(val is IJsonSerialize))
            {   // a hashtable or some other kind of dictionary is serialized as 
                val = new JSonDictionary(ht, val.GetType());
            }
            if (val is System.Drawing.Color || val is System.Drawing.Printing.PageSettings || val is System.Drawing.Printing.Margins || val is System.Drawing.Printing.PaperSize ||
                val is System.Drawing.Printing.PaperSource || val is System.Drawing.Printing.PrinterResolution || val is System.Drawing.Printing.PrinterSettings)
            {
                val = new JSonSubstitute(val);
            }
            if (val is IList lst && !(val is IJsonSerialize))
            {

            }

            int typeIndex;
            bool firstTimeTypeDefinition = false;
            if (!serializedTypes.TryGetValue(val.GetType(), out typeIndex))
            {   // first time a object of this type is beeing written we write the type name
                typeIndex = serializedTypes.Count + 1;
                serializedTypes[val.GetType()] = typeIndex;
                if (val is JsonProxyType junk)
                {   // if it is an unknown type, we use the typename that was given on deserialisation

                    // the following is not correct: if the order of objects has been changed, there is no original typename in the JsonProxyType
                    // we would need a more global dictionare of proxy types
                    // but this is only an issue, when saving files, where an object could not be deserialised on read
                    (this as IJsonWriteData).AddProperty("$Type", junk.OriginalTypeName);
                    typeIndex = junk.OriginalTypeVersion;
                    string assemblyName = junk.OriginalTypeAssembly;
                    if (assemblyName != null) (this as IJsonWriteData).AddProperty("$Assembly", assemblyName);
                }
                else
                {
                    (this as IJsonWriteData).AddProperty("$Type", val.GetType().FullName);
                    string assemblyName = val.GetType().Assembly.GetName().Name;
                    if (assemblyName != "CADability") (this as IJsonWriteData).AddProperty("$Assembly", assemblyName);
                }
                firstTimeTypeDefinition = true;
            }
            else
            {
                if (verbose) (this as IJsonWriteData).AddProperty("$Type(Debug)", val.GetType().Name);
            }
            (this as IJsonWriteData).AddProperty("$TypeIndex", typeIndex);
            if (val is IJsonSerialize)
            {
                if (firstTimeTypeDefinition)
                {
                    int version = SerializeVersion(val.GetType());
                    (this as IJsonWriteData).AddProperty("$TypeVersion", version);
                }
                (val as IJsonSerialize).GetObjectData(this); // calls one ore more of the AddValue methods
            }
            else if (val is ISerializable)
            {
                if (firstTimeTypeDefinition)
                {
                    (this as IJsonWriteData).AddProperty("$TypeVersion", -1);
                }
                SerializationInfo si = new SerializationInfo(val.GetType(), new FormatterConverter());
                StreamingContext sc = new StreamingContext();
                (val as ISerializable).GetObjectData(si, sc);
                foreach (SerializationEntry se in si)
                {
                    AddTypedProperty(se.Name, se.Value);
                }
            }
            else
            {
                bool serialized = false;
                SerializableAttribute sa = (SerializableAttribute)System.Attribute.GetCustomAttribute(val.GetType(), typeof(SerializableAttribute));
                if (sa != null)
                {
                    ConstructorInfo ci = val.GetType().GetConstructor(BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.DefaultBinder, new Type[] { }, null);
                    if (ci != null)
                    {
                        PropertyInfo[] pi = val.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        for (int i = 0; i < pi.Length; i++)
                        {
                            object[] nsa = pi[i].GetCustomAttributes(typeof(NonSerializedAttribute), true);
                            if ((nsa == null || nsa.Length == 0) && pi[i].CanRead && pi[i].CanWrite)
                            {
                                object pval = pi[i].GetValue(val, new object[0]);
                                (this as IJsonWriteData).AddProperty(pi[i].Name, pval);
                                serialized = true; // at least one property can be read and written
                            }
                        }
                    }
                }
                if (serialized)
                {
                    if (firstTimeTypeDefinition)
                    {
                        (this as IJsonWriteData).AddProperty("$TypeVersion", -1);
                    }
                }
                else
                {
                    throw new ApplicationException("Cannot serialize object" + val.ToString());
                }
            }
            EndObject();
        }

        private void AddTypedProperty(string name, object value)
        {
            WriteProperty(name);
            if (value == null)
            {
                WriteNull();
                return;
            }
            Type type = value.GetType();
            bool serializeAsStruct = SerializeAsStruct(type);
            if (value is bool)
            {
                if ((bool)value) outStream.Write("true");
                else outStream.Write("false");
            }
            else if (value is string)
            {
                WriteString((string)value);
            }
            else if (value.GetType().IsPrimitive)
            {
                BeginObject();
                WriteProperty("$Type");
                WriteString(value.GetType().Name);
                WriteProperty("$Value");
                if (value is double) WriteString(((double)value).ToString(NumberFormatInfo.InvariantInfo));
                else WriteString(value.ToString());
                EndObject();

            }
            else if (value.GetType().IsEnum)
            {
                BeginObject();
                WriteProperty("$Type");
                WriteString(value.GetType().FullName);
                WriteProperty("$Value");
                WriteString(value.ToString());
                EndObject();

            }
            else if (serializeAsStruct)
            {
                BeginObject();
                WriteProperty("$Type");
                WriteString(value.GetType().FullName);
                WriteProperty("$Value");
                (value as IJsonSerialize).GetObjectData(this);
                EndObject();
            }
            //else if (value is Hashtable) // 
            //{
            //    BeginObject();
            //    WriteProperty("$Type");
            //    WriteString(value.GetType().FullName);
            //    WriteProperty("$Value");
            //    BeginArray();
            //    bool first = true;
            //    foreach (DictionaryEntry item in value as Hashtable)
            //    {
            //        if (first) first = false;
            //        else Seperator();
            //        BeginArray();
            //        WriteValue(item.Key);
            //        Seperator();
            //        WriteValue(item.Value);
            //        EndArray();
            //    }
            //    EndArray();
            //    EndObject();
            //}
            else if (value is IJsonSerialize || value is ISerializable)
            {
                int index;
                if (!objectToIndex.TryGetValue(value, out index))
                {
                    queue.Enqueue(value);
                    index = objectCount + queue.Count;
                    objectToIndex[value] = index;
                }
                WriteRefIndex(index);
            }
            else if (value is IEnumerable)
            {
                BeginObject();
                WriteProperty("$Type");
                WriteString(value.GetType().FullName);
                WriteProperty("$Value");
                if (value.GetType().IsArray && value.GetType().GetArrayRank() == 2) // only one- or two-dimensional arrays implemented
                {
                    BeginArray();
                    bool first = true;
                    Array arr = value as Array;
                    for (int i = 0; i < arr.GetLength(0); i++)
                    {
                        if (first) first = false;
                        else Seperator();
                        bool innerfirst = true;
                        BeginArray();
                        for (int j = 0; j < arr.GetLength(1); j++)
                        {
                            if (innerfirst) innerfirst = false;
                            else Seperator();
                            WriteValue(arr.GetValue(i, j));
                        }
                        EndArray();
                    }
                    EndArray();
                }
                else
                {
                    BeginArray();
                    bool first = true;
                    foreach (object sub in value as IEnumerable)
                    {
                        if (first) first = false;
                        else Seperator();
                        WriteValue(sub);
                    }
                    EndArray();
                }
                EndObject();
            }
            else if (System.Attribute.GetCustomAttribute(value.GetType(), typeof(SerializableAttribute)) != null)
            {
                int index;
                if (!objectToIndex.TryGetValue(value, out index))
                {
                    queue.Enqueue(value);
                    index = objectCount + queue.Count;
                    objectToIndex[value] = index;
                }
                WriteRefIndex(index);
            }
            else
            {
                throw new ApplicationException("Cannot serialize value" + value.ToString());
            }
        }

        private void WriteProperty(string name)
        {
            if (firstEntry.Peek())
            {
                firstEntry.Pop();
                firstEntry.Push(false);
            }
            else
            {
                Seperator();
            }
            WriteString(name, false);
            Colon();
        }
        private void WriteValue(object value)
        {
            if (value == null)
            {
                WriteNull();
                return;
            }
            if (value is IDictionary ht && !(value is IJsonSerialize))
            {   // a hashtable or some other kind of dictionary is serialized as 
                value = new JSonDictionary(ht, value.GetType());
            }
            if (value is System.Drawing.Color || value is System.Drawing.Printing.PageSettings || value is System.Drawing.Printing.Margins || value is System.Drawing.Printing.PaperSize ||
                value is System.Drawing.Printing.PaperSource || value is System.Drawing.Printing.PrinterResolution || value is System.Drawing.Printing.PrinterSettings)
            {
                value = new JSonSubstitute(value);
            }

            //if (value is IList lst && !(value is IJsonSerialize) && !value.GetType().IsArray)
            //{
            //    value = new JSonList(lst, value.GetType());
            //}
            Type type = value.GetType();
            bool serializeAsStruct = SerializeAsStruct(type);
            if (type.IsEnum)
            {
                WriteString(value.ToString());
            }
            else if (type.IsPrimitive)
            {
                if (value is bool)
                {
                    if ((bool)value) outStream.Write("true");
                    else outStream.Write("false");
                }
                else
                {
                    outStream.Write(value); // Boolean, (S)Byte, (U)IntNn, Single, Double
                }
            }
            else if (value is string)
            {
                WriteString((string)value);
            }
            else if (serializeAsStruct)
            {
                (value as IJsonSerialize).GetObjectData(this);
            }
            else if (value is IJsonSerialize || value is ISerializable)
            {
                int index;
                if (!objectToIndex.TryGetValue(value, out index))
                {
                    queue.Enqueue(value);
                    index = objectCount + queue.Count;
                    objectToIndex[value] = index;
                }
                WriteRefIndex(index);
            }
            else if (value is IEnumerable)
            {
                BeginArray();
                bool first = true;
                foreach (object sub in value as IEnumerable)
                {
                    if (first) first = false;
                    else Seperator();
                    WriteValue(sub);
                }
                EndArray();
            }
            else if (System.Attribute.GetCustomAttribute(value.GetType(), typeof(SerializableAttribute)) != null)
            {
                int index;
                if (!objectToIndex.TryGetValue(value, out index))
                {
                    queue.Enqueue(value);
                    index = objectCount + queue.Count;
                    objectToIndex[value] = index;
                }
                WriteRefIndex(index);
            }
            else
            {
                throw new ApplicationException("Cannot serialize value" + value.ToString());
            }
        }
#if DEBUG
        public static void DebugRead()
        {
            Stream stream = File.Open(@"C:\Temp\json.json", FileMode.Open);
            JsonSerialize js = new JsonSerialize();
            js.FromStream(stream);
            stream.Close();
        }
        public static void DebugWrite(Project pr)
        {
            Stream stream = File.Open(@"C:\Temp\json.json", FileMode.Create);
            JsonSerialize js = new JsonSerialize();
            js.ToStream(stream, pr);
            stream.Close();
        }
#endif
        public static string ToString(object ser)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                JsonSerialize js = new JsonSerialize();
                js.ToStream(ms, ser, false); // don't close the memory stream
                ms.Seek(0, SeekOrigin.Begin);
                StreamReader sr = new StreamReader(ms);
                string res = sr.ReadToEnd();
                sr.Close();
                return res;
            }
        }
        public static object FromString(string ser)
        {
            using (var stream = new MemoryStream())
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(ser);
                writer.Flush();
                stream.Position = 0;
                JsonSerialize js = new JsonSerialize();
                object res = js.FromStream(stream);
                stream.Close();
                return res;
            }
        }

        #region IJsonWriteData implementation
        void IJsonWriteData.AddProperty(string name, object value)
        {
            WriteProperty(name);
            WriteValue(value);
        }
        void IJsonWriteData.AddValue(object value)
        {
            WriteValue(value);
        }
        void IJsonWriteData.AddValues(params object[] value)
        {
            WriteValue(value);
        }
        void IJsonWriteData.AddHashTable(string name, Hashtable ht)
        {   // key must be a string (and all keys are different)
            WriteProperty(name);
            BeginObject();
            foreach (DictionaryEntry item in ht)
            {
                (this as IJsonWriteData).AddProperty(item.Key.ToString(), item.Value);
            }
            EndObject();
        }
        void IJsonWriteData.RegisterForSerializationDoneCallback(CADability.IJsonSerializeDone toCall)
        {
            SerializationDoneCallback.Add(toCall);
        }
        #endregion
    }

    interface IJsonConvert
    {
        object Convert();
    }
    internal class JSonDictionary : Hashtable, IJsonSerialize, IJsonConvert
    {
        Type originalType;
        public JSonDictionary(IDictionary ht, Type originalType) : base(ht)
        {
            this.originalType = originalType;
        }
        protected JSonDictionary() { }
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("$OriginalType", originalType.FullName);
            // we need to restore the types of keys and values when reading. Maybe they are the same for all items
            // then we create properties "$KeyType" and "$ValueType". 
            Type keyType = null;
            Type valType = null;
            bool firstItem = true;
            bool keyIsPrimitv = false;
            bool valIsPrimitv = false;
            List<string> valTypes = new List<string>();
            foreach (DictionaryEntry item in this)
            {
                Type kt = item.Key.GetType();
                Type vt = item.Value == null ? typeof(object) : item.Value.GetType();
                valTypes.Add(vt.FullName);
                if (kt.IsPrimitive || JsonSerialize.SerializeAsStruct(kt)) keyIsPrimitv = true;
                if (vt != null && (vt.IsPrimitive || JsonSerialize.SerializeAsStruct(kt))) valIsPrimitv = true;
                if (firstItem)
                {
                    keyType = kt; // cannot be null
                    valType = vt;
                    firstItem = false;
                }
                else
                {
                    if (keyType != null && keyType != kt) keyType = null; // no unique key type
                    if (valType == typeof(object) && item.Value != null) valType = vt;
                    if (valType != null && item.Value != null && valType != vt) valType = null; // no unique key type
                }
            }
            List<object[]> asList = new List<object[]>();
            if (keyType == null && !keyIsPrimitv) keyType = typeof(object); // objects are read with the correct type
            else if (keyType == null) throw new ApplicationException("unable to save dictionary with different types for the key");
            if (valType == null && !valIsPrimitv) valType = typeof(object);
            if (keyType != null) data.AddProperty("$KeyType", keyType.ToString());
            if (valType != null) data.AddProperty("$ValueType", valType.ToString());
            else data.AddProperty("$ValueTypes", valTypes.ToArray()); // the types, synchronous to the values
            foreach (DictionaryEntry item in this)
            {
                asList.Add(new object[] { item.Key, item.Value });
            }
            data.AddProperty("$Entries", asList.ToArray());
        }
        public void SetObjectData(IJsonReadData data)
        {
            Type keyType = null, valType = null;
            string[] valTypes = null;
            if (data.HasProperty("$KeyType")) keyType = Type.GetType(data.GetProperty<string>("$KeyType"));
            if (data.HasProperty("$ValueType")) valType = Type.GetType(data.GetProperty<string>("$ValueType"));
            if (data.HasProperty("$ValueTypes")) valTypes = data.GetProperty<string[]>("$ValueTypes");
            List<object> entries = data.GetProperty<List<object>>("$Entries");
            for (int i = 0; i < entries.Count; i++)
            {
                List<object> kv = entries[i] as List<object>;
                object key = kv[0];
                if (key == null) continue;
                Type vt;
                if (valTypes != null) vt = Type.GetType(valTypes[i]);
                else vt = valType;
                object val = kv[1];
                if (!key.GetType().IsSubclassOf(keyType)) key = Convert.ChangeType(key, keyType);
                if (val != null && !val.GetType().IsSubclassOf(vt))
                {
                    if (JsonSerialize.SerializeAsStruct(vt) && val is List<object> lo)
                    {
                        JsonSerialize.JsonArray jsonArray = new JsonSerialize.JsonArray(lo, null);
                        ConstructorInfo cie = vt.GetConstructor(BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.DefaultBinder, new Type[] { typeof(IJsonReadStruct) }, null);
                        val = cie.Invoke(new object[] { jsonArray });
                    }
                    else
                    {
                        val = Convert.ChangeType(val, vt);
                    }
                }
                this[key] = val;
            }
            originalType = Type.GetType(data.GetProperty<string>("$OriginalType"));
        }
        object IJsonConvert.Convert()
        {
            ConstructorInfo construct = originalType.GetConstructor(BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.DefaultBinder, new Type[] { }, null);
            // original typenames contain version specific information: [System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089], which we would like to avoid
            // we could strip that information, but with System.Windows.Forms.Shortcut this doesn't work. Maybe we need special handling of enums
            // Type.GetType("System.Collections.Generic.Dictionary`2[[System.String],[System.Windows.Forms.Shortcut]]"); // doesn't work, probably because System.Windows.Forms.Shortcut is an enum
            // Type.GetType("System.Collections.Generic.Dictionary`2[[System.String],[System.String]]"); // is working

            object res = construct.Invoke(new object[0]);
            IDictionary dict = res as IDictionary;
            foreach (DictionaryEntry item in this)
            {
                dict[item.Key] = item.Value;
            }
            return res;
        }
    }
    internal class JsonProxyType : Hashtable, IJsonSerialize
    {
        Dictionary<string, object> dict;
        protected JsonProxyType()
        {
            dict = new Dictionary<string, object>();
        }
        public void GetObjectData(IJsonWriteData data)
        {
            foreach (KeyValuePair<string, object> item in dict)
            {
                data.AddProperty(item.Key, item.Value);
            }
        }
        public void SetObjectData(IJsonReadData data)
        {
            foreach (KeyValuePair<string, object> item in data)
            {
                dict.Add(item.Key, item.Value);
            }
        }
        public string OriginalTypeName => dict["$Type"] as string;
        public string OriginalTypeAssembly
        {
            get
            {
                if (dict.TryGetValue("$Assembly", out object assembly)) return assembly as string;
                return null;
            }
        }
        public int OriginalTypeVersion => (int)dict["$TypeVersion"];
    }
    internal class JSonSubstitute : IJsonSerialize, IJsonConvert
    {
        object toSerialize;
        public JSonSubstitute(object toSerialize)
        {
            this.toSerialize = toSerialize;
        }

        object IJsonConvert.Convert()
        {
            return toSerialize; // it is already the correct object
        }
        protected JSonSubstitute()
        {

        }
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("$OriginalType", toSerialize.GetType().FullName);
            if (toSerialize is System.Drawing.Color color)
            {
                data.AddProperty("Argb", color.ToArgb());
            }
            else if (toSerialize is System.Drawing.Printing.PageSettings pageSettings)
            {
                data.AddProperty("Color", pageSettings.Color);
                data.AddProperty("Landscape", pageSettings.Landscape);
                data.AddProperty("Margins", pageSettings.Margins);
                data.AddProperty("PaperSize", pageSettings.PaperSize);
                data.AddProperty("PaperSource", pageSettings.PaperSource);
                data.AddProperty("PrinterResolution", pageSettings.PrinterResolution);
                data.AddProperty("PrinterSettings", pageSettings.PrinterSettings);
            }
            else if (toSerialize is System.Drawing.Printing.Margins margins)
            {
                data.AddProperty("Left", margins.Left);
                data.AddProperty("Right", margins.Right);
                data.AddProperty("Top", margins.Top);
                data.AddProperty("Bottom", margins.Bottom);
            }
            else if (toSerialize is System.Drawing.Printing.PaperSize paperSize)
            {
                data.AddProperty("Height", paperSize.Height);
                data.AddProperty("PaperName", paperSize.PaperName);
                data.AddProperty("RawKind", paperSize.RawKind);
                data.AddProperty("Width", paperSize.Width);
            }
            else if (toSerialize is System.Drawing.Printing.PaperSource paperSource)
            {
                data.AddProperty("RawKind", paperSource.RawKind);
                data.AddProperty("SourceName", paperSource.SourceName);
            }
            else if (toSerialize is System.Drawing.Printing.PrinterResolution printerResolution)
            {
                data.AddProperty("Kind", printerResolution.Kind);
                data.AddProperty("X", printerResolution.X);
                data.AddProperty("Y", printerResolution.Y);
            }
            else if (toSerialize is System.Drawing.Printing.PrinterSettings printerSettings)
            {
                data.AddProperty("Collate", printerSettings.Collate);
                data.AddProperty("Copies", printerSettings.Copies);
                data.AddProperty("Duplex", printerSettings.Duplex);
                data.AddProperty("FromPage", printerSettings.FromPage);
                data.AddProperty("MaximumPage", printerSettings.MaximumPage);
                data.AddProperty("MinimumPage", printerSettings.MinimumPage);
                data.AddProperty("PrinterName", printerSettings.PrinterName);
                data.AddProperty("PrintFileName", printerSettings.PrintFileName);
                data.AddProperty("PrintRange", printerSettings.PrintRange);
                data.AddProperty("PrintToFile", printerSettings.PrintToFile);
                data.AddProperty("ToPage", printerSettings.ToPage);
            }
        }

        public void SetObjectData(IJsonReadData data)
        {
            string typeName = data.GetProperty<string>("$OriginalType");
            switch (typeName)
            {
                case "System.Drawing.Color":
                    toSerialize = System.Drawing.Color.FromArgb(data.GetProperty<int>("Argb"));
                    break;
                case "System.Drawing.Printing.PageSettings":
                    System.Drawing.Printing.PageSettings pageSettings = new System.Drawing.Printing.PageSettings();
                    toSerialize = pageSettings;
                    pageSettings.Color = data.GetProperty<bool>("Color");
                    pageSettings.Landscape = data.GetProperty<bool>("Landscape");
                    pageSettings.Margins = data.GetProperty<System.Drawing.Printing.Margins>("Margins");
                    pageSettings.PaperSize = data.GetProperty<System.Drawing.Printing.PaperSize>("PaperSize");
                    pageSettings.PaperSource = data.GetProperty<System.Drawing.Printing.PaperSource>("PaperSource");
                    pageSettings.PrinterResolution = data.GetProperty<System.Drawing.Printing.PrinterResolution>("PrinterResolution");
                    pageSettings.PrinterSettings = data.GetProperty<System.Drawing.Printing.PrinterSettings>("PrinterSettings");
                    break;
                case "System.Drawing.Printing.Margins":
                    System.Drawing.Printing.Margins margins = new System.Drawing.Printing.Margins();
                    toSerialize = margins;
                    margins.Left = data.GetProperty<int>("Left");
                    margins.Right = data.GetProperty<int>("Right");
                    margins.Top = data.GetProperty<int>("Top");
                    margins.Bottom = data.GetProperty<int>("Bottom");
                    break;
                case "System.Drawing.Printing.PaperSize":
                    System.Drawing.Printing.PaperSize paperSize = new System.Drawing.Printing.PaperSize();
                    toSerialize = paperSize;
                    paperSize.Height = data.GetProperty<int>("Height");
                    paperSize.PaperName = data.GetProperty<string>("PaperName");
                    paperSize.RawKind = data.GetProperty<int>("RawKind");
                    paperSize.Width = data.GetProperty<int>("Width");
                    break;
                case "System.Drawing.Printing.PaperSource":
                    System.Drawing.Printing.PaperSource paperSource = new System.Drawing.Printing.PaperSource();
                    toSerialize = paperSource;
                    paperSource.RawKind = data.GetProperty<int>("RawKind");
                    paperSource.SourceName = data.GetProperty<string>("SourceName");
                    break;
                case "System.Drawing.Printing.PrinterResolution":
                    System.Drawing.Printing.PrinterResolution printerResolution = new System.Drawing.Printing.PrinterResolution();
                    toSerialize = printerResolution;
                    printerResolution.Kind = data.GetProperty<System.Drawing.Printing.PrinterResolutionKind>("Kind");
                    printerResolution.X = data.GetProperty<int>("X");
                    printerResolution.Y = data.GetProperty<int>("Y");
                    break;
                case "System.Drawing.Printing.PrinterSettings":
                    System.Drawing.Printing.PrinterSettings printerSettings = new System.Drawing.Printing.PrinterSettings();
                    toSerialize = printerSettings;
                    printerSettings.Collate = data.GetProperty<bool>("Collate");
                    printerSettings.Copies = data.GetProperty<short>("Copies");
                    printerSettings.Duplex = data.GetProperty<System.Drawing.Printing.Duplex>("Duplex");
                    printerSettings.FromPage = data.GetProperty<int>("FromPage");
                    printerSettings.MaximumPage = data.GetProperty<int>("MaximumPage");
                    printerSettings.MinimumPage = data.GetProperty<int>("MinimumPage");
                    printerSettings.PrinterName = data.GetProperty<string>("PrinterName");
                    string pfn = data.GetProperty<string>("PrintFileName");
                    if (!String.IsNullOrEmpty(pfn))
                        printerSettings.PrintFileName = pfn;
                    printerSettings.PrintRange = data.GetProperty<System.Drawing.Printing.PrintRange>("PrintRange");
                    printerSettings.PrintToFile = data.GetProperty<bool>("PrintToFile");
                    printerSettings.ToPage = data.GetProperty<int>("ToPage");
                    break;
            }
        }
    }
    //internal class JSonList: List<object>, IJsonSerialize, IJsonConvert
    //{
    //    Type originalType;
    //    public JSonList(IList lst, Type originalType) 
    //    {
    //        foreach (var item in lst)
    //        {
    //            this.Add(item);
    //        }
    //        this.originalType = originalType;
    //    }
    //    protected JSonList() { }
    //    public void GetObjectData(IJsonWriteData data)
    //    {
    //        data.AddProperty("$OriginalType", originalType.FullName);
    //        // we need to restore the types of keys and values when reading. Maybe they are the same for all items
    //        // then we create properties "$KeyType" and "$ValueType". 
    //        Type elementType = null;
    //        bool sameType = true;
    //        foreach (object item in this)
    //        {
    //            if (elementType == null && item != null) elementType = item.GetType();
    //            if (item != null && item.GetType() != elementType) sameType = false;
    //        }
    //        if (sameType && elementType != null) data.AddProperty("$ElementType", elementType.ToString());

    //        object[] toAdd;
    //        if (sameType)
    //        {
    //            toAdd = new object[Count];
    //            for (int i = 0; i < Count; i++)
    //            {
    //                toAdd[i] = this[i];
    //            }
    //        }
    //        else
    //        {
    //            throw new NotImplementedException("JsonList with different types not implemented");
    //        }
    //        data.AddProperty("$Entries", toAdd);
    //    }

    //    public void SetObjectData(IJsonReadData data)
    //    {
    //        Type elementType = null;
    //        if (data.HasProperty("$ElementType")) elementType = Type.GetType(data.GetProperty<string>("$ElementType"));
    //        object[] entries = data.GetProperty<object[]>("$Entries");
    //        for (int i = 0; i < entries.Length; i++)
    //        {
    //            var key = entries[i];
    //            if (!key.GetType().IsSubclassOf(elementType)) key = Convert.ChangeType(key, elementType);
    //            this.Add(key);
    //        }
    //        originalType = Type.GetType(data.GetProperty<string>("$OriginalType"));
    //    }

    //    object IJsonConvert.Convert()
    //    {
    //        ConstructorInfo construct = originalType.GetConstructor(BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.DefaultBinder, new Type[] { }, null);
    //        object res = construct.Invoke(new object[0]);
    //        IList dict = res as IList;
    //        foreach (var item in this)
    //        {
    //            dict.Add(item);
    //        }
    //        return res;
    //    }

    //}
}
