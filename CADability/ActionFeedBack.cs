using CADability.Attribute;
using CADability.GeoObject;
using System.Collections.Generic;
#if WEBASSEMBLY
using CADability.WebDrawing;
using Point = CADability.WebDrawing.Point;
#else
using System.Drawing;
using Point = System.Drawing.Point;
#endif

namespace CADability.Actions
{
    /// <summary>
    /// A container for Feedback objects. These are objects that give visual feedback
    /// to the user while a <see cref="Action"/> is in progress. Currently
    /// you can add <see cref="IGeoObject"/>s or <see cref="FeedBackPlane"/>s that provide the feedback.
    /// </summary>


    public class ActionFeedBack
    {
        private List<IFeedBack> repaintObjects; // Liste diverser Objekte, z.Z. nur IGeoObject, geplant weiteres Interface IFeedBack
        private List<IGeoObject> paintAsSelected; // Liste von Objekten, die als markiert dargestellt werden sollen
        private List<IGeoObject> paintAsTransparent;
        private IFrame frame;
        private bool makeTransparent;
        private int selectWidth;
        internal ActionFeedBack()
        {
            repaintObjects = new List<IFeedBack>();
            paintAsSelected = new List<IGeoObject>();
            paintAsTransparent = new List<IGeoObject>();
            makeTransparent = false;
        }
        private void Invalidate()
        {
        }
        internal IFrame Frame
        {
            set
            {
                frame = value;
                selectWidth = frame.GetIntSetting("Select.SelectWidth", 2);
                for (int i = 0; i < repaintObjects.Count; ++i)
                {
                    IFeedBack go = repaintObjects[i];
                    if (go != null)
                    {
                        OnFeedBackChanged(go); // fürs invalidate
                    }
                }
            }
        }
        internal void Repaint(Rectangle IsInvalid, IView View, IPaintTo3D paintTo3D)
        {
            if (Settings.GlobalSettings.GetBoolValue("ActionFeedBack.UseZBuffer", true)) paintTo3D.UseZBuffer(true);

            Color selectColor = frame.GetColorSetting("Select.SelectColor", Color.Yellow); // die Farbe für die selektierten Objekte
            // Repaint is called once for each view with that view's paintTo3D, so the feedback is painted once here (issue #308:
            // a loop over all views painted it once per view into the same view).
            for (int i = 0; i < repaintObjects.Count; ++i)
            {
                IFeedBack go = repaintObjects[i] as IFeedBack;
                if (go != null)
                {
                    go.PaintTo3D(paintTo3D);
                }
            }

            if (paintAsSelected.Count > 0)
            {
                bool oldSelectMode = paintTo3D.SelectMode;
                paintTo3D.SelectMode = true;
                bool pse = paintTo3D.PaintSurfaceEdges;
                paintTo3D.PaintSurfaceEdges = false;
                paintTo3D.SelectColor = selectColor;
                // all objects painted as selected go into a single display list, not one list per object and repaint (issue #308)
                paintTo3D.OpenList("feedback");
                for (int i = 0; i < paintAsSelected.Count; ++i)
                {
                    IGeoObjectImpl go = paintAsSelected[i] as IGeoObjectImpl;
                    if (go != null) go.PaintTo3D(paintTo3D);
                }
                IPaintTo3DList list = paintTo3D.CloseList();
                if (list != null) paintTo3D.SelectedList(list, -1); // changed from selectWidth to -1 (also show faces which are behind other faces)
                paintTo3D.PaintSurfaceEdges = pse;
                paintTo3D.SelectMode = oldSelectMode;
            }

            if (paintAsTransparent.Count > 0)
            {
                paintTo3D.OpenList("feedback-transparent");

                foreach (IGeoObject go in paintAsTransparent)
                {
                    go.PaintTo3D(paintTo3D);
                }

                IPaintTo3DList displayList = paintTo3D.CloseList();

                paintTo3D.Blending(true);
                paintTo3D.List(displayList);
                paintTo3D.Blending(false);
            }
        }
        /// <summary>
        /// Meant to switch the outline of the objects added with <see cref="AddSelected(IGeoObject)"/> on (width from the
        /// setting "Select.SelectWidth") or off. The current repaint does not evaluate it and always draws these objects
        /// in the select mode, so setting it has no visible effect.
        /// </summary>
        public bool SelectOutline { get{ return selectWidth > 0; } set { selectWidth = value ? frame.GetIntSetting("Select.SelectWidth", 2) : 0; } }
        /// <summary>
        /// Requests that the views draw the model transparent while the action runs, so that feedback objects inside
        /// solids stay visible, and triggers a repaint. The request is forwarded to the views whenever the feedback
        /// changes; whether it is honoured depends on the view (<see cref="ModelView"/> currently only repaints).
        /// </summary>
        /// <param name="transparent">true to draw the model transparent, false to draw it normally</param>
        public void MakeModelTransparent(bool transparent)
        {
            makeTransparent = transparent;
            OnFeedBackChanged(null);
        }
        /// <summary>
        /// Adds an <see cref="IFeedBack"/> to the list of feedback objects. The object
        /// shows in the appropriate view and the view reflects all changes of the object.
        /// Currently there are the GeoObjects and the <see cref="FeedBackPlane"/> object which support the IFeedBack interface.
        /// You could also implement your own IFeedBack objects.
        /// </summary>
        /// <param name="feedBackObject">the object to show</param>
        /// <returns>the index in the list, may be used for <see cref="Remove(int)"/></returns>
        public int Add(IFeedBack feedBackObject)
        {
            repaintObjects.Add(feedBackObject);
            feedBackObject.FeedBackChangedEvent += new FeedBackChangedDelegate(OnFeedBackChanged);
            OnFeedBackChanged(feedBackObject);
            return repaintObjects.Count - 1;
        }
        /// <summary>
        /// Adds all objects of <paramref name="feedBackObjects"/> that implement <see cref="IFeedBack"/> (which the
        /// GeoObjects of CADability do) to the list of feedback objects, see <see cref="Add(IFeedBack)"/>.
        /// </summary>
        /// <param name="feedBackObjects">the objects to show</param>
        /// <returns>the index of the last added object in the list, may be used for <see cref="Remove(int)"/></returns>
        public int Add(GeoObjectList feedBackObjects)
        {
            foreach (IGeoObject go in feedBackObjects)
            {
                if (go is IFeedBack fb)
                {
                    repaintObjects.Add(fb);
                    fb.FeedBackChangedEvent += new FeedBackChangedDelegate(OnFeedBackChanged);
                    OnFeedBackChanged(fb);
                }
            }
            return repaintObjects.Count - 1;
        }
        /// <summary>
        /// Adds all objects of <paramref name="feedBackObjects"/> to the list of feedback objects, see
        /// <see cref="Add(IFeedBack)"/>. The objects must implement <see cref="IFeedBack"/>, which the GeoObjects of
        /// CADability do.
        /// </summary>
        /// <param name="feedBackObjects">the objects to show</param>
        /// <returns>the index of the last added object in the list, may be used for <see cref="Remove(int)"/></returns>
        public int Add(IEnumerable<IGeoObject> feedBackObjects)
        {
            foreach (IGeoObject go in feedBackObjects)
            {
                IFeedBack feedBackObject = go as IFeedBack;
                repaintObjects.Add(feedBackObject);
                feedBackObject.FeedBackChangedEvent += new FeedBackChangedDelegate(OnFeedBackChanged);
                OnFeedBackChanged(feedBackObject);
            }
            return repaintObjects.Count - 1;
        }

        void OnFeedBackChanged(IFeedBack sender)
        {
            BoundingCube ext = BoundingCube.EmptyBoundingCube;
            for (int i = 0; i < repaintObjects.Count; i++)
            {
                ext.MinMax(repaintObjects[i].GetExtent());
            }
            for (int i = 0; i < paintAsSelected.Count; i++)
            {
                ext.MinMax(paintAsSelected[i].GetExtent(0.0));
            }
            for (int i = 0; i < paintAsTransparent.Count; i++)
            {
                ext.MinMax(paintAsTransparent[i].GetExtent(0.0));
            }
            if (frame != null)
            {
                foreach (IView vw in frame.AllViews)
                {
                    if (!ext.IsEmpty && vw is IActionInputView)
                    {
                        (vw as IActionInputView).SetAdditionalExtent(ext);
                        (vw as IActionInputView).MakeEverythingTranparent(makeTransparent);
                    }
                    vw.InvalidateAll();
                }
            }
        }
        /// <summary>
        /// Removes an previously added <see cref="IFeedBack"/> from the list of feedback objects.
        /// The object is no longer displayed.
        /// </summary>
        /// <param name="feedBackObject">Object to remove</param>
        public void Remove(IFeedBack feedBackObject)
        {
            if (repaintObjects.Contains(feedBackObject))
            {
                repaintObjects.Remove(feedBackObject);
                feedBackObject.FeedBackChangedEvent -= new FeedBackChangedDelegate(OnFeedBackChanged);
                OnFeedBackChanged(feedBackObject);
            }
        }
        /// <summary>
        /// Removes an <see cref="IFeedBack"/> object by its index from the list of the displayed objects.
        /// </summary>
        /// <param name="index">Index of the object to remove</param>
        public void Remove(int index)
        {
            IFeedBack fb = repaintObjects[index] as IFeedBack;
            repaintObjects.RemoveAt(index);
            fb.FeedBackChangedEvent -= new FeedBackChangedDelegate(OnFeedBackChanged);
            OnFeedBackChanged(fb);
        }
        /// <summary>
        /// Adds a GeoObject to the list of displayed objects. In contrary to <see cref="Add(IFeedBack)"/> the object is displayed
        /// in the select color and the select display mode.
        /// </summary>
        /// <param name="feedBackObject">Object to add</param>
        /// <returns>the index in the list, may be used for <see cref="RemoveSelected(int)"/></returns>
        public int AddSelected(IGeoObject feedBackObject)
        {
            paintAsSelected.Add(feedBackObject);
            (feedBackObject as IFeedBack).FeedBackChangedEvent += new FeedBackChangedDelegate(OnFeedBackChanged);
            OnFeedBackChanged(feedBackObject as IFeedBack);
            return paintAsSelected.Count - 1;
        }
        /// <summary>
        /// Adds all objects of <paramref name="feedBackObjects"/> to the list of objects displayed in the select color
        /// and the select display mode, see <see cref="AddSelected(IGeoObject)"/>.
        /// </summary>
        /// <param name="feedBackObjects">the objects to add</param>
        /// <returns>the index of the last added object in the list, may be used for <see cref="RemoveSelected(int)"/></returns>
        public int AddSelected(GeoObjectList feedBackObjects)
        {
            paintAsSelected.AddRange(feedBackObjects);
            foreach (IGeoObject go in feedBackObjects)
            {
                if (go is IFeedBack fb)
                {
                    fb.FeedBackChangedEvent += new FeedBackChangedDelegate(OnFeedBackChanged);
                    OnFeedBackChanged(fb);
                }
            }
            return paintAsSelected.Count - 1;
        }
        /// <summary>
        /// Adds all objects of <paramref name="feedBackObjects"/> to the list of objects displayed in the select color
        /// and the select display mode, see <see cref="AddSelected(IGeoObject)"/>.
        /// </summary>
        /// <param name="feedBackObjects">the objects to add</param>
        /// <returns>the index of the last added object in the list, may be used for <see cref="RemoveSelected(int)"/></returns>
        public int AddSelected(IEnumerable<IGeoObject> feedBackObjects)
        {
            paintAsSelected.AddRange(feedBackObjects);
            foreach (IGeoObject go in feedBackObjects)
            {
                if (go is IFeedBack fb)
                {
                    fb.FeedBackChangedEvent += new FeedBackChangedDelegate(OnFeedBackChanged);
                    OnFeedBackChanged(fb);
                }
            }
            return paintAsSelected.Count - 1;
        }
        /// <summary>
        /// Removes the provided and previously added object from the list of "display as selected" objects
        /// </summary>
        /// <param name="feedBackObject">Object to remove</param>
        public void RemoveSelected(IGeoObject feedBackObject)
        {
            if (paintAsSelected.Contains(feedBackObject))
            {
                paintAsSelected.Remove(feedBackObject);
                (feedBackObject as IFeedBack).FeedBackChangedEvent -= new FeedBackChangedDelegate(OnFeedBackChanged);
                OnFeedBackChanged(feedBackObject as IFeedBack);
            }
        }
        /// <summary>
        /// Removes the object (identified by its index) from the list of "display as selected" objects
        /// </summary>
        /// <param name="index">index of the object to remove</param>
        public void RemoveSelected(int index)
        {
            IFeedBack fb = paintAsSelected[index] as IFeedBack;
            paintAsSelected.RemoveAt(index);
            fb.FeedBackChangedEvent -= new FeedBackChangedDelegate(OnFeedBackChanged);
            OnFeedBackChanged(fb);
        }
        /// <summary>
        /// Removes all objects from the list of "display as selected" objects
        /// </summary>
        public void ClearSelected()
        {
            for (int i = 0; i < paintAsSelected.Count; ++i)
            {
                IGeoObject feedBackObject = paintAsSelected[i];
                (feedBackObject as IFeedBack).FeedBackChangedEvent -= new FeedBackChangedDelegate(OnFeedBackChanged);
            }
            OnFeedBackChanged(null);
            paintAsSelected.Clear();
        }
        /// <summary>
        /// Adds a GeoObject to the list of objects that are displayed semi-transparent (with blending) on top of the
        /// model, e.g. the rotation arrow of the parametric angle action. The view reflects changes of the object.
        /// </summary>
        /// <param name="feedBackObject">the object to add</param>
        public void AddTransparent(IGeoObject feedBackObject)
        {
            paintAsTransparent.Add(feedBackObject);
            (feedBackObject as IFeedBack).FeedBackChangedEvent += new FeedBackChangedDelegate(OnFeedBackChanged);
            OnFeedBackChanged(feedBackObject as IFeedBack);
        }
        /// <summary>
        /// Adds all objects of <paramref name="feedBackObjects"/> to the list of objects displayed semi-transparent, see
        /// <see cref="AddTransparent(IGeoObject)"/>.
        /// </summary>
        /// <param name="feedBackObjects">the objects to add</param>
        public void AddTransparent(IEnumerable<IGeoObject> feedBackObjects)
        {
            paintAsTransparent.AddRange(feedBackObjects);
            foreach (var feedBackObject in feedBackObjects)
            {
                (feedBackObject as IFeedBack).FeedBackChangedEvent += new FeedBackChangedDelegate(OnFeedBackChanged);
                OnFeedBackChanged(feedBackObject as IFeedBack);
            }
        }
        /// <summary>
        /// Removes a previously added object from the list of objects displayed semi-transparent. Does nothing if the
        /// object is not in the list.
        /// </summary>
        /// <param name="feedBackObject">the object to remove</param>
        public void RemoveTransparent(IGeoObject feedBackObject)
        {
            if (paintAsTransparent.Contains(feedBackObject))
            {
                paintAsTransparent.Remove(feedBackObject);
                (feedBackObject as IFeedBack).FeedBackChangedEvent -= new FeedBackChangedDelegate(OnFeedBackChanged);
                OnFeedBackChanged(feedBackObject as IFeedBack);
            }
        }
        /// <summary>
        /// Removes all objects from the list of objects displayed semi-transparent.
        /// </summary>
        public void ClearTransparent()
        {
            for (int i = 0; i < paintAsTransparent.Count; ++i)
            {
                IGeoObject feedBackObject = paintAsTransparent[i];
                (feedBackObject as IFeedBack).FeedBackChangedEvent -= new FeedBackChangedDelegate(OnFeedBackChanged);
            }
            OnFeedBackChanged(null);
            paintAsTransparent.Clear();
        }
        /// <summary>
        /// Removes all feedback objects.
        /// </summary>
        public void ClearAll()
        {
            ClearSelected();
            ClearTransparent();
            for (int i = 0; i < repaintObjects.Count; ++i)
            {
                IFeedBack feedBackObject = repaintObjects[i];
                if (feedBackObject != null)
                {
                    feedBackObject.FeedBackChangedEvent -= new FeedBackChangedDelegate(OnFeedBackChanged);
                }
            }
            OnFeedBackChanged(null);
            repaintObjects.Clear();
        }
        /// <summary>
        /// Creates a point which can be used as a feedback object. The point uses the feedback color as specified
        /// in the project settings.
        /// </summary>
        /// <param name="frame">Frame to gat access to the settings</param>
        /// <returns>A <see cref="GeoObject.Point"/> as a feedback object</returns>
        static public CADability.GeoObject.Point FeedbackPoint(IFrame frame)
        {
            PointSymbol pointSymbol = PointSymbol.Plus;
            return (FeedbackPoint(frame, pointSymbol));
        }
        /// <summary>
        /// Creates a point which can be used as a feedback object and is displayed with the given <paramref name="pointSymbol"/>.
        /// The point uses the feedback color ("Colors.Feedback") as specified in the project settings. Construction actions
        /// typically add the returned point to their feedback to mark positions they are working with.
        /// </summary>
        /// <param name="frame">Frame to get access to the settings</param>
        /// <param name="pointSymbol">The symbol with which the point is displayed</param>
        /// <returns>A <see cref="GeoObject.Point"/> as a feedback object</returns>
        static public CADability.GeoObject.Point FeedbackPoint(IFrame frame, PointSymbol pointSymbol)
        {
            CADability.GeoObject.Point gPoint = CADability.GeoObject.Point.Construct();
            gPoint.Symbol = pointSymbol;
            Color backColor = frame.GetColorSetting("Colors.Feedback", Color.DarkGray);
            gPoint.ColorDef = new ColorDef("", backColor);
            return gPoint;
        }
    }
}

