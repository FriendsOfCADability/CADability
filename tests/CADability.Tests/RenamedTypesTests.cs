using System.Runtime.Serialization;

namespace CADability.Tests
{
    /// <summary>
    /// A project file records the type of every object it holds by name, so a class that is serialized cannot
    /// simply be renamed - every file written before the rename would stop being readable. <see
    /// cref="RenamedTypes"/> is what buys that freedom back, and this is the proof that it does: an object is
    /// written, its type name in the text is put back to what it used to be - which is exactly what an old
    /// file contains - and it has to come back as the class of today. Ported from ShapeIt, with the rename of
    /// <see cref="BoundingBox"/> (formerly BoundingCube) as the case under test.
    /// </summary>
    [TestClass]
    public class RenamedTypesTests
    {
        private const string OldName = "CADability.BoundingCube";
        private const string NewName = "CADability.BoundingBox";

        /// <summary>
        /// The binder the reader of the binary format uses. It is private to <see cref="Project"/>, so it is found by name.
        /// </summary>
        private static SerializationBinder ProjectBinder()
        {
            Type binderType = typeof(Project).Assembly.GetTypes().Single(t => t.Name == "CondorSerializationBinder");
            return (SerializationBinder)Activator.CreateInstance(binderType, true);
        }

        [TestMethod]
        public void a_binary_file_that_still_calls_it_bounding_cube_gets_a_bounding_box()
        {
            // A binary file names the type of every serialized field, so a .cdb with a model in it contains
            // "CADability.BoundingCube" for Model.MinExtend - this is what the reader asks the binder for.
            string assembly = typeof(BoundingBox).Assembly.FullName;
            Assert.AreEqual(typeof(BoundingBox), ProjectBinder().BindToType(assembly, OldName));
            Assert.AreEqual(typeof(BoundingBox), ProjectBinder().BindToType(assembly, NewName));
        }

        [TestMethod]
        public void the_json_format_writes_a_bounding_box_without_its_name()
        {
            // The json format writes a BoundingBox as an array of six numbers, with no type name in it, which
            // is why the rename does not affect json files at all. Should that ever change, the table entry
            // covers it, see resolve_maps_a_renamed_name_and_leaves_everything_else_alone.
            Model model = new Model();
            model.MinExtend = new BoundingBox(-1, 2, -3, 4, -5, 6);
            string written = JsonSerialize.ToString(model);
            Assert.IsFalse(written.Contains(OldName));
            Assert.IsFalse(written.Contains(NewName));
            Model read = JsonSerialize.FromString(written) as Model;
            Assert.IsNotNull(read);
            BoundingBox ext = read.MinExtend;
            Assert.AreEqual(-1.0, ext.Xmin); Assert.AreEqual(2.0, ext.Xmax);
            Assert.AreEqual(-3.0, ext.Ymin); Assert.AreEqual(4.0, ext.Ymax);
            Assert.AreEqual(-5.0, ext.Zmin); Assert.AreEqual(6.0, ext.Zmax);
        }

        [TestMethod]
        public void resolve_maps_a_renamed_name_and_leaves_everything_else_alone()
        {
            Assert.AreEqual(NewName, RenamedTypes.Resolve(OldName));
            Assert.AreEqual(NewName, RenamedTypes.Resolve(NewName), "resolving twice must not change anything");
            Assert.AreEqual("CADability.GeoObject.Ellipse", RenamedTypes.Resolve("CADability.GeoObject.Ellipse"));
            Assert.AreEqual("", RenamedTypes.Resolve(""));
            Assert.IsNull(RenamedTypes.Resolve(null));
        }

        [TestMethod]
        public void resolve_carries_an_array_suffix_over()
        {
            // One entry has to cover arrays of the class as well, those appear in files as their own $Type.
            Assert.AreEqual(NewName + "[]", RenamedTypes.Resolve(OldName + "[]"));
            Assert.AreEqual(NewName + "[][]", RenamedTypes.Resolve(OldName + "[][]"));
            Assert.AreEqual(NewName + "[,]", RenamedTypes.Resolve(OldName + "[,]"));
        }

        [TestMethod]
        public void a_name_may_not_be_mapped_onto_two_different_classes()
        {
            // A chain of renames has to be entered by CHANGING the existing entry, not by adding a second one,
            // because the lookup is a single step. Adding a contradicting entry is therefore refused.
            Assert.ThrowsException<ArgumentException>(() => RenamedTypes.Add(OldName, "CADability.GeoObject.Ellipse"));
            // ...while repeating what is already there is harmless
            RenamedTypes.Add(OldName, NewName);
            Assert.AreEqual(NewName, RenamedTypes.Resolve(OldName));
        }
    }
}
