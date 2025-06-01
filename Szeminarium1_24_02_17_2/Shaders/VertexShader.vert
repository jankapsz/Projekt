#version 330 core
layout (location = 0) in vec3 vPos; // position (0,1,2)
layout (location = 1) in vec4 vCol;
layout (location = 2) in vec3 vNorm; // normal (3,4,5)  
layout (location = 3) in vec2 vTex; // texture (6,7)

uniform mat4 uModel;
uniform mat3 uNormal;
uniform mat4 uView;
uniform mat4 uProjection;

out vec4 outCol;
out vec3 outNormal;
out vec3 outWorldPosition;
out vec2 outTex;


void main()
{
    outCol = vCol;
    outTex = vTex;
    gl_Position = uProjection*uView*uModel*vec4(vPos.x, vPos.y, vPos.z, 1.0);
    outNormal = uNormal*vNorm;
    outWorldPosition = vec3(uModel*vec4(vPos.x, vPos.y, vPos.z, 1.0));
}