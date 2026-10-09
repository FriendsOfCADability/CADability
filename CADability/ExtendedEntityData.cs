using System;
using System.Collections.Generic;

namespace CADability.DXF
{
    public enum XDataCode
    {
        AppReg = 1001,
        String = 1000,
        ControlString = 1002,
        LayerName = 1003,
        BinaryData = 1004,
        DatabaseHandle = 1005,
        RealX = 1010,
        RealY = 1020,
        RealZ = 1030,
        WorldSpacePositionX = 1011,
        WorldSpacePositionY = 1021,
        WorldSpacePositionZ = 1031,
        WorldSpaceDisplacementX = 1012,
        WorldSpaceDisplacementY = 1022,
        WorldSpaceDisplacementZ = 1032,
        WorldDirectionX = 1013,
        WorldDirectionY = 1023,
        WorldDirectionZ = 1033,
        Real = 1040,
        Distance = 1041,
        ScaleFactor = 1042,
        Int16 = 1070,
        Int32 = 1071
    }

    public class ExtendedEntityData : IJsonSerialize
    {
        public string ApplicationName { get; set; }
        public List<KeyValuePair<XDataCode, object>> Data { get; private set; }

        public ExtendedEntityData()
        {
            Data = new List<KeyValuePair<XDataCode, object>>();
        }

        public void GetObjectData(IJsonWriteData data)
        {
            int[] keys = new int[Data.Count];
            object[] values = new object[Data.Count];
            for (int i = 0; i < Data.Count; i++)
            {
                keys[i] = (int)this.Data[i].Key;
                values[i] = this.Data[i].Value;
            }
            data.AddProperty("ApplicationName", ApplicationName);
            data.AddProperty("Keys", keys);
            data.AddProperty("Values", values);
        }

        public void SetObjectData(IJsonReadData data)
        {
            ApplicationName = data.GetStringProperty("ApplicationName");
            List<object> keys = data.GetProperty<List<object>>("Keys");
            List<object> values = data.GetProperty<List<object>>("Values");
            for (int i = 0; i < keys.Count; i++)
            {
                XDataCode code = (XDataCode)(int)(double)(keys[i]);
                Data.Add(new KeyValuePair<XDataCode, object>(code, RestoreValueType(code, values[i])));
            }
        }

        // JSON only knows numbers and strings, so every integer comes back as a double and a
        // control string as a one character string. Give them back the types the DXF/DWG import
        // stores, because the DXF export only accepts those: it wrote a 16 bit integer that
        // had been read back from a cdb file as 0.
        private static object RestoreValueType(XDataCode code, object value)
        {
            switch (code)
            {
                case XDataCode.Int16:
                    if (value is double d16) return (short)d16;
                    break;
                case XDataCode.Int32:
                    if (value is double d32) return (int)d32;
                    break;
                case XDataCode.LayerName:
                case XDataCode.DatabaseHandle:
                    // ACadSharp gives both as the handle of the referenced object
                    if (value is double handle) return (ulong)handle;
                    break;
                case XDataCode.ControlString:
                    if (value is string s && s.Length == 1) return s[0];
                    break;
            }
            return value;
        }
    }
}
