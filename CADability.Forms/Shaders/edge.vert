#version 330 core

layout(location = 0) in vec3 aPosition;

uniform mat4 u_projection;
uniform mat4 u_modelview;

void main()
{
    gl_Position = u_projection * u_modelview * vec4(aPosition, 1.0);
}
