﻿#if !WEBASSEMBLY
using CADability.Attribute;
using CADability.Shapes;
using System;
using System.Collections;
using System.Drawing.Printing;
using System.Runtime.Serialization;

namespace CADability
{
    /// <summary>
    /// A Layout defines the placement of one or more patches on a
    /// paper. A patch is the projection of a model into the two dimensional space with respect to scaling, projection direction,
    /// visibility of layers etc. 
    /// A Layout can be viewed with the LayoutView and can be printed.
    /// </summary>
    [Serializable]
    public class Layout : ISerializable
    {
        private LayoutPatch[] patches;
        private string name; // Name des Layouts
        private double paperWidth;
        private double paperHeight;
        /// <summary>
        /// The printer settings.
        /// </summary>
        public PageSettings pageSettings; // kommt vom PrintDocument, kann auch null sein, ist Serializable
        internal Project project;
        /// <summary>
        /// Creates an empty Layout
        /// </summary>
        /// <param name="project">The <see cref="Project"/> containing the layout</param>
        public Layout(Project project)
        {
            this.project = project;
            patches = new LayoutPatch[0];
        }
        ~Layout()
        {
        }

        internal LayoutPatch[] Patches
        {
            get
            {
                return patches;
            }
        }

        /// <summary>
        /// Adds a "patch" to the layout. A patch is a a model with a certain
        /// projection placed on a section of the layout. The projection includes
        /// the placement of the model inside the section and the scaling.
        /// </summary>
        /// <param name="model">The model for the patch</param>
        /// <param name="projection">the projection for the model</param>
        /// <param name="area">The area on the layout</param>
        public void AddPatch(Model model, Projection projection, Border area)
        {
            AddPatch(model, projection, area, null);
        }
        public void AddPatch(Model model, Projection projection, Border area, LayoutView layoutView)
        {
            ArrayList al = new ArrayList(patches);
            LayoutPatch newPatch = new LayoutPatch(model, projection, area);
            foreach (Layer l in project.LayerList)
            {	// alle Layer sichtbar
                newPatch.visibleLayers[l] = null;
            }
            al.Add(newPatch);
            if (layoutView != null) newPatch.Connect(this, project, layoutView);
            patches = (LayoutPatch[])al.ToArray(typeof(LayoutPatch));
        }
        /// <summary>
		/// Removes the patch with the given index
		/// </summary>
		/// <param name="index"></param>
		public void RemovePatch(int index)
        {
            ArrayList al = new ArrayList(patches);
            al.RemoveAt(index);
            patches = (LayoutPatch[])al.ToArray(typeof(LayoutPatch));
        }
        internal void RemovePatch(LayoutPatch toRemove)
        {
            ArrayList al = new ArrayList(patches);
            al.Remove(toRemove);
            patches = (LayoutPatch[])al.ToArray(typeof(LayoutPatch));
        }
        /// <summary>
		/// Returns the number of the patches
		/// </summary>
		public int PatchCount
        {
            get { return patches.Length; }
        }
        /// <summary>
        /// Returns the data that describe the patch with the given index.
        /// </summary>
        /// <param name="index">Index of the patch</param>
        /// <param name="model">The model for the patch</param>
        /// <param name="projection">the projection for the model</param>
        /// <param name="area">The area on the layout</param>
        public void GetPatch(int index, out Model model, out Projection projection, out Border area)
        {
            model = patches[index].Model;
            projection = patches[index].Projection;
            area = patches[index].Area;
        }
        /// <summary>
        /// Changes the data of the patch with the given index.
        /// </summary>
        /// <param name="index">Index of the patch</param>
        /// <param name="model">The model for the patch</param>
        /// <param name="projection">the projection for the model</param>
        /// <param name="area">The area on the layout</param>
        public void SetPatch(int index, Model model, Projection projection, Border area)
        {
            patches[index].Model = model;
            patches[index].Projection = projection;
            patches[index].Area = area;
        }
        /// <summary>
        /// Horizontal positioning
        /// </summary>
        public enum HorizontalCenter { left, center, right, unchanged }
        /// <summary>
        /// Vertical positioning
        /// </summary>
        public enum VerticalCenter { bottom, center, top, unchanged }
        /// <summary>
        /// Centers the patch with the given index according to the horizontal and vertical 
        /// center mode. If scale is 0.0 the current scaling remains unchanged.
        /// </summary>
        /// <param name="index">index of the patch</param>
        /// <param name="scale">scaling factor or 0.0</param>
        /// <param name="hor">horizontal position mode</param>
        /// <param name="ver">vertical position mode</param>
        public void CenterPatch(int index, double scale, HorizontalCenter hor, VerticalCenter ver)
        {
            CenterPatch(patches[index], scale, hor, ver);
        }
        internal void CenterPatch(LayoutPatch patch, double scale, HorizontalCenter hor, VerticalCenter ver)
        {
            // die Projektion ist ja zwei Anteile, unscaledProjection hält immer den Nullpunkt
            // fest und skaliert nicht, und Placement platziert
            BoundingRect areaext;
            if (patch.Area != null)
            {
                areaext = patch.Area.Extent;
            }
            else
            {
                areaext = new BoundingRect(0.0, 0.0, paperWidth, paperHeight);
            }
            BoundingRect modelext = patch.Model.GetExtent(patch.Projection);
            if (modelext.IsEmpty()) return;
            GeoPoint2D modelcnt = modelext.GetCenter();
            GeoPoint2D areacnt = areaext.GetCenter();
            double factor, dx, dy;
            patch.Projection.GetPlacement(out factor, out dx, out dy);
            if (scale != 0.0) factor = scale;
            switch (hor)
            {
                case HorizontalCenter.left:
                    dx = areaext.Left - (modelext.Left * factor);
                    break;
                case HorizontalCenter.center:
                    dx = areacnt.x - (modelcnt.x * factor);
                    break;
                case HorizontalCenter.right:
                    dx = areaext.Right - (modelext.Right * factor);
                    break;
                default:
                    break;
            }
            switch (ver)
            {
                case VerticalCenter.bottom:
                    dy = areaext.Bottom - (modelext.Bottom * factor);
                    break;
                case VerticalCenter.center:
                    dy = areacnt.y - (modelcnt.y * factor);
                    break;
                case VerticalCenter.top:
                    dy = areaext.Top - (modelext.Top * factor);
                    break;
                default:
                    break;
            }
            patch.Projection.SetPlacement(factor, dx, dy);
        }
        /// <summary>
        /// Places the patch on the paper leaving the scalinfactor unchanged. The parameters 
        /// specify the position of the model origin on the paper.
        /// </summary>
        /// <param name="xPos">horizontal position of the origin</param>
        /// <param name="yPos">vertical position of the origin</param>
        public void MovePatch(int index, double xPos, double yPos)
        {
            MovePatch(patches[index], xPos, yPos);
        }
        internal void MovePatch(LayoutPatch patch, double xPos, double yPos)
        {
            double factor, dx, dy;
            patch.Projection.GetPlacement(out factor, out dx, out dy);
            patch.Projection.SetPlacement(factor, dx + xPos, dy + yPos);
        }
        /// <summary>
        /// Marks the given Layer as visible in the context of this ProjectedModel.
        /// </summary>
        /// <param name="l">The layer</param>
        public void AddVisibleLayer(int index, Layer l)
        {
            patches[index].visibleLayers[l] = null;
        }
        /// <summary>
        /// Marks the given Layer as invisible in the context of this ProjectedModel.
        /// </summary>
        /// <param name="l">The layer</param>
        public void RemoveVisibleLayer(int index, Layer l)
        {
            patches[index].visibleLayers.Remove(l);
        }
        /// <summary>
        /// Determins whether the given <see cref="Layer"/> is marked visible in the context of this ProjectedModel.
        /// </summary>
        /// <param name="l">The layer</param>
        public bool IsLayerVisible(int index, Layer l)
        {
            // if (patches[index].visibleLayers.Count==0) return true;
            if (l == null) return true;
            return patches[index].visibleLayers.ContainsKey(l);
        }
        /// <summary>
        /// Gets or sets the width of the total layout area. When printing the layout this 
        /// is assumed to be in mm
        /// </summary>
        public double PaperWidth
        {
            get
            {
                return paperWidth;
            }
            set
            {
                paperWidth = value;
            }
        }
        /// <summary>
        /// Gets or sets the height of the total layout area. When printing the layout this 
        /// is assumed to be in mm
        /// </summary>
        public double PaperHeight
        {
            get
            {
                return paperHeight;
            }
            set
            {
                paperHeight = value;
            }
        }
        /// <summary>
        /// Gets or sets the name of the layout.
        /// </summary>
        public string Name
        {
            get
            {
                return name;
            }
            set
            {
                if (name == value)
                {
                    return;
                }
                if (project.FindLayoutName(value))
                {
                    throw new NameAlreadyExistsException(project, this, value);
                }
                name = value;
            }
        }
#region ISerializable Members
        protected Layout(SerializationInfo info, StreamingContext context)
        {
            SerializationInfoEnumerator e = info.GetEnumerator();
            while (e.MoveNext()) // to avoid exceptions
            {
                switch (e.Name)
                {
                    case "Name":
                        name = e.Value as string;
                        break;
                    case "Patches":
                        patches = e.Value as LayoutPatch[];
                        break;
                    case "paperWidth":
                        paperWidth = (double)e.Value;
                        break;
                    case "paperHeight":
                        paperHeight = (double)e.Value;
                        break;
                    case "PageSettings":
                        pageSettings = e.Value as PageSettings;
                        break;
                }
            }


        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Name", name, typeof(string));
            info.AddValue("Patches", patches, typeof(LayoutPatch[]));
            info.AddValue("paperWidth", paperWidth, typeof(double));
            info.AddValue("paperHeight", paperHeight, typeof(double));
            // if (pageSettings != null) info.AddValue("PageSettings", pageSettings); // no more page settings, they don' serialize
        }

        #endregion
    }
}
#endif