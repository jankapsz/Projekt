using Silk.NET.Maths;
using Silk.NET.OpenGL;
using StbImageSharp;
using System.Globalization;

namespace Szeminarium1_24_02_17_2
{
    internal class ObjResourceReader
    {
        public static unsafe GlObject CreateObjectWithTexture(GL Gl, string objFileName, string textureFileName, bool flipTextureY = false)
        {
            uint vao = Gl.GenVertexArray();
            Gl.BindVertexArray(vao);

            List<Vector3D<float>> objVertices;
            List<Vector3D<float>> objNormals;
            List<Vector2D<float>> objTexCoords;
            List<(int v, int vt, int vn)[]> objFaces;

            ReadObjData(objFileName, out objVertices, out objNormals, out objTexCoords, out objFaces, flipTextureY);

            List<Vector3D<float>> finalNormals = new List<Vector3D<float>>(new Vector3D<float>[objVertices.Count]);
            int[] normalContribCounts = new int[objVertices.Count];

            bool hasNormals = objNormals.Count > 0;

            // calculating normals if not present in OBJ file
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

            // building vertex data
            List<float> glVertices = new List<float>();
            List<uint> glIndices = new List<uint>();
            Dictionary<string, uint> uniqueVertexMap = new();

            foreach (var face in objFaces)
            {
                foreach (var (vIdx, vtIdx, vnIdx) in face)
                {
                    var position = objVertices[vIdx];
                    Vector3D<float> normal = hasNormals && vnIdx >= 0 ? objNormals[vnIdx] : finalNormals[vIdx];
                    Vector2D<float> texCoord = vtIdx >= 0 && vtIdx < objTexCoords.Count ? objTexCoords[vtIdx] : new Vector2D<float>(0, 0);

                    // vertex deduplication
                    string key = $"{position.X:F6} {position.Y:F6} {position.Z:F6} {normal.X:F6} {normal.Y:F6} {normal.Z:F6} {texCoord.X:F6} {texCoord.Y:F6}";
                    if (!uniqueVertexMap.TryGetValue(key, out uint index))
                    {
                        index = (uint)(glVertices.Count / 8); // 8 = 3 pos + 3 normal + 2 texcoord (for vertex shader)
                        uniqueVertexMap[key] = index;

                        glVertices.Add(position.X);   // [0] pos X
                        glVertices.Add(position.Y);   // [1] pos Y  
                        glVertices.Add(position.Z);   // [2] pos Z
                        glVertices.Add(normal.X);     // [3] normal X
                        glVertices.Add(normal.Y);     // [4] normal Y
                        glVertices.Add(normal.Z);     // [5] normal Z
                        glVertices.Add(texCoord.X);   // [6] texture U
                        glVertices.Add(texCoord.Y);   // [7] texture V
                    }

                    glIndices.Add(index);
                }
            }

            return CreateOpenGlObjectWithTexture(Gl, vao, glVertices, glIndices, textureFileName);
        }

        private static unsafe GlObject CreateOpenGlObjectWithTexture(GL Gl, uint vao, List<float> glVertices, List<uint> glIndices, string textureFileName)
        {
            // memory layout (total: 32 bytes)
            uint offsetPos = 0;
            uint offsetNormal = offsetPos + (3 * sizeof(float));
            uint offsetTexture = offsetNormal + (3 * sizeof(float));
            uint vertexSize = offsetTexture + (2 * sizeof(float));

            uint vertices = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ArrayBuffer, vertices);
            Gl.BufferData(GLEnum.ArrayBuffer, (ReadOnlySpan<float>)glVertices.ToArray().AsSpan(), GLEnum.StaticDraw); // copying into GPU memory

            // position attribute
            Gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, vertexSize, (void*)offsetPos);
            Gl.EnableVertexAttribArray(0);

            // normal attribute
            Gl.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, vertexSize, (void*)offsetNormal);
            Gl.EnableVertexAttribArray(2);

            // texture coordinate attribute
            Gl.VertexAttribPointer(3, 2, VertexAttribPointerType.Float, false, vertexSize, (void*)offsetTexture);
            Gl.EnableVertexAttribArray(3);

            // creating texture
            uint texture = Gl.GenTexture();
            Gl.ActiveTexture(TextureUnit.Texture0);
            Gl.BindTexture(TextureTarget.Texture2D, texture);

            var textureImageResult = ReadTextureImage(textureFileName);
            var textureBytes = (ReadOnlySpan<byte>)textureImageResult.Data.AsSpan();

            Gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)textureImageResult.Width,
                (uint)textureImageResult.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, textureBytes);

            Gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            Gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            Gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            Gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            uint indices = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ElementArrayBuffer, indices);
            Gl.BufferData(GLEnum.ElementArrayBuffer, (ReadOnlySpan<uint>)glIndices.ToArray().AsSpan(), GLEnum.StaticDraw);

            Gl.BindBuffer(GLEnum.ArrayBuffer, 0);
            Gl.BindTexture(TextureTarget.Texture2D, 0);

            var glObject = new GlObject(vao, vertices, 0, indices, (uint)glIndices.Count, Gl);
            glObject.Texture = texture;
            return glObject;
        }

        private static unsafe ImageResult ReadTextureImage(string textureResource)
        {
            ImageResult result;
            using (Stream textureStream = typeof(ObjResourceReader).Assembly.GetManifestResourceStream("Szeminarium1_24_02_17_2.Resources." + textureResource))
                result = ImageResult.FromStream(textureStream, ColorComponents.RedGreenBlueAlpha);

            return result;
        }

        // OBJ file parser
        private static void ReadObjData(string objFileName, out List<Vector3D<float>> objVertices, out List<Vector3D<float>> objNormals, out List<Vector2D<float>> objTexCoords, out List<(int v, int vt, int vn)[]> objFaces, bool flipTextureY = false)
        {
            objVertices = new();
            objNormals = new();
            objTexCoords = new();
            objFaces = new();

            using var stream = typeof(ObjResourceReader).Assembly.GetManifestResourceStream("Szeminarium1_24_02_17_2.Resources." + objFileName);
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
                            float.Parse(tokens[1], CultureInfo.InvariantCulture), // x
                            float.Parse(tokens[2], CultureInfo.InvariantCulture), // y
                            float.Parse(tokens[3], CultureInfo.InvariantCulture) // z
                        ));
                        break;

                    case "vt":
                        if (tokens.Length >= 3)
                        {
                            float textureY = float.Parse(tokens[2], CultureInfo.InvariantCulture);
                            if (flipTextureY) // Blender vs OpenGL
                                textureY = 1.0f - textureY;

                            objTexCoords.Add(new Vector2D<float>(
                                float.Parse(tokens[1], CultureInfo.InvariantCulture), // u
                                textureY                                              // v
                            ));
                        }
                        break;

                    case "vn":
                        objNormals.Add(new Vector3D<float>(
                            float.Parse(tokens[1], CultureInfo.InvariantCulture), // nx
                            float.Parse(tokens[2], CultureInfo.InvariantCulture), // ny
                            float.Parse(tokens[3], CultureInfo.InvariantCulture) // nz
                        ));
                        break;

                    case "f":
                        // handles both triangles (3 vertices) and quads (4 vertices)
                        int vertexCount = tokens.Length - 1;

                        if (vertexCount == 3)
                        {
                            // triangle
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
                        }
                        else if (vertexCount == 4)
                        {
                            // quad - splitting into two triangles
                            var vertices = new (int v, int vt, int vn)[4];
                            for (int i = 0; i < 4; i++)
                            {
                                var parts = tokens[i + 1].Split('/');
                                int vIdx = int.Parse(parts[0]) - 1;
                                int vtIdx = (parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[1])) ? int.Parse(parts[1]) - 1 : -1;
                                int vnIdx = (parts.Length >= 3 && !string.IsNullOrWhiteSpace(parts[2])) ? int.Parse(parts[2]) - 1 : -1;
                                vertices[i] = (vIdx, vtIdx, vnIdx);
                            }

                            // first triangle: 0, 1, 2
                            var face1 = new (int, int, int)[3];
                            face1[0] = vertices[0];
                            face1[1] = vertices[1];
                            face1[2] = vertices[2];
                            objFaces.Add(face1);

                            // second triangle: 0, 2, 3
                            var face2 = new (int, int, int)[3];
                            face2[0] = vertices[0];
                            face2[1] = vertices[2];
                            face2[2] = vertices[3];
                            objFaces.Add(face2);
                        }
                    break;
                }
            }
        }
    }
}