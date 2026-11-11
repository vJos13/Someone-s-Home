using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SomeonesHome
{
    /// <summary>
    /// Clima y ambientación, encima de <see cref="Atmosphere"/>:
    ///  · Nubes en el cielo (cúpula con shader propio) que viajan con el viento y toman el color del sol/luna.
    ///  · Sombras de nubes que cruzan el terreno (cookie de la luz principal).
    ///  · Viento con ráfagas, lluvia intermitente de noche (no entra a la casa: las gotas mueren al chocar),
    ///    relámpagos lejanos con trueno, neblina.
    ///  · Sonido: lluvia, truenos, pájaros; regula el viento y los grillos que ya trae la escena.
    /// Cada fase tiene un rango; dentro de él el clima cambia solo, despacio.
    /// </summary>
    [DefaultExecutionOrder(40)]          // antes que Atmosphere: lo que pone aquí se usa en el mismo cuadro
    public class Weather : MonoBehaviour
    {
        [System.Serializable]
        public struct Profile
        {
            public Vector2 cover, wind, rain;
            [Range(0, 1)] public float rainChance;
            public float fog;
        }

        [Header("Clima por fase (rangos)")]
        [SerializeField] Profile dusk = new() { cover = new(0.3f, 0.55f), wind = new(0.2f, 0.45f), rain = Vector2.zero, rainChance = 0f, fog = 1f };
        [SerializeField] Profile night = new() { cover = new(0.7f, 0.95f), wind = new(0.4f, 0.8f), rain = new(0.25f, 0.85f), rainChance = 0.65f, fog = 1.3f };
        [SerializeField] Profile morning = new() { cover = new(0.35f, 0.6f), wind = new(0.05f, 0.2f), rain = Vector2.zero, rainChance = 0f, fog = 1.15f };
        [Tooltip("Cada cuántos segundos (de juego) el clima elige un nuevo objetivo.")]
        [SerializeField] Vector2 changeEvery = new(45f, 100f);

        [Header("Efectos")]
        [SerializeField] bool clouds = true;
        [SerializeField] bool cloudShadows = true;
        [SerializeField] bool rainEnabled = true;
        [SerializeField] bool lightning = true;
        [SerializeField] int seed = 7;

        [Header("Sonido (volúmenes base)")]
        [SerializeField] float rainVolume = 0.5f;
        [SerializeField] float thunderVolume = 0.8f;
        [SerializeField] float birdsVolume = 0.25f;

        public static Weather Instance { get; private set; }
        public float Cover { get; private set; }
        public float Wind { get; private set; }          // con ráfagas
        public float Rain { get; private set; }
        public bool Indoors { get; private set; }
        public Vector2 WindDir { get; private set; } = new(1f, 0.3f);
        /// <summary>Cuánto cantan los grillos según la hora y la lluvia (lo aplica TensionManager, dueño de esa fuente).</summary>
        public float CricketFactor { get; private set; } = 1f;

        Atmosphere atmo;
        System.Random rngField;
        System.Random rng => rngField ??= new System.Random(seed);   // también se pierde al recargar scripts
        float coverT, windT, rainT, fogT = 1f, fog = 1f, nextChange, windAngle, indoor;
        NightMode.Phase lastPhase = (NightMode.Phase)(-1);

        // Nubes
        Transform dome;
        Material domeMat;
        Vector4 cloudOffset;

        // Sombras de nubes
        Texture2D cookie;
        float[] cookieNoise;
        float cookieCover = -1f;
        Vector2 cookieOffset;
        UniversalAdditionalLightData sunData;
        const int CookieRes = 128;
        const float CookieSize = 140f;

        // Lluvia
        ParticleSystem rain;
        Material rainMat;

        // Sonido
        AudioSource rainSrc, birdSrc, thunderSrc, windSrc;
        float windBase;
        float nextLightning;

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            atmo = GetComponent<Atmosphere>() ? GetComponent<Atmosphere>() : Atmosphere.Instance;
            windAngle = R(0f, 360f);
            if (clouds) BuildDome();
            if (cloudShadows) BuildCookie();
            if (rainEnabled) BuildRain();
            BuildAudio();
            nextLightning = R(30f, 70f);
        }

        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        float R(Vector2 v) => R(v.x, v.y);

        Profile Current => (atmo ? atmo.Phase : NightMode.Phase.Dusk) switch
        {
            NightMode.Phase.Night => night,
            NightMode.Phase.Morning => morning,
            _ => dusk,
        };

        void PickTargets(bool snap)
        {
            var p = Current;
            coverT = R(p.cover);
            windT = R(p.wind);
            rainT = rng.NextDouble() < p.rainChance ? R(p.rain) : 0f;
            fogT = p.fog * (1f + rainT * 0.5f);
            nextChange = R(changeEvery);
            if (snap) { Cover = coverT; Wind = windT; fog = fogT; Rain = rainT * 0.5f; }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            var phase = atmo ? atmo.Phase : NightMode.Phase.Dusk;
            if (phase != lastPhase)
            {
                // Al cambiar de fase hay un corte en la historia (tarjeta en negro): el clima puede saltar.
                PickTargets(true);
                lastPhase = phase;
            }
            nextChange -= dt;
            if (nextChange <= 0f) PickTargets(false);

            // Todo cambia despacio, como el clima real.
            Cover = Mathf.MoveTowards(Cover, coverT, dt * 0.006f);
            float windSlow = Mathf.MoveTowards(Wind, windT, dt * 0.01f);
            Rain = Mathf.MoveTowards(Rain, rainT, dt * (rainT > Rain ? 0.03f : 0.02f));
            fog = Mathf.MoveTowards(fog, fogT, dt * 0.01f);
            windAngle += (Mathf.PerlinNoise(Time.time * 0.01f, 3.3f) - 0.5f) * dt * 4f;
            WindDir = new Vector2(Mathf.Cos(windAngle * Mathf.Deg2Rad), Mathf.Sin(windAngle * Mathf.Deg2Rad));
            float gust = 0.55f + 0.9f * Mathf.PerlinNoise(Time.time * 0.12f, 7.1f) * Mathf.PerlinNoise(Time.time * 0.31f, 1.7f) * 1.6f;
            Wind = windSlow;
            float windNow = Mathf.Clamp01(windSlow * gust);

            // Interior: hay techo encima de la cámara.
            var cam = Camera.main;
            if (cam)
            {
                bool roof = Physics.Raycast(cam.transform.position + Vector3.up * 0.1f, Vector3.up, 30f, ~0, QueryTriggerInteraction.Ignore);
                indoor = Mathf.MoveTowards(indoor, roof ? 1f : 0f, dt * 2f);
                Indoors = indoor > 0.5f;
            }

            // ── Lo que recibe Atmosphere ──
            if (atmo)
            {
                bool isNight = phase == NightMode.Phase.Night;
                atmo.lightDimming = 1f - Cover * (isNight ? 0.5f : 0.25f) - Rain * 0.15f;   // la sombra local la pone el cookie
                atmo.ambientDimming = isNight ? 1f : 1f - Cover * 0.15f - Rain * 0.1f;   // de noche no: ya se ve poco
                atmo.fogMultiplier = fog;
            }

            UpdateDome(cam, dt, windNow);
            UpdateCookie(dt, windNow);
            UpdateRain(cam, windNow);
            UpdateLightning(dt, phase);
            UpdateAudio(windNow, phase);
        }

        // ════════════════════════════════════════════════════════════════
        //  NUBES
        // ════════════════════════════════════════════════════════════════
        void BuildDome()
        {
            var shader = Resources.Load<Shader>("Atmosphere/CloudDome");
            if (!shader || !shader.isSupported) { clouds = false; return; }
            domeMat = new Material(shader) { name = "M_Nubes (runtime)" };
            var go = new GameObject("Atmosfera_Nubes");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = Hemisphere(48, 16);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = domeMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            dome = go.transform;
            cloudOffset = new Vector4(R(0f, 50f), R(0f, 50f), R(0f, 50f), R(0f, 50f));
        }

        static Mesh Hemisphere(int lon, int lat)
        {
            var v = new Vector3[(lon + 1) * (lat + 1)];
            var tris = new int[lon * lat * 6];
            for (int j = 0; j <= lat; j++)
            {
                float t = Mathf.Lerp(-0.08f, 1f, j / (float)lat) * Mathf.PI * 0.5f;   // un poco bajo el horizonte
                for (int i = 0; i <= lon; i++)
                {
                    float p = i / (float)lon * Mathf.PI * 2f;
                    v[j * (lon + 1) + i] = new Vector3(Mathf.Cos(t) * Mathf.Cos(p), Mathf.Sin(t), Mathf.Cos(t) * Mathf.Sin(p));
                }
            }
            int k = 0;
            for (int j = 0; j < lat; j++)
            for (int i = 0; i < lon; i++)
            {
                int a = j * (lon + 1) + i, b = a + lon + 1;
                tris[k++] = a; tris[k++] = b; tris[k++] = a + 1;
                tris[k++] = a + 1; tris[k++] = b; tris[k++] = b + 1;
            }
            var m = new Mesh { name = "Atmosfera_Cupula", vertices = v, triangles = tris };
            m.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            return m;
        }

        void UpdateDome(Camera cam, float dt, float windNow)
        {
            if (!dome) return;
            if (cam)
            {
                dome.position = cam.transform.position;
                dome.localScale = Vector3.one * cam.farClipPlane * 0.85f;
            }
            // Las nubes altas van más lento (en ángulo) que las sombras en el suelo.
            Vector2 move = WindDir * (0.004f + windNow * 0.012f) * dt;
            cloudOffset += new Vector4(move.x, move.y, move.x * 0.4f, move.y * 0.4f);
            domeMat.SetVector("_Offset", cloudOffset);
            domeMat.SetFloat("_Cover", Mathf.Lerp(0.15f, 0.95f, Cover));
            domeMat.SetFloat("_Density", Mathf.Lerp(0.75f, 0.97f, Cover));

            Color amb = RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Flat
                ? RenderSettings.ambientLight : RenderSettings.ambientSkyColor;
            float sunI = 0f;
            Color sunC = Color.black;
            Vector3 toSun = Vector3.up;
            if (atmo && atmo.Sun)
            {
                sunI = Mathf.Clamp01(atmo.Sun.intensity / 1.6f);
                sunC = atmo.Sun.color;
                toSun = atmo.ToLight;
            }
            float flash = atmo ? atmo.flash : 0f;
            var lit = amb * 0.85f + sunC * sunI * 0.6f + new Color(0.7f, 0.75f, 0.9f) * flash;
            var shade = amb * Mathf.Lerp(0.6f, 0.4f, Cover) + RenderSettings.fogColor * 0.2f + new Color(0.4f, 0.45f, 0.6f) * flash;
            lit.a = shade.a = 1f;
            domeMat.SetColor("_LitColor", lit);
            domeMat.SetColor("_ShadeColor", shade);
            domeMat.SetColor("_SunColor", sunC * sunI);
            domeMat.SetVector("_SunDir", toSun);
            domeMat.SetColor("_HorizonColor", RenderSettings.fogColor);
        }

        // ════════════════════════════════════════════════════════════════
        //  SOMBRAS DE NUBES (cookie de la luz principal)
        // ════════════════════════════════════════════════════════════════
        void BuildCookie()
        {
            if (!atmo || !atmo.Sun) { cloudShadows = false; return; }
            cookieNoise = new float[CookieRes * CookieRes];
            var lattice = new float[64 * 64];
            for (int i = 0; i < lattice.Length; i++) lattice[i] = (float)rng.NextDouble();
            float Value(float x, float y, int period)
            {
                int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
                float fx = x - x0, fy = y - y0;
                fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
                float L(int a, int b) => lattice[((b % period + period) % period) * 64 + ((a % period + period) % period)];
                return Mathf.Lerp(Mathf.Lerp(L(x0, y0), L(x0 + 1, y0), fx), Mathf.Lerp(L(x0, y0 + 1), L(x0 + 1, y0 + 1), fx), fy);
            }
            for (int y = 0; y < CookieRes; y++)
            for (int x = 0; x < CookieRes; x++)
            {
                float u = x / (float)CookieRes, w = y / (float)CookieRes, v = 0f, amp = 0.5f;
                for (int o = 0, period = 4; o < 4; o++, period *= 2, amp *= 0.5f)
                    v += amp * Value(u * period, w * period, period);   // periódico: el patrón se repite sin costura
                cookieNoise[y * CookieRes + x] = v / 0.9375f;
            }
            cookie = new Texture2D(CookieRes, CookieRes, TextureFormat.RGBA32, true)
            {
                name = "Atmosfera_SombrasNubes", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear,
            };
            sunData = atmo.Sun.GetUniversalAdditionalLightData();
            atmo.Sun.cookie = cookie;
            sunData.lightCookieSize = new Vector2(CookieSize, CookieSize);
        }

        void UpdateCookie(float dt, float windNow)
        {
            if (!cookie) return;
            if (Mathf.Abs(Cover - cookieCover) > 0.02f) RebuildCookie();
            cookieOffset += WindDir * (1.5f + windNow * 5f) * dt;   // metros por segundo en el suelo
            cookieOffset.x %= CookieSize; cookieOffset.y %= CookieSize;
            sunData.lightCookieOffset = cookieOffset;
        }

        void RebuildCookie()
        {
            cookieCover = Cover;
            float th = 1f - Mathf.Lerp(0.15f, 0.95f, Cover);
            // La sombra de una nube no es negra: el cielo sigue iluminando.
            float strength = Mathf.Lerp(0.45f, 0.7f, Cover);
            var px = new Color32[cookieNoise.Length];
            for (int i = 0; i < px.Length; i++)
            {
                float d = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(th - 0.08f, th + 0.22f, cookieNoise[i]));
                byte b = (byte)(255f * (1f - d * strength));
                px[i] = new Color32(b, b, b, 255);
            }
            cookie.SetPixels32(px);
            cookie.Apply(true);
        }

        // ════════════════════════════════════════════════════════════════
        //  LLUVIA
        // ════════════════════════════════════════════════════════════════
        void BuildRain()
        {
            var shader = Resources.Load<Shader>("Atmosphere/RainStreak");
            if (!shader || !shader.isSupported) { rainEnabled = false; return; }
            rainMat = new Material(shader) { name = "M_Lluvia (runtime)" };

            var go = new GameObject("Atmosfera_Lluvia");
            go.transform.SetParent(transform, false);
            rain = go.AddComponent<ParticleSystem>();
            rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = rain.main;
            main.loop = true;
            main.duration = 5f;
            main.startLifetime = 2.2f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.022f);
            main.startColor = new Color(1f, 1f, 1f, 0.8f);
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 3500;
            main.playOnAwake = false;
            main.scalingMode = ParticleSystemScalingMode.Shape;

            var shape = rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(34f, 0.5f, 34f);

            var rainVelocity = rain.velocityOverLifetime;
            rainVelocity.enabled = true;
            rainVelocity.space = ParticleSystemSimulationSpace.World;
            rainVelocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            rainVelocity.y = new ParticleSystem.MinMaxCurve(-10.5f, -8.5f);
            rainVelocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var rainEmission = rain.emission;
            rainEmission.rateOverTime = 0f;

            // Las gotas mueren al tocar algo: no llueve dentro de la casa ni a través de los aleros.
            var col = rain.collision;
            col.enabled = true;
            col.type = ParticleSystemCollisionType.World;
            col.mode = ParticleSystemCollisionMode.Collision3D;
            col.quality = ParticleSystemCollisionQuality.Medium;
            col.lifetimeLoss = 1f;
            col.radiusScale = 0.1f;
            col.collidesWith = ~(1 << 2);                   // todo menos "Ignore Raycast"
            col.enableDynamicColliders = false;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.05f;
            r.lengthScale = 2f;
            r.sharedMaterial = rainMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            rain.Play();
        }

        void UpdateRain(Camera cam, float windNow)
        {
            if (!rain) return;
            Vector3 lean = new Vector3(WindDir.x, 0f, WindDir.y) * (windNow * 3.5f);
            if (cam)
                rain.transform.position = cam.transform.position + Vector3.up * 14f - lean * 1.5f
                                        + cam.transform.forward * 4f;       // más gotas donde se mira
            // Todas las curvas en el mismo modo (dos constantes), si no Unity se queja.
            // Los módulos se piden cada vez: guardados en campos se rompen si Unity recarga los scripts en Play.
            var rainVelocity = rain.velocityOverLifetime;
            var rainEmission = rain.emission;
            rainVelocity.x = new ParticleSystem.MinMaxCurve(lean.x * 0.85f, lean.x * 1.15f);
            rainVelocity.z = new ParticleSystem.MinMaxCurve(lean.z * 0.85f, lean.z * 1.15f);
            rainEmission.rateOverTime = Rain * 1500f;

            // Las gotas se ven por la luz que hay (y por el relámpago).
            Color amb = RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Flat
                ? RenderSettings.ambientLight : RenderSettings.ambientEquatorColor;
            float flash = atmo ? atmo.flash : 0f;
            var c = amb * 2.2f + new Color(0.18f, 0.2f, 0.24f) + Color.white * flash;
            c.a = Mathf.Lerp(0.25f, 0.45f, Rain);
            rainMat.SetColor("_Color", c);
        }

        // ════════════════════════════════════════════════════════════════
        //  RELÁMPAGOS
        // ════════════════════════════════════════════════════════════════
        void UpdateLightning(float dt, NightMode.Phase phase)
        {
            if (!lightning || phase != NightMode.Phase.Night || Rain < 0.45f) return;
            nextLightning -= dt;
            if (nextLightning > 0f) return;
            nextLightning = R(35f, 110f) * Mathf.Lerp(1.4f, 0.7f, Rain);
            StartCoroutine(Strike(R(0.25f, 1f)));
        }

        IEnumerator Strike(float closeness)
        {
            if (!atmo) yield break;
            // Destello doble, como la descarga de retorno.
            float[] pulses = { 1f, 0.35f, 0.75f };
            foreach (float p in pulses)
            {
                for (float t = 0f; t < 0.09f; t += Time.deltaTime) { atmo.flash = p * closeness * 0.7f * (1f - t / 0.09f); yield return null; }
                yield return new WaitForSeconds(R(0.03f, 0.09f));
            }
            atmo.flash = 0f;
            // El sonido viaja a ~343 m/s: lejos = llega tarde y suave.
            float km = Mathf.Lerp(3.5f, 0.8f, closeness);
            yield return new WaitForSeconds(km * 2.9f);
            if (thunderSrc)
            {
                thunderSrc.pitch = R(0.85f, 1.1f);
                thunderSrc.volume = GameSettings.AmbienceVolume;
                thunderSrc.PlayOneShot(thunderSrc.clip, thunderVolume * Mathf.Lerp(0.35f, 1f, closeness));
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  SONIDO
        // ════════════════════════════════════════════════════════════════
        void BuildAudio()
        {
            rainSrc = Loop("AMB_Lluvia", ProceduralAudio.Kind.Rain, 8f, 11);
            birdSrc = Loop("AMB_Pajaros", ProceduralAudio.Kind.Birds, 14f, 5);
            thunderSrc = Loop("AMB_Trueno", ProceduralAudio.Kind.Thunder, 7f, 3);
            thunderSrc.loop = false;
            thunderSrc.Stop();

            // El viento que ya trae la escena se toma como base y sigue las ráfagas. (Los grillos los maneja TensionManager.)
            foreach (var s in FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
            {
                if (s.name == "AMB_Viento") { windSrc = s; windBase = s.volume; }
            }
        }

        AudioSource Loop(string name, ProceduralAudio.Kind kind, float seconds, int s)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.clip = ProceduralAudio.Build(kind, seconds, s);
            src.loop = true;
            src.spatialBlend = 0f;
            src.playOnAwake = false;
            src.volume = 0f;
            src.Play();
            return src;
        }

        void UpdateAudio(float windNow, NightMode.Phase phase)
        {
            float amb = GameSettings.AmbienceVolume;
            float el = atmo ? atmo.Elevation : 10f;
            float inside = Mathf.Lerp(1f, 0.4f, indoor);           // paredes y techo apagan lo de afuera

            if (rainSrc) rainSrc.volume = rainVolume * Rain * Mathf.Lerp(1f, 0.55f, indoor) * amb;   // adentro suena el techo

            float birds = phase switch
            {
                NightMode.Phase.Morning => 1f,
                NightMode.Phase.Dusk => Mathf.InverseLerp(1.5f, 7f, el) * 0.6f,   // se callan cuando se pone el sol
                _ => 0f,
            };
            if (birdSrc) birdSrc.volume = birdsVolume * birds * (1f - Rain) * (1f - windNow * 0.4f) * inside * amb;

            if (windSrc) windSrc.volume = windBase * Mathf.Lerp(0.35f, 1.6f, windNow) * inside * amb;

            CricketFactor = phase switch
            {
                NightMode.Phase.Night => 1f - Rain * 0.8f,                       // con lluvia casi no cantan
                NightMode.Phase.Dusk => Mathf.Lerp(0.25f, 1f, Mathf.InverseLerp(8f, 1f, el)),
                _ => 0f,
            } * (1f - windNow * 0.3f);
        }
    }
}
