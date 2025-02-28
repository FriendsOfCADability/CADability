#if !WEBASSEMBLY
using CADability.Attribute;
using CADability.GeoObject;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace CADability
{
    public struct WebGLData
    {
        public GeoPoint[] vertices;
        public GeoVector[] normals;
        public int[] indices;
        public int color;
    }
    public interface IConvertForWebGL
    {
        IGeoObject Conversion { get; }
        WebGLData[] Data { get; }
    }

    /// <summary>
    /// Export a Model as a WebGl based html file.
    /// </summary>
    public class ExportToWebGl
    {
        [DataContract]
        class GenData : IComparer<Color>
        {
            public class intervals
            {
                class intv : IComparable
                {
                    public uint start;
                    public uint end;
                    public intv(uint start, uint end)
                    {
                        this.start = start;
                        this.end = end;
                    }
                    int IComparable.CompareTo(object obj)
                    {
                        intv other = obj as intv;
                        if (other != null)
                        {
                            return this.start.CompareTo(other.start);
                        }
                        return -1; // kommt nicht vor
                    }
                }
                List<intv> seg; // Start + Ende in einem, Indizes in das elementArray sind uint
                public intervals()
                {
                    seg = new List<intv>();
                }
                public void add(int start, int end)
                {
                    if (seg.Count > 0 && seg[seg.Count - 1].end == (uint)start)
                    {   // das alte Ende ist der neue Anfang
                        seg[seg.Count - 1].end = (uint)end; // kommt oft vor
                    }
                    else
                    {
                        seg.Add(new intv((uint)start, (uint)end));
                    }

                }
                public void combine()
                {
                    seg.Sort();
                    for (int i = seg.Count - 2; i >= 0; --i)
                    {
                        if (seg[i + 1].start <= seg[i].end)
                        {
                            seg[i].end = Math.Max(seg[i + 1].end, seg[i].end);
                            seg.RemoveAt(i + 1);
                        }
                    }
                }
                public uint[] toArray()
                {
                    uint[] res = new uint[seg.Count * 2];
                    for (int i = 0; i < seg.Count; i++)
                    {
                        res[2 * i] = seg[i].start;
                        res[2 * i + 1] = seg[i].end;
                    }
                    return res;
                }

                internal void unite(int start, int end)
                {
                    seg.Add(new intv((uint)start, (uint)end));
                    combine();
                }
                internal void unite(intervals other)
                {
                    for (int i = 0; i < other.seg.Count; i++)
                    {
                        seg.Add(new intv((uint)other.seg[i].start, (uint)other.seg[i].end));
                    }
                    combine();
                }

                internal intervals clone()
                {
                    intervals res = new intervals();
                    for (int i = 0; i < seg.Count; i++)
                    {
                        res.seg.Add(new intv(seg[i].start, seg[i].end));
                    }
                    return res;
                }
            }
            public class chunk : IComparable
            {
                public GeoPoint[] vertices;
                public GeoVector[] normals;
                public int[] indices;
                public enum vmode { triangles, lines, points, edges, text }; // 1: Dreiecke, 2: Linien, 3: Punkte, 4: Kanten (schwarz), Texte
                public vmode mode;
                internal string layer;
                internal int color;
                internal int memSegIndex; // in diesem MemorySegment
                internal int elementArrayStart; // Startindex dieses Eintrags im elementArray
                public string stringDesc; // nur bei Texten gesetzt: "Font|der Text"

                int IComparable.CompareTo(object obj)
                {
                    chunk other = obj as chunk;
                    if (other == null)
                    {
                        return -1;
                    }
                    // 1. nach Farbe (transparente kommen ans Ende)
                    uint dbg1 = (uint)this.color >> 24;
                    uint dbg2 = (uint)other.color >> 24;
                    if (dbg1 != 0xFF || dbg2 != 0xFF)
                    {
                    }
                    if (((uint)this.color >> 24) > ((uint)other.color >> 24)) return -1;
                    else if (((uint)this.color >> 24) < ((uint)other.color >> 24)) return 1;
                    else if ((this.color & 0x00FFFFFF) < (other.color & 0x00FFFFFF)) return -1;
                    else if ((this.color & 0x00FFFFFF) > (other.color & 0x00FFFFFF)) return 1;
                    else
                    {
                        // 2. nach Art
                        if (this.mode < other.mode) return -1;
                        else if (this.mode > other.mode) return 1;
                        else
                        {
                            // 3. nach layer
                            return this.layer.CompareTo(other.layer);
                        }
                    }
                }

                internal void updateIndizes(int basis)
                {
                    for (int i = 0; i < indices.Length; i++)
                    {
                        indices[i] += basis;
                    }
                }
            }
            [DataContract]
            public class objectData
            {
                [DataContract]
                struct jsondata
                {
                    [DataMember]
                    public string Key;
                    [DataMember]
                    public string Value;
                    [DataMember]
                    public objectData Subitems;
                }
                [DataMember]
                jsondata[] data
                {
                    get
                    {
                        jsondata[] res = new jsondata[dict.Count];
                        int i = 0;
                        foreach (KeyValuePair<string, object> item in dict)
                        {
                            jsondata jd = new jsondata();
                            jd.Key = item.Key;
                            if (item.Value is string)
                            {
                                jd.Value = item.Value as string;
                                jd.Subitems = null;

                            }
                            else if (item.Value is objectData)
                            {
                                jd.Value = null;
                                jd.Subitems = item.Value as objectData;
                            }
                            else if (item.Value is IDictionary<string, object>)
                            {
                                jd.Value = null;
                                jd.Subitems = new objectData(item.Value as IDictionary<string, object>);
                            }
                            else
                            {
                                jd.Value = null;
                                jd.Subitems = null;
                            }
                            res[i] = jd;
                            ++i;
                        }
                        return res;
                    }
                }
                Dictionary<string, object> dict; // zweiter Wert ist entweder ein Dictionary<string, object> (rekursiv) oder ein string
                public objectData()
                {
                    dict = new Dictionary<string, object>();
                }

                public objectData(IDictionary<string, object> iDictionary)
                {
                    this.dict = new Dictionary<string, object>(iDictionary);
                }
                public object this[string key]
                {
                    get
                    {
                        return dict[key];
                    }
                    set
                    {
                        dict[key] = value;
                    }
                }
                public int Count
                {
                    get
                    {
                        return dict.Count;
                    }
                }
            }
            public class glItem
            {
                public string name;
                public glItem[] subItems;
                public chunk chunk;
                public int color;
                public string layer;
                public objectData userData;
                public glItem(IGeoObject go, GenData gd)
                {
                    this.layer = findLayer(go);
                    this.color = Color.FromArgb(findTransparency(go), findColor(go)).ToArgb();
                    chunk = new chunk();
                    chunk.layer = layer;
                    chunk.color = this.color;
                    if (gd.userDataAsName != null && go.UserData != null)
                    {
                        object d = go.UserData.GetData(gd.userDataAsName);
                        if (d != null) name = d.ToString();
                    }
                    this.userData = findUserData(go.UserData, gd);
                }

                private objectData findUserData(IDictionary<string, object> userData, GenData gd)
                {
                    objectData res = null;
                    if (userData != null)
                    {
                        res = new objectData();
                        foreach (string key in userData.Keys)
                        {
                            if (gd != null && gd.userDataAsName == key) continue;
                            if (key == "OpenCascade.Location" || key == "OpenCascade.ImportedName") continue;
                            if (gd != null && gd.userDataIgnore && gd.userDataKeys != null)
                            {
                                if (gd.userDataKeys.Contains(key)) continue;
                            }
                            if (gd != null && !gd.userDataIgnore && gd.userDataKeys != null)
                            {
                                if (!gd.userDataKeys.Contains(key)) continue;
                            }
                            if (userData[key] is IDictionary<string, object>)
                            {
                                res[key] = findUserData(userData[key] as IDictionary<string, object>, null); // soll nur auf oberster Ebene ausgewählt werden
                            }
                            else
                            {
                                if (userData[key] != null) res[key] = userData[key].ToString();
                                else res[key] = null; // nur ein string
                            }
                        }
                    }
                    if (res.Count == 0) res = null; // war nur Name
                    return res;
                }
                private Color findColor(IGeoObject go)
                {
                    if (go is IColorDef && (go as IColorDef).ColorDef != null)
                    {
                        if ((go as IColorDef).ColorDef.Source != ColorDef.ColorSource.fromParent) return (go as IColorDef).ColorDef.Color;
                        if (go.Owner is IGeoObject) return findColor(go.Owner as IGeoObject);
                    }
                    return Color.Black;
                }
                private string findLayer(IGeoObject go)
                {
                    if (go.Layer != null) return go.Layer.Name;
                    if (go.Owner is IGeoObject) return findLayer(go.Owner as IGeoObject);
                    return "";
                }
                private int findTransparency(IGeoObject go)
                {
                    if (go.Layer != null) return 255 - go.Layer.Transparency;
                    if (go.Owner is IGeoObject) return findTransparency(go.Owner as IGeoObject);
                    return 255;
                }
            }
            List<chunk> allChunks;
            HashSet<int> allColors;
            HashSet<string> allLayers;
            private List<glItem> topItems;
            public double precision;
            public string userDataAsName; // dieser Schlüssel soll als Name verwendet werden
            [DataContract]
            public class MemSeg
            {   // WebGl kann leider den Vertex Buffer nur mit ushort indizieren, d.h. man ist auf maximal 65635 Punkte beschränkt
                // Deshalb wird das ganze Szenario aufgeteilt in einen oder mehrere solche Abschnitte
                // der Balauf in WebGl soll so sein: 
                // MemSeg auswählen, d.h. BindBuffer mit ARRAY_BUFFER und ELEMENT_ARRAY_BUFFER
                // {
                //      Farbe setzen
                //      Alle Dreiecke, Linien (und Punkte?) mit dieser Farbe darstellen
                //      edges mit Schwarz darstellen
                // }
                private static string floatbase85(float f)
                {
                    byte[] b = BitConverter.GetBytes(f);
                    uint ui = BitConverter.ToUInt32(b, 0);
                    char[] chars = new char[5];
                    for (int i = 0; i < 5; i++)
                    {
                        int c = (int)(ui % 85) + 35;
                        if (c >= 60) ++c; // "<" eliminieren
                        if (c >= 92) ++c; // "\" eliminieren
                        chars[4 - i] = Convert.ToChar(c);
                        ui = ui / 85;
                    }
                    return new string(chars);
                }
                [DataMember]
                public string vtx
                {
                    get
                    {
                        StringBuilder sb = new StringBuilder();
                        {
                            for (int i = 0; i < vertexArray.Count; i++)
                            {
                                sb.Append(floatbase85((float)vertexArray[i].x));
                                sb.Append(floatbase85((float)vertexArray[i].y));
                                sb.Append(floatbase85((float)vertexArray[i].z));
                            }
                        }
                        return sb.ToString();
                    }
                    set { } // wird benötig wg. DataMember
                }
                [DataMember]
                public string nrm
                {
                    get
                    {
                        StringBuilder sb = new StringBuilder();
                        {
                            for (int i = 0; i < normalArray.Count; i++)
                            {
                                sb.Append(floatbase85((float)normalArray[i].x));
                                sb.Append(floatbase85((float)normalArray[i].y));
                                sb.Append(floatbase85((float)normalArray[i].z));
                            }
                        }
                        return sb.ToString();
                    }
                    set { } // wird benötig wg. DataMember
                }
                /// <summary>
                /// Liste von Indizes in das VertexArray vtx.
                /// Wie diese Indizes zu verwenden sind, also tripel für Dreiecke, Paare für Linien u.s.w.
                /// entscheiden die Intervalle tri, lin, pnt, edg
                /// </summary>
                [DataMember]
                public int[] elt
                {
                    get
                    {
                        return elementArray.ToArray();
                    }
                }
                [DataMember]
                public uint[] tri
                {
                    get
                    {
                        return triangles.toArray();
                    }
                }
                [DataMember]
                public uint[] lin
                {
                    get
                    {
                        return lines.toArray();
                    }
                }
                [DataMember]
                public uint[] pnt
                {
                    get
                    {
                        return points.toArray();
                    }
                }
                [DataMember]
                public uint[] txt
                {
                    get
                    {
                        return texts.toArray();
                    }
                }
                [DataMember]
                public Dictionary<int, string> str
                {
                    get
                    {
                        return strings;
                    }
                }
                [DataMember]
                public uint[] edg
                {
                    get
                    {
                        return edges.toArray();
                    }
                }
                [DataMember]
                public Dictionary<string, uint[]> cls
                {
                    get
                    {
                        Dictionary<string, uint[]> res = new Dictionary<string, uint[]>();
                        foreach (KeyValuePair<int, intervals> item in colorIntervals)
                        {
                            res["_" + ((uint)(item.Key)).ToString("X8")] = item.Value.toArray(); // wichtig, dass es 8 Zeichen sind, wg. decodieren
                        }
                        return res;
                    }
                }
                [DataMember]
                public Dictionary<string, uint[]> lys
                {
                    get
                    {
                        Dictionary<string, uint[]> res = new Dictionary<string, uint[]>();
                        foreach (KeyValuePair<string, intervals> item in layerIntervals)
                        {
                            res["_" + item.Key] = item.Value.toArray();
                        }
                        return res;
                    }
                }

                private List<GeoPoint> vertexArray; // die Punktliste
                private List<GeoVector> normalArray; // die zugehörigen Normalenvektoren
                private List<int> elementArray;  // Indizes in die Punktliste für Dreiecke, Linien oder Punkte
                private intervals triangles; // mehrere Intervalle für elementsArray, jedes Intervall hat eine Farbe
                private intervals edges; // werden immer schwarz gezeichnet
                private intervals lines; // mit Farbe, sonst wie edges
                private intervals points; // Punkte, auch mit Farbe
                private intervals texts; // Punkte, auch mit Farbe
                private SortedDictionary<int, intervals> colorIntervals;
                private SortedDictionary<string, intervals> layerIntervals;
                private Dictionary<int, string> strings;

                public MemSeg(List<GeoPoint> vertexArray, List<GeoVector> normalArray, List<int> elementArray, SortedDictionary<int, intervals> colorIntervals, SortedDictionary<string, intervals> layerIntervals, intervals triangles, intervals edges, intervals lines, intervals points, intervals texts, Dictionary<int, string> strings)
                {
                    this.vertexArray = vertexArray;
                    this.normalArray = normalArray;
                    this.elementArray = elementArray;
                    this.colorIntervals = colorIntervals;
                    this.layerIntervals = layerIntervals;
                    triangles.combine();
                    edges.combine();
                    lines.combine();
                    points.combine();
                    this.triangles = triangles;
                    this.edges = edges;
                    this.lines = lines;
                    this.points = points;
                    this.texts = texts;
                    this.strings = strings;
                }
            }
            [DataMember]
            public List<MemSeg> msg;
            [DataMember]
            public BoundingCube ext;
            [DataMember]
            public GeoVector ipd; // initialProjectionDirection
            [DataMember]
            public ObjectHierarchy[] obh;
            private Model model;
            public string[] userDataKeys; // Liste von UserData keys, die ignoriert oder verwendet werden
            public bool userDataIgnore; // true: die userDataKeys werden ignoriert, false: nur die userDataKeys werden verwendet
            [DataContract]
            public class ObjectHierarchy
            {
                internal static int memSegCount;
                [DataMember]
                public string nam; // der Name, die 3 Buchstaben abkürzungen sollen das JSON vereinheitlichen
                [DataMember]
                public ObjectHierarchy[] sub;
                [DataMember]
                public uint[][] itv
                {
                    get
                    {
                        uint[][] res = new uint[parts.Length][];
                        for (int i = 0; i < res.Length; i++)
                        {
                            res[i] = parts[i].toArray();
                        }
                        return res;
                    }
                }
                [DataMember]
                public objectData dat;
                private intervals[] parts; // für jedes memseg eines
                public ObjectHierarchy(glItem glItem, intervals[] resintv)
                {
                    nam = glItem.name;
                    dat = glItem.userData;
                    parts = new intervals[memSegCount]; // fange mit einem leeren Satz Intervalle an (pro Speichersegment)
                    for (int i = 0; i < parts.Length; i++)
                    {
                        parts[i] = new intervals();
                    }
                    for (int i = 0; i < memSegCount; i++)
                    {
                        parts[i] = new intervals();
                    }
                    if (glItem.subItems != null && glItem.subItems.Length > 0)
                    {
                        sub = new ObjectHierarchy[glItem.subItems.Length];
                        for (int i = 0; i < glItem.subItems.Length; i++)
                        {
                            sub[i] = new ObjectHierarchy(glItem.subItems[i], parts); // akkumuliert auf dem Ergebnis alle Intervaller der subItems
                        }
                    }
                    if (glItem.chunk != null && glItem.chunk.indices != null)
                    {
                        parts[glItem.chunk.memSegIndex].unite(glItem.chunk.elementArrayStart, glItem.chunk.elementArrayStart + glItem.chunk.indices.Length);
                    }
                    //if (glItem.echunk != null && glItem.echunk.indices != null)
                    //{
                    //    parts[glItem.echunk.memSegIndex].unite(glItem.echunk.elementArrayStart, glItem.echunk.elementArrayStart + glItem.echunk.indices.Length);
                    //}
                    for (int i = 0; i < parts.Length; i++)
                    {
                        resintv[i].unite(parts[i]);
                    }
                }
            }

            public GenData(Model m)
            {
                this.model = m;
                this.ext = m.Extent;
                precision = m.Extent.Size * 1e-3;
                userDataAsName = "name";
                userDataIgnore = true; // damit wird nichts ignoriert
                userDataKeys = null;
            }

            public string ToJSON(bool escapeQuote = true)
            {
                MemoryStream stream1 = new MemoryStream();
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(GenData));
                ser.WriteObject(stream1, this);
                stream1.Position = 0;
                StreamReader sr = new StreamReader(stream1);
                string res = sr.ReadToEnd();
                stream1.Dispose();
                sr.Dispose();
                // string res = JsonConvert.SerializeObject(this, Formatting.None);
                if (escapeQuote) res = res.Replace("\\", "").Replace("\"", "\\\"");
                return res;
            }

            private class ColorComparer : IComparer<int>
            {
                public int Compare(int clr1, int clr2)
                {
                    if (((uint)clr1 >> 24) > ((uint)clr2 >> 24)) return -1;
                    else if (((uint)clr1 >> 24) < ((uint)clr2 >> 24)) return 1;
                    else if ((clr1 & 0x00FFFFFF) < (clr2 & 0x00FFFFFF)) return -1;
                    else if ((clr1 & 0x00FFFFFF) > (clr2 & 0x00FFFFFF)) return 1;
                    return 0;
                }
            }
            private void createVertexArrays()
            {
                allChunks.Sort(); // nach Farbe, Art und Layer sortiert
                int lastclr = 0;
                for (int i = 0; i < allChunks.Count; i++)
                {
                    int clr = allChunks[i].color;
                    if (clr != lastclr)
                    {
                        System.Diagnostics.Trace.WriteLine("Farbe: " + clr.ToString("X8"));
                        lastclr = clr;
                    }
                }

                SortedDictionary<int, intervals> colorIntervals = new SortedDictionary<int, intervals>(new ColorComparer());
                SortedDictionary<string, intervals> layerIntervals = new SortedDictionary<string, intervals>();
                List<GeoPoint> vertexArray = new List<GeoPoint>();
                List<GeoVector> normalArray = new List<GeoVector>();
                List<int> elementArray = new List<int>();
                intervals triangles = new intervals();
                intervals edges = new intervals();
                intervals lines = new intervals();
                intervals points = new intervals();
                intervals texts = new intervals();
                Dictionary<int, string> strings = new Dictionary<int, string>();

                for (int i = 0; i < allChunks.Count; i++)
                {
                    allChunks[i].updateIndizes(vertexArray.Count);
                    allChunks[i].memSegIndex = msg.Count;
                    vertexArray.AddRange(allChunks[i].vertices);
                    if (allChunks[i].normals != null) normalArray.AddRange(allChunks[i].normals);
                    else normalArray.AddRange(new GeoVector[allChunks[i].vertices.Length]); // leeres Array
                    int elementStart = elementArray.Count;
                    allChunks[i].elementArrayStart = elementStart;
                    elementArray.AddRange(allChunks[i].indices);
                    int elementEnd = elementArray.Count;

                    switch (allChunks[i].mode)
                    {
                        case chunk.vmode.triangles:
                            triangles.add(elementStart, elementEnd);
                            break;
                        case chunk.vmode.lines:
                            lines.add(elementStart, elementEnd);
                            break;
                        case chunk.vmode.points:
                            points.add(elementStart, elementEnd);
                            break;
                        case chunk.vmode.edges:
                            edges.add(elementStart, elementEnd);
                            break;
                        case chunk.vmode.text:
                            texts.add(elementStart, elementEnd);
                            strings[elementStart] = allChunks[i].stringDesc;
                            break;
                    }
                    if (!layerIntervals.ContainsKey(allChunks[i].layer)) layerIntervals[allChunks[i].layer] = new intervals();
                    layerIntervals[allChunks[i].layer].add(elementStart, elementEnd);
                    if (!colorIntervals.ContainsKey(allChunks[i].color)) colorIntervals[allChunks[i].color] = new intervals();
                    intervals intv;
                    if (!colorIntervals.TryGetValue(allChunks[i].color, out intv))
                    {
                        intv = new intervals();
                        colorIntervals[allChunks[i].color] = intv;
                    }
                    intv.add(elementStart, elementEnd);

                    // wenn zu groß oder letztes, dann abschließen
                    if (i == allChunks.Count - 1 || vertexArray.Count + allChunks[i + 1].vertices.Length >= ushort.MaxValue)
                    {
                        MemSeg memSeg = new MemSeg(vertexArray, normalArray, elementArray, colorIntervals, layerIntervals, triangles, edges, lines, points, texts, strings);
                        msg.Add(memSeg);
                        if (i < allChunks.Count - 1)
                        {   // es geht noch weiter
                            colorIntervals = new SortedDictionary<int, intervals>();
                            layerIntervals = new SortedDictionary<string, intervals>();
                            vertexArray = new List<GeoPoint>();
                            normalArray = new List<GeoVector>();
                            elementArray = new List<int>();
                            triangles = new intervals();
                            edges = new intervals();
                            lines = new intervals();
                            points = new intervals();
                            texts = new intervals();
                            strings = new Dictionary<int, string>();
                        }
                    }
                }
            }

            private void collectChunks(glItem glItem)
            {
                if (glItem.subItems != null)
                {
                    for (int i = 0; i < glItem.subItems.Length; i++)
                    {
                        collectChunks(glItem.subItems[i]);
                    }
                }
                if (glItem.chunk != null && glItem.chunk.vertices != null)
                {
                    allLayers.Add(glItem.layer);
                    allColors.Add(glItem.color);
                    glItem.chunk.layer = glItem.layer;
                    glItem.chunk.color = glItem.color;
                    allChunks.Add(glItem.chunk);
                }
                //if (glItem.echunk != null && glItem.echunk.vertices != null)
                //{
                //    allLayers.Add(glItem.layer);
                //    allColors.Add(glItem.color);
                //    glItem.echunk.layer = glItem.layer;
                //    glItem.echunk.color = glItem.color;
                //    allChunks.Add(glItem.echunk);
                //}
            }

            private glItem parse(IGeoObject go)
            {
                if (go is IConvertForWebGL)
                {
                    WebGLData[] data = (go as IConvertForWebGL).Data;
                    if (data != null && data.Length > 0)
                    {
                        glItem item = new glItem(go, this);
                        if (go is Block) item.name = (go as Block).Name;
                        List<glItem> si = new List<glItem>();
                        for (int i = 0; i < data.Length; i++)
                        {
                            glItem sub = new glItem(go, this);
                            sub.color = data[i].color;
                            sub.name = "face: dontopen"; // damit nicht aufgeklappt wird
                            sub.chunk = new chunk();
                            sub.chunk.color = data[i].color;
                            sub.chunk.indices = data[i].indices;
                            sub.chunk.vertices = data[i].vertices;
                            sub.chunk.normals = data[i].normals;
                            sub.chunk.mode = chunk.vmode.triangles;
                            si.Add(sub);
                        }
                        item.subItems = si.ToArray();
                        return item;
                    }
                    IGeoObject replacement = (go as IConvertForWebGL).Conversion;
                    if (replacement != null) go = replacement;
                }
                if (go is Solid)
                {
                    glItem item = new glItem(go, this);
                    if (item.name == null) item.name = (go as Solid).Name;
                    List<glItem> si = new List<glItem>();
                    for (int i = 0; i < (go as Solid).Shells.Length; i++)
                    {
                        foreach (Face face in (go as Solid).Shells[i].Faces)
                        {
                            glItem sub = triangleFace(face);
                            if (sub != null) si.Add(sub);
                        }
                    }
                    foreach (Edge edge in (go as Solid).Edges)
                    {
                        if (edge.Curve3D != null)
                        {
                            glItem sub = parse(edge.Curve3D as IGeoObject);
                            if (sub != null && sub.chunk != null)
                            {
                                sub.chunk.mode = chunk.vmode.edges;
                                sub.name = "edge: " + edge.Curve3D.GetType().Name;
                                //sub.layer = findLayer(go);
                                si.Add(sub);
                            }
                        }
                    }
                    item.subItems = si.ToArray();
                    return item;
                }
                else if (go is Shell)
                {
                    glItem item = new glItem(go, this);
                    if (item.name == null) item.name = "";
                    List<glItem> si = new List<glItem>();
                    foreach (Face face in (go as Shell).Faces)
                    {
                        glItem sub = triangleFace(face);
                        if (sub != null) si.Add(sub);
                    }
                    foreach (Edge edge in (go as Shell).Edges)
                    {
                        if (edge.Curve3D != null)
                        {
                            glItem sub = parse(edge.Curve3D as IGeoObject);
                            if (sub != null && sub.chunk != null)
                            {
                                sub.chunk.mode = chunk.vmode.edges;
                                sub.name = "edge: " + edge.Curve3D.GetType().Name;
                                //sub.layer = findLayer(go);
                                si.Add(sub);
                            }
                        }
                    }
                    item.subItems = si.ToArray();
                    return item;
                }
                else if (go is Face)
                {
                    glItem item = triangleFace(go as Face);
                    if (item.name == null) item.name = "";
                    return item; // alleinestehendes face
                }
                else if (go is Hatch) // vo Block, da Hatch auch Block ist
                {
                    glItem item = new glItem(go, this);
                    if (item.name == null) item.name = "hatch";
                    List<glItem> si = new List<glItem>();
                    GeoObjectList list = (go as Hatch).Decompose();
                    foreach (IGeoObject sgo in list)
                    {
                        glItem sub = parse(sgo);
                        if (sub != null) si.Add(sub);
                    }
                    item.subItems = si.ToArray();
                    return item;
                }
                else if (go is Block)
                {
                    glItem item = new glItem(go, this);
                    if (item.name == null) item.name = (go as Block).Name;
                    List<glItem> si = new List<glItem>();
                    foreach (IGeoObject sgo in (go as Block).Children)
                    {
                        glItem sub = parse(sgo);
                        if (sub != null) si.Add(sub);
                    }
                    item.subItems = si.ToArray();
                    return item;
                }
                else if (go is BlockRef)
                {
                    glItem item = new glItem(go, this);
                    if (item.name == null) item.name = (go as BlockRef).ReferencedBlock.Name;
                    List<glItem> si = new List<glItem>();
                    foreach (IGeoObject sgo in (go as BlockRef).Flattened.Children)
                    {
                        glItem sub = parse(sgo);
                        if (sub != null) si.Add(sub);
                    }
                    item.subItems = si.ToArray();
                    return item;
                }
                else if (go is ICurve)
                {
                    if ((go as ICurve).Length < CADability.Precision.eps) return null;
                    ICurve apx = (go as ICurve).Approximate(true, precision);
                    glItem item = new glItem(go, this);
                    if (item.name == null) item.name = "curve: " + go.GetType().Name;
                    if (apx is Polyline)
                    {
                        item.chunk.vertices = (apx as Polyline).Vertices;
                        if ((apx as Polyline).IsClosed)
                        {
                            item.chunk.indices = new int[item.chunk.vertices.Length * 2];
                            for (int i = 0; i < item.chunk.indices.Length; i++)
                            {
                                item.chunk.indices[i] = (i + 1) / 2; // 0,1,1,2,2,3...
                            }
                            item.chunk.indices[item.chunk.indices.Length - 2] = item.chunk.vertices.Length - 1;
                            item.chunk.indices[item.chunk.indices.Length - 1] = 0;
                        }
                        else
                        {
                            item.chunk.indices = new int[item.chunk.vertices.Length * 2 - 2];
                            for (int i = 0; i < item.chunk.indices.Length; i++)
                            {
                                item.chunk.indices[i] = (i + 1) / 2; // 0,1,1,2,2,3...
                            }
                        }
                        item.chunk.mode = chunk.vmode.lines;
                        return item;
                    }
                    else if (apx is CADability.GeoObject.Path)
                    {
                        (apx as CADability.GeoObject.Path).Flatten();
                        if ((apx as CADability.GeoObject.Path).CurveCount > 0)
                        {
                            item.chunk.vertices = (apx as CADability.GeoObject.Path).Vertices;
                            if ((apx as CADability.GeoObject.Path).IsClosed)
                            {
                                item.chunk.indices = new int[item.chunk.vertices.Length * 2];
                                for (int i = 0; i < item.chunk.indices.Length; i++)
                                {
                                    item.chunk.indices[i] = (i + 1) / 2; // 0,1,1,2,2,3...
                                }
                                item.chunk.indices[item.chunk.indices.Length - 2] = item.chunk.vertices.Length - 1;
                                item.chunk.indices[item.chunk.indices.Length - 1] = 0;
                            }
                            else
                            {
                                item.chunk.indices = new int[item.chunk.vertices.Length * 2 - 2];
                                for (int i = 0; i < item.chunk.indices.Length; i++)
                                {
                                    item.chunk.indices[i] = (i + 1) / 2; // 0,1,1,2,2,3...
                                }
                            }
                            item.chunk.mode = chunk.vmode.lines;
                            return item;
                        }
                    }
                    else if (apx is Line)
                    {
                        item.chunk.vertices = new GeoPoint[] { (apx as Line).StartPoint, (apx as Line).EndPoint };
                        item.chunk.indices = new int[] { 0, 1 };
                        item.chunk.mode = chunk.vmode.lines;
                        return item;
                    }
                }
                else if (go is CADability.GeoObject.Point)
                {
                    glItem item = new glItem(go, this);
                    if (item.name == null) item.name = "point: " + go.GetType().Name;
                    GeoPoint p = (go as CADability.GeoObject.Point).Location;
                    item.chunk.vertices = new GeoPoint[] { p, p, p, p }; // 4 mal den gleichen Punkt, das ist so am einfachsten. Im shader wird es so benötigt
                    item.chunk.indices = new int[] { 0, 1, 2, 0, 2, 3 }; // zwei Dreiecke
                    item.chunk.mode = chunk.vmode.points;
                    return item;
                }
                else if (go is CADability.GeoObject.Text)
                {
                    glItem item = new glItem(go, this);
                    if (item.name == null) item.name = "text: " + go.GetType().Name;
                    GeoPoint[] p = (go as CADability.GeoObject.Text).FourPoints;
                    item.chunk.vertices = new GeoPoint[] { p[0], p[1], p[2], p[3] }; // links unten, rechts unten, rechts oben, links oben
                    item.chunk.indices = new int[] { 0, 1, 2, 0, 2, 3 }; // zwei Dreiecke
                    item.chunk.mode = chunk.vmode.text;
                    item.chunk.stringDesc = (go as CADability.GeoObject.Text).Font + "|" + (go as CADability.GeoObject.Text).TextString;
                    return item;
                }
                else if (go is Dimension)
                {

                    glItem item = new glItem(go, this);
                    if (item.name == null) item.name = "dimension";
                    List<glItem> si = new List<glItem>();
                    GeoObjectList list = (go as Dimension).GetList();
                    for (int i = 0; i < list.Count; i++)
                    {
                        glItem sub = parse(list[i]);
                        if (sub != null) si.Add(sub);
                    }
                    item.subItems = si.ToArray();
                    return item;
                }
                return null;
            }

            private string findLayer(IGeoObject go)
            {
                if (go.Layer != null) return go.Layer.Name;
                if (go.Owner is IGeoObject) return findLayer(go.Owner as IGeoObject);
                return "";
            }

            private glItem triangleFace(Face face)
            {
                GeoPoint[] trianglePoint;
                GeoPoint2D[] triangleUVPoint;
                int[] triangleIndex;
                int[] edgeIndex;
                face.GetSimpleTriangulation(precision, false, out trianglePoint, out triangleUVPoint, out triangleIndex, out edgeIndex);
                GeoVector[] normals = new GeoVector[trianglePoint.Length];
                for (int i = 0; i < normals.Length; i++)
                {
                    normals[i] = face.Surface.GetNormal(triangleUVPoint[i]);
                    if (!normals[i].IsNullVector()) normals[i].Norm();
                }
                if (trianglePoint != null)
                {
                    glItem item = new glItem(face, this);
                    if (item.name == null) item.name = "face: " + face.Surface.GetType().Name;
                    item.chunk = new chunk();
                    item.chunk.vertices = trianglePoint;
                    item.chunk.normals = normals;
                    item.chunk.indices = triangleIndex;
                    item.chunk.mode = chunk.vmode.triangles;

                    return item;
                }
                return null;
            }

            int IComparer<Color>.Compare(Color x, Color y)
            {
                return x.ToArgb().CompareTo(y.ToArgb());
            }

            internal void createJSON(bool isDemo)
            {
                allChunks = new List<chunk>();
                allColors = new HashSet<int>();
                allLayers = new HashSet<string>();

                topItems = new List<glItem>();
                ColorDef lastColor = null;
                foreach (IGeoObject go in model)
                {
                    glItem item = parse(go);
                    if (item != null) topItems.Add(item);
                    if (go is IColorDef) lastColor = (go as IColorDef).ColorDef;
                }
                if (isDemo)
                {
                    Text demo = Text.Construct();
                    double txtsize = Math.Max(ext.Size / 24.0, Math.Min(ext.ZDiff / 2.0, ext.YDiff / 8.0));
                    demo.Set(txtsize * GeoVector.YAxis, txtsize * GeoVector.ZAxis, new GeoPoint(ext.Xmax + precision * 10, ext.Ymin, ext.Zmin), "Arial", "Demo", FontStyle.Regular, Text.AlignMode.Bottom, Text.LineAlignMode.Left);
                    demo.ColorDef = lastColor;
                    topItems.Add(parse(demo));

                    demo = Text.Construct();
                    txtsize = Math.Max(ext.Size / 24.0, Math.Min(ext.ZDiff / 2.0, ext.YDiff / 8.0));
                    demo.Set(txtsize * GeoVector.XAxis, txtsize * GeoVector.ZAxis, new GeoPoint(ext.Xmin, ext.Ymin - precision * 10, ext.Zmin), "Arial", "Demo", FontStyle.Regular, Text.AlignMode.Bottom, Text.LineAlignMode.Left);
                    demo.ColorDef = lastColor;
                    topItems.Add(parse(demo));

                    ext.Expand(precision * 10);
                }
                for (int i = 0; i < topItems.Count; i++)
                {
                    collectChunks(topItems[i]);
                }
                msg = new List<MemSeg>();
                createVertexArrays();
                obh = new ObjectHierarchy[topItems.Count];
                ObjectHierarchy.memSegCount = msg.Count;
                intervals[] intvs = new intervals[msg.Count];
                for (int i = 0; i < intvs.Length; i++)
                {
                    intvs[i] = new intervals();
                }
                for (int i = 0; i < topItems.Count; i++)
                {
                    obh[i] = new ObjectHierarchy(topItems[i], intvs);
                }
            }
        }


        private Model m;
        private GenData data;
        /// <summary>
        /// Precision for triangulation of Faces and approximation of curves to polylines.
        /// If no value is provided the precision will be 1/1000 of the model extend
        /// </summary>
        public double Precision
        {
            get
            {
                return data.precision;
            }
            set
            {
                data.precision = value;
            }
        }
        /// <summary>
        /// Use the value of this UserData as the name property of the object (as shown in the HTML ControlCenter)
        /// </summary>
        public string UserDataAsName
        {
            get
            {
                return data.userDataAsName;
            }
            set
            {
                data.userDataAsName = value;
            }
        }
        /// <summary>
        /// A list of keys for UserData which will be ignored (for display in the HTML ControlCenter).
        /// IgnoreUserData and UseOnlyUserData are mutually exclusive.
        /// </summary>
        public string[] IgnoreUserData
        {
            get
            {
                if (data.userDataIgnore) return data.userDataKeys;
                return null;
            }
            set
            {
                data.userDataKeys = value;
                data.userDataIgnore = true;
            }
        }
        /// <summary>
        /// A list of keys for UserData which will be used (for display in the HTML ControlCenter).
        /// IgnoreUserData and UseOnlyUserData are mutually exclusive.
        /// </summary>
        public string[] UseOnlyUserData
        {
            get
            {
                if (!data.userDataIgnore) return data.userDataKeys;
                return null;
            }
            set
            {
                data.userDataKeys = value;
                data.userDataIgnore = false;
            }
        }
        /// <summary>
        /// Location of the template html file. If no value is provided the file "CADability.WebGl.html" in the CADability installation directory will be used
        /// </summary>
        public string HtmlTemplatePath;
        /// <summary>
        /// A initial direction for the projection may be specified.
        /// </summary>
        public GeoVector InitialProjectionDirection;
        /// <summary>
        /// Create the ExportToWebGl class with the model to be exported
        /// </summary>
        /// <param name="m"></param>
        public ExportToWebGl(Model m)
        {
            this.m = m;
#if DEBUG
            // testweise UserData erzeugen
            foreach (IGeoObject go in m)
            {
                go.UserData.Add("Type", go.GetType().ToString());
                if (go is Solid)
                {
                    go.UserData.Add("*Faces", (go as Solid).Shells.Length);
                    go.UserData.Add("Link", "<a href='http://www.sofa.de'>SOFA</a>");
                }
                if (go is Dimension)
                {
                    Dictionary<string, object> dp = new Dictionary<string, object>();
                    go.UserData.Add("Points", dp);
                    Dimension dim = go as Dimension;
                    for (int i = 0; i < dim.PointCount; i++)
                    {
                        GeoPoint p = dim.GetPoint(i);
                        dp[i.ToString()] = (object)p;
                    }
                }
            }
#endif
            InitialProjectionDirection = new GeoVector(-1, -1, -1);
            data = new GenData(m);
        }
        /// <summary>
        /// Write the resulting html file to the provided location.
        /// </summary>
        /// <param name="outPath"></param>
        /// <returns>true on success</returns>
        public bool WriteToFile(string outPath)
        {
            data.ipd = InitialProjectionDirection;

            bool isDemo = true;
            if (HtmlTemplatePath != null)
            {
                using (StreamReader infile = new StreamReader(HtmlTemplatePath))
                {
                    string line = infile.ReadLine();
                    if (line != null && line.Contains("licensed"))
                    {
                        string[] parts = line.Split(new string[] { ":" }, 2, StringSplitOptions.None);
                        if (parts.Length == 2)
                        {
                            string company = parts[1].Replace("-->", "").Trim();
                            isDemo = false;
                        }
                    }
                    infile.Close();
                }
            }

            data.createJSON(isDemo);
            if (HtmlTemplatePath == null)
            {
                Assembly ThisAssembly = Assembly.GetExecutingAssembly();
                int lastSlash = ThisAssembly.Location.LastIndexOf('\\');
                HtmlTemplatePath = ThisAssembly.Location.Substring(0, lastSlash) + "\\CADability.WebGl.html";
            }

            try
            {
                string line;
                using (StreamReader infile = new StreamReader(HtmlTemplatePath))
                using (StreamWriter outfile = new StreamWriter(outPath, false, Encoding.UTF8))
                {
                    bool firstLine = true;
                    while ((line = infile.ReadLine()) != null)
                    {
                        if (line.IndexOf("%%CADability-WebGL-Data%%") >= 0 && line.Contains("document[\"cadModel\"]"))
                        {
                            line = line.Replace("%%CADability-WebGL-Data%%", data.ToJSON());
                        }
                        if (!(firstLine && line.Contains("licensed"))) outfile.WriteLine(line);
                        firstLine = false;
                    }

                    infile.Close();
                    outfile.Close();
                }
            }
            catch (IOException)
            {
                return false;
            }

            return true;
        }
        public string GetData(bool escapeQuote)
        {
            data.ipd = InitialProjectionDirection;
            data.createJSON(false);
            return data.ToJSON(escapeQuote);
        }
    }
}
#endif