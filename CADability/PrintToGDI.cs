#if !WEBASSEMBLY
using CADability.Attribute;
using CADability.Curve2D;
using CADability.GeoObject;
using CADability.Shapes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;

using Wintellect.PowerCollections;

namespace CADability
{

    internal abstract class IPrintItemImpl : IQuadTreeInsertable
    {
        bool printing, printed;
        Set<IPrintItemImpl> coveredObjects; // die liegen alle unter mir
        public IPrintItemImpl()
        {
            coveredObjects = new Set<IPrintItemImpl>();
            printing = false;
            printed = false;
        }
        public bool PrintAll(Graphics gr, bool doShading, HashSet<IPrintItemImpl> objectsToUse)
        {
            if (!objectsToUse.Contains(this)) return true;
            if (printed)
            {
                return true; // nicht nötig, ist schon gedruckt
            }
            if (printing)
            {
                printing = false;
                return false; // es ist zyklisch, d.h. die Menge muss neu aufgeteilt werden
            }
            printing = true;
            foreach (IPrintItemImpl item in coveredObjects)
            {
                if (!item.PrintAll(gr, doShading, objectsToUse))
                {
                    printing = false;
                    return false; // im rekursiven Aufruf wurde ein Zyklus festgestellt
                }
            }
            Print(gr, doShading); // nachdem die drunterliegenden gedruckt sind, dieses drucken
            printed = true; // das hier ist fertig, wird bedingungslos gedruckt
            printing = false;
            return true;
        }
#if DEBUG
        public GeoObjectList CheckAll(HashSet<IPrintItemImpl> objectsToUse)
        {
            if (!objectsToUse.Contains(this)) return null;
            if (printed)
            {
                return null; // nicht nötig, ist schon gedruckt
            }
            if (printing)
            {
                printing = false;
                IGeoObject go = this.Debug;
                go.UserData.Add("PrintItem", this);
                return new GeoObjectList(go); // es ist zyklisch, d.h. die Menge muss neu aufgeteilt werden
            }
            printing = true;
            foreach (IPrintItemImpl item in coveredObjects)
            {
                GeoObjectList l = item.CheckAll(objectsToUse);
                if (l != null)
                {
                    IGeoObject go = this.Debug;
                    go.UserData.Add("PrintItem", this);
                    l.Add(go);
                    return l;
                }
            }
            printed = true; // das hier ist fertig, wird bedingungslos gedruckt
            printing = false;
            return null;
        }
        public GeoObjectList DebugAll
        {
            get
            {
                GeoObjectList res = new GeoObjectList();
                res.Add(this.Debug);
                foreach (IPrintItemImpl item in coveredObjects)
                {
                    IGeoObject toAdd = item.Debug;
                    if (toAdd != null) res.Add(toAdd);
                }
                return res;
            }
        }
#endif
#region abstract Members
        public abstract void Print(Graphics gr, bool doShading);
        public abstract double ZMaximum
        {
            get;
        }
        public abstract double ZMinimum
        {
            get;
        }
        public abstract bool CommonPoint(IPrintItemImpl other, out GeoPoint2D commonPoint);
        public abstract bool CommonPoint(IPrintItemImpl other, BoundingRect rect, out GeoPoint2D commonPoint);
        public abstract int CompareCommonPointZ(BoundingRect rect, IPrintItemImpl other);
        public abstract double ZPositionAt(GeoPoint2D here);
        public abstract IPrintItemImpl Clone();
#if DEBUG
        public abstract IGeoObject Debug { get; }
#endif
#endregion
        public bool Printed
        {
            get
            {
                return printed;
            }
            set
            {
                printed = value;
            }
        }
        public bool Printing
        {
            get
            {
                return printing;
            }
            set
            {
                printing = value;
            }
        }
        private void AddCoveredObject(IPrintItemImpl above, IPrintItemImpl below)
        {
            //if (below.coveredObjects.Contains(above))
            //{
            //}
            above.coveredObjects.Add(below);
        }
        public void CheckCoverage(IPrintItemImpl other)
        {
            BoundingRect ext = this.GetExtent();
            BoundingRect oext = other.GetExtent();
            if (ext.Interferes(ref oext))
            {   // wenn sich ein Objekt völlig unterhalb des anderen befindet
                // dann kann man es auch zu den coveredObjects zählen, es schadet jedenfalls
                // nicht, auch wenn sie sich garnicht überlappen. Und es erspart die etwas aufwendigere
                // CommonPoint und ZPositionAt Abfrage
                // Test: das folgende liefert zu viele Überdeckungen, die sich letztlich in Schleifen äußern
                //if (other.ZMaximum < this.ZMinimum)
                //{
                //    AddCoveredObject(this, other);
                //}
                //else if (this.ZMaximum < other.ZMinimum)
                //{
                //    AddCoveredObject(other, this);
                //}
                //else
                {
                    GeoPoint2D cp;
                    if (CommonPoint(other, out cp))
                    {
                        double z1 = this.ZPositionAt(cp);
                        double z2 = other.ZPositionAt(cp);
                        if (Math.Abs(z2 - z1) < 1e-6)
                        {
                            if (this is PrintTriangle && other is PrintTriangle)
                            {

                                if (TriangleInnerIntersection((this as PrintTriangle).tp1.To2D(), (this as PrintTriangle).tp2.To2D(), (this as PrintTriangle).tp3.To2D(),
                                    (other as PrintTriangle).tp1.To2D(), (other as PrintTriangle).tp2.To2D(), (other as PrintTriangle).tp3.To2D(), out cp))
                                {
                                    z1 = this.ZPositionAt(cp);
                                    z2 = other.ZPositionAt(cp);
                                }
                                else return; // sollte nicht vorkommen
                            }
                        }
                        if (Math.Abs(z2 - z1) > 1e-3) // der Wert muss aus der Gesamtgröße kommen
                        {
                            if (z1 > z2) AddCoveredObject(this, other);
                            else AddCoveredObject(other, this);
                        }
                    }
                }
            }
        }
        public void CheckCoverage(IPrintItemImpl other, BoundingRect rect)
        {
            BoundingRect ext = this.GetExtent();
            BoundingRect oext = other.GetExtent();
            if (ext.Interferes(ref oext))
            {   // wenn sich ein Objekt völlig unterhalb des anderen befindet
                // dann kann man es auch zu den coveredObjects zählen, es schadet jedenfalls
                // nicht, auch wenn sie sich garnicht überlappen. Und es erspart die etwas aufwendigere
                // CommonPoint und ZPositionAt Abfrage
                // Test: das folgende liefert zu viele Überdeckungen, die sich letztlich in Schleifen äußern
                //if (other.ZMaximum < this.ZMinimum)
                //{
                //    AddCoveredObject(this, other);
                //}
                //else if (this.ZMaximum < other.ZMinimum)
                //{
                //    AddCoveredObject(other, this);
                //}
                //else
                {
                    GeoPoint2D cp;
                    if (CommonPoint(other, rect, out cp))
                    {
                        double z1 = this.ZPositionAt(cp);
                        double z2 = other.ZPositionAt(cp);
                        if (Math.Abs(z2 - z1) < 1e-6)
                        {
                            if (this is PrintTriangle && other is PrintTriangle)
                            {

                                if (TriangleInnerIntersection((this as PrintTriangle).tp1.To2D(), (this as PrintTriangle).tp2.To2D(), (this as PrintTriangle).tp3.To2D(),
                                    (other as PrintTriangle).tp1.To2D(), (other as PrintTriangle).tp2.To2D(), (other as PrintTriangle).tp3.To2D(), out cp))
                                {
                                    z1 = this.ZPositionAt(cp);
                                    z2 = other.ZPositionAt(cp);
                                }
                                else return; // sollte nicht vorkommen
                            }
                        }
                        if (Math.Abs(z2 - z1) > 1e-3) // der Wert muss aus der Gesamtgröße kommen
                        {
                            if (z1 > z2) AddCoveredObject(this, other);
                            else AddCoveredObject(other, this);
                        }
                    }
                }
            }
        }
        internal static bool IntersectLLparInside(GeoPoint2D StartPoint1, GeoPoint2D EndPoint1, GeoPoint2D StartPoint2, GeoPoint2D EndPoint2, out double pos1, out double pos2)
        {   // nur ganz echte innere Schnitte sind gesucht, am Ende die gelten nicht
            GeoVector2D Direction1 = EndPoint1 - StartPoint1;
            GeoVector2D Direction2 = EndPoint2 - StartPoint2;
            double d1 = Direction1.x * Direction2.y;
            double d2 = Direction2.x * Direction1.y;
            double dd = d1 - d2;
            if (Math.Abs(dd) > 1e-13)
            {
                pos1 = ((StartPoint2.y + Direction2.y - StartPoint1.y) * (StartPoint2.x - StartPoint1.x) - (StartPoint2.x + Direction2.x - StartPoint1.x) * (StartPoint2.y - StartPoint1.y)) / (dd);
                if (pos1 < 1e-13 || pos1 > 1 - 1e-13)
                {
                    pos2 = -1.0;
                    return false;
                }
                pos2 = ((StartPoint1.y + Direction1.y - StartPoint2.y) * (StartPoint1.x - StartPoint2.x) - (StartPoint1.x + Direction1.x - StartPoint2.x) * (StartPoint1.y - StartPoint2.y)) / (-dd);
                if (pos2 > 1e-13 && pos2 < 1 - 1e-13) return true;
            }
            pos1 = -1.0;
            pos2 = -1.0;
            return false;
        }
        protected bool LineIntersection(GeoPoint2D sp1, GeoPoint2D ep1, GeoPoint2D sp2, GeoPoint2D ep2, out GeoPoint2D commonPoint)
        {
            double pos1, pos2;
            if (IntersectLLparInside(sp1, ep1, sp2, ep2, out pos1, out pos2))
            {
                commonPoint = Geometry.LinePos(sp1, ep1, pos1);
                return true;
            }
            else
            {
                commonPoint = GeoPoint2D.Origin; // unnötigerweise
                return false;
            }
        }
        protected bool TriangleLineIntersection(GeoPoint2D tp1, GeoPoint2D tp2, GeoPoint2D tp3, GeoPoint2D sp, GeoPoint2D ep, out GeoPoint2D commonPoint)
        {
            ModOp2D toTr1 = new ModOp2D(tp2 - tp1, tp3 - tp1, tp1);
            commonPoint = GeoPoint2D.Origin; // damit der compiler nicht meckert
            try
            {
                ModOp2D fromTr1 = toTr1.GetInverse(); // ist relativ schnell
                GeoPoint2D spo = fromTr1 * sp;
                GeoPoint2D epo = fromTr1 * ep;
                // alle auf einer Seite: kein Schnitt
                if (spo.x < 1e-6 && epo.x < 1e-6)
                {
                    if (spo.x > -1e-6 && epo.x > -1e-6)
                    {   // genau die linke Kante
                        commonPoint = toTr1 * new GeoPoint2D(0.0, (Math.Max(0.0, Math.Min(spo.y, epo.y)) + Math.Min(1.0, Math.Max(spo.y, epo.y))) / 2.0);
                        return true; // die Linie liefert immer einen etwas höheren Wert und wird somit obendrüber dargestellt
                    }
                    return false;
                }
                if (spo.y < 1e-6 && epo.y < 1e-6)
                {
                    if (spo.y > -1e-6 && epo.y > -1e-6)
                    {   // genau die untere Kante
                        commonPoint = toTr1 * new GeoPoint2D((Math.Max(0.0, Math.Min(spo.x, epo.x)) + Math.Min(1.0, Math.Max(spo.x, epo.x))) / 2.0, 1.0);
                        return true; // die Linie liefert immer einen etwas höheren Wert und wird somit obendrüber dargestellt
                    }
                    return false;
                }
                if (spo.x + spo.y > 1 - 1e-6 && epo.x + epo.y > 1 - 1e-6)
                {
                    if (spo.x + spo.y < 1 + 1e-6 && epo.x + epo.y < 1 + 1e-6)
                    {
                        // genau auf der Diagonalen
                        double pos1 = Geometry.LinePar(tp2, tp3, sp);
                        double pos2 = Geometry.LinePar(tp2, tp3, ep);
                        double pos = Math.Max(0.0, Math.Min(pos1, pos2)) + Math.Min(1.0, Math.Max(pos1, pos2)) / 2.0;
                        commonPoint = Geometry.LinePos(tp2, tp3, pos); // mittlerer gemeinsamer Punkt
                        return true;
                    }
                    return false;
                }
                // ein Eckpunkt drin: diesen Eckpunkt liefern
                if (spo.x + spo.y < 1 - 1e-6 && spo.x > 1e-6 && spo.y > 1e-6)
                {
                    commonPoint = sp;
                    return true;
                }
                if (epo.x + epo.y < 1 - 1e-6 && epo.x > 1e-6 && epo.y > 1e-6)
                {
                    commonPoint = ep;
                    return true;
                }
            }
            catch (ModOpException)
            {   // Dreieck liegt senkrecht
                return false;
            }
            if (LineIntersection(tp1, tp2, sp, ep, out commonPoint)) return true;
            if (LineIntersection(tp2, tp3, sp, ep, out commonPoint)) return true;
            if (LineIntersection(tp3, tp1, sp, ep, out commonPoint)) return true;
            return false;
        }
        protected bool TriangleInnerIntersection(GeoPoint2D tp11, GeoPoint2D tp12, GeoPoint2D tp13, GeoPoint2D tp21, GeoPoint2D tp22, GeoPoint2D tp23, out GeoPoint2D commonPoint)
        {   // wird nur verwendet, wenn TriangleIntersection einen nicht eindeutigen Punkt geliefert hat. Dies ist meis ein Eckpunkt
            // des einen Dreiecks, welcher genau im anderen Dreieck liegt. Hier wird dagegen ein echter innerer Punkt gesucht
            // indem Rechtecke immer weiter verkleiner werden
            commonPoint = GeoPoint2D.Origin; // damit der compiler nicht meckert
            BoundingRect b1 = new BoundingRect(tp11, tp12, tp13);
            BoundingRect b2 = new BoundingRect(tp21, tp22, tp23);
            BoundingRect b = BoundingRect.Intersect(b1, b2);
            if (b.Width <= 0 || b.Height <= 0) return false;
            ModOp2D toTr1 = new ModOp2D(tp12 - tp11, tp13 - tp11, tp11);
            ModOp2D fromTr1 = toTr1.GetInverse(); // ist relativ schnell
            ModOp2D toTr2 = new ModOp2D(tp22 - tp21, tp23 - tp21, tp21);
            ModOp2D fromTr2 = toTr2.GetInverse(); // ist relativ schnell

            // wenn 2 Punkte identisch aber kein Schnitt fliegt dieser Test hier raus
            GeoPoint2D tp21o = fromTr1 * tp21;
            GeoPoint2D tp22o = fromTr1 * tp22;
            GeoPoint2D tp23o = fromTr1 * tp23;
            // alle auf einer Seite: kein Schnitt
            if (tp21o.x < 1e-6 && tp22o.x < 1e-6 && tp23o.x < 1e-6) return false;
            if (tp21o.y < 1e-6 && tp22o.y < 1e-6 && tp23o.y < 1e-6) return false;
            if (tp21o.x + tp21o.y > 1 - 1e-6 && tp22o.x + tp22o.y > 1 - 1e-6 && tp23o.x + tp23o.y > 1 - 1e-6) return false;

            GeoPoint2D tp11o = fromTr2 * tp11;
            GeoPoint2D tp12o = fromTr2 * tp12;
            GeoPoint2D tp13o = fromTr2 * tp13;
            // alle auf einer Seite: kein Schnitt
            if (tp11o.x < 1e-6 && tp12o.x < 1e-6 && tp13o.x < 1e-6) return false;
            if (tp11o.y < 1e-6 && tp12o.y < 1e-6 && tp13o.y < 1e-6) return false;
            if (tp11o.x + tp11o.y > 1 - 1e-6 && tp12o.x + tp12o.y > 1 - 1e-6 && tp13o.x + tp13o.y > 1 - 1e-6) return false;

            // hier also echte Überlappung:
            ClipRect bclr = new ClipRect(b);
            ClipRect[] clr = bclr.Split();
            do
            {
                List<ClipRect> rest = new List<ClipRect>();
                for (int i = 0; i < clr.Length; i++)
                {
                    GeoPoint2D cnt = fromTr1 * clr[i].Center;
                    if (cnt.x + cnt.y < 1 - 1e-6 && cnt.x > 1e-6 && cnt.y > 1e-6)
                    {   // Punkt ist im 1. Dreieck
                        cnt = fromTr2 * clr[i].Center;
                        if (cnt.x + cnt.y < 1 - 1e-6 && cnt.x > 1e-6 && cnt.y > 1e-6)
                        {   // Punkt ist im 2. Dreieck
                            commonPoint = clr[i].Center;
                            return true;
                        }
                    }
                    if (clr[i].TriangleHitTest(tp11, tp12, tp13) && clr[i].TriangleHitTest(tp21, tp22, tp23))
                    {
                        rest.AddRange(clr[i].Split());
                    }
                }
                if (rest.Count == 0 || rest.Count > 1024) return false;
                clr = rest.ToArray();
            } while (clr[0].Size > 1e-6);
            return false;
        }
        protected bool TriangleIntersection(GeoPoint2D tp11, GeoPoint2D tp12, GeoPoint2D tp13, GeoPoint2D tp21, GeoPoint2D tp22, GeoPoint2D tp23, out GeoPoint2D commonPoint)
        {
            // zuerste extend bemühen?
            // Dreieck in Einheitsdreieck umwandeln, dann ist leichter zu checken
            ModOp2D toTr1 = new ModOp2D(tp12 - tp11, tp13 - tp11, tp11);
            commonPoint = GeoPoint2D.Origin; // damit der compiler nicht meckert
            try
            {
                ModOp2D fromTr1 = toTr1.GetInverse(); // ist relativ schnell
                // DEBUG:
                GeoPoint2D dbg0 = fromTr1 * tp11;
                GeoPoint2D dbg1 = fromTr1 * tp12;
                GeoPoint2D dbg2 = fromTr1 * tp13;

                GeoPoint2D tp21o = fromTr1 * tp21;
                GeoPoint2D tp22o = fromTr1 * tp22;
                GeoPoint2D tp23o = fromTr1 * tp23;
                // alle auf einer Seite: kein Schnitt
                if (tp21o.x < 1e-6 && tp22o.x < 1e-6 && tp23o.x < 1e-6) return false;
                if (tp21o.y < 1e-6 && tp22o.y < 1e-6 && tp23o.y < 1e-6) return false;
                if (tp21o.x + tp21o.y > 1 - 1e-6 && tp22o.x + tp22o.y > 1 - 1e-6 && tp23o.x + tp23o.y > 1 - 1e-6) return false;
                // ein Eckpunkt drin: diesen Eckpunkt liefern
                if (tp21o.x + tp21o.y < 1 - 1e-6 && tp21o.x > 1e-6 && tp21o.y > 1e-6)
                {
                    commonPoint = tp21;
                    return true;
                }
                if (tp22o.x + tp22o.y < 1 - 1e-6 && tp22o.x > 1e-6 && tp22o.y > 1e-6)
                {
                    commonPoint = tp22;
                    return true;
                }
                if (tp23o.x + tp23o.y < 1 - 1e-6 && tp23o.x > 1e-6 && tp23o.y > 1e-6)
                {
                    commonPoint = tp23;
                    return true;
                }
                // jetzt der umgekehrte Test 
                ModOp2D toTr2 = new ModOp2D(tp22 - tp21, tp23 - tp21, tp21);
                ModOp2D fromTr2 = toTr2.GetInverse(); // ist relativ schnell

                GeoPoint2D tp11o = fromTr2 * tp11;
                GeoPoint2D tp12o = fromTr2 * tp12;
                GeoPoint2D tp13o = fromTr2 * tp13;
                // alle auf einer Seite: kein Schnitt
                if (tp11o.x < 1e-6 && tp12o.x < 1e-6 && tp13o.x < 1e-6) return false;
                if (tp11o.y < 1e-6 && tp12o.y < 1e-6 && tp13o.y < 1e-6) return false;
                if (tp11o.x + tp11o.y > 1 - 1e-6 && tp12o.x + tp12o.y > 1 - 1e-6 && tp13o.x + tp13o.y > 1 - 1e-6) return false;
                // ein Eckpunkt drin: diesen Eckpunkt liefern
                if (tp11o.x + tp11o.y < 1 - 1e-6 && tp11o.x > 1e-6 && tp11o.y > 1e-6)
                {
                    commonPoint = tp11;
                    return true;
                }
                if (tp12o.x + tp12o.y < 1 - 1e-6 && tp12o.x > 1e-6 && tp12o.y > 1e-6)
                {
                    commonPoint = tp12;
                    return true;
                }
                if (tp13o.x + tp13o.y < 1 - 1e-6 && tp13o.x > 1e-6 && tp13o.y > 1e-6)
                {
                    commonPoint = tp13;
                    return true;
                }
            }
            catch (ModOpException)
            {
                return false; // senkrechtes Dreieck, könnte noch besser behandelt werden
            }
            // Jetzt kann es höchstens noch echte Kantenschnitte geben
            if (LineIntersection(tp11, tp12, tp21, tp22, out commonPoint)) return true;
            if (LineIntersection(tp11, tp12, tp22, tp23, out commonPoint)) return true;
            if (LineIntersection(tp11, tp12, tp23, tp21, out commonPoint)) return true;
            if (LineIntersection(tp12, tp13, tp21, tp22, out commonPoint)) return true;
            if (LineIntersection(tp12, tp13, tp22, tp23, out commonPoint)) return true;
            if (LineIntersection(tp12, tp13, tp23, tp21, out commonPoint)) return true;
            if (LineIntersection(tp13, tp11, tp21, tp22, out commonPoint)) return true;
            if (LineIntersection(tp13, tp11, tp22, tp23, out commonPoint)) return true;
            if (LineIntersection(tp13, tp11, tp23, tp21, out commonPoint)) return true;

            return false;
        }
#region IQuadTreeInsertable Members

        public abstract BoundingRect GetExtent();
        public abstract bool HitTest(ref BoundingRect rect, bool includeControlPoints);
        public object ReferencedObject
        {   // brauchen wir nicht
            get { throw new NotImplementedException(); }
        }
#endregion

        internal bool IsCoverdBy(IPrintItemImpl pi2)
        {
            return pi2.coveredObjects.Contains(this);
        }
    }

    internal class PrintLine : IPrintItemImpl
    {
        public GeoPoint startPoint;
        public GeoPoint endPoint;
        Color color;
        double width;
        double[] pattern;
        double patternoffset;

        public PrintLine(GeoPoint startPoint, GeoPoint endPoint, Color color, double width, double[] pattern, double patternoffset)
            : base()
        {
            this.startPoint = startPoint;
            this.endPoint = endPoint;
            this.color = color;
            this.width = width;
            this.pattern = pattern;
            this.patternoffset = patternoffset;
        }
#if DEBUG
        public override IGeoObject Debug
        {
            get
            {
                Line res = Line.Construct();
                res.SetTwoPoints(startPoint, endPoint);
                return res;
            }
        }
#endif

        public override bool CommonPoint(IPrintItemImpl other, out GeoPoint2D commonPoint)
        {   // hier müssen halt alle Fälle getestet werden, in der Hierarchie dieses gegen gleich oder weniger komplexe PrintItems
            if (other is PrintLine)
            {
                return LineIntersection(new GeoPoint2D(startPoint), new GeoPoint2D(endPoint),
                    new GeoPoint2D((other as PrintLine).startPoint), new GeoPoint2D((other as PrintLine).endPoint), out commonPoint);
            }
            else return other.CommonPoint(this, out commonPoint);
        }
        public override bool CommonPoint(IPrintItemImpl other, BoundingRect rect, out GeoPoint2D commonPoint)
        {
            if (other is PrintLine)
            {
                if (LineIntersection(new GeoPoint2D(startPoint), new GeoPoint2D(endPoint),
                    new GeoPoint2D((other as PrintLine).startPoint), new GeoPoint2D((other as PrintLine).endPoint), out commonPoint))
                {
                    if (rect.Contains(commonPoint)) return true;
                }
                return false;
            }
            else return other.CommonPoint(this, rect, out commonPoint);
        }

        public override int CompareCommonPointZ(BoundingRect rect, IPrintItemImpl other)
        {   // hier müssen halt alle Fälle getestet werden, in der Hierarchie dieses gegen gleich oder weniger komplexe PrintItems
            // es geht um das Sortieren innerhalb eines Rechtecks
            if (other is PrintLine)
            {
                double pos1, pos2;
                if (IntersectLLparInside(new GeoPoint2D(startPoint), new GeoPoint2D(endPoint),
                    new GeoPoint2D((other as PrintLine).startPoint), new GeoPoint2D((other as PrintLine).endPoint), out pos1, out pos2))
                {
                    GeoPoint2D commonPoint = Geometry.LinePos(new GeoPoint2D(startPoint), new GeoPoint2D(endPoint), pos1);
                    if (rect.Contains(commonPoint))
                    {
                        Geometry.LinePos(startPoint, endPoint, pos1).z.CompareTo(Geometry.LinePos((other as PrintLine).startPoint, (other as PrintLine).endPoint, pos2).z);
                    }
                }
                return 0;
            }
            else return -other.CompareCommonPointZ(rect, this);
        }
        public override double ZPositionAt(GeoPoint2D here)
        {
            double par = Geometry.LinePar(new GeoPoint2D(startPoint), new GeoPoint2D(endPoint), here);
            return Geometry.LinePos(startPoint, endPoint, par).z + 1e-6;
            // + 1e-6 holt es ein bisschen nach vorne. Aber die richtige Lösung wäre: Linie mit gleichen Eckpunkten
            // wie Dreieck kommt immer vor Dreieck
        }
        public override IPrintItemImpl Clone()
        {
            return new PrintLine(startPoint, endPoint, color, width, pattern, patternoffset);
        }
        public override void Print(Graphics gr, bool doShading)
        {
            Pen pen = new Pen(color, (float)width);
            if (pattern != null && pattern.Length > 0)
            {
                // if (pen.Width == 0.0) pen.Width = 1.0f;
                float[] fpattern = new float[pattern.Length];
                for (int i = 0; i < pattern.Length; i++)
                {
                    fpattern[i] = (float)pattern[i];
                }
                pen.DashPattern = fpattern;
            }
            PointF sp = new PointF((float)startPoint.x, (float)startPoint.y);
            PointF ep = new PointF((float)endPoint.x, (float)endPoint.y);
            PointF[] dbg = new PointF[] { sp, ep };
            gr.Transform.TransformPoints(dbg);
            // System.Diagnostics.Trace.WriteLine("PrintLine (GDI Koordinaten): " + dbg[0].ToString() + ", " + dbg[1].ToString());
            gr.DrawLine(pen, sp, ep);
            pen.Dispose();
        }
        public override double ZMaximum
        {
            get
            {
                return Math.Max(startPoint.z, endPoint.z);
            }
        }
        public override double ZMinimum
        {
            get
            {
                return Math.Min(startPoint.z, endPoint.z);
            }
        }

#region IQuadTreeInsertable Members
        public override BoundingRect GetExtent()
        {
            return new BoundingRect(new GeoPoint2D(startPoint), new GeoPoint2D(endPoint));
        }
        public override bool HitTest(ref BoundingRect rect, bool includeControlPoints)
        {
            return (new ClipRect(rect).LineHitTest(new GeoPoint2D(startPoint), new GeoPoint2D(endPoint)));
        }
#endregion
    }

    internal class PrintText : IPrintItemImpl
    {
        GeoVector lineDirection;
        GeoVector glyphDirection;
        GeoPoint location;
        string fontName;
        string textString;
        System.Drawing.FontStyle fontStyle;
        CADability.GeoObject.Text.AlignMode alignment;
        CADability.GeoObject.Text.LineAlignMode lineAlignment;
        Color color;
        Text txt; // also GeoObject, zum Berechenen der Größe etc;

        public PrintText(GeoVector lineDirection, GeoVector glyphDirection, GeoPoint location, string fontName, string textString, System.Drawing.FontStyle fontStyle, CADability.GeoObject.Text.AlignMode alignment, CADability.GeoObject.Text.LineAlignMode lineAlignment, Color color)
            : base()
        {
            this.lineDirection = lineDirection;
            this.glyphDirection = glyphDirection;
            this.location = location;
            this.fontName = fontName;
            this.textString = textString;
            this.fontStyle = fontStyle;
            this.alignment = alignment;
            this.lineAlignment = lineAlignment;
            this.color = color;
            txt = Text.Construct();
            txt.Set(lineDirection, glyphDirection, location, fontName, textString, fontStyle, alignment, lineAlignment);
        }
        public override void Print(Graphics gr, bool doShading)
        {
            if (Math.Abs((glyphDirection ^ lineDirection).z) < Precision.eps) return;
            FontFamily ff;
            try
            {
                ff = new FontFamily(fontName);
            }
            catch (System.ArgumentException)
            {
                ff = new FontFamily(System.Drawing.Text.GenericFontFamilies.SansSerif);
            }
            StringFormat sf = StringFormat.GenericTypographic.Clone() as StringFormat;
            sf.LineAlignment = StringAlignment.Near;
            sf.Alignment = StringAlignment.Near;
            int fs = (int)fontStyle;
            int em = ff.GetEmHeight((FontStyle)fs);
            int dc = ff.GetCellDescent((FontStyle)fs);
            int ac = ff.GetCellAscent((FontStyle)fs);
            float acdc = (float)em; //  (ac + dc);
            RectangleF rectf = new RectangleF(0.0f, acdc, acdc, -acdc);
            PointF[] plg = new PointF[3];
            GeoPoint loc = txt.FourPoints[0];
            plg[2] = new PointF((float)(loc.x + glyphDirection.x), (float)(loc.y + glyphDirection.y));
            plg[0] = new PointF((float)loc.x, (float)loc.y);
            plg[1] = new PointF((float)(loc.x + lineDirection.x), (float)(loc.y + lineDirection.y));
            System.Drawing.Drawing2D.Matrix m = new System.Drawing.Drawing2D.Matrix(rectf, plg);
            System.Drawing.Drawing2D.GraphicsContainer container = gr.BeginContainer();
            System.Drawing.Drawing2D.Matrix trsf = gr.Transform;
            m.Multiply(trsf);
            // System.Diagnostics.Trace.WriteLine("PrintString: " + textString);
            gr.Transform = m;
            Font font = new Font(ff, em, (FontStyle)fs, GraphicsUnit.World);
            Brush brush = new SolidBrush(color);
            gr.DrawString(textString, font, brush, new PointF(0.0f, 0.0f));
            gr.EndContainer(container);
        }
        public override bool CommonPoint(IPrintItemImpl other, BoundingRect rect, out GeoPoint2D commonPoint)
        {
            return CommonPoint(other, out commonPoint);
        }
        public override bool CommonPoint(IPrintItemImpl other, out GeoPoint2D commonPoint)
        {
            GeoPoint[] p = txt.FourPoints; // lu, ru, ro, lo
            GeoPoint2D[] p2d = new GeoPoint2D[4];
            for (int i = 0; i < 4; i++)
            {
                p2d[i] = new GeoPoint2D(p[i]);
            }
            if (other is PrintLine)
            {
                if (TriangleLineIntersection(p2d[0], p2d[1], p2d[2], new GeoPoint2D((other as PrintLine).startPoint), new GeoPoint2D((other as PrintLine).endPoint), out commonPoint))
                {
                    return true;
                }
                if (TriangleLineIntersection(p2d[0], p2d[2], p2d[3], new GeoPoint2D((other as PrintLine).startPoint), new GeoPoint2D((other as PrintLine).endPoint), out commonPoint))
                {
                    return true;
                }
                return false;
            }
            if (other is PrintTriangle)
            {
                if (TriangleIntersection(p2d[0], p2d[1], p2d[2], new GeoPoint2D((other as PrintTriangle).tp1),
                    new GeoPoint2D((other as PrintTriangle).tp2), new GeoPoint2D((other as PrintTriangle).tp3), out commonPoint))
                {
                    return true;
                }
                if (TriangleIntersection(p2d[0], p2d[2], p2d[3], new GeoPoint2D((other as PrintTriangle).tp1),
                    new GeoPoint2D((other as PrintTriangle).tp2), new GeoPoint2D((other as PrintTriangle).tp3), out commonPoint))
                {
                    return true;
                }
                return false;
            }
            if (other is PrintText)
            {
                GeoPoint[] po = (other as PrintText).txt.FourPoints;
                GeoPoint2D[] p2do = new GeoPoint2D[4];
                for (int i = 0; i < 4; i++)
                {
                    p2do[i] = new GeoPoint2D(po[i]);
                }
                if (TriangleIntersection(p2d[0], p2d[1], p2d[2], p2do[0], p2do[1], p2do[2], out commonPoint))
                {
                    return true;
                }
                if (TriangleIntersection(p2d[0], p2d[2], p2d[3], p2do[0], p2do[1], p2do[2], out commonPoint))
                {
                    return true;
                }
                if (TriangleIntersection(p2d[0], p2d[1], p2d[2], p2do[0], p2do[2], p2do[3], out commonPoint))
                {
                    return true;
                }
                if (TriangleIntersection(p2d[0], p2d[2], p2d[3], p2do[0], p2do[2], p2do[3], out commonPoint))
                {
                    return true;
                }
                return false;
            }
            else
            {
                commonPoint = GeoPoint2D.Origin;
                return false;
            }
        }
        public override int CompareCommonPointZ(BoundingRect rect, IPrintItemImpl other)
        {   // hier müssen halt alle Fälle getestet werden, in der Hierarchie dieses gegen gleich oder weniger komplexe PrintItems
            // es geht um das Sortieren innerhalb eines Rechtecks
            return 0; // d.h. noch nicht implementiert
        }
        public override double ZPositionAt(GeoPoint2D here)
        {
            if (Precision.IsPerpendicular(txt.Plane.Normal, GeoVector.ZAxis, false))
            {
                return txt.Location.z; // ist sowieso egal, da der Text von der Kante her gesehen wird
            }
            else
            {
                return txt.Plane.Intersect(new GeoPoint(here), GeoVector.ZAxis).z;
            }
        }
        public override IPrintItemImpl Clone()
        {
            return new PrintText(lineDirection, glyphDirection, location, fontName, textString, fontStyle, alignment, lineAlignment, color);
        }
        public override double ZMaximum
        {
            get
            {   // besser nur einmal ausrechnen
                double res = double.MinValue;
                GeoPoint[] fp = txt.FourPoints;
                for (int i = 0; i < fp.Length; i++)
                {
                    if (fp[i].z > res) res = fp[i].z;
                }
                return res;
            }
        }
        public override double ZMinimum
        {
            get
            {   // besser nur einmal ausrechnen
                double res = double.MaxValue;
                GeoPoint[] fp = txt.FourPoints;
                for (int i = 0; i < fp.Length; i++)
                {
                    if (fp[i].z < res) res = fp[i].z;
                }
                return res;
            }
        }
#if DEBUG
        public override IGeoObject Debug
        {
            get
            {
                return null;
            }
        }
#endif
#region IQuadTreeInsertable Members
        public override BoundingRect GetExtent()
        {
            BoundingCube bc = txt.GetBoundingCube();
            return new BoundingRect(bc.Xmin, bc.Ymin, bc.Xmax, bc.Ymax);
        }
        public override bool HitTest(ref BoundingRect rect, bool includeControlPoints)
        {
            return true;
        }
#endregion
    }

    internal class PrintTriangle : IPrintItemImpl
    {

        GeoPoint[] points;
        GeoVector[] normals;
        int i0, i1, i2;
        public Color color;
#if DEBUG
        public static int idcounter = 0;
        public int id;
        public IGeoObject source;
#endif

        public PrintTriangle(GeoPoint[] points, GeoVector[] normals, int i0, int i1, int i2, Color color)
            : base()
        {
            this.points = points;
            this.normals = normals;
            this.i0 = i0;
            this.i1 = i1;
            this.i2 = i2;
            this.color = color;
#if DEBUG
            id = ++idcounter;
#endif
        }

        public GeoPoint tp1
        {
            get
            {
                return points[i0];
            }
        }
        public GeoPoint tp2
        {
            get
            {
                return points[i1];
            }
        }
        public GeoPoint tp3
        {
            get
            {
                return points[i2];
            }
        }
#if DEBUG
        public override IGeoObject Debug
        {
            get
            {
                Face fc = Face.MakeFace(points[i0], points[i1], points[i2]);
                fc.ColorDef = new ColorDef(color.ToString(), color);
                return fc;
            }
        }
#endif

        public override int CompareCommonPointZ(BoundingRect rect, IPrintItemImpl other)
        {   // hier müssen halt alle Fälle getestet werden, in der Hierarchie dieses gegen gleich oder weniger komplexe PrintItems
            // es geht um das Sortieren innerhalb eines Rechtecks
            if (other is PrintTriangle)
            {
                SimpleShape ss1 = new SimpleShape(Border.MakePolygon(new GeoPoint2D(tp1), new GeoPoint2D(tp2), new GeoPoint2D(tp3)));
                SimpleShape ss2 = new SimpleShape(Border.MakePolygon(new GeoPoint2D((other as PrintTriangle).tp1), new GeoPoint2D((other as PrintTriangle).tp2), new GeoPoint2D((other as PrintTriangle).tp3)));
                CompoundShape cs = SimpleShape.Intersect(ss1, ss2);
                if (cs.SimpleShapes.Length > 0 && cs.Area > 1e-3)
                {
                    cs = CompoundShape.Intersection(cs, new CompoundShape(new SimpleShape(Border.MakeRectangle(rect))));
                    if (cs.SimpleShapes.Length == 1)
                    {
                        try
                        {
                            GeoPoint2D ip = cs.SimpleShapes[0].Outline.SomeInnerPoint;
                            return ZPositionAt(ip).CompareTo((other as PrintTriangle).ZPositionAt(ip));
                        }
                        catch (BorderException)
                        {
                            return 0;
                        }
                    }
                }
                return 0;
            }
            return 0; // d.h. noch nicht implementiert
        }
        public override bool CommonPoint(IPrintItemImpl other, out GeoPoint2D commonPoint)
        {
            if (other is PrintLine)
            {
                if (TriangleLineIntersection(new GeoPoint2D(points[i0]), new GeoPoint2D(points[i1]), new GeoPoint2D(points[i2]), new GeoPoint2D((other as PrintLine).startPoint), new GeoPoint2D((other as PrintLine).endPoint), out commonPoint))
                {
                    return true;
                }
                return false;
            }
            else if (other is PrintTriangle)
            {
                // Problem: gleiche Ecken oder gleiche Kanten
                PrintTriangle ptother = (other as PrintTriangle);
                return (TriangleIntersection(new GeoPoint2D(points[i0]), new GeoPoint2D(points[i1]), new GeoPoint2D(points[i2]),
                    new GeoPoint2D(ptother.tp1), new GeoPoint2D(ptother.tp2), new GeoPoint2D(ptother.tp3), out commonPoint));
            }
            else
            {
                return other.CommonPoint(this, out commonPoint);
            }
        }
        public override bool CommonPoint(IPrintItemImpl other, BoundingRect rect, out GeoPoint2D commonPoint)
        {
            if (other is PrintLine)
            {
                // erstmal recht ineffizient:
                Border bt1 = Border.MakePolygon(new GeoPoint2D[] { new GeoPoint2D(points[i0]), new GeoPoint2D(points[i1]), new GeoPoint2D(points[i2]) });
                Line2D l2d = new Line2D(new GeoPoint2D((other as PrintLine).startPoint), new GeoPoint2D((other as PrintLine).endPoint));
                double[] ips = bt1.Clip(l2d, true);
                if (ips != null && ips.Length == 2)
                {
                    commonPoint = l2d.PointAt((ips[0] + ips[1]) / 2.0);
                    return true;
                }
                commonPoint = GeoPoint2D.Origin;
                return false;
            }
            else if (other is PrintTriangle)
            {
                // erstmal recht ineffizient:
                PrintTriangle ptother = (other as PrintTriangle);
                Border bt1 = Border.MakePolygon(new GeoPoint2D[] { new GeoPoint2D(points[i0]), new GeoPoint2D(points[i1]), new GeoPoint2D(points[i2]) });
                Border bt2 = Border.MakePolygon(new GeoPoint2D[] { new GeoPoint2D(ptother.tp1), new GeoPoint2D(ptother.tp2), new GeoPoint2D(ptother.tp3) });
                CompoundShape cs = SimpleShape.Intersect(new SimpleShape(bt1), new SimpleShape(bt2));
                if (cs.SimpleShapes.Length > 0 && cs.Area > rect.Size * 1e-4)
                {
                    cs = CompoundShape.Intersection(cs, new CompoundShape(new SimpleShape(Border.MakeRectangle(rect))));
                    if (cs.SimpleShapes.Length == 1)
                    {
                        if (cs.SimpleShapes[0].Outline.Area > rect.Size * 1e-4)
                        {
                            commonPoint = cs.SimpleShapes[0].Outline.SomeInnerPoint;
                            return true;
                        }
                    }
                }
                commonPoint = GeoPoint2D.Origin;
                return false;
            }
            else
            {
                return other.CommonPoint(this, rect, out commonPoint);
            }
        }
        public override double ZPositionAt(GeoPoint2D here)
        {
            Plane pln = new Plane(points[i0], points[i1], points[i2]);
            if (Precision.IsPerpendicular(pln.Normal, GeoVector.ZAxis, false))
            {
                return pln.Location.z; // ist sowieso egal, da das Dreieck von der Kante her gesehen wird
            }
            else
            {
                return pln.Intersect(new GeoPoint(here), GeoVector.ZAxis).z;
            }
        }
        public override IPrintItemImpl Clone()
        {
            return new PrintTriangle(points, normals, i0, i1, i2, color);
        }
        public override double ZMaximum
        {
            get
            {
                double res = double.MinValue;
                if (points[i0].z > res) res = points[i0].z;
                if (points[i1].z > res) res = points[i1].z;
                if (points[i2].z > res) res = points[i2].z;
                return res;

            }
        }
        public override double ZMinimum
        {
            get
            {
                double res = double.MaxValue;
                if (points[i0].z < res) res = points[i0].z;
                if (points[i1].z < res) res = points[i1].z;
                if (points[i2].z < res) res = points[i2].z;
                return res;
            }
        }
        // wird so gut wie nie verwendet
        List<PrintTriangle> DivideTriangles(PrintTriangle pt)
        {
            List<PrintTriangle> result = new List<PrintTriangle>();
            List<PrintTriangle> result2 = new List<PrintTriangle>();
            // Finde Strecke mit zwei Schnittpunkten   
            GeoPoint2D x1 = new GeoPoint2D(points[i0]);
            GeoPoint2D y1 = new GeoPoint2D(points[i1]);
            GeoPoint2D z1 = new GeoPoint2D(points[i2]);
            GeoPoint2D x2 = new GeoPoint2D(pt.points[pt.i0]);
            GeoPoint2D y2 = new GeoPoint2D(pt.points[pt.i1]);
            GeoPoint2D z2 = new GeoPoint2D(pt.points[pt.i2]);
            GeoPoint2D[] dreieck1 = new GeoPoint2D[] { x1, y1, z1 };
            GeoPoint2D[] dreieck2 = new GeoPoint2D[] { x2, y2, z2 };
            GeoPoint[] dreieck3D1 = new GeoPoint[] { points[i0], points[i1], points[i2] };
            GeoPoint[] dreieck3D2 = new GeoPoint[] { pt.points[pt.i0], pt.points[pt.i1], pt.points[pt.i2] };
            // Seitenbezeichnung a1 = x1y1 , b1=y1z1 ,c1=z1x1
            //List<GeoPoint> Zerlegepunkte1;
            GeoPoint2D[,] schnittpunkte2D = new GeoPoint2D[3, 3];
            GeoPoint[,] schnittpunktedreickeck1 = new GeoPoint[3, 3];
            GeoPoint[,] schnittpunktedreickeck2 = new GeoPoint[3, 3];
            GeoVector standardvektor = new GeoVector(0, 0, 1);
            GeoVector[] normalen = new GeoVector[] { standardvektor, standardvektor, standardvektor, standardvektor };
            for (int i = 0; i < 3; i++)
            {
                for (int j = i; j < 3; j++)
                {
                    GeoPoint2D intersect = new GeoPoint2D();
                    if (Geometry.IntersectLL(dreieck1[i], dreieck1[(i + 1) % 3], dreieck2[j], dreieck2[(j + 1) % 3], out intersect))
                    {
                        schnittpunkte2D[i, j] = intersect;
                        double u = Geometry.LinePar(dreieck1[i], dreieck1[(i + 1) % 3], intersect);
                        double v = Geometry.LinePar(dreieck2[j], dreieck2[(j + 1) % 3], intersect);
                        if (u > 0 && u < 1 && v > 0 && v < 1)
                        {
                            double h1 = dreieck3D1[i].z + u * (dreieck3D1[(i + 1) % 3].z - dreieck3D1[i].z);
                            double h2 = dreieck3D2[j].z + v * (dreieck3D2[(j + 1) % 3].z - dreieck3D2[j].z);
                            schnittpunktedreickeck1[i, j] = new GeoPoint(intersect, h1);
                            schnittpunktedreickeck2[i, j] = new GeoPoint(intersect, h2);
                            GeoPoint[] punktea = new GeoPoint[] { dreieck3D1[i], dreieck3D1[(i + 1) % 3], dreieck3D1[(i + 2) % 3], schnittpunktedreickeck1[i, j] };
                            GeoPoint[] punkteb = new GeoPoint[] { dreieck3D2[j], dreieck3D2[(j + 1) % 3], dreieck3D2[(j + 2) % 3], schnittpunktedreickeck2[i, j] };
                            result.Add(new PrintTriangle(punktea, normalen, 0, 3, 2, this.color));
                            result.Add(new PrintTriangle(punktea, normalen, 3, 1, 2, this.color));
                            result2.Add(new PrintTriangle(punkteb, normalen, 0, 3, 2, pt.color));
                            result2.Add(new PrintTriangle(punkteb, normalen, 3, 1, 2, pt.color));
                        }
                    }
                }
            }
            foreach (PrintTriangle pr in result2)
            {
                result.Add(pr);
            }
            return result;
        }

        public override void Print(Graphics gr, bool doShading)
        {
            PointF[] pnts = new PointF[3];
            pnts[0] = points[i0].ToPointF();
            pnts[1] = points[i1].ToPointF();
            pnts[2] = points[i2].ToPointF();
            bool flat = false;
            float area = 0.0f;
            // System.Diagnostics.Trace.WriteLine("PrintTriangle: " + pnts[0].ToString() + ", " + pnts[1].ToString() + ", " + pnts[2].ToString());
            if (!doShading) flat = true;
            else if (Precision.SameDirection(normals[i0], normals[i1], true) && Precision.SameDirection(normals[i1], normals[i2], true)) flat = true;
            if (!flat)
            {
                area = (pnts[0].X * (pnts[1].Y - pnts[2].Y) + pnts[1].X * (pnts[2].Y - pnts[0].Y) + pnts[2].X * (pnts[0].Y - pnts[1].Y));
                flat = Math.Abs(area) < 1e-6;
            }
            try
            {

                gr.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                gr.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;//HighQuality; 
                //if (true)
                if (flat)
                {
                    Brush brush;
                    brush = new SolidBrush(ShadedColor(normals[i0] + normals[i1] + normals[i2]));
                    gr.FillPolygon(brush, pnts);
                    /* Umrandung testweise nicht zeichnen
                    Pen colorpen = new Pen(brush,(float)0.0);
                    gr.DrawPolygon(colorpen, pnts);
                    colorpen.Dispose();
                    //*/
                    brush.Dispose();
                }
                else
                {
                    GeoPoint2D p0 = points[i0].To2D();
                    GeoPoint2D p1 = points[i1].To2D();
                    GeoPoint2D p2 = points[i2].To2D();
                    GeoPoint2D[] punkte = new GeoPoint2D[] { p0, p1, p2 };
                    double[] brightness = new double[] {
                        Brightness(normals[i0]),
                        Brightness(normals[i1]),
                        Brightness(normals[i2])
                    };

                    //*
                    //bestimmung der Reihenfolge etwas zu kompliziert
                    int indmax, indmin, indmid;
                    if (brightness[0] > brightness[1])
                    {
                        if (brightness[0] > brightness[2])
                        {
                            indmax = 0;
                            if (brightness[2] > brightness[1])
                            {
                                indmin = 1;
                                indmid = 2;
                            }
                            else
                            {
                                indmin = 2;
                                indmid = 1;
                            }
                        }
                        else
                        {
                            indmax = 2;
                            indmin = 1;
                            indmid = 0;
                        }
                    }
                    else
                    {
                        if (brightness[1] > brightness[2])
                        {
                            indmax = 1;
                            if (brightness[2] > brightness[0])
                            {
                                indmin = 0;
                                indmid = 2;
                            }
                            else
                            {
                                indmin = 2;
                                indmid = 0;
                            }

                        }
                        else
                        {
                            indmax = 2;
                            indmin = 0;
                            indmid = 1;
                        }
                    }
                    double anteil = (brightness[indmid] - brightness[indmin]) / (brightness[indmax] - brightness[indmin]);
                    GeoPoint2D zw = punkte[indmin] + anteil * (punkte[indmax] - punkte[indmin]);
                    //*
                    // Bestimmen des Vektors senkrecht auf c-zw
                    GeoVector2D richtung = new GeoVector2D();
                    GeoVector2D u = punkte[indmid] - zw;
                    if (u.y != 0)
                    {
                        richtung = new GeoVector2D(1, -u.x / u.y);
                    }
                    else
                    {
                        richtung = new GeoVector2D(-u.y / u.x, 1);
                    }
                    //richtung.Norm();
                    // Berechnung der laenge des Vektors
                    // Berechnung des Schnittpunktes
                    GeoPoint2D directionpoint = new GeoPoint2D();
                    // Berechnung des Schnittpunktes
                    double diff1 = brightness[indmid] - brightness[indmin];
                    double diff2 = brightness[indmax] - brightness[indmid];
                    GeoVector2D properdirection;
                    GeoPoint2D startpoint;
                    GeoPoint2D endpoint;
                    if (diff1 > diff2)
                    {
                        Geometry.IntersectLL(zw, richtung, punkte[indmin], u, out directionpoint);
                        properdirection = zw - directionpoint;
                        startpoint = zw - brightness[indmid] / diff1 * properdirection;
                        endpoint = zw + (1 - brightness[indmid]) / diff1 * properdirection;
                    }
                    else
                    {
                        Geometry.IntersectLL(zw, richtung, punkte[indmax], u, out directionpoint);
                        properdirection = directionpoint - zw;


                        startpoint = zw - brightness[indmid] / diff2 * properdirection;
                        endpoint = zw + (1 - brightness[indmid]) / diff2 * properdirection;
                    }
                    LinearGradientBrush br = new LinearGradientBrush(startpoint.PointF, endpoint.PointF, Color.Black, Color.Black);
                    ColorBlend cb = new ColorBlend();
                    cb.Positions = new float[] { 0.0f, 0.65f, 1.0f };
                    cb.Colors = new Color[] { Color.Black, this.color, Color.White };
                    br.InterpolationColors = cb;
                    gr.FillPolygon(br, pnts);
                    /*
                    Pen colorpen = new Pen(br, (float)0.0);
                    gr.DrawPolygon(colorpen, pnts);  
                    // brush freigeben
                    colorpen.Dispose();
                    //*/
                    br.Dispose();
                }
            }
            catch (Exception)
            {
            }


        }

        double Brightness(GeoVector normal)
        {
            GeoVector light = new GeoVector(1, 1, 1); // der sollte woher kommen
            light.Norm();
            Angle a = new Angle(normal, light);
            return 1 - (a.Radian / Math.PI);
        }

        // Farbe fuer Solidbrush, sollte an Linearbrush angepasst sein.
        Color ShadedColor(GeoVector normal)
        {
            GeoVector light = new GeoVector(1, 1, 1); // der sollte woher kommen
            light.Norm();
            Angle a = new Angle(normal, light);
            // a ist zwischen 0 und pi bei 0 ganz hell, bei pi/4 normal, darüber dunkler
            Color res = new Color();
            if (a.Radian > Math.PI / 4.0)
            {   // dunkler machen, pi/4 bleibt so
                double f = 1.0 - (a.Radian - Math.PI / 4.0) / Math.PI * 4.0 / 5.0;
                res = Color.FromArgb((int)(f * color.R), (int)(f * color.G), (int)(f * color.B));
            }
            else
            {
                double f = (Math.PI / 4.0 - a.Radian) / Math.PI * 4.0;
                res = Color.FromArgb((int)(color.R + f * (255 - color.R)), (int)(color.G + f * (255 - color.G)), (int)(color.B + f * (255 - color.B)));
            }
            return res;
        }

#region IQuadTreeInsertable Members
        public override BoundingRect GetExtent()
        {
            //throw new Exception("The method or operation is not implemented.");
            return new BoundingRect(new GeoPoint2D(points[i0]), new GeoPoint2D(points[i1]), new GeoPoint2D(points[i2]));
        }
        public override bool HitTest(ref BoundingRect rect, bool includeControlPoints)
        {
            return new ClipRect(rect).TriangleHitTest(new GeoPoint2D(points[i0]), new GeoPoint2D(points[i1]), new GeoPoint2D(points[i2]));
            //throw new Exception("The method or operation is not implemented.");
        }
#endregion
    }

    internal class PrintBitmap : IPrintItemImpl
    {
        Bitmap toPrint;
        GeoPoint location;
        GeoVector xDirection;
        GeoVector yDirection;
        public PrintBitmap(Bitmap toPrint, GeoPoint location, GeoVector xDirection, GeoVector yDirection)
        {
            this.toPrint = toPrint;
            this.location = location;
            this.xDirection = xDirection;
            this.yDirection = yDirection;
        }

        public override void Print(Graphics gr, bool doShading)
        {
            PointF[] destPoints = new PointF[3];
            destPoints[0] = (location + yDirection).ToPointF();
            destPoints[1] = (location + yDirection + xDirection).ToPointF();
            destPoints[2] = (location).ToPointF();
            // System.Diagnostics.Trace.WriteLine("PrintBitmap: " + destPoints[0].ToString() + ", " + destPoints[1].ToString() + ", " + destPoints[2].ToString());
            gr.DrawImage(toPrint, destPoints);
        }

        public override double ZMaximum
        {
            get
            {
                return Math.Max(Math.Max((location + xDirection).z, (location + xDirection).z), Math.Max((location + xDirection + yDirection).z, location.z));
            }
        }

        public override double ZMinimum
        {
            get
            {
                return Math.Min(Math.Min((location + xDirection).z, (location + xDirection).z), Math.Min((location + xDirection + yDirection).z, location.z));
            }
        }

        public override bool CommonPoint(IPrintItemImpl other, BoundingRect rect, out GeoPoint2D commonPoint)
        {
            throw new NotImplementedException();
        }

        public override bool CommonPoint(IPrintItemImpl other, out GeoPoint2D commonPoint)
        {
            GeoPoint[] points = new GeoPoint[4];
            points[0] = location;
            points[1] = location + xDirection;
            points[2] = location + xDirection + yDirection;
            points[3] = location + yDirection;
            if (other is PrintLine)
            {
                if (TriangleLineIntersection(new GeoPoint2D(points[0]), new GeoPoint2D(points[1]), new GeoPoint2D(points[2]), new GeoPoint2D((other as PrintLine).startPoint), new GeoPoint2D((other as PrintLine).endPoint), out commonPoint))
                {
                    return true;
                }
                if (TriangleLineIntersection(new GeoPoint2D(points[0]), new GeoPoint2D(points[2]), new GeoPoint2D(points[3]), new GeoPoint2D((other as PrintLine).startPoint), new GeoPoint2D((other as PrintLine).endPoint), out commonPoint))
                {
                    return true;
                }
                return false;
            }
            else if (other is PrintTriangle)
            {
                // Problem: gleiche Ecken oder gleiche Kanten
                PrintTriangle ptother = (other as PrintTriangle);
                if (TriangleIntersection(new GeoPoint2D(points[0]), new GeoPoint2D(points[1]), new GeoPoint2D(points[2]),
                    new GeoPoint2D(ptother.tp1), new GeoPoint2D(ptother.tp2), new GeoPoint2D(ptother.tp3), out commonPoint))
                {
                    return true;
                }
                if (TriangleIntersection(new GeoPoint2D(points[0]), new GeoPoint2D(points[2]), new GeoPoint2D(points[3]),
                    new GeoPoint2D(ptother.tp1), new GeoPoint2D(ptother.tp2), new GeoPoint2D(ptother.tp3), out commonPoint))
                {
                    return true;
                }
                return false;
            }
            else if (other is PrintBitmap)
            {
                PrintBitmap opb = (other as PrintBitmap);
                GeoPoint[] opoints = new GeoPoint[4];
                opoints[0] = opb.location;
                opoints[1] = opb.location + opb.xDirection;
                opoints[2] = opb.location + opb.xDirection + opb.yDirection;
                opoints[3] = opb.location + opb.yDirection;
                if (TriangleIntersection(new GeoPoint2D(points[0]), new GeoPoint2D(points[1]), new GeoPoint2D(points[2]),
                    new GeoPoint2D(opoints[0]), new GeoPoint2D(opoints[1]), new GeoPoint2D(opoints[2]), out commonPoint))
                {
                    return true;
                }
                if (TriangleIntersection(new GeoPoint2D(points[0]), new GeoPoint2D(points[1]), new GeoPoint2D(points[2]),
                    new GeoPoint2D(opoints[0]), new GeoPoint2D(opoints[2]), new GeoPoint2D(opoints[3]), out commonPoint))
                {
                    return true;
                }
                if (TriangleIntersection(new GeoPoint2D(points[0]), new GeoPoint2D(points[2]), new GeoPoint2D(points[3]),
                    new GeoPoint2D(opoints[0]), new GeoPoint2D(opoints[1]), new GeoPoint2D(opoints[2]), out commonPoint))
                {
                    return true;
                }
                if (TriangleIntersection(new GeoPoint2D(points[0]), new GeoPoint2D(points[2]), new GeoPoint2D(points[3]),
                    new GeoPoint2D(opoints[0]), new GeoPoint2D(opoints[2]), new GeoPoint2D(opoints[3]), out commonPoint))
                {
                    return true;
                }
                return false;
            }
            else
            {
                commonPoint = GeoPoint2D.Origin;
                return false;
            }
        }
        public override int CompareCommonPointZ(BoundingRect rect, IPrintItemImpl other)
        {   // hier müssen halt alle Fälle getestet werden, in der Hierarchie dieses gegen gleich oder weniger komplexe PrintItems
            // es geht um das Sortieren innerhalb eines Rechtecks
            return 0; // noch nicht implementiert
        }
        public override double ZPositionAt(GeoPoint2D here)
        {
            Plane pln = new Plane(location, xDirection, yDirection);
            if (Precision.IsPerpendicular(pln.Normal, GeoVector.ZAxis, false))
            {
                return pln.Location.z; // ist sowieso egal, da das Dreieck von der Kante her gesehen wird
            }
            else
            {
                return pln.Intersect(new GeoPoint(here), GeoVector.ZAxis).z;
            }
        }
        public override IPrintItemImpl Clone()
        {
            return new PrintBitmap(toPrint, location, xDirection, yDirection);
        }

        public override BoundingRect GetExtent()
        {
            return new BoundingRect(new GeoPoint2D(location), new GeoPoint2D(location + xDirection), new GeoPoint2D(location + yDirection), new GeoPoint2D(location + xDirection + yDirection));
        }

        public override bool HitTest(ref BoundingRect rect, bool includeControlPoints)
        {
            GeoPoint[] points = new GeoPoint[4];
            points[0] = location;
            points[1] = location + xDirection;
            points[2] = location + xDirection + yDirection;
            points[3] = location + yDirection;
            if (new ClipRect(rect).TriangleHitTest(new GeoPoint2D(points[0]), new GeoPoint2D(points[1]), new GeoPoint2D(points[2])))
                return true;
            if (new ClipRect(rect).TriangleHitTest(new GeoPoint2D(points[0]), new GeoPoint2D(points[2]), new GeoPoint2D(points[3])))
                return true;
            return false;
        }
#if DEBUG
        public override IGeoObject Debug
        {
            get
            {
                return null;
            }
        }
#endif
    }

    // must be implemented in CADability.Forms!
    public class PrintToGDI : IPaintTo3D, QuadTree<IPrintItemImpl>.IIterateQuadTreeLists
    {

        LayoutView layoutView; // nur eines von diesen dreien ist gesetzt
        GDI2DView gdiView; // nur eines von diesen dreien ist gesetzt
        Bitmap bitmap; // nur eines von diesen dreien ist gesetzt
        Matrix4 projection;
        Stack<Matrix4> projectionStack;
        LineWidth currentLineWidth;
        LinePattern currentLinePattern;
        Color currentColor;
        PaintTo3D.PaintMode paintMode;
        List<IPrintItemImpl> currentCollectionList;
        QuadTree<IPrintItemImpl> currentCollectionQuad;
        double currentPrecision;
        Graphics currentGraphics;
        Projection currentProjection;
        GraphicsPath graphicsPath; // wenn!=null wird darauf gezeichnet
        bool print2D;
        bool shading;
        bool checkCoverage;
        double lineWidthFactor;

        public PrintToGDI(GDI2DView gdiView)
        {
            this.gdiView = gdiView;
            shading = false;
            checkCoverage = true;
            projectionStack = new Stack<Matrix4>();
            lineWidthFactor = 1.0;
        }
        public PrintToGDI(LayoutView layoutView)
        {
            this.layoutView = layoutView;
            // print2D = Settings.GlobalSettings.GetBoolValue("Printing.GDI.2D", false);
            // wenn print2D, dann wird nicht gesammelt sondern gleich gedruckt
            // und es werden auch Pfade und Ellipsen verwendet
            shading = Settings.GlobalSettings.GetBoolValue("Printing.GDIShading", false);
            // shading sagt, dass Dreiecke mit einem LinearGradientBrush gemacht werden sollen
            // ansonsten sollen sie mit einem SolidBrush gemacht werden
            checkCoverage = Settings.GlobalSettings.GetBoolValue("Printing.GDICoverage", false);
            projectionStack = new Stack<Matrix4>();
            lineWidthFactor = 1.0;
        }
        public PrintToGDI(Bitmap bitmap)
        {
            this.bitmap = bitmap;
            checkCoverage = true;
            projectionStack = new Stack<Matrix4>();
            lineWidthFactor = 1.0;
        }
        public static void PrintSinglePage(Project pr, string nameOfView, bool print2D, PrintDocument pd)
        {
            try
            {
                GDI2DView found = null;
                for (int i = 0; i < pr.GdiViews.Count; i++)
                {
                    if (pr.GdiViews[i].Name == nameOfView)
                    {
                        found = pr.GdiViews[i];
                    }
                }
                if (pd == null)
                {
                    pd = pr.printDocument;
                    if (pd == null) pd = new PrintDocument();
                }
                if (found != null)
                {
                    PrintToGDI ptg = new PrintToGDI(found);
                    bool sf = (found as IView).Projection.ShowFaces;
                    (found as IView).Projection.ShowFaces = !print2D;
                    //ptg.print2D = true; // ShowFaces überschreibt dieses leider
                    ptg.checkCoverage = false;
                    pd.PrintPage += new PrintPageEventHandler(ptg.OnPrintPage);
                    try
                    {
                        pd.Print();
                    }
                    catch (Exception)
                    {
                    }
                    pd.PrintPage -= new PrintPageEventHandler(ptg.OnPrintPage);
                    (found as IView).Projection.ShowFaces = sf;
                }
            } catch (Exception)
            {
                // MessageBox.Show(ex.StackTrace, "Exception in PrintSinglePage");
            }
        }
        public void PrintToBitmap(Model model, Projection pr, Layer[] visibleLayers, bool shading, bool curvesOnly)
        {
            this.shading = shading;
            this.print2D = curvesOnly;
            currentGraphics = Graphics.FromImage(bitmap);
            Brush whiteBrush = new SolidBrush(Color.White);
            currentGraphics.FillRectangle(whiteBrush, 0, 0, bitmap.Width, bitmap.Height);

            // currentGraphics.Transform = new System.Drawing.Drawing2D.Matrix((float)(fct), 0.0f, 0.0f, (float)(-fct), 0.0f, (float)(PaperWidth * height / gdiView.PaperHeight));

            BoundingRect ext;
            ext = new BoundingRect(0.0, 0.0, bitmap.Width, bitmap.Height);

            GeoPoint2D ll = ext.GetLowerLeft();
            GeoPoint2D ur = ext.GetUpperRight();
            Rectangle clipRectangle = Rectangle.FromLTRB((int)ll.x, (int)ll.y, (int)ur.x, (int)ur.y);

            currentProjection = pr;
            if (pr.IsPerspective)
                projection = pr.PerspectiveProjection;
            else projection = new Matrix4(pr.UnscaledProjection);
            projectionStack = new Stack<Matrix4>();
            projectionStack.Push(projection);
            print2D = !pr.ShowFaces; // bei 2D wird direkt ausgegeben, nicht sortiert
            double plfact, dx, dy;
            pr.GetPlacement(out plfact, out dx, out dy);
            currentGraphics.Transform = new System.Drawing.Drawing2D.Matrix((float)(plfact), 0.0f, 0.0f, (float)(-plfact), (float)dx, (float)dy);
            // projection = new Matrix4(new double[,] { { plfact, 0, 0, dx }, { 0, plfact, 0, dy }, { 0, 0, plfact, 0.0 }, { 0, 0, 0, 1 } }) * projection;
            currentPrecision = 1.0 / plfact;//0.1; // noch vernünftig setzen
            //currentCollection = new OctTree<IPrintItemImpl>(lp.Model.Extent, 0.1); // Parameter noch falsch             
            currentCollectionList = new List<IPrintItemImpl>();
            currentCollectionQuad = new QuadTree<IPrintItemImpl>(new BoundingRect(model.Extent.Xmin, model.Extent.Ymin, model.Extent.Xmax, model.Extent.Ymax));
            // Sorgt nicht fuer eine Verbesserung

            //currentCollectionQuad.MaxDeepth = 9;
            //currentCollectionQuad.MaxListLen = 9;
            // System.Diagnostics.Trace.WriteLine("Beginn: Zeitmessung der QuadTreeErstellung");
            DateTime starttime = DateTime.Now;
            if (print2D)
            {
                (this as IPaintTo3D).PaintFaces(PaintTo3D.PaintMode.CurvesOnly);
            }
            else
            {
                (this as IPaintTo3D).PaintFaces(PaintTo3D.PaintMode.All);
            }

            GeoObjectList l = new GeoObjectList(model.AllObjects);
            if (!Settings.GlobalSettings.GetBoolValue("PrintToGDI.PrintBlocks", false)) l.DecomposeBlocks(false);
            HashSet<Layer> vlset = new HashSet<Layer>(visibleLayers);
            foreach (IGeoObject go in l)
            {
                // Sammeln der Geoobjekte
                if (go.Layer == null || vlset.Contains(go.Layer))
                    CollectPrintItems(go);
            }
            DateTime endtime = DateTime.Now;
            TimeSpan diff = endtime - starttime;
            // System.Diagnostics.Trace.WriteLine("Benoetigte Zeit  zum Sammeln: " + diff.TotalMilliseconds.ToString() + " ms");
            PrintPrintItems();
            currentGraphics.Dispose();
        }
        public void OnPrintPage(object sender, PrintPageEventArgs e)
        {
            try
            {
                currentGraphics = e.Graphics;
                Rectangle r = e.PageBounds; // ist IMMER in 100 dpi
                                            // die Pixelauflösung des Druckers ist in e.Graphics.DpiX bzw. DpiY
                double fctpap = 100 / 25.4;
                //double fct = 100 / 25.4;
                double fct = e.Graphics.DpiX / 25.4; // das ist die echte Auflösung
                                                     // fct /= 2; war nur ein Test
                e.Graphics.PageScale = (float)(1.0);

                bool paintOnBitmap;
                if (gdiView != null) paintOnBitmap = false;
                else paintOnBitmap = Settings.GlobalSettings.GetBoolValue("Printing.GDIBitmap", false);
                int width = 0, height = 0;
                Bitmap bmp = null;
                if (paintOnBitmap)
                {
                    width = (int)(e.Graphics.DpiX * e.PageBounds.Width / 100.0);
                    height = (int)(e.Graphics.DpiY * e.PageBounds.Height / 100.0);
                    bmp = new Bitmap(width, height);
                    currentGraphics = Graphics.FromImage(bmp);
                    Brush whiteBrush = new SolidBrush(Color.White);
                    currentGraphics.FillRectangle(whiteBrush, 0, 0, width, height);
                }
                if (layoutView != null)
                {
                    for (int i = 0; i < layoutView.Layout.PatchCount; ++i)
                    {
                        LayoutPatch lp = layoutView.Layout.Patches[i];

                        if (paintOnBitmap)
                        {
                            //currentGraphics.Transform = new System.Drawing.Drawing2D.Matrix((float)(width / layoutView.Layout.PaperWidth), 0.0f, 0.0f, (float)(-height / layoutView.Layout.PaperHeight), 0.0f, (float)(layoutView.Layout.PaperHeight * height / layoutView.Layout.PaperHeight));
                            currentGraphics.Transform = new System.Drawing.Drawing2D.Matrix((float)(fct), 0.0f, 0.0f, (float)(-fct), 0.0f, (float)(layoutView.Layout.PaperHeight * height / layoutView.Layout.PaperHeight));
                        }
                        else
                        {
                            currentGraphics.Transform = new System.Drawing.Drawing2D.Matrix((float)fctpap, 0.0f, 0.0f, (float)-fctpap, 0.0f, (float)(layoutView.Layout.PaperHeight * fctpap));
                        }

                        BoundingRect ext;
                        if (lp.Area != null)
                        {
                            ext = lp.Area.Extent;
                        }
                        else
                        {
                            ext = new BoundingRect(0.0, 0.0, layoutView.Layout.PaperWidth, layoutView.Layout.PaperHeight);
                        }

                        GeoPoint2D ll = ext.GetLowerLeft();
                        GeoPoint2D ur = ext.GetUpperRight();
                        Rectangle clipRectangle = Rectangle.FromLTRB((int)ll.x, (int)ll.y, (int)ur.x, (int)ur.y);
                        Projection pr = lp.Projection.Clone();

                        currentProjection = pr;
                        projection = pr.PerspectiveProjection;
                        if (!projection.IsValid)
                        {   // parallelprojektion
                            projection = new Matrix4(pr.UnscaledProjection);
                        }
                        print2D = !pr.ShowFaces; // bei 2D wird direkt ausgegeben, nicht sortiert
                        double plfact, dx, dy;
                        pr.GetPlacement(out plfact, out dx, out dy);
                        projection = new Matrix4(new double[,] { { plfact, 0, 0, dx }, { 0, plfact, 0, dy }, { 0, 0, plfact, 0.0 }, { 0, 0, 0, 1 } }) * projection;
                        currentPrecision = 0.5;//0.1; // noch vernünftig setzen
                                               //currentCollection = new OctTree<IPrintItemImpl>(lp.Model.Extent, 0.1); // Parameter noch falsch             
                        currentCollectionList = new List<IPrintItemImpl>();
                        currentCollectionQuad = new QuadTree<IPrintItemImpl>(new BoundingRect(lp.Model.Extent.Xmin, lp.Model.Extent.Ymin, lp.Model.Extent.Xmax, lp.Model.Extent.Ymax));
                        // Sorgt nicht fuer eine Verbesserung

                        //currentCollectionQuad.MaxDeepth = 9;
                        //currentCollectionQuad.MaxListLen = 9;
                        // System.Diagnostics.Trace.WriteLine("Beginn: Zeitmessung der QuadTreeErstellung");
                        DateTime starttime = DateTime.Now;
                        if (print2D)
                        {
                            (this as IPaintTo3D).PaintFaces(PaintTo3D.PaintMode.CurvesOnly);
                        }
                        else
                        {
                            (this as IPaintTo3D).PaintFaces(PaintTo3D.PaintMode.All);
                        }
                        if (!checkCoverage) print2D = true; // damit gefüllte Flächen als gefüllte Pfad dargestellt werden
                        if (Settings.GlobalSettings.GetBoolValue("Printing.UseZOrder", false))
                        {   // nach Layer DisplayOrder sortieren
                            GeoObjectList l = new GeoObjectList(lp.Model.AllObjects);
                            l.DecomposeBlocks(false);
                            SortedDictionary<int, List<IGeoObject>> sortedGeoObjectsByDisplayOrder = new SortedDictionary<int, List<IGeoObject>>();
                            foreach (IGeoObject go in l)
                            {
                                int ind = 0;
                                if (go.Layer != null) ind = go.Layer.DisplayOrder;
                                List<IGeoObject> insertInto;
                                if (!sortedGeoObjectsByDisplayOrder.TryGetValue(ind, out insertInto))
                                {
                                    insertInto = new List<IGeoObject>();
                                    sortedGeoObjectsByDisplayOrder[ind] = insertInto;
                                }
                                insertInto.Add(go);
                            }
                            foreach (List<IGeoObject> list in sortedGeoObjectsByDisplayOrder.Values)
                            {
                                GeoObjectList dbg = new GeoObjectList(list.ToArray());
                                foreach (IGeoObject go in list)
                                {
                                    // Sammeln der Geoobjekte
                                    CollectPrintItems(go);
                                }
                            }
                        }
                        else
                        {
                            foreach (IGeoObject go in lp.Model)
                            {
                                // Sammeln der Geoobjekte
                                //Set<Layer> visibleLayers = new Set<Layer>();
                                //foreach (Layer layer in lp.visibleLayers.Keys)
                                //{
                                //    visibleLayers.Add(layer);
                                //}
                                CollectPrintItems(go);
                            }
                        }
                        DateTime endtime = DateTime.Now;
                        TimeSpan diff = endtime - starttime;
                        // System.Diagnostics.Trace.WriteLine("Benoetigte Zeit  zum Sammeln: " + diff.TotalMilliseconds.ToString() + " ms");
                        PrintPrintItems();

                    }
                }
                else if (gdiView != null)
                {

                    if (paintOnBitmap)
                    {
                        currentGraphics.Transform = new System.Drawing.Drawing2D.Matrix((float)(fct), 0.0f, 0.0f, (float)(-fct), 0.0f, (float)(gdiView.PaperWidth * height / gdiView.PaperHeight));
                    }
                    else
                    {   // hier wird auf Wunsch von Kastenholz skaliert wenns nicht draufpasst.
                        bool doScaling = Settings.GlobalSettings.GetBoolValue("Printing.GDI.Scale", false);
                        bool useMargin = Settings.GlobalSettings.GetBoolValue("Printing.GDI.UseMargin", false);
                        double additionalScaleFactor = Settings.GlobalSettings.GetDoubleValue("Printing.GDI.AdditionalScalingFactor", 100.0);
                        if (doScaling)
                        {
                            if (useMargin) r = e.MarginBounds;
                            if (r.Width / fctpap < gdiView.PaperWidth) fctpap = r.Width / gdiView.PaperWidth;
                            if (r.Height / fctpap < gdiView.PaperHeight) fctpap = r.Height / gdiView.PaperHeight;
                        }
                        fctpap = fctpap * additionalScaleFactor / 100.0;
                        currentGraphics.Transform = new System.Drawing.Drawing2D.Matrix((float)fctpap, 0.0f, 0.0f, (float)-fctpap, r.Left, (float)(gdiView.PaperHeight * fctpap + r.Top));
                    }

                    BoundingRect ext;
                    ext = new BoundingRect(0.0, 0.0, gdiView.PaperWidth, gdiView.PaperHeight);

                    GeoPoint2D ll = ext.GetLowerLeft();
                    GeoPoint2D ur = ext.GetUpperRight();
                    Rectangle clipRectangle = Rectangle.FromLTRB((int)ll.x, (int)ll.y, (int)ur.x, (int)ur.y);
                    Projection pr = (gdiView as IView).Projection.Clone();

                    currentProjection = pr;
                    projection = pr.PerspectiveProjection;
                    if (!projection.IsValid)
                    {   // parallelprojektion
                        projection = new Matrix4(pr.UnscaledProjection);
                    }
                    print2D = !pr.ShowFaces; // bei 2D wird direkt ausgegeben, nicht sortiert
                    double plfact, dx, dy;
                    pr.GetPlacement(out plfact, out dx, out dy);
                    // projection = new Matrix4(new double[,] { { plfact, 0, 0, dx }, { 0, plfact, 0, dy }, { 0, 0, plfact, 0.0 }, { 0, 0, 0, 1 } }) * projection;
                    currentPrecision = 0.5;//0.1; // noch vernünftig setzen
                                           //currentCollection = new OctTree<IPrintItemImpl>(lp.Model.Extent, 0.1); // Parameter noch falsch             
                    currentCollectionList = new List<IPrintItemImpl>();
                    currentCollectionQuad = new QuadTree<IPrintItemImpl>(new BoundingRect(gdiView.Model.Extent.Xmin, gdiView.Model.Extent.Ymin, gdiView.Model.Extent.Xmax, gdiView.Model.Extent.Ymax));
                    // Sorgt nicht fuer eine Verbesserung

                    //currentCollectionQuad.MaxDeepth = 9;
                    //currentCollectionQuad.MaxListLen = 9;
                    // System.Diagnostics.Trace.WriteLine("Beginn: Zeitmessung der QuadTreeErstellung");
                    DateTime starttime = DateTime.Now;
                    if (print2D)
                    {
                        (this as IPaintTo3D).PaintFaces(PaintTo3D.PaintMode.CurvesOnly);
                    }
                    else
                    {
                        (this as IPaintTo3D).PaintFaces(PaintTo3D.PaintMode.All);
                    }
                    if (Settings.GlobalSettings.GetBoolValue("Printing.UseZOrder", false))
                    {   // nach Layer DisplayOrder sortieren
                        GeoObjectList l = new GeoObjectList(gdiView.Model.AllObjects);
                        l.DecomposeBlocks(false);
                        SortedDictionary<int, List<IGeoObject>> sortedGeoObjectsByDisplayOrder = new SortedDictionary<int, List<IGeoObject>>();
                        foreach (IGeoObject go in l)
                        {
                            int ind = 0;
                            if (go.Layer != null) ind = go.Layer.DisplayOrder;
                            if (go.Layer == null || gdiView.VisibleLayers.IsLayerChecked(go.Layer))
                            {
                                List<IGeoObject> insertInto;
                                if (!sortedGeoObjectsByDisplayOrder.TryGetValue(ind, out insertInto))
                                {
                                    insertInto = new List<IGeoObject>();
                                    sortedGeoObjectsByDisplayOrder[ind] = insertInto;
                                }
                                insertInto.Add(go);
                            }
                        }
                        foreach (List<IGeoObject> list in sortedGeoObjectsByDisplayOrder.Values)
                        {
                            foreach (IGeoObject go in list)
                            {
                                // Sammeln der Geoobjekte
                                CollectPrintItems(go);
                            }
                        }
                    }
                    else
                    {
                        foreach (IGeoObject go in gdiView.Model)
                        {
                            // Sammeln der Geoobjekte
                            CollectPrintItems(go);
                        }
                    }
                    DateTime endtime = DateTime.Now;
                    TimeSpan diff = endtime - starttime;
                    // System.Diagnostics.Trace.WriteLine("Benoetigte Zeit  zum Sammeln: " + diff.TotalMilliseconds.ToString() + " ms");
                    PrintPrintItems();
                }
                if (paintOnBitmap)
                {
                    currentGraphics.Dispose();
                    RectangleF src = new RectangleF(0f, 0f, (float)width, (float)height);
                    RectangleF dest = new RectangleF(0f, 0f, (float)e.PageBounds.Width, (float)e.PageBounds.Height);
                    e.Graphics.DrawImage(bmp, dest, src, GraphicsUnit.Pixel);
                }
            }
            catch (Exception)
            {
                // MessageBox.Show(ex.StackTrace, "Exception in OnPrintPage");
            }
            // DEBUG:
            // bmp.Save(@"C:\Temp\Debug.jpg", System.Drawing.Imaging.ImageFormat.Jpeg);
        }
        public double LineWidthFactor
        {
            get
            {
                return lineWidthFactor;
            }
            set
            {
                lineWidthFactor = value;
            }
        }
        private void Sort(BoundingRect rect, IPrintItemImpl[] toSort)
        {
            for (int i = 0; i < toSort.Length; i++)
            {
                Array.Sort(toSort, new Comparison<IPrintItemImpl>(
                    delegate (IPrintItemImpl pi1, IPrintItemImpl pi2)
                    {
                        return pi1.CompareCommonPointZ(rect, pi2);
                    }));

            }
        }

        int itcount = 0;
        QuadTree<IPrintItemImpl>.IterateAction QuadTree<IPrintItemImpl>.IIterateQuadTreeLists.Iterate(ref BoundingRect rect, HashSet<IPrintItemImpl> objects, bool hasSubNodes)
        {
            ++itcount;
            GraphicsState gs = currentGraphics.Save();
            currentGraphics.SetClip(rect.ToRectangleF());
            double w = currentGraphics.Transform.Elements[0] * rect.Width;
            double h = currentGraphics.Transform.Elements[0] * rect.Height;
            bool toSmall = true; // w < 6 || h < 6;
#if DEBUG
            PointF[] cc = new PointF[] { rect.GetLowerLeft().PointF, rect.GetUpperRight().PointF };
            currentGraphics.Transform.TransformPoints(cc);
            bool doit = false; //  (cc[0].X < 1680 && cc[1].X > 1680 && cc[1].Y < 2870 && cc[0].Y > 2870); // y steht auf dem Kopf
            if (doit && !hasSubNodes)
            {
                foreach (IPrintItemImpl item in objects)
                {
                    GeoObjectList l = item.CheckAll(objects);
                    //if (l != null) for (int i = 0; i < l.Count; i++)
                    //    {
                    //        if (l[i].UserData.Contains("PrintItem"))
                    //        {
                    //            PrintTriangle pt = l[i].UserData.GetData("PrintItem") as PrintTriangle;
                    //            if (pt != null && pt.source != null)
                    //            {
                    //                item.CheckCoverage(pt);
                    //            }
                    //        }
                    //    }
                    if (l != null & !hasSubNodes)
                    {
                        l.Add(item.Debug);
                        HashSet<IGeoObject> sources = new HashSet<IGeoObject>();
                        for (int i = 0; i < l.Count; i++)
                        {
                            if (l[i].UserData.Contains("PrintItem"))
                            {
                                PrintTriangle pt = l[i].UserData.GetData("PrintItem") as PrintTriangle;
                                if (pt != null && pt.source != null) sources.Add(pt.source);
                            }
                        }
                        GeoObjectList sl = new GeoObjectList();
                        foreach (IGeoObject go in sources)
                        {
                            sl.Add(go);
                        }
                    }
                }
                foreach (IPrintItemImpl item in objects)
                {
                    item.Printed = false; // wieder alle auf nicht gedruckt zurücksetzen für desn nächsten Aufruf von Iterate
                    item.Printing = false; // wieder alle auf nicht gedruckt zurücksetzen für desn nächsten Aufruf von Iterate
                }
            }
#endif
            bool ok = true;
            foreach (IPrintItemImpl item in objects)
            {
                ok = item.PrintAll(currentGraphics, shading, objects) && ok;
                // PrintAll sollte im not ok Fall den Zyklus zurückliefern, der die Ausgabe behindert hat
                // dann müsste man das auslösende Dreieck zerstückeln und eine Kopie der Liste in einen neuen QuadTree werfen
                // den man dann ausgibt.
                // Das große Problem sind aber sich gegenseitig durchdringende Flächen. Die resultierenden Dreiecke sind mal vor, mal hinter
                // den anderen, je nach dem, welcher gemeinsame Punkt gewählt wird. Das führt häufig zu Zyklen, wenn z.B. ein Balken eine Wand durchdringt
                if (!ok && hasSubNodes) break;
            }
#if DEBUG
            GeoObjectList pto = new GeoObjectList();
            Polyline polyline = Polyline.Construct();
            polyline.SetPoints(new GeoPoint[] { new GeoPoint(rect.GetLowerLeft()), new GeoPoint(rect.GetLowerRight()), new GeoPoint(rect.GetUpperRight()), new GeoPoint(rect.GetUpperLeft()) }, true);
            pto.Add(polyline);
            IPrintItemImpl pi11 = null;
            foreach (IPrintItemImpl item in objects)
            {
                PrintTriangle pt = item as PrintTriangle;
                if (pt != null && pt.source != null)
                {
                    if (pt.id == 11) pi11 = pt;
                    IGeoObject go = pt.Debug;
                    go.UserData.Add("ID", pt.id);
                    pto.Add(go);
                    // sources.Add(pt.source);
                }
            }
            if (pi11 != null)
            {
                foreach (IPrintItemImpl item in objects)
                {
                    PrintTriangle pt = item as PrintTriangle;
                    if (pt != null)
                    {
                        if (pi11.IsCoverdBy(pt))
                        {
                        }
                    }
                }
            }
#endif
            if (!ok && !hasSubNodes && toSmall)
            {
#if DEBUG
                foreach (IPrintItemImpl item in objects)
                {
                    item.Printed = false;
                    item.Printing = false;
                }
                foreach (IPrintItemImpl item in objects)
                {
                    GeoObjectList l = item.CheckAll(objects);
                    if (l != null)
                    {
                        GeoObjectList lines = new GeoObjectList();
                        for (int i = 0; i < l.Count - 1; i++)
                        {
                            IPrintItemImpl pt1 = l[i].UserData.GetData("PrintItem") as IPrintItemImpl;
                            IPrintItemImpl pt2 = l[i + 1].UserData.GetData("PrintItem") as IPrintItemImpl;
                            GeoPoint2D cp;
                            if (pt1.CommonPoint(pt2, out cp))
                            {
                                double z = pt1.ZPositionAt(cp);
                                GeoPoint sp = new GeoPoint(cp.x, cp.y, z);
                                z = pt2.ZPositionAt(cp);
                                GeoPoint ep = new GeoPoint(cp.x, cp.y, z);
                                Line ln = Line.Construct();
                                ln.SetTwoPoints(sp, ep);
                                lines.Add(ln);
                            }
                            else
                            {
                            }
                        }
                    }
                }
#endif
                IPrintItemImpl[] sorted = new IPrintItemImpl[objects.Count];
                objects.CopyTo(sorted);
                for (int i = 0; i < sorted.Length; i++)
                {
                    sorted[i] = sorted[i].Clone();
                }
                for (int i = 0; i < sorted.Length - 1; i++)
                {
                    for (int j = i + 1; j < sorted.Length; j++)
                    {
                        sorted[i].CheckCoverage(sorted[j], rect);
                    }
                }
                ok = true;
                HashSet<IPrintItemImpl> all = new HashSet<IPrintItemImpl>(sorted);
                for (int i = 0; i < sorted.Length; i++)
                {
                    ok = sorted[i].PrintAll(currentGraphics, shading, all) && ok;
                    if (!ok) break;
                }

                if (!ok)    // im Notfall nach z sortieren
                {
                    GeoPoint2D center = rect.GetCenter();
                    Array.Sort(sorted, new Comparison<IPrintItemImpl>(
                        delegate (IPrintItemImpl pi1, IPrintItemImpl pi2)
                        {
                            return pi1.ZPositionAt(center).CompareTo(pi2.ZPositionAt(center));
                        }));
                    for (int i = 0; i < sorted.Length; i++)
                    {
                        sorted[i].Print(currentGraphics, shading);
                    }
                }
#if DEBUG
                GeoObjectList ptl = new GeoObjectList();
                HashSet<IGeoObject> sources = new HashSet<IGeoObject>();
                for (int i = 0; i < sorted.Length; i++)
                {
                    PrintTriangle pt = sorted[i] as PrintTriangle;
                    if (pt != null && pt.source != null)
                    {
                        ptl.Add(pt.Debug);
                        sources.Add(pt.source);
                    }
                }
                GeoObjectList sl = new GeoObjectList();
                foreach (IGeoObject go in sources)
                {
                    sl.Add(go);
                }
#endif
                // ok = true; dann weiter aufteilen
            }
            currentGraphics.Restore(gs);
            foreach (IPrintItemImpl item in objects)
            {
                item.Printed = false; // wieder alle auf nicht gedruckt zurücksetzen für desn nächsten Aufruf von Iterate
                item.Printing = false; // wieder alle auf nicht gedruckt zurücksetzen für desn nächsten Aufruf von Iterate
            }
            // DEBUG:
            //if (doit)
            //{
            //    Brush whiteBrush = new SolidBrush(Color.Black);
            //    Font fnt = new Font("Small Fonts", 10);
            //    currentGraphics.DrawString(itcount.ToString(), fnt, whiteBrush, new PointF((float)rect.Left, (float)rect.Top));
            //    whiteBrush.Dispose();
            //    fnt.Dispose();
            //}

            if (ok) return QuadTree<IPrintItemImpl>.IterateAction.branchDone;
            else if (toSmall) return QuadTree<IPrintItemImpl>.IterateAction.goDeeper;
            else return QuadTree<IPrintItemImpl>.IterateAction.goDeeperAndSplit;
        }

        private void PrintPrintItems()
        {
            if (checkCoverage)
            {
                // Alle Objekte, die in gleichen QuadTree listen liegen, miteinander in Beziehung setzen
                foreach (List<IPrintItemImpl> pilist in currentCollectionQuad.AllLists)
                {
                    for (int i = 0; i < pilist.Count - 1; i++)
                    {
                        for (int j = i + 1; j < pilist.Count; j++)
                        {
                            pilist[i].CheckCoverage(pilist[j]);
                        }
                    }
                }
                currentCollectionQuad.Iterate(5, this); // 3 heißt, die ganze Fläche von vorneherein in 64 Teile aufteilen
            }
            else
            {
                currentCollectionList.Sort(
                    new Comparison<IPrintItemImpl>(
                        delegate (IPrintItemImpl pi1, IPrintItemImpl pi2)
                        {
                            //if (pi1.IsCoverdBy(pi2)) return -1;
                            //if (pi2.IsCoverdBy(pi1)) return 1;
                            return pi1.ZMaximum.CompareTo(pi2.ZMaximum);
                        }));
                for (int i = 0; i < currentCollectionList.Count; i++)
                {
                    currentCollectionList[i].Print(currentGraphics, shading);
                }
            }
            //if (checkCoverage)
            //{
            // Alle Objekte, die in gleichen QuadTree listen liegen, miteinander in Beziehung setzen
            //    foreach (List<IPrintItemImpl> pilist in currentCollectionQuad.AllLists)
            //    {
            //        for (int i = 0; i < pilist.Count - 1; i++)
            //        {
            //            for (int j = i + 1; j < pilist.Count; j++)
            //            {
            //                pilist[i].CheckCoverage(pilist[j]);
            //            }
            //        }
            //    }
            //}
            //currentCollectionList.Sort(
            //    new Comparison<IPrintItemImpl>(
            //        delegate(IPrintItemImpl pi1, IPrintItemImpl pi2)
            //        {
            //            //if (pi1.IsCoverdBy(pi2)) return -1;
            //            //if (pi2.IsCoverdBy(pi1)) return 1;
            //            return pi1.ZMaximum.CompareTo(pi2.ZMaximum);
            //        }));
            //for (int i = 0; i < currentCollectionList.Count; i++)
            //{
            //    currentCollectionList[i].PrintAll(currentGraphics, shading, BoundingRect.EmptyBoundingRect);
            //}
        }

        private void CollectPrintItems(IGeoObject go)
        {   // wg. visibleLayers Blöcke auflösen
            // System.Diagnostics.Trace.WriteLine("Printing: " + go.GetType().ToString());
            go.PaintTo3D(this);
        }

#region IPaintTo3D Members
        internal class Transform : IDisposable
        {
            private Graphics graphics;
            private Matrix previousTransform;
            public Transform(Graphics graphics, Matrix Transform)
            {
                this.graphics = graphics;
                previousTransform = graphics.Transform;
                graphics.Transform = Transform;
            }
            public Transform(Graphics graphics, Matrix Transform, bool Append)
            {
                this.graphics = graphics;
                previousTransform = graphics.Transform;
                Matrix newTransform = previousTransform.Clone();
                if (Append) newTransform.Multiply(Transform, MatrixOrder.Append);
                else newTransform.Multiply(Transform, MatrixOrder.Prepend);
                graphics.Transform = newTransform;
            }
#region IDisposable Members

            public void Dispose()
            {
                this.graphics.Transform = previousTransform;
            }

#endregion
        }
        private Pen MakePen()
        {
            Color clr;
            clr = currentColor;
            Pen res;
            // lineWidthFactor ist gewöhnlich 1.0
            if (currentLineWidth != null && currentLineWidth.Scale == LineWidth.Scaling.Device)
                res = new Pen(clr, (float)(currentLineWidth.Width * lineWidthFactor));
            else if (currentLineWidth != null)
                res = new Pen(clr, (float)(currentLineWidth.Width * lineWidthFactor * currentProjection.WorldToDeviceFactor));
            else
                res = new Pen(clr, 1.0f);
            if (res.Width == 0.0) res.Width = 0.1f; // 0.1 mm als dünnste Linie, ist wichtig bei Drucken über Bitmap 
            if (currentLinePattern != null && currentLinePattern.Pattern.Length > 0)
            {
                float[] fpattern = new float[currentLinePattern.Pattern.Length];
                float w = res.Width;
                if (w == 0.0f) w = 1.0f;
                float offsetNull = 0.0f; // wenn aus der 0 eine 1 wird (um einen Punkt zu zeichnen) muss der folgende Abstand entsprechend kleiner werden.
                double fct = 1.0; // nicht lineWidthFactor, Herr Rohr möchte das nicht so
                if (currentLinePattern.Scale != LinePattern.Scaling.Device) fct = currentProjection.WorldToDeviceFactor; // s.o. * lineWidthFactor;
                else fct = lineWidthFactor;
                for (int i = 0; i < fpattern.Length; i++)
                {
                    // fpattern[i] = (float)(currentLinePattern[i] * currentProjection.WorldToDeviceFactor / w);
                    //fpattern[i] = (float)(currentLinePattern[i] / w);
                    fpattern[i] = Math.Max(0.0f, (float)(currentLinePattern.Pattern[i] * fct / w) - offsetNull);
                    if (fpattern[i] == 0.0)
                    {
                        fpattern[i] = 1.0f;
                        offsetNull = 1.0f;
                    }
                    else
                    {
                        offsetNull = 0.0f;
                    }
                    //if (fpattern[i] == 0.0) fpattern[i] = 1.0f;
                }
                res.DashPattern = fpattern;
                res.DashOffset = 0.5f; // eine halbe Strichstärke versetzt ist wichtig wg. Mauells Raster
            }
            return res;
        }

        bool IPaintTo3D.PaintSurfaces
        {
            get
            {
                return paintMode != PaintTo3D.PaintMode.CurvesOnly;
            }
        }

        bool IPaintTo3D.PaintEdges
        {
            get
            {
                return paintMode != PaintTo3D.PaintMode.FacesOnly;
            }
        }

        bool IPaintTo3D.PaintSurfaceEdges
        {
            get
            {
                return true;
            }
            set
            {
                throw new NotImplementedException("The method or operation is not implemented.");
            }
        }

        bool IPaintTo3D.UseLineWidth
        {
            get
            {

                return false;
            }
            set
            {

            }
        }

        double IPaintTo3D.Precision
        {
            get
            {
                return currentPrecision;
            }
            set
            {
                throw new NotImplementedException("The method or operation is not implemented.");
            }
        }

        double IPaintTo3D.PixelToWorld
        {
            get { return currentProjection.DeviceToWorldFactor; }
        }

        bool IPaintTo3D.SelectMode
        {
            get
            {
                return false;
            }
            set
            {
                throw new NotImplementedException("The method or operation is not implemented.");
            }
        }

        System.Drawing.Color IPaintTo3D.SelectColor
        {
            get
            {
                throw new NotImplementedException("The method or operation is not implemented.");
            }
            set
            {
                throw new NotImplementedException("The method or operation is not implemented.");
            }
        }

        bool IPaintTo3D.DelayText
        {
            get
            {
                throw new NotImplementedException("The method or operation is not implemented.");
            }
            set
            {
                throw new NotImplementedException("The method or operation is not implemented.");
            }
        }

        bool IPaintTo3D.DelayAll
        {
            get
            {
                throw new NotImplementedException("The method or operation is not implemented.");
            }
            set
            {
                throw new NotImplementedException("The method or operation is not implemented.");
            }
        }

        bool IPaintTo3D.TriangulateText
        {
            get { return false; }
        }
        PaintCapabilities IPaintTo3D.Capabilities
        {
            get
            {
                // aber nicht PaintCapabilities.ZoomIndependentDisplayList
                if (print2D)
                {
                    return PaintCapabilities.CanDoArcs | PaintCapabilities.CanFillPaths;
                }
                else
                {
                    return PaintCapabilities.Standard;
                }
            }
        }

        //void Init(System.Windows.Forms.Control ctrl)
        //{
        //    throw new NotImplementedException("The method or operation is not implemented.");
        //}

        //void Disconnect(System.Windows.Forms.Control ctrl)
        //{
        //    throw new NotImplementedException("The method or operation is not implemented.");
        //}

        void IPaintTo3D.MakeCurrent()
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        void IPaintTo3D.SetColor(System.Drawing.Color color)
        {
            currentColor = color;
        }

        void IPaintTo3D.AvoidColor(System.Drawing.Color color)
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        void IPaintTo3D.SetLineWidth(LineWidth lineWidth)
        {
            currentLineWidth = lineWidth;
        }

        void IPaintTo3D.SetLinePattern(LinePattern pattern)
        {
            currentLinePattern = pattern;
        }

        void IPaintTo3D.Polyline(GeoPoint[] points)
        {
            if (print2D)
            {
                PointF[] pointsf = new PointF[points.Length];
                for (int i = 0; i < points.Length; i++)
                {   // hier ist halt die Frage: enthält die Projektion schon die ganze Zoom/Scroll Info
                    // oder wird das nach graphics gesteckt. Aber das ist ja auch egal hier.
                    GeoPoint p = projection * points[i];
                    pointsf[i] = new PointF((float)p.x, (float)p.y);
                }
                if (graphicsPath != null)
                {
                    graphicsPath.AddLines(pointsf);
                }
                else
                {
                    using (Pen pen = MakePen())
                    {
                        currentGraphics.DrawLines(pen, pointsf);
                    }
                }
            }
            else
            {
                for (int i = 0; i < points.Length - 1; ++i)
                {
                    double w = 1.0;
                    if (currentLineWidth != null) w = currentLineWidth.Width;
                    double[] pat = null;
                    if (currentLinePattern != null) pat = currentLinePattern.Pattern;
                    PrintLine pl = new PrintLine(projection * points[i], projection * points[i + 1], currentColor, w, pat, 0.0);
                    //currentCollection.AddObject(pl);
                    currentCollectionList.Add(pl);
                    currentCollectionQuad.AddObject(pl);
                }
            }
        }

        void IPaintTo3D.FilledPolyline(GeoPoint[] points)
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        void IPaintTo3D.Points(GeoPoint[] points, float size, PointSymbol pointSymbol)
        {
            
        }

        void IPaintTo3D.Triangle(GeoPoint[] vertex, GeoVector[] normals, int[] indextriples)
        {
            if (!print2D)
            {
                GeoPoint[] prvertex = new GeoPoint[vertex.Length];
                GeoVector[] prnormals = new GeoVector[normals.Length];
                for (int i = 0; i < vertex.Length; ++i)
                {
                    prvertex[i] = projection * vertex[i];
                    prnormals[i] = (projection * normals[i]).Normalized;
                }
                for (int i = 0; i < indextriples.Length; i = i + 3)
                {
                    PrintTriangle pt = new PrintTriangle(prvertex, prnormals, indextriples[i], indextriples[i + 1], indextriples[i + 2], currentColor);
#if DEBUG
                    pt.source = geoObjectBeeingRendered;
#endif
                    // Ueberpruefe, ob das Dreieck von oben sichtbar ist, also senkrecht auf der x-y-Ebene steht.
                    // Vektoren
                    GeoVector s = new GeoVector(prvertex[indextriples[i]], prvertex[indextriples[i + 1]]);
                    GeoVector t = new GeoVector(prvertex[indextriples[i]], prvertex[indextriples[i + 2]]);
                    // z-Komponente des KreuzProdukts
                    if (Math.Abs(s.x * t.y - s.y * t.x) < Precision.eps) continue;
                    //currentCollection.AddObject(pt);
                    currentCollectionList.Add(pt);
                    currentCollectionQuad.AddObject(pt);
                }
            }
        }

        void IPaintTo3D.PrepareText(string fontName, string textString, System.Drawing.FontStyle fontStyle)
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        void IPaintTo3D.PrepareIcon(System.Drawing.Bitmap icon)
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }
        void IPaintTo3D.PreparePointSymbol(PointSymbol pointSymbol)
        {

        }
        void IPaintTo3D.Text(GeoVector lineDirection, GeoVector glyphDirection, GeoPoint location, string fontName, string textString, System.Drawing.FontStyle fontStyle, CADability.GeoObject.Text.AlignMode alignment, CADability.GeoObject.Text.LineAlignMode lineAlignment)
        {
            if (print2D)
            {
                glyphDirection = projection * glyphDirection;
                lineDirection = projection * lineDirection;
                if (Math.Abs((glyphDirection ^ lineDirection).z) < Precision.eps) return;
                FontFamily ff;
                try
                {
                    ff = new FontFamily(fontName);
                }
                catch (System.ArgumentException)
                {
                    ff = new FontFamily(System.Drawing.Text.GenericFontFamilies.SansSerif);
                }
                StringFormat sf = StringFormat.GenericTypographic.Clone() as StringFormat;
                sf.LineAlignment = StringAlignment.Near;
                sf.Alignment = StringAlignment.Near;
                int fs = (int)fontStyle;
                int em = ff.GetEmHeight((FontStyle)fs);
                int dc = ff.GetCellDescent((FontStyle)fs);
                int ac = ff.GetCellAscent((FontStyle)fs);
                float acdc = (float)em; //  (ac + dc);
                Font font = new Font(ff, em, (FontStyle)fs, GraphicsUnit.World);
                RectangleF rectf = new RectangleF(0.0f, acdc, acdc, -acdc);
                SizeF txtsize = currentGraphics.MeasureString(textString, font);
                PointF[] plg = new PointF[3];
                //Text txt = Text.Construct(); // blöderweise den Text wieder herstellen, as müsste auch anders gehen
                //txt.Set(lineDirection, glyphDirection, location, fontName, textString, fontStyle, alignment, lineAlignment);
                GeoPoint loc = projection * location;
                switch (lineAlignment)
                {
                    default:
                    case Text.LineAlignMode.Left:
                        // loc = projection * location;
                        break;
                    case Text.LineAlignMode.Center:
                        loc = loc - (txtsize.Width / em / 2.0) * lineDirection;
                        break;
                    case Text.LineAlignMode.Right:
                        loc = loc - (txtsize.Width / em) * lineDirection;
                        break;
                }
                switch (alignment)
                {
                    //case Text.AlignMode.Bottom: loc = loc + (txtsize.Height / (double)em * (ac + dc - em) / (double)em) * glyphDirection; break;
                    //case Text.AlignMode.Baseline: loc = loc - (txtsize.Height / (double)em * (em - ac) / (double)em) * glyphDirection; break;
                    //case Text.AlignMode.Center: loc = loc + (txtsize.Height / (double)em * (ac / 2 + dc / 2 - em) / (double)em) * glyphDirection; break;
                    //case Text.AlignMode.Top: loc = loc - (txtsize.Height / (double)em) * glyphDirection; break;
                    case Text.AlignMode.Bottom: loc = loc + ((ac + dc - em) / (double)em) * glyphDirection; break;
                    case Text.AlignMode.Baseline: loc = loc - ((em - ac) / (double)em) * glyphDirection; break;
                    case Text.AlignMode.Center: loc = loc + ((ac / 2 + dc / 2 - em) / (double)em) * glyphDirection; break;
                    case Text.AlignMode.Top: loc = loc - glyphDirection; break;
                }

                plg[2] = new PointF((float)(loc.x + glyphDirection.x), (float)(loc.y + glyphDirection.y));
                plg[0] = new PointF((float)loc.x, (float)loc.y);
                plg[1] = new PointF((float)(loc.x + lineDirection.x), (float)(loc.y + lineDirection.y));
                System.Drawing.Drawing2D.Matrix m = new System.Drawing.Drawing2D.Matrix(rectf, plg);
                System.Drawing.Drawing2D.GraphicsContainer container = currentGraphics.BeginContainer();
                System.Drawing.Drawing2D.Matrix trsf = currentGraphics.Transform;
                m.Multiply(trsf);
                currentGraphics.Transform = m;
                Brush brush = new SolidBrush(currentColor);
                currentGraphics.DrawString(textString, font, brush, new PointF(0.0f, 0.0f));
                currentGraphics.EndContainer(container);
                font.Dispose();
                brush.Dispose();
            }
            else
            {
                PrintText pt = new PrintText(projection * lineDirection, projection * glyphDirection, projection * location, fontName, textString, fontStyle, alignment, lineAlignment, currentColor);
                //currentCollection.AddObject(pt);
                currentCollectionList.Add(pt);
                currentCollectionQuad.AddObject(pt);
            }
        }

        void IPaintTo3D.List(IPaintTo3DList paintThisList)
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        void IPaintTo3D.SelectedList(IPaintTo3DList paintThisList, int wobbleRadius)
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        void IPaintTo3D.Nurbs(GeoPoint[] poles, double[] weights, double[] knots, int degree)
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        void IPaintTo3D.Line2D(int sx, int sy, int ex, int ey)
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        void IPaintTo3D.Line2D(System.Drawing.PointF p1, System.Drawing.PointF p2)
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        void IPaintTo3D.FillRect2D(System.Drawing.PointF p1, System.Drawing.PointF p2)
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        void IPaintTo3D.Point2D(int x, int y)
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        void IPaintTo3D.DisplayIcon(GeoPoint p, System.Drawing.Bitmap icon)
        {
            //throw new NotImplementedException("The method or operation is not implemented.");
        }
        void IPaintTo3D.DisplayBitmap(GeoPoint p, System.Drawing.Bitmap bitmap)
        {
        }
        void IPaintTo3D.PrepareBitmap(System.Drawing.Bitmap bitmap, int xoffset, int yoffset)
        {
        }
        void IPaintTo3D.PrepareBitmap(System.Drawing.Bitmap bitmap)
        {
        }
        void IPaintTo3D.RectangularBitmap(System.Drawing.Bitmap bitmap, GeoPoint location, GeoVector directionWidth, GeoVector directionHeight)
        {
            if (print2D)
            {
                GeoPoint loc = projection * location;
                GeoVector xDirection = projection * directionWidth;
                GeoVector yDirection = projection * directionHeight;
                PointF[] destPoints = new PointF[3];
                destPoints[0] = (loc + yDirection).ToPointF();
                destPoints[1] = (loc + yDirection + xDirection).ToPointF();
                destPoints[2] = (loc).ToPointF();
                // System.Diagnostics.Trace.WriteLine("PrintBitmap: " + destPoints[0].ToString() + ", " + destPoints[1].ToString() + ", " + destPoints[2].ToString());
                currentGraphics.DrawImage(bitmap, destPoints);
            }
            else
            {
                PrintBitmap pb = new PrintBitmap(bitmap, projection * location, projection * directionWidth, projection * directionHeight);
                currentCollectionList.Add(pb);
                currentCollectionQuad.AddObject(pb);
            }
        }
        void IPaintTo3D.SetProjection(Projection projection, BoundingCube boundingCube)
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        void IPaintTo3D.Clear(System.Drawing.Color background)
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        void IPaintTo3D.Resize(int width, int height)
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        void IPaintTo3D.OpenList(string name)
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        IPaintTo3DList IPaintTo3D.CloseList()
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        void IPaintTo3D.OpenPath()
        {
            if (print2D)
            {
                graphicsPath = new GraphicsPath();
                graphicsPath.FillMode = FillMode.Alternate;
                // ab jetzt auf graphicsPath zeichnen
            }
        }
        void IPaintTo3D.CloseFigure()
        {
            if (print2D)
            {
                graphicsPath.CloseFigure();
            }
        }
        void IPaintTo3D.ClosePath(System.Drawing.Color color)
        {
            if (print2D)
            {
                Brush br = new SolidBrush(color);
                currentGraphics.FillPath(br, graphicsPath);
                graphicsPath = null; // ab jetzt wieder direkt auf GDI zeichnen
                br.Dispose();
            }
        }
        void IPaintTo3D.Arc(GeoPoint center, GeoVector majorAxis, GeoVector minorAxis, double startParameter, double sweepParameter)
        {
            if (print2D)
            {
                GeoVector normal = majorAxis ^ minorAxis; // normale der Ebene des Bogens
                // wird der Bogen von vorne oder hinten betrachtet?
                // statt projection.Direction besser die Richtung zum Mittelpunkt (bei perspektivischer Projektion)
                double sc = currentProjection.Direction.Normalized * normal.Normalized;
                if (Math.Abs(sc) < 1e-6)
                {   // eine Linie
                    GeoVector dir = normal ^ currentProjection.Direction; // Richtung der Linie
                    double pmin, pmax;
                    double par = Geometry.LinePar(center, dir, center + Math.Cos(startParameter) * majorAxis + Math.Sin(startParameter) * minorAxis);
                    pmin = pmax = par;
                    par = Geometry.LinePar(center, dir, center + Math.Cos(startParameter + sweepParameter) * majorAxis + Math.Sin(startParameter + sweepParameter) * minorAxis);
                    if (par < pmin) pmin = par;
                    if (par > pmax) pmax = par;
                    // fehlt noch: jetzt noch die Achsenpunkt abprüfen...
                    (this as IPaintTo3D).Polyline(new GeoPoint[] { center + pmin * dir, center + pmax * dir });
                }
                else
                {
                    GeoPoint2D center2d = (projection * center).To2D();
                    GeoVector2D maj2D = (projection * (center + majorAxis)).To2D() - center2d;
                    GeoVector2D min2D = (projection * (center + minorAxis)).To2D() - center2d;
                    if (maj2D.IsNullVector() || min2D.IsNullVector())
                    {   // eigentlich auch eine Linie
                        return;
                    }
                    GeoPoint2D sp = center2d + Math.Cos(startParameter) * maj2D + Math.Sin(startParameter) * min2D;
                    GeoPoint2D ep = center2d + Math.Cos(startParameter + sweepParameter) * maj2D + Math.Sin(startParameter + sweepParameter) * min2D;
                    GeoPoint dbg = projection * (center + Math.Cos(startParameter) * majorAxis + Math.Sin(startParameter) * minorAxis);
                    dbg = projection * (center + Math.Cos(startParameter + sweepParameter) * majorAxis + Math.Sin(startParameter + sweepParameter) * minorAxis);
                    normal = projection * normal;
                    bool counterclock = sweepParameter > 0.0;
                    //if (normal.z > 0.0) counterclock = !counterclock;
                    EllipseArc2D ea2d = EllipseArc2D.Create(center2d, maj2D, min2D, sp, ep, counterclock);
                    ea2d.MakePositivOriented();
                    GeoVector2D prmaj2D, prmin2D;
                    // Geometry.PrincipalAxis(maj2D, min2D, out prmaj2D, out prmin2D);
                    prmaj2D = ea2d.majorAxis;
                    prmin2D = ea2d.minorAxis;
                    Angle rot = prmaj2D.Angle;
                    //ModOp2D toHorizontal = ModOp2D.Rotate(center2d, -rot.Radian);
                    ModOp2D fromHorizontal = ModOp2D.Rotate(center2d, rot.Radian);
                    SweepAngle swapar = new SweepAngle(ea2d.StartPoint - center2d, ea2d.EndPoint - center2d);
                    if (counterclock && swapar < 0) swapar += 360;
                    if (!counterclock && swapar > 0) swapar -= 360;
                    float swPar = (float)(ea2d.axisSweep / Math.PI * 180);
                    float stPar = (float)(ea2d.axisStart / Math.PI * 180);
                    try
                    {
                        Matrix r = fromHorizontal.Matrix2D;
                        double maxRad = prmaj2D.Length;
                        double minRad = prmin2D.Length;
                        if (graphicsPath != null)
                        {   // kann auch schräge Ellipsen zufügen mit Transformation
                            GraphicsPath tmpPath = new GraphicsPath();
                            tmpPath.AddArc((float)(center2d.x - maxRad), (float)(center2d.y - minRad), (float)(2.0 * maxRad), (float)(2.0 * minRad), stPar, swPar);
                            tmpPath.Transform(r);
                            graphicsPath.AddPath(tmpPath, true);
                        }
                        else
                        {
                            Pen drawPen = MakePen();
                            using (drawPen)
                            {
                                using (new Transform(currentGraphics, r, false))
                                {
                                    currentGraphics.DrawArc(drawPen, (float)(center2d.x - maxRad), (float)(center2d.y - minRad), (float)(2.0 * maxRad), (float)(2.0 * minRad), stPar, swPar);
                                }
                            }
                        }
                    }
                    catch (ArgumentException)
                    {
                    }
                    catch (ModOpException)
                    {
                    }
                }
            }
            else
            {

            }
        }
        IPaintTo3DList IPaintTo3D.MakeList(List<IPaintTo3DList> sublists)
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        void IPaintTo3D.FreeUnusedLists()
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        void IPaintTo3D.UseZBuffer(bool use)
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        void IPaintTo3D.Blending(bool on)
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        void IPaintTo3D.FinishPaint()
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        void IPaintTo3D.PaintFaces(PaintTo3D.PaintMode paintMode)
        {
            this.paintMode = paintMode;
        }

        IDisposable IPaintTo3D.FacesBehindEdgesOffset
        {
            get { throw new NotImplementedException("The method or operation is not implemented."); }
        }

        void IPaintTo3D.Dispose()
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        void IPaintTo3D.PushState()
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        void IPaintTo3D.PopState()
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        void IPaintTo3D.PushMultModOp(ModOp insertion)
        {
            projectionStack.Push(projection);
            projection = projection * new Matrix4(insertion);
        }

        void IPaintTo3D.PopModOp()
        {
            projection = projectionStack.Pop();
        }

        void IPaintTo3D.SetClip(System.Drawing.Rectangle clipRectangle)
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

#endregion

#region IPaintTo3D Member


        public bool DontRecalcTriangulation
        {
            get
            {
                return false;
                //throw new NotImplementedException();
            }
            set
            {
                //throw new NotImplementedException();
            }
        }

        bool IPaintTo3D.DontRecalcTriangulation { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        bool IPaintTo3D.IsBitmap => throw new NotImplementedException();

#endregion

#if DEBUG
        IGeoObject geoObjectBeeingRendered;
        internal void SetGeoObject(IGeoObject go)
        {
            geoObjectBeeingRendered = go;
        }

#endif
    }
}
#endif