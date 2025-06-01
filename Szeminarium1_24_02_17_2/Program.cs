using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;

namespace Szeminarium1_24_02_17_2
{
    internal static class Program
    {
        private static CameraDescriptor cameraDescriptor = new();

        private static CubeArrangementModel cubeArrangementModel = new();

        private static IWindow window;

        private static IInputContext inputContext;

        private static GL Gl;

        private static ImGuiController controller;

        private static uint program;

        private static GlObject teapot;

        private static GlObject table;

        private static GlObject spaceship;
        private static GlObject sun;

        private static GlObject mercury;
        private static List<Vector3D<float>> mercuryPositions = new List<Vector3D<float>>();
        private static List<float> mercuryScales = new List<float>();
        private static int numberOfMercuries = 20;

        private static GlObject asteroid;
        private static List<Vector3D<float>> asteroidPositions = new List<Vector3D<float>>();
        private static List<float> asteroidScales = new List<float>();
        private static List<Vector3D<float>> asteroidRotations = new List<Vector3D<float>>(); // Random forgatások
        private static int numberOfAsteroids = 100; // Több aszteroida
        private static float asteroidGlobalScale = 1.0f;

        private static List<Vector3D<float>> asteroidVelocities = new List<Vector3D<float>>(); // Sebesség vektorok
        private static float asteroidSpeed = 2.0f; // Globális sebesség szorzó

        private static Vector3D<float> asteroidGlobalDrift = new Vector3D<float>(0.2f, 0.1f, 0.3f); // Globális drift irány
        private static float asteroidDriftStrength = 1.0f; // Mennyire erős a drift

        private static GlCube glCubeRotating;

        private static GlCube skyBox;

        private static float Shininess = 50;

        private static Vector3D<float> spaceshipStartPosition = new Vector3D<float>(-70f, 0f, -70f);
        private static Vector3D<float> spaceshipPosition = spaceshipStartPosition;

        private static bool FirstPersonView = false;

        private static float spaceshipRotationY = 0f;
        private static float spaceshipMoveSpeed = 5.0f;


        private const string ModelMatrixVariableName = "uModel";
        private const string NormalMatrixVariableName = "uNormal";
        private const string ViewMatrixVariableName = "uView";
        private const string ProjectionMatrixVariableName = "uProjection";

        private const string TextureUniformVariableName = "uTexture";

        private const string LightColorVariableName = "lightColor";
        private const string LightPositionVariableName = "lightPos";
        private const string ViewPosVariableName = "viewPos";
        private const string ShininessVariableName = "shininess";

        static void Main(string[] args)
        {
            WindowOptions windowOptions = WindowOptions.Default;
            windowOptions.Title = "2 szeminárium";
            windowOptions.Size = new Vector2D<int>(1000, 1000);

            // on some systems there is no depth buffer by default, so we need to make sure one is created
            windowOptions.PreferredDepthBufferBits = 24;

            window = Window.Create(windowOptions);

            window.Load += Window_Load;
            window.Update += Window_Update;
            window.Render += Window_Render;
            window.Closing += Window_Closing;

            window.Run();
        }

        private static void Window_Load()
        {
            //Console.WriteLine("Load");

            // set up input handling
            inputContext = window.CreateInput();
            foreach (var keyboard in inputContext.Keyboards)
            {
                keyboard.KeyDown += Keyboard_KeyDown;
            }

            Gl = window.CreateOpenGL();

            controller = new ImGuiController(Gl, window, inputContext);

            // Handle resizes
            window.FramebufferResize += s =>
            {
                // Adjust the viewport to the new window size
                Gl.Viewport(s);
            };


            Gl.ClearColor(System.Drawing.Color.Black);

            SetUpObjects();

            LinkProgram();

            //Gl.Enable(EnableCap.CullFace);

            Gl.Enable(EnableCap.DepthTest);
            Gl.DepthFunc(DepthFunction.Lequal);
        }

        private static void LinkProgram()
        {
            uint vshader = Gl.CreateShader(ShaderType.VertexShader);
            uint fshader = Gl.CreateShader(ShaderType.FragmentShader);

            Gl.ShaderSource(vshader, ReadShader("VertexShader.vert"));
            Gl.CompileShader(vshader);
            Gl.GetShader(vshader, ShaderParameterName.CompileStatus, out int vStatus);
            if (vStatus != (int)GLEnum.True)
                throw new Exception("Vertex shader failed to compile: " + Gl.GetShaderInfoLog(vshader));

            Gl.ShaderSource(fshader, ReadShader("FragmentShader.frag"));
            Gl.CompileShader(fshader);

            program = Gl.CreateProgram();
            Gl.AttachShader(program, vshader);
            Gl.AttachShader(program, fshader);
            Gl.LinkProgram(program);
            Gl.GetProgram(program, GLEnum.LinkStatus, out var status);
            if (status == 0)
            {
                Console.WriteLine($"Error linking shader {Gl.GetProgramInfoLog(program)}");
            }
            Gl.DetachShader(program, vshader);
            Gl.DetachShader(program, fshader);
            Gl.DeleteShader(vshader);
            Gl.DeleteShader(fshader);
        }

        private static string ReadShader(string shaderFileName)
        {
            using (Stream shaderStream = typeof(Program).Assembly.GetManifestResourceStream("Szeminarium1_24_02_17_2.Shaders." + shaderFileName))
            using (StreamReader shaderReader = new StreamReader(shaderStream))
                return shaderReader.ReadToEnd();
        }

        private static void Keyboard_KeyDown(IKeyboard keyboard, Key key, int arg3)
        {
            switch (key)
            {
                case Key.Left:
                    cameraDescriptor.DecreaseZYAngle();
                    break;
                case Key.Right:
                    cameraDescriptor.IncreaseZYAngle();
                    break;
                case Key.Down:
                    cameraDescriptor.IncreaseDistance();
                    break;
                case Key.Up:
                    cameraDescriptor.DecreaseDistance();
                    break;
                case Key.U:
                    cameraDescriptor.IncreaseZXAngle();
                    break;

                // Űrhajó mozgás - ÚJ LOGIKA
                case Key.W:
                    MoveSpaceshipForward(1f); // Előre
                    break;
                case Key.S:
                    MoveSpaceshipForward(-1f); // Hátra
                    break;
                case Key.A:
                    RotateSpaceship(0.1f); // Balra forgatás
                    break;
                case Key.D:
                    RotateSpaceship(-0.1f); // Jobbra forgatás
                    break;
                case Key.Q:
                    MoveSpaceshipVertical(1f); // Fel
                    break;
                case Key.E:
                    MoveSpaceshipVertical(-1f); // Le
                    break;
                case Key.V:
                    FirstPersonView = !FirstPersonView;
                    break;
            }
        }

        private static void Window_Update(double deltaTime)
        {
            //Console.WriteLine($"Update after {deltaTime} [s].");
            // multithreaded
            // make sure it is threadsafe
            // NO GL calls
            cubeArrangementModel.AdvanceTime(deltaTime);

            CheckCollisions();

            // Aszteroidák mozgatása
            for (int i = 0; i < asteroidPositions.Count; i++)
            {
                // Pozíció frissítése sebességvektor alapján
                var currentPosition = asteroidPositions[i];
                var velocity = asteroidVelocities[i];

                // Új pozíció = régi pozíció + sebesség * idő * globális sebesség
                var newPosition = currentPosition + velocity * (float)deltaTime * asteroidSpeed;
                asteroidPositions[i] = newPosition;

                // Opcionális: Aszteroida forgatása mozgás közben
                var currentRotation = asteroidRotations[i];
                asteroidRotations[i] = new Vector3D<float>(
                    currentRotation.X + (float)deltaTime * 0.5f, // Lassú forgatás X tengely körül
                    currentRotation.Y + (float)deltaTime * 0.3f, // Lassú forgatás Y tengely körül
                    currentRotation.Z + (float)deltaTime * 0.7f  // Lassú forgatás Z tengely körül
                );
            }

            // Határok ellenőrzése - ha túl messze megy, fordítsd meg az irányt
            for (int i = 0; i < asteroidPositions.Count; i++)
            {
                var position = asteroidPositions[i];
                float maxDistance = 3000f;
                float distance = (float)Math.Sqrt(position.X * position.X + position.Y * position.Y + position.Z * position.Z);

                if (distance > maxDistance)
                {
                    Random random = new Random();

                    // ÚJ RANDOM POZÍCIÓ nagy területen
                    float x = (float)(random.NextDouble() * 6000 - 3000);
                    float y = (float)(random.NextDouble() * 6000 - 3000);
                    float z = (float)(random.NextDouble() * 6000 - 3000);
                    asteroidPositions[i] = new Vector3D<float>(x, y, z);

                    // ÚJ RANDOM IRÁNY
                    float velX = (float)(random.NextDouble() * 2 - 1);
                    float velY = (float)(random.NextDouble() * 2 - 1);
                    float velZ = (float)(random.NextDouble() * 2 - 1);
                    asteroidVelocities[i] = Vector3D.Normalize(new Vector3D<float>(velX, velY, velZ));
                }
            }

            controller.Update((float)deltaTime);
        }

        private static unsafe void Window_Render(double deltaTime)
        {
            //Console.WriteLine($"Render after {deltaTime} [s].");

            // GL here
            Gl.Clear(ClearBufferMask.ColorBufferBit);
            Gl.Clear(ClearBufferMask.DepthBufferBit);


            Gl.UseProgram(program);

            if (FirstPersonView)
            {
                var forwardDir = new Vector3D<float>(
                    (float)Math.Sin(spaceshipRotationY),
                    0f,
                    (float)Math.Cos(spaceshipRotationY)
                );

                // Kamera pozíció: az űrhajó pozíciójában, kissé előrébb (az orr felé)
                var cameraOffset = forwardDir * 20.0f; // 2 egységgel előrébb az orr irányába
                cameraDescriptor.OverridePosition = spaceshipPosition + new Vector3D<float>(0f, 0.5f, 0f) + cameraOffset;

                // Kamera target: messze az űrhajó irányába néz
                cameraDescriptor.Target = spaceshipPosition + forwardDir * 100f + new Vector3D<float>(0f, 0.5f, 0f);

            }
            else
            {
                var backwardDir = new Vector3D<float>(
                    -(float)Math.Sin(spaceshipRotationY),
                    0,
                    -(float)Math.Cos(spaceshipRotationY)
                );

                cameraDescriptor.OverridePosition = spaceshipPosition + backwardDir * 30f + new Vector3D<float>(0f, 10f, 0f);
                cameraDescriptor.Target = spaceshipPosition;
            }


            SetViewMatrix();
            SetProjectionMatrix();

            SetLightColor();
            SetLightPosition();
            SetViewerPosition();
            SetShininess();

            DrawPulsingTeapot();
            DrawSun();
            DrawMercuries();
            DrawAsteroids();

            DrawSkyBox();

            ImGuiNET.ImGui.Begin("Controls",
            ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar);

            // Kameraváltás checkbox
            ImGuiNET.ImGui.Checkbox("First Person View", ref FirstPersonView);

            ImGuiNET.ImGui.Separator(); // Elválasztó vonal

            // Lighting beállítások
            ImGuiNET.ImGui.Text("Lighting Properties:");
            ImGuiNET.ImGui.SliderFloat("Shininess", ref Shininess, 1, 200);

            ImGuiNET.ImGui.End();



            controller.Render();
        }

        private static unsafe void DrawSkyBox()
        {
            Matrix4X4<float> modelMatrix = Matrix4X4.CreateScale(10000f);
            SetModelMatrix(modelMatrix);
            Gl.BindVertexArray(skyBox.Vao);

            int textureLocation = Gl.GetUniformLocation(program, TextureUniformVariableName);
            if (textureLocation == -1)
            {
                throw new Exception($"{TextureUniformVariableName} uniform not found on shader.");
            }
            // set texture 0
            Gl.Uniform1(textureLocation, 0);

            Gl.ActiveTexture(TextureUnit.Texture0);
            Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (float)GLEnum.Linear);
            Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (float)GLEnum.Linear);
            Gl.BindTexture(TextureTarget.Texture2D, skyBox.Texture.Value);

            Gl.DrawElements(GLEnum.Triangles, skyBox.IndexArrayLength, GLEnum.UnsignedInt, null);
            Gl.BindVertexArray(0);

            CheckError();
            Gl.BindTexture(TextureTarget.Texture2D, 0);
            CheckError();
        }

        private static unsafe void DrawSun()
        {
            // Nap a középpontban (0,0,0), megfelelő méretezéssel
            var modelMatrixForSun = Matrix4X4.CreateScale(0.1f) * // Méret állítása
                                   Matrix4X4.CreateTranslation(0f, 0f, 0f); // Középpont
            SetModelMatrix(modelMatrixForSun);

            Gl.BindVertexArray(sun.Vao);

            // Textúra beállítása
            int textureLocation = Gl.GetUniformLocation(program, TextureUniformVariableName);
            if (textureLocation != -1)
            {
                Gl.Uniform1(textureLocation, 0);
                Gl.ActiveTexture(TextureUnit.Texture0);
                Gl.BindTexture(TextureTarget.Texture2D, sun.Texture);
            }

            Gl.DrawElements(GLEnum.Triangles, sun.IndexArrayLength, GLEnum.UnsignedInt, null);
            Gl.BindVertexArray(0);
            Gl.BindTexture(TextureTarget.Texture2D, 0);
        }

        private static unsafe void SetLightColor()
        {
            int location = Gl.GetUniformLocation(program, LightColorVariableName);

            if (location == -1)
            {
                throw new Exception($"{LightColorVariableName} uniform not found on shader.");
            }

            Gl.Uniform3(location, 1.5f, 1.4f, 1f);
            CheckError();
        }

        private static unsafe void SetLightPosition()
        {
            int location = Gl.GetUniformLocation(program, LightPositionVariableName);

            if (location == -1)
            {
                throw new Exception($"{LightPositionVariableName} uniform not found on shader.");
            }

            Gl.Uniform3(location, 0f, 0f, 0f);
            CheckError();
        }

        private static unsafe void SetViewerPosition()
        {
            int location = Gl.GetUniformLocation(program, ViewPosVariableName);

            if (location == -1)
            {
                throw new Exception($"{ViewPosVariableName} uniform not found on shader.");
            }

            Gl.Uniform3(location, cameraDescriptor.Position.X, cameraDescriptor.Position.Y, cameraDescriptor.Position.Z);
            CheckError();
        }

        private static unsafe void SetShininess()
        {
            int location = Gl.GetUniformLocation(program, ShininessVariableName);

            if (location == -1)
            {
                throw new Exception($"{ShininessVariableName} uniform not found on shader.");
            }

            Gl.Uniform1(location, Shininess);
            CheckError();
        }

        private static unsafe void DrawPulsingTeapot()
        {
            var modelMatrixForCenterCube = Matrix4X4.CreateScale(0.05f) *
                                         Matrix4X4.CreateRotationY(spaceshipRotationY) *
                                         Matrix4X4.CreateTranslation(spaceshipPosition);
            SetModelMatrix(modelMatrixForCenterCube);

            Gl.BindVertexArray(spaceship.Vao);

            // Textúra beállítása
            int textureLocation = Gl.GetUniformLocation(program, TextureUniformVariableName);
            if (textureLocation != -1)
            {
                Gl.Uniform1(textureLocation, 0);
                Gl.ActiveTexture(TextureUnit.Texture0);
                Gl.BindTexture(TextureTarget.Texture2D, spaceship.Texture);
            }

            Gl.DrawElements(GLEnum.Triangles, spaceship.IndexArrayLength, GLEnum.UnsignedInt, null);
            Gl.BindVertexArray(0);
            Gl.BindTexture(TextureTarget.Texture2D, 0);
        }

        private static unsafe void DrawMercuries()
        {
            for (int i = 0; i < mercuryPositions.Count; i++)
            {
                var position = mercuryPositions[i];
                var scale = mercuryScales[i];

                var modelMatrix = Matrix4X4.CreateScale(scale) *
                                 Matrix4X4.CreateTranslation(position);
                SetModelMatrix(modelMatrix);

                Gl.BindVertexArray(mercury.Vao);

                // Textúra beállítása
                int textureLocation = Gl.GetUniformLocation(program, TextureUniformVariableName);
                if (textureLocation != -1)
                {
                    Gl.Uniform1(textureLocation, 0);
                    Gl.ActiveTexture(TextureUnit.Texture0);
                    Gl.BindTexture(TextureTarget.Texture2D, mercury.Texture);
                }

                Gl.DrawElements(GLEnum.Triangles, mercury.IndexArrayLength, GLEnum.UnsignedInt, null);
            }

            Gl.BindVertexArray(0);
            Gl.BindTexture(TextureTarget.Texture2D, 0);
        }

        private static unsafe void DrawAsteroids()
        {
            for (int i = 0; i < asteroidPositions.Count; i++)
            {
                var position = asteroidPositions[i];
                var scale = asteroidScales[i] * asteroidGlobalScale;
                var rotation = asteroidRotations[i];

                // Komplex transzformáció: méretezés + forgatás + elhelyezés
                var modelMatrix = Matrix4X4.CreateScale(scale) *
                                 Matrix4X4.CreateRotationX(rotation.X) *
                                 Matrix4X4.CreateRotationY(rotation.Y) *
                                 Matrix4X4.CreateRotationZ(rotation.Z) *
                                 Matrix4X4.CreateTranslation(position);
                SetModelMatrix(modelMatrix);

                Gl.BindVertexArray(asteroid.Vao);

                // Textúra beállítása
                int textureLocation = Gl.GetUniformLocation(program, TextureUniformVariableName);
                if (textureLocation != -1)
                {
                    Gl.Uniform1(textureLocation, 0);
                    Gl.ActiveTexture(TextureUnit.Texture0);
                    Gl.BindTexture(TextureTarget.Texture2D, asteroid.Texture);
                }

                Gl.DrawElements(GLEnum.Triangles, asteroid.IndexArrayLength, GLEnum.UnsignedInt, null);
            }

            Gl.BindVertexArray(0);
            Gl.BindTexture(TextureTarget.Texture2D, 0);
        }

        private static void MoveSpaceshipForward(float amount)
        {
            // Az űrhajó saját irányában mozog
            var forwardDirection = new Vector3D<float>(
                (float)Math.Sin(spaceshipRotationY),
                0f,
                (float)Math.Cos(spaceshipRotationY)
            );

            spaceshipPosition += forwardDirection * amount * spaceshipMoveSpeed;

            // Kamera target frissítése
            cameraDescriptor.Target = spaceshipPosition;
        }

        // Fel/le mozgás
        private static void MoveSpaceshipVertical(float amount)
        {
            spaceshipPosition += new Vector3D<float>(0f, amount * spaceshipMoveSpeed, 0f);
            cameraDescriptor.Target = spaceshipPosition;
        }

        private static void RotateSpaceship(float rotationAmount)
        {
            spaceshipRotationY += rotationAmount;
        }

        private static unsafe void SetModelMatrix(Matrix4X4<float> modelMatrix)
        {
            int location = Gl.GetUniformLocation(program, ModelMatrixVariableName);
            if (location == -1)
            {
                throw new Exception($"{ModelMatrixVariableName} uniform not found on shader.");
            }

            Gl.UniformMatrix4(location, 1, false, (float*)&modelMatrix);
            CheckError();

            var modelMatrixWithoutTranslation = new Matrix4X4<float>(modelMatrix.Row1, modelMatrix.Row2, modelMatrix.Row3, modelMatrix.Row4);
            modelMatrixWithoutTranslation.M41 = 0;
            modelMatrixWithoutTranslation.M42 = 0;
            modelMatrixWithoutTranslation.M43 = 0;
            modelMatrixWithoutTranslation.M44 = 1;

            Matrix4X4<float> modelInvers;
            Matrix4X4.Invert<float>(modelMatrixWithoutTranslation, out modelInvers);
            Matrix3X3<float> normalMatrix = new Matrix3X3<float>(Matrix4X4.Transpose(modelInvers));
            location = Gl.GetUniformLocation(program, NormalMatrixVariableName);
            if (location == -1)
            {
                throw new Exception($"{NormalMatrixVariableName} uniform not found on shader.");
            }
            Gl.UniformMatrix3(location, 1, false, (float*)&normalMatrix);
            CheckError();
        }

        private static unsafe void SetUpObjects()
        {

            float[] face1Color = [1f, 0f, 0f, 1.0f];
            float[] face2Color = [0.0f, 1.0f, 0.0f, 1.0f];
            float[] face3Color = [0.0f, 0.0f, 1.0f, 1.0f];
            float[] face4Color = [1.0f, 0.0f, 1.0f, 1.0f];
            float[] face5Color = [0.0f, 1.0f, 1.0f, 1.0f];
            float[] face6Color = [1.0f, 1.0f, 0.0f, 1.0f];

            spaceship = ObjResourceReader.CreateObjectWithTexture(Gl, "spaceship.obj", "spaceship_texture.jpg", flipTextureY: true);
            sun = ObjResourceReader.CreateObjectWithTexture(Gl, "sun.obj", "sun_texture.jpg");
            mercury = ObjResourceReader.CreateObjectWithTexture(Gl, "mercury.obj", "mercury_texture.jpg");
            asteroid = ObjResourceReader.CreateObjectWithTexture(Gl, "asteroid.obj", "asteroid_texture.jpg");

            // Random bolygó pozíciók generálása
            Random random = new Random();
            for (int i = 0; i < numberOfMercuries; i++)
            {
                // Random pozíció a nap körül (de nem túl közel)
                float distance = random.Next(100, 2000); // 100-2000 egység távolság a naptól
                float angleY = (float)(random.NextDouble() * 2 * Math.PI); // Random szög
                float angleX = (float)(random.NextDouble() * Math.PI - Math.PI / 2); // -90° és +90° között

                float x = distance * (float)Math.Cos(angleX) * (float)Math.Cos(angleY);
                float y = distance * (float)Math.Sin(angleX);
                float z = distance * (float)Math.Cos(angleX) * (float)Math.Sin(angleY);

                mercuryPositions.Add(new Vector3D<float>(x, y, z));

                // Random méret
                float scale = (float)(random.NextDouble() * 0.02 + 0.01); // 0.01-0.06 közötti méret
                mercuryScales.Add(scale);
            }

            // Aszteroidák generálása
            for (int i = 0; i < numberOfAsteroids; i++)
            {
                // Aszteroidák közelebb a naphoz és szétszórtabban
                float distance = random.Next(50, 1500); // Közelebb mint a bolygók
                float angleY = (float)(random.NextDouble() * 2 * Math.PI);
                float angleX = (float)(random.NextDouble() * Math.PI - Math.PI / 2);

                float x = distance * (float)Math.Cos(angleX) * (float)Math.Cos(angleY);
                float y = distance * (float)Math.Sin(angleX);
                float z = distance * (float)Math.Cos(angleX) * (float)Math.Sin(angleY);

                asteroidPositions.Add(new Vector3D<float>(x, y, z));

                // Kisebb és változatosabb méretek
                float scale = (float)(random.NextDouble() * 0.02 + 0.005); // 0.005-0.025 közötti méret
                asteroidScales.Add(scale);

                // Random forgatás minden tengely körül
                float rotX = (float)(random.NextDouble() * 2 * Math.PI);
                float rotY = (float)(random.NextDouble() * 2 * Math.PI);
                float rotZ = (float)(random.NextDouble() * 2 * Math.PI);
                asteroidRotations.Add(new Vector3D<float>(rotX, rotY, rotZ));

                // ÚJ: Random mozgási irány
                float velX = (float)(random.NextDouble() * 2 - 1); // -1 és 1 között
                float velY = (float)(random.NextDouble() * 2 - 1);
                float velZ = (float)(random.NextDouble() * 2 - 1);

                // Normalizáljuk a sebességvektort, hogy ugyanolyan gyorsak legyenek
                var velocity = Vector3D.Normalize(new Vector3D<float>(velX, velY, velZ));
                asteroidVelocities.Add(velocity);
            }

            float[] tableColor = [System.Drawing.Color.Azure.R/256f,
                                  System.Drawing.Color.Azure.G/256f,
                                  System.Drawing.Color.Azure.B/256f,
                                  1f];
            table = GlCube.CreateSquare(Gl, tableColor);

            glCubeRotating = GlCube.CreateCubeWithFaceColors(Gl, face1Color, face2Color, face3Color, face4Color, face5Color, face6Color);

            skyBox = GlCube.CreateInteriorCube(Gl, "");
        }

        private static void CheckCollisions()
        {
            float spaceshipRadius = 1.0f; // Űrhajó ütközési sugara

            // Ütközés a nappal
            float sunRadius = 58.0f; // Nap ütközési sugara
            float distanceToSun = (float)Math.Sqrt(spaceshipPosition.X * spaceshipPosition.X +
                                                  spaceshipPosition.Y * spaceshipPosition.Y +
                                                  spaceshipPosition.Z * spaceshipPosition.Z);

            if (distanceToSun < (spaceshipRadius + sunRadius))
            {
                ResetGame();
                return;
            }

            // Ütközés a Merkúr bolygókkal
            for (int i = 0; i < mercuryPositions.Count; i++)
            {
                var mercuryPos = mercuryPositions[i];
                float mercuryRadius = mercuryScales[i] * 2600.0f; // Méretezés alapján sugár

                float distance = Vector3D.Distance(spaceshipPosition, mercuryPos);

                if (distance < (spaceshipRadius + mercuryRadius))
                {
                    ResetGame();
                    return;
                }
            }

            // Ütközés az aszteroidákkal
            for (int i = 0; i < asteroidPositions.Count; i++)
            {
                var asteroidPos = asteroidPositions[i];
                float asteroidRadius = asteroidScales[i] * 500.0f; // Méretezés alapján sugár

                float distance = Vector3D.Distance(spaceshipPosition, asteroidPos);

                if (distance < (spaceshipRadius + asteroidRadius))
                {
                    ResetGame();
                    return;
                }
            }
        }

        private static void ResetGame()
        {
            // Űrhajó visszaállítása kezdő pozícióba
            spaceshipPosition = spaceshipStartPosition;
            spaceshipRotationY = 0f;

            // Kamera visszaállítása
            cameraDescriptor.OverridePosition = null;
            cameraDescriptor.Target = spaceshipPosition;

            // FirstPersonView kikapcsolása
            FirstPersonView = false;

            Console.WriteLine("ÜTKÖZÉS! Játék újraindítva.");
        }


        private static void Window_Closing()
        {
            spaceship.ReleaseGlObject();
            glCubeRotating.ReleaseGlObject();
            sun.ReleaseGlObject();
            mercury.ReleaseGlObject();
            asteroid.ReleaseGlObject();
        }

        private static unsafe void SetProjectionMatrix()
        {
            // Az utolsó paraméter (1000) a far plane - ez legyen nagyobb mint a skybox
            var projectionMatrix = Matrix4X4.CreatePerspectiveFieldOfView<float>(
                (float)Math.PI / 4f, 1024f / 768f, 0.1f, 200000f); // 1000f helyett 200000f

            int location = Gl.GetUniformLocation(program, ProjectionMatrixVariableName);
            if (location == -1)
            {
                throw new Exception($"{ViewMatrixVariableName} uniform not found on shader.");
            }

            Gl.UniformMatrix4(location, 1, false, (float*)&projectionMatrix);
            CheckError();
        }

        private static unsafe void SetViewMatrix()
        {
            var viewMatrix = Matrix4X4.CreateLookAt(cameraDescriptor.Position, cameraDescriptor.Target, cameraDescriptor.UpVector);
            int location = Gl.GetUniformLocation(program, ViewMatrixVariableName);

            if (location == -1)
            {
                throw new Exception($"{ViewMatrixVariableName} uniform not found on shader.");
            }

            Gl.UniformMatrix4(location, 1, false, (float*)&viewMatrix);
            CheckError();
        }

        public static void CheckError()
        {
            var error = (ErrorCode)Gl.GetError();
            if (error != ErrorCode.NoError)
                throw new Exception("GL.GetError() returned " + error.ToString());
        }
    }
}