using System;
using System.Reflection;
using System.Runtime.Serialization;

namespace CADability
{
    /// <summary>
    /// Encapsulates the SerializationInfo.GetInfo function
    /// </summary>

    public class InfoReader
    {
        /// <summary>
        /// Reads the requested object. Returns null if the object cannot be read.
        /// Calls info.GetValue(key,type).
        /// </summary>
        /// <param name="info">the SerializationInfo object</param>
        /// <param name="key">the name of the object</param>
        /// <param name="type">the type of the requested object</param>
        /// <returns>the object or null</returns>
        static public object Read(SerializationInfo info, string key, System.Type type)
        {
            try
            {
                return info.GetValue(key, type);
            }
            catch (SerializationException)
            {
                return null;
            }
        }
        /// <summary>
        /// Reads or creates the requested object.
        /// Calls info.GetValue(key,type).
        /// If the object cannot be read, this method tries to invoke a constructor for the
        /// object with the given parameters. If the constructor invokation fails null will be returned.
        /// </summary>
        /// <param name="info">the SerializationInfo object</param>
        /// <param name="key">the name of the object</param>
        /// <param name="type">the type of the requested object</param>
        /// <param name="args">parameters for the constructor</param>
        /// <returns>the read or created object</returns>
        static public object ReadOrCreate(SerializationInfo info, string key, System.Type type, params object[] args)
        {
            try
            {
                return info.GetValue(key, type);
            }
            catch (SerializationException)
            {
                System.Type[] types;
                if (args == null)
                    types = new Type[] { };
                else
                {
                    types = new Type[args.Length];
                    for (int i = 0; i < args.Length; i++)
                        types[i] = args[i].GetType();
                }
                ConstructorInfo ci = type.GetConstructor(types);
                object res = null;
                if (ci != null)
                    res = ci.Invoke(args);
                return res;
            }

        }
        static public bool ReadBool(object val)
        {   // wenn man mit info.GetEnumerator über alle SerializationInfos iteriert, dann komm der bool value als string
            // in der xml Serialisierung (überhaupt kommen so alle primitiven Typen als string)
            if (val is string)
            {
                switch (val as string)
                {
                    case "true": return true;
                    case "false": return false;
                }
            }
            else
            {
                return (bool)(val);
            }
            return false; // kommt nicht vor
        }
    }
}
