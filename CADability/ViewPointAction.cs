using CADability.UserInterface;
using System.Drawing;
using Point = System.Drawing.Point;
using System;

using MouseEventArgs = CADability.Substitutes.MouseEventArgs;
using DragEventArgs = CADability.Substitutes.DragEventArgs;
using MouseButtons = CADability.Substitutes.MouseButtons;
using DragDropEffects = CADability.Substitutes.DragDropEffects;
using Keys = CADability.Substitutes.Keys;

namespace CADability.Actions
{
    class ViewPointAction : Action, IIntermediateConstruction
    {
        private Point lastPanPosition;
        ModelView modelView; // dort wo das MouseDown stattgefunden hat
        public override string GetID()
        {
            return "ViewPoint[LeaveSelectProperties]";
        }
        public override void OnMouseDown(MouseEventArgs e, IView vw)
        {
            lastPanPosition = new Point(e.X, e.Y);
            modelView = vw as ModelView; // nur da soll gedreht werden
        }
        public override void OnMouseMove(MouseEventArgs e, IView vw)
        {
            if (e.Button != MouseButtons.Left) return;
            if (modelView == null) return;
            int HOffset = e.X - lastPanPosition.X;
            int VOffset = e.Y - lastPanPosition.Y;
            if (VOffset != 0 || HOffset != 0)
            {
                Projection Projection = vw.Projection;
                lastPanPosition = new Point(e.X, e.Y);
                GeoVector haxis = Projection.InverseProjection * GeoVector.XAxis;
                GeoVector vaxis = Projection.InverseProjection * GeoVector.YAxis;
                ModOp mh = ModOp.Rotate(vaxis, SweepAngle.Deg(HOffset / 5.0));
                ModOp mv = ModOp.Rotate(haxis, SweepAngle.Deg(VOffset / 5.0));

                ModOp project = Projection.UnscaledProjection * mv * mh;
                // jetzt noch die Z-Achse einrichten. Die kann senkrecht nach oben oder unten
                // zeigen. Wenn die Z-Achse genau auf den Betrachter zeigt oder genau von ihm weg,
                // dann wird keine Anpassung vorgenommen, weils sonst zu sehr wackelt
                GeoVector z = project * GeoVector.ZAxis;
                if (modelView.ZAxisUp)
                {
                    const double mindeg = 0.05; // nur etwas aufrichten, aber in jedem Durchlauf
                    if (z.y < -0.1)
                    {   // Z-Achse soll nach unten zeigen
                        Angle a = new Angle(-GeoVector2D.YAxis, new GeoVector2D(z.x, z.y));
                        if (a.Radian > mindeg) a.Radian = mindeg;
                        if (a.Radian < -mindeg) a.Radian = -mindeg;
                        if (z.x < 0)
                        {
                            project = project * ModOp.Rotate(vaxis ^ haxis, -a.Radian);
                        }
                        else
                        {
                            project = project * ModOp.Rotate(vaxis ^ haxis, a.Radian);
                        }
                        z = project * GeoVector.ZAxis;
                    }
                    else if (z.y > 0.1)
                    {
                        Angle a = new Angle(GeoVector2D.YAxis, new GeoVector2D(z.x, z.y));
                        if (a.Radian > mindeg) a.Radian = mindeg;
                        if (a.Radian < -mindeg) a.Radian = -mindeg;
                        if (z.x < 0)
                        {
                            project = project * ModOp.Rotate(vaxis ^ haxis, a.Radian);
                        }
                        else
                        {
                            project = project * ModOp.Rotate(vaxis ^ haxis, -a.Radian);
                        }
                        z = project * GeoVector.ZAxis;
                    }
                }
                // Fixpunkt bestimmen
                Point clcenter = vw.Canvas.ClientRectangle.Location;
                clcenter.X += vw.Canvas.ClientRectangle.Width / 2;
                clcenter.Y += vw.Canvas.ClientRectangle.Height / 2;
                // ium Folgenden ist Temporary auf true zu setzen und ein Mechanismus zu finden
                // wie der QuadTree von ProjektedModel berechnet werden soll
                //GeoVector newDirection = mv * mh * Projection.Direction;
                //GeoVector oldDirection = Projection.Direction;
                //GeoVector perp = oldDirection ^ newDirection;

                GeoPoint fixpoint;
                //if (Settings.GlobalSettings.ContainsSetting("ViewFixPoint"))
                if (modelView != null && modelView.FixPointValid)
                {
                    //fixpoint = (GeoPoint)Settings.GlobalSettings.GetValue("ViewFixPoint");
                    fixpoint = modelView.FixPoint;
                }
                else
                {
                    fixpoint = Projection.UnProject(clcenter);
                }
                modelView.SetViewDirection(project, fixpoint, true);
                if (Math.Abs(HOffset) > Math.Abs(VOffset))
                {
                    if (HOffset > 0) vw.Canvas.Cursor = "PanEast";
                    else vw.Canvas.Cursor = "PanWest";
                }
                else
                {
                    if (HOffset > 0) vw.Canvas.Cursor = "PanSouth";
                    else vw.Canvas.Cursor = "PanNorth";
                }
                modelView.projectedModelNeedsRecalc = false;
            }
        }
        public override void OnMouseUp(MouseEventArgs e, IView vw)
        {
            base.RemoveThisAction();
        }
        public override void OnInactivate(Action NewActiveAction, bool RemovingAction)
        {
            if (modelView != null) modelView.projectedModelNeedsRecalc = true;
            if (!RemovingAction) base.RemoveThisAction();
            base.OnInactivate(NewActiveAction, RemovingAction);
        }
        public override bool OnEscape()
        {
            base.RemoveThisAction();
            base.OnEscape();
            return true;
        }
#region IIntermediateConstruction Members
        IPropertyEntry IIntermediateConstruction.GetHandledProperty()
        {
            return null;
        }
        bool IIntermediateConstruction.Succeeded
        {
            get { return true; }
        }
#endregion
    }
}
