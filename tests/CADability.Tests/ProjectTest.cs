using System.Diagnostics;
using System.Drawing;
using CADability.Forms;
using System.IO.Compression;
using CADability.Attribute;
using CADability.Shapes;

namespace CADability.Tests
{
    [TestClass]
    public class ProjectTest
    {

        public TestContext TestContext { get; set; }

        [TestMethod]
        [DeploymentItem(@"Files/Dxf/square_100x100.dxf", nameof(import_dxf_square_succeds))]
        [DeploymentItem(@"Files/Dxf/square_100x100.png", nameof(import_dxf_square_succeds))]
        public void import_dxf_square_succeds()
        {
            var file = Path.Combine(this.TestContext.DeploymentDirectory, this.TestContext.TestName, "square_100x100.dxf");
            Assert.IsTrue(File.Exists(file));
            var bmpFile = Path.Combine(this.TestContext.DeploymentDirectory, this.TestContext.TestName, "square_100x100.png");
            Assert.IsTrue(File.Exists(bmpFile));

            var project = Project.ReadFromFile(file, "dxf");
            Assert.IsNotNull(project);
            var model = project.GetActiveModel();
            Assert.IsNotNull(model);
            var obj = Assert.That.Single(model.AllObjects);
            var polyline = Assert.That.IsInstanceOfType<GeoObject.Polyline>(obj);
            Assert.AreEqual(400, polyline.Length);


            using (var expected = (Bitmap)Image.FromFile(bmpFile))
            using (var actual = PaintToSilkGL.PaintToBitmap(model.AllObjects, GeoVector.ZAxis, 100, 100))
            {
                Assert.That.BitmapsAreEqual(expected, actual);
            }
        }
        [TestMethod]
        [DeploymentItem(@"Files/Step/issue101.stp", nameof(import_step_issue101_succeds))]
        [DeploymentItem(@"Files/Step/issue101.png", nameof(import_step_issue101_succeds))]
        public void import_step_issue101_succeds()
        {
            // cylinder.OutwardOriented throws an NotImplementedException
            // because there is a concret implementation
            //   public bool OutwardOriented => toCylinder.Determinant > 0;
            // and an explicit interface implementation
            //   bool ICylinder.OutwardOriented => throw new NotImplementedException();

            var file = Path.Combine(this.TestContext.DeploymentDirectory, this.TestContext.TestName, "issue101.stp");
            Assert.IsTrue(File.Exists(file));
            var bmpFile = Path.Combine(this.TestContext.DeploymentDirectory, this.TestContext.TestName, "issue101.png");
            Assert.IsTrue(File.Exists(bmpFile));

            var project = Project.ReadFromFile(file, "stp");
            Assert.IsNotNull(project);
            var model = project.GetActiveModel();
            Assert.IsNotNull(model);
            Assert.AreEqual(1, model.AllObjects.Count);

            var solid = Assert.That.IsInstanceOfType<GeoObject.Solid>(model.AllObjects[0]);
            var shell = solid.Shells[0];
            // get a cylinder (order faces and cylinders to always get the same entity)
            var cylinder = shell
                .Faces
                .OrderBy(x => x.Area)
                .Select(x => x.Surface)
                .OfType<GeoObject.ICylinder>()
                .OrderBy(x => x.Radius)
                .FirstOrDefault();
            Assert.IsTrue(cylinder.OutwardOriented);

            using (var expected = (Bitmap)Image.FromFile(bmpFile))
            using (var actual = PaintToSilkGL.PaintToBitmap(model.AllObjects, GeoVector.NullVector, 200, 200))
            {
                Assert.That.BitmapsAreEqual(expected, actual);
            }
        }

        [TestMethod]
        public void export_dxf_issue_129_succeds()
        {
            // in master@82ffb34 exporting a text object to txt does not set the location,
            // so all texts are all not in the correct location / orientation

            // create a simple project and add a text object
            var project = Project.CreateSimpleProject();
            var model = project.GetActiveModel();
            var expected = GeoObject.Text.Construct();
            expected.Font = "Arial";
            expected.TextString = "Test";
            expected.Location = new GeoPoint(50, 50);
            model.Add(expected);

            // export the project and load it again
            var fileName = this.TestContext.TestName + ".dxf";
            project.Export(fileName, "dxf");
            project = Project.ReadFromFile(fileName, "dxf");
            model = project.GetActiveModel();
            var actual = model.AllObjects.Cast<GeoObject.Text>().Single();

            // verify some values
            Assert.AreEqual(expected.Location, actual.Location);
            Assert.AreEqual(expected.TextString, actual.TextString);
            Assert.AreEqual(expected.Font, actual.Font);

        }

        [TestMethod]
        [DeploymentItem(@"Files/Dxf/issue143.dxf", nameof(import_dxf_issue143_succeds))]
        [DeploymentItem(@"Files/Dxf/issue143.png", nameof(import_dxf_issue143_succeds))]
        public void import_dxf_issue143_succeds()
        {
            var file = Path.Combine(this.TestContext.DeploymentDirectory, this.TestContext.TestName, "issue143.dxf");
            Assert.IsTrue(File.Exists(file));
            var bmpFile = Path.Combine(this.TestContext.DeploymentDirectory, this.TestContext.TestName, "issue143.png");
            // uncomment after file exists
            Assert.IsTrue(File.Exists(bmpFile));

            var project = Project.ReadFromFile(file, "dxf");
            Assert.IsNotNull(project);
            var model = project.GetActiveModel();
            Assert.IsNotNull(model);
            Assert.AreEqual(3, model.AllObjects.Count);

            var ellipse1 = Assert.That.IsInstanceOfType<GeoObject.Ellipse>(model.AllObjects[1]);
            Assert.IsTrue(ellipse1.HasValidData());
            var ellipse2 = Assert.That.IsInstanceOfType<GeoObject.Ellipse>(model.AllObjects[2]);
            Assert.IsTrue(ellipse2.HasValidData());

            using (var expected = (Bitmap)Image.FromFile(bmpFile))
            using (var actual = PaintToSilkGL.PaintToBitmap(model.AllObjects, GeoVector.NullVector, 200, 200))
            {
                // Uncomment once to generate bitmap for later comparison
                //actual.Save(bmpFile);
                Assert.That.BitmapsAreEqual(expected, actual);
            }
        }

        [TestMethod]
        [DeploymentItem(@"Files/Dxf/issue171.dxf", nameof(import_dxf_issue171_succeds))]
        public void import_dxf_issue171_succeds()
        {
            // DXF file with Codepage DOS850

            var file = Path.Combine(this.TestContext.DeploymentDirectory, this.TestContext.TestName, "issue171.dxf");
            Assert.IsTrue(File.Exists(file));

            var project = Project.ReadFromFile(file, "dxf");
            Assert.IsNotNull(project);
        }

        [TestMethod]
        [DeploymentItem(@"Files/Step/issue153.stp", nameof(import_step_issue153_succeeds))]
        public void import_step_issue153_succeeds()
        {
            //The file issue153.stp is using Metres as unit. But it's imported as mm in CADability.

            var file = Path.Combine(this.TestContext.DeploymentDirectory, this.TestContext.TestName, "issue153.stp");
            Assert.IsTrue(File.Exists(file));

            var project = Project.ReadFromFile(file, "stp");
            Assert.IsNotNull(project);

            var allObjects = project.GetActiveModel().AllObjects;
            var solid = (CADability.GeoObject.Solid)allObjects[0];
            var vol = solid.Volume(0);

            //If this file is imported as mm instead of m the volume will be around 0.00055309963466116513
            //The real volumen should be around 553099.66263763292
            double rightVolume = 553099.66263763292;
            Debug.Assert((Math.Abs(vol - rightVolume) < Precision.eps));
        }
    }
}
