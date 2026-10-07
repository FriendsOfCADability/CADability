using CADability.GeoObject;
using System.Collections.Generic;

namespace CADability
{
    /* ÜBERLEGUNGEN:
     * 1. startCube kann mann sich sparen, stattdessen den Anfang des voxel-strings mit bytes mit jeweils einer 1 verwenden
     * 2. in voxels kann man die "0" weglassen, wenn man die Tiefe kannt.
     * 3. in voxels könnte man um Überspringen eines Astes die Länge dieses Asten speichern, allerdings wohl in 2 byte (oder in einem byte, und 0 bedeutet: Länge unbekannt,
     * dann muss man halt den Abstige machen)
     * 
     * Es scheint mir ohnehin besser, einen "UnitOctTree" zu verwenden, dessen Anfangs-Cube -2^n .. 2^n ist: damit könnte man:
     * 1. zwei OctTrees synchron absteigen um Schnittkandidaten zu finden.
     * 2. Objekte könnten statt viele HitTests zu machen einen voxel-string liefern (mit einer vorgegebenen Tiefe), z.B. Kurven durch Schnitte mit parallelen zu den Standardebenen,
     * Faces mit den Schnitten zu achsenparalellen Linien, so ein Schnitt liefert gleich 4 betroffene cubes
     * 
     */
    /// <summary>
    /// Describes a 3d object (curve or face) in a voxel representation at a dynamic resolution.
    /// </summary>
    public class VoxelTree
    {
        short baseCube; // 0 is the unit cube from -1 to +1, n: the cube from -2^n to +2^n (in x, y an z)
        byte[] startCube; // the subcubes are numbered 0..7, this is a descenting path to the subcube, which contains the whole object
        byte[] voxels; // each bit in this byte is 1, if the corresponding subcube (voxel) belongs to the object and 0 otherwise
        // for each bit which is 1, there is a byte following with the voxel description. If a byte is 0, this means the voxel is no further devided and it belongs to the object
        class VoxelIterator
        {
            byte[] voxels;
            int index;
            BoundingBox current;
            public VoxelIterator(VoxelTree vt) //byte[] voxels, int index, BoundingBox start)
            {
                voxels = vt.voxels;
                index = 0;
                double size;
                current = new BoundingBox();
                if (vt.baseCube >= 0) size = 1 << vt.baseCube;
                else size = 1.0 / (1 << -vt.baseCube);
                current.Set(-size, size, -size, size, -size, size); // this BoundingBox contains the whole object
                for (int i = 0; i < vt.startCube.Length; i++)
                {
                    GeoPoint m = current.GetCenter();
                    switch (vt.startCube[i])
                    {
                        case 0: current = new BoundingBox(m.x, current.Xmax, m.y, current.Ymax, m.z, current.Zmax); break;
                        case 1: current = new BoundingBox(current.Xmin, m.x, m.y, current.Ymax, m.z, current.Zmax); break;
                        case 2: current = new BoundingBox(m.x, current.Xmax, current.Ymin, m.y, m.z, current.Zmax); break;
                        case 3: current = new BoundingBox(current.Xmin, m.x, current.Ymin, m.y, m.z, current.Zmax); break;
                        case 4: current = new BoundingBox(m.x, current.Xmax, m.y, current.Ymax, current.Zmin, m.z); break;
                        case 5: current = new BoundingBox(current.Xmin, m.x, m.y, current.Ymax, current.Zmin, m.z); break;
                        case 6: current = new BoundingBox(m.x, current.Xmax, current.Ymin, m.y, current.Zmin, m.z); break;
                        case 7: current = new BoundingBox(current.Xmin, m.x, current.Ymin, m.y, current.Zmin, m.z); break;
                    }
                }

            }
            public IEnumerable<BoundingBox> AllBoundingCubes
            {
                get
                {
                    byte pattern = voxels[index];
                    if (voxels[index] == 0)
                    {
                        ++index;
                        yield return current;
                    }
                    else
                    {
                        BoundingBox start = current;
                        BoundingBox[] sc = VoxelTree.subCubes(current);
                        for (int i = 0; i < 8; i++)
                        {
                            if ((pattern & 1 << i) != 0)
                            {
                                ++index;
                                current = sc[i];
                                foreach (BoundingBox sb in AllBoundingCubes)
                                {
                                    yield return sb;
                                }
                            }
                        }
                        current = start;
                    }
                }
            }
        }
        /// <summary>
        /// Creates the voxel representation of the provided <paramref name="obj"/> (only GetExtent and HitTest are beeing used)
        /// </summary>
        /// <param name="obj">the object for which the voxel represenation is to be created</param>
        /// <param name="precision">the precision, size of the smallest voxel</param>
        public VoxelTree(IOctTreeInsertable obj, double precision)
        {
            BoundingBox ext = obj.GetExtent(precision);
            BoundingBox unit = new BoundingBox(-1, 1, -1, 1, -1, 1);
            baseCube = 1;
            double size = 1;
            while (unit.Contains(ext))
            {
                size /= 2;
                baseCube--;
                unit.Set(-size, size, -size, size, -size, size);
            }
            if (baseCube == 1)
            {   // ext is not contained in -1..1
                size = 2;
                unit.Set(-2, 2, -2, 2, -2, 2);
                while (!unit.Contains(ext))
                {
                    size *= 2;
                    baseCube++;
                    unit.Set(-size, size, -size, size, -size, size);
                }
            }
            if (baseCube >= 0) size = 1 << baseCube;
            else size = 1.0 / (1 << -baseCube);
            unit.Set(-size, size, -size, size, -size, size); // this BoundingBox contains the whole object
            List<byte> startCubesList = new List<byte>();
            do
            {
                byte found = 255;
                BoundingBox[] sc = subCubes(unit);
                for (byte i = 0; i < 8; i++)
                {
                    if (sc[i].Contains(ext))
                    {
                        if (found == 255) found = i;
                        else
                        {   // two different cubes contain ext, so we must stop here
                            found = 255;
                            break;
                        }
                    }
                }
                if (found != 255)
                {
                    startCubesList.Add(found);
                    unit = sc[found];
                }
                else break;
            } while (true);
            startCube = startCubesList.ToArray();
            List<byte> voxelList = new List<byte>();
            Add(voxelList, unit, obj, precision);
            voxels = voxelList.ToArray();
        }

        static BoundingBox[] subCubes(BoundingBox start)
        {
            GeoPoint m = start.GetCenter();
            BoundingBox[] res = new BoundingBox[8];
            res[0] = new BoundingBox(m.x, start.Xmax, m.y, start.Ymax, m.z, start.Zmax);
            res[1] = new BoundingBox(start.Xmin, m.x, m.y, start.Ymax, m.z, start.Zmax);
            res[2] = new BoundingBox(m.x, start.Xmax, start.Ymin, m.y, m.z, start.Zmax);
            res[3] = new BoundingBox(start.Xmin, m.x, start.Ymin, m.y, m.z, start.Zmax);
            res[4] = new BoundingBox(m.x, start.Xmax, m.y, start.Ymax, start.Zmin, m.z);
            res[5] = new BoundingBox(start.Xmin, m.x, m.y, start.Ymax, start.Zmin, m.z);
            res[6] = new BoundingBox(m.x, start.Xmax, start.Ymin, m.y, start.Zmin, m.z);
            res[7] = new BoundingBox(start.Xmin, m.x, start.Ymin, m.y, start.Zmin, m.z);
            return res;
        }
        private void Add(List<byte> voxelList, BoundingBox test, IOctTreeInsertable obj, double precision)
        {
            int ind = voxelList.Count;
            voxelList.Add(0); // 0 means: this is a final voxel, belonging to the object (we know, test interferes with the object)
            if (test.XDiff > precision)
            {
                BoundingBox[] sc = subCubes(test);
                for (int i = 0; i < 8; i++)
                {
                    if (obj.HitTest(ref sc[i], precision))
                    {
                        voxelList[ind] |= (byte)(1 << i); // this adds a subvoxel to the list, it is no more final
                        Add(voxelList, sc[i], obj, precision);
                    }
                }
            }
        }
        internal IEnumerable<BoundingBox> AllBoundingCubes
        {
            get
            {
                VoxelIterator vi = new VoxelIterator(this);
                return vi.AllBoundingCubes;
            }
        }
        public GeoObjectList Debug
        {
            get
            {
                GeoObjectList res = new GeoObjectList();
                foreach (BoundingBox boundingCube in AllBoundingCubes)
                {
                    res.Add(boundingCube.AsBox);
                }
                return res;
            }
        }
    }
}
