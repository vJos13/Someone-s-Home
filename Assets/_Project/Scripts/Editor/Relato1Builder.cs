using System.Collections.Generic;
using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace SomeonesHome.EditorTools
{
    /// <summary>
    /// Construye el GREYBOX completo del Relato 1 ("Across the River") en Assets/_Project/Scenes/Hito3_Main.unity.
    ///
    /// Lo ejecuta el asistente (Unity Pipeline) o por CLI:
    ///   -executeMethod SomeonesHome.EditorTools.Relato1Builder.BuildFromCommandLine
    ///
    /// ⚠️ Sobrescribe la escena. Úsenlo para generar la base; después el equipo la viste a mano
    /// (reemplazar primitivas por modelos, pintar terreno, bake de luces, etc.) y NO vuelvan a ejecutarlo.
    /// </summary>
    public static class Relato1Builder
    {
        const string ScenePath = "Assets/_Project/Scenes/Hito3_Main.unity";
        const string MatDir = "Assets/_Project/Art/Materials/Greybox";
        const string TexDir = "Assets/_Project/Art/Textures/Greybox";
        const string TerrainDir = "Assets/_Project/Art/Terrain";
        const string VolumeDir = "Assets/_Project/Rendering/VolumeProfiles";

        // ── Medidas del mundo (metros) ─────────────────────────────────
        const float W = 200f;            // ancho/largo del terreno
        const float H = 40f;             // altura máxima del terreno
        const float Plateau = 20f;       // campo y casa
        const float Bank = 6f;           // orillas del río
        const float SlopeTop = 108f;     // z donde empieza la colina
        const float SlopeBottom = 120f;  // z donde termina (14 m de caída en 12 m ≈ 49°)
        const float RiverNear = 133f, RiverFar = 147f;
        const float WaterY = 4.6f;

        static readonly Dictionary<string, Material> mats = new();

        public static void BuildFromCommandLine()
        {
            Build();
            EditorApplication.Exit(0);
        }

        public static void Build()
        {
            EnsureDirs();
            mats.Clear();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lighting = Group("--- LIGHTING ---");
            var env = Group("--- ENVIRONMENT ---");
            var dressing = Group("Narrative_SetDressing");
            var narrative = Group("--- NARRATIVE ---");
            var audio = Group("--- AUDIO ---");
            var cams = Group("--- CAMERAS ---");

            // ── Terreno, río, colina ──
            var terrain = BuildTerrain(env.transform);
            BuildRiver(env.transform);
            var rockPath = BuildRockPath(env.transform, terrain);
            BuildTrees(env.transform, terrain);
            BuildBoundaries(env.transform);

            // ── Casa ──
            var house = BuildHouse(env.transform, out Door backDoor, dressing.transform);

            // ── Columpio, hermano, criatura ──
            var swingSeat = BuildSwing(dressing.transform, out Vector3 swingPos);
            var brother = BuildBrother(narrative.transform, swingPos);
            var creature = BuildCreature(narrative.transform, terrain);

            // ── Luz y post-procesamiento ──
            var sun = BuildLighting(lighting.transform, rockPath[0]);
            var (baseVol, tensionVol) = BuildVolumes(lighting.transform);

            // ── Jugador y cámaras ──
            var player = BuildPlayer(cams.transform, house);
            var reveal = BuildRevealCamera(cams.transform, brother.transform, creature);

            // ── Audio ──
            var crickets = BuildAudio(audio.transform, swingPos, out var voiceField, out var mimicRiver, out var swingCreak, out var scream, brother);

            // ── UI ──
            BuildHUD();

            // ── Tensión ──
            var tensionGO = new GameObject("TensionManager");
            tensionGO.transform.SetParent(narrative.transform);
            var tm = tensionGO.AddComponent<TensionManager>();
            Set(tm, "tensionVolume", tensionVol);
            Set(tm, "sun", sun);
            Set(tm, "crickets", crickets);
            SetGradient(tm, "sunColor", new Color(1f, 0.63f, 0.36f), new Color(0.85f, 0.35f, 0.32f));
            SetGradient(tm, "fogColor", new Color(0.78f, 0.58f, 0.46f), new Color(0.42f, 0.3f, 0.36f));

            // ── Triggers ──
            var tField = Trigger(narrative.transform, "T_Campo", new Vector3(90, Plateau + 2, 72), new Vector3(70, 6, 4));
            var tHill = Trigger(narrative.transform, "T_BordeColina", new Vector3(85, Plateau + 2, SlopeTop - 4), new Vector3(70, 6, 5));
            var tSwing = Trigger(narrative.transform, "T_DetrasDelColumpio", new Vector3(swingPos.x, Bank + 1.5f, swingPos.z - 4.5f), new Vector3(16, 5, 6));
            var tClimb = Trigger(narrative.transform, "T_SubiendoRocas", rockPath[1] + Vector3.up, new Vector3(6, 4, 6));
            var tHouse = Trigger(narrative.transform, "T_EntrarCasa", house + new Vector3(2, 1.5f, 2.2f), new Vector3(8, 3, 3));

            // ── Director ──
            var dirGO = new GameObject("Level1Director");
            dirGO.transform.SetParent(narrative.transform);
            var dir = dirGO.AddComponent<Level1Director>();
            Set(dir, "player", player);
            Set(dir, "backDoor", backDoor);
            Set(dir, "fieldTrigger", tField);
            Set(dir, "hillTopTrigger", tHill);
            Set(dir, "swingTrigger", tSwing);
            Set(dir, "climbTrigger", tClimb);
            Set(dir, "houseReturnTrigger", tHouse);
            Set(dir, "brother", brother);
            Set(dir, "creature", creature);
            Set(dir, "voiceFromField", voiceField);
            Set(dir, "mimicFromRiver", mimicRiver);
            Set(dir, "swingCreak", swingCreak);
            Set(dir, "brotherScream", scream);
            Set(dir, "swingSeat", swingSeat);
            Set(dir, "revealShot", reveal);

            // Camino de huida de Sam: orilla → rocas → casa.
            var pathRoot = new GameObject("SamRunPath").transform;
            pathRoot.SetParent(narrative.transform);
            var pts = new List<Vector3> { rockPath[2], rockPath[1], rockPath[0], house + new Vector3(2, 0, 5.5f) };
            var pathTs = new List<Object>();
            for (int i = 0; i < pts.Count; i++)
            {
                var p = new GameObject($"P{i}").transform;
                p.SetParent(pathRoot);
                p.position = pts[i];
                pathTs.Add(p);
            }
            SetArray(dir, "brotherRunPath", pathTs);

            CharacterSetup.FaceNpcsToPlayer();
            NatureSetup.ConfigureModels();
            NatureSetup.Populate();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[Relato1Builder] Greybox del Relato 1 construido en " + ScenePath);
        }

        // ════════════════════════════════════════════════════════════════
        //  TERRENO
        // ════════════════════════════════════════════════════════════════
        static float HeightAt(float x, float z)
        {
            float h;
            if (z < SlopeTop) h = Plateau;
            else if (z < SlopeBottom) h = Mathf.Lerp(Plateau, Bank, (z - SlopeTop) / (SlopeBottom - SlopeTop));
            else if (z < RiverNear) h = Bank;
            else if (z < RiverFar)
            {
                float t = (z - RiverNear) / (RiverFar - RiverNear);
                h = Bank - Mathf.Sin(t * Mathf.PI) * 4.5f;
            }
            else if (z < 175f) h = Bank;
            else h = Mathf.Lerp(Bank, 8.5f, Mathf.SmoothStep(0, 1, (z - 175f) / 25f)); // bajas: la cosa se recorta contra el cielo

            // Lomas detrás de la casa y a los lados (límites naturales, sin muros invisibles visibles).
            if (z < 35f) h = Mathf.Max(h, Mathf.Lerp(30f, Plateau, Mathf.SmoothStep(0, 1, z / 35f)));
            float edge = Mathf.Min(x, W - x);
            if (edge < 30f) h = Mathf.Max(h, Mathf.Lerp(h + 16f, h, Mathf.SmoothStep(0, 1, edge / 30f)));

            // Ruido suave: solo en zonas planas lejos de la colina (la colina debe ser lineal para las rocas).
            bool onSlope = z > SlopeTop - 2f && z < SlopeBottom + 1f;
            if (!onSlope)
            {
                float n = Mathf.PerlinNoise(x * 0.04f, z * 0.04f) - 0.5f;
                float amp = edge < 30f ? 4f : z > 175f ? 1.5f : (z < SlopeTop ? 0.5f : 0.25f);
                h += n * amp;
            }
            return h;
        }

        static Terrain BuildTerrain(Transform parent)
        {
            var data = new TerrainData { heightmapResolution = 257, alphamapResolution = 256 };
            data.size = new Vector3(W, H, W);
            int res = data.heightmapResolution;
            var heights = new float[res, res];
            for (int iz = 0; iz < res; iz++)
            for (int ix = 0; ix < res; ix++)
            {
                float x = ix / (float)(res - 1) * W, z = iz / (float)(res - 1) * W;
                heights[iz, ix] = Mathf.Clamp01(HeightAt(x, z) / H);
            }
            data.SetHeights(0, 0, heights);

            var grass = Layer("TL_Pasto", new Color(0.42f, 0.42f, 0.2f), new Color(0.55f, 0.5f, 0.26f), 0.1f);
            var dirt = Layer("TL_TierraRoca", new Color(0.36f, 0.3f, 0.24f), new Color(0.45f, 0.4f, 0.33f), 0.15f);
            var mud = Layer("TL_Orilla", new Color(0.3f, 0.27f, 0.2f), new Color(0.38f, 0.36f, 0.26f), 0.25f);
            data.terrainLayers = new[] { grass, dirt, mud };

            int ar = data.alphamapResolution;
            var alpha = new float[ar, ar, 3];
            for (int iz = 0; iz < ar; iz++)
            for (int ix = 0; ix < ar; ix++)
            {
                float nx = ix / (float)(ar - 1), nz = iz / (float)(ar - 1);
                float steep = data.GetSteepness(nx, nz);
                float h = data.GetInterpolatedHeight(nx, nz);
                float wDirt = Mathf.InverseLerp(25f, 40f, steep);
                float wMud = h < Bank + 0.6f ? 1f - wDirt : 0f;
                float wGrass = Mathf.Max(0f, 1f - wDirt - wMud);
                alpha[iz, ix, 0] = wGrass; alpha[iz, ix, 1] = wDirt; alpha[iz, ix, 2] = wMud;
            }
            data.SetAlphamaps(0, 0, alpha);

            AssetDatabase.CreateAsset(data, $"{TerrainDir}/TD_Relato01.asset");
            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain_Rio";
            go.transform.SetParent(parent);
            var t = go.GetComponent<Terrain>();
            var tmat = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
            AssetDatabase.CreateAsset(tmat, $"{MatDir}/M_Terrain.mat");
            t.materialTemplate = tmat;
            t.basemapDistance = 120f;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return t;
        }

        static TerrainLayer Layer(string name, Color a, Color b, float smooth)
        {
            var tex = NoiseTexture(name, a, b);
            var layer = new TerrainLayer { diffuseTexture = tex, tileSize = new Vector2(6, 6), smoothness = smooth };
            AssetDatabase.CreateAsset(layer, $"{TerrainDir}/{name}.terrainlayer");
            return layer;
        }

        static Texture2D NoiseTexture(string name, Color a, Color b)
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.09f, y * 0.09f) * 0.6f + Mathf.PerlinNoise(x * 0.4f, y * 0.4f) * 0.4f;
                tex.SetPixel(x, y, Color.Lerp(a, b, n));
            }
            tex.Apply();
            string path = $"{TexDir}/T_{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ════════════════════════════════════════════════════════════════
        //  RÍO, ROCAS, ÁRBOLES, LÍMITES
        // ════════════════════════════════════════════════════════════════
        static void BuildRiver(Transform parent)
        {
            var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "Water_Rio (asignar shader de agua propio)";
            water.transform.SetParent(parent);
            water.transform.position = new Vector3(W / 2, WaterY, (RiverNear + RiverFar) / 2);
            water.transform.localScale = new Vector3(W / 10f, 1, (RiverFar - RiverNear + 2) / 10f);
            Object.DestroyImmediate(water.GetComponent<Collider>());
            water.GetComponent<Renderer>().sharedMaterial = Mat("M_Water", new Color(0.12f, 0.17f, 0.2f), 0.92f);
            water.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>
        /// Sendero de rocas en zigzag por la colina. La colina es más empinada que el slopeLimit del jugador,
        /// así que la ÚNICA forma de bajar caminando es por aquí (fiel al relato).
        /// Devuelve [inicio arriba, descanso, final abajo].
        /// </summary>
        static Vector3[] BuildRockPath(Transform parent, Terrain terrain)
        {
            var root = new GameObject("RockPath (sendero de rocas)").transform;
            root.SetParent(parent);
            float zMid = (SlopeTop + SlopeBottom) / 2f;
            Vector3 a = new(68f, 0, SlopeTop - 0.5f);
            Vector3 b = new(100f, 0, zMid);
            Vector3 c = new(80f, 0, SlopeBottom + 0.8f);
            a.y = HeightAt(a.x, SlopeTop) + 0.12f;
            b.y = HeightAt(b.x, zMid) + 0.12f;
            c.y = Bank + 0.12f;

            var rockMat = Mat("M_Roca", new Color(0.42f, 0.4f, 0.37f), 0.15f);
            var rng = new System.Random(7);
            void Segment(Vector3 from, Vector3 to)
            {
                float len = Vector3.Distance(new Vector3(from.x, 0, from.z), new Vector3(to.x, 0, to.z));
                int count = Mathf.CeilToInt(len / 0.85f); // ~0.25-0.3 m de subida por roca
                Vector3 flatDir = new Vector3(to.x - from.x, 0, to.z - from.z).normalized;
                for (int i = 0; i <= count; i++)
                {
                    float t = i / (float)count;
                    Vector3 p = Vector3.Lerp(from, to, t);
                    float jitterYaw = (float)(rng.NextDouble() - 0.5) * 18f;
                    float w = 2.1f + (float)rng.NextDouble() * 0.5f;
                    var rock = Cube(root, $"Roca_{root.childCount:00}", p + Vector3.down * 2f,
                        new Vector3(w, 4f, 1.5f), rockMat);
                    rock.transform.rotation = Quaternion.LookRotation(flatDir) * Quaternion.Euler(0, jitterYaw, 0);
                    rock.transform.position = new Vector3(p.x, p.y - 2f, p.z);
                    Static(rock);
                }
            }
            // Zigzag: el giro lo forman las dos rocas del vértice (misma altura), sin escalón extra.
            Segment(a, b);
            Segment(b, c);
            CarveTrail(terrain, a, b);
            CarveTrail(terrain, b, c);
            return new[] { a, b, c };
        }

        /// <summary>
        /// Talla una "cornisa" en la colina a lo largo del sendero: el terreno queda por debajo de las rocas
        /// en un corredor de halfWidth metros, para que el muro de la pendiente no roce al jugador al subir.
        /// </summary>
        static void CarveTrail(Terrain terrain, Vector3 from, Vector3 to, float halfWidth = 1.9f)
        {
            var data = terrain.terrainData;
            int res = data.heightmapResolution;
            float[,] h = data.GetHeights(0, 0, res, res);
            Vector2 a = new(from.x, from.z), b = new(to.x, to.z);
            Vector2 ab = b - a;
            for (int iz = 0; iz < res; iz++)
            for (int ix = 0; ix < res; ix++)
            {
                Vector2 p = new(ix / (float)(res - 1) * W, iz / (float)(res - 1) * W);
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                float dist = Vector2.Distance(p, a + ab * t);
                if (dist > halfWidth + 1.2f) continue;
                float top = Mathf.Lerp(from.y, to.y, t) - 0.35f;              // bajo la superficie de las rocas
                float blend = Mathf.InverseLerp(halfWidth + 1.2f, halfWidth, dist); // borde suave
                float cur = h[iz, ix] * H;
                if (cur > top) h[iz, ix] = Mathf.Lerp(cur, top, blend) / H;
            }
            data.SetHeights(0, 0, h);
        }

        static void BuildTrees(Transform parent, Terrain terrain)
        {
            var root = new GameObject("Trees").transform;
            root.SetParent(parent);
            var trunk = Mat("M_Tronco", new Color(0.25f, 0.18f, 0.12f), 0.1f);
            var leaves = Mat("M_Follaje", new Color(0.16f, 0.22f, 0.12f), 0.05f);
            var rng = new System.Random(3);

            void Tree(Vector3 pos, float scale)
            {
                pos.y = terrain.SampleHeight(pos);
                var t = new GameObject("Arbol").transform;
                t.SetParent(root);
                t.position = pos;
                var tr = Cyl(t, "Tronco", pos + Vector3.up * 2.5f * scale, new Vector3(0.35f, 2.5f, 0.35f) * scale, trunk);
                var c1 = Sphere(t, "Copa", pos + Vector3.up * 5.5f * scale, new Vector3(3f, 4.5f, 3f) * scale, leaves);
                var c2 = Sphere(t, "Copa2", pos + Vector3.up * 7.8f * scale, new Vector3(2f, 3f, 2f) * scale, leaves);
                tr.transform.SetParent(t, true); c1.transform.SetParent(t, true); c2.transform.SetParent(t, true);
                Object.DestroyImmediate(c1.GetComponent<Collider>());
                Object.DestroyImmediate(c2.GetComponent<Collider>());
                // LOD: de lejos se dibuja solo la copa grande; muy lejos se recorta.
                var lod = t.gameObject.AddComponent<LODGroup>();
                lod.SetLODs(new[]
                {
                    new LOD(0.12f, new Renderer[] { tr.GetComponent<Renderer>(), c1.GetComponent<Renderer>(), c2.GetComponent<Renderer>() }),
                    new LOD(0.02f, new Renderer[] { c1.GetComponent<Renderer>() }),
                });
                Static(t.gameObject);
            }

            // Weenie: árbol enorme y solitario donde empieza el sendero de rocas.
            Tree(new Vector3(64f, 0, SlopeTop - 3f), 1.8f);

            for (int i = 0; i < 26; i++) // al otro lado del río, dejando libre el fondo de la criatura (x 82–114)
            {
                float x = 20 + (float)rng.NextDouble() * 160f;
                if (x > 82f && x < 114f) x = x < 98f ? x - 32f : x + 32f;
                Tree(new Vector3(x, 0, 160f + (float)rng.NextDouble() * 30f), 0.8f + (float)rng.NextDouble() * 0.6f);
            }
            for (int i = 0; i < 14; i++) // bordes del campo
            {
                float x = rng.NextDouble() > 0.5 ? 25f + (float)rng.NextDouble() * 20f : 155f + (float)rng.NextDouble() * 20f;
                Tree(new Vector3(x, 0, 40f + (float)rng.NextDouble() * 60f), 0.8f + (float)rng.NextDouble() * 0.5f);
            }
            for (int i = 0; i < 8; i++) // orilla, lejos del columpio
            {
                float x = rng.NextDouble() > 0.5 ? 30f + (float)rng.NextDouble() * 30f : 130f + (float)rng.NextDouble() * 40f;
                Tree(new Vector3(x, 0, 123f + (float)rng.NextDouble() * 8f), 0.7f + (float)rng.NextDouble() * 0.4f);
            }
        }

        static void BuildBoundaries(Transform parent)
        {
            var root = new GameObject("Limites (invisibles)").transform;
            root.SetParent(parent);
            void Wall(string n, Vector3 c, Vector3 s)
            {
                var g = new GameObject(n);
                g.transform.SetParent(root);
                g.transform.position = c;
                g.AddComponent<BoxCollider>().size = s;
            }
            Wall("Orilla_NoCruzarRio", new Vector3(W / 2, Bank + 2f, RiverNear + 0.5f), new Vector3(W, 6, 1));
            Wall("Borde_Oeste", new Vector3(18, 20, W / 2), new Vector3(1, 60, W));
            Wall("Borde_Este", new Vector3(W - 18, 20, W / 2), new Vector3(1, 60, W));
            Wall("Borde_Sur", new Vector3(W / 2, 25, 30), new Vector3(W, 60, 1));
        }

        // ════════════════════════════════════════════════════════════════
        //  CASA (greybox) — devuelve el centro del piso
        // ════════════════════════════════════════════════════════════════
        static Vector3 BuildHouse(Transform parent, out Door backDoor, Transform dressing)
        {
            var root = new GameObject("Casa").transform;
            root.SetParent(parent);
            Vector3 c = new(90f, Plateau + 0.35f, 58f); // un poco elevada: el ruido del terreno no atraviesa el piso
            float wx = 12f, wz = 9f, wh = 3f, th = 0.2f;
            var wall = Mat("M_ParedCasa", new Color(0.72f, 0.66f, 0.56f), 0.2f);
            var floor = Mat("M_PisoMadera", new Color(0.4f, 0.28f, 0.18f), 0.35f);
            var roof = Mat("M_Techo", new Color(0.3f, 0.22f, 0.2f), 0.2f);

            Static(Cube(root, "Piso", c + new Vector3(0, -0.6f, 0), new Vector3(wx, 1.2f, wz), floor));
            Static(Cube(root, "Escalon_Trasero", new Vector3(c.x + 2f, c.y - 0.45f, c.z + wz / 2 + 0.6f), new Vector3(1.6f, 0.9f, 1.2f), floor));
            Static(Cube(root, "Techo", c + new Vector3(0, wh + 0.1f, 0), new Vector3(wx + 1f, 0.2f, wz + 1f), roof));
            Static(Cube(root, "Pared_Oeste", c + new Vector3(-wx / 2, wh / 2, 0), new Vector3(th, wh, wz), wall));
            Static(Cube(root, "Pared_Este", c + new Vector3(wx / 2, wh / 2, 0), new Vector3(th, wh, wz), wall));
            Static(Cube(root, "Pared_Frente", c + new Vector3(0, wh / 2, -wz / 2), new Vector3(wx, wh, th), wall));

            // Pared trasera con hueco de puerta (x = +2) y ventana (x = -3).
            float zb = c.z + wz / 2, doorX = c.x + 2f, doorW = 1.1f, doorH = 2.2f;
            Static(Cube(root, "PTrasera_Izq", new Vector3((c.x - wx / 2 + doorX - doorW / 2) / 2, c.y + wh / 2, zb), new Vector3(doorX - doorW / 2 - (c.x - wx / 2), wh, th), wall));
            Static(Cube(root, "PTrasera_Der", new Vector3((doorX + doorW / 2 + c.x + wx / 2) / 2, c.y + wh / 2, zb), new Vector3(c.x + wx / 2 - (doorX + doorW / 2), wh, th), wall));
            Static(Cube(root, "PTrasera_Dintel", new Vector3(doorX, c.y + (doorH + wh) / 2, zb), new Vector3(doorW, wh - doorH, th), wall));

            // Puerta trasera: pivote en la bisagra.
            var hinge = new GameObject("PuertaTrasera (bisagra)").transform;
            hinge.SetParent(root);
            hinge.position = new Vector3(doorX - doorW / 2, c.y, zb);
            var panel = Cube(hinge, "Panel", new Vector3(doorX, c.y + doorH / 2, zb), new Vector3(doorW - 0.05f, doorH - 0.05f, 0.08f),
                Mat("M_Puerta", new Color(0.33f, 0.22f, 0.14f), 0.3f));
            backDoor = hinge.gameObject.AddComponent<Door>();
            SetValue(backDoor, "openAngle", -105f);

            // Cocina cálida (capa 1: establecimiento).
            var wood = Mat("M_Mueble", new Color(0.45f, 0.32f, 0.2f), 0.3f);
            var cloth = Mat("M_Mantel", new Color(0.7f, 0.25f, 0.2f), 0.1f);
            Vector3 table = c + new Vector3(-1.5f, 0, 0.5f);
            Static(Cube(dressing, "Mesa", table + Vector3.up * 0.75f, new Vector3(1.8f, 0.08f, 1f), wood));
            Static(Cube(dressing, "Mantel", table + Vector3.up * 0.8f, new Vector3(1.9f, 0.02f, 1.1f), cloth));
            for (int i = 0; i < 4; i++)
            {
                Vector3 off = new((i % 2 == 0 ? -0.6f : 0.6f), 0.45f, (i < 2 ? -0.8f : 0.8f));
                var chair = Cube(dressing, $"Silla_{i}", table + off, new Vector3(0.45f, 0.9f, 0.45f), wood);
                chair.transform.rotation = Quaternion.Euler(0, Random.Range(-12f, 12f), 0); // tip de Miyamoto: nada alineado
                Static(chair);
            }
            Static(Cube(dressing, "Mesada", c + new Vector3(-wx / 2 + 0.4f, 0.45f, 0), new Vector3(0.7f, 0.9f, 5f), wood));
            // 4 platos servidos = hay cena esperando.
            var plate = Mat("M_Plato", new Color(0.9f, 0.9f, 0.85f), 0.7f);
            for (int i = 0; i < 4; i++)
                Cyl(dressing, $"Plato_{i}", table + new Vector3(i % 2 == 0 ? -0.5f : 0.5f, 0.82f, i < 2 ? -0.28f : 0.28f), new Vector3(0.28f, 0.01f, 0.28f), plate);

            // Tammy y Emily (greybox).
            NPC(dressing, "Tammy", c + new Vector3(-wx / 2 + 1.2f, 0, -1f), new Color(0.45f, 0.3f, 0.45f), 1.7f);
            NPC(dressing, "Emily", table + new Vector3(1.35f, 0, -0.1f), new Color(0.3f, 0.45f, 0.35f), 1.55f);

            // Lámpara (accent cálido = refugio).
            var lampGO = new GameObject("Lampara_Cocina");
            lampGO.transform.SetParent(root);
            lampGO.transform.position = table + Vector3.up * 2.5f;
            var lamp = lampGO.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.72f, 0.45f);
            lamp.intensity = 4f;
            lamp.range = 9f;
            lamp.lightmapBakeType = LightmapBakeType.Mixed;
            lamp.shadows = LightShadows.None;
            var bulb = Sphere(root, "Foco", table + Vector3.up * 2.55f, Vector3.one * 0.18f, Emissive("M_Foco", new Color(1f, 0.75f, 0.45f), 3f));
            Object.DestroyImmediate(bulb.GetComponent<Collider>());
            EnvironmentSetup.SpawnHouse(root, c, hinge, dressing);   // modelo de Blender si existe
            return c;
        }

        static void NPC(Transform parent, string name, Vector3 feet, Color color, float height)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent);
            root.position = feet;
            root.rotation = Quaternion.Euler(0, 90, 0); // miran hacia el centro de la cocina
            if (CharacterSetup.SpawnHuman(root, name)) return;   // modelo de Blender si existe
            var m = Mat($"M_NPC_{name}", color, 0.2f);
            Capsule(root, "Cuerpo", feet + Vector3.up * height * 0.42f, new Vector3(0.5f, height * 0.42f, 0.4f), m);
            Sphere(root, "Cabeza", feet + Vector3.up * (height - 0.12f), Vector3.one * 0.26f, Mat("M_Piel", new Color(0.8f, 0.62f, 0.5f), 0.3f));
        }

        // ════════════════════════════════════════════════════════════════
        //  COLUMPIO, SAM, CRIATURA
        // ════════════════════════════════════════════════════════════════
        static Transform BuildSwing(Transform parent, out Vector3 swingPos)
        {
            swingPos = new Vector3(92f, Bank, 127f);
            var root = new GameObject("Columpio (glider 3 asientos)").transform;
            root.SetParent(parent);
            root.position = swingPos;
            var metal = Mat("M_MetalPintado", new Color(0.32f, 0.36f, 0.3f), 0.5f);
            var wood = Mat("M_MaderaGastada", new Color(0.5f, 0.42f, 0.32f), 0.2f);
            float top = 2.3f, half = 1.4f;
            foreach (float sx in new[] { -half, half })
            {
                var l1 = Cyl(root, "Pata", swingPos + new Vector3(sx, top / 2, -0.55f), new Vector3(0.08f, top / 2 + 0.05f, 0.08f), metal);
                l1.transform.rotation = Quaternion.Euler(-14, 0, 0);
                var l2 = Cyl(root, "Pata", swingPos + new Vector3(sx, top / 2, 0.55f), new Vector3(0.08f, top / 2 + 0.05f, 0.08f), metal);
                l2.transform.rotation = Quaternion.Euler(14, 0, 0);
            }
            var bar = Cyl(root, "Barra", swingPos + Vector3.up * top, new Vector3(0.08f, half + 0.1f, 0.08f), metal);
            bar.transform.rotation = Quaternion.Euler(0, 0, 90);

            var seatPivot = new GameObject("SwingSeat (pivote)").transform;
            seatPivot.SetParent(root);
            seatPivot.position = swingPos + Vector3.up * top;
            Cube(seatPivot, "Asiento", swingPos + new Vector3(0, 0.55f, 0), new Vector3(2.3f, 0.08f, 0.55f), wood);
            Cube(seatPivot, "Respaldo", swingPos + new Vector3(0, 0.85f, -0.27f), new Vector3(2.3f, 0.55f, 0.06f), wood);
            foreach (float sx in new[] { -1.05f, 1.05f })
                Cyl(seatPivot, "Cadena", swingPos + new Vector3(sx, top - 0.85f, 0), new Vector3(0.03f, 0.85f, 0.03f), metal);
            EnvironmentSetup.SpawnSwing(root, seatPivot);            // modelo de Blender si existe
            return seatPivot;
        }

        static BrotherNPC BuildBrother(Transform parent, Vector3 swingPos)
        {
            var root = new GameObject("Sam (hermano)").transform;
            root.SetParent(parent);
            root.position = swingPos + new Vector3(0.6f, 0, 0.05f);
            root.gameObject.layer = 2; // Ignore Raycast: no tapa la vista hacia la criatura
            var body = new GameObject("Body").transform;
            body.SetParent(root, false);
            var hoodie = Mat("M_NPC_Sam", new Color(0.2f, 0.28f, 0.45f), 0.2f);
            var b = Capsule(body, "Torso", root.position + Vector3.up * 0.75f, new Vector3(0.45f, 0.75f, 0.35f), hoodie);
            var h = Sphere(body, "Cabeza", root.position + Vector3.up * 1.6f, Vector3.one * 0.25f, Mat("M_Piel", new Color(0.8f, 0.62f, 0.5f), 0.3f));
            Object.DestroyImmediate(b.GetComponent<Collider>());
            Object.DestroyImmediate(h.GetComponent<Collider>());

            var sit = new GameObject("SitPoint").transform;
            sit.SetParent(parent);
            sit.SetPositionAndRotation(root.position, Quaternion.identity);
            var stand = new GameObject("StandPoint").transform;
            stand.SetParent(parent);
            stand.position = swingPos + new Vector3(0.6f, 0, 1.3f);

            var npc = root.gameObject.AddComponent<BrotherNPC>();
            Set(npc, "body", body);
            Set(npc, "sitPoint", sit);
            Set(npc, "standPoint", stand);
            if (CharacterSetup.SpawnSam(body) is { } poses)
            {
                Set(npc, "sittingPose", poses[0]);
                Set(npc, "standingPose", poses[1]);
                Set(npc, "runningPose", poses[2]);
            }
            return npc;
        }

        /// <summary>Figura de "venado" erguida, ~3 m. Greybox: el equipo la reemplaza por un modelo + su shader.</summary>
        static RiverCreature BuildCreature(Transform parent, Terrain terrain)
        {
            Vector3 pos = new(97f, 0, 156f);
            pos.y = terrain.SampleHeight(pos);
            var root = new GameObject("LaCosa (criatura)").transform;
            root.SetParent(parent);
            root.position = pos;
            root.rotation = Quaternion.Euler(0, 180, 0); // mira hacia la casa
            var vis = new GameObject("Visual").transform;
            vis.SetParent(root, false);

            var skin = Mat("M_Creature (asignar shader propio)", new Color(0.23f, 0.19f, 0.16f), 0.25f);
            var dark = Mat("M_CreatureDark", new Color(0.1f, 0.08f, 0.07f), 0.2f);
            Vector3 P(float x, float y, float z) => root.TransformPoint(new Vector3(x, y, z));

            // Patas traseras de venado (muy delgadas, de pie).
            foreach (float sx in new[] { -0.2f, 0.2f })
            {
                Cyl(vis, "Muslo", P(sx, 1.25f, 0.05f), new Vector3(0.16f, 0.4f, 0.16f), skin);
                Cyl(vis, "Canilla", P(sx, 0.45f, -0.08f), new Vector3(0.07f, 0.45f, 0.07f), dark);
            }
            Capsule(vis, "Torso", P(0, 2.0f, 0), new Vector3(0.55f, 0.6f, 0.4f), skin);
            // Brazos/patas delanteras colgando, demasiado largos.
            foreach (float sx in new[] { -0.36f, 0.36f })
            {
                var arm = Cyl(vis, "Brazo", P(sx, 1.65f, 0.05f), new Vector3(0.07f, 0.65f, 0.07f), dark);
                arm.transform.localRotation = Quaternion.Euler(0, 0, sx > 0 ? -6 : 6);
            }
            Cyl(vis, "Cuello", P(0, 2.7f, 0.06f), new Vector3(0.14f, 0.22f, 0.14f), skin);

            var head = new GameObject("Head").transform;
            head.SetParent(vis, false);
            head.position = P(0, 2.95f, 0.08f);
            head.rotation = root.rotation;
            Cube(head, "Craneo", head.TransformPoint(new Vector3(0, 0.05f, 0.12f)), new Vector3(0.24f, 0.26f, 0.42f), skin);
            Cube(head, "Hocico", head.TransformPoint(new Vector3(0, -0.02f, 0.38f)), new Vector3(0.15f, 0.14f, 0.2f), dark);
            foreach (float sx in new[] { -0.17f, 0.17f })
            {
                var ear = Cube(head, "Oreja", head.TransformPoint(new Vector3(sx, 0.2f, 0.02f)), new Vector3(0.06f, 0.2f, 0.1f), skin);
                ear.transform.localRotation = Quaternion.Euler(0, 0, sx > 0 ? -35 : 35);
                var antler = Cyl(head, "Asta", head.TransformPoint(new Vector3(sx * 0.6f, 0.42f, 0f)), new Vector3(0.03f, 0.25f, 0.03f), dark);
                antler.transform.localRotation = Quaternion.Euler(-10, 0, sx > 0 ? -20 : 20);
            }
            var eyeMat = Emissive("M_CreatureEyes", new Color(0.9f, 0.85f, 0.6f), 2.5f);
            foreach (float sx in new[] { -0.09f, 0.09f })
                Sphere(head, "Ojo", head.TransformPoint(new Vector3(sx, 0.08f, 0.33f)), Vector3.one * 0.045f, eyeMat);

            var jaw = new GameObject("Jaw (pivote)").transform;
            jaw.SetParent(head, false);
            jaw.localPosition = new Vector3(0, -0.06f, 0.22f);
            Cube(jaw, "Mandibula", jaw.TransformPoint(new Vector3(0, -0.05f, 0.14f)), new Vector3(0.13f, 0.05f, 0.22f), dark);

            foreach (var col in vis.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);

            var whisper = root.gameObject.AddComponent<AudioSource>();
            whisper.playOnAwake = false;
            whisper.loop = true;
            whisper.spatialBlend = 1f;
            whisper.minDistance = 8f;
            whisper.maxDistance = 60f;
            whisper.volume = 0.5f;
            var pa = root.gameObject.AddComponent<ProceduralAudio>();
            SetEnum(pa, "kind", (int)ProceduralAudio.Kind.Whisper);
            SetValue(pa, "seconds", 2.5f);
            SetValue(pa, "playOnStart", false);

            if (CharacterSetup.SpawnCreature(vis) is { } model) { head = model.head; jaw = model.jaw; }

            var rc = root.gameObject.AddComponent<RiverCreature>();
            Set(rc, "visualRoot", vis);
            Set(rc, "jaw", jaw);
            Set(rc, "head", head);
            Set(rc, "whisper", whisper);
            return rc;
        }

        // ════════════════════════════════════════════════════════════════
        //  LUZ Y POST-PROCESAMIENTO
        // ════════════════════════════════════════════════════════════════
        static Light BuildLighting(Transform parent, Vector3 pathStart)
        {
            // KEY: sol de atardecer DETRÁS de la criatura (contraluz → silueta).
            var sunGO = new GameObject("SOL (atardecer)");
            sunGO.transform.SetParent(parent);
            sunGO.transform.rotation = Quaternion.Euler(9f, 195f, 0f);
            var sun = sunGO.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.63f, 0.36f);
            sun.intensity = 1.6f;
            sun.shadows = LightShadows.Soft;
            sun.lightmapBakeType = LightmapBakeType.Mixed;

            // FILL: cielo + ambiente en gradiente (las sombras nunca negro puro).
            var sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunSize", 0.06f);
            sky.SetFloat("_AtmosphereThickness", 1.6f);
            sky.SetColor("_SkyTint", new Color(0.55f, 0.45f, 0.5f));
            sky.SetFloat("_Exposure", 1.1f);
            AssetDatabase.CreateAsset(sky, $"{MatDir}/M_SkyAtardecer.mat");
            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.42f, 0.55f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.38f, 0.32f);
            RenderSettings.ambientGroundColor = new Color(0.14f, 0.12f, 0.1f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.011f;
            RenderSettings.fogColor = new Color(0.78f, 0.58f, 0.46f);

            // ACCENT: luz cálida tenue sobre el inicio del sendero (la luz guía).
            var spotGO = new GameObject("Accent_InicioSendero");
            spotGO.transform.SetParent(parent);
            spotGO.transform.position = pathStart + new Vector3(-1.5f, 6f, -2f);
            spotGO.transform.LookAt(pathStart);
            var spot = spotGO.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = new Color(1f, 0.78f, 0.5f);
            spot.intensity = 18f;
            spot.range = 14f;
            spot.spotAngle = 50f;
            spot.shadows = LightShadows.None;
            spot.lightmapBakeType = LightmapBakeType.Baked;
            return sun;
        }

        static (Volume, Volume) BuildVolumes(Transform parent)
        {
            var baseProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(baseProfile, $"{VolumeDir}/VP_Relato01_Base.asset");
            var tone = Add<Tonemapping>(baseProfile); tone.mode.Override(TonemappingMode.ACES);
            var bloom = Add<Bloom>(baseProfile); bloom.intensity.Override(0.6f); bloom.threshold.Override(1.05f); bloom.scatter.Override(0.6f);
            var ca = Add<ColorAdjustments>(baseProfile);
            ca.postExposure.Override(0.15f); ca.contrast.Override(14f); ca.saturation.Override(-12f);
            ca.colorFilter.Override(new Color(1f, 0.93f, 0.84f));
            var vig = Add<Vignette>(baseProfile); vig.intensity.Override(0.28f); vig.smoothness.Override(0.45f);
            var grain = Add<FilmGrain>(baseProfile); grain.intensity.Override(0.18f); grain.type.Override(FilmGrainLookup.Thin2);

            var tensionProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(tensionProfile, $"{VolumeDir}/VP_Relato01_Tension.asset");
            var tca = Add<ColorAdjustments>(tensionProfile);
            tca.saturation.Override(-50f); tca.contrast.Override(22f); tca.postExposure.Override(-0.1f);
            tca.colorFilter.Override(new Color(0.82f, 0.86f, 1f));
            var tvig = Add<Vignette>(tensionProfile); tvig.intensity.Override(0.48f); tvig.smoothness.Override(0.6f);
            var chrom = Add<ChromaticAberration>(tensionProfile); chrom.intensity.Override(0.35f);
            var tgrain = Add<FilmGrain>(tensionProfile); tgrain.intensity.Override(0.45f);
            EditorUtility.SetDirty(baseProfile);
            EditorUtility.SetDirty(tensionProfile);

            Volume MakeVolume(string name, VolumeProfile p, float priority, float weight)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent);
                var v = go.AddComponent<Volume>();
                v.isGlobal = true;
                v.sharedProfile = p;
                v.priority = priority;
                v.weight = weight;
                return v;
            }
            return (MakeVolume("PostFX_Base", baseProfile, 0, 1f), MakeVolume("PostFX_Tension (peso = tensión)", tensionProfile, 1, 0f));
        }

        static T Add<T>(VolumeProfile p) where T : VolumeComponent
        {
            var c = p.Add<T>(false);
            c.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(c, p);
            return c;
        }

        // ════════════════════════════════════════════════════════════════
        //  JUGADOR, CÁMARAS
        // ════════════════════════════════════════════════════════════════
        static FirstPersonController BuildPlayer(Transform cams, Vector3 house)
        {
            var player = new GameObject("Player") { tag = "Player" };
            player.transform.position = house + new Vector3(1.2f, 0.05f, -2.2f);
            player.transform.rotation = Quaternion.Euler(0, -70, 0); // mirando a Tammy
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.75f; cc.radius = 0.3f; cc.center = new Vector3(0, 0.875f, 0);
            cc.slopeLimit = 40f; cc.stepOffset = 0.45f;

            var head = new GameObject("Head").transform;
            head.SetParent(player.transform, false);
            head.localPosition = new Vector3(0, 1.62f, 0);

            var fpsCam = new GameObject("VCam_FPS").AddComponent<CinemachineCamera>();
            fpsCam.transform.SetParent(head, false);
            fpsCam.Priority = 10;
            var fpsLens = LensSettings.Default;
            fpsLens.FieldOfView = 68f; fpsLens.NearClipPlane = 0.05f; fpsLens.FarClipPlane = 400f;
            fpsCam.Lens = fpsLens;

            var steps = player.AddComponent<AudioSource>();
            steps.playOnAwake = false; steps.spatialBlend = 0f; steps.volume = 0.5f;

            var fpc = player.AddComponent<FirstPersonController>();
            Set(fpc, "head", head);
            Set(fpc, "footstepSource", steps);
            var inter = player.AddComponent<Interactor>();
            Set(inter, "rayOrigin", head);

            // Main Camera con Brain (Cinemachine decide qué vcam manda).
            var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
            camGO.transform.SetParent(cams);
            var cam = camGO.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 400f;
            var urp = camGO.AddComponent<UniversalAdditionalCameraData>();
            urp.renderPostProcessing = true;
            urp.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            camGO.AddComponent<AudioListener>();
            var brain = camGO.AddComponent<CinemachineBrain>();
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 1.6f);
            return fpc;
        }

        static CinematicShot BuildRevealCamera(Transform cams, Transform brother, RiverCreature creature)
        {
            var go = new GameObject("VCam_NarrativeReveal");
            go.transform.SetParent(cams);
            Vector3 headPos = creature.transform.position + Vector3.up * 2.9f;
            // Por encima del hombro de Sam, con teleobjetivo: Sam en primer plano, la cosa al fondo.
            go.transform.position = brother.position + new Vector3(-0.9f, 1.9f, -1.6f);
            go.transform.rotation = Quaternion.LookRotation(headPos + Vector3.down * 0.9f - go.transform.position);
            var vcam = go.AddComponent<CinemachineCamera>();
            vcam.Priority = 0;
            var lens = LensSettings.Default;
            lens.FieldOfView = 24f; lens.NearClipPlane = 0.05f; lens.FarClipPlane = 400f;
            vcam.Lens = lens;

            var noiseProfile = AssetDatabase.LoadAssetAtPath<NoiseSettings>("Packages/com.unity.cinemachine/Presets/Noise/Handheld_normal_mild.asset");
            if (noiseProfile)
            {
                var noise = go.AddComponent<CinemachineBasicMultiChannelPerlin>();
                noise.NoiseProfile = noiseProfile;
                noise.AmplitudeGain = 0.6f;
                noise.FrequencyGain = 0.5f;
            }
            var shot = go.AddComponent<CinematicShot>();
            Set(shot, "vcam", vcam);
            return shot;
        }

        // ════════════════════════════════════════════════════════════════
        //  AUDIO (procedural de placeholder)
        // ════════════════════════════════════════════════════════════════
        static AudioSource BuildAudio(Transform parent, Vector3 swingPos, out AudioSource voiceField, out AudioSource mimicRiver,
            out AudioSource swingCreak, out AudioSource scream, BrotherNPC brother)
        {
            AudioSource Src(string name, Vector3 pos, ProceduralAudio.Kind kind, float seconds, bool loop, bool play, float vol, float blend, float maxDist = 80f, int seed = 1)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent);
                go.transform.position = pos;
                var s = go.AddComponent<AudioSource>();
                s.loop = loop; s.playOnAwake = false; s.volume = vol; s.spatialBlend = blend;
                s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = 4f; s.maxDistance = maxDist;
                var p = go.AddComponent<ProceduralAudio>();
                SetEnum(p, "kind", (int)kind);
                SetValue(p, "seconds", seconds);
                SetValue(p, "seed", seed);
                SetValue(p, "playOnStart", play);
                return s;
            }

            Src("AMB_Viento", Vector3.zero, ProceduralAudio.Kind.Wind, 12f, true, true, 0.22f, 0f);
            var crickets = Src("AMB_Grillos", Vector3.zero, ProceduralAudio.Kind.Crickets, 8f, true, true, 0.3f, 0f);
            for (int i = 0; i < 3; i++)
                Src($"AMB_Rio_{i}", new Vector3(50 + i * 50, WaterY, (RiverNear + RiverFar) / 2), ProceduralAudio.Kind.River, 7f, true, true, 0.7f, 1f, 60f, i + 2);
            swingCreak = Src("SFX_Columpio", swingPos + Vector3.up * 2f, ProceduralAudio.Kind.Creak, 4.8f, true, true, 0.45f, 1f, 25f);
            voiceField = Src("VO_VozDesdeElCampo (reemplazar por voz grabada)", new Vector3(swingPos.x - 4f, Plateau + 1.5f, SlopeTop - 1f),
                ProceduralAudio.Kind.Whisper, 1.6f, false, false, 1f, 1f, 70f, 11);
            mimicRiver = Src("VO_ImitaEmily (reemplazar por voz grabada)", new Vector3(swingPos.x + 6f, Bank, RiverNear - 1f),
                ProceduralAudio.Kind.Whisper, 2.2f, false, false, 1f, 1f, 70f, 12);

            var sGO = new GameObject("VO_GritoSam (asignar clip)");
            sGO.transform.SetParent(brother.transform, false);
            scream = sGO.AddComponent<AudioSource>();
            scream.playOnAwake = false; scream.spatialBlend = 1f;
            return crickets;
        }

        // ════════════════════════════════════════════════════════════════
        //  HUD (uGUI con fuente integrada, sin dependencias)
        // ════════════════════════════════════════════════════════════════
        static void BuildHUD()
        {
            var font = HUDSetup.LoadFont();
            var canvasGO = new GameObject("--- UI --- HUD");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            TMPro.TMP_Text MakeText(string name, Transform parent, TextAnchor anchor, int size, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = offMin; rt.offsetMax = offMax;
                var t = go.AddComponent<TMPro.TextMeshProUGUI>();
                HUDSetup.Style(t, font, size, anchor, new Color(0.95f, 0.93f, 0.88f));
                return t;
            }

            CanvasGroup Group(string name)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(canvasGO.transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
                var g = go.AddComponent<CanvasGroup>();
                g.blocksRaycasts = false; g.interactable = false;
                return g;
            }

            var objGroup = Group("Objetivo");
            var objective = MakeText("ObjetivoTexto", objGroup.transform, TextAnchor.UpperLeft, 30, new Vector2(0, 1), new Vector2(0.6f, 1), new Vector2(60, -140), new Vector2(0, -50));

            var crosshair = MakeText("Mira", canvasGO.transform, TextAnchor.MiddleCenter, 26, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-20, -20), new Vector2(20, 20));
            crosshair.text = "·";
            var prompt = MakeText("Prompt", canvasGO.transform, TextAnchor.UpperCenter, 26, new Vector2(0.3f, 0.5f), new Vector2(0.7f, 0.5f), new Vector2(0, -90), new Vector2(0, -30));

            var faderGO = new GameObject("Fader", typeof(RectTransform));
            faderGO.transform.SetParent(canvasGO.transform, false);
            var frt = (RectTransform)faderGO.transform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;
            var img = faderGO.AddComponent<Image>(); img.color = Color.black; img.raycastTarget = false;
            var fader = faderGO.AddComponent<CanvasGroup>(); fader.blocksRaycasts = false;

            var card = MakeText("Tarjeta", canvasGO.transform, TextAnchor.MiddleCenter, 36, new Vector2(0.15f, 0.2f), new Vector2(0.85f, 0.8f), Vector2.zero, Vector2.zero);
            card.color = new Color(0.95f, 0.93f, 0.88f, 0f);
            card.lineSpacing = 20f;

            var subGroup = Group("Subtitulos");
            var subtitle = MakeText("SubtituloTexto", subGroup.transform, TextAnchor.LowerCenter, 32, new Vector2(0.15f, 0), new Vector2(0.85f, 0.3f), new Vector2(0, 70), Vector2.zero);

            var hud = canvasGO.AddComponent<HUD>();
            Set(hud, "objectiveText", objective);
            Set(hud, "subtitleText", subtitle);
            Set(hud, "promptText", prompt);
            Set(hud, "cardText", card);
            Set(hud, "fader", fader);
            Set(hud, "subtitleGroup", subGroup);
            Set(hud, "objectiveGroup", objGroup);
        }

        // ════════════════════════════════════════════════════════════════
        //  UTILIDADES
        // ════════════════════════════════════════════════════════════════
        static void EnsureDirs()
        {
            foreach (var d in new[] { MatDir, TexDir, TerrainDir, VolumeDir })
                if (!Directory.Exists(d)) Directory.CreateDirectory(d);
            AssetDatabase.Refresh();
        }

        static GameObject Group(string name) => new(name);

        static NarrativeTrigger Trigger(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = center;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            var t = go.AddComponent<NarrativeTrigger>();
            SetValue(t, "id", name);
            return t;
        }

        static Material Mat(string name, Color color, float smoothness)
        {
            if (mats.TryGetValue(name, out var m)) return m;
            string safe = name.Split(' ')[0];
            string path = $"{MatDir}/{safe}.mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                // Solo al crearlo: si ya existe (p. ej. con texturas PBR aplicadas) no se pisa.
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.SetColor("_BaseColor", color);
                m.SetFloat("_Smoothness", smoothness);
                AssetDatabase.CreateAsset(m, path);
            }
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            mats[name] = m;
            return m;
        }

        static Material Emissive(string name, Color color, float intensity)
        {
            var m = Mat(name, color, 0.6f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * intensity);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return m;
        }

        static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        static GameObject Cube(Transform p, string n, Vector3 pos, Vector3 s, Material m) => Prim(PrimitiveType.Cube, p, n, pos, s, m);
        static GameObject Sphere(Transform p, string n, Vector3 pos, Vector3 s, Material m) => Prim(PrimitiveType.Sphere, p, n, pos, s, m);
        static GameObject Cyl(Transform p, string n, Vector3 pos, Vector3 s, Material m) => Prim(PrimitiveType.Cylinder, p, n, pos, s, m);
        static GameObject Capsule(Transform p, string n, Vector3 pos, Vector3 s, Material m) => Prim(PrimitiveType.Capsule, p, n, pos, s, m);

        static void Static(GameObject go)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>())
                GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                    StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic);
        }

        static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[Relato1Builder] Campo '{field}' no existe en {target.GetType().Name}"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetArray(Object target, string field, List<Object> values)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            p.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetValue(Object target, string field, float v) => Modify(target, field, p => p.floatValue = v);
        static void SetValue(Object target, string field, int v) => Modify(target, field, p => p.intValue = v);
        static void SetValue(Object target, string field, bool v) => Modify(target, field, p => p.boolValue = v);
        static void SetValue(Object target, string field, string v) => Modify(target, field, p => p.stringValue = v);
        static void SetEnum(Object target, string field, int index) => Modify(target, field, p => p.enumValueIndex = index);

        static void SetGradient(Object target, string field, Color from, Color to)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            Modify(target, field, p => p.gradientValue = g);
        }

        static void Modify(Object target, string field, System.Action<SerializedProperty> apply)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[Relato1Builder] Campo '{field}' no existe en {target.GetType().Name}"); return; }
            apply(p);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
