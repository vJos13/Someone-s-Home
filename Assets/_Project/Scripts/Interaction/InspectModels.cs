using TMPro;
using UnityEngine;

namespace SomeonesHome
{
    /// <summary>
    /// Modelos de las pistas y objetos del inventario, armados por código. La foto y el reloj de la casa son parte
    /// de la malla combinada de la cocina (no se pueden sacar sueltos), así que la versión "en mano" se construye aquí.
    /// Convención: el frente del modelo mira hacia -Z (hacia quien lo sostiene) y el origen está en su centro.
    /// </summary>
    public static class InspectModels
    {
        static Shader lit;
        static TMP_FontAsset font;

        /// <summary>Fuente para los textos 3D (la del HUD, para que todo use la misma tipografía).</summary>
        public static TMP_FontAsset Font
        {
            get
            {
                if (font) return font;
                var hud = HUD.Instance;
                var t = hud ? hud.GetComponentInChildren<TMP_Text>(true) : null;
                font = t ? t.font : TMP_Settings.defaultFontAsset;
                return font;
            }
        }

        // ════════════════════════ PIEZAS ════════════════════════
        public static Material Mat(Color color, float smoothness = 0.3f, float metallic = 0f, Texture tex = null)
        {
            if (!lit) lit = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(lit) { name = "M_EnMano (runtime)" };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (tex) m.SetTexture("_BaseMap", tex);
            return m;
        }

        /// <summary>Papel fotográfico / papel de la nota: un poco de luz propia para que se lea en cualquier escena.</summary>
        static Material Paper(Texture tex, Color tint, float glow, float smoothness)
        {
            var m = Mat(tint, smoothness, 0f, tex);
            m.EnableKeyword("_EMISSION");
            m.SetTexture("_EmissionMap", tex);
            m.SetColor("_EmissionColor", tint * glow);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            return m;
        }

        static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat,
                               Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static TextMeshPro Label(Transform parent, string text, Vector3 pos, float size, Color color, float width, float height,
                                 FontStyles style = FontStyles.Normal, TextAlignmentOptions align = TextAlignmentOptions.Center,
                                 Vector3 euler = default)
        {
            var go = new GameObject("Texto", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            var t = go.AddComponent<TextMeshPro>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.rectTransform.sizeDelta = new Vector2(width, height);
            return t;
        }

        static Transform Root(string name)
        {
            return new GameObject(name).transform;
        }

        /// <summary>Copia solo lo visual de un objeto de la escena (malla + materiales), centrado en el origen.</summary>
        public static GameObject CloneVisual(Renderer source, string name)
        {
            var root = Root(name);
            if (!source || !source.TryGetComponent<MeshFilter>(out var mf) || !mf.sharedMesh) return root.gameObject;
            var go = new GameObject("Malla");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = source.sharedMaterials;
            // Misma orientación que en la escena (la linterna queda acostada a lo largo, como sobre la mesada).
            go.transform.localRotation = source.transform.rotation;
            go.transform.localScale = source.transform.lossyScale;
            go.transform.localPosition = -go.GetComponent<MeshRenderer>().bounds.center;   // la raíz está en el origen
            return root.gameObject;
        }

        // ════════════════════════ PISTAS ════════════════════════
        /// <summary>La foto vieja: la familia de Tammy en la orilla del río… y alguien muy alto entre los árboles.</summary>
        public static GameObject Photo()
        {
            var root = Root("Inspeccion_Foto");
            var wood = Mat(new Color(0.3f, 0.18f, 0.1f), 0.35f);
            Part(PrimitiveType.Cube, "Marco", root, Vector3.zero, new Vector3(0.27f, 0.205f, 0.016f), wood);
            Part(PrimitiveType.Quad, "Foto", root, new Vector3(0f, 0f, -0.0085f), new Vector3(0.235f, 0.169f, 1f),
                 Paper(PhotoTexture(), Color.white, 0.45f, 0.5f));
            // Detrás, con lápiz: lo que se ve al girarla.
            Label(root, "<i>El río, agosto.\nTodos juntos.</i>", new Vector3(0f, 0f, 0.0085f), 0.22f, new Color(0.25f, 0.22f, 0.2f),
                  0.22f, 0.12f, FontStyles.Normal, TextAlignmentOptions.Center, new Vector3(0f, 180f, 0f));
            return root.gameObject;
        }

        /// <summary>La nota del refri, con letra de adulto.</summary>
        public static GameObject FridgeNote()
        {
            var root = Root("Inspeccion_Nota");
            Part(PrimitiveType.Cube, "Papel", root, Vector3.zero, new Vector3(0.15f, 0.2f, 0.0015f),
                 Paper(RuledPaper(), new Color(0.93f, 0.9f, 0.8f), 0.3f, 0.1f));
            Part(PrimitiveType.Cylinder, "Iman", root, new Vector3(0.0f, 0.085f, -0.004f), new Vector3(0.028f, 0.004f, 0.028f),
                 Mat(new Color(0.55f, 0.1f, 0.08f), 0.5f), new Vector3(90f, 0f, 0f));
            Label(root,
                  "Cuando oscurezca:\n<b>cerrojo puesto.</b>\n\nNo le abran a nadie.\n\n<u>Aunque sea alguien conocido.</u>\n\n<size=70%>— papá</size>",
                  new Vector3(0f, -0.008f, -0.0012f), 0.13f, new Color(0.1f, 0.14f, 0.3f), 0.125f, 0.16f, FontStyles.Italic);
            return root.gameObject;
        }

        /// <summary>El reloj de la pared, parado en las 3:07.</summary>
        public static GameObject Clock()
        {
            var root = Root("Inspeccion_Reloj");
            var wood = Mat(new Color(0.2f, 0.12f, 0.07f), 0.4f);
            var ink = Mat(new Color(0.06f, 0.06f, 0.06f), 0.3f);
            Part(PrimitiveType.Cylinder, "Aro", root, Vector3.zero, new Vector3(0.3f, 0.012f, 0.3f), wood, new Vector3(90f, 0f, 0f));
            Part(PrimitiveType.Cylinder, "Esfera", root, new Vector3(0f, 0f, -0.0125f), new Vector3(0.26f, 0.001f, 0.26f),
                 Mat(new Color(0.86f, 0.82f, 0.72f), 0.2f), new Vector3(90f, 0f, 0f));
            for (int i = 0; i < 12; i++)
            {
                var tick = new GameObject("Marca").transform;
                tick.SetParent(root, false);
                tick.localEulerAngles = new Vector3(0f, 0f, -i * 30f);
                bool major = i % 3 == 0;
                Part(PrimitiveType.Cube, "Raya", tick, new Vector3(0f, 0.112f, -0.0145f),
                     new Vector3(major ? 0.008f : 0.004f, major ? 0.022f : 0.014f, 0.002f), ink);
            }
            string[] nums = { "12", "3", "6", "9" };
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f * Mathf.Deg2Rad;
                Label(root, nums[i], new Vector3(Mathf.Sin(a) * 0.082f, Mathf.Cos(a) * 0.082f, -0.0157f), 0.3f,
                      new Color(0.08f, 0.08f, 0.08f), 0.05f, 0.04f);
            }
            // Agujas: el ángulo va en sentido horario desde las 12 (en el modelo, rotación negativa en Z).
            Hand(root, "Horario", 93.5f, 0.058f, 0.009f, ink, -0.0163f);      // 3 h + 7 min → 93,5°
            Hand(root, "Minutero", 42f, 0.09f, 0.006f, ink, -0.0172f);         // 7 min → 42°
            Hand(root, "Segundero", 252f, 0.095f, 0.002f, Mat(new Color(0.55f, 0.08f, 0.06f), 0.3f), -0.0181f);
            Part(PrimitiveType.Cylinder, "Eje", root, new Vector3(0f, 0f, -0.0185f), new Vector3(0.012f, 0.0015f, 0.012f), ink,
                 new Vector3(90f, 0f, 0f));
            return root.gameObject;
        }

        static void Hand(Transform root, string name, float clockwiseDeg, float length, float width, Material mat, float z)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(root, false);
            pivot.localEulerAngles = new Vector3(0f, 0f, -clockwiseDeg);
            Part(PrimitiveType.Cube, "Aguja", pivot, new Vector3(0f, length * 0.4f, z), new Vector3(width, length, 0.0015f), mat);
        }

        /// <summary>El collar de Buster: el del pasto, con la placa grabada.</summary>
        public static GameObject Collar(Renderer source)
        {
            var root = CloneVisual(source, "Inspeccion_Collar");
            // La placa cuelga del frente del aro (calculado con el tamaño real del collar).
            var b = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var r in root.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            var anchor = new Vector3(0f, b.min.y + b.size.y * 0.35f, b.min.z + 0.004f);
            var tagPivot = new GameObject("Placa").transform;
            tagPivot.SetParent(root.transform, false);
            tagPivot.localPosition = anchor + new Vector3(0f, -0.028f, -0.006f);
            tagPivot.localEulerAngles = new Vector3(58f, 0f, 0f);        // compensa la inclinación de la inspección: queda de frente
            Part(PrimitiveType.Cylinder, "Disco", tagPivot, Vector3.zero, new Vector3(0.06f, 0.0015f, 0.06f),
                 Mat(new Color(0.78f, 0.75f, 0.68f), 0.75f, 0.9f), new Vector3(90f, 0f, 0f));
            Part(PrimitiveType.Cylinder, "Argolla", root.transform, anchor + new Vector3(0f, -0.005f, -0.004f), new Vector3(0.012f, 0.004f, 0.012f),
                 Mat(new Color(0.6f, 0.58f, 0.52f), 0.7f, 0.9f));
            Label(tagPivot, "<b>BUSTER</b>", new Vector3(0f, 0f, -0.0018f), 0.1f, new Color(0.12f, 0.12f, 0.12f), 0.06f, 0.024f);
            return root;
        }

        /// <summary>Las huellas donde cambian: pezuña… y después pie descalzo. Copia las de la escena con su tierra.</summary>
        public static GameObject Footprints(Transform printsRoot, Vector3 center, float radius)
        {
            var root = Root("Inspeccion_Huellas");
            var content = new GameObject("Contenido").transform;
            content.SetParent(root, false);
            // Se copian con la raíz todavía en el origen y sin girar (mundo = local); después se inclina todo junto.
            if (printsRoot)
                foreach (Transform print in printsRoot)
                {
                    if ((print.position - center).sqrMagnitude > radius * radius) continue;
                    var copy = Object.Instantiate(print.gameObject, content);
                    copy.transform.SetPositionAndRotation(print.position - center, print.rotation);
                    copy.transform.localScale = print.lossyScale;
                }
            Part(PrimitiveType.Cube, "Tierra", content, new Vector3(0f, -0.012f, 0f), new Vector3(radius * 0.85f, 0.01f, radius * 1.75f),
                 Mat(new Color(0.42f, 0.33f, 0.22f), 0.15f, 0f, DirtTexture()));
            // De pie en el suelo → inclinado hacia quien mira.
            content.localEulerAngles = new Vector3(-62f, 0f, 0f);
            return root.gameObject;
        }

        // ════════════════════════ OBJETOS DEL INVENTARIO ════════════════════════
        public static GameObject ShedKey()
        {
            var root = Root("Llave del cobertizo");
            var brass = Mat(new Color(0.72f, 0.56f, 0.28f), 0.7f, 0.95f);
            Part(PrimitiveType.Cylinder, "Cabeza", root, new Vector3(-0.028f, 0f, 0f), new Vector3(0.026f, 0.0015f, 0.026f), brass,
                 new Vector3(90f, 0f, 0f));
            Part(PrimitiveType.Cube, "Cana", root, new Vector3(0.008f, 0f, 0f), new Vector3(0.05f, 0.0055f, 0.0025f), brass);
            Part(PrimitiveType.Cube, "Diente1", root, new Vector3(0.022f, -0.0055f, 0f), new Vector3(0.005f, 0.006f, 0.0025f), brass);
            Part(PrimitiveType.Cube, "Diente2", root, new Vector3(0.03f, -0.0045f, 0f), new Vector3(0.004f, 0.004f, 0.0025f), brass);
            // Etiqueta de cartón atada al aro.
            var cardboard = Mat(new Color(0.8f, 0.72f, 0.55f), 0.1f);
            Part(PrimitiveType.Cube, "Etiqueta", root, new Vector3(-0.056f, -0.012f, 0f), new Vector3(0.03f, 0.018f, 0.001f), cardboard,
                 new Vector3(0f, 0f, 20f));
            Label(root, "<b>COBERTIZO</b>", new Vector3(-0.056f, -0.012f, -0.0007f), 0.05f, new Color(0.15f, 0.1f, 0.08f),
                  0.03f, 0.012f, FontStyles.Normal, TextAlignmentOptions.Center, new Vector3(0f, 0f, 20f));
            return root.gameObject;
        }

        public static GameObject Padlock()
        {
            var root = Root("Candado");
            var steel = Mat(new Color(0.5f, 0.5f, 0.48f), 0.65f, 0.9f);
            var brass = Mat(new Color(0.62f, 0.48f, 0.24f), 0.55f, 0.9f);
            Part(PrimitiveType.Cube, "Cuerpo", root, new Vector3(0f, -0.018f, 0f), new Vector3(0.045f, 0.04f, 0.018f), brass);
            Part(PrimitiveType.Cylinder, "ArcoIzq", root, new Vector3(-0.014f, 0.014f, 0f), new Vector3(0.006f, 0.014f, 0.006f), steel);
            Part(PrimitiveType.Cylinder, "ArcoDer", root, new Vector3(0.014f, 0.014f, 0f), new Vector3(0.006f, 0.014f, 0.006f), steel);
            Part(PrimitiveType.Cube, "ArcoSup", root, new Vector3(0f, 0.028f, 0f), new Vector3(0.034f, 0.006f, 0.006f), steel);
            Part(PrimitiveType.Cylinder, "Ojo", root, new Vector3(0f, -0.024f, -0.0095f), new Vector3(0.008f, 0.0008f, 0.008f),
                 Mat(new Color(0.08f, 0.07f, 0.06f), 0.3f), new Vector3(90f, 0f, 0f));
            return root.gameObject;
        }

        /// <summary>Celular del jugador. La pantalla (hijo "Pantalla") se enciende con SetPhoneScreen.</summary>
        /// <summary>Celular de 1998 (de barra): carcasa gris oscura, antena corta, pantalla LCD verde y teclado.</summary>
        public static GameObject PhoneModel()
        {
            var root = Root("Celular");
            var plastic = Mat(new Color(0.12f, 0.13f, 0.15f), 0.35f, 0.05f);
            var keyMat = Mat(new Color(0.22f, 0.24f, 0.27f), 0.5f);
            var dark = Mat(new Color(0.03f, 0.03f, 0.035f), 0.6f);
            // Carcasa: cuerpo + "frente" un poco más angosto (bisel) + antena.
            Part(PrimitiveType.Cube, "Cuerpo", root, Vector3.zero, new Vector3(0.048f, 0.118f, 0.022f), plastic);
            Part(PrimitiveType.Cube, "Frente", root, new Vector3(0f, 0.004f, -0.0112f), new Vector3(0.042f, 0.104f, 0.002f),
                 Mat(new Color(0.08f, 0.085f, 0.1f), 0.45f));
            Part(PrimitiveType.Cylinder, "Antena", root, new Vector3(0.015f, 0.068f, 0.002f), new Vector3(0.009f, 0.012f, 0.009f), dark);
            Part(PrimitiveType.Cube, "Auricular", root, new Vector3(0f, 0.05f, -0.0125f), new Vector3(0.014f, 0.0025f, 0.001f), dark);
            // Pantalla LCD: verde, chica, hundida en un marco negro.
            Part(PrimitiveType.Cube, "MarcoPantalla", root, new Vector3(0f, 0.028f, -0.0122f), new Vector3(0.034f, 0.03f, 0.0008f), dark);
            var screen = Part(PrimitiveType.Quad, "Pantalla", root, new Vector3(0f, 0.028f, -0.01265f), new Vector3(0.029f, 0.024f, 1f),
                              Mat(new Color(0.42f, 0.5f, 0.3f), 0.2f));
            SetPhoneScreen(screen.GetComponent<Renderer>(), false);
            // Tecla de navegación, dos de llamada y 4×3 numéricas.
            Part(PrimitiveType.Cube, "Navegar", root, new Vector3(0f, 0.004f, -0.0125f), new Vector3(0.018f, 0.006f, 0.0018f), keyMat);
            Part(PrimitiveType.Cube, "Verde", root, new Vector3(-0.014f, 0.004f, -0.0124f), new Vector3(0.007f, 0.005f, 0.0016f),
                 Mat(new Color(0.16f, 0.32f, 0.18f), 0.5f));
            Part(PrimitiveType.Cube, "Roja", root, new Vector3(0.014f, 0.004f, -0.0124f), new Vector3(0.007f, 0.005f, 0.0016f),
                 Mat(new Color(0.4f, 0.12f, 0.1f), 0.5f));
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 3; c++)
                    Part(PrimitiveType.Cube, "Tecla", root, new Vector3((c - 1) * 0.0115f, -0.008f - r * 0.0105f, -0.0123f),
                         new Vector3(0.009f, 0.0072f, 0.0016f), keyMat);
            return root.gameObject;
        }

        public static void SetPhoneScreen(Renderer screen, bool on)
        {
            if (!screen) return;
            var m = screen.sharedMaterial;
            m.EnableKeyword("_EMISSION");
            // Retroiluminación verde del LCD (apagada: el verde grisáceo del cristal líquido sin luz).
            m.SetColor("_EmissionColor", on ? new Color(0.45f, 0.75f, 0.3f) * 1.4f : new Color(0.04f, 0.05f, 0.03f));
        }

        // ════════════════════════ TEXTURAS ════════════════════════
        /// <summary>Tierra húmeda: manchas, grumos y briznas secas.</summary>
        static Texture2D DirtTexture()
        {
            const int n = 128;
            var px = new Color32[n * n];
            var rng = new System.Random(19);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float big = Mathf.PerlinNoise(x * 0.05f, y * 0.05f);
                float small = Mathf.PerlinNoise(x * 0.3f + 40f, y * 0.3f);
                float l = 0.55f + big * 0.35f + small * 0.2f + ((float)rng.NextDouble() - 0.5f) * 0.12f;
                if (rng.NextDouble() < 0.012) l += 0.35f;                     // piedritas / briznas claras
                px[y * n + x] = new Color(l, l * 0.93f, l * 0.82f);
            }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "T_Tierra (runtime)" };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        static Texture2D RuledPaper()
        {
            const int w = 128, h = 170;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float line = (y % 14 == 3) && y < h - 20 ? 0.12f : 0f;
                float margin = x == 18 ? 0.18f : 0f;
                float n = Mathf.PerlinNoise(x * 0.08f, y * 0.08f) * 0.05f;
                var c = new Color(0.95f - n - line * 0.3f, 0.93f - n - line * 0.2f - margin * 0.4f, 0.84f - n + line * 0.15f - margin * 0.4f);
                px[y * w + x] = c;
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "T_Papel (runtime)", wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        /// <summary>
        /// Foto analógica vieja (sepia, grano, viñeta, rayas): árboles al fondo, el río, la familia en la orilla…
        /// y, entre los árboles de la derecha, una figura demasiado alta y demasiado flaca.
        /// </summary>
        public static Texture2D PhotoTexture()
        {
            const int W = 512, H = 368;
            var px = new Color[W * H];
            var rng = new System.Random(307);

            // Gente en la orilla: (x, altura, ancho de hombros)
            var family = new (float x, float hgt, float w)[] { (176, 92, 12), (204, 86, 11), (230, 58, 9), (250, 52, 8), (290, 90, 12) };
            const float baseY = 66f;

            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)W, v = y / (float)H;
                // Cielo lavado
                float lum = Mathf.Lerp(0.68f, 0.86f, v);
                // Bosque del otro lado: borde superior irregular y troncos verticales
                float treeTop = 0.66f + 0.05f * Mathf.Sin(u * 23f) + 0.035f * Mathf.Sin(u * 61f + 1f) + 0.02f * Mathf.Sin(u * 151f + 2f)
                              + 0.05f * (Mathf.PerlinNoise(u * 9f, 1.3f) - 0.5f);
                if (v < treeTop && v > 0.4f)
                {
                    float trunks = Mathf.Abs(Mathf.Sin(u * 210f + Mathf.PerlinNoise(u * 30f, v * 3f) * 4f));
                    lum = 0.22f + 0.07f * Mathf.PerlinNoise(u * 40f, v * 25f) + (trunks > 0.93f ? 0.07f : 0f);
                    lum += Mathf.SmoothStep(0f, 1f, (v - (treeTop - 0.04f)) / 0.04f) * 0.15f;   // borde más claro
                }
                // Río: banda clara con reflejos horizontales
                else if (v <= 0.4f && v > 0.25f)
                {
                    float streak = Mathf.PerlinNoise(u * 3f, v * 120f);
                    lum = 0.52f + streak * 0.18f + (v > 0.385f ? -0.08f : 0f);
                }
                // Orilla de este lado (pasto)
                else if (v <= 0.25f)
                    lum = 0.4f + 0.08f * Mathf.PerlinNoise(u * 60f, v * 60f) - (0.25f - v) * 0.4f;

                // La familia (siluetas oscuras, un poco movidas)
                foreach (var f in family)
                {
                    float top = baseY + f.hgt;
                    float shoulderY = top - f.w * 2.1f;
                    float head = Circle(x, y, f.x, top - f.w * 0.75f, f.w * 0.72f);
                    float body = Capsule(x, y, f.x, baseY + f.hgt * 0.42f, f.x, shoulderY, f.w * 0.55f);
                    float shoulders = Capsule(x, y, f.x - f.w * 0.55f, shoulderY, f.x + f.w * 0.55f, shoulderY, f.w * 0.32f);
                    float arms = Mathf.Min(Capsule(x, y, f.x - f.w * 0.85f, shoulderY, f.x - f.w * 1.0f, baseY + f.hgt * 0.45f, f.w * 0.2f),
                                           Capsule(x, y, f.x + f.w * 0.85f, shoulderY, f.x + f.w * 1.0f, baseY + f.hgt * 0.45f, f.w * 0.2f));
                    float legs = Mathf.Min(Capsule(x, y, f.x - f.w * 0.3f, baseY, f.x - f.w * 0.25f, baseY + f.hgt * 0.45f, f.w * 0.24f),
                                           Capsule(x, y, f.x + f.w * 0.3f, baseY, f.x + f.w * 0.25f, baseY + f.hgt * 0.45f, f.w * 0.24f));
                    body = Mathf.Min(body, Mathf.Min(shoulders, arms));
                    float d = Mathf.Min(head, Mathf.Min(body, legs));
                    lum = Mathf.Lerp(lum, 0.14f, Cover(d, 1.4f));
                }

                // La figura: entre los árboles de la derecha, pálida, demasiado alta, brazos hasta las rodillas.
                const float fx = 404f, fBase = 150f;
                float fig = Mathf.Min(
                    Mathf.Min(Capsule(x, y, fx, fBase + 58f, fx, fBase + 122f, 3.8f),                            // torso
                              Ellipse(x, y, fx + 1f, fBase + 135f, 5.5f, 8.5f)),                                 // cabeza
                    Mathf.Min(
                        Mathf.Min(Capsule(x, y, fx - 3.5f, fBase, fx - 2f, fBase + 60f, 1.9f),                  // piernas
                                  Capsule(x, y, fx + 3.5f, fBase, fx + 2f, fBase + 60f, 1.9f)),
                        Mathf.Min(Capsule(x, y, fx - 5f, fBase + 118f, fx - 11f, fBase + 40f, 1.6f),            // brazos
                                  Capsule(x, y, fx + 5f, fBase + 118f, fx + 10f, fBase + 38f, 1.6f))));
                float figCover = Cover(fig, 1.8f);
                // Un tronco delante le tapa medio cuerpo.
                float trunk = Cover(Capsule(x, y, fx - 15f, fBase - 20f, fx - 13f, H, 4.2f), 1.6f);
                lum = Mathf.Lerp(lum, 0.66f, figCover * (1f - trunk));
                if (trunk > 0f && v > 0.4f) lum = Mathf.Lerp(lum, 0.17f + 0.04f * Mathf.PerlinNoise(x * 0.3f, y * 0.05f), trunk);

                px[y * W + x] = new Color(lum, lum, lum);
            }

            // Analógico: desenfoque leve, sepia, viñeta, grano, rayas y blancos lavados.
            px = Blur(px, W, H);
            const float scratchX1 = 138f, scratchX2 = 300f;
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)W - 0.5f, v = y / (float)H - 0.5f;
                float l = px[y * W + x].r;
                l *= 1f - (u * u + v * v) * 1.3f;                                      // viñeta
                l += ((float)rng.NextDouble() - 0.5f) * 0.09f;                          // grano
                if (Mathf.Abs(x - scratchX1 - y * 0.05f) < 0.7f || Mathf.Abs(x - scratchX2 + y * 0.03f) < 0.6f) l += 0.18f;
                l = Mathf.Lerp(0.08f, 0.92f, Mathf.Clamp01(l));                         // negros y blancos lavados
                px[y * W + x] = new Color(l * 1.06f, l * 0.93f, l * 0.74f);            // sepia
            }

            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "T_FotoRio (runtime)", wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels(px);
            tex.Apply(true, true);
            return tex;
        }

        static Color[] Blur(Color[] src, int w, int h)
        {
            var dst = new Color[src.Length];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float s = 0f; int n = 0;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int xx = Mathf.Clamp(x + dx, 0, w - 1), yy = Mathf.Clamp(y + dy, 0, h - 1);
                    s += src[yy * w + xx].r; n++;
                }
                float l = s / n;
                dst[y * w + x] = new Color(l, l, l);
            }
            return dst;
        }

        // Distancias con signo en píxeles (negativo = adentro).
        static float Circle(float x, float y, float cx, float cy, float r) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;

        static float Ellipse(float x, float y, float cx, float cy, float rx, float ry)
        {
            float dx = (x - cx) / rx, dy = (y - cy) / ry;
            return (Mathf.Sqrt(dx * dx + dy * dy) - 1f) * Mathf.Min(rx, ry);
        }

        static float Capsule(float x, float y, float ax, float ay, float bx, float by, float r)
        {
            float pax = x - ax, pay = y - ay, bax = bx - ax, bay = by - ay;
            float h = Mathf.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
            float dx = pax - bax * h, dy = pay - bay * h;
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }

        /// <summary>Cobertura con borde suave de 'soft' píxeles.</summary>
        static float Cover(float d, float soft) => Mathf.Clamp01(0.5f - d / soft);
    }
}
