using System.Collections.Generic;

namespace CADability
{
    /// <summary>
    /// Types that have been renamed since they were first serialized.
    /// <para>
    /// Both persistence formats record type names: the binary format writes the declared type of
    /// every <c>SerializationInfo.AddValue</c> entry, and the JSON format writes a <c>$Type</c>
    /// property for values it cannot infer from context. A file written before a rename therefore
    /// carries the old name, and without a mapping the type is simply not found - the binary reader
    /// substitutes <c>UnDeseriazableObject</c> and the following cast fails, the JSON reader falls
    /// back to a proxy object and the value is silently lost.
    /// </para>
    /// <para>
    /// Add an entry here whenever a serialized type is renamed, and never remove one: the entry is
    /// the only thing keeping files written by earlier versions readable.
    /// </para>
    /// </summary>
    internal static class RenamedTypes
    {
        private static readonly Dictionary<string, string> map = new Dictionary<string, string>
        {
            // renamed because the type carries three independent edge lengths (XDiff, YDiff, ZDiff),
            // so "cube" claimed something that is not true
            { "CADability.BoundingCube", "CADability.BoundingBox" },
        };

        /// <summary>
        /// Returns the current name of <paramref name="typeName"/>, or <paramref name="typeName"/>
        /// itself if that type has not been renamed. Array names ("...[]", "...[,]") are resolved
        /// through their element type. Names with an embedded assembly qualification (the generic
        /// argument lists the binary formatter writes) are not resolved.
        /// </summary>
        public static string Resolve(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return typeName;
            if (map.TryGetValue(typeName, out string renamed)) return renamed;
            int bracket = typeName.IndexOf('[');
            if (bracket > 0 && typeName[typeName.Length - 1] == ']'
                && map.TryGetValue(typeName.Substring(0, bracket), out renamed))
            {
                return renamed + typeName.Substring(bracket);
            }
            return typeName;
        }
    }
}
