using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Graphing;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;

namespace SomeonesHome.EditorTools
{
    /// <summary>
    /// Genera los 3 Shader Graph propios del Relato 1 (agua del río, piel de la criatura, ojos de la criatura)
    /// como archivos .shadergraph normales: se abren y editan en el editor de Shader Graph.
    /// El asmdef se llama "Unity.ShaderGraphTool.Editor" porque Shader Graph solo expone su API de grafos a ese nombre.
    /// Lo ejecuta el asistente: SomeonesHome.EditorTools.ShaderGraphBuilder.BuildAll()
    /// </summary>
    public static class ShaderGraphBuilder
    {
        const string Dir = "Assets/_MyShaders/Graphs";
        const string PbrDir = "Assets/_Project/Art/Textures/PBR";

        public static void BuildAll()
        {
            BuildWater();
            BuildCreature();
            BuildEyes();
            AssetDatabase.Refresh();
            AssignMaterials();
            Debug.Log("[ShaderGraph] SG_Agua, SG_Criatura y SG_OjosCriatura generados en " + Dir);
        }

        /// <summary>Pone los shaders propios en los materiales que ya usa la escena.</summary>
        public static void AssignMaterials()
        {
            const string M = "Assets/_MyShaders/Materials/";
            void Use(string mat, string shader, Action<Material> setup)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(M + mat + ".mat");
                var sh = AssetDatabase.LoadAssetAtPath<Shader>($"{Dir}/{shader}.shadergraph");
                if (!m || !sh) { Debug.LogWarning($"[ShaderGraph] Falta {mat} o {shader}"); return; }
                m.shader = sh;
                setup?.Invoke(m);
                EditorUtility.SetDirty(m);
            }
            Use("M_Water", "SG_Agua", null);
            Use("M_Creature", "SG_Criatura", m => m.SetColor("_Tinte", Color.white));
            Use("M_CreatureDark", "SG_Criatura", m => m.SetColor("_Tinte", new Color(0.45f, 0.4f, 0.38f)));
            Use("M_CreatureEyes", "SG_OjosCriatura", null);
            AssetDatabase.SaveAssets();
        }

        // ════════════════════════════════════════════════════════════════
        //  Utilidades
        // ════════════════════════════════════════════════════════════════
        static Type T(string name) =>
            AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).FirstOrDefault(t => t != null);

        static Target UrpTarget(string subTarget, bool transparent)
        {
            var target = (Target)Activator.CreateInstance(T("UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget"));
            target.GetType().GetMethod("TrySetActiveSubTarget").Invoke(target, new object[] { T("UnityEditor.Rendering.Universal.ShaderGraph." + subTarget) });
            if (transparent)
            {
                var prop = target.GetType().GetProperty("surfaceType");
                prop.SetValue(target, Enum.Parse(prop.PropertyType, "Transparent"));
            }
            return target;
        }

        static GraphData NewGraph(Target target, params BlockFieldDescriptor[] blocks)
        {
            var g = new GraphData();
            g.AddContexts();
            g.InitializeOutputs(new[] { target }, blocks);
            g.AddCategory(CategoryData.DefaultCategory());
            g.path = "Someone's Home";
            return g;
        }

        static T Node<T>(GraphData g, float x, float y) where T : AbstractMaterialNode, new()
        {
            var n = new T();
            n.drawState = new DrawState { position = new Rect(x, y, 0, 0), expanded = true };
            g.AddNode(n);
            return n;
        }

        static MaterialSlot Slot(AbstractMaterialNode n, string name, SlotType type)
        {
            var slots = new System.Collections.Generic.List<MaterialSlot>();
            n.GetSlots(slots);
            var s = slots.FirstOrDefault(x => x.slotType == type && (x.RawDisplayName() == name || x.shaderOutputName == name));
            if (s == null) throw new Exception($"{n.GetType().Name} no tiene el slot {type} '{name}'. Hay: " + string.Join(", ", slots.Select(x => x.RawDisplayName())));
            return s;
        }

        static void Link(GraphData g, AbstractMaterialNode from, string outName, AbstractMaterialNode to, string inName)
        {
            var a = Slot(from, outName, SlotType.Output);
            var b = Slot(to, inName, SlotType.Input);
            g.Connect(from.GetSlotReference(a.id), to.GetSlotReference(b.id));
        }

        static void ToBlock(GraphData g, AbstractMaterialNode from, string outName, string blockName)
        {
            var ctxs = new[] { g.fragmentContext, g.vertexContext };
            var block = ctxs.SelectMany(c => c.blocks).Select(b => b.value).FirstOrDefault(b => b.descriptor.name == blockName);
            if (block == null) throw new Exception("No está el bloque " + blockName);
            var a = Slot(from, outName, SlotType.Output);
            g.Connect(from.GetSlotReference(a.id), block.GetSlotReference(0));
        }

        static void SetSpace(GeometryNode n, CoordinateSpace space) =>
            typeof(GeometryNode).GetField("m_Space", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(n, space);

        static void SetFloat(AbstractMaterialNode n, string slot, float v)
        {
            switch (Slot(n, slot, SlotType.Input))
            {
                case Vector1MaterialSlot s1: s1.value = v; break;
                case DynamicVectorMaterialSlot sv: sv.value = new Vector4(v, v, v, v); break;
                case DynamicValueMaterialSlot sm:
                    var c = new Vector4(v, v, v, v);
                    sm.value = new Matrix4x4(c, c, c, c);
                    break;
                case var other: throw new Exception($"No sé asignar un float al slot {other.GetType().Name}");
            }
        }
        static void SetVec2(AbstractMaterialNode n, string slot, Vector2 v) => ((Vector2MaterialSlot)Slot(n, slot, SlotType.Input)).value = v;

        static PropertyNode Prop(GraphData g, AbstractShaderProperty p, string reference, string display, float x, float y)
        {
            p.displayName = display;
            g.AddGraphInput(p);
            p.SetReferenceNameAndSanitizeForGraph(g, reference);
            var n = Node<PropertyNode>(g, x, y);
            n.property = p;
            return n;
        }

        static PropertyNode Float(GraphData g, string reference, string display, float value, float x, float y) =>
            Prop(g, new Vector1ShaderProperty { value = value }, reference, display, x, y);

        static PropertyNode Range(GraphData g, string reference, string display, float value, float min, float max, float x, float y) =>
            Prop(g, new Vector1ShaderProperty { value = value, floatType = FloatType.Slider, rangeValues = new Vector2(min, max) }, reference, display, x, y);

        static PropertyNode Col(GraphData g, string reference, string display, Color value, bool hdr, float x, float y) =>
            Prop(g, new ColorShaderProperty { value = value, colorMode = hdr ? ColorMode.HDR : ColorMode.Default }, reference, display, x, y);

        static PropertyNode Tex(GraphData g, string reference, string display, string texPath, bool normal, float x, float y)
        {
            var p = new Texture2DShaderProperty();
            p.value.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            p.defaultType = normal ? Texture2DShaderProperty.DefaultType.NormalMap : Texture2DShaderProperty.DefaultType.White;
            return Prop(g, p, reference, display, x, y);
        }

        static void Save(GraphData g, string name)
        {
            g.ValidateGraph();
            string path = $"{Dir}/{name}.shadergraph";
            FileUtilities.WriteShaderGraphToDisk(path, g);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        // ════════════════════════════════════════════════════════════════
        //  SG_Agua: río oscuro con ondas animadas (dos capas de ruido que se cruzan),
        //  color que cambia con el ángulo (Fresnel) y transparencia.
        // ════════════════════════════════════════════════════════════════
        static void BuildWater()
        {
            var g = NewGraph(UrpTarget("UniversalLitSubTarget", true),
                BlockFields.VertexDescription.Position, BlockFields.VertexDescription.Normal, BlockFields.VertexDescription.Tangent,
                BlockFields.SurfaceDescription.BaseColor, BlockFields.SurfaceDescription.NormalTS, BlockFields.SurfaceDescription.Metallic,
                BlockFields.SurfaceDescription.Smoothness, BlockFields.SurfaceDescription.Emission, BlockFields.SurfaceDescription.Occlusion,
                BlockFields.SurfaceDescription.Alpha);

            var deep = Col(g, "_ColorProfundo", "Color profundo", new Color(0.035f, 0.075f, 0.085f), false, -1400, -300);
            var shallow = Col(g, "_ColorSuperficie", "Color superficie (ángulo rasante)", new Color(0.16f, 0.22f, 0.23f), false, -1400, -240);
            var scale = Float(g, "_EscalaOndas", "Escala de las ondas", 0.06f, -1400, 0);
            var speed = Float(g, "_Velocidad", "Velocidad de la corriente", 0.12f, -1400, 120);
            var nStrength = Range(g, "_FuerzaNormal", "Fuerza de las ondas", 0.3f, 0f, 2f, -1400, 240);
            var smooth = Range(g, "_Suavidad", "Suavidad (reflejo)", 0.82f, 0f, 1f, -1400, 360);
            var fresPow = Float(g, "_PotenciaFresnel", "Potencia Fresnel", 3f, -1400, -160);
            var opacity = Range(g, "_Opacidad", "Opacidad", 0.86f, 0f, 1f, -1400, 480);

            // UV en mundo (XZ) para que las ondas no dependan del tamaño del plano.
            var pos = Node<PositionNode>(g, -1200, 0);
            SetSpace(pos, CoordinateSpace.World);
            var split = Node<SplitNode>(g, -1000, 0);
            Link(g, pos, "Out", split, "In");
            var uvW = Node<Vector2Node>(g, -820, 0);
            Link(g, split, "R", uvW, "X");
            Link(g, split, "B", uvW, "Y");
            var uvS = Node<MultiplyNode>(g, -640, 0);
            Link(g, uvW, "Out", uvS, "A");
            Link(g, scale, "Out", uvS, "B");

            // Corriente: la capa A va río abajo, la B en diagonal y más lenta.
            var time = Node<TimeNode>(g, -1200, 160);
            var tA = Node<MultiplyNode>(g, -1000, 160);
            Link(g, time, "Time", tA, "A");
            Link(g, speed, "Out", tA, "B");
            var tB = Node<MultiplyNode>(g, -820, 200);
            Link(g, tA, "Out", tB, "A");
            SetFloat(tB, "B", -0.6f);

            var uvA = Node<TilingAndOffsetNode>(g, -460, -40);
            Link(g, uvS, "Out", uvA, "UV");
            Link(g, tA, "Out", uvA, "Offset");
            var uvB = Node<TilingAndOffsetNode>(g, -460, 140);
            Link(g, uvS, "Out", uvB, "UV");
            SetVec2(uvB, "Tiling", new Vector2(1.7f, 1.3f));
            Link(g, tB, "Out", uvB, "Offset");

            var noiseA = Node<GradientNoiseNode>(g, -260, -40);
            Link(g, uvA, "Out", noiseA, "UV");
            SetFloat(noiseA, "Scale", 9f);
            var noiseB = Node<GradientNoiseNode>(g, -260, 140);
            Link(g, uvB, "Out", noiseB, "UV");
            SetFloat(noiseB, "Scale", 16f);

            var hA = Node<NormalFromHeightNode>(g, -60, -40);
            Link(g, noiseA, "Out", hA, "In");
            SetFloat(hA, "Strength", 0.06f);
            var hB = Node<NormalFromHeightNode>(g, -60, 140);
            Link(g, noiseB, "Out", hB, "In");
            SetFloat(hB, "Strength", 0.04f);
            var blend = Node<NormalBlendNode>(g, 140, 40);
            Link(g, hA, "Out", blend, "A");
            Link(g, hB, "Out", blend, "B");
            var nStr = Node<NormalStrengthNode>(g, 320, 40);
            Link(g, blend, "Out", nStr, "In");
            Link(g, nStrength, "Out", nStr, "Strength");
            ToBlock(g, nStr, "Out", "NormalTS");

            // Color: profundo mirando hacia abajo, más claro en ángulo rasante.
            var fres = Node<FresnelNode>(g, -260, -300);
            Link(g, fresPow, "Out", fres, "Power");
            var col = Node<LerpNode>(g, 140, -300);
            Link(g, deep, "Out", col, "A");
            Link(g, shallow, "Out", col, "B");
            Link(g, fres, "Out", col, "T");
            ToBlock(g, col, "Out", "BaseColor");
            ToBlock(g, smooth, "Out", "Smoothness");

            var alpha = Node<LerpNode>(g, 140, 300);
            Link(g, opacity, "Out", alpha, "A");
            SetFloat(alpha, "B", 1f);
            Link(g, fres, "Out", alpha, "T");
            ToBlock(g, alpha, "Out", "Alpha");

            Save(g, "SG_Agua");
        }

        // ════════════════════════════════════════════════════════════════
        //  SG_Criatura: piel PBR (color, normal, metallic/smoothness) + borde pálido (Fresnel)
        //  que la recorta contra la oscuridad + "respiración": el cuerpo se infla apenas, siempre.
        // ════════════════════════════════════════════════════════════════
        static void BuildCreature()
        {
            var g = NewGraph(UrpTarget("UniversalLitSubTarget", false),
                BlockFields.VertexDescription.Position, BlockFields.VertexDescription.Normal, BlockFields.VertexDescription.Tangent,
                BlockFields.SurfaceDescription.BaseColor, BlockFields.SurfaceDescription.NormalTS, BlockFields.SurfaceDescription.Metallic,
                BlockFields.SurfaceDescription.Smoothness, BlockFields.SurfaceDescription.Emission, BlockFields.SurfaceDescription.Occlusion);

            string set = $"{PbrDir}/PielCriatura/T_PielCriatura_";
            var baseTex = Tex(g, "_BaseMap", "Piel (color)", set + "BaseColor.png", false, -1200, -400);
            var nrmTex = Tex(g, "_BumpMap", "Piel (normal)", set + "Normal.png", true, -1200, -100);
            var maskTex = Tex(g, "_MaskMap", "Piel (metallic/smoothness)", set + "MetallicSmoothness.png", false, -1200, 200);
            var tint = Col(g, "_Tinte", "Tinte", Color.white, false, -1200, -500);
            var tiling = Float(g, "_Tiling", "Repetición", 3f, -1200, -600);
            var smooth = Range(g, "_Suavidad", "Suavidad (piel húmeda)", 0.35f, 0f, 1f, -1200, 300);
            var rimCol = Col(g, "_ColorBorde", "Color del borde", new Color(0.6f, 0.55f, 0.45f), true, -1200, 460);
            var rimStr = Range(g, "_FuerzaBorde", "Fuerza del borde", 0.12f, 0f, 3f, -1200, 520);
            var rimPow = Float(g, "_PotenciaBorde", "Potencia del borde", 5f, -1200, 580);
            var breath = Range(g, "_Respiracion", "Respiración (m)", 0.006f, 0f, 0.03f, -1200, 700);
            var breathSpeed = Float(g, "_VelocidadRespiracion", "Velocidad de respiración", 1.7f, -1200, 760);

            var uv = Node<TilingAndOffsetNode>(g, -900, -600);
            Link(g, tiling, "Out", uv, "Tiling");

            var sBase = Node<SampleTexture2DNode>(g, -700, -450);
            Link(g, baseTex, "Out", sBase, "Texture");
            Link(g, uv, "Out", sBase, "UV");
            var tinted = Node<MultiplyNode>(g, -420, -450);
            Link(g, sBase, "RGBA", tinted, "A");
            Link(g, tint, "Out", tinted, "B");
            ToBlock(g, tinted, "Out", "BaseColor");

            var sNrm = Node<SampleTexture2DNode>(g, -700, -150);
            sNrm.textureType = TextureType.Normal;
            sNrm.normalMapSpace = NormalMapSpace.Tangent;
            Link(g, nrmTex, "Out", sNrm, "Texture");
            Link(g, uv, "Out", sNrm, "UV");
            ToBlock(g, sNrm, "RGBA", "NormalTS");

            var sMask = Node<SampleTexture2DNode>(g, -700, 150);
            Link(g, maskTex, "Out", sMask, "Texture");
            Link(g, uv, "Out", sMask, "UV");
            ToBlock(g, sMask, "R", "Metallic");
            var sm = Node<MultiplyNode>(g, -420, 220);
            Link(g, sMask, "A", sm, "A");
            Link(g, smooth, "Out", sm, "B");
            ToBlock(g, sm, "Out", "Smoothness");

            var fres = Node<FresnelNode>(g, -900, 480);
            Link(g, rimPow, "Out", fres, "Power");
            var rim1 = Node<MultiplyNode>(g, -700, 480);
            Link(g, fres, "Out", rim1, "A");
            Link(g, rimCol, "Out", rim1, "B");
            var rim2 = Node<MultiplyNode>(g, -420, 480);
            Link(g, rim1, "Out", rim2, "A");
            Link(g, rimStr, "Out", rim2, "B");
            ToBlock(g, rim2, "Out", "Emission");

            // Respiración: posición + normal · sin(t · velocidad) · amplitud.
            var time = Node<TimeNode>(g, -900, 760);
            var tm = Node<MultiplyNode>(g, -720, 760);
            Link(g, time, "Time", tm, "A");
            Link(g, breathSpeed, "Out", tm, "B");
            var sin = Node<SineNode>(g, -560, 760);
            Link(g, tm, "Out", sin, "In");
            var amp = Node<MultiplyNode>(g, -400, 760);
            Link(g, sin, "Out", amp, "A");
            Link(g, breath, "Out", amp, "B");
            var nrm = Node<NormalVectorNode>(g, -400, 880);
            SetSpace(nrm, CoordinateSpace.Object);
            var push = Node<MultiplyNode>(g, -220, 820);
            Link(g, nrm, "Out", push, "A");
            Link(g, amp, "Out", push, "B");
            var pos = Node<PositionNode>(g, -220, 960);
            SetSpace(pos, CoordinateSpace.Object);
            var add = Node<AddNode>(g, -40, 880);
            Link(g, pos, "Out", add, "A");
            Link(g, push, "Out", add, "B");
            ToBlock(g, add, "Out", "Position");

            Save(g, "SG_Criatura");
        }

        // ════════════════════════════════════════════════════════════════
        //  SG_OjosCriatura (Unlit): brillo de ojos de animal. Más fuerte cuando te mira de frente
        //  (Fresnel invertido) y late despacio. Con valores HDR, el Bloom los hace brillar en la oscuridad.
        // ════════════════════════════════════════════════════════════════
        static void BuildEyes()
        {
            var g = NewGraph(UrpTarget("UniversalUnlitSubTarget", false),
                BlockFields.VertexDescription.Position, BlockFields.VertexDescription.Normal, BlockFields.VertexDescription.Tangent,
                BlockFields.SurfaceDescription.BaseColor);

            var eyeCol = Col(g, "_ColorOjos", "Color de los ojos", new Color(1f, 0.72f, 0.32f), true, -900, -200);
            var bright = Range(g, "_Brillo", "Brillo", 2.5f, 0f, 10f, -900, -120);
            var pulse = Float(g, "_Pulso", "Pulso (Hz)", 0.35f, -900, 120);
            var facePow = Float(g, "_Mirada", "Brillo al mirarte de frente", 1.5f, -900, 0);

            var fres = Node<FresnelNode>(g, -700, 0);
            Link(g, facePow, "Out", fres, "Power");
            var facing = Node<OneMinusNode>(g, -520, 0);
            Link(g, fres, "Out", facing, "In");

            var time = Node<TimeNode>(g, -900, 200);
            var tm = Node<MultiplyNode>(g, -720, 200);
            Link(g, time, "Time", tm, "A");
            Link(g, pulse, "Out", tm, "B");
            var tm2 = Node<MultiplyNode>(g, -560, 200);
            Link(g, tm, "Out", tm2, "A");
            SetFloat(tm2, "B", 6.2831f);
            var sin = Node<SineNode>(g, -400, 200);
            Link(g, tm2, "Out", sin, "In");
            var sinScale = Node<MultiplyNode>(g, -260, 200);
            Link(g, sin, "Out", sinScale, "A");
            SetFloat(sinScale, "B", 0.25f);
            var beat = Node<AddNode>(g, -120, 200);
            Link(g, sinScale, "Out", beat, "A");
            SetFloat(beat, "B", 0.75f);

            var c1 = Node<MultiplyNode>(g, -520, -160);
            Link(g, eyeCol, "Out", c1, "A");
            Link(g, bright, "Out", c1, "B");
            var c2 = Node<MultiplyNode>(g, -300, -60);
            Link(g, c1, "Out", c2, "A");
            Link(g, facing, "Out", c2, "B");
            var c3 = Node<MultiplyNode>(g, -80, 0);
            Link(g, c2, "Out", c3, "A");
            Link(g, beat, "Out", c3, "B");
            ToBlock(g, c3, "Out", "BaseColor");

            Save(g, "SG_OjosCriatura");
        }
    }
}
