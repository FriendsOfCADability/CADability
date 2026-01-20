import re

# Read the file
with open('CADability/ImportDxf.cs', 'r') as f:
    lines = f.readlines()

# Process line by line for complex replacements
output_lines = []
i = 0
while i < len(lines):
    line = lines[i]
    
    # Update FindOrCreateHatchStyleLines signature
    if 'private HatchStyleLines FindOrCreateHatchStyleLines(netDxf.Entities.EntityObject entity' in line:
        output_lines.append('        private HatchStyleLines FindOrCreateHatchStyleLines(IDxfEntity entity, double lineAngle, double lineDistance, double[] dashes)\n')
        i += 1
        continue
    
    # Update FindOrCreateColor signature
    if 'private ColorDef FindOrCreateColor(AciColor color, netDxf.Tables.Layer layer)' in line:
        output_lines.append('        private ColorDef FindOrCreateColor(int? colorArgb, string layerName)\n')
        i += 1
        continue
        
    # Update SetAttributes signature
    if 'private void SetAttributes(IGeoObject go, netDxf.Entities.EntityObject entity)' in line:
        output_lines.append('        private void SetAttributes(IGeoObject go, IDxfEntity entity)\n')
        i += 1
        continue
        
    # Update SetUserData signature
    if 'private void SetUserData(IGeoObject go, netDxf.Entities.EntityObject entity)' in line:
        output_lines.append('        private void SetUserData(IGeoObject go, IDxfEntity entity)\n')
        i += 1
        continue
    
    # Update FindBlock signature
    if 'private GeoObject.Block FindBlock(netDxf.Blocks.Block entity)' in line:
        output_lines.append('        private GeoObject.Block FindBlock(IDxfBlock entity)\n')
        i += 1
        continue
    
    output_lines.append(line)
    i += 1

# Write back
with open('CADability/ImportDxf.cs', 'w') as f:
    f.writelines(output_lines)

print("Signature updates complete")
