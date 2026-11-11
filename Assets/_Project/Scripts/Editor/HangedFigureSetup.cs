using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SomeonesHome.EditorTools
{
    /// <summary>
    /// El cuerpo colgado del álamo al otro lado del río (escena del río, Relato 1).
    ///  - Reutiliza la malla de Tammy (Art/Models/Characters/Tammy.fbx) SIN tocar el FBX ni sus materiales:
    ///    copia la malla, le da la pose de colgado (cabeza caída hacia delante y a un lado, puntas de los pies
    ///    hacia abajo, una rodilla apenas flexionada) y quita el delantal. Ropa y piel usan copias tintadas
    ///    (oscuras, desaturadas) de los materiales originales: de lejos es una silueta, no "Tammy".
    ///  - Genera la cuerda: vueltas alrededor de la rama, caída tensa y la soga con el nudo junto al cuello.
    ///  - Lo cuelga del Alamo_A que queda detrás de la criatura, dentro del plano VCam_NarrativeReveal,
    ///    de la rama baja que sale hacia el costado (vista desde el columpio no tapa el tronco).
    ///  - La aparición la controla HangedFigure (lee el estado de RiverCreature; no la modifica).
    /// Lo ejecuta el asistente vía Unity Pipeline: SomeonesHome.EditorTools.HangedFigureSetup.Build()
    /// Solo agrega/reemplaza el objeto "Ahorcado (otro lado del río)" en la escena abierta.
    /// </summary>
    public static class HangedFigureSetup
    {
        const string SourceModel = "Assets/_Project/Art/Models/Characters/Tammy.fbx";
        const string DerivedDir = "Assets/_Project/Art/Models/Characters/Derivados";
        const string MeshAssetPath = DerivedDir + "/Ahorcado.asset";
        const string MatDir = "Assets/_Project/Art/Materials/Characters";
        public const string RootName = "Ahorcado (otro lado del río)";

        // ── Árbol ────────────────────────────────────────────────────────
        // Alamo_A detrás de la criatura (NatureSetup, semilla fija). Esqueleto de build_nature.py → alamo("Alamo_A", 11, 15):
        // coordenadas de Blender sin escalar; en Unity: (-x, z, -y) · 1.15 (escala interna del modelo) · transform del árbol.
        static readonly Vector3 TreeApprox = new(94.22f, 8.03f, 194.11f);
        const float TreeInternalScale = 1.15f;
        static readonly Vector3 SkFork = new(-0.04f, 0.05f, 5.00f);    // donde el tronco se abre
        static readonly Vector3 SkCrotch = new(-0.92f, 1.49f, 7.76f);  // fin de la rama baja lateral (horqueta)
        static readonly Vector3 SkOuter = new(-2.78f, 1.70f, 9.33f);   // rama que sale de la horqueta hacia afuera
        const float OuterBranchRadius = 0.185f;                        // radio en la base de esa rama (sin escalar)
        const float WrapAlong = 0.60f;                                 // metros desde la horqueta donde se amarra

        // Desde el columpio (posición del plano de revelación) para orientar el cuerpo.
        static readonly Vector3 ViewPoint = new(91.7f, 7.9f, 125.45f);
        const float YawFromView = 25f;          // tres cuartos: ni de frente ni de perfil
        const float FeetAboveGround = 1.45f;

        // ── Pose (espacio del modelo de Tammy: pies en y=0, mira a +Z) ────
        static readonly Vector3 HeadPivot = new(0f, 1.44f, -0.01f);
        static readonly Quaternion HeadRot = Quaternion.Euler(24f, 0f, 13f);   // hacia delante y hacia -X (lejos del nudo)
        const float NeckStretch = 0.018f;
        const float AnkleY = 0.09f, KneeY = 0.49f;

        // ── Cuerda ───────────────────────────────────────────────────────
        const float RopeRadius = 0.014f;
        static readonly Vector3 NooseCenter = new(0f, 1.432f, -0.012f);
        const float NooseRx = 0.046f, NooseRz = 0.052f, NooseTilt = 24f;     // la soga sube por detrás
        static readonly Vector2 KnotDir = new Vector2(0.96f, -0.30f).normalized; // nudo detrás de la oreja (+X)
        const float KnotLength = 0.11f, KnotRadius = 0.026f;

        public static string Build()
        {
            var report = new System.Text.StringBuilder();
            var scene = EditorSceneManager.GetActiveScene();

            var creature = Object.FindAnyObjectByType<RiverCreature>(FindObjectsInactive.Include);
            if (!creature) return "No hay RiverCreature en la escena abierta.";
            var tree = FindTree();
            if (!tree) return "No se encontró el Alamo_A detrás de la criatura.";

            var src = AssetDatabase.LoadAssetAtPath<GameObject>(SourceModel)?.GetComponentInChildren<MeshFilter>()?.sharedMesh;
            if (!src) return "No se encontró la malla de " + SourceModel;
            var srcMats = AssetDatabase.LoadAssetAtPath<GameObject>(SourceModel).GetComponentInChildren<MeshRenderer>().sharedMaterials;

            // ── Geometría del árbol en mundo ──
            Vector3 W(Vector3 b) => tree.TransformPoint(new Vector3(-b.x, b.z, -b.y) * TreeInternalScale);
            Vector3 crotch = W(SkCrotch);
            Vector3 dir = (W(SkOuter) - crotch).normalized;
            float branchR = OuterBranchRadius * TreeInternalScale * tree.lossyScale.x * 0.97f; // la rama se afina
            Vector3 wrapCenter = crotch + dir * WrapAlong;
            float wrapR = branchR + RopeRadius * 0.6f;                         // la cuerda se hunde un poco en la corteza
            Vector3 down = Vector3.ProjectOnPlane(Vector3.down, dir).normalized;
            Vector3 side = Vector3.Cross(dir, down).normalized;
            Vector3 pivotWorld = wrapCenter + down * wrapR;

            // ── Malla del cuerpo y de la soga ──
            var body = BakeBody(src);
            var noose = BuildNoose(out Vector3 knotTop);
            float ground = Terrain.activeTerrain ? Terrain.activeTerrain.SampleHeight(pivotWorld) : tree.position.y;
            float dropLength = pivotWorld.y - (ground + FeetAboveGround + knotTop.y);
            var drop = new MeshBuilder();
            drop.Tube(new[] { Vector3.zero, new Vector3(0, -dropLength * 0.5f, 0), new Vector3(0, -dropLength - 0.02f, 0) }, RopeRadius, 8, 6f);
            var dropMesh = drop.ToMesh("Ahorcado_Cuerda");

            // ── Materiales (copias; los originales no se tocan) ──
            Material Find(string n) => srcMats.FirstOrDefault(m => m && m.name == n);
            var matPants = DerivedMaterial("M_Ahorcado_Pantalon", Find("M_Char_Slacks"), new Color(0.115f, 0.112f, 0.11f), null);
            var matTop = DerivedMaterial("M_Ahorcado_Ropa", Find("M_Tammy_Sweater"), new Color(0.2f, 0.185f, 0.165f), null);
            var matSkin = DerivedMaterial("M_Ahorcado_Piel", Find("M_Char_SkinTammy"), new Color(0.44f, 0.415f, 0.40f), 0.22f);
            var matRope = DerivedMaterial("M_Cuerda", Find("M_Char_Slacks"), new Color(0.40f, 0.33f, 0.24f), null);
            if (matRope.HasProperty("_BaseMap")) matRope.SetTextureScale("_BaseMap", new Vector2(1f, 1f));
            var bodyMats = new[] { matPants, matTop, matSkin, Find("M_Char_HairDark"), Find("M_Char_Eyes") };

            // ── Escena ──
            var old = GameObject.Find(RootName);
            if (old) Object.DestroyImmediate(old);

            var parent = creature.transform.parent;
            var root = new GameObject(RootName);
            root.transform.SetParent(parent, true);
            Vector3 toView = ViewPoint - pivotWorld; toView.y = 0f;
            root.transform.SetPositionAndRotation(pivotWorld,
                Quaternion.Euler(0f, Mathf.Atan2(toView.x, toView.z) * Mathf.Rad2Deg + YawFromView, 0f));

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);

            // Vueltas alrededor de la rama (estáticas) + cabo suelto.
            var wrap = new MeshBuilder();
            var helix = new List<Vector3>();
            const float turns = 3f, pitch = RopeRadius * 2.3f;
            for (int i = 0; i <= 72; i++)
            {
                float a = i / 72f * turns * 2f * Mathf.PI;          // a = 2π → punto más bajo de la vuelta central (el pivote)
                float s = pitch * (a / (2f * Mathf.PI) - 1f);
                Vector3 p = wrapCenter + dir * s + (down * Mathf.Cos(a) + side * Mathf.Sin(a)) * wrapR;
                helix.Add(root.transform.InverseTransformPoint(p));
            }
            wrap.Tube(helix.ToArray(), RopeRadius, 7, 6f);
            Vector3 tailStart = wrapCenter + dir * (pitch * 2f) + down * wrapR;
            wrap.Tube(new[]
            {
                root.transform.InverseTransformPoint(tailStart),
                root.transform.InverseTransformPoint(tailStart + Vector3.down * 0.12f + dir * 0.03f),
                root.transform.InverseTransformPoint(tailStart + Vector3.down * 0.26f + dir * 0.02f),
            }, RopeRadius * 0.9f, 7, 6f);
            var wrapMesh = wrap.ToMesh("Ahorcado_Amarre");

            var pivot = new GameObject("Pivote");
            pivot.transform.SetParent(visual.transform, false);

            // Guardar mallas (un solo archivo con sub-assets).
            if (!AssetDatabase.IsValidFolder(DerivedDir)) AssetDatabase.CreateFolder("Assets/_Project/Art/Models/Characters", "Derivados");
            if (AssetDatabase.LoadAssetAtPath<Mesh>(MeshAssetPath)) AssetDatabase.DeleteAsset(MeshAssetPath);
            AssetDatabase.CreateAsset(body, MeshAssetPath);
            AssetDatabase.AddObjectToAsset(noose, body);
            AssetDatabase.AddObjectToAsset(dropMesh, body);
            AssetDatabase.AddObjectToAsset(wrapMesh, body);
            AssetDatabase.SaveAssets();

            Renderer AddMesh(string name, Transform p, Mesh m, Material[] mats)
            {
                var go = new GameObject(name);
                go.transform.SetParent(p, false);
                go.AddComponent<MeshFilter>().sharedMesh = m;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
                return r;
            }

            AddMesh("Amarre", visual.transform, wrapMesh, new[] { matRope });
            AddMesh("Cuerda", pivot.transform, dropMesh, new[] { matRope });
            var bodyR = AddMesh("Cuerpo", pivot.transform, body, bodyMats);
            bodyR.transform.localPosition = new Vector3(-knotTop.x, -dropLength - knotTop.y, -knotTop.z);
            AddMesh("Soga", bodyR.transform, noose, new[] { matRope });

            var hf = root.AddComponent<HangedFigure>();
            var so = new SerializedObject(hf);
            so.FindProperty("creature").objectReferenceValue = creature;
            so.FindProperty("visual").objectReferenceValue = visual;
            so.FindProperty("swingPivot").objectReferenceValue = pivot.transform;
            so.FindProperty("body").objectReferenceValue = bodyR;
            so.FindProperty("swayPeriod").floatValue = 2f * Mathf.PI * Mathf.Sqrt((dropLength + 1.0f) / 9.81f);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);

            // ── Comprobaciones ──
            report.AppendLine($"Árbol {tree.name} {tree.position}; horqueta {crotch}; pivote {pivotWorld}; suelo {ground:F2}");
            report.AppendLine($"Cuerda {dropLength:F2} m; periodo péndulo {so.FindProperty("swayPeriod").floatValue:F2} s");
            var b = bodyR.bounds;
            report.AppendLine($"Cuerpo bounds {b.min} → {b.max}; pies a {b.min.y - ground:F2} m del suelo");
            // Separación del tronco (eje del tronco: base → horqueta inferior).
            Vector3 tb = W(new Vector3(0, 0, 0.5f)), tf = W(SkFork);
            Vector3 fwd = W(SkCrotch);
            float minTrunk = float.MaxValue, minLimb = float.MaxValue;
            var verts = body.vertices;
            foreach (var v in verts)
            {
                var wv = bodyR.transform.TransformPoint(v);
                minTrunk = Mathf.Min(minTrunk, SegDist(wv, tb, tf) - 0.48f * TreeInternalScale * tree.lossyScale.x);
                minLimb = Mathf.Min(minLimb, SegDist(wv, tf, fwd) - 0.298f * TreeInternalScale * tree.lossyScale.x);
            }
            report.AppendLine($"Holgura cuerpo↔tronco ≥ {minTrunk:F2} m; cuerpo↔rama baja ≥ {minLimb:F2} m");
            // La cuerda no atraviesa la cabeza: vértices del cuerpo cerca de la línea de caída (sobre el nudo).
            Vector3 lineA = bodyR.transform.InverseTransformPoint(pivot.transform.position);
            float ropeClear = verts.Where(v => v.y > knotTop.y).Select(v => SegDist(v, knotTop, lineA)).DefaultIfEmpty(9f).Min();
            report.AppendLine($"Holgura cuerda↔cabeza {ropeClear - RopeRadius:F3} m");
            float ropeLimb = float.MaxValue;
            for (float t = 0; t <= 1f; t += 0.02f)
                ropeLimb = Mathf.Min(ropeLimb, SegDist(Vector3.Lerp(pivotWorld, pivotWorld + Vector3.down * dropLength, t), tf, fwd) - 0.298f * TreeInternalScale * tree.lossyScale.x);
            report.AppendLine($"Holgura cuerda↔rama baja {ropeLimb:F2} m");
            Debug.Log("[Ahorcado] " + report);
            return report.ToString();
        }

        static Transform FindTree()
        {
            Transform best = null;
            float bestD = 2f;
            foreach (var g in Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Include))
            {
                if (!g.name.StartsWith("Alamo_A")) continue;
                float d = Vector3.Distance(g.transform.position, TreeApprox);
                if (d < bestD) { bestD = d; best = g.transform; }
            }
            return best;
        }

        static float SegDist(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector3.Distance(p, a + ab * t);
        }

        static float Smooth(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        static Material DerivedMaterial(string name, Material source, Color color, float? smoothness)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(source);
                AssetDatabase.CreateAsset(m, path);
            }
            else m.CopyPropertiesFromMaterial(source);
            m.name = name;
            m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (smoothness.HasValue) m.SetFloat("_Smoothness", smoothness.Value);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ════════════════════════════════════════════════════════════════
        //  POSE
        // ════════════════════════════════════════════════════════════════
        static Mesh BakeBody(Mesh src)
        {
            var v = src.vertices;
            var n = src.normals;
            var t = src.tangents;
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 p = v[i];
                Quaternion q = Quaternion.identity;

                if (p.y < 0.6f && Mathf.Abs(p.x) > 0.02f)
                {
                    float sx = Mathf.Sign(p.x);
                    // Hijo primero (tobillo), luego padre (rodilla).
                    float wf = Smooth(0.16f, 0.07f, p.y);
                    if (wf > 0f)
                    {
                        var qf = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(sx < 0 ? 38f : 31f, sx * 4f, 0f), wf);
                        var ankle = new Vector3(sx * 0.105f, AnkleY, 0f);
                        p = ankle + qf * (p - ankle);
                        q = qf * q;
                    }
                    float wk = Smooth(0.56f, 0.44f, v[i].y);
                    if (wk > 0f)
                    {
                        // Una rodilla apenas flexionada (asimetría = deja de parecer un maniquí).
                        var qk = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(sx < 0 ? 8f : 2.5f, 0f, sx * -1.2f), wk);
                        var knee = new Vector3(sx * 0.105f, KneeY, 0.015f);
                        p = knee + qk * (p - knee);
                        q = qk * q;
                    }
                }

                float wh = Smooth(1.415f, 1.49f, v[i].y);
                if (wh > 0f)
                {
                    var qh = Quaternion.Slerp(Quaternion.identity, HeadRot, wh);
                    p = HeadPivot + qh * (p - HeadPivot) + Vector3.up * NeckStretch * wh;
                    q = qh * q;
                }

                // Hombros caídos: los brazos se cierran un poco hacia los muslos.
                // (por debajo de y=1 la cadera llega a |x|≈0.175 y la mano empieza en 0.20: solo se mueve el brazo)
                float ax = Mathf.Abs(v[i].x);
                float wa = (v[i].y < 1f ? Smooth(0.178f, 0.192f, ax) : Smooth(0.165f, 0.185f, ax)) * Smooth(1.45f, 1.30f, v[i].y);
                if (wa > 0f && v[i].y > 0.75f)
                {
                    float sx = Mathf.Sign(v[i].x);
                    var qa = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(-2f, 0f, sx * -1.8f), wa);
                    var shoulder = new Vector3(sx * 0.18f, 1.38f, 0f);
                    p = shoulder + qa * (p - shoulder) + Vector3.down * 0.012f * wa;
                    q = qa * q;
                }

                v[i] = p;
                n[i] = q * n[i];
                Vector3 tt = q * new Vector3(t[i].x, t[i].y, t[i].z);
                t[i] = new Vector4(tt.x, tt.y, tt.z, t[i].w);
            }

            var m = new Mesh { name = "Ahorcado_Cuerpo" };
            m.vertices = v;
            m.normals = n;
            m.tangents = t;
            m.uv = src.uv;
            m.subMeshCount = 5;                 // 0 pantalón, 1 suéter, 2 piel, 3 pelo/zapatos, 4 ojos (sin delantal)
            for (int s = 0; s < 5; s++) m.SetTriangles(src.GetTriangles(s), s);
            m.RecalculateBounds();
            return m;
        }

        static Mesh BuildNoose(out Vector3 knotTop)
        {
            var mb = new MeshBuilder();
            var tilt = Quaternion.Euler(NooseTilt, 0f, 0f);
            Vector3 Loop(float a) => NooseCenter + tilt * new Vector3(Mathf.Cos(a) * NooseRx, 0f, Mathf.Sin(a) * NooseRz);
            float knotA = Mathf.Atan2(KnotDir.y, KnotDir.x);
            var ring = new List<Vector3>();
            for (int i = 0; i <= 40; i++) ring.Add(Loop(knotA + 0.25f + i / 40f * (2f * Mathf.PI - 0.5f)));
            mb.Tube(ring.ToArray(), RopeRadius, 7, 6f);

            // Nudo: espiral apretada (vueltas) que sube desde la soga.
            Vector3 kb = Loop(knotA);
            Vector3 outward = new Vector3(KnotDir.x, 0f, KnotDir.y) * 0.012f;
            var coil = new List<Vector3>();
            for (int i = 0; i <= 64; i++)
            {
                float f = i / 64f;
                float a = f * 7f * 2f * Mathf.PI;
                coil.Add(kb + outward + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (KnotRadius - RopeRadius) + Vector3.up * (f * KnotLength));
            }
            mb.Tube(coil.ToArray(), RopeRadius * 0.95f, 6, 6f);
            // Alma del nudo (rellena la espiral) y salida hacia arriba.
            knotTop = kb + outward + Vector3.up * KnotLength;
            mb.Tube(new[] { kb + outward - Vector3.up * 0.005f, knotTop + Vector3.up * 0.03f }, KnotRadius - RopeRadius * 0.8f, 8, 6f);
            return mb.ToMesh("Ahorcado_Soga");
        }

        // ════════════════════════════════════════════════════════════════
        //  Tubos (cuerda)
        // ════════════════════════════════════════════════════════════════
        class MeshBuilder
        {
            readonly List<Vector3> v = new(), n = new();
            readonly List<Vector2> uv = new();
            readonly List<int> tri = new();

            /// <summary>Tubo a lo largo de un camino (marcos por transporte paralelo, sin giros bruscos).</summary>
            public void Tube(Vector3[] path, float r, int sides, float uvPerMeter)
            {
                int start = v.Count;
                Vector3 prevT = (path[1] - path[0]).normalized;
                Vector3 normal = Vector3.Cross(prevT, Mathf.Abs(prevT.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                float len = 0f;
                for (int i = 0; i < path.Length; i++)
                {
                    Vector3 tg = i == 0 ? prevT
                        : i == path.Length - 1 ? (path[i] - path[i - 1]).normalized
                        : (path[i + 1] - path[i - 1]).normalized;
                    if (i > 0)
                    {
                        len += Vector3.Distance(path[i], path[i - 1]);
                        normal = Quaternion.FromToRotation(prevT, tg) * normal;
                    }
                    prevT = tg;
                    Vector3 bin = Vector3.Cross(tg, normal);
                    for (int s = 0; s <= sides; s++)
                    {
                        float a = s / (float)sides * 2f * Mathf.PI;
                        Vector3 d = normal * Mathf.Cos(a) + bin * Mathf.Sin(a);
                        v.Add(path[i] + d * r);
                        n.Add(d);
                        uv.Add(new Vector2(s / (float)sides, len * uvPerMeter));
                    }
                }
                int ring = sides + 1;
                for (int i = 0; i < path.Length - 1; i++)
                    for (int s = 0; s < sides; s++)
                    {
                        int a = start + i * ring + s, b = a + ring;
                        tri.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
                    }
            }

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                m.SetVertices(v);
                m.SetNormals(n);
                m.SetUVs(0, uv);
                m.SetTriangles(tri, 0);
                m.RecalculateTangents();
                m.RecalculateBounds();
                return m;
            }
        }
    }
}
