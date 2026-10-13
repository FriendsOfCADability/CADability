using CADability.GeoObject;
using CADability.UserInterface;
using System.Collections;

namespace CADability.Actions
{
    /// <summary>
    /// Construction of a <see cref="BSpline"/> that passes through a sequence of points. The user enters any number of
    /// points (points can also be inserted into or removed from the list) and chooses whether the spline is open or
    /// closed; the degree is taken from the setting "Constr.BSpline.Degree" (default 3). Menu id: "Constr.BSpline".
    /// </summary>
    internal class ConstrBSplinePoints : ConstructAction, IIndexedGeoPoint
    {
        private ArrayList points;
        private bool closed;
        private BSpline bSpline;
        private int degree;
        public ConstrBSplinePoints()
        {
            points = new ArrayList();
            bSpline = BSpline.Construct();
            bSpline.ShowPoints(true, false);
            closed = false;
            degree = 3; // könnte auch über einen IntInput gesetzt werden
            degree = Settings.GlobalSettings.GetIntValue("Constr.BSpline.Degree", 3);
        }
        public override void OnSetAction()
        {
            base.TitleId = "Constr.BSpline";
            base.ActiveObject = bSpline;
            BooleanInput bi = new BooleanInput("Constr.BSpline.Mode", "Constr.BSpline.Mode.Values");
            // bi Anzeige: open ist true!
            bi.GetBooleanEvent += new CADability.Actions.ConstructAction.BooleanInput.GetBooleanDelegate(OnGetClosed);
            bi.SetBooleanEvent += new CADability.Actions.ConstructAction.BooleanInput.SetBooleanDelegate(OnSetClosed);
            MultiPointInput mi = new MultiPointInput(this);
            mi.ResourceId = "Constr.BSpline.Points";
            base.SetInput(mi, bi);
            base.ShowAttributes = true;

            base.OnSetAction();
        }
        public override string GetID()
        {
            return "Constr.BSpline";
        }
        #region IIndexedGeoPoint Members
        public void SetGeoPoint(int Index, GeoPoint ThePoint)
        {
            points[Index] = ThePoint;
            bSpline.ThroughPoints((GeoPoint[])points.ToArray(typeof(GeoPoint)), degree, closed);
        }
        public GeoPoint GetGeoPoint(int Index)
        {
            return (GeoPoint)points[Index];
        }
        public void InsertGeoPoint(int Index, GeoPoint ThePoint)
        {
            if (Index == -1)
            {
                points.Add(ThePoint);
            }
            else
            {
                points.Insert(Index, ThePoint);
            }
            bSpline.ThroughPoints((GeoPoint[])points.ToArray(typeof(GeoPoint)), degree, closed);
        }
        public void RemoveGeoPoint(int Index)
        {
            if (Index == -1) Index = points.Count - 1;
            points.RemoveAt(Index);
            bSpline.ThroughPoints((GeoPoint[])points.ToArray(typeof(GeoPoint)), degree, closed);
        }
        public int GetGeoPointCount()
        {

            return points.Count;
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

        public override void OnDone()
        {
            bSpline.ShowPoints(false, false);
            if (points.Count <= 1) base.ActiveObject = null;
            base.OnDone();
        }

        private bool OnGetClosed()
        {
            return closed;
        }

        private void OnSetClosed(bool val)
        {
            closed = val;
            bSpline.ThroughPoints((GeoPoint[])points.ToArray(typeof(GeoPoint)), degree, closed);
        }
    }
}
