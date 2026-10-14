using CADability.GeoObject;
using System;


namespace CADability.Actions
{
    /// <summary>
    /// Construction of a parallelogram (a closed <see cref="Polyline"/>) from a corner point and two vectors that define
    /// the directions and lengths of its sides. Menu id: "Constr.Rect.Parallelogram".
    /// </summary>
    internal class ConstrRectParallelogram : ConstructAction
    {
        public ConstrRectParallelogram()
        { }

        private Polyline line;
        private GeoVectorInput xVectorInput;
        private GeoVectorInput yVectorInput;

        private void StartPoint(GeoPoint p)
        {
            //			line.ParallelogramLocation = p;
            if (xVectorInput.Fixed || yVectorInput.Fixed)
            {
                line.ParallelogramLocation = p;
            }
            else
            {
                double angSav = line.ParallelogramAngle;
                line.SetParallelogram(p, line.ParallelogramWidth * base.ActiveDrawingPlane.DirectionX, line.ParallelogramHeight * base.ActiveDrawingPlane.DirectionY);
                line.ParallelogramAngle = angSav;
            }
        }

        private bool XVector(GeoVector v)
        {
            if (!v.IsNullVector())
            {
                if (!Precision.SameDirection(v, line.ParallelogramSecondaryDirection, false))
                {
                    line.SetParallelogram(line.ParallelogramLocation, v, line.ParallelogramSecondaryDirection);
                    return true;
                }
            }
            return false;
        }

        private bool YVector(GeoVector v)
        {
            if (!v.IsNullVector())
            {
                if (!Precision.SameDirection(v, line.ParallelogramMainDirection, false))
                {
                    line.SetParallelogram(line.ParallelogramLocation, line.ParallelogramMainDirection, v);
                    return true;
                }
            }
            return false;
        }

        //		private void XPoint(GeoPoint p)
        //		{	
        //			if (!Precision.IsEqual(line.ParallelogramLocation,p)) 
        //			{
        //				GeoVector dirX = new GeoVector(line.ParallelogramLocation,p);
        //				if (!Precision.SameDirection(dirX,line.ParallelogramSecondaryDirection,false)) 
        //					line.SetParallelogram(line.ParallelogramLocation,dirX,line.ParallelogramSecondaryDirection);
        //	
        //			}
        //		}

        //		private void YPoint(GeoPoint p)
        //		{	
        //			if (!Precision.IsEqual(line.ParallelogramLocation,p)) 
        //			{	
        //				GeoVector dirY = new GeoVector(line.ParallelogramLocation,p);
        //				if (!Precision.SameDirection(dirY,line.ParallelogramMainDirection,false)) 
        //					line.SetParallelogram(line.ParallelogramLocation,line.ParallelogramMainDirection,dirY);
        //			}
        //		}

        public override void OnSetAction()
        {
            line = Polyline.Construct();
            line.SetParallelogram(ConstrDefaults.DefaultStartPoint, ConstrDefaults.DefaultRectWidth * base.ActiveDrawingPlane.DirectionX, ConstrDefaults.DefaultRectHeight * base.ActiveDrawingPlane.DirectionY);
            line.ParallelogramAngle = Math.PI / 3;
            base.BasePoint = ConstrDefaults.DefaultStartPoint;

            base.ActiveObject = line;
            base.TitleId = "Constr.Rect.Parallelogram";

            GeoPointInput startPointInput = new GeoPointInput("Constr.Rect.Parallelogram.StartPoint");
            startPointInput.DefaultGeoPoint = ConstrDefaults.DefaultStartPoint;
            startPointInput.DefinesBasePoint = true;
            startPointInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(StartPoint);

            xVectorInput = new GeoVectorInput("Constr.Rect.Parallelogram.X-Direction");
            xVectorInput.SetGeoVectorEvent += new ConstructAction.GeoVectorInput.SetGeoVectorDelegate(XVector);
            //			xVectorInput.IsAngle = true;
            //			xVectorInput.ForwardMouseInputTo = startPointInput;

            yVectorInput = new GeoVectorInput("Constr.Rect.Parallelogram.Y-Direction");
            yVectorInput.SetGeoVectorEvent += new ConstructAction.GeoVectorInput.SetGeoVectorDelegate(YVector);
            //			yVectorInput.IsAngle = true;
            //			yVectorInput.ForwardMouseInputTo = startPointInput;

            //			GeoPointInput xPointInput = new GeoPointInput("Constr.Rect.Parallelogram.X-Direction");
            //			xPointInput.OnSetGeoPoint += new ConstructAction.GeoPointInput.SetGeoPointDelegate(XPoint);
            //			GeoPointInput yPointInput = new GeoPointInput("Constr.Rect.Parallelogram.Y-Direction");
            //			yPointInput.OnSetGeoPoint += new ConstructAction.GeoPointInput.SetGeoPointDelegate(YPoint);

            base.SetInput(startPointInput, xVectorInput, yVectorInput);
            base.ShowAttributes = true;

            base.OnSetAction();
        }

        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
        }

        public override string GetID()
        {
            return "Constr.Rect.Parallelogram";
        }

        public override void OnDone()
        {
            ConstrDefaults.DefaultStartPoint.Point = line.ParallelogramLocation + line.ParallelogramMainDirection + line.ParallelogramSecondaryDirection;
            // wird auf den Diagonalpunkt gesetzt
            base.OnDone();
        }

    }
}

