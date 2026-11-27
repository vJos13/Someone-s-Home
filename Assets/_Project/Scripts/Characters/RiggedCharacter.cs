using System.Collections.Generic;
using UnityEngine;

namespace SomeonesHome
{
    /// <summary>
    /// Personaje con esqueleto y animaciones (Resources/Characters/&lt;Nombre&gt;_Rig.fbx, hecho en Blender por
    /// Tools/Blender/build_rigged_characters.py). Reemplaza en tiempo de ejecución al modelo quieto de la escena:
    /// lo oculta, pone el modelo animado en su lugar y elige el clip solo:
    ///   · se mueve rápido → Run · se mueve → Walk (la velocidad del clip sigue a la del personaje)
    ///   · quieto → el clip base (Idle, Sit, Shiver, Talk… lo pone quien lo maneje con <see cref="SetBase"/>)
    /// Usa el componente Animation (clips Legacy): cero assets extra y mezcla suave con CrossFade.
    /// </summary>
    public class RiggedCharacter : MonoBehaviour
    {
        const float Fade = 0.3f;

        Animation anim;
        Transform model;
        readonly Dictionary<string, string> clips = new();   // "Walk" → nombre real del clip ("Sam_Armature|Walk")
        readonly Dictionary<string, Transform> bones = new();
        string baseClip = "Idle";
        string current;
        Vector3 lastPos;
        float speed;

        [Tooltip("Velocidad (m/s) con la que el clip Walk se ve natural; Run igual.")]
        public float walkReference = 1.35f, runReference = 4.2f;
        public float runThreshold = 2.4f;
        /// <summary>Si es false, no cambia a Walk/Run por su cuenta (p. ej. sentado en el columpio que se mece).</summary>
        public bool autoLocomotion = true;

        public Transform Model => model;
        public string Current => current;
        public float Speed => speed;

        /// <summary>
        /// Pone el modelo animado <paramref name="rig"/> bajo <paramref name="parent"/> (o el host) y oculta los
        /// renderers de <paramref name="hide"/>. Si ya estaba, devuelve el existente. null si el FBX no está.
        /// </summary>
        public static RiggedCharacter Attach(GameObject host, string rig, Transform parent = null, params Transform[] hide)
        {
            if (!host) return null;
            if (host.TryGetComponent(out RiggedCharacter existing)) return existing;
            var prefab = Resources.Load<GameObject>("Characters/" + rig);
            if (!prefab) { Debug.LogWarning($"[Personajes] Falta Resources/Characters/{rig}.fbx"); return null; }
            var rc = host.AddComponent<RiggedCharacter>();
            rc.Build(prefab, parent ? parent : host.transform, hide);
            return rc;
        }

        void Build(GameObject prefab, Transform parent, Transform[] hide)
        {
            foreach (var h in hide)
                if (h) foreach (var r in h.GetComponentsInChildren<Renderer>(true)) r.enabled = false;

            var go = Instantiate(prefab, parent, false);
            go.name = prefab.name + " (animado)";
            model = go.transform;
            // El FBX llega con la rotación de importación de Blender: se conserva (ya mira a +Z).
            anim = go.GetComponent<Animation>() ?? go.AddComponent<Animation>();
            anim.playAutomatically = false;
            anim.cullingType = AnimationCullingType.BasedOnRenderers;
            foreach (AnimationState s in anim)
            {
                string shortName = s.name.Contains("|") ? s.name.Substring(s.name.LastIndexOf('|') + 1) : s.name;
                clips[shortName] = s.name;
                s.wrapMode = shortName == "Scream" ? WrapMode.ClampForever : WrapMode.Loop;
            }
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) bones[t.name] = t;
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.updateWhenOffscreen = false;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            lastPos = transform.position;
            Play(baseClip, 0f);
        }

        public bool Has(string clip) => clips.ContainsKey(clip);

        /// <summary>Hueso por nombre ("Head", "Jaw", "Hand_R"…).</summary>
        public Transform Bone(string name) => bones.TryGetValue(name, out var t) ? t : null;

        /// <summary>Clip cuando está quieto (Idle, Sit, Shiver, Talk, Scream…).</summary>
        public void SetBase(string clip, bool instant = false)
        {
            if (!Has(clip)) clip = "Idle";
            baseClip = clip;
            if (instant || speed < 0.25f) Play(clip, instant ? 0f : Fade);
        }

        public string Base => baseClip;

        void Play(string clip, float fade)
        {
            if (!anim || !clips.TryGetValue(clip, out var real)) return;
            if (current == clip && anim.IsPlaying(real)) return;
            current = clip;
            if (fade <= 0f) anim.Play(real);
            else anim.CrossFade(real, fade);
        }

        // En LateUpdate: las corrutinas que mueven al personaje (relato, persecución) ya corrieron en este frame,
        // así el desplazamiento y el deltaTime son del mismo frame (en el Editor los frames alternan 5 ms / 100 ms).
        void LateUpdate()
        {
            if (!anim) return;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            Vector3 d = transform.position - lastPos;
            lastPos = transform.position;
            d.y = 0f;
            float raw = d.magnitude / dt;
            if (raw > 40f) raw = 0f;                      // teletransporte
            speed = Mathf.Lerp(speed, raw, 1f - Mathf.Exp(-dt * 10f));

            string want = baseClip;
            if (autoLocomotion)
            {
                if (speed > runThreshold && Has("Run")) want = "Run";
                else if (speed > 0.3f && Has("Walk")) want = "Walk";
            }
            Play(want, Fade);
            if (clips.TryGetValue(current, out var real))
            {
                var st = anim[real];
                if (current == "Walk") st.speed = Mathf.Clamp(speed / walkReference, 0.6f, 1.8f);
                else if (current == "Run") st.speed = Mathf.Clamp(speed / runReference, 0.7f, 1.6f);
                else st.speed = 1f;
            }
        }
    }
}
