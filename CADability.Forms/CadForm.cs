using CADability.UserInterface;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using System.Text;
using System.Windows.Forms;


namespace CADability.Forms
{
    public partial class CadForm : Form, ICommandHandler
    {
        private CadFrame cadFrame; // the frame, which knows about the views, the ControlCenter (PropertiesExplorer), the menu
        private ProgressForm progressForm;
        private MenuStrip mainMenuStrip;
        public CadForm(string[] args)
        {
            InitializeComponent(); // makes the cadCanvas and the propertiesExplorer
            KeyPreview = true; // used to filter the escape key (and maybe some more?)
            cadFrame = new CadFrame(propertiesExplorer, cadCanvas, this);
            cadFrame.ProgressAction = (show, percent, title) => { this.ProgressForm.ShowProgressBar(show, percent, title); };
            cadCanvas.Frame = cadFrame;
            propertiesExplorer.Frame = cadFrame;
            // show this menu in the MainForm
            MenuWithHandler[] mainMenu = MenuResource.LoadMenuDefinition("SDI Menu", true, cadFrame);
            #region DebuggerPlayground, you can remove this region
            // in the following lines a "DebuggerPlayground" object is created via reflection. This class is a playground to write testcode
            // which is not included in the sources. This is why it is constructed via reflection, there is no need to have this class in the project.
            Type dbgplygnd = Type.GetType("CADability.Forms.DebuggerPlayground", false);
            if (dbgplygnd != null)
            {
                MethodInfo connect = dbgplygnd.GetMethod("Connect");
                if (connect != null) mainMenu = connect.Invoke(null, new object[] { cadFrame, mainMenu }) as MenuWithHandler[];
            }
            #endregion DebuggerPlayground
            SetMainMenu(MenuManager.MakeMainMenu(mainMenu));
            cadFrame.FormMenu = mainMenuStrip;
            // open an existing Project or create a new one
            ToolBars.CreateOrRestoreToolbars(topToolStripContainer, cadFrame);
            Application.Idle += new EventHandler(OnIdle); // update the toolbars (menus are updated when they popup)
        }
        /// <summary>
        /// Resets the main menu of the form. After MenuResource.SetMenuResource has been loaded, the mainmenu in CADability 
        /// is changed to the new resource. In order to set this menu in the form, call this method.
        /// </summary>
        /// <param name="debuggerPlaygroundClass">if not null, specifies an additional menue class, which will be created by reflection</param>
        public void ResetMainMenu(string debuggerPlaygroundClass)
        {
            MenuWithHandler[] mainMenu = MenuResource.LoadMenuDefinition("SDI Menu", true, cadFrame);
            if (!string.IsNullOrEmpty(debuggerPlaygroundClass))
            {
                // in the following lines a "DebuggerPlayground" object is created via reflection. This class is a playground to write testcode
                // which is not included in the sources. This is why it is constructed via reflection, there is no need to have this class in the project.
                Type dbgplygnd = Type.GetType(debuggerPlaygroundClass, false);
                if (dbgplygnd != null)
                {
                    MethodInfo connect = dbgplygnd.GetMethod("Connect");
                    if (connect != null) mainMenu = connect.Invoke(null, new object[] { cadFrame, mainMenu }) as MenuWithHandler[];
                }
            }
            SetMainMenu(MenuManager.MakeMainMenu(mainMenu));
            cadFrame.FormMenu = mainMenuStrip;
        }
        private void SetMainMenu(MenuStrip newMenu)
        {
            if (mainMenuStrip != null)
            {
                topToolStripContainer.TopToolStripPanel.Controls.Remove(mainMenuStrip);
                mainMenuStrip.Dispose();
            }
            mainMenuStrip = newMenu;
            topToolStripContainer.TopToolStripPanel.Controls.Add(mainMenuStrip);
            MainMenuStrip = mainMenuStrip;
        }

        // Access the components of the MainForm from the CadFrame.
        public ProgressForm ProgressForm
        {
            get
            {
                if (progressForm == null)
                {
                    progressForm = new ProgressForm
                    {
                        TopLevel = true,
                        Owner = this,
                        Visible = false
                    };
                }
                return progressForm;
            }
        }
        public PropertiesExplorer PropertiesExplorer => propertiesExplorer;
        public CadCanvas CadCanvas => cadCanvas;
        public CadFrame CadFrame => cadFrame;
        private void OnIdle(object sender, EventArgs e)
        {
            ToolBars.UpdateCommandState(topToolStripContainer.TopToolStripPanel);
        }
        protected override void OnClosing(CancelEventArgs e)
        {   // maybe we need to save the project
            ToolBars.SaveToolbarPositions(topToolStripContainer);
            Settings.SaveGlobalSettings();
            // ToolStripManager.SaveSettings(this); // save the positions of the toolbars (doesn't work correctly)
            base.OnClosing(e);
        }
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // Unsubscribe from the static event first so OnIdle cannot fire after fields
            // are nulled below (topToolStripContainer would throw NullReferenceException).
            Application.Idle -= OnIdle;
            // Only dispose non-WinForms objects here. WinForms-managed controls
            // (cadCanvas, propertiesExplorer, topToolStripContainer, splitContainer,
            // mainMenuStrip) are in the Controls hierarchy and are disposed automatically
            // when the Form itself is disposed after this method returns.
            // Never call this.Dispose() inside OnFormClosed — the form is still in its
            // closing sequence and WinForms will call Dispose() once teardown is complete.
            cadFrame?.Dispose();
            progressForm?.Dispose();

            mainMenuStrip = null;
            cadFrame = null;
            cadCanvas = null;
            propertiesExplorer = null;
            topToolStripContainer = null;
            splitContainer = null;
            progressForm = null;

            base.OnFormClosed(e);
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys nmKeyData = (Keys)((int)keyData & 0x0FFFF);
            bool preProcess = nmKeyData >= Keys.F1 && nmKeyData <= Keys.F24;
            preProcess = preProcess || (nmKeyData == Keys.Escape);
            preProcess = preProcess || (nmKeyData == Keys.Up) || (nmKeyData == Keys.Down);
            preProcess = preProcess || (nmKeyData == Keys.Tab) || (nmKeyData == Keys.Enter);
            preProcess = preProcess || keyData.HasFlag(Keys.Control) || keyData.HasFlag(Keys.Alt); // menu shortcut
            if (propertiesExplorer.EntryWithTextBox == null) preProcess |= (nmKeyData == Keys.Delete); // the delete key is preferred by the textbox, if there is one 
            Substitutes.KeyEventArgs e = new Substitutes.KeyEventArgs((Substitutes.Keys)keyData);
            if (preProcess)
            {
                e.Handled = false;
                cadFrame.PreProcessKeyDown(e);
                if (e.Handled) return true;
            }
            CadFrame.PreProcessKeyDown(e);
            if (e.Handled) return true;
            //if (msg.Msg== 0x0101) // WM_KEYUP 
            //{ }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        public virtual bool OnCommand(string MenuId)
        {
            return false;
        }

        public virtual bool OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            return false;
        }

        public virtual void OnSelected(MenuWithHandler selectedMenuItem, bool selected)
        {

        }

    }
}
