using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SomeonesHome.EditorTools
{
    /// <summary>
    /// Monta en Hito3_Main lo que necesitan las escenas nuevas del Relato 1:
    /// tareas (platos, basura), celular, perro, "acechador", noche (sacos, cortina, cerrojo, interruptor, golpes).
    /// Usa las piezas de Casa.fbx (build_environment.py → build_props) y llena Relato1Props + el director.
    /// Ejecutar DESPUÉS de EnvironmentSetup (que recrea "Casa (modelo)"). Se puede repetir sin duplicar.
    /// Lo ejecuta el asistente: SomeonesHome.EditorTools.Relato1ExtrasSetup.Apply()
    /// </summary>
    public static class Relato1ExtrasSetup
    {
        const string RootName = "Relato1 (tareas, acecho y noche)";

        public static void Apply()
        {
            var house = GameObject.Find("Casa (modelo)")?.transform;
            var director = Object.FindAnyObjectByType<Level1Director>(FindObjectsInactive.Include);
            var door = Object.FindAnyObjectByType<Door>(FindObjectsInactive.Include);
            var terrain = Terrain.activeTerrain;
            if (!house || !director || !door || !terrain) { Debug.LogError("[Relato1] Falta la casa, el director, la puerta o el terreno."); return; }
            var floor = GameObject.Find("Casa").transform.Find("Piso");
            Vector3 c = floor.position + new Vector3(0, 0.6f, 0);           // centro del piso (como el builder)

            var old = GameObject.Find(RootName);
            if (old) Object.DestroyImmediate(old);
            var narrative = director.transform.parent;
            var root = new GameObject(RootName).transform;
            root.SetParent(narrative, false);
            var props = root.gameObject.AddComponent<Relato1Props>();

            float Ground(float x, float z) => terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y;
            Transform Point(string name, float x, float z, float yOff = 0f)
            {
                var t = new GameObject(name).transform;
                t.SetParent(root, false);
                t.position = new Vector3(x, Ground(x, z) + yOff, z);
                return t;
            }
            Transform Part(string name)
            {
                var t = house.Find(name) ?? door.transform.Find(name);
                if (!t) Debug.LogWarning($"[Relato1] No está {name} en Casa.fbx (¿se ejecutó build_environment.py?).");
                return t;
            }
            void Dynamic(Transform t) { if (t) foreach (var x in t.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(x.gameObject, 0); }
            SimpleInteractable Use(Transform t, string prompt, Vector3? pad = null)
            {
                if (!t) return null;
                foreach (var col in t.GetComponents<Collider>()) Object.DestroyImmediate(col);
                var bc = t.gameObject.AddComponent<BoxCollider>();
                if (pad.HasValue) bc.size += pad.Value;
                var si = t.GetComponent<SimpleInteractable>() ?? t.gameObject.AddComponent<SimpleInteractable>();
                si.Prompt = prompt;
                return si;
            }
            AudioSource Sound(string name, Vector3 pos, ProceduralAudio.Kind kind, float seconds, float volume, float minDist, float maxDist)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root, false);
                go.transform.position = pos;
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false; src.spatialBlend = 1f; src.volume = volume;
                src.rolloffMode = AudioRolloffMode.Logarithmic; src.minDistance = minDist; src.maxDistance = maxDist;
                if (seconds > 0f)
                {
                    var pa = go.AddComponent<ProceduralAudio>();
                    var so = new SerializedObject(pa);
                    so.FindProperty("kind").enumValueIndex = (int)kind;
                    so.FindProperty("seconds").floatValue = seconds;
                    so.FindProperty("seed").intValue = name.GetHashCode() & 0xffff;
                    so.FindProperty("playOnStart").boolValue = false;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                return src;
            }

            // ── Poner la mesa ──
            var pila = Part("Casa_PilaPlatos"); Dynamic(pila);
            props.platesStack = Use(pila, "Tomar los platos", new Vector3(0.1f, 0.15f, 0.1f));
            var vajilla = Part("Casa_Vajilla"); Dynamic(vajilla);
            props.tableSetting = vajilla ? vajilla.gameObject : null;
            var tableUse = new GameObject("Mesa (poner platos)").transform;
            tableUse.SetParent(root, false);
            tableUse.position = c + new Vector3(-1.5f, 0.86f, 0.5f);
            var tbc = tableUse.gameObject.AddComponent<BoxCollider>(); tbc.size = new Vector3(1.9f, 0.1f, 1.1f);
            props.table = tableUse.gameObject.AddComponent<SimpleInteractable>();
            props.table.Prompt = "Poner los platos";

            // ── Basura ──
            var bolsa = Part("Casa_Bolsa"); Dynamic(bolsa);
            props.trashBag = Use(bolsa, "Tomar la bolsa de basura", new Vector3(0.1f, 0.1f, 0.1f));
            var tacho = Part("Casa_Tacho");
            if (tacho)
            {
                var p = tacho.position; p.y = Ground(p.x, p.z) - 0.03f; tacho.position = p;
                props.trashBin = Use(tacho, "Tirar la basura", new Vector3(0.1f, 0f, 0.1f));
                var top = new GameObject("Boca del tacho").transform;
                top.SetParent(tacho, false);
                top.localPosition = new Vector3(0, 0.5f, 0);
                props.trashBinTop = top;
            }
            var dogGO = Sound("Perro (vecinos)", new Vector3(38f, Ground(38f, 82f) + 0.6f, 82f), ProceduralAudio.Kind.Bark, 0f, 1f, 12f, 160f).gameObject;
            props.dog = dogGO.AddComponent<DogBarker>();
            props.glimpsePoint = Point("Vistazo: borde de la colina", 101f, 104.6f);

            // ── Acechador (la misma criatura, otra instancia) ──
            var stalkerGO = new GameObject("Acechador");
            stalkerGO.transform.SetParent(root, false);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(stalkerGO.transform, false);
            var spawned = CharacterSetup.SpawnCreature(visual);
            var stalker = stalkerGO.AddComponent<Stalker>();
            var sso = new SerializedObject(stalker);
            sso.FindProperty("visual").objectReferenceValue = visual;
            sso.FindProperty("head").objectReferenceValue = spawned?.head;
            sso.ApplyModifiedPropertiesWithoutUndo();
            visual.gameObject.SetActive(false);
            props.stalker = stalker;
            props.stalkerSteps = Sound("Pasos detrás (acecho)", Vector3.zero, ProceduralAudio.Kind.Step, 0f, 0.9f, 2f, 30f);
            props.stalkerPoints = new[]
            {
                Point("Acecho 1: orilla", 86.5f, 129.5f),
                Point("Acecho 2: pie de las rocas", 78.5f, 122.2f),
                Point("Acecho 3: borde de la colina", 64.8f, 106.6f),
                Point("Acecho 4: campo", 80f, 82f),
            };

            // ── Noche ──
            props.night = root.gameObject.AddComponent<NightMode>();
            var sacoAlex = Part("Casa_SacoAlex"); Dynamic(sacoAlex);
            var sacoSam = Part("Casa_SacoSam"); Dynamic(sacoSam);
            var cel = Part("Casa_CelularSam"); Dynamic(cel);
            var pantalla = Part("Casa_PantallaSam"); Dynamic(pantalla);
            var nightOnly = new List<GameObject>();
            foreach (var t in new[] { sacoAlex, sacoSam, cel, pantalla }) if (t) nightOnly.Add(t.gameObject);
            props.sleepingBag = Use(sacoAlex, "Acostarse", new Vector3(0, 0.3f, 0));
            if (sacoSam) Object.DestroyImmediate(sacoSam.GetComponent<Collider>());
            props.samPhoneScreen = pantalla ? pantalla.GetComponent<Renderer>() : null;
            if (pantalla)
            {
                var lgo = new GameObject("Luz pantalla (Sam)");
                lgo.transform.SetParent(root, false);
                lgo.transform.position = pantalla.GetComponent<Renderer>().bounds.center + Vector3.up * 0.18f;
                var l = lgo.AddComponent<Light>();
                l.type = LightType.Point; l.range = 2.2f; l.intensity = 0.9f; l.color = new Color(0.55f, 0.68f, 1f);
                l.shadows = LightShadows.None; l.enabled = false;
                props.samPhoneLight = l;
            }
            props.nightOnly = nightOnly.ToArray();

            var dayOnly = new List<GameObject>();
            foreach (var n in new[] { "Tammy", "Emily" }) { var g = GameObject.Find(n); if (g) dayOnly.Add(g); }
            props.dayOnly = dayOnly.ToArray();

            var spawn = new GameObject("Inicio de la noche").transform;
            spawn.SetParent(root, false);
            spawn.SetPositionAndRotation(c + new Vector3(1.6f, 0.1f, -1.3f), Quaternion.Euler(0, 90f, 0));
            props.nightSpawn = spawn;

            var cortina = Part("Casa_CortinaCerrada"); Dynamic(cortina);
            props.curtainClosed = cortina ? cortina.gameObject : null;
            var win = new GameObject("Ventana este (cortina)").transform;
            win.SetParent(root, false);
            win.position = c + new Vector3(5.83f, 0.95f + 0.625f, -1.8f);
            var wbc = win.gameObject.AddComponent<BoxCollider>(); wbc.size = new Vector3(0.12f, 1.3f, 1.35f);
            props.window = win.gameObject.AddComponent<SimpleInteractable>();
            props.window.Prompt = "Cerrar la cortina";

            props.lightSwitch = Use(Part("Casa_Interruptor"), "Apagar la luz", new Vector3(0.12f, 0.12f, 0.12f));
            var lights = new List<Light>();
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (l.type == LightType.Directional || l == props.samPhoneLight) continue;
                Vector3 lp = l.transform.position - c;
                if (Mathf.Abs(lp.x) < 6.2f && Mathf.Abs(lp.z) < 4.7f && lp.y > -0.5f && lp.y < 3.5f) lights.Add(l);
            }
            props.houseLights = lights.ToArray();
            var lamp = Part("Casa_Lampara");
            props.lampBulbs = lamp ? lamp.GetComponentsInChildren<Renderer>() : new Renderer[0];

            var cerrojo = Part("Casa_Cerrojo");
            var oldBolt = door.transform.Find("Casa_Cerrojo");
            if (oldBolt && oldBolt != cerrojo) Object.DestroyImmediate(oldBolt.gameObject);
            if (cerrojo)
            {
                cerrojo.SetParent(door.transform, true);
                Dynamic(cerrojo);
                props.deadbolt = Use(cerrojo, "Poner el cerrojo");
                var bc = cerrojo.GetComponent<BoxCollider>();
                bc.size = new Vector3(0.16f, 0.16f, 0.1f);
                bc.center = new Vector3(0, 0, -0.04f);                 // sobresale hacia adentro, delante del panel de la puerta
            }
            props.interiorDoor = Use(Part("Casa_PuertaInterior"), "Puerta del pasillo");

            float wx = c.x + 7.0f, wz = c.z - 1.8f;
            props.windowOutside = Point("Afuera: ventana este", wx, wz);
            props.doorOutside = Point("Afuera: puerta trasera", c.x + 2f, c.z + 5.6f);
            Vector3 winPos = c + new Vector3(6.15f, 1.55f, -1.8f);
            props.windowKnock = Sound("Golpes: ventana", winPos, ProceduralAudio.Kind.Knock, 1f, 1f, 2f, 40f);
            props.doorKnock = Sound("Golpes: puerta", c + new Vector3(2f, 1.2f, 4.75f), ProceduralAudio.Kind.Knock, 1f, 1f, 2f, 40f);
            props.doorRattle = Sound("Manija: puerta", c + new Vector3(2.4f, 1.0f, 4.7f), ProceduralAudio.Kind.Rattle, 1.8f, 0.9f, 2f, 30f);
            props.whisper = Sound("Susurro: ventana", winPos, ProceduralAudio.Kind.Whisper, 2.5f, 0.8f, 1.5f, 20f);

            // ════════ LLEGADA ════════
            Transform PointRot(string name, float x, float z, float yaw)
            {
                var t = Point(name, x, z);
                t.rotation = Quaternion.Euler(0, yaw, 0);
                return t;
            }
            // Apoya en el terreno una pieza de Casa.fbx (origen en la base) y devuelve cuánto bajó/subió.
            float Snap(Transform t, float extra = 0f)
            {
                if (!t) return 0f;
                Vector3 p = t.position;
                float g = Ground(p.x, p.z);
                foreach (var o in new[] { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1) })
                    g = Mathf.Max(g, Ground(p.x + o.x, p.z + o.y));
                float dy = g + extra - p.y;
                t.position = p + Vector3.up * dy;
                return dy;
            }
            void Box(Transform t, Vector3 center, Vector3 size)
            {
                var bc = t.gameObject.AddComponent<BoxCollider>();
                bc.center = center; bc.size = size;
            }
            void TruckColliders(Transform t, bool south)
            {
                foreach (var col in t.GetComponents<Collider>()) Object.DestroyImmediate(col);
                float f = south ? 1f : -1f;
                Box(t, new Vector3(0, 0.56f, 0), new Vector3(1.8f, 1.12f, 5.1f));
                Box(t, new Vector3(0, 1.34f, -0.2f * f), new Vector3(1.7f, 0.74f, 1.6f));
            }

            var truck = Part("Casa_Camioneta");
            var mochila = Part("Casa_Mochila"); Dynamic(mochila);
            if (truck)
            {
                float dy = Snap(truck, -0.02f);
                if (mochila) mochila.position += Vector3.up * dy;
                TruckColliders(truck, true);
            }
            props.backpack = Use(mochila, "Tomar mi mochila", new Vector3(0.1f, 0.1f, 0.1f));
            var placed = new GameObject("Mochila: en la sala").transform;
            placed.SetParent(root, false);
            placed.SetPositionAndRotation(c + new Vector3(5.2f, 0f, 1.4f), Quaternion.Euler(0, -80f, 0));
            props.backpackPlaced = placed;
            var drop = new GameObject("Dejar la mochila (sala)").transform;
            drop.SetParent(root, false);
            drop.position = c + new Vector3(4.6f, 0.4f, 1.0f);
            Box(drop, Vector3.zero, new Vector3(1.6f, 0.8f, 1.6f));
            props.backpackDrop = drop.gameObject.AddComponent<SimpleInteractable>();
            props.backpackDrop.Prompt = "Dejar la mochila aquí";

            props.arrivalSpawn = PointRot("Llegada: jugador", 101.5f, 58.6f, 180f);
            props.tammyArrival = PointRot("Llegada: Tammy", 99.3f, 55.2f, 30f);
            props.emilyArrival = PointRot("Llegada: Emily", 98.7f, 57.9f, 70f);
            props.samArrival = PointRot("Llegada: Sam", 100.2f, 60.1f, 120f);
            props.samRunOff = new[]
            {
                Point("Sam corre 1", 99.5f, 64.5f), Point("Sam corre 2", 97f, 72f), Point("Sam corre 3", 92f, 85f),
                Point("Sam corre 4", 86f, 97f), Point("Sam corre 5", 80f, 104f),
            };

            var front = new GameObject("Puerta principal (trabada)").transform;
            front.SetParent(root, false);
            front.position = c + new Vector3(0f, 1.05f, -4.5f);
            Box(front, Vector3.zero, new Vector3(1.1f, 2.1f, 0.5f));
            props.frontDoor = front.gameObject.AddComponent<SimpleInteractable>();
            props.frontDoor.Prompt = "Abrir la puerta principal";

            SimpleInteractable Spot(string name, Vector3 localPos, Vector3 size, string prompt)
            {
                var t = new GameObject(name).transform;
                t.SetParent(root, false);
                t.position = c + localPos;
                Box(t, Vector3.zero, size);
                var si = t.gameObject.AddComponent<SimpleInteractable>();
                si.Prompt = prompt;
                return si;
            }
            var nota = Part("Casa_NotaRefri");
            props.inspect = new[]
            {
                Spot("Mirar: foto del río", new Vector3(5.86f, 1.7f, 3.0f), new Vector3(0.14f, 0.55f, 0.75f), "Mirar la foto"),
                Use(nota, "Leer la nota", new Vector3(0.08f, 0.12f, 0.12f)),
                Spot("Mirar: reloj", new Vector3(-1.5f, 2.2f, 4.33f), new Vector3(0.45f, 0.45f, 0.12f), "Mirar el reloj"),
                Spot("Mirar: ventana al campo", new Vector3(-3.0f, 1.575f, 4.45f), new Vector3(1.3f, 1.3f, 0.3f), "Mirar por la ventana"),
            };

            // ════════ APAGÓN ════════
            var player = Object.FindAnyObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            if (player)
            {
                var head = player.Head ? player.Head : player.transform;
                var oldBeam = head.Find("Linterna (luz)");
                if (oldBeam) Object.DestroyImmediate(oldBeam.gameObject);
                var beamGO = new GameObject("Linterna (luz)");
                beamGO.transform.SetParent(head, false);
                beamGO.transform.localPosition = new Vector3(0.18f, -0.18f, 0.1f);
                var beam = beamGO.AddComponent<Light>();
                beam.type = LightType.Spot; beam.spotAngle = 58f; beam.innerSpotAngle = 26f; beam.range = 30f;
                beam.intensity = 6f; beam.color = new Color(1f, 0.94f, 0.82f); beam.shadows = LightShadows.Soft;
                beam.enabled = false;
                var fl = player.GetComponent<Flashlight>() ?? player.gameObject.AddComponent<Flashlight>();
                var clickSrc = Sound("Linterna: clic", Vector3.zero, ProceduralAudio.Kind.Click, 0.15f, 0.35f, 1f, 5f);
                clickSrc.transform.SetParent(beamGO.transform, false);
                clickSrc.spatialBlend = 0f;
                var fso = new SerializedObject(fl);
                fso.FindProperty("beam").objectReferenceValue = beam;
                fso.FindProperty("click").objectReferenceValue = clickSrc;
                fso.ApplyModifiedPropertiesWithoutUndo();
                props.flashlight = fl;
            }
            var linterna = Part("Casa_Linterna"); Dynamic(linterna);
            props.flashlightPickup = Use(linterna, "Tomar la linterna", new Vector3(0.12f, 0.12f, 0.1f));

            var shed = Part("Casa_Cobertizo");
            var caja = Part("Casa_CajaFusibles");
            var palanca = Part("Casa_Palanca"); Dynamic(palanca);
            if (shed)
            {
                float dy = Snap(shed, 0.0f);
                if (caja) caja.position += Vector3.up * dy;
                if (palanca) palanca.position += Vector3.up * dy;
                foreach (var col in shed.GetComponents<Collider>()) Object.DestroyImmediate(col);
                Box(shed, new Vector3(0, 0.04f, 0), new Vector3(2.6f, 0.08f, 2.2f));
                Box(shed, new Vector3(-1.26f, 1.05f, 0), new Vector3(0.08f, 2.1f, 2.2f));
                Box(shed, new Vector3(0, 1.05f, 1.06f), new Vector3(2.6f, 2.1f, 0.08f));
                Box(shed, new Vector3(0, 1.05f, -1.06f), new Vector3(2.6f, 2.1f, 0.08f));
                Box(shed, new Vector3(1.26f, 1.05f, -0.79f), new Vector3(0.08f, 2.1f, 0.63f));
                Box(shed, new Vector3(1.26f, 1.05f, 0.79f), new Vector3(0.08f, 2.1f, 0.63f));
                Box(shed, new Vector3(1.77f, 0.97f, -0.5f), new Vector3(0.9f, 1.86f, 0.04f));
                var inside = new GameObject("Cobertizo: adentro").transform;
                inside.SetParent(root, false);
                inside.position = shed.position;
                props.shedInside = inside;
                props.shedKnock = Sound("Golpes: cobertizo", shed.position + new Vector3(-1.5f, 1.2f, 0.3f), ProceduralAudio.Kind.Knock, 1f, 1f, 2f, 40f);
            }
            props.breaker = Use(caja, "Subir el breaker", new Vector3(0.1f, 0.12f, 0.12f));
            props.breakerLever = palanca;
            props.fieldWatch = Point("Apagón: algo en el campo", 84f, 79f);
            props.houseCorner = Point("Apagón: entre el cobertizo y la casa", 86.5f, 65.2f);
            props.fieldWhisper = Sound("Susurro: campo", new Vector3(84f, Ground(84f, 80f) + 1.5f, 80f), ProceduralAudio.Kind.Whisper, 2.5f, 1f, 4f, 50f);
            props.powerClick = Sound("Clic: se corta la luz", c + new Vector3(0f, 2.5f, 0f), ProceduralAudio.Kind.Click, 0.4f, 1f, 3f, 40f);

            // ════════ MAÑANA ════════
            var morning = new List<GameObject>();
            var sacoVacio = Part("Casa_SacoSamVacio"); Dynamic(sacoVacio);
            if (sacoVacio) { foreach (var col in sacoVacio.GetComponents<Collider>()) Object.DestroyImmediate(col); morning.Add(sacoVacio.gameObject); }
            props.samBagFull = sacoSam ? sacoSam.gameObject : null;
            var papa = Part("Casa_CamionetaPapa");
            if (papa)
            {
                Snap(papa, -0.02f);
                props.dadTruck = Use(papa, "Subir a la camioneta");
                Dynamic(papa);
                morning.Add(papa.gameObject);
            }
            var collar = Part("Casa_Collar"); Dynamic(collar);
            if (collar) { Snap(collar, 0.0f); morning.Add(collar.gameObject); }
            props.collar = Use(collar, "Recoger el collar", new Vector3(0.3f, 0.2f, 0.3f));
            props.samMorning = PointRot("Mañana: Sam junto a la ventana", 97.7f, 57.7f, 200f);
            props.samAtTruck = PointRot("Mañana: Sam junto a la camioneta", 99.4f, 47.6f, 0f);
            props.horn = Sound("Bocina (papá)", papa ? papa.position + Vector3.up : new Vector3(97.5f, 21f, 45.5f), ProceduralAudio.Kind.Horn, 1.4f, 1f, 8f, 150f);

            var trailPts = new[]
            {
                new Vector2(97.0f, 55.6f), new Vector2(97.3f, 58.5f), new Vector2(97.6f, 62.4f), new Vector2(95.0f, 64.3f),
                new Vector2(92.0f, 64.4f), new Vector2(88f, 64.6f), new Vector2(84f, 65.2f), new Vector2(80.6f, 65.9f),
                new Vector2(80.4f, 70f), new Vector2(81f, 76f), new Vector2(81.6f, 79.5f), new Vector2(82f, 83.6f),
            };
            const int changeAt = 10;                                   // desde aquí: pie descalzo
            var trailT = new List<Transform>();
            for (int i = 0; i < trailPts.Length; i++) trailT.Add(Point($"Huellas {i}", trailPts[i].x, trailPts[i].y));
            props.trail = trailT.ToArray();
            var printsRoot = new GameObject("Huellas (mañana)").transform;
            printsRoot.SetParent(root, false);
            var mudPath = "Assets/_Project/Art/Materials/Environment/M_Prop_Huella.mat";
            var mud = AssetDatabase.LoadAssetAtPath<Material>(mudPath);
            if (!mud) { mud = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mud, mudPath); }
            mud.SetColor("_BaseColor", new Color(0.085f, 0.065f, 0.05f));
            mud.SetFloat("_Smoothness", 0.2f);
            EditorUtility.SetDirty(mud);
            void Blob(Transform parent, Vector3 local, Vector3 scale)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(g.GetComponent<Collider>());
                g.name = "marca";
                g.transform.SetParent(parent, false);
                g.transform.localPosition = local;
                g.transform.localScale = scale;
                var r = g.GetComponent<MeshRenderer>();
                r.sharedMaterial = mud;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            int k = 0;
            for (int i = 0; i + 1 < trailPts.Length; i++)
            {
                Vector2 a = trailPts[i], b = trailPts[i + 1];
                Vector2 dir = (b - a).normalized;
                Vector2 side = new(dir.y, -dir.x);
                float len = Vector2.Distance(a, b);
                bool human = i >= changeAt;
                for (float d = 0f; d < len; d += 0.78f, k++)
                {
                    Vector2 p2 = a + dir * d + side * ((k % 2 == 0) ? 0.13f : -0.13f) * (human ? 1.2f : 0.35f);
                    var pr = new GameObject(human ? "pie" : "pezuña").transform;
                    pr.SetParent(printsRoot, false);
                    pr.position = new Vector3(p2.x, Ground(p2.x, p2.y) + 0.004f, p2.y);
                    pr.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.y)) * Quaternion.Euler(0, (k % 3 - 1) * 6f, 0);
                    if (human)
                    {
                        Blob(pr, new Vector3(0, 0, 0.04f), new Vector3(0.1f, 0.02f, 0.2f));
                        Blob(pr, new Vector3(0, 0, -0.1f), new Vector3(0.075f, 0.02f, 0.09f));
                        for (int t = 0; t < 4; t++) Blob(pr, new Vector3(-0.045f + t * 0.03f, 0, 0.16f - t * 0.008f), new Vector3(0.026f, 0.015f, 0.03f));
                    }
                    else
                    {
                        Blob(pr, new Vector3(-0.028f, 0, 0), new Vector3(0.045f, 0.02f, 0.095f));
                        Blob(pr, new Vector3(0.028f, 0, 0), new Vector3(0.045f, 0.02f, 0.095f));
                    }
                }
            }
            // Donde se quedó parado: muchas marcas juntas (ventana, puerta, cobertizo).
            foreach (int idx in new[] { 0, 4, 7 })
            {
                var spot = trailPts[idx];
                for (int j = 0; j < 7; j++)
                {
                    float ang = j * 51f, rad = 0.12f + (j % 3) * 0.1f;
                    float px = spot.x + Mathf.Cos(ang * Mathf.Deg2Rad) * rad, pz = spot.y + Mathf.Sin(ang * Mathf.Deg2Rad) * rad;
                    var pr = new GameObject("pezuña (parado)").transform;
                    pr.SetParent(printsRoot, false);
                    pr.position = new Vector3(px, Ground(px, pz) + 0.004f, pz);
                    pr.rotation = Quaternion.Euler(0, ang * 1.7f, 0);
                    Blob(pr, new Vector3(-0.028f, 0, 0), new Vector3(0.045f, 0.02f, 0.095f));
                    Blob(pr, new Vector3(0.028f, 0, 0), new Vector3(0.045f, 0.02f, 0.095f));
                }
            }
            morning.Add(printsRoot.gameObject);
            props.morningOnly = morning.ToArray();

            // ── Celular (en el HUD) ──
            var hud = Object.FindAnyObjectByType<HUD>(FindObjectsInactive.Include);
            if (hud && !hud.GetComponent<Phone>()) hud.gameObject.AddComponent<Phone>();

            // ── Director ──
            var dso = new SerializedObject(director);
            dso.FindProperty("props").objectReferenceValue = props;
            dso.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(props);
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Relato1] Escenas nuevas montadas: {props.houseLights.Length} luces de la casa, {nightOnly.Count} objetos de noche.");
        }
    }
}
