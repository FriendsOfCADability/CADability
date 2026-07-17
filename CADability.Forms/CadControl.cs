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
    /// <summary>
    /// This class is an UserControl which allow us to use the cad in a custom form.
    /// The idea is not to force the user to use a form derived from CadForm.
    /// If the CreateMainMenu property is true, it creates the MainMenu in the parent form.
    /// The ProgressAction property allow us to use a custom progress ui.
    /// </summary>
    public partial class CadControl : UserControl, ICommandHandler
    {
        #region PRIVATE FIELDS

        private CadFrame cadFrame; // the frame, which knows about the views, the ControlCenter (PropertiesExplorer), the menu

        #endregion PRIVATE FIELDS

        /// <summary>
        /// Default constructor
        /// </summary>
        public CadControl()
        {
            InitializeComponent(); // makes the cadCanvas and the propertiesExplorer

            cadFrame = new CadFrame(propertiesExplorer, cadCanvas, this);
            cadCanvas.Frame = cadFrame;
            propertiesExplorer.Frame = cadFrame;

            // We cannot complete the control setup at this moment because the ParentForm is null
            // We must use the OnHandleCreated method
        }

        #region PUBLIC PROPERTIES

        public PropertiesExplorer PropertiesExplorer => propertiesExplorer;
        public CadCanvas CadCanvas => cadCanvas;
        public CadFrame CadFrame => cadFrame;

        /// <summary>
        /// Action that delegate the progress.
        /// In this way we can use a custom progress ui.
        /// </summary>
        public Action<bool, double, string> ProgressAction
        {
            get { return cadFrame.ProgressAction; }
            set { cadFrame.ProgressAction = value; }
        }

        /// <summary>
        /// Indicates to create the menu on the parent form
        /// </summary>
        public bool CreateMainMenu { get; set; }

        /// <summary>
        /// Get or set a value indicating whether the ProperiesExplorer is displayed
        /// </summary>
        public bool PropertiesExplorerVisible
        {
            get { return propertiesExplorer.Visible; }
            set
            {
                propertiesExplorer.Visible = value;
                splitContainer.Panel2Collapsed = !value;
                this.Invalidate();
            }
        }

        /// <summary>
        /// Get or set a value indicating whether the toolbars are displayed
        /// </summary>
        public bool ToolbarsVisible
        {
            get { return toolStripContainer.TopToolStripPanelVisible; }
            set
            {
                // Show or hide all the side panels
                toolStripContainer.TopToolStripPanelVisible = value;
                toolStripContainer.LeftToolStripPanelVisible = value;
                toolStripContainer.RightToolStripPanelVisible = value;
                toolStripContainer.BottomToolStripPanelVisible = value;
                this.Invalidate();
            }
        }

        /// <summary>
        /// Func that allow to listen (and eventually handle) the commands
        /// </summary>
        public Func<string, bool> CommandRequest;

        #endregion PUBLIC PROPERTIES

        #region PUBLIC METHODS

        public virtual bool OnCommand(string MenuId)
        {
            bool handled = false;
            if (this.CommandRequest != null)
                handled = this.CommandRequest(MenuId);

            return handled;
        }

        public virtual bool OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            return false;
        }

        public virtual void OnSelected(MenuWithHandler selectedMenuItem, bool selected)
        {

        }

        #endregion PUBLIC METHODS

        #region PRIVATE/PROTECTED METHODS

        private void OnIdle(object sender, EventArgs e)
        {
            ToolBars.UpdateCommandState(toolStripContainer.TopToolStripPanel);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            setupParentForm();

            // open an existing Project or create a new one
            ToolBars.CreateOrRestoreToolbars(toolStripContainer, cadFrame);
            Application.Idle += new EventHandler(OnIdle); // update the toolbars (menus are updated when they popup)

            base.OnHandleCreated(e);
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
            if (preProcess)
            {
                Substitutes.KeyEventArgs e = new Substitutes.KeyEventArgs((Substitutes.Keys)keyData);
                e.Handled = false;
                cadFrame.PreProcessKeyDown(e);
                if (e.Handled) return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void setupParentForm()
        {
            Form parentForm = this.FindForm();
            if (parentForm != null)
            {
                if (this.CreateMainMenu)
                {
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
                    MenuStrip menuStrip = MenuManager.MakeMainMenu(mainMenu);
                    parentForm.Controls.Add(menuStrip);
                    parentForm.MainMenuStrip = menuStrip;
                    cadFrame.FormMenu = menuStrip;
                }
                parentForm.KeyPreview = true; // used to filter the escape key (and maybe some more?)                
                parentForm.FormClosed += ParentForm_FormClosed;
            }
        }

        private void ParentForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            // maybe we need to save the project
            ToolBars.SaveToolbarPositions(toolStripContainer);
            Settings.SaveGlobalSettings();
            // ToolStripManager.SaveSettings(this); // save the positions of the toolbars (doesn't work correctly)
            cadFrame.Dispose();
            Application.Idle -= OnIdle;
        }
        #endregion PRIVATE/PROTECTED METHODS

    }
}
