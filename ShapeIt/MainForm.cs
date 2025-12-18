using CADability;
using CADability.Actions;
using CADability.Attribute;
using CADability.Forms.NET8;
using CADability.GeoObject;
using CADability.UserInterface;
using MathNet.Numerics.LinearAlgebra.Factorization;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using static ShapeIt.MainForm;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using Point = System.Drawing.Point;

namespace ShapeIt
{

    public partial class MainForm : CadForm
    {
        private PictureBox logoBox;
        private ModellingPropertyEntries modellingPropertyEntries;
        private DateTime lastSaved; // time, when the current file has been saved the last time, see OnIdle
        private bool modifiedSinceLastAutosave = false;
        bool projectionChanged = false; // to handle projection changes in OnIdle
        bool crashChecked = false;

        private Control FindControlByName(Control parent, string name)
        {
            foreach (Control child in parent.Controls)
            {
                if (child.Name == name)
                    return child;

                Control found = FindControlByName(child, name);
                if (found != null)
                    return found;
            }

            return null;
        }

        void FadeOutPictureBox(PictureBox pb)
        {
            var timer = new System.Windows.Forms.Timer();
            timer.Interval = 50;
            double alpha = 1.0;

            Image original = pb.Image;
            Bitmap faded = new Bitmap(original.Width, original.Height);

            timer.Tick += (s, e) =>
            {
                alpha -= 0.01;
                if (alpha <= 0)
                {
                    timer.Stop();
                    pb.Parent.Controls.Remove(pb);
                    //pb.Visible = false;
                    pb.Dispose();
                    return;
                }

                using (Graphics g = Graphics.FromImage(faded))
                {
                    g.Clear(Color.Transparent);
                    ColorMatrix matrix = new ColorMatrix
                    {
                        Matrix33 = (float)alpha // Alpha-Kanal
                    };
                    ImageAttributes attributes = new ImageAttributes();
                    attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

                    g.DrawImage(original,
                        new Rectangle(0, 0, faded.Width, faded.Height),
                        0, 0, original.Width, original.Height,
                        GraphicsUnit.Pixel,
                        attributes);
                }

                pb.Image = (Image)faded.Clone(); // neues Bild setzen
            };

            timer.Start();
        }

        private void ShowLogo()
        {
            Control pex = FindControlByName(this, "propertiesExplorer");
            // Create PictureBox 
            logoBox = new PictureBox();
            Assembly ThisAssembly = Assembly.GetExecutingAssembly();
            using (System.IO.Stream str = ThisAssembly.GetManifestResourceStream("ShapeIt.Resources.ShapeIt2.png"))
            {
                logoBox.Image = new Bitmap(str);
            }
            logoBox.SizeMode = PictureBoxSizeMode.Zoom;

            double aspectRatio = (double)logoBox.Image.Height / logoBox.Image.Width;

            // Zielbreite übernehmen
            int targetWidth = pex.ClientSize.Width - 4;
            int berechneteHoehe = (int)(targetWidth * aspectRatio);

            // Größe setzen
            logoBox.Size = new Size(targetWidth, berechneteHoehe);

            // Position am unteren Rand
            logoBox.Location = new Point(2, pex.ClientSize.Height - berechneteHoehe - 2);

            // Logo zum Ziel-Control hinzufügen
            pex.Controls.Add(logoBox);
            logoBox.BringToFront();

            FadeOutPictureBox(logoBox);

            pex.Resize += (s, e) =>
            {
                int newWidth = pex.ClientSize.Width - 4;
                int newHeight = (int)(newWidth * aspectRatio);
                logoBox.Size = new Size(newWidth, newHeight);
                logoBox.Location = new Point(2, pex.ClientSize.Height - newHeight - 2);
            };
        }

        private string ReadEmbeddedVersion()
        {
            var asm = typeof(Program).Assembly;
            using var s = asm.GetManifestResourceStream("App.Version");
            using var sr = new StreamReader(s!);
            return sr.ReadToEnd().Trim();
        }
        public MainForm(string[] args) : base(args)
        {   // interpret the command line arguments as a name of a file, which should be opened

            //InitializeComponent();
            ShowLogo();
            // this.Icon = Properties.Resources.Icon;
            Assembly ThisAssembly = Assembly.GetExecutingAssembly();
            System.IO.Stream str;
            using (str = ThisAssembly.GetManifestResourceStream("ShapeIt.Resources.Icon.ico"))
            {
                this.Icon = new System.Drawing.Icon(str);
            }

            string fileName = "";
            for (int i = 0; i < args.Length; i++)
            {
                if (!args[i].StartsWith("-"))
                {
                    fileName = args[i];
                    break;
                }
            }
            Project toOpen = null;
            if (!String.IsNullOrWhiteSpace(fileName))
            {
                try
                {
                    toOpen = Project.ReadFromFile(fileName);
                }
                catch { }
            }
            if (toOpen == null) CadFrame.GenerateNewProject();
            else CadFrame.Project = toOpen;

            string version = ReadEmbeddedVersion(); // version from version.txt
            this.Text = $"ShapeIt with CADability – Version: {version}";

            if (!Settings.GlobalSettings.ContainsSetting("UserInterface"))
            {
                Settings UserInterface = new Settings("UserInterface");
                Settings.GlobalSettings.AddSetting("UserInterface", UserInterface);
                IntegerProperty ToolbarButtonSize = new IntegerProperty("ToolbarButtonSize", "ToolbarButtonSize");
                ToolbarButtonSize.IntegerValue = 0;
                UserInterface.AddSetting("ToolbarButtonSize", ToolbarButtonSize);
                IntegerProperty MenuSize = new IntegerProperty("MenuSize", "MenuSize");
                MenuSize.IntegerValue = 0;
                UserInterface.AddSetting("MenuSize", MenuSize);
            }
            Settings.GlobalSettings.SetValue("Construct.3D_Delete2DBase", false);
            bool exp = Settings.GlobalSettings.GetBoolValue("Experimental.TestNewContextMenu", false);
            bool tst = Settings.GlobalSettings.GetBoolValue("ShapeIt.Initialized", false);
            Settings.GlobalSettings.SetValue("ShapeIt.Initialized", true);
            CadFrame.FileNameChangedEvent += (name) =>
            {
                if (string.IsNullOrEmpty(name)) this.Text = "ShapeIt with CADability";
                else this.Text = "ShapeIt -- " + name;
                lastSaved = DateTime.Now; // a new file has been opened
            };
            CadFrame.ProjectClosedEvent += OnProjectClosed;
            CadFrame.ProjectOpenedEvent += OnProjectOpened;
            CadFrame.UIService.ApplicationIdle += OnIdle;
            CadFrame.ViewsChangedEvent += OnViewsChanged;
            if (CadFrame.ActiveView != null) OnViewsChanged(CadFrame);
            CadFrame.ControlCenter.RemovePropertyPage("View");
            using (str = ThisAssembly.GetManifestResourceStream("ShapeIt.StringTableDeutsch.xml"))
            {
                XmlDocument stringXmlDocument = new XmlDocument();
                stringXmlDocument.Load(str);
                StringTable.AddStrings(stringXmlDocument);
            }

            using (str = ThisAssembly.GetManifestResourceStream("ShapeIt.StringTableEnglish.xml"))
            {
                XmlDocument stringXmlDocument = new XmlDocument();
                stringXmlDocument.Load(str);
                StringTable.AddStrings(stringXmlDocument);
            }
            using (str = ThisAssembly.GetManifestResourceStream("ShapeIt.MenuResource.xml"))
            {
                XmlDocument menuDocument = new XmlDocument();
                menuDocument.Load(str);
#if DEBUG
                // inject an additional menu "Debug", which we can use to directly execute some debugging code
                XmlNode mainMenu = menuDocument.SelectSingleNode("Menus/MainMenu/Popup[@MenuId='MenuId.Extras']");
                if (mainMenu != null)
                {
                    // Create a new MenuItem element.
                    XmlElement debugMenuItem = menuDocument.CreateElement("MenuItem");
                    // Set the MenuId attribute to "MenuId.Debug".
                    debugMenuItem.SetAttribute("MenuId", "MenuId.Debug");
                    // Append the new MenuItem element to the MainMenu node.
                    mainMenu.AppendChild(debugMenuItem);
                }
#endif
                XmlNode toolbar = menuDocument.SelectSingleNode("Menus/Popup[@MenuId='Toolbar']");
                SetToolbar(toolbar);
                MenuResource.SetMenuResource(menuDocument);
                ResetMainMenu(null);
            }

            lastSaved = DateTime.Now;
            AppDomain.CurrentDomain.UnhandledException += (sender, exobj) =>
            {
                // exobj.ExceptionObject as Exception;
                try
                {
                    string path = System.IO.Path.GetTempPath();
                    path = System.IO.Path.Combine(path, "ShapeIt");
                    DirectoryInfo dirInfo = Directory.CreateDirectory(path);
                    string currentFileName = CadFrame.Project.FileName;
                    if (string.IsNullOrEmpty(CadFrame.Project.FileName))
                    {
                        path = System.IO.Path.Combine(path, "crash_" + DateTime.Now.ToString("yyMMddHHmm") + ".cdb.json");
                        currentFileName = "unknown";
                    }
                    else
                    {
                        string crashFileName = System.IO.Path.GetFileNameWithoutExtension(CadFrame.Project.FileName);
                        if (crashFileName.EndsWith(".cdb")) crashFileName = System.IO.Path.GetFileNameWithoutExtension(crashFileName); // we usually have two extensions: .cdb.json
                        path = System.IO.Path.Combine(path, crashFileName + "_X.cdb.json");
                    }
                    CadFrame.Project.WriteToFile(path);
                    File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), @"ShapeIt\Crash.txt"), currentFileName + "\n" + path);
                }
                catch (Exception) { }
                ;
            };
            // the following installs the property page for modelling. This connects all modelling
            // tasks of ShapeIt with CADability
            IPropertyPage modellingPropPage = CadFrame.ControlCenter.AddPropertyPage("Modelling", 6);
            modellingPropertyEntries = new ModellingPropertyEntries(CadFrame);
            modellingPropPage.Add(modellingPropertyEntries, false);
            CadFrame.ControlCenter.ShowPropertyPage("Modelling");
        }

        private void OnViewsChanged(IFrame theFrame)
        {
            theFrame.ActiveView.Projection.ProjectionChangedEvent -= OnProjectionChanged; // not to double it?
            theFrame.ActiveView.Projection.ProjectionChangedEvent += OnProjectionChanged;
        }

        private void OnProjectionChanged(Projection sender, EventArgs args)
        {
            projectionChanged = true;
        }

        protected override void OnShown(EventArgs e)
        {
            // check for crash
            if (!crashChecked)
            {
                crashChecked = true;
                string crashPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), @"ShapeIt\Crash.txt");
                if (File.Exists(crashPath))
                {
                    string[] lines = File.ReadAllLines(System.IO.Path.Combine(System.IO.Path.GetTempPath(), @"ShapeIt\Crash.txt"));
                    if (lines.Length == 2)
                    {
                        string ask = StringTable.GetFormattedString("ShapeIt.RestoreAfterCrash", lines[0]);
                        if (CadFrame.UIService.ShowMessageBox(ask, "ShapeIt", CADability.Substitutes.MessageBoxButtons.YesNo) == CADability.Substitutes.DialogResult.Yes)
                        {
                            CadFrame.Project = Project.ReadFromFile(lines[1]);
                            CadFrame.Project.FileName = lines[0];

                            this.Text = "ShapeIt -- " + lines[0];
                        }
                    }
                    File.Delete(crashPath);
                }
#if DEBUG
                AutoDebug();
#endif
            }
            base.OnActivated(e);
        }
#if DEBUG
        private void AutoDebug()
        {
           // return;
            string? filename = null; // @"F:\Zeichnungen\RoundTest5.cdb.json";
            // add code here to be executed automatically upon start in debug mode
            // there is no mouse interaction before this code is finished
            if (string.IsNullOrEmpty(filename))
            {
                string[] mru = MRUFiles.GetMRUFiles();
                if (mru.Length > 0) filename = mru.Last().Split(';')[0];
            }
            if (!string.IsNullOrEmpty(filename)) CadFrame.Project = Project.ReadFromFile(filename);

            string command = "";
            List<CADability.GeoObject.Solid> slds = new List<CADability.GeoObject.Solid>();
            Solid operand1 = null, operand2 = null;
            List<Solid> difference = new List<Solid>();
            List<Solid> intersection = new List<Solid>();
            List<Solid> union = new List<Solid>();
            List<ICurve> edgeMarkers = new List<ICurve>();
            foreach (CADability.GeoObject.IGeoObject go in CadFrame.Project.GetActiveModel().AllObjects)
            {
                if (go is CADability.GeoObject.Solid sld)
                {
                    slds.Add(sld);
                    if (sld.Style != null)
                    {
                        if (sld.Style.Name == "Operand1") operand1 = sld;
                        else if (sld.Style.Name == "Operand2") operand2 = sld;
                        else if (sld.Style.Name == "Difference") difference.Add(sld);
                        else if (sld.Style.Name == "Union") union.Add(sld);
                        else if (sld.Style.Name == "Intersection") intersection.Add(sld);
                    }
                }
                if (go is ICurve curve)
                {
                    if (go.Style.Name == "EdgeMarker")
                    {
                        edgeMarkers.Add(curve);
                    }
                }
                if (go is Text txt)
                {
                    command = txt.TextString; // there sould only be one
                }
            }
            if (operand1 != null && operand2 != null)
            {
                //if (difference.Count > 0)
                //{
                //    Solid[] sres = NewBooleanOperation.Subtract(operand1, operand2);
                //    if (sres.Length > 0)
                //    {
                //        Project proj = Project.CreateSimpleProject();
                //        proj.GetActiveModel().Add(sres);
                //        proj.WriteToFile("c:\\Temp\\subtract.cdb.json");
                //    }
                //}
                //if (command.StartsWith("Difference",StringComparison.OrdinalIgnoreCase))
                //{
                //    Solid[] sres = NewBooleanOperation.Subtract(operand1, operand2);
                //}
            }
            if (operand1 != null && operand2 != null)
            {
                if (command.Equals("Difference", StringComparison.OrdinalIgnoreCase))
                {
                    Solid[] sres = NewBooleanOperation.Subtract(operand1, operand2);
                }
                if (command.Equals("Union", StringComparison.OrdinalIgnoreCase))
                {
                    Solid sres = NewBooleanOperation.Unite(operand1, operand2);
                }
            }
            if (slds.Count == 2)
            {
                //Solid un = NewBooleanOperation.Unite(slds[0], slds[1]);
                //Solid[] sld;
                //if (slds[0].Volume(0.1) > slds[1].Volume(0.1))
                //{
                //    sld = NewBooleanOperation.Subtract(slds[0], slds[1]);
                //}
                //else
                //{
                //    sld = NewBooleanOperation.Subtract(slds[1], slds[0]);
                //}
                //if (sld.Length > 0)
                //{
                //    Project proj = Project.CreateSimpleProject();
                //    proj.GetActiveModel().Add(sld);
                //    proj.WriteToFile("c:\\Temp\\subtract.cdb.json");
                //}
            }
            if (edgeMarkers.Count > 0)
            {
                List<Edge> edgesToRound = [];
                Shell? shellToRound = null;
                foreach (Solid s in slds)
                {
                    foreach (Edge edg in s.Shells[0].Edges)
                    {
                        foreach (ICurve em in edgeMarkers)
                        {
                            if (edg.Curve3D.SameGeometry(em, 0.1))
                            {
                                edgesToRound.Add(edg);
                                shellToRound = s.Shells[0];
                            }
                        }
                    }
                }
                if (command.StartsWith("RoundEdges", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = command.Split(':');
                    if (parts.Length==2)
                    {
                        double d = double.Parse(parts[1]);
                        if (d>0)
                        {
                            Shell? rounded = shellToRound?.RoundEdges(edgesToRound, d);
                        }
                    }
                }
                if (command.StartsWith("ChamferEdges", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = command.Split(':');
                    if (parts.Length == 2)
                    {
                        double d = double.Parse(parts[1]);
                        if (d > 0)
                        {
                            Shell? rounded = shellToRound?.ChamferEdges(edgesToRound, d, d);
                        }
                    }
                }
            }
        }
#endif

        /// <summary>
        /// Filter the escape key for the modelling property page
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="keyData"></param>
        /// <returns></returns>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys nmKeyData = (Keys)((int)keyData & 0x0FFFF);
            CADability.Substitutes.KeyEventArgs e = new CADability.Substitutes.KeyEventArgs((CADability.Substitutes.Keys)keyData);
            if (nmKeyData == Keys.Escape)
            {
                if (modellingPropertyEntries.OnEscape()) return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
        /// <summary>
        /// Called when CADability is idle. We use it to save the current project data to a temp file in case of a crash
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        // slowdown OnIdle polling:
        readonly Stopwatch _idleSw = Stopwatch.StartNew();
        const int MinIdleCheckMs = 250;
        bool _idleBusy;
        void OnIdle(object sender, EventArgs e)
        {
            if (_idleBusy) return;
            if (_idleSw.ElapsedMilliseconds < MinIdleCheckMs) return;

            _idleBusy = true;
            _idleSw.Restart();
            try
            {
                if (projectionChanged)
                {
                    projectionChanged = false;
                    modellingPropertyEntries.OnProjectionChanged(); // to update the feedback objects, which are projection dependant
                }
                if (modifiedSinceLastAutosave && (DateTime.Now - lastSaved).TotalMinutes > 2)
                {
                    modifiedSinceLastAutosave = false;
                    string path = System.IO.Path.GetTempPath();
                    path = System.IO.Path.Combine(path, "ShapeIt");
                    DirectoryInfo dirInfo = Directory.CreateDirectory(path);
                    string currentFileName = CadFrame.Project.FileName;
                    if (string.IsNullOrEmpty(CadFrame.Project.FileName)) path = System.IO.Path.Combine(path, "noname.cdb.json");
                    else
                    {
                        string fileName = System.IO.Path.GetFileNameWithoutExtension(CadFrame.Project.FileName);
                        if (fileName.EndsWith(".cdb")) fileName = System.IO.Path.GetFileNameWithoutExtension(fileName); // we usually have two extensions: .cdb.json
                        path = System.IO.Path.Combine(path, fileName + "_.cdb.json");
                    }
                    CadFrame.Project.WriteToFile(path);
                    CadFrame.Project.FileName = currentFileName; // Project.WriteToFile changes the Project.FileName, restore the current name
                    lastSaved = DateTime.Now;
                }
            }
            finally { _idleBusy = false; }
        }
        void OnProjectClosed(Project theProject, IFrame theFrame)
        {
            // manage autosave OnIdle, remove autosaved files
        }
        /// <summary>
        /// When a new or exisiting project has been opened
        /// </summary>
        /// <param name="theProject"></param>
        /// <param name="theFrame"></param>
        private void OnProjectOpened(Project theProject, IFrame theFrame)
        {
            theProject.ProjectModifiedEvent += (Project sender) =>
            {   // register modifications of the project to manage autosave
                if (sender == theProject) modifiedSinceLastAutosave = true;
            };
        }
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // this is for recording the session with 1280x720 pixel. 
            this.Size = new Size(1294, 727);

        }
        /// <summary>
        /// Give the user a chance to save the modified project
        /// </summary>
        /// <param name="e"></param>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!CadFrame.Project.SaveModified()) e.Cancel = true;
            base.OnFormClosing(e);
        }
        public override bool OnCommand(string MenuId)
        {
            // forward to modellingPropertyEntries first
            if (modellingPropertyEntries.OnCommand(MenuId)) return true;
            if (MenuId == "MenuId.App.Exit")
            {   // this command cannot be handled by CADability.dll
                Application.Exit();
                return true;
            }
#if DEBUG
            else if (MenuId == "MenuId.Debug")
            {
                Debug();
                return true;
            }
#endif
            else return base.OnCommand(MenuId);
        }
        public override bool OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            // forward to modellingPropertyEntries first
            if (modellingPropertyEntries.OnUpdateCommand(MenuId, CommandState)) return true;
            return base.OnUpdateCommand(MenuId, CommandState);
        }
        public override void OnSelected(MenuWithHandler selectedMenuItem, bool selected)
        {
            modellingPropertyEntries.OnSelected(selectedMenuItem, selected);
            base.OnSelected(selectedMenuItem, selected);
        }
#if DEBUG
        private static Random rnd = new Random();
        private GeoVector RandomVector(double len)
        {
            double fx = 1.0, fy = 1.0, fz = 1.0;
            if (rnd.NextDouble() < 0.5) fx = -fx;
            if (rnd.NextDouble() < 0.5) fy = -fy;
            if (rnd.NextDouble() < 0.5) fz = -fz;
            return len * (new GeoVector(fx * rnd.NextDouble(), fy * rnd.NextDouble(), fz * rnd.NextDouble())).Normalized;
        }
        private double RandomDouble(double min, double max)
        {
            return min + (max - min) * rnd.NextDouble();
        }
        /// <summary>
        /// Here we can add some debug code
        /// </summary>
        private void DebugX()
        {
            Model model = CadFrame.Project.GetActiveModel();
            Style stl = CadFrame.Project.StyleList.GetDefault(CADability.Attribute.Style.EDefaultFor.Solids);
            for (int i = 0; i < 64; i++)
            {
                double fx = 1.0, fy = 1.0, fz = 1.0;
                if ((i & 0x1) != 0) fx = -fx;
                if ((i & 0x2) != 0) fy = -fy;
                if ((i & 0x4) != 0) fz = -fz;
                GeoVector dir = RandomVector(RandomDouble(40, 60));
                GeoPoint center = GeoPoint.Origin + dir;
                Solid sld = Make3D.MakeSphere(center, 10 + 20 * rnd.NextDouble());
                ModOp rotate = ModOp.Rotate(center, RandomVector(1.0), SweepAngle.Deg(rnd.NextDouble() * 360.0));
                sld.Modify(rotate);
                sld.Style = stl;
                model.Add(sld);
            }
            Solid mainSphere = Make3D.MakeSphere(GeoPoint.Origin, 50);
            mainSphere.Style = stl;
            model.Add(mainSphere);
        }


        private void DebugY()
        {
            List<CADability.GeoObject.Solid> slds = new List<CADability.GeoObject.Solid>();
            CADability.GeoObject.Solid sldbig = null;
            Face cone = null;
            Face upper = null;
            Face lower = null;
            foreach (CADability.GeoObject.IGeoObject go in CadFrame.Project.GetActiveModel().AllObjects)
            {
                if (go is CADability.GeoObject.Solid sld)
                {
                    if (sld.Volume(0.1) > 500000) sldbig = sld;
                    else slds.Add(sld);
                }
            }

            var rng = new Random(71);
            var order = Enumerable.Range(0, slds.Count).ToArray();

            // Fisher–Yates
            for (int i = slds.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }


            if (slds.Count > 1 && sldbig != null)
            {

                for (int i = 0; i < slds.Count; i++)
                {
                    Solid[] res = NewBooleanOperation.Subtract(sldbig, slds[order[i]]);
                    if (res != null && res.Length >= 1) sldbig = res[0];
                    else { }
                    //System.Diagnostics.Debug.WriteLine("Unite " + i.ToString() + " -> " + sldbig.GetExtent(0.0).Size.ToString("F0"));
                }
            }
        }
        private void Debug()
        {
            List<CADability.GeoObject.Solid> slds = new List<CADability.GeoObject.Solid>();
            Solid operand1 = null, operand2 = null;
            List<Solid> difference = new List<Solid>();
            List<Solid> intersection = new List<Solid>();
            List<Solid> union = new List<Solid>();
            List<ICurve> edgeMarkers = new List<ICurve>();
            foreach (CADability.GeoObject.IGeoObject go in CadFrame.Project.GetActiveModel().AllObjects)
            {
                if (go is CADability.GeoObject.Solid sld)
                {
                    slds.Add(sld);
                    if (sld.Style != null)
                    {
                        if (sld.Style.Name == "Operand1") operand1 = sld;
                        else if (sld.Style.Name == "Operand2") operand2 = sld;
                        else if (sld.Style.Name == "Difference") difference.Add(sld);
                        else if (sld.Style.Name == "Union") union.Add(sld);
                        else if (sld.Style.Name == "Intersection") intersection.Add(sld);
                    }
                }
                if (go is ICurve curve)
                {
                    if (go.Style.Name == "EdgeMarker")
                    {
                        edgeMarkers.Add(curve);
                    }
                }
            }
            if (operand1 != null && operand2 != null)
            {
                if (difference.Count > 0)
                {
                    Solid[] sres = NewBooleanOperation.Subtract(operand1, operand2);
                    if (sres.Length > 0)
                    {
                        Project proj = Project.CreateSimpleProject();
                        proj.GetActiveModel().Add(sres);
                        proj.WriteToFile("c:\\Temp\\subtract.cdb.json");
                    }
                }
            }
            if (slds.Count == 2)
            {
                //Solid un = NewBooleanOperation.Unite(slds[0], slds[1]);
                Solid[] sld;
                if (slds[0].Volume(0.1) > slds[1].Volume(0.1))
                {
                    sld = NewBooleanOperation.Subtract(slds[0], slds[1]);
                }
                else
                {
                    sld = NewBooleanOperation.Subtract(slds[1], slds[0]);
                }
                //if (sld.Length > 0)
                //{
                //    Project proj = Project.CreateSimpleProject();
                //    proj.GetActiveModel().Add(sld);
                //    proj.WriteToFile("c:\\Temp\\subtract.cdb.json");
                //}
            }
            if (edgeMarkers.Count > 0)
            {
                foreach (Solid s in slds)
                {
                    foreach (Edge edg in s.Shells[0].Edges)
                    {
                        foreach (ICurve em in edgeMarkers)
                        {
                            if (edg.Curve3D.SameGeometry(em, 0.1))
                            {
                                Shell rounded = s.Shells[0].RoundEdges(new Edge[] { edg }, 2);
                            }
                        }
                    }
                }
            }
            //if (slds.Count == 1)
            //{
            //    Shell shell = slds[0].Shells[0];
            //    foreach (var vtx in shell.Vertices)
            //    {
            //        if ((vtx.Position | new GeoPoint(58, 12, 17)) < 3)
            //        {
            //            Shell rounded = shell.RoundEdges(vtx.Edges, 2);
            //        }
            //    }
            //}
        }
#endif
    }
}
