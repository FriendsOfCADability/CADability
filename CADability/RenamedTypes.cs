using System;
using System.Collections.Generic;

namespace CADability
{
    /// <summary>
    /// Classes that have been renamed since files were written which contain them.
    /// <para>
    /// A project file records the type of every object it holds by name, so renaming a class would make every
    /// existing file containing one unreadable - which is why class names tend to ossify. This table is what
    /// keeps them free to change: the reader looks up the name it found in the file and gets the name the
    /// class has today. Renaming a serialized class is then two steps, the rename itself and one line here.
    /// </para>
    /// <para>
    /// Both readers use it, so one entry covers both formats: <see cref="JsonSerialize"/> for the current one
    /// and the serialization binder of <see cref="Project"/> for the old binary one. That binder has been
    /// doing the same thing by hand since the Condor days - it still maps Condor.* onto CADability.* and the
    /// attribute classes into their namespace - and those entries are deliberately left where they are rather
    /// than moved here, because they also rewrite the assembly name, which this table does not do.
    /// </para>
    /// <para>
    /// An entry is forever: a file written years ago is still read, so nothing here may be removed, only
    /// added. Renaming a class that is already in the table means CHANGING the value of its entry, never
    /// adding a second one - the lookup is a single step and does not follow chains.
    /// </para>
    /// </summary>
    public static class RenamedTypes
    {
        private static readonly Dictionary<string, string> renamed = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // when the bounding box was given a name that says what it is: it is a cuboid, not a cube
            { "CADability.BoundingCube", "CADability.BoundingBox" },
            // when the two ProjectedCurve classes became one, as in ShapeIt: the 2d curves of an InterpolatedDualSurfaceCurve
            // are ProjectedCurves which read the format of the nested class too
            { "CADability.InterpolatedDualSurfaceCurve+ProjectedCurve", "CADability.ProjectedCurve" },
        };

        /// <summary>
        /// The name <paramref name="typeName"/> stands for today, or the name itself when it was never
        /// renamed. An array suffix is carried over, so the entry for a class also covers arrays of it.
        /// <para>
        /// A renamed class appearing as the argument of a generic type - "Dictionary`2[[...],[...]]" - is NOT
        /// rewritten. No file has ever held one, and the assembly qualified names inside those brackets would
        /// need a parser rather than a lookup. Should it ever become necessary, this is the one place to do it.
        /// </para>
        /// </summary>
        public static string Resolve(string typeName)
        {
            if (string.IsNullOrEmpty(typeName) || renamed.Count == 0) return typeName;
            if (renamed.TryGetValue(typeName, out string current)) return current;

            // an array, a jagged array or a pointer: map the element type and put the suffix back on
            int suffix = typeName.IndexOfAny(new char[] { '[', '*', '&' });
            if (suffix > 0 && renamed.TryGetValue(typeName.Substring(0, suffix), out current))
                return current + typeName.Substring(suffix);
            return typeName;
        }

        /// <summary>
        /// Adds a rename. For the classes of CADability itself the table above is the place; this is for an
        /// assembly built on top of it which serializes classes of its own and renames one of them.
        /// </summary>
        /// <param name="oldName">the full name as it appears in files already written</param>
        /// <param name="newName">the full name the class has now</param>
        public static void Add(string oldName, string newName)
        {
            if (string.IsNullOrEmpty(oldName)) throw new ArgumentNullException(nameof(oldName));
            if (string.IsNullOrEmpty(newName)) throw new ArgumentNullException(nameof(newName));
            if (renamed.TryGetValue(oldName, out string existing) && existing != newName)
                throw new ArgumentException($"'{oldName}' is already mapped onto '{existing}', it cannot also "
                    + $"be mapped onto '{newName}'. A name stands for one class.", nameof(oldName));
            renamed[oldName] = newName;
        }
    }
}
