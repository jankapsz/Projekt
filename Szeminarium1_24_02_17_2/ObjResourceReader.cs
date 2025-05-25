using Silk.NET.Maths;
using Silk.NET.OpenGL;
using System.Globalization;

namespace Szeminarium1_24_02_17_2
{
    internal class ObjResourceReader
    {
        public static unsafe GlObject CreateTeapotWithColor(GL Gl, float[] faceColor)
        {
            uint vao = Gl.GenVertexArray();
            Gl.BindVertexArray(vao);

            List<Vector3D<float>> objVertices;
            List<Vector3D<float>> objNormals;
            List<(int v, int vn)[]> objFaces;

            ReadObjDataForTeapot(out objVertices, out objNormals, out objFaces);

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

            List<float> glVertices = new List<float>();
            List<float> glColors = new List<float>();
            List<uint> glIndices = new List<uint>();
            Dictionary<string, uint> uniqueVertexMap = new();

            foreach (var face in objFaces)
            {
                foreach (var (vIdx, vnIdx) in face)
                {
                    var position = objVertices[vIdx];
                    Vector3D<float> normal = hasNormals && vnIdx >= 0 ? objNormals[vnIdx] : finalNormals[vIdx];

                    string key = $"{position.X:F6} {position.Y:F6} {position.Z:F6} {normal.X:F6} {normal.Y:F6} {normal.Z:F6}";
                    if (!uniqueVertexMap.TryGetValue(key, out uint index))
                    {
                        index = (uint)(glVertices.Count / 6);
                        uniqueVertexMap[key] = index;

                        glVertices.Add(position.X);
                        glVertices.Add(position.Y);
                        glVertices.Add(position.Z);
                        glVertices.Add(normal.X);
                        glVertices.Add(normal.Y);
                        glVertices.Add(normal.Z);

                        glColors.AddRange(faceColor);
                    }

                    glIndices.Add(index);
                }
            }

            return CreateOpenGlObject(Gl, vao, glVertices, glColors, glIndices);
        }

        private static unsafe GlObject CreateOpenGlObject(GL Gl, uint vao, List<float> glVertices, List<float> glColors, List<uint> glIndices)
        {
            uint offsetPos = 0;
            uint offsetNormal = offsetPos + (3 * sizeof(float));
            uint vertexSize = offsetNormal + (3 * sizeof(float));

            uint vertices = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ArrayBuffer, vertices);
            Gl.BufferData(GLEnum.ArrayBuffer, (ReadOnlySpan<float>)glVertices.ToArray().AsSpan(), GLEnum.StaticDraw);
            Gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, vertexSize, (void*)offsetPos);
            Gl.EnableVertexAttribArray(0);

            Gl.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, vertexSize, (void*)offsetNormal);
            Gl.EnableVertexAttribArray(2);

            uint colors = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ArrayBuffer, colors);
            Gl.BufferData(GLEnum.ArrayBuffer, (ReadOnlySpan<float>)glColors.ToArray().AsSpan(), GLEnum.StaticDraw);
            Gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 0, null);
            Gl.EnableVertexAttribArray(1);

            uint indices = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ElementArrayBuffer, indices);
            Gl.BufferData(GLEnum.ElementArrayBuffer, (ReadOnlySpan<uint>)glIndices.ToArray().AsSpan(), GLEnum.StaticDraw);

            Gl.BindBuffer(GLEnum.ArrayBuffer, 0);
            return new GlObject(vao, vertices, colors, indices, (uint)glIndices.Count, Gl);
        }

        private static void ReadObjDataForTeapot(out List<Vector3D<float>> objVertices, out List<Vector3D<float>> objNormals, out List<(int v, int vn)[]> objFaces)
        {
            objVertices = new();
            objNormals = new();
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
                        Console.WriteLine("hi");
                        break;

                    case "vn":
                        objNormals.Add(new Vector3D<float>(
                            float.Parse(tokens[1], CultureInfo.InvariantCulture),
                            float.Parse(tokens[2], CultureInfo.InvariantCulture),
                            float.Parse(tokens[3], CultureInfo.InvariantCulture)
                        ));
                        break;

                    case "f":
                        var face = new (int, int)[3];
                        for (int i = 0; i < 3; i++)
                        {
                            var parts = tokens[i + 1].Split('/');
                            int vIdx = int.Parse(parts[0]) - 1;
                            int vnIdx = (parts.Length >= 3 && !string.IsNullOrWhiteSpace(parts[2])) ? int.Parse(parts[2]) - 1 : -1;
                            face[i] = (vIdx, vnIdx);
                        }
                        objFaces.Add(face);
                        break;
                }
            }
        }
    }
}
