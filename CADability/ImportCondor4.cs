using CADability.Attribute;
using CADability.Curve2D;
using CADability.GeoObject;
using CADability.Shapes;
using CADability.UserInterface;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;

namespace CADability
{


    /// <summary>
    /// Exception of the import of Condor 4 files (the predecessor of CADability). The import itself is no longer part of the
    /// library; only this exception class remains and it is not thrown anywhere.
    /// </summary>
    public class ImportCondor4Exception : ApplicationException
    {
        public ImportCondor4Exception(string msg)
            : base(msg)
        {
        }
    }
}
