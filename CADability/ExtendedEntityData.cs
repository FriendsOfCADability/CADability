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
                Data.Add(new KeyValuePair<XDataCode, object>((XDataCode)(int)(double)(keys[i]), values[i]));
            }
        }
    }
}
