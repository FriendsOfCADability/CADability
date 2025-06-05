using System;
using System.Globalization;
using System.Reflection;
using System.Threading;

namespace CADability
{
    /// <summary>
    /// This class contains the information to undo a change in the project database.
    /// Undoing a change is done by reflection. So we need an object, the name of a method
    /// or a property and the parameters to use in the call. This information must be
    /// provided in the constructor. The method or property must have public access.
    /// </summary>

    public class ReversibleChange
    {
        private object objectToChange;
        private string methodOrPropertyName;
        private object[] parameters;
        private Type interfaceForMethod;
        private Func<bool> undo; // this is the modern way to do it, all data is captured
        /// <summary>
        /// Creates a ReversibleChange object. MethodOrPropertyName must be the name (casesensitive!)
        /// of a public method or property that reverses the change when called with the parameters
        /// given in the parameter "Parameter". If parameters contains exactly 1 object, the Undo
        /// method will first look for a set-property with that type to reverse the change. If there
        /// is no set-property with that name and type then the system will look for a method
        /// with this single parameter.
        /// </summary>
        /// <param name="objectToChange">The object which will be or was changed</param>
        /// <param name="interfaceForMethod">the interface on which contains the method or property</param>
        /// <param name="methodOrPropertyName">the case sensitive name of the method or property</param>
        /// <param name="parameters">The parameters neede to call this method or property</param>
        public ReversibleChange(object objectToChange, Type interfaceForMethod, string methodOrPropertyName, params object[] parameters)
        {
            this.objectToChange = objectToChange;
            this.methodOrPropertyName = methodOrPropertyName;
            this.parameters = (object[])parameters.Clone();
            this.interfaceForMethod = interfaceForMethod;
        }
        /// <summary>
        /// Creates a ReversibleChange object. MethodOrPropertyName must be the name (casesensitive!)
        /// of a public method or property that reverses the change when called with the parameters
        /// given in the parameter "Parameter". If parameters contains exactly 1 object, the Undo
        /// method will first look for a set-property with that type to reverse the change. If there
        /// is no set-property with that name and type then the system will look for a method
        /// with this single parameter.
        /// </summary>
        /// <param name="objectToChange">The object which will be or was changed</param>
        /// <param name="methodOrPropertyName">the case sensitive name of the method or property</param>
        /// <param name="parameters">The parameters neede to call this method or property</param>
        public ReversibleChange(object objectToChange, string methodOrPropertyName, params object[] parameters)
        {
            this.objectToChange = objectToChange;
            this.methodOrPropertyName = methodOrPropertyName;
            this.parameters = (object[])parameters.Clone();
            this.interfaceForMethod = null;
        }
        /// <summary>
        /// The provided funtion knows how to undo the change and has the data, which is needed for the undo, captured
        /// </summary>
        /// <param name="undo"></param>
        public ReversibleChange(Func<bool> undo)
        { this.undo = undo; }
        private PropertyInfo FindProperty(object o, string propname, Type ret)
        {
            PropertyInfo propertyInfo = o.GetType().GetProperty(propname, ret);
            if (propertyInfo == null)
            {   // speziell für ERSACAD: dort haben wg. der flexiblen Namensvergabe die Properties manchmal
                // einen prefix, der nicht entsprechend geändert wird. Hier wird, wenn eine passende Property
                // nicht gefunden wird, eine solche gesucht, die mit dem namen endet. Ggf könnte man auch den Typ sicherstellen
                PropertyInfo[] props = o.GetType().GetProperties();
                for (int i = 0; i < props.Length; i++)
                {
                    if (props[i].Name.EndsWith(propname, true, CultureInfo.InvariantCulture))
                    {
                        propertyInfo = props[i];
                        break;
                    }
                }
            }
            return propertyInfo;
        }
        private PropertyInfo FindProperty(object o, string propname)
        {
            PropertyInfo propertyInfo = o.GetType().GetProperty(propname);
            if (propertyInfo == null)
            {   // speziell für ERSACAD: dort haben wg. der flexiblen Namensvergabe die Properties manchmal
                // einen prefix, der nicht entsprechend geändert wird. Hier wird, wenn eine passende Property
                // nicht gefunden wird, eine solche gesucht, die mit dem namen endet. Ggf könnte man auch den Typ sicherstellen
                PropertyInfo[] props = o.GetType().GetProperties();
                for (int i = 0; i < props.Length; i++)
                {
                    if (props[i].Name.EndsWith(propname, true, CultureInfo.InvariantCulture))
                    {
                        propertyInfo = props[i];
                        break;
                    }
                }
            }
            return propertyInfo;
        }
        private MethodInfo FindMethod(object o, string methodname, Type[] ret)
        {
            MethodInfo methodInfo = o.GetType().GetMethod(methodname, ret);
            if (methodInfo == null)
            {   // speziell für ERSACAD: dort haben wg. der flexiblen Namensvergabe die Properties manchmal
                // einen prefix, der nicht entsprechend geändert wird. Hier wird, wenn eine passende Property
                // nicht gefunden wird, eine solche gesucht, die mit dem namen endet. Ggf könnte man auch den Typ sicherstellen
                MethodInfo[] methods = o.GetType().GetMethods();
                for (int i = 0; i < methods.Length; i++)
                {
                    if (methods[i].Name.EndsWith(methodname, true, CultureInfo.InvariantCulture))
                    {
                        methodInfo = methods[i];
                        break;
                    }
                }
            }
            return methodInfo;
        }
        public bool Undo()
        {
            if (undo != null) return undo(); // this is the modern way
            if (parameters.Length == 1)
            {
                try
                {
                    // nur ein Parameter, zuerst probieren ob es eine Set Property gibt:
                    PropertyInfo propertyInfo;
                    if (parameters[0] != null)
                    {
                        Type parType = parameters[0].GetType();
                        propertyInfo = FindProperty(objectToChange, methodOrPropertyName, parType);
                        while (propertyInfo == null && parType.BaseType != null)
                        {
                            parType = parType.BaseType;
                            propertyInfo = FindProperty(objectToChange, methodOrPropertyName, parType);
                        }

                    }
                    else
                    {
                        propertyInfo = FindProperty(objectToChange, methodOrPropertyName);
                    }
                    if (propertyInfo != null)
                    {
                        MethodInfo mi = propertyInfo.GetSetMethod();
                        if (mi != null)
                        {
                            mi.Invoke(objectToChange, parameters);
                            return true;
                        }
                    }
                }
                catch (Exception e) // wenn hier irgendwas schief geht, dann nicht mehr asynchron laufen lassen
                {
                    if (e is ThreadAbortException) throw (e);
                    return false;
                }
            }
            try
            {
                Type[] types = new Type[parameters.Length];
                for (int i = 0; i < types.Length; ++i)
                {
                    if (parameters[i] != null) types[i] = parameters[i].GetType();
                    else types[i] = typeof(object);
                }
                MethodInfo mi = FindMethod(objectToChange, methodOrPropertyName, types);
                // Wenn die Methode nicht gefunden wird, dann kann es sein, dass ein oder mehrere Parameter abgeleitete typen haben
                // und eigentlich die Basistypen gefragt sind. In diesem Fall müsste man auch ein Array von Typen mit übergeben, welches dann hier
                // verwendet werden kann. Oder man gibt gleich die methodinfo mit, dann braucht man sie nicht zu suchen
                // NOCH BESSER: man ruft GetMethods auf, sucht nach dem passenden Namen und überprüft die Anzahl der Parameter und dann mit
                // Type.IsSubclassOf ob die Typen passen.
                if (mi != null)
                {
                    mi.Invoke(objectToChange, parameters);
                    return true;
                }
                if (interfaceForMethod != null)
                {
                    mi = interfaceForMethod.GetMethod(methodOrPropertyName, types);
                    if (mi != null)
                    {
                        mi.Invoke(objectToChange, parameters);
                        return true;
                    }
                }
                Type type = objectToChange.GetType();
                while (type != null)
                {
                    type = type.BaseType;
                    if (type != null)
                    {
                        mi = type.GetMethod(methodOrPropertyName, types);
                        if (mi != null)
                        {
                            mi.Invoke(objectToChange, parameters);
                            return true;
                        }
                    }
                }
            }
            catch (Exception e) // wenn hier irgendwas schief geht, dann nicht mehr asynchron laufen lassen
            {
                if (e is ThreadAbortException) throw (e);
                return false;
            }
            return false;
        }
        /// <summary>
        /// Gets the method or property name for this ReversibleChange
        /// </summary>
		public string MethodOrPropertyName
        {   // nur zum Debuggen
            get { return methodOrPropertyName; }
        }
        /// <summary>
        /// Gets the objects which is changed by this ReversibleChange
        /// </summary>
        public object ObjectToChange
        {
            get
            {
                return objectToChange;
            }
        }
        /// <summary>
        /// Gets the parameters, that can be used in the method call of this ReversibleChange
        /// </summary>
        public object[] Parameters
        {
            get
            {
                return parameters.Clone() as object[];
            }
        }
        /// <summary>
        /// Checks whether this ReversibleChange ist a method with the given name
        /// </summary>
        /// <param name="methodName">The name of the method</param>
        /// <returns>true, if methodName is the name of the ReversibleChange method</returns>
        public bool IsMethod(string methodName)
        {
            return (methodOrPropertyName == methodName);
        }
        /// <summary>
        /// Overrides objec.ToString()
        /// </summary>
        /// <returns>the call for the undo as a string</returns>
		public override string ToString()
        {
            string res = "ReversibleChange: " + objectToChange.ToString() + "." + methodOrPropertyName;
            if (parameters != null && parameters.Length > 0)
            {
                res = res + "(";
                for (int i = 0; i < parameters.Length; ++i)
                {
                    if (parameters[i] != null)
                    {
                        if (i > 0) res = res + ", ";
                        res = res + parameters[i].ToString();
                    }
                }
                res = res + ")";
            }
            return res;
        }

    }
}
