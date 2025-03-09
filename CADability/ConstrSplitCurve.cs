using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Collections.Generic;

namespace CADability.Actions
{
	internal class ConstrSplitCurve : ConstructAction
	{
		private readonly bool autorepeat; // zeigt an, ob autorepeat ausgeführt werden soll: nur wenn nicht aus dem Kontextmenue
		private ICurve theCurve; // die zu zerschneidende Kurve
		private static int number = 2; // Anzahl der Stücke, statisch, da von Aktion zu Aktion beibehalten
		private int mode; // prozentual (0) | Längen (1)
		private readonly List<double> sections; // die einzelnen Abschnitte
		private InputContainer distances; // die Anzeigen der einzelnen Abschnitte

		public ConstrSplitCurve()
		{   // für neue Aktionen
			theCurve = null;
			autorepeat = true;
			sections = new List<double>();
		}
		public ConstrSplitCurve(ICurve toSplit)
		{   // aus dem Kontextmenue
			theCurve = toSplit;
			autorepeat = false;
			sections = new List<double>();
		}
		public override string GetID()
		{
			return "Constr.SplitCurve";
		}
		public override void OnSetAction()
		{
			// The title appears in the control center
			base.TitleId = "Constr.SplitCurve";

			CurveInput curveInput = null;
			if (theCurve == null)
			{
				curveInput = new CurveInput("Constr.SplitCurve.Curve");
				curveInput.MouseOverCurvesEvent += OnMouseOverCurves;
			}

			IntInput numberInput = new IntInput("Constr.SplitCurve.Count");
			numberInput.SetIntEvent += OnSetNumber;
			numberInput.SetMinMax(2, 100, false);
			numberInput.GetIntEvent += OnGetNumber;

			MultipleChoiceInput modeInput = new MultipleChoiceInput("Constr.SplitCurve.Mode", "Constr.SplitCurve.Mode.Values");
			// |prozentual|Längen
			modeInput.SetChoiceEvent += OnSetMode;

			distances = new InputContainer("Constr.SplitCurve.Distances");
			RefreshDistances();

			if (theCurve == null)
			{
				base.SetInput(curveInput, numberInput, modeInput, distances);
			}
			else
			{
				base.SetInput(numberInput, modeInput, distances);
			}
			base.OnSetAction();
			Recalc();
		}

		private void RefreshDistances()
		{   // wird aufgerufen, wenn sich die Anzahl geändert hat
			// alle Längen werden gleich vorbesetzt.
			if (sections.Count < number)
			{   // nur verlängern, nicht kürzen
				for (int i = sections.Count; i < number; ++i)
				{
					sections.Add(0.0);
				}
			}
			for (int i = 0; i < sections.Count; ++i)
			{
				sections[i] = 1.0 / (double)number;
			}
			// die Untereinträge mit den Längen werden erzeugt
			IPropertyEntry[] doubleProperties = new IPropertyEntry[number];
			for (int i = 0; i < number; ++i)
			{
				int currentIndex = i;
				DoubleProperty doubleProperty = new DoubleProperty(Frame, "Constr.SplitCurve.Distance");
				doubleProperty.LabelText = StringTable.GetFormattedString("Constr.SplitCurve.Distance" + ".Label", i + 1);
				doubleProperty.OnGetValue = () =>
				{
					double factor = 1.0;
					switch (mode)
					{
						case 0:
							factor = 100;
							break;
						case 1:
							factor = theCurve.Length;
							break;
					}

					return factor * sections[currentIndex];
				};
				doubleProperty.OnSetValue = l => OnSetDistance(currentIndex, l);
				doubleProperties[i] = doubleProperty;
			}
			distances.SetShowProperties(doubleProperties);
			for (int i = 0; i < number; ++i)
			{
				DoubleProperty doubleProperty = doubleProperties[i] as DoubleProperty;
				doubleProperty.Refresh();
			}
		}

		private void OnDistanceKey(IPropertyEntry sender, char keyPressed)
		{   // das Enter auf einem Abstand beendet immer die Aktion, sonst muss man halt mit TAB durch
			// man könnte überprüfen obs der letzte ist, macht aber m.E. keinen Sinn
			if (keyPressed == 13)
			{
				base.Finish();
			}
		}

		private void OnSetDistance(int index, double l)
		{   // benutzereingabe für eine bestimmte Länge
			double factor = 1.0;
			switch (mode)
			{
				case 0: // prozent, 0.x*100
					factor = 100;
					break;
				case 1:
					factor = theCurve.Length;
					break;
			}
			sections[index] = Math.Min(1.0, l / factor); // höchstens 1.0
														 // im Folgenden werden alle anderen Werte so geändert, dass die Summe 1.0 ergibt.
														 // Es werden erst die folgenden Werte verändert, wenn das nicht reicht die vorhergehenden
			double sum = 0.0;
			for (int i = 0; i < number; ++i)
			{
				sum += sections[i];
			}
			if (sum > 1.0)
			{   // zu groß, die folgenden kleiner machen
				double toReduce = sum - 1.0;
				for (int i = index + 1; i < number; ++i)
				{
					if (sections[i] > toReduce)
					{
						sections[i] -= toReduce;
						toReduce = 0.0;
						break;
					}
					else
					{
						toReduce -= sections[i];
						sections[i] = 0.0;
					}
				}
				if (toReduce > 0.0)
				{   // immer noch zu groß, vorne anfangen
					for (int i = 0; i < index; ++i)
					{
						if (sections[i] > toReduce)
						{
							sections[i] -= toReduce;
							toReduce = 0.0;
							break;
						}
						else
						{
							toReduce -= sections[i];
							sections[i] = 0.0;
						}
					}
				}
			}
			else if (sum < 1.0)
			{   // zu klein, einfach den folgenden Wert entsprechend vergrößern
				double toAdd = 1.0 - sum;
				int nextIndex = index + 1;
				if (nextIndex >= number) nextIndex = 0;
				sections[nextIndex] += toAdd;
			}
			// alle Werte bis auf diesen updaten.
			IPropertyEntry[] sub = distances.GetShowProperties();
			for (int i = 0; i < sub.Length; ++i)
			{
				if (i != index) (sub[i] as DoubleProperty).Refresh();
			}
			Recalc();
		}

		private int OnGetNumber()
		{
			return number;
		}

		private void OnSetMode(int val)
		{   // prozent (0) oder Länge (1)
			mode = val;
			// Refresh für die Einträge
			IPropertyEntry[] sub = distances.GetShowProperties();
			for (int i = 0; i < sub.Length; ++i)
			{
				(sub[i] as DoubleProperty).Refresh();
			}
		}

		private void OnSetNumber(int val)
		{   // Anzahl der Abschnitte
			number = val;
			RefreshDistances();
			Recalc();
		}

		private bool OnMouseOverCurves(ConstructAction.CurveInput sender, ICurve[] TheCurves, bool up)
		{   // kommt z.Z. nicht vor, da die Aktion nur aus dem Kontextmenue gestrtet wird
			if (up && TheCurves.Length > 0)
			{
				theCurve = TheCurves[0];
				Recalc();
			}
			return TheCurves.Length > 0;
		}
		private void Recalc()
		{   // jeder zweite Abschnitt wird als selektiertes Feedbackobjekt dargestellt
			base.FeedBack.ClearAll();
			ICurve toSplit = theCurve;
			double totlen = theCurve.Length;
			double sumlen = 0.0;
			for (int i = 0; i < number; ++i)
			{
				ICurve splitted = toSplit.Clone();
				double start = theCurve.PositionAtLength(sumlen);
				sumlen += sections[i] * totlen;
				double end = theCurve.PositionAtLength(sumlen);
				splitted.Trim(start, end);

				if ((i & 1) == 0)
				{
					IGeoObject toAdd = splitted as IGeoObject;
					base.FeedBack.AddSelected(toAdd);
				}
			}

			// alter Text, ungleich aufgeteilt
			//if ((number & 1) != 0)
			//{
			//    base.FeedBack.AddSelected(toSplit as IGeoObject);
			//}

			//for (int i = 0; i < number; ++i)
			//{
			//    double sum = 0.0;
			//    for (int j = i; j < number; ++j)
			//    {
			//        sum += sections[j];
			//    }
			//    Positions[i] = sections[i] / sum;
			//}
			//for (int i = 0; i < number - 1; ++i)
			//{
			//    ICurve[] splitted = toSplit.Split(Positions[i]);
			//    if (splitted.Length == 2 )
			//    {
			//        if ((i & 1) == 0)
			//        {
			//            IGeoObject toAdd = splitted[0] as IGeoObject;
			//            base.FeedBack.AddSelected(toAdd);
			//        }
			//        toSplit = splitted[1];
			//    }
			//}
			//if ((number & 1) != 0)
			//{
			//    base.FeedBack.AddSelected(toSplit as IGeoObject);
			//}
		}
		public override void OnDone()
		{   // zerstückeln, original entfernen und Stücke einfügen
			// TODO: noch auf 0.0 aufpassen!
			// TODO: Polyline geht nicht richtig, Ellipse auch nicht, da nicht linear auf dem Umfang
			if (theCurve is IGeoObject go)
			{
				IGeoObjectOwner owner = go.Owner;
				if (owner != null)
				{
					using (Frame.Project.Undo.UndoFrame)
					{
						//if (go is Polyline)
						//{
						//    (go as Polyline).IsClosed = false; // aufbrechen
						//}
						// nicht aufbrechen, das ist jetzt in Split ordentlich geregelt
						owner.Remove(go);
						ICurve toSplit = theCurve;
						double totlen = theCurve.Length;
						double sumlen = 0.0;
						for (int i = 0; i < number; ++i)
						{
							ICurve splitted = toSplit.Clone();
							double start = theCurve.PositionAtLength(sumlen);
							sumlen += sections[i] * totlen;
							double end = theCurve.PositionAtLength(sumlen);
							splitted.Trim(start, end);
							(splitted as IGeoObject).CopyAttributes(go);
							owner.Add(splitted as IGeoObject);
						}

						//double[] Positions = new double[number];
						//for (int i = 0; i < number; ++i)
						//{
						//    double sum = 0.0;
						//    for (int j = i; j < number; ++j)
						//    {
						//        sum += sections[j];
						//    }
						//    Positions[i] = sections[i] / sum;
						//}
						//for (int i = 0; i < number-1; ++i)
						//{
						//    ICurve[] splitted = toSplit.Split(Positions[i]);
						//    if (splitted.Length == 2)
						//    {
						//        IGeoObject toAdd = splitted[0] as IGeoObject;
						//        toAdd.CopyAttributes(go);
						//        owner.Add(toAdd);
						//        toSplit = splitted[1];
						//    }
						//}
						//(toSplit as IGeoObject).CopyAttributes(go);
						//owner.Add(toSplit as IGeoObject);
					}
				}
			}
			base.OnDone();
		}
		public override bool AutoRepeat()
		{   // nur Autorepeat, wenn nicht aus dem Kontextmenue
			return autorepeat;
		}
		public override void OnActivate(Action oldActiveAction, bool settingAction)
		{   // das Öffnen der Abstände geht erst nachdem Activate dran war, dann wurde nämlich der Treeview zusammengesetzt
			base.OnActivate(oldActiveAction, settingAction);
			distances.Open(true); // erst hier kann man öffnen
		}
	}
}
