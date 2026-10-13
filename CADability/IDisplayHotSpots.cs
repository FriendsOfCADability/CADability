using CADability.UserInterface;

namespace CADability
{
    /*
	 * So geht das mit den HotSpots:
	 * Die SelectObjectAction oder besser gesagt, das dazugehörige IShowProperty, also SelectedObjectsProperty
	 * bekommt die IShowProperties von allen markierten Objekten. Wenn diese auch noch das Interface
	 * IDisplayHotSpots implementieren, dann hängt sich die SelectedObjectsProperty bei deren OnHotspotChanged
	 * event ein, bekommt also mit, wenn HotSpots angezeigt werden sollen oder wieder verschwinden sollen.
	 * Der Hotspot selbst muss IHotSpot implementieren. Das ist entweder eine GeoPointProperty
	 * oder Stellvertreterobjekte wie LengthHotSpot, die sich auf eine Property beziehen aber selbst
	 * die Lage des HotSpots verwalten.
	 * Wenn der user auf den Hotspot klickt, dann wird IHotSpot.IsSelected gesetzt, wenn er daran
	 * zieht, wird IHotSpot.StartDrag aufgerufen. Einfache Aktionen wie GeneralGeoVectorAction
	 * verändern dann die entsprechende Einstellung.
	 * */

    public enum HotspotChangeMode { Visible, Invisible, Selected, Deselected, Moved }
    public delegate void HotspotChangedDelegate(IHotSpot sender, HotspotChangeMode mode);
    /// <summary>
    /// Implemented by property entries (typically the property entry of a selected object) which display hotspots: draggable
    /// handles in the drawing area which represent properties of the object, e.g. a point or a length. The
    /// <see cref="SelectedObjectsProperty"/> of the <see cref="CADability.Actions.SelectObjectsAction"/> subscribes to
    /// <see cref="HotspotChangedEvent"/> to learn when hotspots are to be shown, hidden, selected or moved. The hotspots
    /// themselves implement <see cref="IHotSpot"/>.
    /// </summary>

    public interface IDisplayHotSpots
    {
        event HotspotChangedDelegate HotspotChangedEvent;
        void ReloadProperties(); // gefällt mir hier nicht
    }
    /// <summary>
    /// Interface implemented by HotSpots. 
    /// </summary>

    public interface IHotSpot
    {
        /// <summary>
        /// Gets the position of the HotSpot
        /// </summary>
        /// <returns>The position in the world coordinate system</returns>
        GeoPoint GetHotspotPosition();
        /// <summary>
        /// Sets or gets the selection state of a hotspot
        /// </summary>
        bool IsSelected { get; set; }
        /// <summary>
        /// Called when the user starts dragging a hotspot, i.e. the left mouse button
        /// is pushed while the mouse position leaves the hotspot square.
        /// </summary>
        void StartDrag(IFrame frame);
        /// <summary>
        /// Returns the info text to be displayed in the ToolTip.
        /// </summary>
        /// <param name="Level">Requested info level (simple info or detailed info)</param>
        /// <returns>The info string</returns>
        string GetInfoText(CADability.UserInterface.InfoLevelMode Level);
        /// <summary>
        /// Get the ResourceId of the Hotspot
        /// </summary>
        string ResourceId { get; }
        MenuWithHandler[] ContextMenu { get; }
        // hier könnte man noch ein Paint dazunehmen, um den Hotspots die
        // Möglichkeit zu geben, sich selbst gemäß ihrer Bedeutung darzustellen
        /// <summary>
        /// A hotspot may be hidden if the respective property is readonly
        /// </summary>
        bool Hidden { get; }
    }
}
