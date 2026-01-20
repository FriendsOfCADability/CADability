import re

# Read the file
with open('CADability/ImportDxf.cs', 'r') as f:
    content = f.read()

# Fix CreateMText to directly create GeoObject.Text instead of using netDxf.Entities.Text intermediate
mtext_method = '''        private IGeoObject CreateMText(IDxfMText mText)
        {
            GeoObject.Text text = GeoObject.Text.Construct();
            string txtstring = processAcadString(mText.PlainText);
            if (txtstring.Trim().Length == 0) return null;
            
            string filename = mText.FontName ?? mText.StyleName ?? "Arial";
            bool bold = false;
            bool italic = false;
            
            GeoPoint pos = GeoPoint(mText.Position);
            Angle a = Angle.Deg(mText.Rotation);
            double h = mText.Height;
            Plane plane = Plane(mText.Position, mText.Normal);

            text.Font = filename;
            text.Bold = bold;
            text.Italic = italic;
            text.TextString = txtstring;
            text.Location = pos;
            text.LineDirection = h * plane.ToGlobal(new GeoVector2D(a));
            text.GlyphDirection = h * plane.ToGlobal(new GeoVector2D(a + SweepAngle.ToLeft));
            text.TextSize = h;
            text.Alignment = GeoObject.Text.AlignMode.Bottom;
            text.LineAlignment = GeoObject.Text.LineAlignMode.Left;
            
            if (text.TextSize < 1e-5) return null;
            return text;
        }'''

# Replace the CreateMText method
content = re.sub(
    r'private IGeoObject CreateMText\(IDxfMText mText\).*?return CreateText\(txt\);.*?\n        \}',
    mtext_method,
    content,
    flags=re.DOTALL
)

# Fix Text creation - need to handle switch statement for alignment
# Remove TextAlignment enum references since we don't have that in abstraction
content = re.sub(r'TextAlignment\.', r'// TextAlignment.', content)

# Write back
with open('CADability/ImportDxf.cs', 'w') as f:
    f.write(content)

print("MText and related issues fixed")
