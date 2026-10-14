using CADability.GeoObject;
using CADability.UserInterface;

namespace CADability.Actions
{
    /// <summary>
    /// Construction of a polyline through any number of points picked by the user; a boolean input decides whether the
    /// polyline is closed. Menu id: "Constr.Polyline".
    /// </summary>
    internal class ConstrPolylinePoints : ConstructAction, IIndexedGeoPoint
    {
        private Polyline polyline;
        public ConstrPolylinePoints()
        {
            polyline = Polyline.Construct();
        }
        public override void OnSetAction()
        {
            base.TitleId = "Constr.Polyline";
            base.ActiveObject = polyline;
            // BooleanInput bi = new BooleanInput("Constr.BSpline.Mode","Constr.BSpline.OpenClosed");
            MultiPointInput mi = new MultiPointInput(this);
            mi.ResourceId = "Constr.Polyline.Points";
            ConstructAction.BooleanInput bi = new BooleanInput("Constr.Polyline.OpenClosed", "Polyline.OpenClosed.Values");
            bi.GetBooleanEvent += new CADability.Actions.ConstructAction.BooleanInput.GetBooleanDelegate(OnGetClosed);
            bi.SetBooleanEvent += new CADability.Actions.ConstructAction.BooleanInput.SetBooleanDelegate(OnSetClosed);
            base.SetInput(mi, bi);
            base.ShowAttributes = true;

            base.OnSetAction();
        }
        public override string GetID()
        {
            return "Constr.Polyline";
        }
        #region IIndexedGeoPoint Members
        public void SetGeoPoint(int Index, GeoPoint ThePoint)
        {
            if (Index >= polyline.Vertices.Length) polyline.AddPoint(ThePoint);
            else polyline.SetPoint(Index, ThePoint);
        }

        public GeoPoint GetGeoPoint(int Index)
        {
            return polyline.GetPoint(Index);
        }

        public void InsertGeoPoint(int Index, GeoPoint ThePoint)
        {
            if (Index == -1)
            {
                polyline.AddPoint(ThePoint);
            }
            else
            {
                polyline.InsertPoint(Index, ThePoint);
            }
        }

        public void RemoveGeoPoint(int Index)
        {
            if (Index == -1) Index = polyline.PointCount - 1;
            polyline.RemovePoint(Index);
        }

        public int GetGeoPointCount()
        {
            return polyline.PointCount;
        }
        bool IIndexedGeoPoint.MayInsert(int Index)
        {
            return true;
        }
        bool IIndexedGeoPoint.MayDelete(int Index)
        {
            return true;
        }
        #endregion

        private bool OnGetClosed()
        {
            if (polyline != null) return polyline.IsClosed;
            return false;
        }

        private void OnSetClosed(bool val)
        {
            if (polyline != null) polyline.IsClosed = val;
        }
        public override void OnDone()
        {
            base.OnDone();
            polyline.RemoveDoublePoints();
        }
        public override void OnInactivate(Action NewActiveAction, bool RemovingAction)
        {
            if (NewActiveAction != null && NewActiveAction.GetID().StartsWith("Constr.") && polyline.Vertices.Length > 1)
            {
                polyline.RemoveDoublePoints();
                if (polyline.HasValidData()) CurrentMouseView.Model.Add(polyline);
            }
            base.OnInactivate(NewActiveAction, RemovingAction);
        }
        public override bool OnCommand(string MenuId)
        {
            if (MenuId == "MenuId.Select.Notify")
            {   // wenn der Anwender auf "selektieren" geht, wird diese polylinie noch eingefügt
                // könnte auch bei anderen Aktionen so gemacht werden, z.B. bei mehrfacher Punktbemaßung
                if (polyline.Vertices.Length > 1)
                {
                    polyline.RemoveDoublePoints();
                    if (polyline.HasValidData()) CurrentMouseView.Model.Add(polyline);
                }
            }
            return base.OnCommand(MenuId);
        }
    }
}

