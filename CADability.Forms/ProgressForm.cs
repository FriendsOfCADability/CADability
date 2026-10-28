using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CADability.Forms
{
    public class ProgressForm : Form
    {
        private ProgressBar progressBar;
        private Label label;
        public ProgressForm()
        {
            progressBar = new ProgressBar();
            this.Controls.Add(progressBar);
            progressBar.Minimum = 0;
            progressBar.Maximum = 1000;
            label = new Label();
            this.Controls.Add(label);
        }
        private void Init(string title)
        {
            this.ControlBox = false;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            Rectangle bnd = RectangleToScreen(Owner.Bounds);
            int width = bnd.Width / 3;
            int height = 5 * FontHeight;
            Rectangle dbounds = new Rectangle(bnd.Left + width, bnd.Top + bnd.Height / 2, width, height);
            this.DesktopBounds = RectangleToClient(dbounds);
            System.Drawing.Rectangle clr = this.ClientRectangle;
            int sep = 2 * clr.Height / 3; 
            progressBar.Location = new System.Drawing.Point(FontHeight, sep + (clr.Height - sep - FontHeight) / 2);
            progressBar.Width = clr.Width - 2 * FontHeight;
            progressBar.Height = FontHeight;
            label.Location = new Point(0, 0);
            label.Width = clr.Width;
            label.Height = sep;
            label.Text = title;
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.AutoEllipsis = true;
        }
        public void ShowProgressBar(bool show, double percent, string title)
        {
            if (Owner == null || Owner.IsDisposed) return; // has been killed
            if (Owner.InvokeRequired)
            {
                Owner.BeginInvoke(new Action<bool, double, string>(ShowProgressBar), show, percent, title);
            }
            else
            {
                if (show && !Visible)
                {
                    // show it
                    Init(title);
                    this.Show();
                    Cursor.Current = Cursors.WaitCursor;
                }
                else if (!show && Visible)
                {
                    this.Hide();
                    Cursor.Current = Cursors.Default;
                }
                if (show)
                {
                    if (title != null)
                    {
                        label.Text = title;
                        Update();
                    }
                    int val = Math.Max(0, Math.Min(1000, (int)(percent * 10))); // minimum, maximum = 0, 1000
                    if (val != progressBar.Value)
                    {
                        progressBar.Value = val;
                        Update();
                    }
                }
            }
        }
    }
}
