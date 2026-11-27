using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SomeonesHome
{
    /// <summary>
    /// Linterna del jugador (el haz sale de la cabeza; el modelo se ve en la mano vía <see cref="InventorySystem"/>).
    /// · Solo alumbra si está en la mano. [F] la saca y la prende/apaga; con la linterna en mano, clic izquierdo también.
    /// · Batería limitada: se gasta encendida. Por debajo del 25 % alumbra menos y titila; a 0 se apaga.
    ///   Apagada, una pila agotada se recupera un poco (como las pilas reales), así nunca quedas a oscuras del todo.
    /// · Cerca de la entidad (acechador o la cosa del río, visibles) el haz tiembla y se corta a ratos.
    /// · Flicker(): parpadeo guionado de los sustos (el director lo usa).
    /// </summary>
    public class Flashlight : MonoBehaviour
    {
        [SerializeField] Light beam;
        [SerializeField] AudioSource click;

        [Header("Batería")]
        [Tooltip("Segundos encendida con la batería llena.")]
        [SerializeField] float batteryLife = 360f;
        [Tooltip("Hasta dónde se recupera una pila agotada si la dejas apagada.")]
        [SerializeField, Range(0f, 0.3f)] float reboundLevel = 0.12f;
        [Tooltip("Segundos apagada para recuperar ese nivel.")]
        [SerializeField] float reboundTime = 40f;

        [Header("Entidad cerca")]
        [SerializeField] float hauntRadius = 16f;

        public bool Owned { get; private set; }
        public bool On => beam && beam.enabled;
        /// <summary>Carga de 0 a 1.</summary>
        public float Battery { get; private set; } = 1f;

        bool flickering;            // parpadeo guionado en curso
        bool wantOn;                // lo que el jugador eligió (se respeta al volver a la mano)
        bool warnedLow, warnedEmpty;
        float baseIntensity = -1f;
        float sputterTimer, sputterDip = 1f;
        Stalker[] stalkers;
        RiverCreature[] creatures;
        InventorySystem inventory;

        bool InHand => inventory ? inventory.Equipped == ItemId.Flashlight : Owned;

        void Start()
        {
            if (beam) baseIntensity = beam.intensity;
            if (beam && !FlashlightHaze.Disabled && !beam.GetComponent<FlashlightHaze>()) beam.gameObject.AddComponent<FlashlightHaze>();   // haz visible en la niebla
            stalkers = FindObjectsByType<Stalker>(FindObjectsInactive.Include);
            creatures = FindObjectsByType<RiverCreature>(FindObjectsInactive.Include);
            inventory = GetComponentInParent<InventorySystem>();
            if (inventory) inventory.EquippedChanged += OnEquippedChanged;
        }

        void OnDestroy()
        {
            if (inventory) inventory.EquippedChanged -= OnEquippedChanged;
        }

        // ════════════════════════ API (director) ════════════════════════
        public void Give()
        {
            Owned = true;
            wantOn = true;
            if (inventory)
            {
                inventory.RegisterModel(ItemId.Flashlight, FlashlightModel);
                inventory.Add(ItemId.Flashlight, equip: true);       // OnEquippedChanged la prende
            }
            SetOn(true);
        }

        public void Take()
        {
            Owned = false;
            wantOn = false;
            SetOn(false);
            if (inventory) { inventory.Remove(ItemId.Flashlight); inventory.ClearWorldCopies(ItemId.Flashlight); }
        }

        /// <summary>Modelo en mano: copia del objeto "Casa_Linterna" de la escena, o uno simple si no está.</summary>
        public System.Func<GameObject> FlashlightModel = DefaultModel;

        static GameObject DefaultModel()
        {
            var root = new GameObject("Linterna").transform;
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(root, false);
            body.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
            body.transform.localScale = new Vector3(0.035f, 0.1f, 0.035f);
            body.GetComponent<Renderer>().sharedMaterial = InspectModels.Mat(new Color(0.1f, 0.1f, 0.1f), 0.5f, 0.6f);
            return root.gameObject;
        }

        public void SetOn(bool on)
        {
            if (!beam) return;
            if (on && Battery <= 0.001f) on = false;
            if (beam.enabled != on && click) click.Play();
            beam.enabled = on;
        }

        /// <summary>Parpadea y se apaga 'darkTime' segundos; 'during' corre mientras está apagada.</summary>
        public IEnumerator Flicker(float darkTime, System.Action during = null)
        {
            if (!beam || !Owned) { during?.Invoke(); yield break; }
            if (!InHand)
            {
                // Guardada: el susto pasa igual, a oscuras.
                during?.Invoke();
                yield return new WaitForSeconds(darkTime);
                yield break;
            }
            flickering = true;
            float baseI = CurrentIntensity();
            beam.enabled = true;                    // el susto se ve aunque la tuvieras apagada
            for (int i = 0; i < 4; i++)
            {
                beam.intensity = baseI * Random.Range(0.05f, 0.5f);
                yield return new WaitForSeconds(Random.Range(0.04f, 0.1f));
                beam.intensity = baseI;
                yield return new WaitForSeconds(Random.Range(0.03f, 0.09f));
            }
            beam.enabled = false;
            during?.Invoke();
            yield return new WaitForSeconds(darkTime);
            beam.intensity = baseI;
            // Vuelve encendida (el guion cuenta con la luz) si sigue en la mano y le queda pila.
            beam.enabled = Battery > 0f && InHand;
            if (beam.enabled) wantOn = true;
            flickering = false;
        }

        // ════════════════════════ UPDATE ════════════════════════
        void OnEquippedChanged(ItemId item)
        {
            if (!Owned || flickering) return;
            if (item == ItemId.Flashlight) SetOn(wantOn);
            else SetOn(false);                      // a la mochila: se apaga (wantOn se recuerda)
        }

        void Update()
        {
            // La señal de la cinta se ensucia con la entidad cerca (con o sin linterna).
            VhsLook.Haunt = Mathf.MoveTowards(VhsLook.Haunt, ComputeHaunt(), Time.deltaTime * 1.5f);
            if (!Owned || !beam) return;
            if (baseIntensity < 0f) baseIntensity = beam.intensity;
            if (!flickering && !GamePause.IsPaused) ReadInput();
            UpdateBattery(Time.deltaTime);
            if (!flickering && beam.enabled) beam.intensity = CurrentIntensity() * HauntDip(Time.deltaTime);
        }

        void ReadInput()
        {
            bool blocked = inventory && inventory.InputBlocked;
            if (blocked) return;
            var kb = Keyboard.current;
            bool f = kb != null && kb.fKey.wasPressedThisFrame;
            var mouse = Mouse.current;
            bool lmb = mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState == CursorLockMode.Locked
                       && !GameMenu.IsOpen && InHand;
            var pad = GamepadGuard.Current;
            bool padToggle = pad != null && pad.rightShoulder.wasPressedThisFrame;
            if (!f && !lmb && !padToggle) return;

            if (!InHand)
            {
                // [F] con otra cosa en la mano: saca la linterna y la prende.
                wantOn = true;
                inventory.Select(ItemId.Flashlight);
                return;
            }
            wantOn = !On;
            if (wantOn && Battery <= 0.001f)
            {
                if (click) click.Play();
                HUD.Instance?.Say("", "No prende. La pila está muerta… si la dejo descansar, quizá.", 2.5f);
                wantOn = false;
                return;
            }
            SetOn(wantOn);
        }

        void UpdateBattery(float dt)
        {
            if (beam.enabled && !flickering)
            {
                Battery = Mathf.Max(0f, Battery - dt / Mathf.Max(1f, batteryLife));
                if (Battery < 0.2f && !warnedLow) { warnedLow = true; HUD.Instance?.Say("", "La linterna se está quedando sin pila.", 2.5f); }
                if (Battery <= 0f)
                {
                    beam.enabled = false;
                    if (click) click.Play();
                    if (!warnedEmpty) { warnedEmpty = true; HUD.Instance?.Say("", "Se apagó. Se acabó la pila.", 2.5f); }
                }
            }
            else if (Battery < reboundLevel)
                Battery = Mathf.Min(reboundLevel, Battery + dt * reboundLevel / Mathf.Max(1f, reboundTime));
        }

        /// <summary>Intensidad según la carga: plena hasta el 25 %, después baja y titila.</summary>
        float CurrentIntensity()
        {
            float k = Mathf.Clamp01(Battery / 0.25f);
            return baseIntensity * Mathf.Lerp(0.3f, 1f, Mathf.Sqrt(k));
        }

        /// <summary>0–1: qué tan cerca está la entidad visible (acechador o la cosa del río).</summary>
        float ComputeHaunt()
        {
            float haunt = 0f;
            Vector3 p = transform.position;
            if (stalkers != null)
                foreach (var s in stalkers)
                    if (s && s.Visible) haunt = Mathf.Max(haunt, 1f - Vector3.Distance(p, s.transform.position) / hauntRadius);
            if (creatures != null)
                foreach (var c in creatures)
                    if (c && c.Current != RiverCreature.State.Hidden && c.Current != RiverCreature.State.Gone)
                        haunt = Mathf.Max(haunt, 1f - Vector3.Distance(p, c.transform.position) / hauntRadius);
            return Mathf.Clamp01(haunt);
        }

        /// <summary>Multiplicador de parpadeo: por batería baja y por la entidad cerca. 1 = estable.</summary>
        float HauntDip(float dt)
        {
            float haunt = ComputeHaunt();
            float low = Battery < 0.25f ? 1f - Battery / 0.25f : 0f;
            float chance = Mathf.Clamp01(haunt) * 7f + low * 1.5f;     // cortes por segundo (aprox.)

            sputterTimer -= dt;
            if (sputterTimer <= 0f)
            {
                if (Random.value < chance * 0.08f)
                {
                    sputterDip = Random.Range(0.02f, 0.45f);
                    sputterTimer = Random.Range(0.04f, 0.12f);
                }
                else { sputterDip = 1f; sputterTimer = 0.08f; }
            }
            // Temblor fino continuo cuando la cosa está cerca.
            float tremor = 1f - Mathf.Clamp01(haunt) * 0.25f * Mathf.PerlinNoise(Time.time * 18f, 0.3f);
            return sputterDip * tremor;
        }
    }
}
