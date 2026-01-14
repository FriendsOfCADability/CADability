using System;
using System.Collections.Generic;

namespace CADability
{
    internal class OpenGlList : IPaintTo3DList
    {
        private int listNumber; // nur lesen
        static List<int> toDelete = new List<int>();
        public OpenGlList(int listNr)
        {
            listNumber = listNr;
        }
        public bool hasContents, isDeleted;
        public OpenGlList()
        {
            FreeLists();
            listNumber = Gl.glGenLists(1); // genau eine Liste
            // System.Diagnostics.Trace.WriteLine("Creating OpenGl List Nr.: " + listNumber.ToString());
            // Gl.glIsList()
        }
        ~OpenGlList()
        {
            lock (toDelete)
            {
                if (!isDeleted) toDelete.Add(listNumber);
            }
        }
        static public void FreeLists()
        {
            // CRITICAL: Must lock BEFORE checking Count to avoid race condition
            // Thread A could read Count > 0, then Thread B clears the list, then A proceeds with stale data
            lock (toDelete)
            {
                if (toDelete.Count > 0)
                {
                    for (int i = 0; i < toDelete.Count; ++i)
                    {
                        // System.Diagnostics.Trace.WriteLine("Deleting OpenGl List Nr.: " + toDelete[i].ToString());
                        try
                        {
                            Gl.glDeleteLists(toDelete[i], 1);
                        }
                        catch (Exception e)
                        {
                            if (e is System.Threading.ThreadAbortException) throw (e);
                        }
                    }
                    toDelete.Clear();
                }
            }
        }
        public int ListNumber
        {
            get
            {
                return listNumber;
            }
        }
        public void SetHasContents()
        {
            hasContents = true;
        }
        public bool HasContents()
        {
            return hasContents;
        }
        private class OpenList : IDisposable
        {
            public OpenList(int toOpen)
            {
                Gl.glNewList(toOpen, Gl.GL_COMPILE);
            }

            #region IDisposable Members

            void IDisposable.Dispose()
            {
                Gl.glEndList();
            }

            #endregion
        }

        public IDisposable List()
        {
            return new OpenList(listNumber);
        }
        public void Open()
        {
            Gl.glNewList(listNumber, Gl.GL_COMPILE);
        }
        public void Close()
        {
            Gl.glEndList();
        }
        public void Delete()
        {
            // System.Diagnostics.Trace.WriteLine("Direct Deleting OpenGl List Nr.: " + listNumber.ToString());
            // CRITICAL: Double-checked locking to prevent multiple deletions
            if (isDeleted) return;
            
            lock (toDelete)
            {
                // Re-check after acquiring lock
                if (isDeleted) return;
                isDeleted = true;
            }
            
            try
            {
                Gl.glDeleteLists(listNumber, 1);
            }
            catch (Exception e)
            {
                if (e is System.Threading.ThreadAbortException) throw;
            }
        }
        #region IPaintTo3DList Members
        private string name;
        string IPaintTo3DList.Name
        {
            get
            {
                return name;
            }
            set
            {
                name = value;
            }
        }
        private List<IPaintTo3DList> keepAlive;
        List<IPaintTo3DList> IPaintTo3DList.containedSubLists
        {
            // das Problem mit den SubLists ist so:
            // Es werden meherere OpenGlList objekte generiert (z.B. Block)
            // dann werden diese Listen durch "glCallList" in eine zusammengeführt. Aber gl
            // merkt sich nur die Nummern. deshalb müssen diese Listen am Leben bleiben
            // und dürfen nicht freigegeben werden. Hier ist der Platz sie zu erhalten.
            set
            {
                keepAlive = value;
            }
        }
        #endregion

        internal static OpenGlList[] CreateMany(int num)
        {
            int start = Gl.glGenLists(num);
            OpenGlList[] res = new OpenGlList[num];
            for (int i = 0; i < num; ++i)
            {
                res[i] = new OpenGlList(start + i);
            }
            return res;
        }
    }
}
