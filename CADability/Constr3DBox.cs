using CADability.GeoObject;
using System;

namespace CADability.Actions
{
    /// <summary>
    /// Construction of a box. Normally startpoint (left downer corner) width, length and height define the box in x-y-z-orientation.
    /// Definition of x-, y- or z-direction by defining a vector refering to the startpoint change the axis-parallel orientation of that box-axis
    /// </summary>
    internal class Constr3DBox : ConstructAction
    {
        public Constr3DBox()
        { }

        private Solid box;
        private GeoPoint boxStartPoint;
        private double boxLengthX;
        private double boxLengthY;
        private double boxLengthZ;
        private LengthInput width;
        private LengthInput length;
        private LengthInput height;
        private GeoVectorInput xVectorInput;
        private GeoVectorInput yVectorInput;
        private GeoVectorInput zVectorInput;
        private GeoVector boxDirX;
        private GeoVector boxDirY;
        private GeoVector boxDirZ;

        private void boxOrg(int sw)
        { // die manuelle Auswahl einer Vektor-Achseneingabe macht alle noch nicht bestimmten Längeneingaben optional, und die nocht nicht bestimmten Achseneingaben obligatorisch
            length.Optional = true;
            width.Optional = true;
            height.Optional = true;
            switch (sw)
            {
                case 1: // x-Achse
                    if (width.Fixed) yVectorInput.Optional = true;
                    else yVectorInput.Optional = false;
                    if (height.Fixed) zVectorInput.Optional = true;
                    else zVectorInput.Optional = false;
                    break;
                case 2: // y-Achse
                    if (length.Fixed) xVectorInput.Optional = true;
                    else xVectorInput.Optional = false;
                    if (height.Fixed) zVectorInput.Optional = true;
                    else zVectorInput.Optional = false;
                    break;
                case 3: // z-Achse
                    if (width.Fixed) yVectorInput.Optional = true;
                    else yVectorInput.Optional = false;
                    if (length.Fixed) xVectorInput.Optional = true;
                    else xVectorInput.Optional = false;
                    break;

                default:
                    break;
            }

        }


        private void StartPoint(GeoPoint p)
        {
            boxStartPoint = p;
            box = Make3D.MakeBox(boxStartPoint, boxDirX, boxDirY, boxDirZ);
            box.CopyAttributes(base.ActiveObject);
            base.ActiveObject = box;
        }


        private double LengthCalculate(GeoPoint MousePosition)
        {  // falls die Länge über einen Punkt im Raum über der jetzigen Box bestimmt wird:
            // der Lotfußpunkt von MousePosition auf die Ebene Y-Z
            double rh = (boxDirX ^ boxDirY) * boxDirZ;
            Plane pl;
            if (rh>0) pl = new Plane(boxStartPoint, boxDirY, boxDirZ);
            else pl = new Plane(boxStartPoint, boxDirZ, boxDirY);
            double l = pl.ToLocal(MousePosition).z;
            if (Math.Abs(l) > Precision.eps)
            {	// Neue Box 
                boxLengthX = l;
                box = Make3D.MakeBox(boxStartPoint, boxLengthX * boxDirX.Normalized, boxDirY, boxDirZ);
                box.CopyAttributes(base.ActiveObject);
                base.ActiveObject = box;
                // nun die Länge zurückliefern
                return boxLengthX;
            }
            return 0;
        }

        private bool Length(double length)
        {
            if (Math.Abs(length) > Precision.eps)
            {
                boxLengthX = length;
                box = Make3D.MakeBox(boxStartPoint, boxLengthX * boxDirX.Normalized, boxDirY, boxDirZ);
                box.CopyAttributes(base.ActiveObject);
                base.ActiveObject = box;
                return true;
            }
            return false;
        }

        double GetLength()
        {
            return (boxLengthX);
        }

        private double WidthCalculate(GeoPoint MousePosition)
        {  // falls die Breite über einen Punkt im Raum über der jetzigen Box bestimmt wird:
           // der Lotfußpunkt von MousePosition auf die Ebene Z-X
            double rh = (boxDirX ^ boxDirY) * boxDirZ;
            Plane pl;
            if (rh > 0) pl = new Plane(boxStartPoint, boxDirZ, boxDirX);
            else pl = new Plane(boxStartPoint, boxDirX, boxDirZ);
            double l = pl.ToLocal(MousePosition).z;
            if (Math.Abs(l) > Precision.eps)
            {	// Neue Box 
                boxLengthY = l;
                box = Make3D.MakeBox(boxStartPoint, boxDirX, boxLengthY * boxDirY.Normalized, boxDirZ);
                box.CopyAttributes(base.ActiveObject);
                base.ActiveObject = box;
                // nun die Länge zurückliefern
                return boxLengthY;
            }
            return 0;
        }

        private bool Width(double length)
        {
            if (Math.Abs(length) > Precision.eps)
            {
                boxLengthY = length;
                box = Make3D.MakeBox(boxStartPoint, boxDirX, boxLengthY * boxDirY.Normalized, boxDirZ);
                box.CopyAttributes(base.ActiveObject);
                base.ActiveObject = box;
                return true;
            }
            return false;
        }

        double GetWidth()
        {
            return (boxLengthY);
        }


        private double HeightCalculate(GeoPoint MousePosition)
        {  // if the length and the width of the box have already been defined, the height is determined along an axis
            // perpendicular to the base plane of the box in the center of the bas face
            double rh = (boxDirX ^ boxDirY) * boxDirZ;
            Plane pl;
            if (rh > 0) pl = new Plane(boxStartPoint, boxDirX, boxDirY);
            else pl = new Plane(boxStartPoint, boxDirY, boxDirX);
            GeoPoint cnt = boxStartPoint + 0.5 * boxDirX + 0.5 * boxDirY;
            GeoVector perp = pl.Normal.Normalized;
            Axis beamFromMouse = base.CurrentMouseView.Projection.PointBeam(base.CurrentMousePoint);
            Geometry.DistLL(cnt, perp, beamFromMouse.Location, beamFromMouse.Direction, out double par1, out double par2);
            double l = par1;
            if (l != double.MaxValue)
            {	// Neue Box 
                boxLengthZ = l;
                box = Make3D.MakeBox(boxStartPoint, boxDirX, boxDirY, boxLengthZ * boxDirZ.Normalized);
                box.CopyAttributes(base.ActiveObject);
                base.ActiveObject = box;
                // nun die Länge zurückliefern
                return boxLengthZ;
            }
            return 0;
        }

        private bool Height(double length)
        {
            if (Math.Abs(length) > Precision.eps)
            {
                boxLengthZ = length;
                box = Make3D.MakeBox(boxStartPoint, boxDirX, boxDirY, boxLengthZ * boxDirZ.Normalized);
                box.CopyAttributes(base.ActiveObject);
                base.ActiveObject = box;
                return true;
            }
            return false;
        }

        double GetHeight()
        {
            return (boxLengthZ);
        }



        private bool XVector(GeoVector v)
        {
            if (!v.IsNullVector())
            {
                if (!Precision.SameDirection(v, boxDirY, false) && !Precision.SameDirection(v, boxDirZ, false))
                {
                    boxDirX = v;
                    boxLengthX = boxDirX.Length;
                    box = Make3D.MakeBox(boxStartPoint, boxDirX, boxDirY, boxDirZ);
                    box.CopyAttributes(base.ActiveObject);
                    base.ActiveObject = box;
                    boxOrg(1);
                    return true;
                }
            }
            return false;
        }

        GeoVector GetXVector()
        {
            return (boxDirX);
        }

        private bool YVector(GeoVector v)
        {
            if (!v.IsNullVector())
            {
                if (!Precision.SameDirection(v, boxDirX, false) && !Precision.SameDirection(v, boxDirZ, false))
                {
                    boxDirY = v;
                    boxLengthY = boxDirY.Length;
                    box = Make3D.MakeBox(boxStartPoint, boxDirX, boxDirY, boxDirZ);
                    box.CopyAttributes(base.ActiveObject);
                    base.ActiveObject = box;
                    boxOrg(2);
                    return true;
                }
            }
            return false;
        }
        GeoVector GetYVector()
        {
            return (boxDirY);
        }

        private bool ZVector(GeoVector v)
        {
            if (!v.IsNullVector())
            {
                if (!Precision.SameDirection(v, boxDirX, false) && !Precision.SameDirection(v, boxDirY, false))
                {
                    boxDirZ = v;
                    boxLengthZ = boxDirZ.Length;
                    box = Make3D.MakeBox(boxStartPoint, boxDirX, boxDirY, boxDirZ);
                    box.CopyAttributes(base.ActiveObject);
                    base.ActiveObject = box;
                    boxOrg(3);
                    return true;
                }
            }
            return false;
        }

        GeoVector GetZVector()
        {
            return (boxDirZ);
        }



        public override void OnSetAction()
        {
            box = Solid.Construct();
            base.BasePoint = ConstrDefaults.DefaultStartPoint;
            boxLengthX = Math.Abs(ConstrDefaults.DefaultRectWidth);
            boxLengthY = Math.Abs(ConstrDefaults.DefaultRectHeight);
            boxLengthZ = Math.Abs(ConstrDefaults.DefaultBoxHeight);
            boxDirX = boxLengthX * base.ActiveDrawingPlane.DirectionX;
            boxDirY = boxLengthY * base.ActiveDrawingPlane.DirectionY;
            boxDirZ = boxLengthZ * (base.ActiveDrawingPlane.DirectionX ^ base.ActiveDrawingPlane.DirectionY);
            // construct a box (cuboid) parallel to the axis of the drawing plane
            // the boxDirX and boxDirY should point away from the user, so the startPoint is not hidden by the box
            if (CurrentMouseView.Projection.Direction * boxDirX < 0) boxDirX = -boxDirX;
            if (CurrentMouseView.Projection.Direction * boxDirY < 0) boxDirY = -boxDirY;
            box = Make3D.MakeBox(ConstrDefaults.DefaultStartPoint, boxDirX, boxDirY, boxDirZ);

            base.ActiveObject = box;
            base.TitleId = "Constr.Box";

            GeoPointInput startPointInput = new GeoPointInput("Constr.Box.StartPoint");
            startPointInput.DefaultGeoPoint = ConstrDefaults.DefaultStartPoint;
            startPointInput.DefinesBasePoint = true;
            startPointInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(StartPoint);

            length = new LengthInput("Constr.Box.Length");
            length.DefaultLength = ConstrDefaults.DefaultRectWidth;
            length.SetLengthEvent += new ConstructAction.LengthInput.SetLengthDelegate(Length);
            length.GetLengthEvent += new LengthInput.GetLengthDelegate(GetLength);
            length.CalculateLengthEvent += new CADability.Actions.ConstructAction.LengthInput.CalculateLengthDelegate(LengthCalculate);
            length.ForwardMouseInputTo = startPointInput;

            width = new LengthInput("Constr.Box.Width");
            width.DefaultLength = ConstrDefaults.DefaultRectHeight;
            width.SetLengthEvent += new ConstructAction.LengthInput.SetLengthDelegate(Width);
            width.GetLengthEvent += new LengthInput.GetLengthDelegate(GetWidth);
            width.CalculateLengthEvent += new CADability.Actions.ConstructAction.LengthInput.CalculateLengthDelegate(WidthCalculate);
            width.ForwardMouseInputTo = startPointInput;

            height = new LengthInput("Constr.Box.Height");
            height.DefaultLength = ConstrDefaults.DefaultBoxHeight;
            height.SetLengthEvent += new ConstructAction.LengthInput.SetLengthDelegate(Height);
            height.GetLengthEvent += new LengthInput.GetLengthDelegate(GetHeight);
            height.CalculateLengthEvent += new LengthInput.CalculateLengthDelegate(HeightCalculate);
            height.ForwardMouseInputTo = startPointInput;


            xVectorInput = new GeoVectorInput("Constr.Box.X-Direction");
            xVectorInput.SetGeoVectorEvent += new ConstructAction.GeoVectorInput.SetGeoVectorDelegate(XVector);
            xVectorInput.GetGeoVectorEvent += new GeoVectorInput.GetGeoVectorDelegate(GetXVector);
            xVectorInput.ForwardMouseInputTo = startPointInput;
            xVectorInput.Optional = true;

            yVectorInput = new GeoVectorInput("Constr.Box.Y-Direction");
            yVectorInput.SetGeoVectorEvent += new ConstructAction.GeoVectorInput.SetGeoVectorDelegate(YVector);
            yVectorInput.GetGeoVectorEvent += new GeoVectorInput.GetGeoVectorDelegate(GetYVector);
            yVectorInput.ForwardMouseInputTo = startPointInput;
            yVectorInput.Optional = true;

            zVectorInput = new GeoVectorInput("Constr.Box.Z-Direction");
            zVectorInput.SetGeoVectorEvent += new ConstructAction.GeoVectorInput.SetGeoVectorDelegate(ZVector);
            zVectorInput.GetGeoVectorEvent += new GeoVectorInput.GetGeoVectorDelegate(GetZVector);
            zVectorInput.ForwardMouseInputTo = startPointInput;
            zVectorInput.Optional = true;

            SeparatorInput separatorLengths = new SeparatorInput("Constr.Box.SeparatorLengths");
            SeparatorInput separatorDirections = new SeparatorInput("Constr.Box.SeparatorDirections");


            base.SetInput(startPointInput, separatorLengths, length, width, height, separatorDirections, xVectorInput, yVectorInput, zVectorInput);
            base.ShowAttributes = true;

            base.OnSetAction();
        }



        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
        }

        public override string GetID()
        {
            return "Constr.Box";
        }

        public override void OnDone()
        {
            ConstrDefaults.DefaultStartPoint.Point = boxStartPoint + boxDirX;
            // wird auf den Eckpunkt gesetzt
            if (base.ActiveObject != null)
            {
                KeepAsLastStyle(ActiveObject);
                using (base.Frame.Project.Undo.UndoFrame)
                {
                    if (Frame.SolidModellingMode == Make3D.SolidModellingMode.unite)
                    { // vereinigen, wenn möglich
                        base.Frame.Project.GetActiveModel().UniteSolid(base.ActiveObject as Solid, false);
                        base.ActiveObject = null;
                    }
                    if (Frame.SolidModellingMode == Make3D.SolidModellingMode.subtract)
                    { // abziehen, wenn möglich, sonst: nichts tun
                        base.Frame.Project.GetActiveModel().RemoveSolid(base.ActiveObject as Solid, false);
                        base.ActiveObject = null;
                    }
                }
            }
            base.OnDone();
        }

    }
}
