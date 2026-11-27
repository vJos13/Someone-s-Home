using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace SomeonesHome
{
    /// <summary>
    /// Relato 1 · "Across the River". Ver docs/RELATO_01_EL_RIO.md.
    ///
    /// Estructura (tensión que sube de a poco, como en los relatos "basados en hechos reales"):
    ///   0. Llegada: la camioneta, la mochila, la puerta principal trabada, explorar la casa (foto, nota, reloj, ventana).
    ///   1. Tarde en la cocina: mensajes de mamá, tareas (poner la mesa, sacar la basura).
    ///      Afuera el perro ladra… y se calla. Algo parado en el borde de la colina.
    ///   2. Llamar a Sam: buzón. Ir a buscarlo al río (campo → colina → rocas → columpio).
    ///   3. El río: la voz, la cosa del otro lado, el grito de Sam (beats originales).
    ///   4. El regreso: nos sigue. Cada vez que miras atrás está más cerca.
    ///   5. La noche: Sam te escribe desde su saco. Golpes, voces que imitan. Cortina, cerrojo, luz.
    ///      Se corta la luz: linterna y llave del candado, cobertizo, breaker. Si abres la puerta (sin motivo): final malo.
    ///   6. La mañana siguiente: Sam afuera, las huellas alrededor de la casa, el collar, llega papá.
    /// Antes de todo, si está puesto, el menú principal (MainMenuController) — el relato espera a que se pulse JUGAR.
    /// Pistas (foto, nota, reloj, collar, huellas): se examinan en 3D con InspectionView.
    /// Objetos (celular, bolsa, linterna, llave, collar): entran y salen del inventario (InventorySystem).
    /// Todo es una corrutina lineal; las tareas en paralelo usan corrutinas auxiliares.
    /// </summary>
    public class Level1Director : MonoBehaviour
    {
        [Header("Jugador")]
        [SerializeField] FirstPersonController player;

        [Header("Casa")]
        [SerializeField] Door backDoor;

        [Header("Triggers (en orden)")]
        [SerializeField] NarrativeTrigger fieldTrigger;
        [SerializeField] NarrativeTrigger hillTopTrigger;
        [SerializeField] NarrativeTrigger swingTrigger;
        [SerializeField] NarrativeTrigger climbTrigger;
        [SerializeField] NarrativeTrigger houseReturnTrigger;

        [Header("Actores")]
        [SerializeField] BrotherNPC brother;
        [SerializeField] RiverCreature creature;
        [SerializeField] Transform[] brotherRunPath;

        [Header("Sonido")]
        [Tooltip("Voz que llama tu nombre desde lo alto del campo (detrás del jugador).")]
        [SerializeField] AudioSource voiceFromField;
        [Tooltip("Voz que imita a Emily desde el río, durante el regreso.")]
        [SerializeField] AudioSource mimicFromRiver;
        [SerializeField] AudioSource swingCreak;
        [SerializeField] AudioSource brotherScream;
        [SerializeField] Transform swingSeat;

        [Header("Cinemática")]
        [SerializeField] CinematicShot revealShot;

        [Header("Tareas, acecho y noche")]
        [SerializeField] Relato1Props props;

        [Header("Textos")]
        [SerializeField] string playerName = "Alex";
        [SerializeField] string brotherName = "Sam";
        [SerializeField] bool skipIntro;

        HUD hud;
        TensionManager tension;
        Phone phone;
        Transform eye;
        bool swingMoving = true;
        bool finished;

        // Día
        enum Carry { None, Plates, Bag }
        Carry carrying;
        bool tableSet, trashOut;
        bool trashShown;                 // la bolsa ya está en el tacho (se tacha al instante, antes del vistazo)
        readonly List<Component> goalTargets = new();

        // Regreso / noche
        bool stalkerSteps;
        bool isNight, badEnding;
        bool curtainDone, boltDone, lightDone;
        bool errand;                                  // de noche, yendo al cobertizo: abrir la puerta no es el final malo
        bool breakerUp;
        GameObject tammyGO, emilyGO;
        NpcLife tammyLife, emilyLife;
        Pose tammyHome, emilyHome;
        readonly System.Collections.Generic.Dictionary<Material, Color> bulbEmission = new();

        /// <summary>Paso actual de la historia (lo lee el bot de pruebas).</summary>
        public string Beat { get; private set; } = "intro";
        /// <summary>La cosa está persiguiendo al jugador por el campo (el bot no se detiene a mirar atrás).</summary>
        public bool Chasing { get; private set; }
        /// <summary>Veces que la cosa alcanzó al jugador en la persecución (pruebas).</summary>
        public int ChaseCatches { get; private set; }

        /// <summary>Asiento del columpio del río (el menú principal encuadra ahí).</summary>
        public Transform SwingSeat => swingSeat;
        /// <summary>Llave del candado del cobertizo y el candado de la caja del breaker (se crean en el apagón).</summary>
        public SimpleInteractable ShedKey { get; private set; }
        public SimpleInteractable Padlock { get; private set; }
        /// <summary>Punto para examinar las huellas donde cambian (la mañana siguiente, opcional).</summary>
        public SimpleInteractable FootprintSpot { get; private set; }

        InventorySystem inventory;

        /// <summary>
        /// Botón "Saltar historia" (GameMenu): disponible durante las tarjetas de la intro y la charla de llegada,
        /// hasta que el jugador recibe el control. Saltar NO se salta código: las mismas corrutinas corren con el
        /// HUD en avance rápido (sin tarjetas ni líneas), así Sam igual sale al columpio, la puerta se destraba, etc.
        /// </summary>
        public bool CanSkipIntro { get; private set; }
        public bool IntroSkipped { get; private set; }

        public void SkipIntro()
        {
            if (!CanSkipIntro || IntroSkipped) return;
            IntroSkipped = true;
            hud.BeginFastForward();
        }

        void EndIntroSkip()
        {
            CanSkipIntro = false;
            if (IntroSkipped) hud.EndFastForward();
        }

        Vector3 P => player.transform.position;

        IEnumerator Start()
        {
            hud = HUD.Instance;
            tension = TensionManager.Instance;
            phone = Phone.Instance;
            eye = player.Head;
            creature.Init(eye);
            if (props && props.stalker) props.stalker.Init(eye);
            backDoor.SetLocked(true);
            backDoor.Opened += OnDoorOpened;
            // Los triggers se activan recién cuando la historia los espera (si el jugador pasa antes, no se pierden).
            foreach (var t in new[] { fieldTrigger, hillTopTrigger, swingTrigger, climbTrigger, houseReturnTrigger })
                if (t) t.gameObject.SetActive(false);
            tammyGO = GameObject.Find("Tammy");
            emilyGO = GameObject.Find("Emily");
            if (tammyGO) tammyHome = new Pose(tammyGO.transform.position, tammyGO.transform.rotation);
            if (emilyGO) emilyHome = new Pose(emilyGO.transform.position, emilyGO.transform.rotation);
            // Modelos animados (Blender): caminan por la cocina, te miran y gesticulan al hablar.
            if (tammyGO) tammyLife = NpcLife.Setup(tammyGO, "Tammy_Rig", "Tammy", tammyGO.transform.Find("Tammy (modelo)"));
            if (emilyGO) emilyLife = NpcLife.Setup(emilyGO, "Emily_Rig", "Emily", emilyGO.transform.Find("Emily (modelo)"));
            if (!(props && props.arrivalSpawn)) KitchenRoutines();
            SetupDay();
            SetupInventory();

            // ── Menú principal (misma escena): el relato espera a JUGAR ──
            Lock(true, true);
            while (MainMenuController.Active) yield return null;
            inventory?.Add(ItemId.Phone, equip: false);

            // ── 0 · INTRO ──────────────────────────────────────────────
            Lock(true, true);
            hud.SetFadeInstant(1f);
            bool arrival = props && props.arrivalSpawn;
            if (arrival) PlaceArrival();
            CanSkipIntro = true;
            if (!skipIntro)
            {
                yield return hud.ShowCard("SOMEONE'S HOME", 2.5f);
                yield return hud.ShowCard("Basado en hechos reales.", 2.5f);
                yield return hud.ShowCard("Wenatchee, Washington.\n\nNuestros padres estaban fuera del estado,\nasí que nos quedamos con la familia de mi amiga.", 4.5f);
                if (arrival) yield return hud.ShowCard("Llegamos un viernes, en la tarde.", 2.5f);
            }
            tension.SetTarget(0f);
            yield return hud.Fade(0f, 2f);
            Lock(true, false); // puede mirar, no caminar, mientras le hablan

            // ── 0b · LA LLEGADA ───────────────────────────────────────
            if (arrival) yield return Arrival();
            else EndIntroSkip();

            // ── 1 · LA COCINA (todo normal… por ahora) ────────────────
            Beat = "cocina";
            yield return new WaitForSeconds(1.2f);
            if (phone)
            {
                phone.Receive("Mamá", "¿Cómo va todo por allá? Pórtense bien con Tammy.");
                yield return new WaitForSeconds(2.2f);
                phone.Receive("Mamá", "Te queremos. Avísame si necesitan algo.");
                yield return new WaitForSeconds(1.5f);
                yield return hud.SayAndWait("", "Mamá. Le contesto después.");
            }
            yield return hud.SayAndWait("Tammy", $"{playerName}, ¿me das una mano? La cena ya casi está.");
            yield return hud.SayAndWait("Tammy", "Pon la mesa y saca la basura, porfa. El tacho está afuera, junto a la casa.");
            yield return hud.SayAndWait(playerName, $"¿Y {brotherName}?");
            yield return hud.SayAndWait("Emily", "Sigue en el río, creo.");
            yield return hud.SayAndWait("Tammy", "Ahorita lo llamas para que venga a comer.");
            Lock(false, false);
            backDoor.SetLocked(false);

            // ── 2 · TAREAS ────────────────────────────────────────────
            Beat = "tareas";
            UpdateChoresObjective();
            if (props)
            {
                StartCoroutine(TableChore());
                StartCoroutine(TrashChore());
                yield return new WaitUntil(() => tableSet && trashOut);
                hud.CompleteObjective();
            }
            yield return new WaitForSeconds(1f);

            // ── 3 · LLAMAR A SAM ──────────────────────────────────────
            Beat = "llamar";
            yield return hud.SayAndWait("Tammy", $"¡Gracias, {playerName}! Ahora llama a {brotherName}, porfa, que se enfría la comida.");
            if (phone)
            {
                hud.SetObjective($"Llama a {brotherName}  [Q]");
                phone.CallContact = brotherName;
                phone.CallAvailable = true;
                phone.CallRequested = false;
                yield return new WaitUntil(() => phone.CallRequested);
                phone.CallAvailable = false;
                hud.CompleteObjective();
                hud.SetObjective(null);
                yield return phone.Call(brotherName, 2, $"Hola, soy {brotherName}. Deja tu mensaje… o no.");
                yield return hud.SayAndWait("", "No contesta. Siempre contesta.");
            }
            yield return hud.SayAndWait("Tammy", "¿No contesta? Ve a buscarlo al río, porfa.");
            hud.SetObjective($"Busca a {brotherName} junto al río");

            // ── 4 · EL CAMPO ──────────────────────────────────────────
            Beat = "campo";
            // El trigger del campo (franja z 70–74) se activa recién aquí. Si el jugador ya cruzó el campo durante la
            // llamada, o lo rodea por un costado, nunca lo pisaría y la escena del río no empezaría: basta con estar más allá.
            yield return WaitTrigger(fieldTrigger, () => P.z > 75f);
            tension.SetTarget(0.08f);
            hud.Say("", "El patio trasero en realidad es un campo.");

            // ── 5 · LA COLINA ─────────────────────────────────────────
            // Si baja la ladera por otro lado (o rápido) y se salta el trigger, igual sigue.
            yield return WaitTrigger(hillTopTrigger, () => P.z > 106f);
            Beat = "colina";
            hud.SetObjective("Baja por las rocas");
            hud.Say("", "Una colina estúpidamente empinada. Hay que ir pisando las rocas… o sales volando.");

            // ── 6 · EL COLUMPIO ───────────────────────────────────────
            // El trigger está frente al columpio; si llega por un costado o por detrás de Sam, basta con acercarse.
            yield return WaitTrigger(swingTrigger, () => NearBrother(9f));
            Beat = "columpio";
            hud.CompleteObjective();
            hud.SetObjective(null);
            tension.SetTarget(0.15f);
            yield return hud.SayAndWait("", $"Ahí está {brotherName}, en el columpio. Podría empujarlo un poco y asustarlo…");
            yield return new WaitForSeconds(1.5f);

            // ── 7 · LA VOZ A MIS ESPALDAS ─────────────────────────────
            Beat = "voz";
            if (voiceFromField) voiceFromField.Play();
            hud.Say("???", $"{Stretch(playerName)}…", 2.5f);
            tension.SetTarget(0.25f);

            float waited = 0f;
            while (GazeUtility.FlatAngleTo(eye, voiceFromField.transform.position) > 45f)
            {
                waited += Time.deltaTime;
                if (waited > 7f) { hud.Say("", "¿Emily? Venía de arriba, del campo."); waited = -5f; }
                yield return null;
            }

            // ── 8 · NO HAY NADIE (mientras tanto, detrás…) ────────────
            yield return hud.SayAndWait("", "No hay nadie en el campo.");
            swingMoving = false;
            if (swingCreak) swingCreak.Stop();
            tension.SilenceCrickets(true);
            brother.StandAndStare(creature.transform.position);
            creature.Appear();
            tension.SetTarget(0.4f);
            hud.Say("", "Siempre he tenido mucha imaginación. Seguro fue eso.");

            float lost = 0f;
            while (GazeUtility.FlatAngleTo(eye, brother.transform.position) > 40f)
            {
                lost += Time.deltaTime;
                if (lost > 6f) { hud.Say("", $"¿Y {brotherName}? Ya no se escucha el columpio."); lost = -6f; }
                yield return null;
            }

            // ── 9 · LA REVELACIÓN (Cinemachine) ───────────────────────
            Beat = "revelacion";
            player.FaceTowards(creature.transform.position);
            tension.SetTarget(0.6f);
            yield return revealShot.Play(player, 3.5f);
            yield return hud.SayAndWait("", $"{brotherName} está de pie, mirando al otro lado del río.");
            yield return hud.SayAndWait("", "Algo parecido a un venado. Parado en sus patas traseras.");
            hud.Say("", "Otra vez mi imaginación.");

            // ── 10 · APARTAR LA MIRADA… Y VOLVER A MIRAR ─────────────
            float away = 0f, staring = 0f;
            bool hinted = false;
            while (away < 2.5f)
            {
                bool looking = GazeUtility.IsLookingAt(eye, creature.LookTarget.position, 30f, false);
                away = looking ? 0f : away + Time.deltaTime;
                staring = looking ? staring + Time.deltaTime : staring;
                if (!hinted && staring > 9f) { hinted = true; hud.Say("", "Aparto la vista unos segundos. Tiene que ser mi imaginación."); }
                yield return null;
            }
            brother.SetShaking(true);
            creature.StartSpeaking();
            tension.SetTarget(0.75f);

            float notLooking = 0f;
            while (!GazeUtility.IsLookingAt(eye, creature.LookTarget.position, 20f, false))
            {
                notLooking += Time.deltaTime;
                if (notLooking > 6f) { hud.Say("", "Tengo que volver a mirar. Al otro lado del río."); notLooking = -6f; }
                yield return null;
            }
            Beat = "habla";
            tension.SetTarget(0.95f);
            hud.Say("???", $"…{Stretch(playerName)}…", 3f);
            yield return hud.SayAndWait("", "Sigue ahí. Pero ahora su boca se mueve.");
            yield return hud.SayAndWait("", "Está diciendo mi nombre.");
            yield return new WaitForSeconds(1.5f);

            // ── 11 · EL GRITO Y EL DIÁLOGO ────────────────────────────
            Beat = "dialogo";
            Lock(true, false);
            StartCoroutine(VanishWhenNotObserved());
            yield return brother.TurnTo(player.transform.position, 0.35f);
            brother.SetShaking(false);
            brother.Scream();
            if (brotherScream) brotherScream.Play();
            yield return hud.SayAndWait(brotherName, "¡AAAH!", 1.2f);
            brother.SetShaking(true);                      // habla temblando, abrazándose
            tension.SetTarget(0.7f);
            yield return hud.SayAndWait(playerName, "¿Qué pasó?");
            yield return hud.SayAndWait(brotherName, "¿Tú también lo viste?");
            yield return hud.SayAndWait(playerName, "¿El venado?");
            yield return hud.SayAndWait(brotherName, "No era un venado. No sé qué era esa cosa, pero no era un venado.");
            yield return hud.SayAndWait(playerName, "¿Te estaba diciendo mi nombre?");
            yield return hud.SayAndWait(brotherName, "No. Estaba diciendo MI nombre.");
            yield return hud.SayAndWait(playerName, "Hablamos después. Me mandaron a buscarte hace cinco minutos. La cena está lista.");

            // ── 12 · EL REGRESO: NOS SIGUE ────────────────────────────
            Beat = "regreso";
            Lock(false, false);
            hud.SetObjective("Vuelve a la casa");
            StartCoroutine(brother.RunAlong(brotherRunPath));
            var mimic = StartCoroutine(MimicOnClimb());
            var stalk = props && props.stalker ? StartCoroutine(StalkerReturn()) : null;
            var steps = StartCoroutine(StepsBehind());

            yield return WaitTrigger(houseReturnTrigger);
            hud.CompleteObjective();
            StopCoroutine(mimic);
            if (stalk != null) StopCoroutine(stalk);
            StopCoroutine(steps);
            stalkerSteps = false;
            Chasing = false;
            if (props && props.stalker) props.stalker.Hide();
            Lock(true, true);
            hud.SetObjective(null);
            yield return hud.Fade(1f, 1.5f);
            yield return hud.ShowCard("Entramos y cenamos.\nNo dijimos nada.", 3.5f);

            // ── 13 · LA NOCHE ─────────────────────────────────────────
            if (props) { yield return Night(); if (badEnding) yield break; }

            // ── 14 · LA MAÑANA SIGUIENTE ──────────────────────────────
            if (props && props.trail != null && props.trail.Length > 11) yield return MorningAfter();
            else
            {
                yield return hud.ShowCard("No dormimos.", 2.5f);
                yield return hud.ShowCard("A la mañana siguiente había huellas alrededor de la casa.\n\nNo eran de venado.", 4.5f);
            }

            // ── 15 · EPÍLOGO ──────────────────────────────────────────
            Beat = "epilogo";
            yield return hud.ShowCard($"Un año después, {brotherName} entró a mi cuarto.\n\n\"Investigué. Pudimos haber muerto si nos acercábamos.\nResultó que era un skinwalker.\"", 6f);
            yield return hud.ShowCard($"{brotherName} todavía tiene pesadillas.\nSolo habla de eso cuando él empieza la conversación.", 4.5f);
            yield return hud.ShowCard("Nunca volvimos a Wenatchee.", 3.5f);
            Beat = "fin";
            finished = true;
            FirstPersonController.LockCursor(false);
            yield return hud.ShowCardStay("SOMEONE'S HOME\n<size=26>Relato 1 · Across the River</size>\n\n<size=20>[R] Volver a jugar</size>");
        }

        // ════════════════════════════════════════════════════════════════
        //  DÍA: TAREAS
        // ════════════════════════════════════════════════════════════════
        void SetupDay()
        {
            if (!props) return;
            Show(props.nightOnly, false);
            Show(props.dayOnly, true);
            if (props.tableSetting) props.tableSetting.SetActive(false);
            if (props.curtainClosed) props.curtainClosed.SetActive(false);
            Avail(props.platesStack, false); Avail(props.table, false);
            Avail(props.trashBag, false); Avail(props.trashBin, false);
            Avail(props.window, false); Avail(props.lightSwitch, false);
            Avail(props.deadbolt, false); Avail(props.sleepingBag, false);
            Avail(props.interiorDoor, true);
            if (props.interiorDoor) props.interiorDoor.Used += _ =>
                hud.Say("", isNight ? "Tammy y Emily duermen ahí. Mejor no despertarlas." : "El pasillo a los cuartos.");
            if (props.stalker) props.stalker.Hide();
            SetSamPhone(false);
            Show(props.morningOnly, false);
            Avail(props.backpack, false); Avail(props.backpackDrop, false); Avail(props.frontDoor, false);
            if (props.backpackDrop) props.backpackDrop.gameObject.SetActive(false);
            if (props.inspect != null) foreach (var i in props.inspect) Avail(i, false);
            Avail(props.flashlightPickup, false); Avail(props.breaker, false);
            Avail(props.collar, false); Avail(props.dadTruck, false);
            if (props.flashlight) props.flashlight.Take();
        }

        // ════════════════════════════════════════════════════════════════
        //  LLEGADA
        // ════════════════════════════════════════════════════════════════
        void PlaceArrival()
        {
            player.Teleport(props.arrivalSpawn.position + Vector3.up * 0.1f, props.arrivalSpawn.rotation);
            player.Pitch = 4f;
            if (tammyGO && props.tammyArrival) tammyGO.transform.SetPositionAndRotation(props.tammyArrival.position, props.tammyArrival.rotation);
            if (emilyGO && props.emilyArrival) emilyGO.transform.SetPositionAndRotation(props.emilyArrival.position, props.emilyArrival.rotation);
            if (brother && props.samArrival) brother.PlaceStanding(props.samArrival.position, P);
        }

        IEnumerator Arrival()
        {
            Beat = "llegada";
            yield return new WaitForSeconds(0.8f);
            yield return hud.SayAndWait("Tammy", "¡Llegamos! Bienvenidos a la casa.");
            yield return hud.SayAndWait("Emily", "Es vieja, pero es bonita. Te va a gustar.");
            yield return hud.SayAndWait("Tammy", "Bajen sus cosas de la camioneta. Yo voy adentro a ver qué hay para la cena.");
            yield return hud.SayAndWait(brotherName, "¿Ese es el campo? ¿El río está por allá?");
            yield return hud.SayAndWait("Emily", "Abajo de la colina. Hay un columpio en la orilla.");
            yield return hud.SayAndWait(brotherName, "¡Voy a verlo!");
            if (brother && props.samRunOff != null && props.samRunOff.Length > 0) StartCoroutine(SamRunsOff());
            yield return hud.SayAndWait("Tammy", $"¡{brotherName}! …No te alejes mucho. Y vuelve antes de que oscurezca.");
            EndIntroSkip();                                // aquí termina la intro: lo que sigue es jugable
            Lock(false, false);
            StartCoroutine(GoInsideWhenUnseen());

            // ── La mochila ──
            Beat = "mochila";
            backDoor.SetLocked(false);
            const string bpTake = "Saca tu mochila de la camioneta", bpDrop = "Déjala en la sala";
            hud.SetObjective(Task(false, bpTake) + "\n" + Task(false, bpDrop));
            Avail(props.backpack, true);
            Goal(props.backpack);
            yield return WaitUse(props.backpack, null);         // [E] Agarrar mochila → inventario
            Avail(props.backpack, false);
            props.backpack.gameObject.SetActive(false);
            hud.SetObjective(Task(true, bpTake) + "\n" + Task(false, bpDrop));     // tachado al instante
            Goal();
            bool triedFront = false;
            Avail(props.frontDoor, true);
            void Front(SimpleInteractable _)
            {
                hud.Say(triedFront ? "Tammy" : "", triedFront ? "¡Esa no abre! ¡Entra por la de atrás!" : "Está trabada. No se mueve.");
                triedFront = true;
            }
            if (props.frontDoor) props.frontDoor.Used += Front;
            props.backpackDrop.gameObject.SetActive(true);
            yield return Deliver(ItemId.Backpack, props.backpackDrop, 1.8f);   // [E] aquí o soltarla con [Q] al lado
            hud.CompleteObjective();
            Avail(props.backpackDrop, false);
            props.backpackDrop.gameObject.SetActive(false);
            if (props.frontDoor) { props.frontDoor.Used -= Front; Avail(props.frontDoor, false); }
            props.backpack.Item = ItemId.None;                  // desde aquí es decorado
            Avail(props.backpack, false);
            props.backpack.transform.SetPositionAndRotation(props.backpackPlaced.position, props.backpackPlaced.rotation);
            props.backpack.gameObject.SetActive(true);
            hud.SetObjective(null);
            yield return hud.SayAndWait("Tammy", "Deja tus cosas en la sala. Esta noche ustedes duermen ahí, en sacos.");

            // ── Explorar la casa ──
            Beat = "explorar";
            var items = props.inspect;
            int total = items != null ? items.Length : 0, seenCount = 0;
            var seen = new bool[total];
            if (items != null) foreach (var i in items) Avail(i, true);
            hud.SetObjective($"Explora la casa (0/{total})");
            float idle = 0f;
            while (seenCount < total)
            {
                bool any = false;
                for (int i = 0; i < total; i++)
                {
                    if (seen[i] || !items[i] || items[i].UseCount == 0) continue;
                    seen[i] = true; any = true; seenCount++;
                    Avail(items[i], false);
                    hud.SetObjective($"Explora la casa ({seenCount}/{total})");
                    // La foto, la nota y el reloj se examinan de cerca; la ventana es solo mirar afuera.
                    switch (i)
                    {
                        case 0: InspectionView.Open(InspectModels.Photo(), "Foto vieja"); break;
                        case 1: InspectionView.Open(InspectModels.FridgeNote(), "Nota del refri", default, 0.22f); break;
                        case 2: InspectionView.Open(InspectModels.Clock(), "Reloj de la pared"); break;
                    }
                    yield return InspectLines(i);
                    yield return InspectionView.WaitClosed();
                }
                idle = any ? 0f : idle + Time.deltaTime;
                if (idle > 40f) { idle = -999f; hud.Say("", "La foto de la pared, el refri, el reloj, la ventana…"); }
                yield return null;
            }
            hud.CompleteObjective();
            hud.SetObjective(null);
            yield return new WaitForSeconds(1f);
            yield return hud.SayAndWait("Tammy", $"{playerName}, ¿ya te instalaste? Ven a la cocina un momento.");
        }

        IEnumerator InspectLines(int i)
        {
            switch (i)
            {
                case 0:
                    yield return hud.SayAndWait("", "Una foto vieja: la familia de Tammy en la orilla del río.");
                    yield return hud.SayAndWait("", "Atrás, entre los árboles, hay alguien. Muy alto. Muy flaco.");
                    yield return hud.SayAndWait("", "Seguro es un tronco.");
                    break;
                case 1:
                    yield return hud.SayAndWait("", "Una nota en el refri, con letra de adulto:");
                    yield return hud.SayAndWait("", "\"Cuando oscurezca: cerrojo puesto. No le abran a nadie. Aunque sea alguien conocido.\"");
                    yield return hud.SayAndWait("Emily", "La escribió mi papá. Dice que hay coyotes.");
                    yield return hud.SayAndWait("Emily", "Yo digo que no son coyotes.");
                    break;
                case 2:
                    yield return hud.SayAndWait("", "El reloj está parado en las 3:07.");
                    yield return hud.SayAndWait("", "Nadie le cambió la pila, supongo.");
                    break;
                default:
                    yield return hud.SayAndWait("", "Desde aquí se ve todo el campo. Termina de golpe, en la colina.");
                    yield return hud.SayAndWait("", $"{brotherName} ya no se ve. Debe estar abajo, en el río.");
                    break;
            }
        }

        IEnumerator SamRunsOff()
        {
            var pts = System.Array.ConvertAll(props.samRunOff, t => t.position);
            yield return brother.RunAlong(pts, true);
            brother.ResetToSwing();                        // ya está en el columpio cuando llegues al río
        }

        /// <summary>Tammy y Emily entran a la casa cuando no las estás mirando.</summary>
        IEnumerator GoInsideWhenUnseen()
        {
            // Caminan hacia la puerta de atrás de verdad; entran (se teletransportan) cuando no las miras.
            var door = new Vector3(92.3f, 0f, 64.9f);
            if (tammyLife) { var d = door; d.y = tammyLife.transform.position.y; StartCoroutine(tammyLife.WalkTo(d)); }
            if (emilyLife) { var d = door + new Vector3(-0.8f, 0f, 0.6f); d.y = emilyLife.transform.position.y; StartCoroutine(emilyLife.WalkTo(d, 1.25f)); }
            float t = 0f;
            while (t < 25f)
            {
                bool seenT = tammyGO && GazeUtility.FlatAngleTo(eye, tammyGO.transform.position) < 70f;
                bool seenE = emilyGO && GazeUtility.FlatAngleTo(eye, emilyGO.transform.position) < 70f;
                if (!seenT && !seenE) break;
                t += Time.deltaTime;
                yield return null;
            }
            if (tammyGO) tammyGO.transform.SetPositionAndRotation(tammyHome.position, tammyHome.rotation);
            if (emilyGO) emilyGO.transform.SetPositionAndRotation(emilyHome.position, emilyHome.rotation);
            KitchenRoutines();
        }

        /// <summary>En la cocina: Tammy va y viene por la mesada cocinando; Emily da vueltas cerca de la mesa.</summary>
        void KitchenRoutines()
        {
            if (tammyLife)
            {
                tammyLife.StopWalking();
                Vector3 h = tammyHome.position;
                tammyLife.SetRoute(h, new Vector3(85.25f, h.y, 59.4f), h, new Vector3(85.3f, h.y, 55.9f));
            }
            if (emilyLife)
            {
                emilyLife.StopWalking();
                Vector3 h = emilyHome.position;
                emilyLife.SetRoute(h, new Vector3(90.7f, h.y, 56.6f), h, new Vector3(90.4f, h.y, 59.9f));
            }
        }

        void UpdateChoresObjective()
        {
            string Line(bool done, string s) => done ? $"<s><color=#999>{s}</color></s>" : s;
            bool plates = inventory ? inventory.Has(ItemId.Plates) : carrying == Carry.Plates;
            bool bag = inventory ? inventory.Has(ItemId.TrashBag) : carrying == Carry.Bag;
            string table = plates ? "Pon los platos en la mesa" : "Pon la mesa (platos en la mesada)";
            string trash = bag ? "Tira la bolsa al tacho de afuera" : "Saca la basura";
            hud.SetObjective(Line(tableSet, table) + "\n" + Line(trashOut || trashShown, trash));
        }

        IEnumerator TableChore()
        {
            Avail(props.platesStack, true);
            ObjectiveHighlighter.Mark(props.platesStack);
            yield return WaitUse(props.platesStack, inventory ? null : () => carrying == Carry.None);
            carrying = Carry.Plates;
            props.platesStack.gameObject.SetActive(false);
            UpdateChoresObjective();
            yield return Deliver(ItemId.Plates, props.table, 1.9f);     // [E] en la mesa o soltarlos encima con [Q]
            Avail(props.table, false);
            carrying = Carry.None;
            props.tableSetting.SetActive(true);
            tableSet = true;
            UpdateChoresObjective();
            hud.Say("Emily", "Gracias.");
        }

        IEnumerator TrashChore()
        {
            Avail(props.trashBag, true);
            ObjectiveHighlighter.Mark(props.trashBag);
            yield return WaitUse(props.trashBag, inventory ? null : () => carrying == Carry.None);
            carrying = Carry.Bag;
            props.trashBag.gameObject.SetActive(false);
            if (inventory && !inventory.Has(ItemId.TrashBag)) inventory.Add(ItemId.TrashBag, equip: true);
            UpdateChoresObjective();
            yield return Deliver(ItemId.TrashBag, props.trashBin, 1.6f); // [E] en el tacho o botarla al lado con [Q]
            trashShown = true;
            Avail(props.trashBin, false);
            carrying = Carry.None;
            Avail(props.trashBag, false);                  // la bolsa queda adentro del tacho tapado
            UpdateChoresObjective();
            yield return Glimpse();
            trashOut = true;
            UpdateChoresObjective();
        }

        /// <summary>
        /// Entrega de un objeto del inventario en su lugar: con [E] sobre el destino (solo disponible si lo llevas)
        /// o soltándolo con [Q] cerca. Al entregarlo sale del inventario y desaparece la copia soltada.
        /// Si se suelta lejos, queda en el suelo y se vuelve a agarrar con [E].
        /// </summary>
        IEnumerator Deliver(ItemId id, SimpleInteractable target, float radius)
        {
            if (!target) yield break;
            if (!inventory)
            {
                Avail(target, true);
                yield return WaitUse(target, null);
                yield break;
            }
            bool done = false;
            void OnDrop(ItemId dropped, GameObject copy)
            {
                if (dropped != id || !copy) return;
                Vector3 d = copy.transform.position - target.transform.position;
                if (new Vector2(d.x, d.z).sqrMagnitude <= radius * radius && Mathf.Abs(d.y) < 2.5f) done = true;
                else ObjectiveHighlighter.Mark(copy.transform);   // soltado lejos: brilla él hasta que lo recojas
            }
            // El destino brilla solo mientras llevas el objeto (su Available lo sigue).
            ObjectiveHighlighter.Mark(target);
            void OnChanged() => UpdateChoresObjectiveIfActive();
            inventory.Dropped += OnDrop;
            inventory.Changed += OnChanged;
            int start = target.UseCount;
            while (!done)
            {
                target.Available = inventory.Has(id);
                if (target.UseCount > start) done = true;
                yield return null;
            }
            inventory.Dropped -= OnDrop;
            inventory.Changed -= OnChanged;
            ObjectiveHighlighter.Mark(target, false);
            target.Available = false;
            inventory.Remove(id);
            inventory.ClearWorldCopies(id);
            OneShotAudio.Play(ProceduralAudio.Build(ProceduralAudio.Kind.Knock, 0.12f, 9), target.transform.position, 0.35f);
        }

        /// <summary>Línea de una lista de tareas: tachada y atenuada si ya se cumplió.</summary>
        static string Task(bool done, string text) => done ? $"<s><color=#999>{text}</color></s>" : text;

        /// <summary>Los objetos que pide el objetivo actual brillan (ObjectiveHighlighter); los anteriores se apagan.</summary>
        void Goal(params Component[] targets)
        {
            foreach (var t in goalTargets) ObjectiveHighlighter.Mark(t, false);
            goalTargets.Clear();
            foreach (var t in targets)
            {
                if (!t) continue;
                ObjectiveHighlighter.Mark(t, true);
                goalTargets.Add(t);
            }
        }

        void UpdateChoresObjectiveIfActive()
        {
            if (Beat == "tareas") UpdateChoresObjective();
        }

        /// <summary>Junto al tacho: el perro se pone loco, se calla de golpe… y hay algo en la colina.</summary>
        IEnumerator Glimpse()
        {
            Beat = "vistazo";
            var dog = props.dog;
            var s = props.stalker;
            yield return new WaitForSeconds(0.8f);
            if (dog) dog.Frantic();
            tension.SetTarget(0.15f);
            yield return new WaitForSeconds(2.2f);
            if (s && props.glimpsePoint)
            {
                // Aparece donde no estás mirando.
                float t = 0f;
                while (GazeUtility.FlatAngleTo(eye, props.glimpsePoint.position) < 50f && t < 6f) { t += Time.deltaTime; yield return null; }
                s.PlaceAt(props.glimpsePoint.position, P);
            }
            yield return new WaitForSeconds(1.2f);
            if (dog) dog.Silence();
            tension.SilenceCrickets(true);
            yield return hud.SayAndWait("", "El perro se calló de golpe.");
            if (!s) { tension.SilenceCrickets(false); yield break; }

            float wait = 0f;
            bool hinted = false;
            while (!s.WasSeen && wait < 14f)
            {
                wait += Time.deltaTime;
                if (!hinted && wait > 5f) { hinted = true; hud.Say("", "Siento que alguien me mira. Desde el campo."); }
                yield return null;
            }
            if (s.WasSeen)
            {
                tension.SetTarget(0.3f);
                yield return hud.SayAndWait("", "¿Un venado? …Está parado muy derecho.");
                float t = 0f;
                while (s.PlayerLooking(30f) && t < 8f) { t += Time.deltaTime; yield return null; }
                s.Hide();
                t = 0f;
                while (GazeUtility.FlatAngleTo(eye, props.glimpsePoint.position) > 15f && t < 6f) { t += Time.deltaTime; yield return null; }
                yield return hud.SayAndWait("", "Ya no está.");
            }
            else
            {
                s.Hide();
            }
            tension.SilenceCrickets(false);
            tension.SetTarget(0.1f);
        }

        void OnDoorOpened()
        {
            // De día: al abrir por primera vez, se oye al perro de los vecinos.
            if (!isNight && props && props.dog && !props.dog.Barking && !trashOut) props.dog.StartBarking();
            // De noche: abrir la puerta es el final malo.
            if (isNight && !errand && !badEnding && !finished) StartCoroutine(BadEnding());
        }

        // ════════════════════════════════════════════════════════════════
        //  REGRESO: EL ACECHO
        // ════════════════════════════════════════════════════════════════
        IEnumerator StalkerReturn()
        {
            var pts = props.stalkerPoints;
            if (pts == null || pts.Length < 4) yield break;
            yield return new WaitForSeconds(3f);
            stalkerSteps = true;
            yield return StalkBeat(pts[0], () => P.z < 117.5f && P.y > 9f);      // subiendo por las rocas → en la orilla
            yield return StalkBeat(pts[1], () => P.y > 18.5f);                    // arriba de la colina → al pie de las rocas
            // Ya en el campo: deja de esconderse y corre hacia ti desde el borde de la colina.
            yield return new WaitUntil(() => P.y > 18.5f && P.z < 99f);
            yield return FieldChase(pts[2].position);
        }

        // ════════════════════════════════════════════════════════════════
        //  PERSECUCIÓN POR EL CAMPO
        // ════════════════════════════════════════════════════════════════
        const float ChaseSpeed = 5.6f;          // corriendo (4.4 m/s) llegas con unos metros de ventaja; caminando no
        const float ChaseCatchUp = 6.8f;        // si queda muy atrás, acelera (nunca deja de ser una amenaza)
        const float CatchDistance = 1.5f;
        static AudioClip shriekClip;

        IEnumerator FieldChase(Vector3 spawn)
        {
            var s = props.stalker;
            Vector3 restart = P;
            Quaternion restartRot = player.transform.rotation;
            var hoof = ProceduralAudio.Build(ProceduralAudio.Kind.Step, 0.3f, 57);
            shriekClip ??= CreatureShriek();
            while (true)
            {
                stalkerSteps = false;
                s.PlaceAt(Stalker.Ground(spawn), P);
                s.Chasing = true;
                Chasing = true;
                OneShotAudio.Play(shriekClip, s.HeadPosition, 1f);
                VhsLook.Pulse(0.8f, 0.9f);
                tension.SetTarget(1f);
                hud.SetObjective("¡CORRE A LA CASA!");
                hud.Say("", ChaseCatches == 0 ? "¡Viene corriendo!" : "¡Otra vez! ¡No te detengas!");

                // Un instante quieta en el borde, mirándote… y arranca.
                for (float t = 0f; t < 1.1f; t += Time.deltaTime) yield return null;
                float speed = 1.5f, nextHoof = 0f;
                while (true)
                {
                    float dt = Time.deltaTime;
                    Vector3 flat = s.transform.position - P; flat.y = 0f;
                    float dist = flat.magnitude;
                    speed = Mathf.MoveTowards(speed, dist > 20f ? ChaseCatchUp : ChaseSpeed, 3.5f * dt);
                    s.ChaseStep(P, speed, dt);
                    // Pezuñas en el pasto, cada vez más rápidas y fuertes al acercarse.
                    nextHoof -= dt;
                    if (nextHoof <= 0f)
                    {
                        nextHoof = Mathf.Lerp(0.2f, 0.32f, Mathf.InverseLerp(6.5f, 3f, speed));
                        OneShotAudio.Play(hoof, s.transform.position, Mathf.Lerp(1f, 0.45f, dist / 25f));
                    }
                    VhsLook.Pulse(Mathf.Clamp01(1f - dist / 14f) * 0.35f, 0.15f);
                    if (dist < CatchDistance) break;
                    yield return null;
                }

                // ── Te alcanzó ──
                ChaseCatches++;
                yield return Caught(s);
                s.Hide();
                Chasing = false;
                player.Teleport(restart, restartRot);
                player.Pitch = 4f;
                yield return new WaitForSeconds(0.6f);
                yield return hud.Fade(0f, 1.2f);
                Lock(false, false);
                yield return new WaitForSeconds(1.4f);     // ventaja: arranca de nuevo atrás tuyo
            }
        }

        IEnumerator Caught(Stalker s)
        {
            Lock(true, true);
            s.Chasing = false;
            Vector3 fwd = player.transform.forward; fwd.y = 0f; fwd.Normalize();
            // Se planta delante, encima de ti; la cámara gira de golpe hacia su cara.
            Vector3 at = Stalker.Ground(P + fwd * 1.3f);
            s.PlaceAt(at, P);
            Vector3 toHead = s.HeadPosition - eye.position;
            player.Teleport(P, Quaternion.LookRotation(new Vector3(toHead.x, 0f, toHead.z)));
            player.Pitch = -Mathf.Atan2(toHead.y, new Vector2(toHead.x, toHead.z).magnitude) * Mathf.Rad2Deg;
            OneShotAudio.Play(shriekClip, s.HeadPosition, 1f);
            VhsLook.Pulse(1.6f, 1.3f);
            tension.SetTarget(1f);
            yield return new WaitForSeconds(0.75f);
            hud.SetFadeInstant(1f);
            hud.SetObjective(null);
            yield return hud.ShowCard("Te alcanzó.", 1.6f);
        }

        /// <summary>Chillido de la cosa: un grito casi humano que cae de tono, con aire y aspereza (sintetizado).</summary>
        static AudioClip CreatureShriek()
        {
            const int rate = 22050;
            int n = (int)(rate * 1.4f);
            var data = new float[n];
            var rnd = new System.Random(7);
            float phase = 0f, phase2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float f = Mathf.Lerp(1150f, 420f, Mathf.Pow(t / 1.4f, 0.7f)) * (1f + 0.04f * Mathf.Sin(t * 38f));
                phase += f / rate; phase2 += f * 1.51f / rate;
                float saw = 2f * (phase - Mathf.Floor(phase + 0.5f));
                float tone = saw * 0.6f + Mathf.Sin(2f * Mathf.PI * phase2) * 0.3f;
                float noise = (float)(rnd.NextDouble() * 2 - 1);
                float env = Mathf.Min(1f, t * 25f) * Mathf.Pow(1f - t / 1.4f, 1.6f);
                data[i] = Mathf.Clamp((tone * 0.75f + noise * 0.35f) * env * 0.8f, -1f, 1f);
            }
            var clip = AudioClip.Create("SFX_ChillidoCosa", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        bool firstStalkSeen;

        IEnumerator StalkBeat(Transform point, Func<bool> when)
        {
            var s = props.stalker;
            yield return new WaitUntil(when);
            float t = 0f;
            while (GazeUtility.FlatAngleTo(eye, point.position) < 80f && t < 8f) { t += Time.deltaTime; yield return null; }
            s.PlaceAt(point.position, P);
            float life = 0f;
            while (life < 16f)
            {
                life += Time.deltaTime;
                if (s.WasSeen)
                {
                    if (!firstStalkSeen)
                    {
                        firstStalkSeen = true;
                        tension.SetTarget(0.9f);
                        hud.Say("", "Nos está siguiendo.");
                    }
                    float since = 0f;
                    while (s.PlayerLooking(35f) && since < 6f) { since += Time.deltaTime; yield return null; }
                    break;
                }
                if (Vector3.Distance(P, point.position) < 9f) break;
                yield return null;
            }
            s.Hide();
        }

        /// <summary>Pasos sobre el pasto seco, detrás del jugador, mientras camina (se detienen si te detienes).</summary>
        IEnumerator StepsBehind()
        {
            var src = props ? props.stalkerSteps : null;
            if (!src) yield break;
            var clip = ProceduralAudio.Build(ProceduralAudio.Kind.Step, 0.25f, 31);
            Vector3 last = P;
            while (true)
            {
                yield return new WaitForSeconds(0.62f);
                bool moving = (P - last).sqrMagnitude > 0.2f;
                last = P;
                if (!stalkerSteps || !moving) continue;
                Vector3 back = player.transform.forward; back.y = 0;
                src.transform.position = P - back.normalized * 6f + Vector3.up * 0.2f;
                src.pitch = UnityEngine.Random.Range(0.85f, 1.05f);
                src.PlayOneShot(clip, 0.9f);
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  NOCHE
        // ════════════════════════════════════════════════════════════════
        IEnumerator Night()
        {
            Beat = "noche";
            isNight = true;
            tension.SilenceCrickets(false);
            tension.SetTarget(0.2f);
            if (props.night) props.night.Apply();
            Show(props.dayOnly, false);
            Show(props.nightOnly, true);
            if (brother) brother.gameObject.SetActive(false);
            if (creature) creature.gameObject.SetActive(false);
            backDoor.ForceClose();
            backDoor.SetLocked(false);
            if (props.nightSpawn) player.Teleport(props.nightSpawn.position, props.nightSpawn.rotation);
            player.Pitch = 8f;

            yield return hud.ShowCard("Esa noche dormimos en la sala,\nen sacos de dormir.", 3.5f);
            yield return hud.Fade(0f, 2.5f);
            Lock(false, false);
            yield return hud.SayAndWait("", "No podía dormir.");
            yield return new WaitForSeconds(2f);

            // ── Sam escribe desde su saco ──
            Beat = "mensajes";
            yield return SamTexts("¿estás despierto?");
            yield return new WaitForSeconds(0.8f);
            yield return hud.SayAndWait("", $"{brotherName} está a dos metros de mí.");
            yield return Reply("sí");
            yield return new WaitForSeconds(1.5f);
            yield return SamTexts("no le digas a nadie lo de hoy");
            yield return Reply("no");

            // ── Se corta la luz ──
            if (props.flashlight && props.breaker) yield return PowerOut();

            yield return new WaitForSeconds(3f);
            yield return SamTexts("¿oyes eso?");

            // ── Golpes en la ventana ──
            Beat = "ventana";
            tension.SilenceCrickets(true);
            yield return new WaitForSeconds(1.8f);
            if (props.windowKnock) props.windowKnock.Play();
            tension.SetTarget(0.55f);
            yield return new WaitForSeconds(1.2f);
            hud.SetObjective("Revisa la ventana");

            var s = props.stalker;
            if (s && props.windowOutside)
            {
                float t = 0f;
                while (GazeUtility.FlatAngleTo(eye, props.windowOutside.position) < 70f && t < 3f) { t += Time.deltaTime; yield return null; }
                s.PlaceAt(props.windowOutside.position, P);
                float wait = 0f, knockAgain = 9f;
                while (!s.WasSeen && wait < 25f)
                {
                    wait += Time.deltaTime;
                    if (wait > knockAgain) { knockAgain += 9f; if (props.windowKnock) props.windowKnock.Play(); }
                    yield return null;
                }
                if (s.WasSeen)
                {
                    hud.CompleteObjective();
                    tension.SetTarget(0.7f);
                    yield return hud.SayAndWait("", "Había algo afuera. Pegado al vidrio.");
                    float since = 0f;
                    while (s.PlayerLooking(30f) && since < 5f) { since += Time.deltaTime; yield return null; }
                }
                s.Hide();
            }

            // ── La voz en la puerta ──
            Beat = "voz_puerta";
            if (props.doorKnock) props.doorKnock.Play();
            yield return new WaitForSeconds(1f);
            yield return hud.SayAndWait("???", $"¿{playerName}? Soy Tammy… Me quedé afuera. Ábreme, porfa.");
            yield return SamTexts("NO ABRAS");
            yield return SamTexts("tammy está en su cuarto. la escuché roncar hace un rato");

            // ── Tareas de la noche (cualquier orden) ──
            Beat = "asegurar";
            Avail(props.window, true);
            Avail(props.deadbolt, true);
            Avail(props.lightSwitch, true);
            props.window.Used += _ => { if (curtainDone) return; curtainDone = true; Avail(props.window, false); props.curtainClosed.SetActive(true); NightObjective(); };
            props.deadbolt.Used += _ =>
            {
                if (boltDone) return;
                boltDone = true; Avail(props.deadbolt, false);
                backDoor.ForceClose(false);
                backDoor.SetLocked(true);
                props.deadbolt.transform.localRotation *= Quaternion.Euler(0, 0, 90);
                NightObjective();
            };
            props.lightSwitch.Used += _ =>
            {
                if (lightDone) return;
                lightDone = true; Avail(props.lightSwitch, false);
                SetHouseLights(false);
                NightObjective();
            };
            NightObjective();
            Goal(props.window, props.deadbolt, props.lightSwitch);
            var harass = StartCoroutine(Harass());
            yield return new WaitUntil(() => (curtainDone && boltDone && lightDone) || badEnding);
            Goal();
            if (!badEnding) hud.CompleteObjective();
            StopCoroutine(harass);
            if (props.doorRattle) props.doorRattle.Stop();
            if (badEnding) yield break;

            // ── Acostarse ──
            Beat = "acostarse";
            tension.SetTarget(0.5f);
            hud.SetObjective("Acuéstate");
            yield return new WaitForSeconds(1.5f);
            yield return hud.SayAndWait("", "Silencio.");
            Avail(props.sleepingBag, true);
            Goal(props.sleepingBag);
            yield return WaitUse(props.sleepingBag, null);
            Avail(props.sleepingBag, false);
            Goal();
            hud.CompleteObjective();
            hud.SetObjective(null);
            Lock(true, true);
            yield return hud.Fade(0.85f, 2f);
            yield return new WaitForSeconds(2.5f);
            if (props.whisper) props.whisper.Play();
            yield return hud.SayAndWait("???", $"…{Stretch(brotherName)}…", 2.5f);
            yield return SamTexts("ya no está");
            yield return new WaitForSeconds(1.5f);
            yield return SamTexts("¿verdad?");
            yield return new WaitForSeconds(2f);
            yield return hud.Fade(1f, 2f);
        }

        // ════════════════════════════════════════════════════════════════
        //  APAGÓN
        // ════════════════════════════════════════════════════════════════
        IEnumerator PowerOut()
        {
            Beat = "apagon";
            yield return new WaitForSeconds(2.5f);
            if (props.powerClick) props.powerClick.Play();
            SetHouseLights(false);
            tension.SetTarget(0.35f);
            yield return new WaitForSeconds(0.8f);
            yield return hud.SayAndWait("", "Se fue la luz.");
            yield return SamTexts("¿se fue la luz?");
            yield return new WaitForSeconds(1f);
            const string hall = "Tammy (desde el pasillo)";
            yield return hud.SayAndWait(hall, $"¿{playerName}? ¿Se fue la luz?…");
            yield return hud.SayAndWait(hall, "Siempre se bota el breaker. La caja está en el cobertizo, al lado de la casa.");
            yield return hud.SayAndWait(hall, "Hay una linterna en la mesada, junto al fregadero. Porfa, que si no se echa a perder todo lo del refri.");
            yield return hud.SayAndWait(hall, "Y la llave del candado de la caja está ahí mismo, al lado.");

            // Linterna y llave: las dos en la mesada, en cualquier orden.
            CreateShedKeyAndPadlock();
            Avail(props.flashlightPickup, true);
            Avail(ShedKey, true);
            bool gotLight = false, gotKey = false;
            void PowerObjective()
            {
                string Line(bool done, string t) => done ? $"<s><color=#999>{t}</color></s>" : t;
                hud.SetObjective(Line(gotLight, "Toma la linterna (mesada de la cocina)") + "\n" + Line(gotKey, "Toma la llave del candado"));
            }
            PowerObjective();
            Goal(props.flashlightPickup, ShedKey);
            while (!gotLight || !gotKey)
            {
                if (!gotLight && props.flashlightPickup.UseCount > 0)
                {
                    gotLight = true;
                    Avail(props.flashlightPickup, false);
                    props.flashlightPickup.gameObject.SetActive(false);
                    props.flashlight.Give();
                    hud.Say("", "Funciona.  [F] o clic: prender y apagar.  [1–5] o rueda: cambiar de objeto.  [Q]: soltar.", 4f);
                    StartCoroutine(SamTextsMany("no vayas", "es broma", "ve rápido"));
                    PowerObjective();
                }
                if (!gotKey && ShedKey && ShedKey.UseCount > 0)
                {
                    gotKey = true;
                    Avail(ShedKey, false);
                    ShedKey.gameObject.SetActive(false);
                    inventory?.Add(ItemId.ShedKey, equip: !gotLight);
                    PowerObjective();
                }
                if (!ShedKey) gotKey = true;
                yield return null;
            }

            errand = true;
            breakerUp = false;
            const string pLock = "Abre el candado de la caja (cobertizo)", pBreaker = "Sube el breaker: restaura la luz";
            hud.CompleteObjective();
            hud.SetObjective(Padlock ? Task(false, pLock) + "\n" + Task(false, pBreaker) : Task(false, pBreaker));
            Goal(Padlock, props.breaker);           // el breaker brilla recién cuando el candado está abierto
            var scares = StartCoroutine(OutsideScares());
            // La caja tiene candado: primero la llave, después el breaker.
            if (Padlock)
            {
                // Sin la llave en el inventario no se puede abrir.
                yield return WaitUse(Padlock, () => !inventory || inventory.Has(ItemId.ShedKey));
                Padlock.Available = false;
                OneShotAudio.Play(ProceduralAudio.Build(ProceduralAudio.Kind.Click, 0.2f, 31), Padlock.transform.position, 0.9f);
                StartCoroutine(DropPadlock(Padlock.transform));
                hud.SetObjective(Task(true, pLock) + "\n" + Task(false, pBreaker));
                hud.Say("", "Abierto.", 1.5f);
            }
            Avail(props.breaker, true);
            yield return WaitUse(props.breaker, null);
            hud.CompleteObjective();                       // tachado en el mismo instante en que sube la palanca
            Goal();
            breakerUp = true;
            Avail(props.breaker, false);
            StopCoroutine(scares);
            if (props.stalker) props.stalker.Hide();
            if (props.breakerLever) props.breakerLever.localRotation *= Quaternion.Euler(0f, 0f, 150f);
            if (props.powerClick && props.powerClick.clip) OneShotAudio.Play(props.powerClick.clip, props.breaker.transform.position, 0.8f);
            SetHouseLights(true);
            tension.SetTarget(0.45f);
            yield return hud.SayAndWait("", "Listo. Volvió la luz.");

            // Golpes en la pared del cobertizo, desde afuera.
            if (props.shedKnock) props.shedKnock.Play();
            tension.SetTarget(0.85f);
            yield return new WaitForSeconds(1.1f);
            if (props.shedKnock) props.shedKnock.Play();
            StartCoroutine(SamTextsMany("volvió la luz", "VUELVE YA"));
            hud.Say("", "Eso vino de afuera. De la pared de atrás.", 3f);
            hud.SetObjective("Vuelve a la casa");

            // Al salir: algo entre el cobertizo y la casa.
            float t = 0f;
            while (props.shedInside && Vector3.Distance(P, props.shedInside.position) < 2.2f && t < 30f) { t += Time.deltaTime; yield return null; }
            yield return CornerScare();

            yield return WaitTrigger(houseReturnTrigger);
            hud.CompleteObjective();
            backDoor.ForceClose(false);
            errand = false;
            hud.SetObjective(null);
            tension.SetTarget(0.45f);
            yield return hud.SayAndWait("", "Cierro la puerta. No miro atrás.");
        }

        IEnumerator SamTextsMany(params string[] texts)
        {
            foreach (var t in texts) { yield return SamTexts(t); yield return new WaitForSeconds(0.6f); }
        }

        /// <summary>Camino al cobertizo: un susurro y algo parado en el campo, en el borde de la luz.</summary>
        IEnumerator OutsideScares()
        {
            var s = props.stalker;
            while (P.z < 63.4f) yield return null;                  // ya afuera
            yield return new WaitForSeconds(3f);
            if (props.fieldWhisper) props.fieldWhisper.Play();
            hud.Say("???", $"…{Stretch(playerName)}…", 2.5f);
            tension.SetTarget(0.5f);
            if (!s || !props.fieldWatch) yield break;
            float t = 0f;
            while (GazeUtility.FlatAngleTo(eye, props.fieldWatch.position) < 60f && t < 6f) { t += Time.deltaTime; yield return null; }
            s.PlaceAt(props.fieldWatch.position, P);
            t = 0f;
            while (!s.WasSeen && t < 25f) { t += Time.deltaTime; yield return null; }
            if (s.WasSeen)
            {
                tension.SetTarget(0.65f);
                yield return hud.SayAndWait("", "Hay algo parado en el campo. Mirándome.");
                yield return new WaitForSeconds(0.8f);
                yield return props.flashlight.Flicker(0.7f, () => s.Hide());
                yield return hud.SayAndWait("", "…Ya no está. Sigue caminando.");
            }
            else s.Hide();
        }

        /// <summary>Saliendo del cobertizo: la linterna parpadea y está ahí, entre tú y la casa.</summary>
        IEnumerator CornerScare()
        {
            var s = props.stalker;
            if (!s || !props.houseCorner) yield break;
            yield return props.flashlight.Flicker(0.6f, () => s.PlaceAt(props.houseCorner.position, P));
            tension.SetTarget(1f);
            yield return new WaitForSeconds(1.1f);
            yield return props.flashlight.Flicker(0.5f, () => s.Hide());
            if (props.stalkerSteps)
            {
                var clip = ProceduralAudio.Build(ProceduralAudio.Kind.Step, 0.25f, 77);
                for (int i = 0; i < 6; i++)
                {
                    props.stalkerSteps.transform.position = P + Quaternion.Euler(0, 40f * i, 0) * Vector3.forward * (4f - i * 0.4f);
                    props.stalkerSteps.PlayOneShot(clip, 1f);
                    yield return new WaitForSeconds(0.18f);
                }
            }
            tension.SetTarget(0.8f);
            hud.Say("", "Corre.", 2f);
        }

        void SetHouseLights(bool on)
        {
            if (props.houseLights != null) foreach (var l in props.houseLights) if (l) l.enabled = on;
            if (props.lampBulbs == null) return;
            foreach (var r in props.lampBulbs)
            {
                if (!r) continue;
                foreach (var m in r.materials)
                {
                    if (!m.name.StartsWith("M_Foco")) continue;
                    if (!bulbEmission.ContainsKey(m)) bulbEmission[m] = m.GetColor("_EmissionColor");
                    if (on) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", bulbEmission[m]); }
                    else { m.DisableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black); }
                }
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  LA MAÑANA SIGUIENTE
        // ════════════════════════════════════════════════════════════════
        IEnumerator MorningAfter()
        {
            Beat = "manana";
            yield return hud.ShowCard("No sé cuándo me quedé dormido.", 3f);
            isNight = false;
            if (props.night) props.night.Morning();
            SetHouseLights(false);
            if (props.flashlight) props.flashlight.Take();
            if (props.samBagFull) props.samBagFull.SetActive(false);
            foreach (var o in props.nightOnly) if (o && o.name.StartsWith("Casa_Celular")) o.SetActive(false);
            if (props.samPhoneScreen) props.samPhoneScreen.gameObject.SetActive(false);
            if (props.samPhoneLight) props.samPhoneLight.enabled = false;
            Show(props.morningOnly, true);
            if (props.curtainClosed) props.curtainClosed.SetActive(false);       // Sam abrió la cortina
            if (boltDone && props.deadbolt) props.deadbolt.transform.localRotation *= Quaternion.Euler(0, 0, -90);
            backDoor.SetLocked(false);
            backDoor.ForceOpen();                                                 // Sam la dejó abierta
            tension.SilenceCrickets(true);
            tension.SetTarget(0.2f);
            if (props.nightSpawn) player.Teleport(props.nightSpawn.position, props.nightSpawn.rotation);
            player.Pitch = 5f;
            if (brother && props.samMorning) brother.PlaceStanding(props.samMorning.position, props.trail[0].position);
            yield return hud.Fade(0f, 3f);
            Lock(false, false);
            yield return hud.SayAndWait("", $"El saco de {brotherName} está vacío.");
            yield return new WaitForSeconds(1.2f);
            if (phone)
            {
                phone.Receive(brotherName, "estoy afuera");
                yield return new WaitForSeconds(1.6f);
                phone.Receive(brotherName, "ven. junto a la ventana de la sala");
            }
            hud.SetObjective($"Sal a buscar a {brotherName}");
            while (Vector3.Distance(P, brother.transform.position) > 3.2f) yield return null;
            hud.CompleteObjective();

            Beat = "huellas";
            hud.SetObjective(null);
            brother.Face(P);
            yield return hud.SayAndWait(brotherName, "Mira.");
            brother.Face(props.trail[0].position);
            yield return hud.SayAndWait("", "Huellas. Debajo de la ventana. Justo donde estaba anoche.");
            yield return hud.SayAndWait(brotherName, "Son de venado. Pero mira cómo están: una delante de la otra.");
            yield return hud.SayAndWait(brotherName, "Como si caminara en dos patas.");
            yield return hud.SayAndWait(brotherName, "Dan la vuelta a la casa. Yo no las voy a seguir.");

            Beat = "rastro";
            hud.SetObjective("Sigue las huellas");
            tension.SetTarget(0.35f);
            // Cada punto se marca al pasar (aunque las frases de antes sigan sonando): nadie se queda trabado.
            var reached = new bool[props.trail.Length];
            var watch = StartCoroutine(WatchTrail(reached));
            Avail(props.collar, true);
            CreateFootprintSpot();
            yield return Checkpoint(reached, 4, "Se quedó parado aquí. Justo afuera de la puerta.", "Cuando la voz decía que era Tammy.");
            yield return Checkpoint(reached, 7, "Siguen hasta el cobertizo.", "Anoche yo estuve ahí adentro.");
            tension.SetTarget(0.5f);
            yield return Checkpoint(reached, 10, "Aquí cambian.", "Ya no son de venado. Son de pie. De pie descalzo.");
            yield return Checkpoint(reached, 11, "Terminan aquí. En medio del campo.");
            StopCoroutine(watch);
            hud.CompleteObjective();
            if (props.collar.UseCount == 0) hud.SetObjective("Hay algo en el pasto");
            Goal(props.collar);
            while (props.collar.UseCount == 0) yield return null;
            Goal();
            hud.CompleteObjective();
            Avail(props.collar, false);
            props.collar.gameObject.SetActive(false);
            hud.SetObjective(null);
            tension.SetTarget(0.6f);
            var collarRenderer = props.collar.GetComponent<Renderer>();
            InspectionView.Open(InspectModels.Collar(collarRenderer), "Collar", new Vector3(-58f, 0f, 0f), 0.24f);
            yield return hud.SayAndWait("", "Un collar de perro. La placa dice \"BUSTER\".");
            yield return hud.SayAndWait("", "El perro de los vecinos. El que se calló de golpe ayer.");
            yield return InspectionView.WaitClosed();
            inventory?.Add(ItemId.BusterCollar, equip: false);
            yield return new WaitForSeconds(2f);

            Beat = "papa";
            if (props.horn) props.horn.Play();
            tension.SetTarget(0.25f);
            yield return new WaitForSeconds(1.6f);
            if (brother && props.samAtTruck) brother.PlaceStanding(props.samAtTruck.position, P);
            yield return hud.SayAndWait(brotherName, $"¡{playerName}! ¡Llegó papá!");
            yield return hud.SayAndWait("", "Papá. Llegó un día antes.");
            hud.SetObjective("Ve a la camioneta de papá");
            Avail(props.dadTruck, true);
            Goal(props.dadTruck);
            yield return WaitUse(props.dadTruck, null);
            Avail(props.dadTruck, false);
            Goal();
            hud.CompleteObjective();
            hud.SetObjective(null);
            Lock(true, true);
            yield return hud.Fade(1f, 1.5f);
            yield return hud.SayAndWait("Papá", "¡Hola, chicos! ¿Cómo lo pasaron? ¿Se divirtieron?");
            yield return hud.SayAndWait(brotherName, "…Sí.");
            yield return hud.SayAndWait("", "Ninguno de los dos dijo nada más en todo el camino.");
            yield return new WaitForSeconds(1f);
        }

        IEnumerator WatchTrail(bool[] reached)
        {
            while (true)
            {
                for (int i = 0; i < reached.Length; i++)
                {
                    var p = props.trail[i].position;
                    if (!reached[i] && new Vector2(P.x - p.x, P.z - p.z).magnitude < 2.6f)
                        for (int j = 0; j <= i; j++) reached[j] = true;      // si llegó más lejos, los anteriores también
                }
                yield return null;
            }
        }

        IEnumerator Checkpoint(bool[] reached, int index, params string[] lines)
        {
            while (!reached[index]) yield return null;
            foreach (var l in lines) yield return hud.SayAndWait("", l);
        }

        void NightObjective()
        {
            string Line(bool done, string s) => done ? $"<s><color=#999>{s}</color></s>" : s;
            hud.SetObjective(Line(curtainDone, "Cierra la cortina") + "\n" + Line(boltDone, "Pon el cerrojo de la puerta") + "\n" + Line(lightDone, "Apaga la luz"));
        }

        /// <summary>Mientras aseguras la casa: golpes, la manija, voces que imitan.</summary>
        IEnumerator Harass()
        {
            int i = 0;
            while (true)
            {
                yield return new WaitForSeconds(7f);
                switch (i++ % 4)
                {
                    case 0:
                        if (!boltDone && props.doorRattle) props.doorRattle.Play();
                        else if (props.doorKnock) props.doorKnock.Play();
                        break;
                    case 1:
                        yield return hud.SayAndWait("???", $"{playerName}. Ábreme. Soy yo, {brotherName}.");
                        yield return SamTexts("no soy yo");
                        yield return SamTexts($"{playerName.ToLower()} no soy yo");
                        if (i < 3) yield return hud.SayAndWait("", $"{brotherName} está acá. En su saco.");
                        break;
                    case 2:
                        if (props.windowKnock) props.windowKnock.Play();
                        break;
                    case 3:
                        if (props.doorKnock) props.doorKnock.Play();
                        yield return new WaitForSeconds(0.8f);
                        hud.Say("???", "Hace frío aquí afuera…");
                        break;
                }
            }
        }

        IEnumerator BadEnding()
        {
            badEnding = true;
            Beat = "final_malo";
            Lock(true, true);
            hud.SetObjective(null);
            var s = props.stalker;
            if (s && props.doorOutside)
            {
                s.PlaceAt(props.doorOutside.position, P);
                player.FaceTowards(props.doorOutside.position);
                player.Pitch = -8f;
            }
            tension.SetTarget(1f);
            yield return new WaitForSeconds(0.9f);
            // De noche Sam está oculto (su AudioSource desactivado): el grito suena desde un objeto temporal.
            if (brotherScream && brotherScream.clip) OneShotAudio.Play(brotherScream.clip, P + Vector3.up, brotherScream.volume);
            hud.SetFadeInstant(1f);
            if (s) s.Hide();
            yield return new WaitForSeconds(1.5f);
            yield return hud.ShowCard("No era Tammy.", 3f);
            finished = true;
            FirstPersonController.LockCursor(false);
            Beat = "fin_malo";
            yield return hud.ShowCardStay("SOMEONE'S HOME\n<size=26>Nunca abras la puerta.</size>\n\n<size=20>[R] Volver a jugar</size>");
        }

        // ════════════════════════════════════════════════════════════════
        //  AYUDANTES
        // ════════════════════════════════════════════════════════════════
        IEnumerator SamTexts(string text)
        {
            SetSamPhone(true);
            if (phone) phone.Receive(brotherName, text);
            yield return new WaitForSeconds(Mathf.Clamp(1.2f + text.Length * 0.05f, 1.6f, 3.5f));
            SetSamPhone(false);
        }

        /// <summary>Respondes cuando abres el celular (o solo, a los pocos segundos).</summary>
        IEnumerator Reply(string text)
        {
            if (!phone) yield break;
            float t = 0f;
            while (!phone.IsOpen && t < 5f) { t += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(0.6f);
            phone.Send(brotherName, text);
        }

        void SetSamPhone(bool on)
        {
            if (!props) return;
            if (props.samPhoneLight) props.samPhoneLight.enabled = on;
            if (props.samPhoneScreen)
            {
                var m = props.samPhoneScreen.material;
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", on ? new Color(0.5f, 0.65f, 1f) * 2.2f : Color.black);
            }
        }

        static void Avail(SimpleInteractable i, bool on) { if (i) i.Available = on; }

        // ════════════════════════════════════════════════════════════════
        //  INVENTARIO Y OBJETOS NUEVOS
        // ════════════════════════════════════════════════════════════════
        void SetupInventory()
        {
            inventory = player.GetComponent<InventorySystem>();
            if (!props) return;
            // Ítems agarrables: [E] "Agarrar …" los manda al inventario (primer espacio libre, en la mano).
            if (props.backpack) props.backpack.Item = ItemId.Backpack;
            if (props.platesStack) props.platesStack.Item = ItemId.Plates;
            if (props.trashBag) props.trashBag.Item = ItemId.TrashBag;
            if (props.flashlightPickup) props.flashlightPickup.Item = ItemId.Flashlight;
            if (props.collar) props.collar.Item = ItemId.BusterCollar;
            if (!inventory) return;
            // Los modelos en mano son copias de los objetos de la escena.
            var flashR = props.flashlightPickup ? props.flashlightPickup.GetComponent<Renderer>() : null;
            var bagR = props.trashBag ? props.trashBag.GetComponent<Renderer>() : null;
            var collarR = props.collar ? props.collar.GetComponent<Renderer>() : null;
            if (props.flashlight && flashR) props.flashlight.FlashlightModel = () => InspectModels.CloneVisual(flashR, "Linterna");
            if (bagR) inventory.RegisterModel(ItemId.TrashBag, () => InspectModels.CloneVisual(bagR, "Bolsa de basura"));
            if (collarR) inventory.RegisterModel(ItemId.BusterCollar, () => InspectModels.Collar(collarR));
        }

        /// <summary>Llave junto a la linterna (sobre la mesada) y candado en la caja del breaker.</summary>
        void CreateShedKeyAndPadlock()
        {
            if (ShedKey || !props || !props.flashlightPickup || !props.breaker) return;
            var cc = player.GetComponent<CharacterController>();

            // Llave: a un palmo de la linterna, apoyada en lo que haya debajo (la mesada).
            Vector3 p = props.flashlightPickup.transform.position + new Vector3(0f, 0f, 0.32f);
            if (Physics.Raycast(p + Vector3.up * 0.3f, Vector3.down, out var hit, 0.8f, ~0, QueryTriggerInteraction.Ignore)
                && !hit.collider.GetComponentInParent<SimpleInteractable>())
                p.y = hit.point.y + 0.004f;
            var key = InspectModels.ShedKey();
            key.transform.SetPositionAndRotation(p, Quaternion.Euler(90f, 35f, 0f));    // acostada sobre la mesada
            var keyCol = key.AddComponent<BoxCollider>();
            keyCol.size = new Vector3(0.16f, 0.06f, 0.12f);
            keyCol.center = new Vector3(-0.015f, 0f, 0f);
            if (cc) Physics.IgnoreCollision(cc, keyCol);
            ShedKey = key.AddComponent<SimpleInteractable>();
            ShedKey.Prompt = "Recoger llave del candado";
            ShedKey.Item = ItemId.ShedKey;
            ShedKey.Available = false;

            // Candado: colgado del frente de la caja, sobre la palanca. Tapa el breaker hasta abrirlo.
            var box = props.breaker.GetComponent<Collider>() ? props.breaker.GetComponent<Collider>().bounds
                                                             : new Bounds(props.breaker.transform.position, Vector3.one * 0.3f);
            Vector3 toPlayer = (props.shedInside ? props.shedInside.position : P) - box.center;
            toPlayer.y = 0f;
            Vector3 n = Mathf.Abs(toPlayer.x) > Mathf.Abs(toPlayer.z) ? new Vector3(Mathf.Sign(toPlayer.x), 0f, 0f)
                                                                      : new Vector3(0f, 0f, Mathf.Sign(toPlayer.z));
            var lockGo = InspectModels.Padlock();
            lockGo.transform.position = box.center + n * (Vector3.Scale(box.extents, n).magnitude + 0.035f) + Vector3.down * 0.06f;
            lockGo.transform.rotation = Quaternion.LookRotation(-n);
            lockGo.transform.localScale = Vector3.one * 1.3f;
            var lockCol = lockGo.AddComponent<BoxCollider>();
            lockCol.size = new Vector3(0.12f, 0.14f, 0.08f);
            if (cc) Physics.IgnoreCollision(cc, lockCol);
            Padlock = lockGo.AddComponent<SimpleInteractable>();
            Padlock.Prompt = "Abrir el candado con la llave";
            Padlock.Available = false;
        }

        IEnumerator DropPadlock(Transform padlock)
        {
            var col = padlock.GetComponent<Collider>();
            if (col) col.enabled = false;
            Vector3 v = Vector3.down * 0.5f;
            Vector3 spin = new Vector3(UnityEngine.Random.Range(-200f, 200f), 0f, UnityEngine.Random.Range(-200f, 200f));
            float floor = padlock.position.y - 1.2f;
            if (Physics.Raycast(padlock.position, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore)) floor = hit.point.y + 0.02f;
            while (padlock.position.y > floor)
            {
                v += Physics.gravity * Time.deltaTime;
                padlock.position += v * Time.deltaTime;
                padlock.Rotate(spin * Time.deltaTime, Space.World);
                yield return null;
            }
            padlock.position = new Vector3(padlock.position.x, floor, padlock.position.z);
            OneShotAudio.Play(ProceduralAudio.Build(ProceduralAudio.Kind.Knock, 0.15f, 5), padlock.position, 0.5f);
        }

        /// <summary>Donde las huellas cambian de pezuña a pie: se pueden examinar (opcional, no frena la historia).</summary>
        void CreateFootprintSpot()
        {
            if (FootprintSpot || props.trail == null || props.trail.Length <= 10 || !props.trail[10]) return;
            Transform prints = null;
            if (props.morningOnly != null)
                foreach (var g in props.morningOnly) if (g && g.name.StartsWith("Huellas")) prints = g.transform;
            if (!prints) return;
            Vector3 c = props.trail[10].position;
            var go = new GameObject("Examinar: huellas");
            go.transform.position = c;
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(1.1f, 0.06f, 1.6f);
            var cc = player.GetComponent<CharacterController>();
            if (cc) Physics.IgnoreCollision(cc, col);
            FootprintSpot = go.AddComponent<SimpleInteractable>();
            FootprintSpot.Prompt = "Examinar las huellas";
            FootprintSpot.Used += _ => StartCoroutine(InspectFootprints(prints, c));
        }

        IEnumerator InspectFootprints(Transform prints, Vector3 c)
        {
            FootprintSpot.Available = false;
            InspectionView.Open(InspectModels.Footprints(prints, c, 0.8f), "Huellas", default, 0.3f);
            yield return hud.SayAndWait("", "Pezuña. Pezuña. Y de golpe… un pie. Descalzo. Dedos largos.");
            yield return hud.SayAndWait("", "Como si en este punto hubiera dejado de fingir.");
            yield return InspectionView.WaitClosed();
            FootprintSpot.Available = true;
        }

        static void Show(GameObject[] objs, bool on)
        {
            if (objs == null) return;
            foreach (var o in objs) if (o) o.SetActive(on);
        }

        static IEnumerator WaitUse(SimpleInteractable i, Func<bool> condition)
        {
            if (!i) yield break;
            int start = i.UseCount;
            while (true)
            {
                if (condition != null) i.Available = condition();
                if (i.UseCount > start) yield break;
                yield return null;
            }
        }

        void Update()
        {
            if (swingSeat)
            {
                float target = swingMoving ? Mathf.Sin(Time.time * 1.3f) * 9f : 0f;
                var e = swingSeat.localEulerAngles;
                float cur = Mathf.DeltaAngle(0, e.x);
                swingSeat.localEulerAngles = new Vector3(Mathf.Lerp(cur, target, Time.deltaTime * 3f), e.y, e.z);
            }

            if (finished && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        IEnumerator MimicOnClimb()
        {
            yield return WaitTrigger(climbTrigger);
            if (mimicFromRiver) mimicFromRiver.Play();
            hud.Say("Emily", $"¿{playerName}? ¡{playerName}, ven acá abajo!", 3f);
            tension.SetTarget(0.85f);
            yield return new WaitForSeconds(3.2f);
            hud.Say("", "Emily se quedó en la casa.");
        }

        /// <summary>La cosa se va cuando dejas de mirarla, nunca delante de ti.</summary>
        IEnumerator VanishWhenNotObserved()
        {
            yield return new WaitForSeconds(2f);
            while (GazeUtility.IsLookingAt(eye, creature.LookTarget.position, 45f, false)) yield return null;
            creature.Vanish();
        }

        bool NearBrother(float radius)
        {
            Vector3 d = P - brother.transform.position;
            return Mathf.Abs(d.y) < 4f && new Vector2(d.x, d.z).magnitude < radius;
        }

        /// <summary>Espera a que el jugador entre al trigger, o a que se cumpla 'orElse' (respaldo si lo esquiva).</summary>
        IEnumerator WaitTrigger(NarrativeTrigger trigger, Func<bool> orElse = null)
        {
            if (!trigger)
            {
                if (orElse != null) yield return new WaitUntil(orElse);
                yield break;
            }
            bool fired = false;
            void OnEntered(NarrativeTrigger _) => fired = true;
            trigger.Entered += OnEntered;
            // Al activarlo con el jugador adentro, Unity dispara OnTriggerEnter igual.
            trigger.gameObject.SetActive(true);
            yield return new WaitForFixedUpdate();
            // Los triggers son de un solo disparo (Entered no vuelve a sonar): el de la casa se usa dos veces
            // (regreso del río y apagón), así que también vale que el jugador esté adentro en cualquier momento.
            while (!fired && !trigger.PlayerInside && (orElse == null || !orElse())) yield return null;
            trigger.Entered -= OnEntered;
        }

        void Lock(bool move, bool look)
        {
            player.InputLocked = move;
            player.LookLocked = look;
        }

        static string Stretch(string name)
        {
            // "Alex" → "Aleeex": la voz arrastra la vocal.
            int i = name.LastIndexOfAny("aeiouAEIOU".ToCharArray());
            return i < 0 ? name : name.Substring(0, i + 1) + new string(name[i], 2) + name.Substring(i + 1);
        }
    }
}
