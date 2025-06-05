using CADability.UserInterface;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace CADability.Forms
{
    /* Alte und neue Klassen:
     * altes CADability: ShowProperty : IPropertyTreeView entspricht PropertyPage: IPropertyPage, es ist ein Tab im ControlCenter
     * altes CADability: ControlCenter entspricht PropertiesExplorer : IControlCenter 
     * 
     * ShowProperty: public void AddShowProperty(IShowProperty ToAdd, bool TopMost) ist die Stelle, an der die Anzeige der Properties zusammengesetzt wird.
     * Das also muss ersetzt werden
     * 
     * Klick auf "TreeViewButton" geht an IPropertyPage
    */
    public class PropertyPage : ScrollableControl, IPropertyPage
    {
        List<IPropertyEntry> rootProperties; // a list of all root entries (not including the subentries of opened entries)
        public string TitleId { get; }
        public int IconId { get; }
        IPropertyEntry[] entries; // all currently shown entries 
        bool[] labelNeedsExtension; // true for all indices, where entries[index].Label doesnt fit into the available box
        private int lineHeight, fontHeight, square, buttonWidth, middle;
        private StringFormat stringFormat;
        private int selected; // the index of the currently selected item
        private ToolTip toolTip;
        private string currentToolTip;
        private bool dragMiddlePosition = false;  // true, when the user moves the middle position (between label and value) with the pressed mouse button
        private Timer delay;
        private PropertiesExplorer propertiesExplorer;
        private Brush _backColorBrush, _foreColorBrush, _indentBrush;
        private Color _indentColor, _separatorColor;
        private Pen _foreColorPen, _separatorPen;
        private Dictionary<char, string> keyboardModifier;
        public event PreProcessKeyDown OnPreProcessKeyDown;
        public event SelectionChanged OnSelectionChanged;

        public PropertyPage(string titleId, int iconId, PropertiesExplorer propExplorer)
        {
            TitleId = titleId;
            IconId = iconId;

            AutoScroll = true;
            BackColor = SystemColors.Window;
            _indentColor = SystemColors.ControlLight;
            _separatorColor = SystemColors.ControlLight;
            //Font = new Font(Font.FontFamily, 20);
            stringFormat = new StringFormat(StringFormat.GenericDefault);
            stringFormat.FormatFlags = StringFormatFlags.NoWrap;
            stringFormat.LineAlignment = StringAlignment.Center;
            stringFormat.Trimming = StringTrimming.EllipsisCharacter;
            rootProperties = new List<IPropertyEntry>();
            selected = -1;
            // avoid flickering:
            SetStyle(ControlStyles.DoubleBuffer | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
            UpdateStyles();
            toolTip = new ToolTip();
            toolTip.InitialDelay = 500;
            propertiesExplorer = propExplorer;
            string[] keyboardModifierStrings = StringTable.GetSplittedStrings("Shortcuts.Modifier");
            if (keyboardModifierStrings.Length < 3) keyboardModifierStrings = new string[] { "Ctrl", "Alt", "Shift" };
            keyboardModifier = new Dictionary<char, string>();
            keyboardModifier['c'] = keyboardModifierStrings[0];
            keyboardModifier['a'] = keyboardModifierStrings[1];
            keyboardModifier['s'] = keyboardModifierStrings[2];
        }
        public Color IndentColor
        {
            get { return _indentColor; }
            set
            {
                if (_indentBrush != null)
                {
                    _indentBrush.Dispose();
                    _indentBrush = null;
                }
                _indentColor = value;
            }
        }
        public Color SeparatorColor
        {
            get { return _separatorColor; }
            set
            {
                if (_separatorPen != null)
                {
                    _separatorPen.Dispose();
                    _separatorPen = null;
                }
                _separatorColor = value;
            }
        }
        private Brush BackColorBrush
        {
            get
            {
                if (_backColorBrush == null)
                    _backColorBrush = new SolidBrush(BackColor);
                return _backColorBrush;
            }
        }
        private Brush ForeColorBrush
        {
            get
            {
                if (_foreColorBrush == null)
                    _foreColorBrush = new SolidBrush(ForeColor);
                return _foreColorBrush;
            }
        }
        private Brush IndentBrush
        {
            get
            {
                if (_indentBrush == null)
                    _indentBrush = new SolidBrush(_indentColor);
                return _indentBrush;
            }
        }
        private Pen ForeColorPen
        {
            get
            {
                if (_foreColorPen == null)
                    _foreColorPen = new Pen(ForeColor);
                return _foreColorPen;
            }
        }
        private Pen SeparatorPen
        {
            get
            {
                if (_separatorPen == null)
                {
                    _separatorPen = new Pen(_separatorColor);
                }
                return _separatorPen;

            }
        }
        private Rectangle ItemArea(int index)
        {
            return new Rectangle(0, index * lineHeight + AutoScrollPosition.Y, ClientSize.Width, lineHeight);
        }
        private Rectangle ValueArea(int index)
        {
            if (entries[index].Value != null)
            {
                int right = ClientSize.Width;
                if (entries[index].Flags.HasFlag(PropertyEntryType.ContextMenu) || entries[index].Flags.HasFlag(PropertyEntryType.DropDown)) right -= buttonWidth;
                if (entries[index].Flags.HasFlag(PropertyEntryType.Lockable)) right -= ButtonImages.ButtonImageList.ImageSize.Width;
                return new Rectangle(middle, index * lineHeight + AutoScrollPosition.Y, right - middle, lineHeight);
            }
            return Rectangle.Empty;
        }
        private Rectangle LabelArea(int index)
        {
            Rectangle area = new Rectangle(0, index * lineHeight + AutoScrollPosition.Y, ClientSize.Width, lineHeight);
            int left = 1;
            left = entries[index].IndentLevel * buttonWidth + buttonWidth + 1; // left side of the label text
            Rectangle labelRect;
            if (entries[index].Value != null) labelRect = new Rectangle(left, area.Top, middle - left, area.Height);
            else labelRect = new Rectangle(left, area.Top, area.Right - left, area.Height);
            return labelRect;
        }
        private Rectangle IndentedItemArea(int index)
        {
            Rectangle area = new Rectangle(0, index * lineHeight + AutoScrollPosition.Y, ClientSize.Width, lineHeight);
            int left = entries[index].IndentLevel * buttonWidth + buttonWidth + 1; // left side of the label text
            return new Rectangle(left, area.Top, area.Right - left, area.Height);
        }
        private GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {   // by ChatGPT
            int diameter = radius * 2;
            var path = new GraphicsPath();

            // Wenn Radius = 0, einfaches Rechteck
            if (radius == 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            Rectangle arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

            // Top-Left
            path.AddArc(arc, 180, 90);

            // Top-Right
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);

            // Bottom-Right
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            // Bottom-Left
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }
        private void DrawShortcut(Graphics g, Rectangle area, Font font, string modifier, string key)
        {   // by ChatGPT
            const int keycapPadding = 0;
            const int spacing = 0;
            int cornerRadius = area.Height / 4;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Teile Modifier auf
            var parts = string.IsNullOrWhiteSpace(modifier)
                ? new List<string>()
                : modifier.Split('+').Select(m => m.Trim()).ToList();

            parts.Add(key);

            // Textgrößen berechnen
            var sizes = parts.Select(p =>
                TextRenderer.MeasureText(p, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding)
            ).ToList();

            int totalWidth = spacing * (parts.Count - 1) +
                             sizes.Sum(sz => sz.Width + keycapPadding * 2);

            // Rechtsbündiger Startpunkt
            int x = area.Right - totalWidth;
            int y = area.Y + (area.Height - font.Height) / 2;

            for (int i = 0; i < parts.Count; i++)
            {
                string text = parts[i];
                Size sz = sizes[i];

                Rectangle keyRect = new Rectangle(
                    x,
                    y,
                    sz.Width + keycapPadding * 2,
                    sz.Height + 2
                );

                using (GraphicsPath path = RoundedRect(keyRect, cornerRadius))
                {
                    using (Brush fill = new SolidBrush(Color.White))
                        g.FillPath(fill, path);

                    using (Pen border = new Pen(SystemColors.ControlDark))
                        g.DrawPath(border, path);

                    TextRenderer.DrawText(
                        g,
                        text,
                        font,
                        keyRect,
                        SystemColors.ControlText,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding
                    );
                }

                x += keyRect.Width + spacing;
            }
        }
        /// <summary>
        /// Splits the label text into the visible text portion and the shortcut parts.
        /// The shortcut syntax is expected to appear at the end of the label in the format [[<modifier1><modifier2><key>]].
        /// Each modifier is optional and must be one of 's', 'c' or 'a' (for Shift, Ctrl, Alt respectively).
        /// The key should be an uppercase letter. If any component is missing, '\0' is returned for it.
        /// </summary>
        /// <param name="labelText">The full label text, e.g. "LabelText [[scX]]"</param>
        /// <returns>
        /// A tuple: (text, firstModifier, secondModifier, key)
        /// where text is the label text without the shortcut portion,
        /// and missing modifier(s) and key are represented as '\0'.
        /// </returns>
        private (string text, char firstModifier, char secondModifier, string key) SplitLabelText(string labelText)
        {
            // Regex pattern:
            // ^(.*?)                       -> Capture group 1: Alles bis zum Shortcut-Teil (non-greedy)
            // (?:\s*\[\[                   -> Non-capturing: optionales Leerzeichen + literal "[["
            //   (?:(?<modifier1>[sca])?    -> Optionaler erster Modifier (s, c oder a)
            //     (?<modifier2>[sca])?     -> Optionaler zweiter Modifier
            //     (?<key>                  -> Capture group "key":
            //         Esc|Del              ->   "Esc" oder "Del"
            //         |F\d{1,2}            ->   Funktionstasten F1–F99 (praktisch F1–F12)
            //         |[A-Z]               ->   einzelner Großbuchstabe
            //     )
            //   )
            // \]\])\s*$                    -> literal "]]" + optionales Leerzeichen bis zum String-Ende
            var regex = new Regex(
                @"^(.*?)(?:\s*\[\[(?:(?<modifier1>[sca])?(?<modifier2>[sca])?(?<key>Esc|Del|F\d{1,2}|[A-Z]))\]\])\s*$"
            );

            char firstModifier = '\0';
            char secondModifier = '\0';
            string key = string.Empty;

            var match = regex.Match(labelText);
            if (match.Success)
            {
                // Textteil ohne Shortcut
                string textPart = match.Groups[1].Value.TrimEnd();

                // Falls vorhanden, Modifier auslesen
                if (match.Groups["modifier1"].Success && match.Groups["modifier1"].Value.Length > 0)
                    firstModifier = match.Groups["modifier1"].Value[0];
                if (match.Groups["modifier2"].Success && match.Groups["modifier2"].Value.Length > 0)
                    secondModifier = match.Groups["modifier2"].Value[0];

                // Key auslesen (kann nun z.B. "Esc" oder "F5" sein)
                if (match.Groups["key"].Success && match.Groups["key"].Value.Length > 0)
                    key = match.Groups["key"].Value;

                return (textPart, firstModifier, secondModifier, key);
            }
            else
            {
                // Kein Shortcut gefunden: Rückgabe mit leerem key
                return (labelText, '\0', '\0', string.Empty);
            }
        }
        private void PaintItem(int index, Graphics graphics)
        {
            int left = 1;
            // draw the tree lines
            bool lastLine = index == entries.Length - 1;
            if (!lastLine) lastLine = entries[index + 1].IndentLevel < entries[index].IndentLevel;
            bool firstLine = index == 0;
            if (!firstLine) firstLine = entries[index - 1].IndentLevel == 0;
            Rectangle area = new Rectangle(0, index * lineHeight + AutoScrollPosition.Y, ClientSize.Width, lineHeight);
            graphics.FillRectangle(BackColorBrush, area);
            graphics.DrawRectangle(SeparatorPen, area);
            Rectangle indent = area;
            indent.Width = entries[index].IndentLevel * buttonWidth;
            graphics.FillRectangle(IndentBrush, indent);
            left = entries[index].IndentLevel * buttonWidth + buttonWidth + 1; // left side of the label text
            if (entries[index].Flags.HasFlag(PropertyEntryType.HasSubEntries))
            {   // draw a square with a "+" or "-" sign in front of the label
                int ym = (area.Top + area.Bottom) / 2;
                int xm = entries[index].IndentLevel * buttonWidth + buttonWidth / 2 + 1;
                int s2 = square / 2;
                int s3 = square / 3;
                graphics.DrawRectangle(ForeColorPen, xm - s2, ym - s2, square, square);
                graphics.DrawLine(ForeColorPen, xm - s3, ym, xm + s3, ym); // the horizontal "minus" line
                if (!entries[index].IsOpen) graphics.DrawLine(ForeColorPen, xm, ym - s3, xm, ym + s3); // the vertical "plus" line
            }
            bool showValue = (entries[index].Value != null) && !entries[index].Flags.HasFlag(PropertyEntryType.Checkable);
            if (showValue) graphics.DrawLine(SeparatorPen, middle, area.Top, middle, area.Bottom); // the vertical divider line
            Rectangle labelRect;
            if (showValue) labelRect = new Rectangle(left, area.Top, middle - left, area.Height);
            else labelRect = new Rectangle(left, area.Top, area.Right - left, area.Height);
            string labelText = entries[index].Label;
            char firstModifier = '\0';
            char secondModifier = '\0';
            string shortCutKey = String.Empty;
            if (entries[index].Flags.HasFlag(PropertyEntryType.Shortcut))
                (labelText, firstModifier, secondModifier, shortCutKey) = SplitLabelText(labelText);
            if (graphics.MeasureString(labelText, Font).Width > labelRect.Width) labelNeedsExtension[index] = true;
            if (index == selected)
            {
                graphics.FillRectangle(SystemBrushes.Highlight, labelRect);
                if (entries[index].Flags.HasFlag(PropertyEntryType.Bold))
                    graphics.DrawString(labelText, new Font(Font, FontStyle.Bold), SystemBrushes.HighlightText, labelRect, stringFormat);
                else
                    graphics.DrawString(labelText, Font, SystemBrushes.HighlightText, labelRect, stringFormat);
            }
            else if (entries[index].Flags.HasFlag(PropertyEntryType.Seperator))
            {
                int ym = (labelRect.Top + labelRect.Bottom) / 2;
                labelRect.Width -= buttonWidth;
                graphics.DrawLine(SeparatorPen, labelRect.Left, ym, labelRect.Right, ym); // the horizontal separator line
                StringFormat seperatorFormat = stringFormat.Clone() as StringFormat;
                seperatorFormat.Alignment = StringAlignment.Center;
                graphics.DrawString(labelText, Font, ForeColorBrush, labelRect, seperatorFormat);
            }
            else
            {
                if (entries[index].Flags.HasFlag(PropertyEntryType.Highlight))
                    graphics.DrawString(labelText, Font, new SolidBrush(Color.Red), labelRect, stringFormat);
                else if (entries[index].Flags.HasFlag(PropertyEntryType.Bold))
                    graphics.DrawString(labelText, new Font(Font, FontStyle.Bold), ForeColorBrush, labelRect, stringFormat);
                else
                    graphics.DrawString(labelText, Font, ForeColorBrush, labelRect, stringFormat);
            }
            // shortcut
            if (entries[index].Flags.HasFlag(PropertyEntryType.Shortcut) && shortCutKey != String.Empty)
            {
                Font smallerFont = new Font(Font.FontFamily, Font.Size * 0.9f, Font.Style);
                Size scsz = graphics.MeasureString("X", smallerFont).ToSize(); // height of shortcut text
                Rectangle scArea = new Rectangle(labelRect.Right - buttonWidth - 2, labelRect.Top + (labelRect.Height - scsz.Height) / 2 - 2, 1, scsz.Height); // width doesn't matter
                // U+2303 (Ctrl), U+2325 (Alt), U+21E7 (Shift)
                StringBuilder shortcutModifier = new StringBuilder();
                if (firstModifier != '\0') shortcutModifier.Append(keyboardModifier[firstModifier] + "+");
                if (secondModifier != '\0') shortcutModifier.Append(keyboardModifier[secondModifier] + "+");
                if (shortcutModifier.ToString().EndsWith("+")) shortcutModifier.Remove(shortcutModifier.Length - 1, 1);
                DrawShortcut(graphics, scArea, smallerFont, shortcutModifier.ToString(), shortCutKey);
            }
            if (showValue)
            {
                int right = area.Right;
                if (entries[index].Flags.HasFlag(PropertyEntryType.ContextMenu) || entries[index].Flags.HasFlag(PropertyEntryType.DropDown)) right -= buttonWidth;
                Rectangle valueRect = new Rectangle(middle, area.Top, right - middle, area.Height);
                DrawString(graphics, entries[index].Value, Font, ForeColorBrush, valueRect, false);
            }
            if (entries[index].Flags.HasFlag(PropertyEntryType.ContextMenu))
            {   // draw three vertical dots (maybe we could also use the Unicode "⁞" character
                Rectangle buttonRect = new Rectangle(area.Right - buttonWidth, area.Top, buttonWidth, area.Height);
                ControlPaint.DrawButton(graphics, buttonRect, ButtonState.Flat);
                int d = Math.Max(area.Height / 9, 2);
                int dy = (area.Height - 5 * d) / 2;
                int xm = (int)(area.Right - buttonWidth / 2.0);
                for (int i = 0; i < 3; i++)
                {
                    graphics.FillRectangle(Brushes.Black, xm - d / 2, buttonRect.Top + dy, d, d);
                    dy += 2 * d;
                }
                if (entries[index].Flags.HasFlag(PropertyEntryType.Lockable))
                {   // test the lock/unlock
                    buttonRect = new Rectangle(area.Right - buttonWidth - ButtonImages.ButtonImageList.ImageSize.Width, area.Top, ButtonImages.ButtonImageList.ImageSize.Width, area.Height); // square rect at the right end
                    ControlPaint.DrawButton(graphics, buttonRect, ButtonState.Flat);
                    dy = (area.Height - ButtonImages.ButtonImageList.ImageSize.Height) / 2;
                    if (entries[index].IsLocked) ButtonImages.ButtonImageList.Draw(graphics, buttonRect.Left, buttonRect.Top + dy, 154); // the "locked" symbol
                    else ButtonImages.ButtonImageList.Draw(graphics, buttonRect.Left, buttonRect.Top + dy, 155); // the "unlocked" symbol
                }
            }
            else if (entries[index].Flags.HasFlag(PropertyEntryType.DropDown))
            {   // draw a combo button
                Rectangle buttonRect = new Rectangle(area.Right - buttonWidth, area.Top, buttonWidth, area.Height);
                ControlPaint.DrawComboButton(graphics, buttonRect, ButtonState.Flat);
                //int numLines = (int)(0.25 * buttonWidth);
                //int xm = (int)(area.Right - buttonWidth / 2.0);
                //int ym = (int)(area.Top + 0.4 * area.Height);
                //graphics.DrawLine(ForeColorPen, xm - numLines, ym - 3, xm + numLines, ym - 3); // the horizontal above line
                //for (int i = 0; i <= numLines; i++)
                //{
                //    int w2 = numLines - i;
                //    if (w2 == 0) graphics.FillRectangle(ForeColorBrush, xm, ym + i, 1, 1); // a single pixel
                //    else graphics.DrawLine(ForeColorPen, xm - w2, ym + i, xm + w2, ym + i); // the horizontal "minus" line
                //}
            }
            else if (entries[index].Flags.HasFlag(PropertyEntryType.DirectMenu))
            {   // draw a combo button
                Rectangle buttonRect = new Rectangle(area.Right - buttonWidth, area.Top, buttonWidth, area.Height);
                ControlPaint.DrawScrollButton(graphics, buttonRect, ScrollButton.Right, ButtonState.Flat);
            }
            else if (entries[index].Flags.HasFlag(PropertyEntryType.CancelButton) || entries[index].Flags.HasFlag(PropertyEntryType.OKButton))
            {
                Font fontForSymbols = Font; // new Font(FontFamily.GenericSansSerif, Font.Size, FontStyle.Bold);
                StringFormat sf = new StringFormat(StringFormatFlags.LineLimit);
                sf.SetMeasurableCharacterRanges(new CharacterRange[] { new CharacterRange(0, 1) });
                sf.Alignment = StringAlignment.Near;
                sf.LineAlignment = StringAlignment.Near;
                System.Drawing.Text.TextRenderingHint txtrendr = graphics.TextRenderingHint;
                graphics.TextContrast = 0;
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                if (entries[index].Flags.HasFlag(PropertyEntryType.CancelButton))
                {
                    Rectangle buttonRect = new Rectangle(area.Right - area.Height, area.Top, area.Height, area.Height); // square rect at the right end
                    ControlPaint.DrawButton(graphics, buttonRect, ButtonState.Flat);
                    Region[] rgn = graphics.MeasureCharacterRanges("✖", fontForSymbols, buttonRect, sf);
                    RectangleF bnds = rgn[0].GetBounds(graphics);
                    SizeF sz = graphics.MeasureString("✖", fontForSymbols);
                    PointF p = new PointF(buttonRect.Left + (buttonRect.Width - bnds.Width) / 2.0f, buttonRect.Top + (buttonRect.Height - bnds.Height) / 2.0f);
                    graphics.DrawString("✖", fontForSymbols, ForeColorBrush, p, sf); // ✓✔✗✘✕✖⋮
                }
                if (entries[index].Flags.HasFlag(PropertyEntryType.OKButton))
                {
                    Rectangle buttonRect = new Rectangle(area.Right - 2 * area.Height, area.Top, area.Height, area.Height); // square rect at the right end
                    ControlPaint.DrawButton(graphics, buttonRect, ButtonState.Flat);
                    Region[] rgn = graphics.MeasureCharacterRanges("✔", fontForSymbols, buttonRect, sf);
                    RectangleF bnds = rgn[0].GetBounds(graphics);
                    SizeF sz = graphics.MeasureString("✔", fontForSymbols);
                    PointF p = new PointF(buttonRect.Left + (buttonRect.Width - bnds.Width) / 2.0f, buttonRect.Top + (buttonRect.Height - bnds.Height) / 2.0f);
                    graphics.DrawString("✔", fontForSymbols, ForeColorBrush, p, sf); // ✓✔✗✘✕✖⋮⁞🔒🔓
                }
                graphics.TextRenderingHint = txtrendr;
            }
            if (entries[index].Flags.HasFlag(PropertyEntryType.Checkable))
            {
                Rectangle cbrect = new Rectangle(entries[index].IndentLevel * buttonWidth, area.Top, area.Height, area.Height);
                cbrect.Inflate(-1, -1);
                ButtonState bs = ButtonState.Flat;
                if (entries[index].Value == "1") bs |= ButtonState.Checked;
                ControlPaint.DrawCheckBox(graphics, cbrect, bs);
            }
        }
        internal static void DrawString(Graphics graphics, string text, Font font, Brush brush, Rectangle rect, bool check)
        {
            StringFormat stringFormat = new StringFormat(StringFormat.GenericDefault);
            stringFormat.FormatFlags = StringFormatFlags.NoWrap;
            stringFormat.LineAlignment = StringAlignment.Center;
            stringFormat.Trimming = StringTrimming.EllipsisCharacter;
            int left = rect.Left;
            if (text.StartsWith("[["))
            {
                string[] parts = text.Split(new string[] { "]]" }, StringSplitOptions.None);
                text = parts[parts.Length - 1];
                string[] command = parts[0].Substring(2).Split(':');
                if (command[0] == "ColorBox" && command.Length == 4)
                {
                    Rectangle clr = new Rectangle(rect.Left + 1, rect.Top + 1, rect.Height - 2, rect.Height - 2);
                    Color color = Color.FromArgb(int.Parse(command[1]), int.Parse(command[2]), int.Parse(command[3]));
                    graphics.FillRectangle(new SolidBrush(color), clr);
                    if (check)
                        ControlPaint.DrawBorder3D(graphics, clr, Border3DStyle.Sunken);
                    else
                        ControlPaint.DrawBorder3D(graphics, clr, Border3DStyle.Flat);
                    left += clr.Width + 2;
                }
            }
            rect = new Rectangle(left, rect.Top, rect.Right - left, rect.Height);
            graphics.DrawString(text, font, brush, rect, stringFormat);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (entries == null) return;
            int firtItem = Math.Max(0, (int)Math.Floor((double)(e.ClipRectangle.Top - AutoScrollPosition.Y) / lineHeight));
            int lastItem = Math.Min(entries.Length, (int)Math.Ceiling((double)(e.ClipRectangle.Bottom - AutoScrollPosition.Y) / lineHeight));
            // System.Diagnostics.Trace.WriteLine("OnPaint: " + firtItem.ToString() + ", " + lastItem.ToString());
            for (int i = firtItem; i < lastItem; i++)
            {
                PaintItem(i, e.Graphics);
            }
        }
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            middle = (int)(0.5 * ClientSize.Width);
            fontHeight = Font.Height; // the Font of the Control
            lineHeight = (int)(fontHeight * 1.4); // Height of a single entry
            square = ((int)(0.6 * lineHeight)) & 0x7FFFFFFE; // make even number
            buttonWidth = (int)(0.8 * lineHeight);
            if (entries != null)
            {
                int totalHeight = entries.Length * lineHeight;
                AutoScrollMinSize = new Size(0, totalHeight);
            }
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (currentToolTip != null)
            {
                HideToolTip();
                currentToolTip = null;
            }
            base.OnMouseDown(e);
            (int index, EMousePos position, Rectangle hitItem) = GetMousePosition(e);
            if (position == EMousePos.onMiddleLine)
            {
                dragMiddlePosition = true;
            }
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            dragMiddlePosition = false;
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (e.Button == MouseButtons.Left && dragMiddlePosition)
            {
                double fct = (e.X - ClientRectangle.Left) / (double)(ClientRectangle.Width);
                if (fct < 0.1) fct = 0.1;
                if (fct > 0.9) fct = 0.9;
                middle = (int)(fct * ClientSize.Width);
                Refresh();
                return;
            }
            dragMiddlePosition = false;
            (int index, EMousePos position, Rectangle hitItem) = GetMousePosition(e);
            if (e.Button == MouseButtons.None && index >= 0 && position == EMousePos.onLabel)
            {   // maybe display a tooltip for this entry
                string toDisplay = null;
                if (entries[index].ResourceId != null)
                {
                    if (StringTable.IsStringDefined(entries[index].ResourceId))
                    {
                        toDisplay = StringTable.GetString(entries[index].ResourceId, StringTable.Category.tip);
                        if (toDisplay == null) toDisplay = StringTable.GetString(entries[index].ResourceId, StringTable.Category.label);
                    }
                    else
                    {
                        toDisplay = entries[index].ResourceId;
                    }
                }
                if (currentToolTip != toDisplay)
                {
                    if (toDisplay == null) HideToolTip();
                    else
                    {
                        if (currentToolTip != null) HideToolTip();
                        Point mp = LabelArea(index).Location; //  e.Location; //
                        mp = PointToClient(MousePosition);
                        mp = PointToClient(PointToScreen(e.Location));
                        mp.Y -= 2 * Font.Height;
                        mp.X -= 2 * Font.Height;
                        DelayShowToolTip(toDisplay, mp);
                    }
                    currentToolTip = toDisplay;
                }
                else if (delay != null)
                {   // delay is already running, but the mouse is still moving. So we modify the point, where the tooltip should appear
                    delay.Tag = new object[] { toDisplay, PointToClient(MousePosition) };
                }
            }
            else
            {
                currentToolTip = null;
                HideToolTip();
            }
            bool showLabelExtension = false;
            switch (position)
            {
                case EMousePos.onCancelButton:
                case EMousePos.onContextMenu:
                case EMousePos.onTreeButton:
                case EMousePos.onOkButton:
                case EMousePos.onLockButton:
                case EMousePos.onCheckbox:
                    Cursor = Cursors.Hand;
                    break;
                case EMousePos.onLabel:
                    if (entries[index].Flags.HasFlag(PropertyEntryType.LabelEditable)) Cursor = Cursors.IBeam;
                    else Cursor = Cursors.Arrow;
                    if (labelNeedsExtension[index])
                    {
                        propertiesExplorer.ShowLabelExtension(RectangleToScreen(IndentedItemArea(index)), entries[index].Label, entries[index]);
                        showLabelExtension = true;
                    }
                    break;
                case EMousePos.onValue:
                    if (entries[index].Flags.HasFlag(PropertyEntryType.ValueEditable)) Cursor = Cursors.IBeam;
                    else Cursor = Cursors.Arrow;
                    break;
                case EMousePos.onMiddleLine:
                    Cursor = Cursors.VSplit;
                    break;
                default:
                    Cursor = Cursors.Arrow;
                    break;
            }
            if (!showLabelExtension && propertiesExplorer.EntryWithLabelExtension != null) propertiesExplorer.HideLabelExtension();
        }
        private void DelayShowToolTip(string toDisplay, Point mp)
        {
            if (delay == null)
            {
                delay = new Timer();
                delay.Interval = 1000;
                delay.Tick += Delay_Tick;
                delay.Tag = new object[] { toDisplay, mp };
                delay.Start();
            }
        }
        private void HideToolTip()
        {
            if (delay != null)
            {
                delay.Stop();
                delay.Tick -= Delay_Tick;
                delay = null;
            }
            toolTip.Hide(this);
        }
        private void Delay_Tick(object sender, EventArgs e)
        {
            object[] oa = (sender as Timer).Tag as object[];
            string toDisplay = oa[0] as string;
            Point mp = (Point)oa[1];
            Point currentMousePosition = PointToClient(MousePosition);
            if (currentMousePosition.Y < mp.Y + 2 * Font.Height) // 
            {   // I don't know, why this happens, because the tooltip is generated with the correct mouse position.
                // it only happens, when the cursor enters the item from below
                mp.Y = currentMousePosition.Y - 2 * Font.Height;
            }
            toolTip.Show(toDisplay, this, mp);
            delay.Stop();
            delay.Tick -= Delay_Tick;
            delay = null;
            Timer toolTipOff = new Timer();
            toolTipOff.Interval = 3500;
            toolTipOff.Tick += ToolTipOff_Tick;
            toolTipOff.Start();
        }

        private void ToolTipOff_Tick(object sender, EventArgs e)
        {
            if (!IsDisposed) toolTip.Hide(this);
            (sender as Timer).Stop();
            (sender as Timer).Tick -= ToolTipOff_Tick;
            // currentToolTip = null; makes tooltip go on repeatedly
        }
        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            (int index, EMousePos position, Rectangle hitItem) = GetMousePosition(e);
            if (position == EMousePos.onLabel)
            {
                if (selected == index)
                {
                    entries[index].ButtonClicked(PropertyEntryButton.doubleclick);
                }
            }
        }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            (int index, EMousePos position, Rectangle hitItem) = GetMousePosition(e);
            if (position == EMousePos.onValue && entries[index].Flags.HasFlag(PropertyEntryType.DropDown)) position = EMousePos.onDropDownButton;
            switch (position)
            {
                case EMousePos.onTreeButton:
                    int added = entries[index].OpenOrCloseSubEntries();
                    if (added != 0)
                    {
                        RefreshEntries(-1, 0);
                    }
                    (this as IPropertyPage).Selected = entries[index];
                    break;
                case EMousePos.onCheckbox:
                    entries[index].ButtonClicked(PropertyEntryButton.check);
                    break;
                case EMousePos.onDropDownButton:
                    {
                        ShowDropDown(index);
                    }
                    break;
                case EMousePos.onCancelButton:
                    entries[index].ButtonClicked(PropertyEntryButton.cancel);
                    break;
                case EMousePos.onOkButton:
                    entries[index].ButtonClicked(PropertyEntryButton.ok);
                    break;
                case EMousePos.onLockButton:
                    entries[index].ButtonClicked(PropertyEntryButton.locked);
                    break;
                case EMousePos.onDirectMenu:
                    entries[index].ButtonClicked(PropertyEntryButton.directMenu);
                    break;
                case EMousePos.onContextMenu:
                    if (entries[index].ContextMenu == null)
                    {
                        throw new NotImplementedException("implement ContextMenu of " + entries[index].GetType().ToString() + ", " + entries[index].ResourceId);
                    }
                    ContextMenuWithHandler cm = MenuManager.MakeContextMenu(entries[index].ContextMenu);
                    cm.UpdateCommand(); // ContextMenu OnPopup is not being called
                    cm.Show(this, e.Location);
                    break;
                case EMousePos.onLabel:
                    if (selected == index && entries[index].Flags.HasFlag(PropertyEntryType.LabelEditable))
                    {
                        SelectedIndex = index;
                        if (propertiesExplorer.EntryWithTextBox == entries[index]) propertiesExplorer.HideTextBox(); // there is a textBox open for the value
                        ShowTextBox(index, e.Location, false);
                    }
                    else
                    {
                        if (entries[index].Flags.HasFlag(PropertyEntryType.Selectable)) SelectedIndex = index;
                    }
                    break;
                case EMousePos.onValue:
                    if (entries[index].Flags.HasFlag(PropertyEntryType.ValueEditable))
                    {
                        SelectedIndex = index; // before ShowTextBox, because this calls unselected and thus updates the currently textBox or listBox (if any)
                        ShowTextBox(index, e.Location, true);
                    }
                    else
                    {
                        if (entries[index].Flags.HasFlag(PropertyEntryType.Selectable)) SelectedIndex = index;
                        if (entries[index].Flags.HasFlag(PropertyEntryType.ValueAsButton)) entries[index].ButtonClicked(PropertyEntryButton.value);
                    }
                    break;
                case EMousePos.onMiddleLine:
                    Cursor = Cursors.VSplit;
                    break;
                default:
                    Cursor = Cursors.Arrow;
                    break;
            }
        }
        private void ShowTextBox(int index, Point location, bool showValue)
        {
            if (propertiesExplorer.EntryWithTextBox == entries[index]) return; // is already shown (maybe this is beeing called twice)
            if (propertiesExplorer.ActivePropertyPage != this) return; // cannot edit an invisible textbox
            entries[index].StartEdit(showValue);
            if (showValue) propertiesExplorer.ShowTextBox(RectangleToScreen(ValueArea(index)), entries[index].Value, entries[index], PointToScreen(location));
            else propertiesExplorer.ShowTextBox(RectangleToScreen(LabelArea(index)), entries[index].Label, entries[index], PointToScreen(location));
        }
        private void ShowDropDown(int index)
        {
            if (propertiesExplorer.ActivePropertyPage != this) return; // cannot use an invisible listbox
            if (propertiesExplorer.EntryWithListBox == entries[index])
            {
                // is already shown
                propertiesExplorer.HideListBox();
            }
            else
            {
                if (selected != index) SelectedIndex = index;
                string[] dropDownList = entries[index].GetDropDownList();
                int selind = -1;
                for (int i = 0; i < dropDownList.Length; i++)
                {
                    if (entries[index].Value == dropDownList[i])
                    {
                        selind = i;
                        break;
                    }
                }
                propertiesExplorer.ShowListBox(RectangleToScreen(ValueArea(index)), dropDownList, selind, entries[index]);
            }
        }
        private void OpenOrCloseSubEntries(int index)
        {
            IPropertyEntry selectedEntry = entries[index];
            int added = selectedEntry.OpenOrCloseSubEntries();
            if (added != 0)
            {
                RefreshEntries(selected, added);
            }
            (this as IPropertyPage).Selected = selectedEntry;
        }
        enum EMousePos { outside, onTreeButton, onLabel, onValue, onDropDownButton, onContextMenu, onOkButton, onCancelButton, onLockButton, onMiddleLine, onCheckbox, onDirectMenu }
        private (int index, EMousePos position, Rectangle hitItem) GetMousePosition(MouseEventArgs e)
        {
            if (entries == null) return (-1, EMousePos.outside, Rectangle.Empty);
            int index = (int)Math.Floor((e.Location.Y - AutoScrollPosition.Y) / (double)lineHeight);
            if (index < 0 || index >= entries.Length) return (-1, EMousePos.outside, Rectangle.Empty);
            int treeLeft = entries[index].IndentLevel * buttonWidth; // left side of the treeViewButton
            int treeRight = treeLeft + buttonWidth;
            int bottom = index * lineHeight + AutoScrollPosition.Y;

            Rectangle area = new Rectangle(0, index * lineHeight + AutoScrollPosition.Y, ClientSize.Width, lineHeight);
            if (entries[index].Flags.HasFlag(PropertyEntryType.Checkable) && e.Location.X >= treeLeft && e.Location.X <= treeRight) return (index, EMousePos.onCheckbox, new Rectangle(ClientRectangle.Width - 2 * lineHeight, bottom, lineHeight, lineHeight));
            if (entries[index].Value != null && e.Location.X >= middle - 2 && e.Location.X <= middle + 2) return (index, EMousePos.onMiddleLine, Rectangle.Empty);
            if (entries[index].Flags.HasFlag(PropertyEntryType.HasSubEntries) && e.Location.X >= treeLeft && e.Location.X <= treeRight) return (index, EMousePos.onTreeButton, new Rectangle(treeLeft, bottom, treeRight - treeLeft, lineHeight));
            if (entries[index].Value != null && e.Location.X >= treeLeft && e.Location.X <= middle) return (index, EMousePos.onLabel, new Rectangle(treeLeft, bottom, middle - treeLeft, lineHeight));
            if (entries[index].Flags.HasFlag(PropertyEntryType.ContextMenu) && e.Location.X >= ClientRectangle.Width - buttonWidth) return (index, EMousePos.onContextMenu, new Rectangle(ClientRectangle.Width - buttonWidth, bottom, buttonWidth, lineHeight));
            if (entries[index].Flags.HasFlag(PropertyEntryType.DirectMenu) && e.Location.X >= ClientRectangle.Width - buttonWidth) return (index, EMousePos.onDirectMenu, new Rectangle(ClientRectangle.Width - buttonWidth, bottom, buttonWidth, lineHeight));
            if (entries[index].Flags.HasFlag(PropertyEntryType.DropDown) && e.Location.X >= ClientRectangle.Width - buttonWidth) return (index, EMousePos.onDropDownButton, new Rectangle(middle, bottom, ClientRectangle.Width - middle, lineHeight));
            if (entries[index].Flags.HasFlag(PropertyEntryType.CancelButton) && e.Location.X >= ClientRectangle.Width - lineHeight) return (index, EMousePos.onCancelButton, new Rectangle(ClientRectangle.Width - lineHeight, bottom, lineHeight, lineHeight));
            if (entries[index].Flags.HasFlag(PropertyEntryType.OKButton) && e.Location.X >= ClientRectangle.Width - 2 * lineHeight) return (index, EMousePos.onOkButton, new Rectangle(ClientRectangle.Width - 2 * lineHeight, bottom, lineHeight, lineHeight)); //OK and Cancel buttons have width = lineHeight (18)
            if (entries[index].Flags.HasFlag(PropertyEntryType.Lockable) && e.Location.X >= ClientRectangle.Width - ButtonImages.ButtonImageList.ImageSize.Width - buttonWidth) return (index, EMousePos.onLockButton, new Rectangle(ClientRectangle.Width - 2 * lineHeight, bottom, lineHeight, lineHeight)); //Lock button has width of the image and is before ContextMenu, context menu has width = buttonWidth
            if (entries[index].Value == null && e.Location.X >= treeLeft) return (index, EMousePos.onLabel, new Rectangle(treeLeft, bottom, ClientSize.Width - treeLeft, lineHeight));
            if (entries[index].Value != null && e.Location.X >= middle) return (index, EMousePos.onValue, new Rectangle(middle, bottom, ClientRectangle.Width - middle, lineHeight));
            return (index, EMousePos.outside, Rectangle.Empty);
        }
        public void ValueChanged(int index)
        {
            throw new NotImplementedException();
        }
        public void LabelChanged(int index)
        {
            throw new NotImplementedException();
        }
        public void Add(IPropertyEntry toAdd, bool showOpen)
        {
            rootProperties.Add(toAdd);
            RefreshEntries(-1, 0);
            if (showOpen) OpenSubEntries(toAdd, true);
            (this as IPropertyPage).Selected = toAdd;
        }
        public void Remove(IPropertyEntry toRemove)
        {
            if (rootProperties.Count > toRemove.Index)
            {
                if (selected >= 0 && selected < entries.Length) propertiesExplorer.UnSelected(entries[selected]); // to close the textbox (if any) and call EndEdit
                rootProperties.RemoveAt(toRemove.Index);
                RefreshEntries(-1, 0);
                Invalidate();
            }
        }
        public void Clear()
        {
            rootProperties.Clear();
            RefreshEntries(-1, 0);
            Invalidate();
        }
        private void RefreshEntries(int current, int added)
        {   // doesn't look like a good implementation
            List<IPropertyEntry> listToUse = new List<IPropertyEntry>();
            for (int i = 0; i < rootProperties.Count; i++)
            {
                listToUse.Add(rootProperties[i]);
                listToUse.AddRange(GetSubEntries(rootProperties[i], 0));
            }
            SetEntries(listToUse.ToArray(), -1, 0);
        }
        private List<IPropertyEntry> GetSubEntries(IPropertyEntry pe, int level)
        {
            List<IPropertyEntry> res = new List<IPropertyEntry>();
            if (pe.IsOpen && pe.Flags.HasFlag(PropertyEntryType.HasSubEntries))
            {
                for (int i = 0; i < pe.SubItems.Length; i++)

                {
                    pe.SubItems[i].IndentLevel = level + 1;
                    res.Add(pe.SubItems[i]);
                    res.AddRange(GetSubEntries(pe.SubItems[i], level + 1));
                }
            }
            return res;
        }
        private void SetEntries(IPropertyEntry[] entries, int index, int added)
        {
            HashSet<IPropertyEntry> oldEntries, newEntries = new HashSet<IPropertyEntry>(entries);
            if (this.entries != null) oldEntries = new HashSet<IPropertyEntry>(this.entries);
            else oldEntries = new HashSet<IPropertyEntry>(); // there is no HashSet.AddRange
            // don't call Removed with a subsequent Added on the same entry, because some entries keep a list of
            // property-pages and rely on the order once it is created
            if (this.entries != null)
            {
                for (int i = 0; i < this.entries.Length; i++)
                {
                    if (!newEntries.Contains(this.entries[i])) this.entries[i].Removed(this);
                }
            }
            List<IPropertyEntry> lentries = new List<IPropertyEntry>(entries);
            for (int i = lentries.Count - 1; i >= 0; --i)
            {
                if (propertiesExplorer.isHidden(lentries[i].ResourceId)) lentries.RemoveAt(i);
            }
            this.entries = lentries.ToArray();
            labelNeedsExtension = new bool[entries.Length]; // all set to false
            for (int i = 0; i < entries.Length; i++)
            {
                entries[i].Index = i;
                if (!oldEntries.Contains(entries[i]))
                {
                    entries[i].Added(this);
                    entries[i].Parent = this; // the propertypage should be responsible that all entries have the correct parent. No other code reqired.
                }
            }
            int totalHeight = entries.Length * lineHeight;
            AutoScrollMinSize = new Size(0, totalHeight);
            Invalidate();
        }
        public void Refresh(IPropertyEntry toRefresh)
        {
            int index = FindEntry(toRefresh);
            propertiesExplorer.Refresh(toRefresh);
            if (index >= 0)
            {
                Refresh(index);
                // maybe the number of subentries has changed: then we simply close and open the subentries again
                // so the entries list will be recalculated. If some subentries have also subentries and are open or closed, this 
                // state will be preserved.
                if (entries[index].Flags.HasFlag(PropertyEntryType.HasSubEntries) && entries[index].IsOpen)
                {
                    OpenSubEntries(toRefresh, false);
                    OpenSubEntries(toRefresh, true);
                }
            }
            // System.Diagnostics.Trace.WriteLine("Refresh: " + index.ToString());
        }
        private int FindEntry(IPropertyEntry toFind)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] == toFind) return i;
            }
            return -1;
        }
        public void Refresh(int index)
        {
            Invalidate(ItemArea(index));
            //Update(); // for single entries: immediately update the result. Not sure, whether this is necessary, if there is a textBox, it will be updated immediately
        }
        internal void SelectNextPropertyEntry(bool forward)
        {
            if (forward)
            {
                for (int i = selected + 1; i < entries.Length; i++)
                {
                    if (entries[i].Flags.HasFlag(PropertyEntryType.Selectable))
                    {
                        SelectedIndex = i;
                        return;
                    }
                }
                for (int i = 0; i < selected; i++)
                {
                    if (entries[i].Flags.HasFlag(PropertyEntryType.Selectable))
                    {
                        SelectedIndex = i;
                        return;
                    }
                }
            }
            else
            {
                for (int i = selected - 1; i >= 0; --i)
                {
                    if (entries[i].Flags.HasFlag(PropertyEntryType.Selectable))
                    {
                        SelectedIndex = i;
                        return;
                    }
                }
                for (int i = entries.Length - 1; i > selected; --i)
                {
                    if (entries[i].Flags.HasFlag(PropertyEntryType.Selectable))
                    {
                        SelectedIndex = i;
                        return;
                    }
                }
            }
        }
        private int SelectedIndex
        {
            set
            {
                if (selected == value) return; // avoid endless recursion when selecting a dropdown entry
                int wasSelected = selected;
                selected = value;
                if (wasSelected >= 0 && wasSelected < entries.Length)
                {
                    entries[wasSelected].UnSelected(selected >= 0 ? entries[selected] : null);
                    propertiesExplorer.UnSelected(entries[wasSelected]);
                    Refresh(wasSelected);
                }
                if (selected >= 0 && selected < entries.Length)
                {
                    entries[selected].Selected(wasSelected >= 0 && wasSelected < entries.Length ? entries[wasSelected] : null);
                    Refresh(selected);
                    // if only the value and not the label is editable, then we show the textbox or the dropdown listbox
                    // otherwise it is simply selected and another click on the value part opens the textbox or listbox
                    if (entries[selected].Flags.HasFlag(PropertyEntryType.ValueEditable) && !entries[selected].Flags.HasFlag(PropertyEntryType.LabelEditable))
                    {
                        Rectangle area = ValueArea(selected);
                        ShowTextBox(selected, new Point(area.Right, area.Bottom), true);
                    }
                    else if (entries[selected].Flags.HasFlag(PropertyEntryType.DropDown) && !entries[selected].Flags.HasFlag(PropertyEntryType.LabelEditable))
                    {
                        ShowDropDown(selected);
                    }
                    propertiesExplorer.Selected(entries[selected]);
                }
                OnSelectionChanged?.Invoke((wasSelected >= 0 && wasSelected < entries.Length) ? entries[wasSelected] : null,
                    (selected >= 0 && selected < entries.Length) ? entries[selected] : null);
            }
        }
        IPropertyEntry IPropertyPage.Selected
        {
            get
            {
                if (selected >= 0) return entries[selected];
                return null;
            }
            set
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    if (entries[i] == value)
                    {
                        SelectedIndex = i;
                        break;
                    }
                }
            }
        }
        public IFrame Frame { get; set; }
        internal void OnEnter(bool ctrl)
        {
            if (selected >= 0)
            {   // activate the edit textbox or if not editable open or close the subentries
                IPropertyEntry selectedEntry = entries[selected];
                if (ctrl && selectedEntry.Flags.HasFlag(PropertyEntryType.HasSubEntries))
                {
                    int added = selectedEntry.OpenOrCloseSubEntries();
                    if (added != 0)
                    {
                        RefreshEntries(FindEntry(selectedEntry), added);
                    }
                    else
                    {
                        RefreshEntries(-1, 0);
                    }
                }
                else if (selectedEntry.Flags.HasFlag(PropertyEntryType.ValueEditable))
                {
                    Rectangle area = ValueArea(selected);
                    ShowTextBox(selected, new Point(area.Right, area.Bottom), true);
                }
                else if (selectedEntry.Flags.HasFlag(PropertyEntryType.DropDown))
                {
                    ShowDropDown(selected);
                }
                else if (selectedEntry.Flags.HasFlag(PropertyEntryType.HasSubEntries))
                {
                    OpenOrCloseSubEntries(selected);
                }
                else if (selectedEntry.Flags.HasFlag(PropertyEntryType.DirectMenu))
                {   // execute the menu as if the direct menu button had been clicked
                    selectedEntry.ButtonClicked(PropertyEntryButton.directMenu);
                }
            }
        }
        internal bool OnEscape(bool ctrl)
        {
            return false;
        }
        void IPropertyPage.BringToFront()
        {
            propertiesExplorer.ShowPropertyPage(this.TitleId);
        }
        public void OpenSubEntries(IPropertyEntry toOpenOrClose, bool open)
        {
            if (open != toOpenOrClose.IsOpen)
            {
                int added = toOpenOrClose.OpenOrCloseSubEntries();
                if (added != 0)
                {
                    RefreshEntries(FindEntry(toOpenOrClose), added);
                }
            }
            else if (toOpenOrClose.IsOpen)
            {   // this is often used as a refresh mechanism, so we refresh the subitems
                RefreshEntries(FindEntry(toOpenOrClose), toOpenOrClose.SubItems.Length);
            }
        }
        public IPropertyEntry GetCurrentSelection()
        {
            if (selected >= 0) return entries[selected];
            return null;
        }
        public void SelectEntry(IPropertyEntry toSelect)
        {
            if (toSelect == null) return;
            // propertiesExplorer.ShowPropertyPage(this.TitleId);
            // do not switch the top property page here
            (this as IPropertyPage).Selected = toSelect;
        }
        private bool ReflectNewSelection(IPropertyEntry NewSelection, IPropertyEntry current)
        {   // currently not used
            bool sel = (current.Index == selected);
            IPropertyEntry selectedChild = null;
            if (current.IsOpen)
            {
                for (int i = 0; i < current.SubItems.Length; ++i)
                {
                    if (ReflectNewSelection(NewSelection, current.SubItems[i]))
                    {
                        selectedChild = current.SubItems[i];
                    }
                }
            }
            return sel || (selectedChild != null);

        }
        public void MakeVisible(IPropertyEntry toShow)
        {
            if (toShow != null)
            {
                int ind = FindEntry(toShow);
                if (ind >= 0) VerticalScroll.Value = ind * lineHeight; // not testes yet
            }
        }
        private IPropertyEntry Search(string helpResourceID)
        {
            for (int i = 0; i < rootProperties.Count; i++)
            {
                IPropertyEntry found = rootProperties[i].FindSubItem(helpResourceID);
                if (found != null) return found;
            }
            return null;
        }
        public IPropertyEntry FindFromHelpLink(string helpResourceID, bool searchTreeAndOpen)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].ResourceId == helpResourceID || (string.IsNullOrEmpty(entries[i].ResourceId) && entries[i].Label == helpResourceID)) return entries[i];
            }
            if (searchTreeAndOpen)
            {
                IPropertyEntry found = Search(helpResourceID);
                if (found != null) return found;
            }
            return null;
        }
        public bool ContainsEntry(IPropertyEntry toTest)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] == toTest) return true;
            }
            return false;
        }
        public void StartEditLabel(IPropertyEntry ToEdit)
        {
            int index = FindEntry(ToEdit);
            if (propertiesExplorer.EntryWithTextBox == entries[index]) return; // is already shown (maybe this is beeing called twice)
            entries[index].StartEdit(false);
            Rectangle labelRect = LabelArea(index);
            propertiesExplorer.ShowTextBox(RectangleToScreen(labelRect), entries[index].Label, entries[index], PointToScreen(new Point(labelRect.Right, labelRect.Top)));
        }

        #region quick adaption to IPropertyTreeView, remove later
        public IView ActiveView => Frame.ActiveView;
        public IPropertyEntry GetParent(IShowProperty child)
        {   // this is a work around, because Parent is not correct implemented. It is always the property page, but it is also used in that way
            return (child as IPropertyEntry).Parent as IPropertyEntry;
        }
        public IFrame GetFrame()
        {
            return Frame;
        }
        public bool IsOpen(IShowProperty toTest)
        {
            return (toTest as IPropertyEntry).IsOpen;
        }
        public bool IsOnTop()
        {
            return propertiesExplorer.ActivePropertyPage == this;
        }
        #endregion

        protected override void Dispose(bool disposing)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                entries[i].Removed(this);
            }
            base.Dispose(disposing);
        }

        internal void PreProcessKeyDown(Substitutes.KeyEventArgs e)
        {
            OnPreProcessKeyDown?.Invoke(e);
        }
    }
}

