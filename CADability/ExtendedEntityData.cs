using System;
using System.Collections.Generic;
using netDxf;

namespace CADability.DXF
{    public class ExtendedEntityData : IJsonSerialize
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
