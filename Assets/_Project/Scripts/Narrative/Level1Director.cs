using System;
using System.Collections;
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
    ///      Se corta la luz: linterna, cobertizo, breaker. Si abres la puerta (sin motivo): final malo.
    ///   6. La mañana siguiente: Sam afuera, las huellas alrededor de la casa, el collar, llega papá.
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

        // Regreso / noche
        bool stalkerSteps;
        bool isNight, badEnding;
        bool curtainDone, boltDone, lightDone;
        bool errand;                                  // de noche, yendo al cobertizo: abrir la puerta no es el final malo
        bool breakerUp;
        GameObject tammyGO, emilyGO;
        Pose tammyHome, emilyHome;
        readonly System.Collections.Generic.Dictionary<Material, Color> bulbEmission = new();

        /// <summary>Paso actual de la historia (lo lee el bot de pruebas).</summary>
        public string Beat { get; private set; } = "intro";

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
            SetupDay();

            // ── 0 · INTRO ──────────────────────────────────────────────
            Lock(true, true);
            hud.SetFadeInstant(1f);
            bool arrival = props && props.arrivalSpawn;
            if (arrival) PlaceArrival();
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
                hud.SetObjective(null);
                yield return phone.Call(brotherName, 2, $"Hola, soy {brotherName}. Deja tu mensaje… o no.");
                yield return hud.SayAndWait("", "No contesta. Siempre contesta.");
            }
            yield return hud.SayAndWait("Tammy", "¿No contesta? Ve a buscarlo al río, porfa.");
            hud.SetObjective($"Busca a {brotherName} junto al río");

            // ── 4 · EL CAMPO ──────────────────────────────────────────
            Beat = "campo";
            yield return WaitTrigger(fieldTrigger);
            tension.SetTarget(0.08f);
            hud.Say("", "El patio trasero en realidad es un campo.");

            // ── 5 · LA COLINA ─────────────────────────────────────────
            yield return WaitTrigger(hillTopTrigger);
            Beat = "colina";
            hud.SetObjective("Baja por las rocas");
            hud.Say("", "Una colina estúpidamente empinada. Hay que ir pisando las rocas… o sales volando.");

            // ── 6 · EL COLUMPIO ───────────────────────────────────────
            yield return WaitTrigger(swingTrigger);
            Beat = "columpio";
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
                if (waited > 7f) { hud.Say("", "¿Emily? Venía de arriba, del campo."); waited = -999f; }
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

            while (GazeUtility.FlatAngleTo(eye, brother.transform.position) > 40f) yield return null;

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

            while (!GazeUtility.IsLookingAt(eye, creature.LookTarget.position, 20f, false)) yield return null;
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
            if (brotherScream) brotherScream.Play();
            yield return hud.SayAndWait(brotherName, "¡AAAH!", 1.2f);
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
            StopCoroutine(mimic);
            if (stalk != null) StopCoroutine(stalk);
            StopCoroutine(steps);
            stalkerSteps = false;
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
            Lock(false, false);
            StartCoroutine(GoInsideWhenUnseen());

            // ── La mochila ──
            Beat = "mochila";
            backDoor.SetLocked(false);
            hud.SetObjective("Saca tu mochila de la camioneta");
            Avail(props.backpack, true);
            yield return WaitUse(props.backpack, null);
            Avail(props.backpack, false);
            props.backpack.gameObject.SetActive(false);
            hud.SetObjective("Lleva tu mochila adentro");
            bool triedFront = false;
            Avail(props.frontDoor, true);
            void Front(SimpleInteractable _)
            {
                hud.Say(triedFront ? "Tammy" : "", triedFront ? "¡Esa no abre! ¡Entra por la de atrás!" : "Está trabada. No se mueve.");
                triedFront = true;
            }
            if (props.frontDoor) props.frontDoor.Used += Front;
            props.backpackDrop.gameObject.SetActive(true);
            Avail(props.backpackDrop, true);
            yield return WaitUse(props.backpackDrop, null);
            Avail(props.backpackDrop, false);
            props.backpackDrop.gameObject.SetActive(false);
            if (props.frontDoor) { props.frontDoor.Used -= Front; Avail(props.frontDoor, false); }
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
                    yield return InspectLines(i);
                }
                idle = any ? 0f : idle + Time.deltaTime;
                if (idle > 40f) { idle = -999f; hud.Say("", "La foto de la pared, el refri, el reloj, la ventana…"); }
                yield return null;
            }
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
            float t = 0f;
            while (t < 10f)
            {
                bool seenT = tammyGO && GazeUtility.FlatAngleTo(eye, tammyGO.transform.position) < 70f;
                bool seenE = emilyGO && GazeUtility.FlatAngleTo(eye, emilyGO.transform.position) < 70f;
                if (!seenT && !seenE) break;
                t += Time.deltaTime;
                yield return null;
            }
            if (tammyGO) tammyGO.transform.SetPositionAndRotation(tammyHome.position, tammyHome.rotation);
            if (emilyGO) emilyGO.transform.SetPositionAndRotation(emilyHome.position, emilyHome.rotation);
        }

        void UpdateChoresObjective()
        {
            string Line(bool done, string s) => done ? $"<s><color=#999>{s}</color></s>" : s;
            string table = carrying == Carry.Plates ? "Pon los platos en la mesa" : "Pon la mesa (platos en la mesada)";
            string trash = carrying == Carry.Bag ? "Tira la bolsa al tacho de afuera" : "Saca la basura";
            hud.SetObjective(Line(tableSet, table) + "\n" + Line(trashOut, trash));
        }

        IEnumerator TableChore()
        {
            Avail(props.platesStack, true);
            yield return WaitUse(props.platesStack, () => carrying == Carry.None);
            carrying = Carry.Plates;
            props.platesStack.gameObject.SetActive(false);
            UpdateChoresObjective();
            Avail(props.table, true);
            yield return WaitUse(props.table, null);
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
            yield return WaitUse(props.trashBag, () => carrying == Carry.None);
            carrying = Carry.Bag;
            props.trashBag.gameObject.SetActive(false);
            UpdateChoresObjective();
            Avail(props.trashBin, true);
            yield return WaitUse(props.trashBin, null);
            Avail(props.trashBin, false);
            carrying = Carry.None;
            Avail(props.trashBag, false);                  // la bolsa queda adentro del tacho tapado
            UpdateChoresObjective();
            yield return Glimpse();
            trashOut = true;
            UpdateChoresObjective();
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
            yield return StalkBeat(pts[2], () => P.z < 92f);                      // en el campo → en el borde de la colina
            yield return StalkBeat(pts[3], () => P.z < 74f);                      // casi en la casa → en el campo, cerca
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
            var harass = StartCoroutine(Harass());
            yield return new WaitUntil(() => (curtainDone && boltDone && lightDone) || badEnding);
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
            yield return WaitUse(props.sleepingBag, null);
            Avail(props.sleepingBag, false);
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

            hud.SetObjective("Toma la linterna (mesada de la cocina)");
            Avail(props.flashlightPickup, true);
            yield return WaitUse(props.flashlightPickup, null);
            Avail(props.flashlightPickup, false);
            props.flashlightPickup.gameObject.SetActive(false);
            props.flashlight.Give();
            hud.Say("", "Funciona.  [F] prende y apaga la linterna.", 3f);
            StartCoroutine(SamTextsMany("no vayas", "es broma", "ve rápido"));

            errand = true;
            breakerUp = false;
            hud.SetObjective("Ve al cobertizo y sube el breaker");
            Avail(props.breaker, true);
            var scares = StartCoroutine(OutsideScares());
            yield return WaitUse(props.breaker, null);
            breakerUp = true;
            Avail(props.breaker, false);
            StopCoroutine(scares);
            if (props.stalker) props.stalker.Hide();
            if (props.breakerLever) props.breakerLever.localRotation *= Quaternion.Euler(0f, 0f, 150f);
            if (props.powerClick && props.powerClick.clip) AudioSource.PlayClipAtPoint(props.powerClick.clip, props.breaker.transform.position, 0.8f);
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
            yield return Checkpoint(reached, 4, "Se quedó parado aquí. Justo afuera de la puerta.", "Cuando la voz decía que era Tammy.");
            yield return Checkpoint(reached, 7, "Siguen hasta el cobertizo.", "Anoche yo estuve ahí adentro.");
            tension.SetTarget(0.5f);
            yield return Checkpoint(reached, 10, "Aquí cambian.", "Ya no son de venado. Son de pie. De pie descalzo.");
            yield return Checkpoint(reached, 11, "Terminan aquí. En medio del campo.");
            StopCoroutine(watch);
            if (props.collar.UseCount == 0) hud.SetObjective("Hay algo en el pasto");
            while (props.collar.UseCount == 0) yield return null;
            Avail(props.collar, false);
            props.collar.gameObject.SetActive(false);
            hud.SetObjective(null);
            tension.SetTarget(0.6f);
            yield return hud.SayAndWait("", "Un collar de perro. La placa dice \"BUSTER\".");
            yield return hud.SayAndWait("", "El perro de los vecinos. El que se calló de golpe ayer.");
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
            yield return WaitUse(props.dadTruck, null);
            Avail(props.dadTruck, false);
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
            if (brotherScream && brotherScream.clip) AudioSource.PlayClipAtPoint(brotherScream.clip, P + Vector3.up, brotherScream.volume);
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

        IEnumerator WaitTrigger(NarrativeTrigger trigger)
        {
            if (!trigger) yield break;
            bool fired = false;
            void OnEntered(NarrativeTrigger _) => fired = true;
            trigger.Entered += OnEntered;
            // Al activarlo con el jugador adentro, Unity dispara OnTriggerEnter igual.
            trigger.gameObject.SetActive(true);
            yield return new WaitForFixedUpdate();
            fired |= trigger.PlayerInside;
            while (!fired) yield return null;
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
