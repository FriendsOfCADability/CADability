using CADability.Curve2D;
using CADability.GeoObject;

namespace CADability
{
	static class GeoObjectsSave
	{
		/// <summary>
		/// Save the GeoObjects to a file. ONLY in DEBUG.
		/// Attention. If the GeoObjects belogns to a Model it will be removed!
		/// </summary>
		/// <param name="fileName">File name with extension .cdb</param>
		/// <param name="obj"></param>
		public static void SaveToFileDBG(string fileName, params IGeoObject[] obj)
		{
#if DEBUG
			SaveToFileDBG(fileName, new GeoObjectList(obj));
#endif
		}

		/// <summary>
		/// Save the border curves to a file. ONLY in DEBUG.
		/// </summary>
		/// <param name="fileName">File name with extension .cdb</param>
		/// <param name="border"></param>
		/// <param name="plane">Plane in which the GeoObjects are made from the 2D border curves</param>
		public static void SaveToFileDBG(string fileName, CADability.Shapes.Border border, CADability.Plane plane)
		{
#if DEBUG
			if (border != null && border.Count > 0)
			{
				GeoObjectList tmp = new GeoObjectList();
				for (int i = 0; i < border.Count; i++)
					tmp.Add(border[i].MakeGeoObject(plane));
				SaveToFileDBG(fileName, tmp);
			}
#endif
		}

		public static void SaveToFileDBG(string fileName, CADability.Shapes.SimpleShape shape, CADability.Plane plane)
		{
#if DEBUG
			if (shape != null)
			{
				SaveToFileDBG(fileName, ToGeoObjectList(shape, plane));
			}
#endif
		}

		private static GeoObjectList ToGeoObjectList(CADability.Shapes.SimpleShape shape, CADability.Plane plane, bool saveHoles = true)
		{
			GeoObjectList tmp = new GeoObjectList();

			for (int i = 0; i < shape.Outline.Count; i++)
			{
				tmp.Add(shape.Outline[i].MakeGeoObject(plane));
			}
			if (saveHoles)
			{
				for (int i = 0; i < shape.NumHoles; i++)
				{
					CADability.Shapes.Border hole = shape.Hole(i);
					for (int j = 0; j < hole.Count; j++)
					{
						tmp.Add(hole[j].MakeGeoObject(plane));
					}
				}
			}

			return tmp;
		}

		public static void SaveToFileDBG(string fileName, CADability.Shapes.CompoundShape compShape, CADability.Plane plane, bool saveHoles)
		{
#if DEBUG
			if (compShape != null)
			{
				GeoObjectList tmp = new GeoObjectList();
				foreach(CADability.Shapes.SimpleShape shape in compShape.SimpleShapes)
				{
					tmp.AddRange(ToGeoObjectList(shape, plane, saveHoles));
				}
				SaveToFileDBG(fileName, tmp);
			}
#endif
		}

		/// <summary>
		/// Save the 2D curve to a file. ONLY in DEBUG.
		/// </summary>
		/// <param name="fileName">File name with extension .cdb</param>
		/// <param name="curves2D"></param>
		/// <param name="plane">Plane in which the GeoObjects are made from the 2D curves</param>
		public static void SaveToFileDBG(string fileName, ICurve2D[] curves2D, CADability.Plane plane)
		{
#if DEBUG
			if (curves2D != null && curves2D.Length > 0)
			{
				GeoObjectList tmp = new GeoObjectList();
				foreach (ICurve2D curve in curves2D)
				{
					if (curve != null)
						tmp.Add(curve.MakeGeoObject(plane));
				}
				SaveToFileDBG(fileName, tmp);
			}
#endif
		}

		public static void SaveToFileDBG(string fileName, GeoPoint2D[] points, CADability.Plane plane)
		{
#if DEBUG
			if (points != null && points.Length > 0)
			{
				GeoObjectList tmp = new GeoObjectList();
				foreach (GeoPoint2D pt2D in points)
				{
					tmp.Add(ToPoint(plane.ToGlobal(pt2D)));
				}
				SaveToFileDBG(fileName, tmp);
			}
#endif
		}

		public static void SaveToFileDBG(string fileName, GeoPoint[] points)
		{
#if DEBUG
			if (points != null && points.Length > 0)
			{
				GeoObjectList tmp = new GeoObjectList();
				foreach (GeoPoint pt in points)
				{
					tmp.Add(ToPoint(pt));
				}
				SaveToFileDBG(fileName, tmp);
			}
#endif
		}

		private static Point ToPoint(GeoPoint pt)
		{
			Point drwPt = Point.Construct();
			drwPt.Location = pt;
			drwPt.Symbol = PointSymbol.Plus;

			return drwPt;
		}

		/// <summary>
		/// Save the GeoObjects to a file. ONLY in DEBUG.
		/// Attention. If the GeoObjects belogns to a Model it will be removed!
		/// </summary>
		/// <param name="fileName">File name with extension .cdb</param>
		/// <param name="list"></param>
		public static void SaveToFileDBG(string fileName, GeoObjectList list)
		{
#if DEBUG
			SaveToFile(fileName, list);
#endif
		}

		public static bool SaveToFile(string fileName, GeoObjectList list)
		{
			bool ok = false;

			Project prj = Project.CreateSimpleProject();
			Model m = prj.GetActiveModel();
			foreach (IGeoObject geoObj in list)
			{
				IGeoObject geoObjC = geoObj.Clone(); //Per non modificare oggetto orignale, se no non sarebbe piu selezionabile nel disegno originale.
				geoObjC.UpdateAttributes(prj); //Per avere progetto valido con attributi degli oggetti. Se no selezione non funziona.
				m.Add(geoObjC);
			}
			//m.Add(list);
			string dir = System.IO.Path.GetDirectoryName(fileName);
			if (!System.IO.Directory.Exists(dir))
				System.IO.Directory.CreateDirectory(dir);
			ok = prj.WriteToFile(fileName);

			return ok;
		}
	}
}
