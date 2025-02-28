using System;

namespace CADability
{
    internal class ClipRectException : System.ApplicationException
    {
        public ClipRectException()
        {
            base.Source = "internal struct ClipRect";
        }
    }

    /// <summary>
    /// Mit dieser Klasse werden primitive Clip-Funktionen abgehandelt.
    /// </summary>
    public struct ClipRect
    {
        const int ClipLeft = 1;
        const int ClipRight = 2;
        const int ClipBottom = 4;
        const int ClipTop = 8;
        public double Left, Right, Bottom, Top;
        public static int ClipCode(ref GeoPoint2D p, ref BoundingRect rect)
        {
            int res = 0;
            if (p.x < rect.Left) res = ClipLeft;
            else if (p.x > rect.Right) res = ClipRight;
            if (p.y < rect.Bottom) res = res | ClipBottom;
            else if (p.y > rect.Top) res = res | ClipTop;
            return res;
        }
        private int ClipCode(ref GeoPoint2D p) // ref wg. Geschwindigkeit
        {
            int res = 0;
            if (p.x < Left) res = ClipLeft;
            else if (p.x > Right) res = ClipRight;
            if (p.y < Bottom) res = res | ClipBottom;
            else if (p.y > Top) res = res | ClipTop;
            return res;
        }
        public ClipRect(BoundingRect r)
        {
            Left = r.Left;
            Right = r.Right;
            Bottom = r.Bottom;
            Top = r.Top;
        }
        public ClipRect(double Left, double Right, double Bottom, double Top)
        {
            this.Left = Left;
            this.Right = Right;
            this.Bottom = Bottom;
            this.Top = Top;
        }
        public bool Contains(GeoPoint2D p)
        {
            return ClipCode(ref p) == 0;
        }
        /// <summary>
        /// Verändert die durch die beiden gegebenen Punkte gegebene Linie so, dass sie
        /// vom Clip-Rechteck geklippt wird. 
        /// </summary>
        /// <param name="p1">Startpunkt der Linie</param>
        /// <param name="p2">Endpunkt der Linie</param>
        /// <returns>true, wenn etwas von der Linie sichtbar bleibt</returns>
        public bool ClipLine(ref GeoPoint2D p1, ref GeoPoint2D p2)
        {
            int c;
            int c1 = ClipCode(ref p1);
            int c2 = ClipCode(ref p2);
            if ((c1 & c2) != 0) return false;
            int count = 0;
            while ((c1 | c2) != 0)
            {
                GeoPoint2D p = new GeoPoint2D();
                if (c1 != 0) c = c1;
                else c = c2;
                if ((ClipLeft & c) != 0)
                {
                    p.y = p1.y + (p2.y - p1.y) / (p2.x - p1.x) * (Left - p1.x);
                    p.x = Left;
                }
                else
                {
                    if ((ClipRight & c) != 0)
                    {
                        p.y = p1.y + (p2.y - p1.y) / (p2.x - p1.x) * (Right - p1.x);
                        p.x = Right;
                    }
                    else
                    {
                        if ((ClipBottom & c) != 0)
                        {
                            p.x = p1.x + (p2.x - p1.x) / (p2.y - p1.y) * (Bottom - p1.y);
                            p.y = Bottom;
                        }
                        else
                        {
                            if ((ClipTop & c) != 0)
                            {
                                p.x = p1.x + (p2.x - p1.x) / (p2.y - p1.y) * (Top - p1.y);
                                p.y = Top;
                            }
                            else
                            {
                                // nur um den Compiler zufriedenzustellen
                                throw new ClipRectException();
                            }
                        }
                    }
                }
                if (c == c1)
                {
                    p1 = p; c1 = ClipCode(ref p);
                }
                else
                {
                    p2 = p; c2 = ClipCode(ref p);
                }
                ++count;
                if (count > 10)
                {
                    return Math.Abs(p1.x - p2.x) > (Right - Left) / 2 || Math.Abs(p1.y - p2.y) > (Top - Bottom) / 2;
                }
                if ((c1 & c2) != 0) return false;
            }
            return true;
        }
        /// <summary>
        /// Testet, ob die durch die beiden Punkte gegebene Linie von dem Rechteck berührt wird.
        /// </summary>
        /// <param name="p1">Startpunkt der Linie</param>
        /// <param name="p2">Endpunkt der Linie</param>
        /// <returns>true, wenn die Linie berührt wird</returns>
        public bool LineHitTest(GeoPoint2D p1, GeoPoint2D p2)
        {
            int c;
            int c1 = ClipCode(ref p1);
            int c2 = ClipCode(ref p2);
            if ((c1 & c2) != 0) return false;
            if (!(c1 != 0 && c2 != 0)) return true;
            while ((c1 | c2) != 0)
            {
                GeoPoint2D p = new GeoPoint2D();
                if (c1 != 0) c = c1; else c = c2;
                if ((ClipLeft & c) != 0)
                {
                    p.y = p1.y + (p2.y - p1.y) / (p2.x - p1.x) * (Left - p1.x);
                    p.x = Left;
                }
                else
                    if ((ClipRight & c) != 0)
                {
                    p.y = p1.y + (p2.y - p1.y) / (p2.x - p1.x) * (Right - p1.x);
                    p.x = Right;
                }
                else
                        if ((ClipBottom & c) != 0)
                {
                    p.x = p1.x + (p2.x - p1.x) / (p2.y - p1.y) * (Bottom - p1.y);
                    p.y = Bottom;
                }
                else
                            if ((ClipTop & c) != 0)
                {
                    p.x = p1.x + (p2.x - p1.x) / (p2.y - p1.y) * (Top - p1.y);
                    p.y = Top;
                }
                else
                {   // nur um den Compiler zufrieden zu stellen
                    throw new ClipRectException();
                }
                if (c == c1)
                {
                    p1 = p; c1 = ClipCode(ref p);
                }
                else
                {
                    p2 = p; c2 = ClipCode(ref p);
                }
                if ((c1 & c2) != 0) return false;
                if (!(c1 != 0 && c2 != 0)) return true;
            }
            return true;
        }
        public static bool LineHitTest(GeoPoint2D p1, GeoPoint2D p2, ref BoundingRect rect)
        {
            int c;
            int c1 = ClipCode(ref p1, ref rect);
            int c2 = ClipCode(ref p2, ref rect);
            if ((c1 & c2) != 0) return false;
            if (!(c1 != 0 && c2 != 0)) return true;
            while ((c1 | c2) != 0)
            {
                GeoPoint2D p = new GeoPoint2D();
                if (c1 != 0) c = c1; else c = c2;
                if ((ClipLeft & c) != 0)
                {
                    p.y = p1.y + (p2.y - p1.y) / (p2.x - p1.x) * (rect.Left - p1.x);
                    p.x = rect.Left;
                }
                else
                    if ((ClipRight & c) != 0)
                {
                    p.y = p1.y + (p2.y - p1.y) / (p2.x - p1.x) * (rect.Right - p1.x);
                    p.x = rect.Right;
                }
                else
                        if ((ClipBottom & c) != 0)
                {
                    p.x = p1.x + (p2.x - p1.x) / (p2.y - p1.y) * (rect.Bottom - p1.y);
                    p.y = rect.Bottom;
                }
                else
                            if ((ClipTop & c) != 0)
                {
                    p.x = p1.x + (p2.x - p1.x) / (p2.y - p1.y) * (rect.Top - p1.y);
                    p.y = rect.Top;
                }
                else
                {   // nur um den Compiler zufrieden zu stellen
                    throw new ClipRectException();
                }
                if (c == c1)
                {
                    p1 = p; c1 = ClipCode(ref p, ref rect);
                }
                else
                {
                    p2 = p; c2 = ClipCode(ref p, ref rect);
                }
                if ((c1 & c2) != 0) return false;
                if (!(c1 != 0 && c2 != 0)) return true;
            }
            return true;
        }
        public bool ArcHitTest(GeoPoint2D c, double r, int q, GeoPoint2D ps, GeoPoint2D pe)
        {
            int c0, c1, c2, s;
            double z;
            GeoPoint2D p = new GeoPoint2D(0.0, 0.0);

            s = 0;
            c1 = ClipCode(ref ps);
            c2 = ClipCode(ref pe);
            if ((c1 & c2) != 0) return false;
            if (c1 == 0) return true;
            if (c2 == 0) return true;
            while ((c1 | c2) != 0)
            {
                c0 = c1 != 0 ? c1 : c2;
                if ((ClipLeft & c0) != 0)
                {
                    if ((ClipLeft & s) != 0) return false; // Notausgang
                    s = s | ClipLeft;

                    z = c.x - Left;
                    z = r * r - z * z;
                    if (z > 0)
                    {
                        if (q == 0 || q == 1) p.y = c.y + Math.Sqrt(z);
                        else p.y = c.y - Math.Sqrt(z);
                    }
                    else p.y = c.y;
                    p.x = Left;
                }
                else if ((ClipRight & c0) != 0)
                {
                    if ((ClipRight & s) != 0) return false;
                    s = s | ClipRight;

                    z = Right - c.x;
                    z = r * r - z * z;
                    if (z > 0)
                    {
                        if (q == 0 || q == 1) p.y = c.y + Math.Sqrt(z);
                        else p.y = c.y - Math.Sqrt(z);
                    }
                    else p.y = c.y;
                    p.x = Right;
                }
                else if ((ClipBottom & c0) != 0)
                {
                    if ((ClipBottom & s) != 0) return false;
                    s = s | ClipBottom;

                    z = c.y - Bottom;
                    z = r * r - z * z;
                    if (z > 0)
                    {
                        if (q == 0 || q == 3) p.x = c.x + Math.Sqrt(z);
                        else p.x = c.x - Math.Sqrt(z);
                    }
                    else p.x = c.x;
                    p.y = Bottom;
                }
                else if ((ClipTop & c0) != 0)
                {
                    if ((ClipTop & s) != 0) return false;
                    s = s | ClipTop;

                    z = Top - c.y;
                    z = r * r - z * z;
                    if (z > 0)
                    {
                        if (q == 0 || q == 3) p.x = c.x + Math.Sqrt(z);
                        else p.x = c.x - Math.Sqrt(z);
                    }
                    else p.x = c.x;
                    p.y = Top;
                }
                if (c0 == c1)
                {
                    ps = p;
                    c1 = ClipCode(ref p);
                }
                else
                {
                    pe = p;
                    c2 = ClipCode(ref p);
                }
                if ((c1 & c2) != 0) return false;
                if (c1 == 0) return true;
                if (c2 == 0) return true;
            }
            return true;
        }
        internal double sqr(double x) { return x * x; }
        public bool EllipseArcHitTest(GeoPoint2D cnt, double rx, double ry, Angle a, int q, GeoPoint2D ps, GeoPoint2D pe)
        {
            int c, s;
            double a1, a2, a3, d;
            GeoPoint2D p = cnt; // nur damit vorbesetzt und der Compiler nicht schimpft
            // da nur in einem quadraten, kann und darf der bogen nicht ueber 360 grad
            // gehen. im folgenden wird auf quadranten 0..3 reduziert
            if (rx == 0 || ry == 0) return false;
            s = 0;
            int c1 = ClipCode(ref ps);
            int c2 = ClipCode(ref pe);
            if ((c1 & c2) != 0) return false;
            a1 = -Math.Sin(2 * a) * (sqr(rx) - sqr(ry));
            a2 = sqr(ry) * sqr(-Math.Sin(a)) + sqr(rx) * sqr(Math.Cos(a));
            a3 = sqr(ry) * sqr(Math.Cos(a)) + sqr(rx) * sqr(-Math.Sin(a));
            while ((c1 | c2) != 0)
            {
                if (c1 == 0) c = c2; else c = c1;
                if ((ClipLeft & c) != 0)
                {
                    if ((ClipLeft & s) != 0) return false;
                    s = s | ClipLeft;
                    p.x = Left - cnt.x;
                    d = Math.Abs(sqr(p.x * a1) - 4 * a2 * (a3 * sqr(p.x) - sqr(rx * ry)));
                    if (q == 0 || q == 1) p.y = (-p.x * a1 + Math.Sqrt(d)) / (2 * a2);
                    else p.y = (-p.x * a1 - Math.Sqrt(d)) / (2 * a2);
                    p.y = p.y + cnt.y;
                    p.x = Left;
                }
                else
                    if ((ClipRight & c) != 0)
                {
                    if ((ClipRight & s) != 0) return false;
                    s = s | ClipRight;
                    p.x = Right - cnt.x;
                    d = Math.Abs(sqr(p.x * a1) - 4 * a2 * (a3 * sqr(p.x) - sqr(rx * ry)));
                    if (q == 0 || q == 1) p.y = (-p.x * a1 + Math.Sqrt(d)) / (2 * a2);
                    else p.y = (-p.x * a1 - Math.Sqrt(d)) / (2 * a2);
                    p.y = p.y + cnt.y;
                    p.x = Right;
                }
                else
                        if ((ClipBottom & c) != 0)
                {
                    if ((ClipBottom & s) != 0) return false;
                    s = s | ClipBottom;
                    p.y = Bottom - cnt.y;
                    d = Math.Abs(sqr(p.y * a1) - 4 * a3 * (a2 * sqr(p.y) - sqr(rx * ry)));
                    if (q == 0 || q == 3) p.x = (-p.y * a1 + Math.Sqrt(d)) / (2 * a3);
                    else p.x = (-p.y * a1 - Math.Sqrt(d)) / (2 * a3);
                    p.x = p.x + cnt.x;
                    p.y = Bottom;
                }
                else
                            if ((ClipTop & c) != 0)
                {
                    if ((ClipTop & s) != 0) return false;
                    s = s | ClipTop;
                    p.y = Top - cnt.y;
                    d = Math.Abs(sqr(p.y * a1) - 4 * a3 * (a2 * sqr(p.y) - sqr(rx * ry)));
                    if (q == 0 || q == 3) p.x = (-p.y * a1 + Math.Sqrt(d)) / (2 * a3);
                    else p.x = (-p.y * a1 - Math.Sqrt(d)) / (2 * a3);
                    p.x = p.x + cnt.x;
                    p.y = Top;
                }
                if (c == c1)
                {
                    ps = p;
                    c1 = ClipCode(ref p);
                }
                else
                {
                    pe = p;
                    c2 = ClipCode(ref p);
                }
                if ((c1 & c2) != 0) return false;
            }
            return true;
        }
        public bool ParallelogramHitTest(GeoPoint2D p, GeoVector2D v1, GeoVector2D v2)
        {
            // p1 = p;
            GeoPoint2D p2 = p + v1;
            GeoPoint2D p3 = p + v1 + v2;
            GeoPoint2D p4 = p + v2;
            int c1 = ClipCode(ref p);
            int c2 = ClipCode(ref p2);
            int c3 = ClipCode(ref p3);
            int c4 = ClipCode(ref p4);
            if ((c1 & c2 & c3 & c4) != 0) return false; // alle Punkte auf einer Seite des Cliprechtecks
            if ((c1 == 0) || (c2 == 0) || (c3 == 0) || (c4 == 0)) return true; // ein Punkt innerhalb des Cliprechteck

            // eine der 4 Seiten schneidet das Cliprechteck
            if (LineHitTest(p, p2)) return true;
            if (LineHitTest(p2, p3)) return true;
            if (LineHitTest(p3, p4)) return true;
            if (LineHitTest(p4, p)) return true;

            // jetzt könnte nur noch das Parallelogramm komplett innerhalb des Cliprechtecks liegen
            GeoPoint2D center = new GeoPoint2D((Left + Right) / 2.0, (Bottom + Top) / 2.0);
            // ersatzweise wird hier der Mittelpunkt getestet
            if (Geometry.OnLeftSide(center, p, v1) != Geometry.OnLeftSide(center, p + v2, v1))
            {	// der Mittelpunkt des Rechtecks liegt auf verschiedenen Seiten der beiden 
                // parallelen Seiten des Parallelogramms. Jetzt noch mit den beiden
                // anderen Seiten probieren:
                if (Geometry.OnLeftSide(center, p, v2) != Geometry.OnLeftSide(center, p + v1, v2))
                    return true; // also Mittelpunkt innerhalb
            }
            return false;
        }
        public bool TriangleHitTest(GeoPoint2D p1, GeoPoint2D p2, GeoPoint2D p3)
        {
            int c1 = ClipCode(ref p1);
            int c2 = ClipCode(ref p2);
            int c3 = ClipCode(ref p3);
            if ((c1 & c2 & c3) != 0) return false; // alle Punkte auf einer Seite des Cliprechtecks
            if ((c1 == 0) || (c2 == 0) || (c3 == 0)) return true; // ein Punkt innerhalb des Cliprechteck

            // eine der 3 Seiten schneidet das Cliprechteck
            if (LineHitTest(p1, p2)) return true;
            if (LineHitTest(p2, p3)) return true;
            if (LineHitTest(p3, p1)) return true;

            // jetzt könnte nur noch das Cliprechteck komplett im Dreieck liegen
            GeoPoint2D center = new GeoPoint2D((Left + Right) / 2.0, (Bottom + Top) / 2.0);
            // ersatzweise wird hier der Mittelpunkt getestet
            // der Mittelpunkt des Rechtecks muss auf der selben Seite von allen Dreiecksseiten liegen
            // also immer rechts, oder immer links wenn es drinnen sein soll.
            // das liese sich auch mit dem Überstreichwinkel testen (wie Border)
            bool OnLeftSidep2p3 = Geometry.OnLeftSide(center, p2, p3 - p2);
            if (Geometry.OnLeftSide(center, p1, p2 - p1) != OnLeftSidep2p3) return false;
            if (OnLeftSidep2p3 != Geometry.OnLeftSide(center, p3, p1 - p3)) return false;
            return true; // auf der gleichen Seite für alle drei Dreiecks-Seiten
        }
        public int PointsHitTest(GeoPoint2D[] points)
        {   // testet ob ein Punkt innerhalb liegt, oder alle Punkte auf einer Seite. Wenn beides nicht, dann unentschieden
            int cand = 0x0F; // alle Bits gesetzt
            for (int i = 0; i < points.Length; ++i)
            {
                int c = ClipCode(ref points[i]);
                if (c == 0) return 1; // ein Punkt innerhalb
                cand &= c;
            }
            if (cand != 0) return 0; // alle auf der selben Seite
            return -1; // der unentschiedene Fall
        }
        public GeoPoint2D Center
        {
            get
            {
                return new GeoPoint2D((Left + Right) / 2.0, (Bottom + Top) / 2.0);
            }
        }
        public ClipRect[] Split()
        {
            GeoPoint2D cnt = Center;
            ClipRect[] res = new ClipRect[4];
            res[0].Left = Left;
            res[0].Bottom = Bottom;
            res[0].Right = cnt.x;
            res[0].Top = cnt.y;

            res[1].Left = Left;
            res[1].Bottom = cnt.y;
            res[1].Right = cnt.x;
            res[1].Top = Top;

            res[2].Left = cnt.x;
            res[2].Bottom = cnt.y;
            res[2].Right = Right;
            res[2].Top = Top;

            res[3].Left = cnt.x;
            res[3].Bottom = Bottom;
            res[3].Right = Right;
            res[3].Top = cnt.y;

            return res;
        }
        public double Size
        {
            get
            {
                return (Right - Left) + (Top - Bottom);
            }
        }
    }
}
