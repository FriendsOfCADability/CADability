#version 330 core

in vec2 vTexCoord;

uniform sampler2D u_texture;
uniform vec4 u_color;

out vec4 FragColor;

void main()
{
    vec4 c = texture(u_texture, vTexCoord) * u_color;
    // parity with the old renderer's glAlphaFunc(GL_GREATER, 0.5):
    // mostly transparent pixels must not write into the depth buffer
    if (c.a <= 0.5) discard;
    FragColor = c;
}
