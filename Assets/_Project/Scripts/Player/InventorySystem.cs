using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SomeonesHome
{
    public enum ItemId { None, Flashlight, Phone, TrashBag, ShedKey, BusterCollar, Backpack, Plates }

    /// <summary>
    /// Inventario del jugador: 5 espacios con selección por teclas 1–5, rueda del ratón o cruceta del mando.
    /// Lo seleccionado se ve en la mano (capa <see cref="ViewModelLayer"/>, sin atravesar paredes).
    /// Volver a pulsar el número del objeto en mano lo guarda (manos vacías).
    ///
    /// Agarrar: [E] sobre un <see cref="SimpleInteractable"/> con Item → <see cref="PickUp"/> (entra al primer espacio
    /// libre y queda en la mano). Soltar: [Q] → <see cref="DropEquipped"/>: el objeto sale de la barra y aparece delante
    /// del jugador como copia con física (Rigidbody, sin atravesar paredes ni terreno), que se vuelve a agarrar con [E].
    /// El celular no se suelta. El relato escucha <see cref="Dropped"/> para detectar entregas (bolsa junto al tacho…).
    ///
    /// La linterna y el celular reaccionan a lo que está en mano (<see cref="EquippedChanged"/>): la linterna solo
    /// alumbra en la mano y el celular se abre al sacarlo.
    /// </summary>
    [DisallowMultipleComponent]
    public class InventorySystem : MonoBehaviour
    {
        public const int SlotCount = 5;

        /// <summary>Datos fijos de cada objeto: nombre y cómo se sostiene (posición/giro/tamaño en la vista).</summary>
        public sealed class ItemDef
        {
            public ItemId id;
            public string name;
            /// <summary>Para frases: "Agarrar bolsa de basura".</summary>
            public string lowerName;
            public bool droppable = true;
            public Vector3 handPos, handEuler;
            public float handSize;
        }

        static readonly Dictionary<ItemId, ItemDef> Defs = new()
        {
            [ItemId.Flashlight] = new ItemDef { id = ItemId.Flashlight, name = "Linterna", lowerName = "linterna", handPos = new(0.16f, -0.15f, 0.36f), handEuler = new(-3f, -5f, 0f), handSize = 0.2f },
            [ItemId.Phone] = new ItemDef { id = ItemId.Phone, name = "Celular", lowerName = "celular", droppable = false, handPos = new(0.11f, -0.14f, 0.3f), handEuler = new(-18f, -14f, 4f), handSize = 0.12f },
            [ItemId.TrashBag] = new ItemDef { id = ItemId.TrashBag, name = "Bolsa de basura", lowerName = "bolsa de basura", handPos = new(0.22f, -0.27f, 0.52f), handEuler = new(0f, 25f, 0f), handSize = 0.38f },
            [ItemId.ShedKey] = new ItemDef { id = ItemId.ShedKey, name = "Llave del cobertizo", lowerName = "llave del cobertizo", handPos = new(0.12f, -0.1f, 0.27f), handEuler = new(-20f, -25f, -15f), handSize = 0.09f },
            [ItemId.BusterCollar] = new ItemDef { id = ItemId.BusterCollar, name = "Collar de Buster", lowerName = "collar", handPos = new(0.15f, -0.14f, 0.33f), handEuler = new(-60f, 10f, 0f), handSize = 0.14f },
            [ItemId.Backpack] = new ItemDef { id = ItemId.Backpack, name = "Mochila", lowerName = "mochila", handPos = new(0.27f, -0.3f, 0.6f), handEuler = new(5f, 25f, 0f), handSize = 0.34f },
            [ItemId.Plates] = new ItemDef { id = ItemId.Plates, name = "Platos", lowerName = "platos", handPos = new(0.05f, -0.24f, 0.45f), handEuler = new(8f, 0f, 0f), handSize = 0.3f },
        };

        public static ItemDef Def(ItemId id) => Defs.TryGetValue(id, out var d) ? d : null;
        public static InventorySystem Instance { get; private set; }

        /// <summary>Cambió el contenido de algún espacio.</summary>
        public event Action Changed;
        /// <summary>Cambió lo que está en la mano (puede ser None).</summary>
        public event Action<ItemId> EquippedChanged;
        /// <summary>Se agarró un objeto de la escena con [E].</summary>
        public event Action<ItemId> PickedUp;
        /// <summary>Se soltó un objeto con [Q]: el ítem y su copia física en el mundo.</summary>
        public event Action<ItemId, GameObject> Dropped;

        readonly List<SimpleInteractable> worldCopies = new();
        AudioClip pickClip, dropClip;

        readonly ItemId[] slots = new ItemId[SlotCount];
        readonly Dictionary<ItemId, Func<GameObject>> modelFactories = new();
        int selected = SlotCount - 1;   // siempre hay un espacio activo (puede estar vacío = manos vacías)
        float scrollAccum;

        // Mano
        Transform hand;             // hijo de la cámara overlay
        GameObject handModel;
        ItemId handItem;
        float raise;                // 0 = abajo (fuera de vista), 1 = en posición
        Quaternion lastCamRot;
        Vector2 sway;
        FirstPersonController player;
        InventoryHotbar hotbar;

        public ItemId Equipped => slots[selected];
        public int SelectedSlot => selected;
        public ItemId this[int slot] => slots[slot];
        public bool Has(ItemId id) => Array.IndexOf(slots, id) >= 0;
        /// <summary>Hay algo que impide cambiar de objeto (cinemática, inspección, menú).</summary>
        public bool InputBlocked => GamePause.IsPaused || InspectionView.IsOpen || MainMenuController.Active
                                    || (player && player.InputLocked && player.LookLocked);

        void Awake()
        {
            Instance = this;
            player = GetComponent<FirstPersonController>();
            modelFactories[ItemId.Phone] = InspectModels.PhoneModel;
            modelFactories[ItemId.ShedKey] = InspectModels.ShedKey;
            pickClip = ProceduralAudio.Build(ProceduralAudio.Kind.Click, 0.12f, 41);
            dropClip = ProceduralAudio.Build(ProceduralAudio.Kind.Step, 0.2f, 43);
        }

        void Start()
        {
            hotbar = InventoryHotbar.Create(this);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ════════════════════════ API ════════════════════════
        /// <summary>Cómo se arma el modelo en mano de un objeto (p. ej. una copia del objeto de la escena).</summary>
        public void RegisterModel(ItemId id, Func<GameObject> factory) => modelFactories[id] = factory;

        /// <summary>Guarda el objeto en el primer espacio libre. Devuelve false si ya estaba o no hay lugar.</summary>
        public bool Add(ItemId id, bool equip = true)
        {
            if (id == ItemId.None || Has(id)) { if (equip) Select(id); return false; }
            // Primer espacio libre. Si no va a la mano, se evita el espacio activo (si hay otro libre).
            int free = -1;
            for (int i = 0; i < SlotCount && free < 0; i++)
                if (slots[i] == ItemId.None && (equip || i != selected)) free = i;
            if (free < 0 && slots[selected] == ItemId.None) free = selected;
            if (free < 0) return false;
            var before = Equipped;
            slots[free] = id;
            Changed?.Invoke();
            hotbar?.Flash(free);
            if (equip) selected = free;
            NotifyEquipped(before);
            return true;
        }

        /// <summary>[E] sobre un ítem: entra al primer espacio libre y queda en la mano. False si no hay lugar.</summary>
        public bool PickUp(SimpleInteractable source)
        {
            var id = source.Item;
            if (Has(id)) { Select(id); return true; }
            if (Array.IndexOf(slots, ItemId.None) < 0)
            {
                hotbar?.Toast("No tengo las manos libres: suelta algo con [Q].");
                return false;
            }
            // La copia en la mano se arma con el aspecto del objeto real (si el relato no registró otra cosa).
            if (!modelFactories.ContainsKey(id))
            {
                var r = source.GetComponentInChildren<Renderer>();
                if (r) modelFactories[id] = () => InspectModels.CloneVisual(r, Def(id)?.name ?? id.ToString());
            }
            Add(id, equip: true);
            worldCopies.Remove(source);
            OneShotAudio.Play(pickClip, source.transform.position, 0.45f);
            hotbar?.Toast("Recogiste: " + (Def(id)?.name ?? id.ToString()));
            PickedUp?.Invoke(id);
            return true;
        }

        /// <summary>[Q]: suelta lo que está en la mano delante del jugador, con física. False si no se puede.</summary>
        public bool DropEquipped()
        {
            var id = Equipped;
            if (id == ItemId.None) return false;
            var def = Def(id);
            if (def != null && !def.droppable) { hotbar?.Toast("Mejor no soltar el " + def.lowerName + "."); return false; }
            if (!modelFactories.TryGetValue(id, out var factory)) return false;
            var cam = Camera.main;
            if (!cam) return false;

            var go = factory();
            go.name = "Soltado: " + (def?.name ?? id.ToString());
            // Caja de colisión a la medida del objeto.
            var b = new Bounds(go.transform.position, Vector3.zero);
            bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            var col = go.AddComponent<BoxCollider>();
            col.center = go.transform.InverseTransformPoint(b.center);
            col.size = Vector3.Max(b.size, Vector3.one * 0.04f);
            float radius = Mathf.Max(0.05f, b.extents.magnitude);

            // Delante del jugador, sin meterlo en una pared: se acorta si algo bloquea el camino.
            Vector3 origin = cam.transform.position;
            Vector3 fwd = cam.transform.forward;
            Vector3 flat = new Vector3(fwd.x, 0f, fwd.z).normalized;
            Vector3 dir = (flat * 0.8f + Vector3.down * 0.35f).normalized;
            float dist = 0.75f;
            foreach (var h in Physics.SphereCastAll(origin, radius * 0.8f, dir, dist, ~0, QueryTriggerInteraction.Ignore))
                if (!h.transform.IsChildOf(transform) && h.distance < dist) dist = Mathf.Max(0.05f, h.distance - 0.02f);
            go.transform.SetPositionAndRotation(origin + dir * dist, Quaternion.LookRotation(flat) * go.transform.rotation);

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = Mathf.Clamp(b.size.x * b.size.y * b.size.z * 300f, 0.2f, 6f);
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;   // no atraviesa el terreno al caer
            rb.linearVelocity = flat * 1.2f + Vector3.up * 0.5f;
            rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 2f;
            if (TryGetComponent<CharacterController>(out var cc)) Physics.IgnoreCollision(cc, col);   // no te traba al caminar
            go.AddComponent<DroppedItemSound>().clip = dropClip;

            var pickup = go.AddComponent<SimpleInteractable>();
            pickup.Item = id;
            pickup.DestroyOnPickup = true;
            worldCopies.Add(pickup);

            Remove(id);
            OneShotAudio.Play(dropClip, go.transform.position, 0.35f);
            hotbar?.Toast("Soltaste: " + (def?.name ?? id.ToString()));
            Dropped?.Invoke(id, go);
            return true;
        }

        /// <summary>Borra las copias soltadas de un objeto (p. ej. la linterna al llegar la mañana).</summary>
        public void ClearWorldCopies(ItemId id)
        {
            for (int i = worldCopies.Count - 1; i >= 0; i--)
            {
                var c = worldCopies[i];
                if (!c) { worldCopies.RemoveAt(i); continue; }
                if (c.Item != id) continue;
                Destroy(c.gameObject);
                worldCopies.RemoveAt(i);
            }
        }

        public void Remove(ItemId id)
        {
            int i = Array.IndexOf(slots, id);
            if (i < 0) return;
            var before = Equipped;
            slots[i] = ItemId.None;
            Changed?.Invoke();
            NotifyEquipped(before);         // si estaba en la mano, el espacio activo queda vacío
        }

        public void Select(ItemId id)
        {
            int i = Array.IndexOf(slots, id);
            if (i >= 0 && i != selected) SelectSlot(i);
        }

        /// <summary>Activa un espacio (1–5 → 0–4). Si está vacío, las manos quedan vacías.</summary>
        public void SelectSlot(int slot)
        {
            if (slot < 0 || slot >= SlotCount) return;
            SetSelected(slot);
        }

        void NotifyEquipped(ItemId before)
        {
            hotbar?.Wake();
            if (Equipped != before) EquippedChanged?.Invoke(Equipped);
        }

        void SetSelected(int slot)
        {
            var before = Equipped;
            selected = slot;
            hotbar?.Selected();
            NotifyEquipped(before);
        }

        // ════════════════════════ ENTRADA ════════════════════════
        void Update()
        {
            if (!InputBlocked) ReadInput();
            UpdateHand(Time.deltaTime);
        }

        void ReadInput()
        {
            var kb = Keyboard.current;
            // [Q] suelta (con el celular abierto, [Q] es "llamar": lo atiende el celular).
            bool phoneBusy = Phone.Instance && (Phone.Instance.IsOpen || Phone.Instance.CallAvailable);
            if (!phoneBusy && ((kb != null && kb.qKey.wasPressedThisFrame)
                               || (GamepadGuard.Current != null && GamepadGuard.Current.buttonWest.wasPressedThisFrame)))
            {
                DropEquipped();
                return;
            }
            if (kb != null)
            {
                for (int i = 0; i < SlotCount; i++)
                    if (kb[Key.Digit1 + i].wasPressedThisFrame || kb[Key.Numpad1 + i].wasPressedThisFrame) { SelectSlot(i); return; }
            }
            int step = 0;
            var mouse = Mouse.current;
            // Ctrl + rueda (y el pellizco del touchpad) es el zoom de la cámara; la rueda sola cambia de objeto.
            bool ctrl = kb != null && (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed);
            if (mouse != null && !ctrl && Application.isFocused)
            {
                // Se acumula hasta una muesca entera: el touchpad manda valores mínimos sueltos que no son intención.
                float s = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(s) > 20f) s /= 120f;              // algunas plataformas mandan 120 por muesca
                scrollAccum = Mathf.Abs(s) > 0f ? scrollAccum + s : Mathf.MoveTowards(scrollAccum, 0f, Time.unscaledDeltaTime * 2f);
                if (scrollAccum >= 0.9f) { step = -1; scrollAccum = 0f; }
                else if (scrollAccum <= -0.9f) { step = 1; scrollAccum = 0f; }
            }
            var pad = GamepadGuard.Current;
            if (pad != null)
            {
                if (pad.dpad.right.wasPressedThisFrame) step = 1;
                else if (pad.dpad.left.wasPressedThisFrame) step = -1;
            }
            if (step != 0) Cycle(step);
        }

        /// <summary>Rueda / cruceta: pasa al espacio siguiente o anterior.</summary>
        void Cycle(int dir)
        {
            // Recorre los 5 espacios (también los vacíos) y da la vuelta.
            SetSelected((selected + dir + SlotCount) % SlotCount);
        }

        // ════════════════════════ MANO ════════════════════════
        void UpdateHand(float dt)
        {
            var phone = Phone.Instance;
            bool visible = Equipped != ItemId.None && !InspectionView.IsOpen && !MainMenuController.Active
                           && !(player && player.InputLocked && player.LookLocked)
                           // Con la pantalla del celular abierta, el celular "sube a la cara": la interfaz ES el celular.
                           && !(handItem == ItemId.Phone && phone && phone.IsOpen);
            // Cambiar de objeto: baja el que había, cambia el modelo, sube el nuevo.
            bool swapping = handItem != Equipped;
            raise = Mathf.MoveTowards(raise, visible && !swapping ? 1f : 0f, dt / (swapping ? 0.14f : 0.22f));
            if (raise <= 0f && swapping) BuildHandModel(Equipped);
            if (!handModel) return;

            var def = Def(handItem);
            var cam = hand.parent;
            // Inercia: la mano se atrasa un poco al girar la cámara y vuelve sola.
            if (dt > 0f)
            {
                var delta = Quaternion.Inverse(lastCamRot) * cam.rotation;
                var e = delta.eulerAngles;
                Vector2 turn = new(Mathf.DeltaAngle(0f, e.y), Mathf.DeltaAngle(0f, e.x));
                float k = GameSettings.CameraMotion;
                sway = Vector2.Lerp(sway, Vector2.ClampMagnitude(turn * 0.6f * k, 6f), 1f - Mathf.Exp(-dt * 8f));
                sway = Vector2.Lerp(sway, Vector2.zero, 1f - Mathf.Exp(-dt * 4f));
            }
            lastCamRot = cam.rotation;
            float r = Mathf.SmoothStep(0f, 1f, raise);
            float breathe = Mathf.Sin(Time.time * 1.4f) * 0.003f * GameSettings.CameraMotion;
            hand.localPosition = def.handPos + new Vector3(-sway.x * 0.002f, breathe + sway.y * 0.002f - (1f - r) * 0.28f, 0f);
            hand.localRotation = Quaternion.Euler(def.handEuler + new Vector3(sway.y * 1.2f, -sway.x * 1.2f, -sway.x * 0.8f));
            handModel.SetActive(raise > 0.001f);

            // En la mano, la pantalla se enciende cuando llega un mensaje sin leer.
            bool lit = phone && phone.Unread;
            if (handItem == ItemId.Phone && phoneScreen && lit != phoneLit) { phoneLit = lit; InspectModels.SetPhoneScreen(phoneScreen, lit); }
        }

        Renderer phoneScreen;
        bool phoneLit;

        void BuildHandModel(ItemId id)
        {
            if (handModel) Destroy(handModel);
            handModel = null;
            phoneScreen = null;
            phoneLit = false;
            handItem = id;
            if (id == ItemId.None || !modelFactories.TryGetValue(id, out var factory)) return;
            var cam = ViewModelLayer.Ensure();
            if (!cam) return;
            if (!hand)
            {
                hand = new GameObject("Mano").transform;
                hand.SetParent(cam.transform, false);
                lastCamRot = cam.transform.rotation;
            }
            handModel = factory();
            if (!handModel) return;
            ViewModelLayer.Apply(handModel, sceneLit: true);
            FitInHand(handModel.transform, Def(id).handSize);
            if (id == ItemId.Phone)
            {
                var s = handModel.transform.Find("Pantalla");
                if (s) phoneScreen = s.GetComponent<Renderer>();
            }
        }

        void FitInHand(Transform m, float size)
        {
            m.SetParent(hand, false);
            m.localPosition = Vector3.zero;
            m.localRotation = Quaternion.identity;
            m.localScale = Vector3.one;
            var rends = m.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) return;
            var inv = hand.worldToLocalMatrix;
            var b = new Bounds(inv.MultiplyPoint3x4(rends[0].bounds.center), Vector3.zero);
            foreach (var r in rends)
            {
                var wb = r.bounds;
                for (int i = 0; i < 8; i++)
                    b.Encapsulate(inv.MultiplyPoint3x4(wb.center + Vector3.Scale(wb.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            }
            float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            float k = longest > 1e-4f ? size / longest : 1f;
            m.localScale = Vector3.one * k;
            m.localPosition = -b.center * k;
        }
    }
}

namespace SomeonesHome
{
    /// <summary>Golpe suave cuando un objeto soltado toca el suelo (una vez por caída fuerte).</summary>
    public class DroppedItemSound : MonoBehaviour
    {
        public AudioClip clip;
        float last = -1f;

        void OnCollisionEnter(Collision c)
        {
            if (!clip || Time.time - last < 0.25f || c.relativeVelocity.sqrMagnitude < 0.8f) return;
            last = Time.time;
            OneShotAudio.Play(clip, transform.position, Mathf.Clamp01(c.relativeVelocity.magnitude * 0.15f));
        }
    }
}
