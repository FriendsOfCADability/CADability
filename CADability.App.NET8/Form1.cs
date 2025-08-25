using CADability;
using CADability.Forms.NET8;

namespace CADability.App.NET8
{
    public partial class MainForm : CadForm
    {
        public MainForm(string[] args):base(args)
        {   // interpret the command line arguments as a name of a file, which should be opened
            string fileName = "";
            for (int i = 0; i < args.Length; i++)
            {
                if (!args[i].StartsWith("-"))
                {
                    fileName = args[i];
                    break;
                }
            }
            Project? toOpen = null;
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
        }

        public override bool OnCommand(string MenuId)
        {
            if (MenuId == "MenuId.App.Exit")
            {   // this command cannot be handled by CADability.dll
#if DEBUG
                System.GC.Collect();
                System.GC.WaitForFullGCComplete();
                System.GC.Collect();
#endif
                Application.Exit();
                return true;
            }
            else return base.OnCommand(MenuId);
        }
    }
}
