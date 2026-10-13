using System.Collections;
using System.Reflection;

namespace CADability.UserInterface
{
    /// <summary>
    /// Maps the cursor names used by the actions (historically the names of ".cur" resource files) to the names understood by
    /// <see cref="ICanvas.Cursor"/>: <see cref="GetCursor"/> strips the ".cur" extension. Internal helper, not an entry point of the API.
    /// </summary>
    public class CursorTable
    {
        public static string GetCursor(string Name)
        {
            return Name.Replace(".cur", "");

        }
    }
}
