#version 330 core

in vec3 vNormal;
in vec3 vFragPos;

uniform vec4 u_color;
uniform vec3 u_light_pos;

out vec4 FragColor;

void main()
{
    vec3 norm = normalize(vNormal);
    vec3 lightDir = normalize(u_light_pos - vFragPos);

    // two-sided lighting
    float diff = abs(dot(norm, lightDir));

    vec3 ambient = 0.2 * u_color.rgb;
    vec3 diffuse = diff * u_color.rgb;

    vec3 viewDir = normalize(-vFragPos);
    vec3 reflectDir = reflect(-lightDir, norm);
    float spec = pow(max(dot(viewDir, reflectDir), 0.0), 32.0);
    vec3 specular = 0.3 * spec * vec3(1.0);

    FragColor = vec4(ambient + diffuse + specular, u_color.a);
}
