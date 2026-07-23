#version 330 core

noperspective in float vDist;

uniform vec4 u_color;

// dash pattern: up to 8 segment lengths in pixels, alternating on/off, starting with on.
// u_patternCount == 0 means solid. u_distScale converts world distance to pixels.
uniform float u_pattern[8];
uniform int   u_patternCount;
uniform float u_patternTotal;
uniform float u_distScale;

out vec4 FragColor;

void main()
{
    if (u_patternCount > 0)
    {
        float m = mod(vDist * u_distScale, u_patternTotal);
        float acc = 0.0;
        for (int i = 0; i < u_patternCount; ++i)
        {
            acc += u_pattern[i];
            if (m < acc)
            {
                if ((i & 1) == 1) discard; // odd segments are the gaps
                break;
            }
        }
    }
    FragColor = u_color;
}
