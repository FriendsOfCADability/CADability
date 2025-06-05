using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Action = CADability.Actions.Action;

namespace CADability.Forms
{
    /* TODO: change IFrame for "KernelOnly" KO
     * Step by Step implement missing methods (copy implementation from SingleDocumentFrame) 
     * and remove interface methods from IFrame with "#if KO" ...
     */
    /// <summary>
    /// Implementation of the abstract FrameImpl, doing things, you cannot do in .NET Core
    /// </summary>
    public class CadFrame : FrameImpl, IUIService
    {
        #region PRIVATE FIELDS

        private ICommandHandler commandHandler;

        #endregion PRIVATE FIELDS

        #region PUBLIC PROPERTIES

        /// <summary>
        /// The parent form menu
        /// </summary>
        public MenuStrip FormMenu { set; private get; }

        /// <summary>
        /// Action that delegate the progress.
        /// In this way we can use a custom progress ui.
        /// </summary>
        public Action<bool, double, string> ProgressAction { get; set; }

        #endregion PUBLIC PROPERTIES

        /// <summary>
        /// Constructor without form dependency.
        /// </summary>
        /// <param name="propertiesExplorer"></param>
        /// <param name="cadCanvas"></param>
        /// <param name="commandHandler"></param>
        public CadFrame(PropertiesExplorer propertiesExplorer, CadCanvas cadCanvas, ICommandHandler commandHandler) 
            : base(propertiesExplorer, cadCanvas)
        {
            this.commandHandler = commandHandler;
        }
        /// <summary>
        /// Constructor without form dependency.
        /// </summary>        
        /// <param name="cadCanvas"></param>
        /// <param name="commandHandler"></param>
        public CadFrame(CadCanvas cadCanvas, ICommandHandler commandHandler)
            : base(cadCanvas)
        {
            this.commandHandler = commandHandler;
        }

        #region FrameImpl override

        public override bool OnCommand(string MenuId)
        {
            if (commandHandler != null && commandHandler.OnCommand(MenuId)) return true;
            return base.OnCommand(MenuId);
        }

        public override bool OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            if (commandHandler != null && commandHandler.OnUpdateCommand(MenuId, CommandState)) return true;
            return base.OnUpdateCommand(MenuId, CommandState);
        }

        private void UpdateMRUMenu(MenuItemWithHandler mi, string[] mruFiles)
        {
            if (mi.DropDownItems.Count > 0)
            {
				foreach (ToolStripItem tsi in mi.DropDownItems)
				{
					if (tsi is MenuItemWithHandler mmi)
					{
						UpdateMRUMenu(mmi, mruFiles);
					}
				}
			}
            else
            {
                MenuWithHandler mwh = mi.Tag as MenuWithHandler;
                if (mwh != null)
                {
                    string MenuId = mwh.ID;
                    if (MenuId.StartsWith("MenuId.File.Mru.File"))
                    {
                        string filenr = MenuId.Substring("MenuId.File.Mru.File".Length);
                        try
                        {
                            int n = int.Parse(filenr);
                            if (n <= mruFiles.Length && n > 0)
                            {
                                string[] parts = mruFiles[mruFiles.Length - n].Split(';');
                                if (parts.Length > 1)
                                    mi.Text = parts[0];
                            }
                        }
                        catch (FormatException) { }
                        catch (OverflowException) { }
                    }
                }
            }
        }

        public override void UpdateMRUMenu(string[] mruFiles)
        {
			if (this.FormMenu != null)
			{
				foreach (MenuItemWithHandler mi in this.FormMenu.Items)
				{
					UpdateMRUMenu(mi, mruFiles);
				}
			}
        }
        #endregion FrameImpl override

        #region IUIService implementation
        public override IUIService UIService => this;
        GeoObjectList IUIService.GetDataPresent(object data)
        {
            if (data is IDataObject idata)
            {
                if (idata.GetDataPresent(System.Windows.Forms.DataFormats.Serializable))
                {
                    return idata.GetData(System.Windows.Forms.DataFormats.Serializable) as GeoObjectList;
                }
            }
            return null;
        }
        Substitutes.Keys IUIService.ModifierKeys => (Substitutes.Keys)Control.ModifierKeys;
        System.Drawing.Point IUIService.CurrentMousePosition => System.Windows.Forms.Control.MousePosition;
        private static Dictionary<string, string> directories = new Dictionary<string, string>();
        Substitutes.DialogResult IUIService.ShowOpenFileDlg(string id, string title, string filter, ref int filterIndex, out string fileName)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = filter;
            openFileDialog.FilterIndex = filterIndex;
            if (!string.IsNullOrWhiteSpace(title)) openFileDialog.Title = title;
            if (!string.IsNullOrWhiteSpace(id) && directories.TryGetValue(id, out string directory))
            {
                openFileDialog.InitialDirectory = directory;
            }
            else
            {
                openFileDialog.RestoreDirectory = true;
            }

            Substitutes.DialogResult res = (Substitutes.DialogResult)openFileDialog.ShowDialog(Application.OpenForms[0]);
            if (res == Substitutes.DialogResult.OK)
            {
                filterIndex = openFileDialog.FilterIndex;
                fileName = openFileDialog.FileName;
                if (!string.IsNullOrWhiteSpace(id))
                {
                    directory = System.IO.Path.GetDirectoryName(fileName);
                    directories[id] = directory;
                }
            }
            else
            {
                fileName = null;
            }
            return res;
        }
        Substitutes.DialogResult IUIService.ShowSaveFileDlg(string id, string title, string filter, ref int filterIndex, ref string fileName)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = filter;
            saveFileDialog.FilterIndex = filterIndex;
            if (!string.IsNullOrWhiteSpace(title)) saveFileDialog.Title = title;
            if (!string.IsNullOrWhiteSpace(id) && directories.TryGetValue(id, out string directory))
            {
                saveFileDialog.InitialDirectory = directory;
            }
            else
            {
                saveFileDialog.RestoreDirectory = true;
            }

            Substitutes.DialogResult res = (Substitutes.DialogResult)saveFileDialog.ShowDialog(Application.OpenForms[0]);
            if (res == Substitutes.DialogResult.OK)
            {
                filterIndex = saveFileDialog.FilterIndex;
                fileName = saveFileDialog.FileName;
                if (!string.IsNullOrWhiteSpace(id))
                {
                    directory = System.IO.Path.GetDirectoryName(fileName);
                    directories[id] = directory;
                }
            }
            return res;
        }
        Substitutes.DialogResult IUIService.ShowMessageBox(string text, string caption, Substitutes.MessageBoxButtons buttons)
        {
            return (Substitutes.DialogResult)MessageBox.Show(Application.OpenForms[0], text, caption, (System.Windows.Forms.MessageBoxButtons)buttons);
        }
        Substitutes.DialogResult IUIService.ShowColorDialog(ref System.Drawing.Color color)
        {
            ColorDialog colorDialog = new ColorDialog();
            colorDialog.Color = color;
            Substitutes.DialogResult dlgres = (Substitutes.DialogResult)colorDialog.ShowDialog(Application.OpenForms[0]);
            color = colorDialog.Color;
            return dlgres;
        }
        void IUIService.ShowProgressBar(bool show, double percent, string title)
        {
            this.ProgressAction?.Invoke(show, percent, title);
        }
        /// <summary>
        /// Returns a bitmap from the specified embeded resource. the name is in the form filename:index
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        Bitmap IUIService.GetBitmap(string name)
        {
            string[] parts = name.Split(':');
            if (parts.Length == 2)
            {
                ImageList il = BitmapTable.GetImageList(parts[0], 15, 15);
                if (il != null)
                {
                    try
                    {
                        int ind = int.Parse(parts[1]);
                        return il.Images[ind] as Bitmap;
                    }
                    catch (FormatException) { }
                    catch (OverflowException) { }
                    catch (ArgumentOutOfRangeException) { }
                }
            }
            return null;
        }
        IPaintTo3D IUIService.CreatePaintInterface(Bitmap paintToBitmap, double precision)
        {
            PaintToOpenGL paintTo3D = new PaintToOpenGL(precision);
            paintTo3D.Init(paintToBitmap);
            return paintTo3D;
        }
        Substitutes.DialogResult IUIService.ShowPageSetupDlg(ref PrintDocument printDocument, PageSettings pageSettings, out int width, out int height, out bool landscape)
        {
            PageSetupDialog psd = new PageSetupDialog();
            psd.AllowPrinter = true;
            psd.EnableMetric = true;
            psd.Document = printDocument;
            Substitutes.DialogResult res = (Substitutes.DialogResult)(int)psd.ShowDialog();
            if (res == Substitutes.DialogResult.OK)
            {
                psd.Document.OriginAtMargins = false;
                printDocument = psd.Document;
                width = psd.PageSettings.PaperSize.Width;
                height = psd.PageSettings.PaperSize.Height;
                landscape = psd.PageSettings.Landscape;
            }
            else
            {
                width = height = 0;
                landscape = false;
            }
            return res;
        }
        Substitutes.DialogResult IUIService.ShowPrintDlg(ref PrintDocument printDocument)
        {
            PrintDialog printDialog = new PrintDialog();
            printDialog.Document = printDocument;
            printDialog.AllowSomePages = false;
            printDialog.AllowCurrentPage = false;
            printDialog.AllowSelection = false;
            printDialog.AllowPrintToFile = false;
            Substitutes.DialogResult res = (Substitutes.DialogResult)printDialog.ShowDialog();
            if (res == Substitutes.DialogResult.OK)
            {
                printDocument = printDialog.Document;
            }
            return res;
        }
        void IUIService.SetClipboardData(GeoObjectList objects, bool copy)
        {
            Clipboard.SetDataObject(objects, copy);
        }
        object IUIService.GetClipboardData(Type typeOfdata)
        {
            IDataObject data = Clipboard.GetDataObject();
            return data.GetData(typeOfdata);
        }
        bool IUIService.HasClipboardData(Type typeOfdata)
        {
            IDataObject data = Clipboard.GetDataObject();
            return data.GetDataPresent(typeOfdata);
            //for (int i = 0; i < formats.Length; i++)
            //{
            //    if (formats[i] == typeOfdata.FullName) return true;
            //}
            //return false;
        }
        event EventHandler IUIService.ApplicationIdle
        {
            add
            {
                Application.Idle += value;
            }

            remove
            {
                Application.Idle -= value;
            }
        }
        #endregion
    }
}
