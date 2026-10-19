using CADability;
using CADability.GeoObject;
using System.Reflection;
using Path = System.IO.Path;

namespace CADability.WasmSmoke
{
    /// <summary>
    /// Runs CADability.dll as WebAssembly (issue #412): the same checks a browser front end depends on, in the browser runtime
    /// of .NET under Node.js, without a frame and without GDI+. Every check prints PASS or FAIL; the exit code is the number
    /// of failed checks.
    /// </summary>
    internal static class Program
    {
        private static int failed;

        private static int Main()
        {
            Console.WriteLine($"CADability WebAssembly smoke test on {System.Runtime.InteropServices.RuntimeInformation.OSDescription} ({System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture})");

            Check("the font comes from bytes", () =>
            {
                // there are no system fonts in the browser: the front end supplies the font files
                ManagedFontProvider fonts = new ManagedFontProvider(Array.Empty<string>(), false);
                fonts.AddFontData(Resource("LiberationSans-Regular.ttf"));
                FontProvider.Current = fonts;
            });

            Project project = null;
            Check("a project is created", () =>
            {
                project = Project.CreateSimpleProject();
                Expect(FrameImpl.MainFrame == null, "there is no frame");
            });
            Model model = project.GetActiveModel();

            Check("a solid with a hole is made by a boolean operation", () =>
            {
                Solid box = Make3D.MakeBox(GeoPoint.Origin, 100 * GeoVector.XAxis, 60 * GeoVector.YAxis, 40 * GeoVector.ZAxis);
                Solid cylinder = Make3D.MakeCylinder(new GeoPoint(50, 30, -10), 15 * GeoVector.XAxis, 60 * GeoVector.ZAxis);
                Solid[] result = Make3D.Difference(box, cylinder);
                Expect(result.Length == 1, $"one solid, got {result.Length}");
                double expected = 100 * 60 * 40 - Math.PI * 15 * 15 * 40;
                double volume = result[0].Volume(1e-3);
                Expect(Math.Abs(volume - expected) < expected * 1e-3, $"volume {volume}, expected {expected}");
                model.Add(result[0]);
            });

            Text text = null;
            Check("a text is measured with the font of the kernel", () =>
            {
                text = Text.Construct();
                text.Font = "Liberation Sans";
                text.TextString = "CADability 412";
                text.Location = new GeoPoint(0, 80, 0);
                text.LineDirection = GeoVector.XAxis;
                text.GlyphDirection = GeoVector.YAxis;
                text.TextSize = 10;
                BoundingCube extent = text.GetBoundingCube();
                Expect(!extent.IsEmpty && extent.Xmax - extent.Xmin > 50 && extent.Ymax - extent.Ymin > 5, $"the extent of the glyphs, got {extent}");
                Expect(text.GetOutline().Count > 0, "the glyph outlines");
                model.Add(text);
            });

            Picture picture = null;
            Check("a picture keeps its image file", () =>
            {
                picture = Picture.Construct();
                picture.Set(Png3x2, new GeoPoint(0, -50, 0), new GeoVector(30, 0, 0), new GeoVector(0, 20, 0));
                Expect((picture.PixelWidth, picture.PixelHeight) == (3, 2), $"3×2 pixels, got {picture.PixelWidth}×{picture.PixelHeight}");
                model.Add(picture);
            });

            Check("the model is painted by a renderer of its own", () =>
            {
                RecordingPaintTo3D renderer = new RecordingPaintTo3D();
                foreach (IGeoObject go in model.AllObjects) go.PrePaintTo3D(renderer);
                foreach (IGeoObject go in model.AllObjects) go.PaintTo3D(renderer);
                Console.WriteLine($"      {renderer.Triangles} triangles, {renderer.Polylines} polylines, {renderer.TextCalls} text calls, {renderer.Bitmaps} bitmaps");
                Expect(renderer.Triangles > 100, "the faces of the solid and the glyphs are triangulated");
                Expect(renderer.Polylines > 10, "the edges are painted");
                Expect(renderer.Extent.Xmin < 1 && renderer.Extent.Xmax > 99 && renderer.Extent.Ymin < -49 && renderer.Extent.Ymax > 85, $"the painted extent, got {renderer.Extent}");
            });

            Check("the project is saved and read as JSON", () =>
            {
                MemoryStream written = new MemoryStream();
                project.WriteToJson(written);
                Model read = Project.ReadFromJson(new MemoryStream(written.ToArray())).GetActiveModel();
                Expect(read.AllObjects.OfType<Solid>().Count() == 1, "the solid");
                Expect(read.AllObjects.OfType<Text>().Single().TextString == "CADability 412", "the text");
                Expect(read.AllObjects.OfType<Picture>().Single().ImageData.SequenceEqual(Png3x2), "the image file of the picture");
            });

            Check("a project file of CADability 1.x with pictures opens", () =>
            {
                Project old = Project.ReadFromJson(new MemoryStream(Resource("pictures-1x.cdb.json")));
                Picture[] pictures = old.GetActiveModel().AllObjects.OfType<Picture>().ToArray();
                Expect(pictures.Length > 0 && pictures.All(p => p.PixelWidth > 0), $"pictures with their image files, got {pictures.Length}");
            });

            Check("a project file of CADability 1.x with page settings opens", () =>
            {
                Project old = Project.ReadFromJson(new MemoryStream(Resource("pagesettings-1x-net48.cdb.json")));
                Expect(old.DefaultPageSetup.PaperName == "A3" && old.DefaultPageSetup.Landscape, "A3 landscape");
            });

            Check("a project file with solids opens", () =>
            {
                Project volumes = Project.ReadFromJson(new MemoryStream(Resource("Volumes.cdb.json")));
                Expect(volumes.GetActiveModel().AllObjects.OfType<Solid>().Any(), "solids");
            });

            Check("a STEP file is imported", () =>
            {
                string file = Path.Combine(Path.GetTempPath(), "issue101.stp");
                File.WriteAllBytes(file, Resource("issue101.stp"));
                Project imported = Project.ReadFromFile(file, "stp");
                Expect(imported.GetActiveModel().AllObjects.Count > 0, "objects");
            });

            Check("the project is exported to DXF and STL", () =>
            {
                string dxf = Path.Combine(Path.GetTempPath(), "smoke.dxf");
                Expect(project.Export(dxf, "dxf") && new FileInfo(dxf).Length > 0, "the DXF file");
                Project imported = Project.ReadFromFile(dxf, "dxf");
                Expect(imported.GetActiveModel().AllObjects.OfType<Text>().Any(), "the text in the DXF file");
                string stl = Path.Combine(Path.GetTempPath(), "smoke.stl");
                project.ExportToSTL(stl);
                Expect(new FileInfo(stl).Length > 0, "the STL file");
            });

            Check("the finalizers run", () =>
            {
                // the GDI version of the FontCache had a finalizer that threw without GDI and ended the process
                for (int i = 0; i < 10; ++i) Text.Construct().GetBoundingCube();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            });

            Console.WriteLine(failed == 0 ? "All checks passed." : $"{failed} check(s) failed.");
            return failed;
        }

        // a PNG of 3×2 pixels
        private static readonly byte[] Png3x2 = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAMAAAACCAIAAAASFvFNAAAAG0lEQVR4nAXBAQEAAACCIDe5qbHOCgCHIeSyPCtwBd1HI03nAAAAAElFTkSuQmCC");

        private static void Check(string name, Action check)
        {
            try
            {
                check();
                Console.WriteLine("PASS  " + name);
            }
            catch (Exception e)
            {
                ++failed;
                Console.WriteLine("FAIL  " + name);
                Console.WriteLine("      " + e.ToString().Replace("\n", "\n      "));
            }
        }
        private static void Expect(bool condition, string what)
        {
            if (!condition) throw new Exception("expected " + what);
        }
        private static byte[] Resource(string name)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            using (MemoryStream copy = new MemoryStream())
            {
                stream.CopyTo(copy);
                return copy.ToArray();
            }
        }
    }
}
