#version 330 core

layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec3 aNormal;

uniform mat4 u_projection;
uniform mat4 u_modelview;
uniform mat3 u_normal_matrix;

out vec3 vNormal;
out vec3 vFragPos;

void main()
{
    vec4 worldPos = u_modelview * vec4(aPosition, 1.0);
    vFragPos = worldPos.xyz;
    vNormal = normalize(u_normal_matrix * aNormal);
    gl_Position = u_projection * worldPos;
}
