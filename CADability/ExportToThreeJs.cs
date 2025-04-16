using CADability.Attribute;
using CADability.GeoObject;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace CADability
{
    public class ExportToThreeJsException : ApplicationException
    {
        public IGeoObject GeoObject;
        public ExportToThreeJsException(IGeoObject go)
        {
            GeoObject = go;
        }
    }

    public class ExportToThreeJs
    {
        [DataContract]
        struct JsonPoint
        {
            [DataMember]
            public double x;
            [DataMember]
            public double y;
            [DataMember]
            public double z;
            public JsonPoint(double x, double y, double z)
            {
                this.x = x;
                this.y = y;
                this.z = z;
            }
            public JsonPoint(GeoPoint p)
            {
                this.x = p.x;
                this.y = p.y;
                this.z = p.z;
            }
            public JsonPoint(GeoVector v)
            {
                this.x = v.x;
                this.y = v.y;
                this.z = v.z;
            }
        }
        [DataContract]
        struct JsonEntity
        {
            [DataMember]
            public List<JsonFace> faces;
            [DataMember]
            public List<JsonLine> lines;
            [DataMember]
            public List<JsonText> text;
            [DataMember]
            public Dictionary<string, object> userData;
        }
        [DataContract]
        struct JsonFace
        {
            [DataMember]
            public List<JsonPoint> vertices;
            [DataMember]
            public List<int> triangles;
            [DataMember]
            public uint color;
        }
        [DataContract]
        struct JsonLine
        {
            [DataMember]
            public List<JsonPoint> vertices;
            [DataMember]
            public uint color;
        }
        [DataContract]
        struct JsonText
        {
            [DataMember]
            public string text;
            [DataMember]
            public string font;
            [DataMember]
            public JsonPoint location;
            [DataMember]
            public JsonPoint directionx;
            [DataMember]
            public JsonPoint directiony;
            [DataMember]
            public uint color;
        }
        //[DataContract]
        //struct JsonDictionary
        //{
        //    [DataMember]
        //    public string Key;
        //    [DataMember]
        //    public object Value;
        //}
        [DataContract]
        struct JsonEntities
        {
            [DataMember]
            public List<JsonEntity> entities;
        }
        private JsonEntities jsonEntities;
        private double precision;
        private Model model;
        public bool ExportEdges;
        public bool ExportTextAsTriangles;
        public ExportToThreeJs(Model m, double precision)
        {
            jsonEntities = new JsonEntities
            {
                entities = new List<JsonEntity>()
            };
            this.precision = precision;
            ExportEdges = false;
            ExportTextAsTriangles = false;
            this.model = m;
            ExportUserData = true;
        }

        private JsonEntity makeJsonEntity(IGeoObject go)
        {
            try
            {
                JsonEntity res = new JsonEntity
                {
                    faces = new List<JsonFace>(),
                    lines = new List<JsonLine>(),
                    text = new List<JsonText>(),
                    userData = new Dictionary<string, object>()
                };

                if (go is Solid)
                {
                    foreach (Face fce in (go as Solid).Shells[0].Faces)
                    {
                        res.faces.Add(makeJsonFace(fce));
                    }
                    if (ExportEdges)
                    {
                        foreach (Edge edg in (go as Solid).Shells[0].Edges)
                        {
                            if (edg.Curve3D != null)
                            {
                                res.lines.Add(makeJsonLine(edg.Curve3D));
                            }
                        }
                    }
                }
                else if (go is Shell)
                {
                    foreach (Face fce in (go as Shell).Faces)
                    {
                        res.faces.Add(makeJsonFace(fce));
                    }
                    if (ExportEdges)
                    {
                        foreach (Edge edg in (go as Shell).Edges)
                        {
                            if (edg.Curve3D != null)
                            {
                                res.lines.Add(makeJsonLine(edg.Curve3D));
                            }
                        }
                    }
                }
                else if (go is Face)
                {
                    res.faces.Add(makeJsonFace(go as Face));
                    if (ExportEdges)
                    {
                        foreach (Edge edg in (go as Face).AllEdges)
                        {
                            if (edg.Curve3D != null)
                            {
                                res.lines.Add(makeJsonLine(edg.Curve3D));
                            }
                        }
                    }
                }
                else if (go is Block)
                {
                    for (int i = 0; i < go.NumChildren; i++)
                    {
                        JsonEntity sub = makeJsonEntity(go.Child(i));
                        res.faces.AddRange(sub.faces);
                        res.lines.AddRange(sub.lines);
                        res.text.AddRange(sub.text);
                    }
                }
                else if (go is ICurve)
                {
                    ICurve path = (go as ICurve).Approximate(true, precision);
                    if (path is Polyline)
                    {
                        JsonLine pl = new JsonLine();
                        pl.vertices = new List<JsonPoint>();
                        for (int i = 0; i < (path as Polyline).Vertices.Length; i++)
                        {
                            pl.vertices.Add(new JsonPoint((path as Polyline).Vertices[i]));
                        }
                        pl.color = getColor(go);
                        res.lines.Add(pl);
                    }
                    if (path is GeoObject.Path)
                    {
                        JsonLine pl = new JsonLine();
                        pl.vertices = new List<JsonPoint>();
                        for (int i = 0; i < (path as GeoObject.Path).CurveCount; i++)
                        {
                            pl.vertices.Add(new JsonPoint((path as GeoObject.Path).Curves[i].StartPoint));
                        }
                        pl.vertices.Add(new JsonPoint((path as GeoObject.Path).Curves.Last().EndPoint));
                        pl.color = getColor(go);
                        res.lines.Add(pl);
                    }
                }
                else if (go is Text)
                {
                    Text txt = (go as Text);
                    JsonText jst = new JsonText();
                    jst.location = new JsonPoint(txt.Location);
                    jst.directionx = new JsonPoint(txt.LineDirection);
                    jst.directiony = new JsonPoint(txt.GlyphDirection);
                    jst.font = txt.Font;
                    jst.text = txt.TextString;
                    jst.color = getColor(go);
                    res.text.Add(jst);
                }
                if (ExportUserData && go.UserData != null)
                {
                    res.userData = new Dictionary<string, object>();
                    foreach (KeyValuePair<string, object> kv in go.UserData)
                    {
                        if (kv.Value.GetType().IsPrimitive || kv.Value is string || kv.Value is Guid)
                        {
                            if (kv.Value == null)
                                res.userData[kv.Key] = "null";
                            else
                                res.userData[kv.Key] = kv.Value;
                        }
                        else
                        {
                            TypeAttributes ta = kv.Value.GetType().Attributes;
                        }
                    }
                    // DataContractResolver 
#if DEBUG
                    if (go.UserData.Count == 0)
                    {
                        res.userData["key1"] = "val1";
                        res.userData["key2"] = "val2";
                    }
#endif
                }
                return res;
            }
            catch (Exception ex)
            {
                if (ex is System.Threading.ThreadAbortException) throw (ex);
                throw new ExportToThreeJsException(go);
            }
        }

        private JsonLine makeJsonLine(ICurve curve3D)
        {
            ICurve path = curve3D.Approximate(true, precision);
            if (path is Polyline)
            {
                JsonLine pl = new JsonLine();
                pl.vertices = new List<JsonPoint>();
                for (int i = 0; i < (path as Polyline).Vertices.Length; i++)
                {
                    pl.vertices.Add(new JsonPoint((path as Polyline).Vertices[i]));
                }
                pl.color = 0; // all edges are black
                return pl;
            }
            else if (path is GeoObject.Path)
            {
                JsonLine pl = new JsonLine();
                pl.vertices = new List<JsonPoint>();
                for (int i = 0; i < (path as GeoObject.Path).CurveCount; i++)
                {
                    pl.vertices.Add(new JsonPoint((path as GeoObject.Path).Curves[i].StartPoint));
                }
                pl.vertices.Add(new JsonPoint((path as GeoObject.Path).Curves.Last().EndPoint));
                pl.color = 0;
                return pl;
            }
            else if (path is Line)
            {
                JsonLine pl = new JsonLine();
                pl.vertices = new List<JsonPoint>();
                pl.vertices.Add(new JsonPoint(curve3D.StartPoint));
                pl.vertices.Add(new JsonPoint(curve3D.EndPoint));
                pl.color = 0;
                return pl;
            }
            return new JsonLine
            {
                vertices = new List<JsonPoint>(),
                color = 0
            };
        }

        private uint getColor(IGeoObject go)
        {
            if (go is IColorDef && (go as IColorDef).ColorDef != null)
            {
                return (uint)((go as IColorDef).ColorDef.Color.ToArgb() & 0x00FFFFFF);
            }
            return 0;
        }

        private JsonFace makeJsonFace(Face fce)
        {
            JsonFace jf = new JsonFace
            {
                vertices = new List<JsonPoint>(),
                triangles = new List<int>(),
                color = 0,
            };
            GeoPoint[] trianglePoint;
            GeoPoint2D[] triangleUVPoint;
            int[] triangleIndex;
            BoundingCube triangleExtent;
            fce.GetTriangulation(precision, out trianglePoint, out triangleUVPoint, out triangleIndex, out triangleExtent);
            for (int i = 0; i < trianglePoint.Length; i++)
            {
                jf.vertices.Add(new JsonPoint(trianglePoint[i]));
            }
            for (int i = 0; i < triangleIndex.Length; i++)
            {
                jf.triangles.Add(triangleIndex[i]);
            }
            jf.color = getColor(fce);
            return jf;
        }

        public string ToJSON(bool escapeQuote = true)
        {
            foreach (IGeoObject go in model)
            {
                jsonEntities.entities.Add(makeJsonEntity(go));
            }
            using (MemoryStream stream1 = new MemoryStream())
            {
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(JsonEntities));
                // in Version .NET 4.5 gibt es DataContractJsonSerializerSettings.UseSimpleDictionaryFormat, das ist besser für UserData, ebenso EmitTypeInformation: Never
                ser.WriteObject(stream1, jsonEntities);
                stream1.Position = 0;
                using (StreamReader sr = new StreamReader(stream1))
                {
                    string res = sr.ReadToEnd();
                    if (escapeQuote) res = res.Replace("\\", "").Replace("\"", "\\\"");
                    return res;
                }
            }
        }

        public bool ExportUserData;
    }
}
