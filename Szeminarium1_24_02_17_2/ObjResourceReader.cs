using Silk.NET.Maths;
using Silk.NET.OpenGL;
using System.Globalization;

namespace Szeminarium1_24_02_17_2
{
    internal class ObjResourceReader
    {
        public static unsafe GlObject CreateTeapotWithTexture(GL Gl, string textureFilePath)
        {
            uint vao = Gl.GenVertexArray();
            Gl.BindVertexArray(vao);

            List<Vector3D<float>> objVertices;
            List<Vector3D<float>> objNormals;
            List<Vector2D<float>> objTexCoords;
            List<(int v, int vt, int vn)[]> objFaces;

            ReadObjDataForTeapot(out objVertices, out objNormals, out objTexCoords, out objFaces);

            List<Vector3D<float>> finalNormals = new List<Vector3D<float>>(new Vector3D<float>[objVertices.Count]);
            int[] normalContribCounts = new int[objVertices.Count];

            bool hasNormals = objNormals.Count > 0;

            if (!hasNormals)
            {
                foreach (var face in objFaces)
                {
                    var a = objVertices[face[0].v];
                    var b = objVertices[face[1].v];
                    var c = objVertices[face[2].v];

                    var normal = Vector3D.Normalize(Vector3D.Cross(b - a, c - a));

                    for (int i = 0; i < 3; i++)
                    {
                        int vi = face[i].v;
                        finalNormals[vi] += normal;
                        normalContribCounts[vi]++;
                    }
                }

                for (int i = 0; i < finalNormals.Count; i++)
                {
                    if (normalContribCounts[i] > 0)
                        finalNormals[i] = Vector3D.Normalize(finalNormals[i]);
                }
            }

            List<float> glVertices = new List<float>(); // pos(3), normal(3), uv(2) = 8 floats per vertex
            List<uint> glIndices = new List<uint>();
            Dictionary<string, uint> uniqueVertexMap = new();

            foreach (var face in objFaces)
            {
                foreach (var (vIdx, vtIdx, vnIdx) in face)
                {
                    var position = objVertices[vIdx];
                    Vector3D<float> normal = hasNormals && vnIdx >= 0 ? objNormals[vnIdx] : finalNormals[vIdx];
                    Vector2D<float> texCoord = (vtIdx >= 0 && vtIdx < objTexCoords.Count) ? objTexCoords[vtIdx] : new Vector2D<float>(0, 0);

                    string key = $"{position.X:F6} {position.Y:F6} {position.Z:F6} {normal.X:F6} {normal.Y:F6} {normal.Z:F6} {texCoord.X:F6} {texCoord.Y:F6}";
                    if (!uniqueVertexMap.TryGetValue(key, out uint index))
                    {
                        index = (uint)(glVertices.Count / 8);
                        uniqueVertexMap[key] = index;

                        glVertices.Add(position.X);
                        glVertices.Add(position.Y);
                        glVertices.Add(position.Z);

                        glVertices.Add(normal.X);
                        glVertices.Add(normal.Y);
                        glVertices.Add(normal.Z);

                        glVertices.Add(texCoord.X);
                        glVertices.Add(texCoord.Y);
                    }

                    glIndices.Add(index);
                }
            }

            return CreateOpenGlObjectWithTexture(Gl, vao, glVertices, glIndices);
        }

        private static unsafe GlObject CreateOpenGlObjectWithTexture(GL Gl, uint vao, List<float> glVertices, List<uint> glIndices)
        {
            uint offsetPos = 0;
            uint offsetNormal = offsetPos + (3 * sizeof(float));
            uint offsetTexCoord = offsetNormal + (3 * sizeof(float));
            uint vertexSize = offsetTexCoord + (2 * sizeof(float));

            uint vbo = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ArrayBuffer, vbo);
            Gl.BufferData(GLEnum.ArrayBuffer, (ReadOnlySpan<float>)glVertices.ToArray().AsSpan(), GLEnum.StaticDraw);

            Gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, vertexSize, (void*)offsetPos);
            Gl.EnableVertexAttribArray(0);

            Gl.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, vertexSize, (void*)offsetNormal);
            Gl.EnableVertexAttribArray(2);

            Gl.VertexAttribPointer(3, 2, VertexAttribPointerType.Float, false, vertexSize, (void*)offsetTexCoord);
            Gl.EnableVertexAttribArray(3);

            uint ebo = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ElementArrayBuffer, ebo);
            Gl.BufferData(GLEnum.ElementArrayBuffer, (ReadOnlySpan<uint>)glIndices.ToArray().AsSpan(), GLEnum.StaticDraw);

            Gl.BindBuffer(GLEnum.ArrayBuffer, 0);

            return new GlObject(vao, vbo, 0, ebo, (uint)glIndices.Count, Gl);
        }

        private static void ReadObjDataForTeapot(out List<Vector3D<float>> objVertices, out List<Vector3D<float>> objNormals, out List<Vector2D<float>> objTexCoords, out List<(int v, int vt, int vn)[]> objFaces)
        {
            objVertices = new();
            objNormals = new();
            objTexCoords = new();
            objFaces = new();

            using var stream = typeof(ObjResourceReader).Assembly.GetManifestResourceStream("Szeminarium1_24_02_17_2.Resources.spaceship.obj");
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 1) continue;

                switch (tokens[0])
                {
                    case "v":
                        objVertices.Add(new Vector3D<float>(
                            float.Parse(tokens[1], CultureInfo.InvariantCulture),
                            float.Parse(tokens[2], CultureInfo.InvariantCulture),
                            float.Parse(tokens[3], CultureInfo.InvariantCulture)
                        ));
                        break;

                    case "vn":
                        objNormals.Add(new Vector3D<float>(
                            float.Parse(tokens[1], CultureInfo.InvariantCulture),
                            float.Parse(tokens[2], CultureInfo.InvariantCulture),
                            float.Parse(tokens[3], CultureInfo.InvariantCulture)
                        ));
                        break;

                    case "vt":
                        objTexCoords.Add(new Vector2D<float>(
                            float.Parse(tokens[1], CultureInfo.InvariantCulture),
                            float.Parse(tokens[2], CultureInfo.InvariantCulture)
                        ));
                        break;

                    case "f":
                        var face = new (int, int, int)[3];
                        for (int i = 0; i < 3; i++)
                        {
                            var parts = tokens[i + 1].Split('/');
                            int vIdx = int.Parse(parts[0]) - 1;
                            int vtIdx = (parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[1])) ? int.Parse(parts[1]) - 1 : -1;
                            int vnIdx = (parts.Length >= 3 && !string.IsNullOrWhiteSpace(parts[2])) ? int.Parse(parts[2]) - 1 : -1;
                            face[i] = (vIdx, vtIdx, vnIdx);
                        }
                        objFaces.Add(face);
                        break;
                }
            }
        }
    }
}
