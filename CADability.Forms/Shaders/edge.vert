#version 330 core

layout(location = 0) in vec3 aPosition;
layout(location = 1) in float aDist; // cumulative distance along the polyline in world units

uniform mat4 u_projection;
uniform mat4 u_modelview;

// screen-space linear interpolation: the dash pattern must not be perspective-distorted
noperspective out float vDist;

void main()
{
    vDist = aDist;
    gl_Position = u_projection * u_modelview * vec4(aPosition, 1.0);
}
