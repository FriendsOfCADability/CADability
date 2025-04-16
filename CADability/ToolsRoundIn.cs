using CADability.Curve2D;
using CADability.GeoObject;
using CADability.UserInterface;
using System;

namespace CADability.Actions
{
	/// <summary>
	/// 
	/// </summary>
	internal class ToolsRoundIn : ConstructAction
	{
		private GeoPoint objectPoint; // der (evtl. mittlere) Pickpunkt zum Runden
		private GeoPoint objectPoint1; // der Pickpunkt der ersten Curve
		private GeoPoint objectPointSav = new GeoPoint(0, 0, 0); // der  Pickpunkt zum Runden als Merker für onDone
		private ICurve iCurve1; // lokales Element
		private ICurve iCurve2; // lokales Element
		private ICurve iCurveComposedSplit; // lokales Element
		private ICurve iCurveComposedSingle2Objects; // lokales Element
		private ICurve iCurve1Sav; // lokales Element
		private CurveInput roundObject;
		private CurveInput roundObject2;
		private LengthInput roundRadius;
		private double roundRad; // der globale RundungsRadius
								 //		private double roundRadCalc; // der globale RundungsRadius berechnet
		private Ellipse arc;
		private IGeoObjectOwner owner;
		private IGeoObjectOwner ownerCreated;
		private Path pathCreatedFromModel; //  synthetisch erzeugter Pfad zusammenhängender Objekte
		private bool composedSplit;
		//		private bool composedSingle;
		private bool composedSingle2Objects;


		public ToolsRoundIn()
		{ }

		private bool showRoundPathTestIntersection(ICurve iCurveTest)
		{   // jetzt getestet, ob der Pfad sich selbst überschneidet:
			//			owner = (iCurveTest as IGeoObject).Owner; // owner merken für löschen und Einfügen

			if (iCurveTest.GetPlanarState() == PlanarState.Planar) // also in einer Ebene
			{
				Plane pl = iCurveTest.GetPlane();
				ICurve2D curve_2D = iCurveTest.GetProjectedCurve(pl); // die 2D-Kurve
				if (curve_2D is Path2D) (curve_2D as Path2D).Flatten();
				ICurve2D curveTemp;
				if (iCurveTest is Polyline)
				{ // GetSelfIntersections geht nur mit Pfaden!
					Path p = Path.Construct();
					p.Set(new ICurve[] { iCurveTest.Clone() });
					p.Flatten();
					curveTemp = p.GetProjectedCurve(pl);
				}
				else curveTemp = curve_2D;
				double[] pathIntersectionPoints = curveTemp.GetSelfIntersections(); // die Schnittpunkte mit sich selbst
				if (pathIntersectionPoints.Length > 0)
				{
					double distS = double.MaxValue; // Entfernung des Pickpunkts zu den Schnittpunkten
					GeoPoint2D objectPoint2D = new GeoPoint2D(0.0, 0.0);
					int interSectIndex = 0;
					for (int i = 0; i < pathIntersectionPoints.Length; i += 2) // immer paarweise, jeweils am Anfang und Ende
					{ // den nächsten Schnittpunkt bestimmen, einer pro Paar reicht dazu
						GeoPoint2D ps = curveTemp.PointAt(pathIntersectionPoints[i]);
						double distLoc = Geometry.Dist(ps, pl.Project(objectPoint));
						if (distLoc < distS)
						{
							distS = distLoc;
							objectPoint2D = ps; // Pickpunkt  merken
							interSectIndex = i;
						}
					}
					// jetzt hat er den nächsten Kreuzungspunkt bestimmt. Nun nachschauen, ob die Mouseposition evtl. noch näher an einem gewöhnlichen Pfadpunkt steht! Wenn ja: nichts machen, der Resr erfolgt dann weiter unten in showRound!
					for (int i = 0; i < (curveTemp as Path2D).SubCurvesCount; i++)
					{
						double distLoc = Geometry.Dist((curveTemp as Path2D).SubCurves[i].StartPoint, pl.Project(objectPoint));
						if (distLoc < distS)
						{ // ein Pfadpunkt liegt näher: das reicht! Raus hier!
							return false;
						}
					}

					// Jetzt: die Kreuzungspunkte (einer von vorne, einer von hinten) in die Testkurve einfuegen!
					if (iCurveTest is Path)
					{
						(iCurveTest as Path).InsertPoint(pathIntersectionPoints[interSectIndex]);
						(iCurveTest as Path).InsertPoint(pathIntersectionPoints[interSectIndex + 1]);
					}
					if (iCurveTest is Polyline)
					{
						(iCurveTest as Polyline).InsertPoint(pathIntersectionPoints[interSectIndex]);
						(iCurveTest as Polyline).InsertPoint(pathIntersectionPoints[interSectIndex + 1]);
					}
					return true;
				}
			}
			return false;
		}

		private bool ShowRound()
		{   // jetzt werden die Rundparameter bestimmt
			base.ActiveObject = null;
			base.FeedBack.ClearSelected();
			if ((iCurve1 != null) && (iCurve2 != null) && (iCurve1 != iCurve2))
			{
				Plane pl;
				if (Curves.GetCommonPlane(iCurve1, iCurve2, out pl))
				{
					if (composedSplit)
					{
						if (iCurveComposedSplit != null) owner = (iCurveComposedSplit as IGeoObject).Owner; // owner merken für löschen und Einfügen
						else owner = ownerCreated;
					}
					else owner = (iCurve1 as IGeoObject).Owner; // owner merken für löschen und Einfügen
																//				owner = (iCurve1 as IGeoObject).Owner; // owner merken für löschen und Einfügen
					bool rndPos = false; // Runden möglich
					double distCP; // Entfernung Pickpunkt - Schnittpunkte
					GeoPoint2D arcP1 = new GeoPoint2D(0.0, 0.0);
					GeoPoint2D arcP2 = new GeoPoint2D(0.0, 0.0);
					GeoPoint2D arcCenter = new GeoPoint2D(0.0, 0.0);
					ICurve2D curve1_2D = iCurve1.GetProjectedCurve(pl); // die 2D-Kurven
					if (curve1_2D is Path2D) (curve1_2D as Path2D).Flatten();
					ICurve2D curve2_2D = iCurve2.GetProjectedCurve(pl);
					if (curve2_2D is Path2D) (curve2_2D as Path2D).Flatten();
					// hier die Schnittpunkte bestimmen und den cutPoint auf den nächsten Schnittpunt setzen
					GeoPoint2DWithParameter cutPoint;
					rndPos = Curves2D.NearestPoint(curve1_2D.Intersect(curve2_2D), pl.Project(objectPoint), out cutPoint);
					if (rndPos) // runden war möglich
					{
						arcCenter = cutPoint.p;
						double locmin1, locmin2; // Parametergrenzen vorbesetzen für den Fall: Realer Schnittpunkt
						locmin1 = 0.0; // Parametergrenzen vorbesetzen für den Fall: Realer Schnittpunkt
						locmin2 = 0.0; // Parametergrenzen vorbesetzen für den Fall: Realer Schnittpunkt
						double locmax1, locmax2; // Parametergrenzen vorbesetzen für den Fall: Realer Schnittpunkt
						locmax1 = 1.0;
						locmax2 = 1.0;
						double locPar; // für den Fall: virtueller Schnittpunkt die Parametergrenzen anpassen
						locPar = curve1_2D.PositionOf(cutPoint.p); // position des Schnittpunktes auf der Kurve1
																   //if ( locPar > 0.5) locmax1 = locPar;
																   //else locmin1 = locPar;
						if (locPar > 1.0) locmax1 = locPar;
						if (locPar < 0.0) locmin1 = locPar;
						locPar = curve2_2D.PositionOf(cutPoint.p); // position des Schnittpunktes auf der Kurve2
						if (locPar > 1.0) locmax2 = locPar;
						if (locPar < 0.0) locmin2 = locPar;
						//if (locPar > 0.5) locmax2 = locPar;
						//else locmin2 = locPar;
						// Kreis synthetisieren
						arc.SetArcPlaneCenterRadius(pl, pl.ToGlobal(arcCenter), roundRad);
						ICurve2D curveArc_2D = (arc as ICurve).GetProjectedCurve(pl);

						// Schnittpunkte Kreisbogen mit Kurve 1
						GeoPoint2DWithParameter[] cutPoints = curve1_2D.Intersect(curveArc_2D);
						distCP = double.MaxValue;
						for (int i = 0; i < cutPoints.Length; ++i) // Schleife über alle Schnittpunkte
						{   // nur der Schnittpunkt innerhalb der Kurve
							if ((cutPoints[i].par1 > locmin1) & (cutPoints[i].par1 < locmax1))
							{
								double distLoc = Geometry.Dist(cutPoints[i].p, pl.Project(objectPoint));
								if (distLoc < distCP)
								{
									distCP = distLoc;
									arcP1 = cutPoints[i].p; // Schnittpunkt schonmal merken	
								}
								//arcP1 = cutPoints[i].p;
								//break;
							}

						}
						// Schnittpunkte Kreisbogen mit Kurve 2
						cutPoints = curve2_2D.Intersect(curveArc_2D);
						distCP = double.MaxValue;
						for (int i = 0; i < cutPoints.Length; ++i) // Schleife über alle Schnittpunkte
						{   // nur der Schnittpunkt innerhalb der Kurve
							if ((cutPoints[i].par1 > locmin2) & (cutPoints[i].par1 < locmax2))
							{
								double distLoc = Geometry.Dist(cutPoints[i].p, pl.Project(objectPoint));
								if (distLoc < distCP)
								{
									distCP = distLoc;
									arcP2 = cutPoints[i].p; // Schnittpunkt schonmal merken	
								}
								// arcP2 = cutPoints[i].p;
								// break;
							}
						}
						if (!Precision.IsEqual(arcP1, arcP2)) // runden war möglich
						{
							// Mittelwert zwischen dem Kurven
							//                            roundRadCalc = (Math.Abs(curve1_2D.Distance(pl.Project(radiusPoint))) + Math.Abs(curve2_2D.Distance(pl.Project(radiusPoint)))) / 2.0;
							//						objectPoint = pl.ToGlobal(cutPoint.p); // merken für onDone, als Entscheidungskriterium
							arc.SetArcPlaneCenterStartEndPoint(base.ActiveDrawingPlane, arcCenter, arcP1, arcP2, pl, false);
							Ellipse arc1 = Ellipse.Construct();
							arc1.SetArcPlaneCenterStartEndPoint(base.ActiveDrawingPlane, arcCenter, arcP1, arcP2, pl, true);
							if (Math.Abs(arc.SweepParameter) > Math.Abs(arc1.SweepParameter)) // es ist immer der kleinere Bogen!
							{
								arc = arc1;
							}
							arc.CopyAttributes(iCurve1 as IGeoObject);
							base.ActiveObject = arc; // merken
							base.FeedBack.AddSelected(arc);// darstellen
							return true;
						}
					}
				}
			}
			//            roundObject.HitCursor = CursorTable.GetCursor("RoundIn.cur");
			return false;
		}

		private bool RoundObject(CurveInput sender, ICurve[] Curves, bool up)
		{   // ... nur die sinnvolen Kurven verwenden
			objectPoint1 = base.CurrentMousePosition;
			objectPoint = objectPoint1;
			if (up)
				if (Curves.Length == 0) sender.SetCurves(Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
				else sender.SetCurves(Curves, Curves[0]);
			//            composedSingle = false;
			composedSplit = false;
			iCurveComposedSplit = null;
			pathCreatedFromModel = null;
			if (Curves.Length == 1)
			{   // er hat nur eine Kurve gewählt, also roundObject2 freischalten
				iCurve1 = Curves[0];
				iCurve1Sav = iCurve1;
				roundObject2.Optional = false;
				roundObject2.ReadOnly = false;
				roundObject.HitCursor = CursorTable.GetCursor("RoundIn.cur");
				base.ActiveObject = null;
				base.FeedBack.ClearSelected();

				//				composedSplit = false;
				ICurve pathHit = null;
				if (Curves[0].IsComposed) // also: Pfad oder Polyline
				{
					pathHit = Curves[0].Clone();
					iCurveComposedSplit = Curves[0];
					// merken für OnDone zum löschen und Attributsetzen
					ToolsRoundOff.pathTestIntersection(pathHit, objectPoint);
					//                    showRoundPathTestIntersection(pathHit);
				}
				else
				{
					Path p = CADability.GeoObject.Path.CreateFromModel(Curves[0] as ICurve, Frame.ActiveView.Model, Frame.ActiveView.Projection, true);
					if (p != null) pathHit = p as ICurve;
					pathCreatedFromModel = p;
					ownerCreated = (Curves[0] as IGeoObject).Owner;
				}
				if (pathHit != null)
				{   // jetzt an der Mausposition aufknacken in 2 Pathes
					ICurve[] tmpCurves = roundObject.SplitAtMousePosition(pathHit);
					if (tmpCurves.Length == 2)
					{
						iCurve1 = tmpCurves[0];
						iCurve2 = tmpCurves[1];
						composedSplit = true;
						if (ShowRound())
						{
							roundObject2.Optional = true;
							roundObject2.ReadOnly = true;
							roundObject.HitCursor = CursorTable.GetCursor("RoundInReady.cur");
						}
						else
						{
							iCurve1 = Curves[0];
							composedSplit = false;
							roundObject2.Optional = false;
							roundObject2.ReadOnly = false;
							roundObject.HitCursor = CursorTable.GetCursor("RoundIn.cur");
						}
					}
				}
				return true;
				//				base.SetFocus(roundObject2,true);
			}
			if (Curves.Length >= 2)
			{   // nachsehen, ob ein realer Schnittpunkt da ist
				double[] cutPlace = CADability.GeoObject.Curves.Intersect(Curves[0], Curves[1], true);
				if (cutPlace.Length > 0) // nur reale Schnittpunkte sollen gelten
				{   // er hat zwei Kurven gewählt, also roundObject2 ausschalten
					iCurve1 = Curves[0];
					iCurve2 = Curves[1];
					if (ShowRound())
					{
						roundObject2.Optional = true;
						roundObject2.ReadOnly = true;
						roundObject.HitCursor = CursorTable.GetCursor("RoundInReady.cur");
						return true;
					}
					else
					{
						roundObject.HitCursor = CursorTable.GetCursor("RoundIn.cur");
						roundObject2.Optional = false;
						roundObject2.ReadOnly = false;
						base.ActiveObject = null;
						base.FeedBack.ClearSelected();
						return false;
					}

					//                    return(showRound());
				}
			}
			roundObject.HitCursor = CursorTable.GetCursor("RoundIn.cur");
			roundObject2.Optional = false;
			roundObject2.ReadOnly = false;
			base.ActiveObject = null;
			base.FeedBack.ClearSelected();
			return false;
		}

		private void RoundObjectChanged(CurveInput sender, ICurve SelectedCurve)
		{
			// da machen wir nichts!
		}

		private bool RoundObject2(CurveInput sender, ICurve[] Curves, bool up)
		{   // Mittelpunkt der beiden Pickpunkte
			objectPoint = new GeoPoint(base.CurrentMousePosition, objectPoint1);
			if (up)
				if (Curves.Length == 0) sender.SetCurves(Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
				else sender.SetCurves(Curves, Curves[0]);
			if (Curves.Length > 0)
			{
				if (composedSingle2Objects) iCurve1 = iCurve1Sav;
				composedSingle2Objects = false;
				iCurve2 = Curves[0];
				if ((iCurve1 is Path) & (iCurve1 == iCurve2) & (iCurve1.SubCurves.Length > 1))
				{
					iCurveComposedSplit = iCurve1;
					composedSplit = true;
					iCurve1 = (Curves[0] as Path).Curve(0).Clone(); // das erste 
					iCurve2 = (Curves[0] as Path).Curve((Curves[0] as Path).Count - 1).Clone(); // das letzte 
					if (ShowRound())
					{
						composedSingle2Objects = true;
						// kompiziertes Konstrukt, um den Bogen selber zu entfernen, temporäres Array, eins kürzer 
						ICurve[] iCurveShort = new ICurve[(iCurveComposedSplit as Path).Count - 2];
						for (int i = 0; i < iCurveShort.Length; ++i)
						{   // wir brauchen Clones um den Originalpfad nicht zu zerstören
							iCurveShort[i] = iCurveComposedSplit.SubCurves[i + 1].Clone();
						}
						// Kopie auf das kürzere Array
						// Array.Copy(iCurveComposedSplit.SubCurves,1,iCurveShort,0,(iCurveComposedSplit as Path).Count - 2);
						//  neu mit dem kleinen Array
						iCurveComposedSingle2Objects = Path.Construct();
						(iCurveComposedSingle2Objects as Path).Set(iCurveShort);
						(iCurveComposedSingle2Objects as IGeoObject).CopyAttributes(iCurveComposedSplit as IGeoObject); // gemerkte Attribute setzen
						roundObject2.HitCursor = CursorTable.GetCursor("RoundOffReady.cur");
						return true;
					}
					iCurve1 = iCurveComposedSplit;
				}
				if (ShowRound())
				{
					roundObject2.HitCursor = CursorTable.GetCursor("RoundOffReady.cur");
					return true;
				}
				else
				{
					//                   roundObject2.HitCursor = CursorTable.GetCursor("RoundOff.cur");
					return false;
				}
			}
			base.ActiveObject = null;
			base.FeedBack.ClearSelected();
			return false;
		}

		private void RoundObjectChanged2(CurveInput sender, ICurve SelectedCurve)
		{
			iCurve2 = SelectedCurve;
			ShowRound();
		}

		private bool RoundRadius(double Length)
		{
			if (Length > Precision.eps)
			{
				roundRad = Length;
				ShowRound();
				return true; // sonst wird es bei der Aktion nicht übernommen
			}
			return false;
		}

		//		private double RoundRadiusCalc(GeoPoint MousePosition)
		//		{
		//			radiusPoint = MousePosition;
		//			if (roundObject.Fixed)	{ if (showRound()) return roundRadCalc; }
		//			return roundRad;
		//		}


		/// <summary>
		/// Overrides <see cref="CADability.Actions.ConstructAction.OnSetAction ()"/>
		/// </summary>
		public override void OnSetAction()
		{
			base.ActiveObject = null;
			arc = Ellipse.Construct();
			base.TitleId = "ToolsRoundIn";
			roundRad = ConstrDefaults.DefaultRoundRadius;
			//			roundRadCalc = roundRad;
			roundObject = new CurveInput("ToolsRoundIn.Object");
			roundObject.MouseOverCurvesEvent += new CurveInput.MouseOverCurvesDelegate(RoundObject);
			roundObject.CurveSelectionChangedEvent += new CurveInput.CurveSelectionChangedDelegate(RoundObjectChanged);
			roundObject.HitCursor = CursorTable.GetCursor("RoundIn.cur");
			roundObject.ModifiableOnly = true;
			roundObject2 = new CurveInput("ToolsRoundIn.Object2");
			roundObject2.ReadOnly = true;
			roundObject2.Optional = true;
			roundObject2.MouseOverCurvesEvent += new CurveInput.MouseOverCurvesDelegate(RoundObject2);
			roundObject2.CurveSelectionChangedEvent += new CurveInput.CurveSelectionChangedDelegate(RoundObjectChanged2);
			roundObject2.HitCursor = CursorTable.GetCursor("RoundIn.cur");
			roundObject2.ModifiableOnly = true;
			roundRadius = new LengthInput("ToolsRoundIn.Radius");
			//			roundRadius.Optional = true;
			roundRadius.ForwardMouseInputTo = roundObject;
			roundRadius.DefaultLength = ConstrDefaults.DefaultRoundRadius;
			//			roundRadius.OnCalculateLength +=new Condor.Actions.ConstructAction.LengthInput.CalculateLength(RoundRadiusCalc);
			roundRadius.SetLengthEvent += new CADability.Actions.ConstructAction.LengthInput.SetLengthDelegate(RoundRadius);
			base.SetInput(roundObject, roundObject2, roundRadius);
			base.ShowActiveObject = false;
			base.OnSetAction();
		}
		/// <summary>
		/// Overrides <see cref="CADability.Actions.ConstructAction.OnRemoveAction ()"/>
		/// </summary>
		public override void OnRemoveAction()
		{
			base.OnRemoveAction();
		}

		/// <summary>
		/// Overrides <see cref="CADability.Actions.Action.GetID ()"/>
		/// </summary>
		/// <returns></returns>
		public override string GetID()
		{
			return "ToolsRoundIn";
		}

		/// <summary>
		/// Overrides <see cref="CADability.Actions.ConstructAction.OnDone ()"/>
		/// </summary>
		public override void OnDone()
		{
			if (base.ActiveObject != null)
			{
				using (base.Frame.Project.Undo.UndoFrame)
				{
					Boolean doIt = true;
					// alten Pfad entfernen
					if (composedSplit)
					{
						if (iCurveComposedSplit != null)
							(iCurveComposedSplit as IGeoObject).Owner.Remove(iCurveComposedSplit as IGeoObject);
						else
						{ // die Einzelelemente des CreateFromModel identifizieren
							for (int j = 0; j < pathCreatedFromModel.Count; ++j)
							{
								IGeoObject obj = null;
								if ((pathCreatedFromModel.Curve(j) as IGeoObject).UserData.ContainsData("CADability.Path.Original"))
									obj = (pathCreatedFromModel.Curve(j) as IGeoObject).UserData.GetData("CADability.Path.Original") as IGeoObject;
								if (obj != null && obj.Owner != null) obj.Owner.Remove(obj); // löschen
							}
						}
					}

					//if (composedSingle)
					//{
					//    if (iCurve1.PositionOf(arc.StartPoint) < iCurve1.PositionOf(arc.EndPoint))
					//        iCurve1.Trim(iCurve1.PositionOf(arc.StartPoint), iCurve1.PositionOf(arc.EndPoint));
					//    else iCurve1.Trim(iCurve1.PositionOf(arc.EndPoint), iCurve1.PositionOf(arc.StartPoint));
					//}
					//else
					{
						if (roundRad == 0.0)
						{

							if (iCurve1.PositionOf(objectPointSav) > 0.5)
								iCurve1.Trim(0.0, iCurve1.PositionOf(objectPointSav));
							else
							{
								iCurve1.Trim(iCurve1.PositionOf(objectPointSav), 1.0);
								if (Frame.GetBooleanSetting("Construct.MakePath", true)) iCurve1.Reverse();
							}
							if (iCurve2.PositionOf(objectPointSav) > 0.5)
							{
								iCurve2.Trim(0.0, iCurve2.PositionOf(objectPointSav));
								if (Frame.GetBooleanSetting("Construct.MakePath", true)) iCurve2.Reverse();
							}
							else iCurve2.Trim(iCurve2.PositionOf(objectPointSav), 1.0);
						}
						else
						{

							if (iCurve1.PositionOf(arc.StartPoint) > iCurve1.PositionOf(arc.Center))
							{ iCurve1.Reverse(); } // evtl. umdrehen, damit das Trimmen unten klappt
							if (iCurve2.PositionOf(arc.EndPoint) < iCurve2.PositionOf(arc.Center))
							{ iCurve2.Reverse(); } // evtl. umdrehen, damit das Trimmen unten klappt

							if (iCurve1.IsClosed)
							{ // um nur die letzte Linie/Element wegzutrimmen und nicht größere Teile
								if (iCurve1 is Path)
								{ // setzt den Anfangspunkt so um, dass das Segment am arc.StartPoint das letzte ist 
									(iCurve1 as Path).CyclicalPermutation(arc.StartPoint, false);
								}
								if (iCurve1 is Polyline)
								{ // setzt den Anfangspunkt so um, dass das Segment am arc.StartPoint das erste ist 
									(iCurve1 as Polyline).CyclicalPermutation(arc.StartPoint, false);
								}
							}
							if (iCurve2.IsClosed)
							{ // um nur die letzte Linie/Element wegzutrimmen und nicht größere Teile
								if (iCurve2 is Path)
								{ // setzt den Anfangspunkt so um, dass das Segment am arc.StartPoint das letzte ist 
									(iCurve2 as Path).CyclicalPermutation(arc.EndPoint, true);
								}
								if (iCurve2 is Polyline)
								{ // setzt den Anfangspunkt so um, dass das Segment am arc.StartPoint das erste ist 
									(iCurve2 as Polyline).CyclicalPermutation(arc.EndPoint, true);
								}
							}

							double trimBorderCurve1End = iCurve1.PositionOf(arc.StartPoint);
							double trimBorderCurve2Start = iCurve2.PositionOf(arc.EndPoint);
							if ((trimBorderCurve1End > 0.0) && (trimBorderCurve2Start < 1.0))
							{
								iCurve1.Trim(0.0, trimBorderCurve1End);
								iCurve2.Trim(trimBorderCurve2Start, 1.0);
							}
							else doIt = false;
							//iCurve1.Trim(0.0,iCurve1.PositionOf(arc.StartPoint));
							//iCurve2.Trim(iCurve2.PositionOf(arc.EndPoint),1.0);
							//if (iCurve1.PositionOf(objectPointSav) > 0.5)
							//    iCurve1.Trim(0.0,iCurve1.PositionOf(arc.StartPoint));
							//else
							//{
							//    iCurve1.Trim(iCurve1.PositionOf(arc.StartPoint),1.0);
							//    if (Frame.GetBooleanSetting("Construct.MakePath",true)) iCurve1.Reverse();
							//}
							//if (iCurve2.PositionOf(objectPointSav) > 0.5)
							//{
							//    iCurve2.Trim(0.0,iCurve2.PositionOf(arc.EndPoint));
							//    if (Frame.GetBooleanSetting("Construct.MakePath",true)) iCurve2.Reverse();
							//}
							//else iCurve2.Trim(iCurve2.PositionOf(arc.EndPoint),1.0);
						}
					}
					if (doIt)
					{
						(arc as IGeoObject).CopyAttributes(iCurve1 as IGeoObject);

						if (Frame.GetBooleanSetting("Construct.MakePath", true) || composedSingle2Objects)
						{   // das Ergebnis soll in einem Pfad zusammengefasst werden!
							Path tmpPath = Path.Construct();
							tmpPath.Add(iCurve1);
							if (roundRad > 0.0)
								tmpPath.Add(arc);
							//                            if (!composedSingle) 
							tmpPath.Add(iCurve2);
							if (composedSingle2Objects)
							{
								tmpPath.Add(iCurveComposedSingle2Objects);

							}
							(tmpPath as IGeoObject).CopyAttributes(iCurve1 as IGeoObject);
							tmpPath.Flatten(); // bringt alles auf die "geometrische" Ebene, Unterpfade werden aufgelöst
							owner.Add(tmpPath as IGeoObject);
						}
						else
						{
							if (roundRad > 0.0)
								owner.Add(arc as IGeoObject);
							if (composedSplit)
							{
								// da neu erzeugt von SplitAtMousePosition, hier explizit einfügen
								owner.Add(iCurve1 as IGeoObject);
								owner.Add(iCurve2 as IGeoObject);
							}
							//if (composedSingle)
							//{
							//    // da neu erzeugt, hier explizit einfügen
							//    owner.Add(iCurve1 as IGeoObject);
							//}
						}
					}

				}
				base.ActiveObject = null;
			}
			base.OnDone();
		}

	}


	//    public override void OnDone()
	//    {
	//        if (base.ActiveObject != null)
	//        {
	//            using (base.Frame.Project.Undo.UndoFrame)
	//            {
	//                // alten Pfad entfernen
	//                if (composedSplit) (iCurveComposedSplit as IGeoObject).Owner.Remove(iCurveComposedSplit as IGeoObject);

	//                if (iCurve1.PositionOf(arc.StartPoint) > iCurve1.PositionOf(arc.Center))
	//                { iCurve1.Reverse(); } // evtl. umdrehen, damit das Trimmen unten klappt
	//                if (iCurve2.PositionOf(arc.EndPoint) < iCurve2.PositionOf(arc.Center))
	//                { iCurve2.Reverse(); } // evtl. umdrehen, damit das Trimmen unten klappt
	//                iCurve1.Trim(0.0, iCurve1.PositionOf(arc.StartPoint));
	//                iCurve2.Trim(iCurve2.PositionOf(arc.EndPoint), 1.0);


	//                //if (iCurve1.PositionOf(objectPoint) > 0.5)
	//                //    iCurve1.Trim(0.0,iCurve1.PositionOf(arc.StartPoint));
	//                //else 
	//                //{
	//                //    iCurve1.Trim(iCurve1.PositionOf(arc.StartPoint),1.0);
	//                //    if (Frame.GetBooleanSetting("Construct.MakePath",true)) iCurve1.Reverse();
	//                //}
	//                //if (iCurve2.PositionOf(objectPoint) > 0.5)
	//                //{
	//                //    iCurve2.Trim(0.0,iCurve2.PositionOf(arc.EndPoint));
	//                //    if (Frame.GetBooleanSetting("Construct.MakePath",true)) iCurve2.Reverse();
	//                //}
	//                //else iCurve2.Trim(iCurve2.PositionOf(arc.EndPoint),1.0);
	//                (arc as IGeoObject).CopyAttributes(iCurve1 as IGeoObject);
	//                if (Frame.GetBooleanSetting("Construct.MakePath",true))
	//                {	// das Ergebnis soll in einem Pfad zusammengefasst werden!
	//                    Path tmpPath =  Path.Construct();
	//                    tmpPath.Add(iCurve1);
	//                    tmpPath.Add(arc);
	//                    tmpPath.Add(iCurve2);
	//                    (tmpPath as IGeoObject).CopyAttributes(iCurve1 as IGeoObject);
	//                    tmpPath.Flatten(); // bringt alles auf die "geometrische" Ebene, Unterpfade werden aufglöst
	//                    owner.Add(tmpPath as IGeoObject);
	//                }
	//                else 
	//                {
	//                    owner.Add(arc as IGeoObject); 
	//                    if (composedSplit)
	//                    {
	//                        // da neu erzeugt von SplitAtMousePosition, hier explizit einfügen
	//                        owner.Add(iCurve1 as IGeoObject); 
	//                        owner.Add(iCurve2 as IGeoObject); 
	//                    }
	//                }
	//            }
	//            base.ActiveObject = null;
	//        }
	//        base.OnDone();
	//    }

	//}
}


