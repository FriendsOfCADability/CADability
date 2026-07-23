#version 330 core

in vec3 vNormal;

uniform vec4 u_color;
uniform vec3 u_light_pos;
uniform vec3 u_view_dir; // direction from the surface toward the viewer, constant per view

out vec4 FragColor;

void main()
{
    // a zero normal marks unlit geometry (text glyphs, see IPaintTo3DFlatText):
    // draw in the plain color so text is not brightened or darkened by the lighting
    if (dot(vNormal, vNormal) < 1e-6)
    {
        FragColor = u_color;
        return;
    }
    vec3 norm = normalize(vNormal);
    // u_light_pos is a direction vector in world space (directional light, like w=0 in old glLightfv)
    vec3 lightDir = normalize(u_light_pos);

    // two-sided lighting
    float diff = abs(dot(norm, lightDir));

    vec3 ambient = 0.2 * u_color.rgb;
    vec3 diffuse = diff * u_color.rgb;

    // The view direction is constant (orthographic viewer at infinity), like the old
    // fixed-function pipeline. A per-fragment direction toward some point would smear
    // the highlight over curved surfaces. Old shininess was 5 (broad, soft highlight);
    // the strength is halved because per-fragment specular is punchier than Gouraud.
    vec3 viewDir = normalize(u_view_dir);
    vec3 reflectDir = reflect(-lightDir, norm);
    float spec = pow(max(dot(viewDir, reflectDir), 0.0), 5.0);
    vec3 specular = 0.5 * spec * vec3(1.0);

    FragColor = vec4(ambient + diffuse + specular, u_color.a);
}
