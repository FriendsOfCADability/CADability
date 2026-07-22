#version 330 core

layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec2 aTexCoord;

uniform mat4 u_projection;
uniform mat4 u_modelview;

out vec2 vTexCoord;

void main()
{
    vTexCoord = aTexCoord;
    gl_Position = u_projection * u_modelview * vec4(aPosition, 1.0);
}
