using System;
using System.Collections.Generic;
using System.Text;

namespace CADability.UserInterface
{
    /// <summary>
    /// Keeps the list of most recently used files in the global settings (key "MRUFileList"). The
    /// <see cref="FrameImpl"/> adds the path of a project when it is opened, imported or saved and fills the
    /// "recent files" menu from <see cref="GetMRUFiles"/>. Each entry has the form "path;filetype"; the
    /// oldest entry is dropped when the list exceeds ten entries.
    /// </summary>

    public class MRUFiles
    {
        public MRUFiles()
        {
            // 
            // TODO: Add constructor logic here
            //
        }
        public static void AddPath(string path, string filetype)
        {
            try
            {
                if (path == null) return;
                path = path.Replace('\\', '*'); // because '\' followed by an 'n' makes a problem (which I don't understand) '*' is not allowed in a filename
                string mru = Settings.GlobalSettings.GetStringValue("MRUFileList", "");
                string[] lastFiles = mru.Split('|');
                List<string> entries = new List<string>();
                for (int i = 0; i < lastFiles.Length; ++i)
                {
                    if (!lastFiles[i].StartsWith(path) && lastFiles[i].Length > 0) entries.Add(lastFiles[i]);
                }
                if (entries.Count > 10) entries.RemoveAt(0);
                entries.Add(path + ";" + filetype);
                StringBuilder b = new StringBuilder();
                for (int i = 0; i < entries.Count; ++i)
                {
                    b.Append(entries[i] as string);
                    if (i < entries.Count - 1) b.Append("|"); // '|' is split character, previously used \n, which made problems when a filename starts with 'n' and the char before is '\'
                }
                mru = b.ToString();
                Settings.GlobalSettings.SetValue("MRUFileList", mru);
            }
            catch { }
        }
        public static string[] GetMRUFiles()
        {

            string mru = Settings.GlobalSettings.GetStringValue("MRUFileList", "");
            string[] res = mru.Split('|');
            for (int i = 0; i < res.Length; i++)
            {
                res[i] = res[i].Replace('*', '\\'); 
            }
            return res;
        }
    }
}
