using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace SomeonesHome
{
    /// <summary>
    /// Relato 1 · "Across the River".
    /// Basado en el post de u/lacoolestalpaca (r/scarystories, 2020). Ver docs/RELATO_01_EL_RIO.md.
    ///
    /// Secuencia lineal escrita como una sola corrutina: cada bloque es un beat del storyboard.
    /// Para cambiar textos o tiempos, editar aquí; para mover cosas, mover los objetos en la escena.
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

        [Header("Textos")]
        [SerializeField] string playerName = "Alex";
        [SerializeField] string brotherName = "Sam";
        [SerializeField] bool skipIntro;

        HUD hud;
        TensionManager tension;
        Transform eye;
        bool swingMoving = true;
        bool finished;

        IEnumerator Start()
        {
            hud = HUD.Instance;
            tension = TensionManager.Instance;
            eye = player.Head;
            creature.Init(eye);
            backDoor.SetLocked(true);
            // Se activan recién en el regreso: el jugador los cruza también a la ida
            // (puerta de la casa y la curva del sendero) y son de un solo uso.
            if (houseReturnTrigger) houseReturnTrigger.gameObject.SetActive(false);
            if (climbTrigger) climbTrigger.gameObject.SetActive(false);

            // ── 0 · INTRO ──────────────────────────────────────────────
            Lock(true, true);
            hud.SetFadeInstant(1f);
            if (!skipIntro)
            {
                yield return hud.ShowCard("SOMEONE'S HOME", 2.5f);
                yield return hud.ShowCard("Basado en un relato real\n<size=22>publicado en r/scarystories (2020)</size>", 3f);
                yield return hud.ShowCard("Wenatchee, Washington.\n\nNuestros padres estaban fuera del estado,\nasí que nos quedamos con la familia de mi amiga.", 4.5f);
            }
            tension.SetTarget(0f);
            yield return hud.Fade(0f, 2f);
            Lock(true, false); // puede mirar, no caminar, mientras le hablan

            // ── 1 · EL ENCARGO (cocina cálida) ────────────────────────
            yield return new WaitForSeconds(0.8f);
            yield return hud.SayAndWait("Tammy", $"¿Puedes ir por tu hermano? La cena está lista.");
            yield return hud.SayAndWait(playerName, "¿Dónde está?");
            yield return hud.SayAndWait("Emily", "Creo que está junto al río.");
            Lock(false, false);
            backDoor.SetLocked(false);
            hud.SetObjective($"Busca a {brotherName} junto al río");

            // ── 2 · EL CAMPO ──────────────────────────────────────────
            yield return WaitTrigger(fieldTrigger);
            tension.SetTarget(0.05f);
            hud.Say("", "El patio trasero en realidad es un campo.");

            // ── 3 · LA COLINA ─────────────────────────────────────────
            yield return WaitTrigger(hillTopTrigger);
            hud.SetObjective("Baja por las rocas");
            hud.Say("", "Una colina estúpidamente empinada. Hay que ir pisando las rocas… o sales volando.");

            // ── 4 · EL COLUMPIO ───────────────────────────────────────
            yield return WaitTrigger(swingTrigger);
            hud.SetObjective(null);
            tension.SetTarget(0.15f);
            yield return hud.SayAndWait("", $"Ahí está {brotherName}, en el columpio. Podría empujarlo un poco y asustarlo…");
            yield return new WaitForSeconds(1.5f);

            // ── 5 · LA VOZ A MIS ESPALDAS ─────────────────────────────
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

            // ── 6 · NO HAY NADIE (mientras tanto, detrás…) ────────────
            yield return hud.SayAndWait("", "No hay nadie en el campo.");
            // El cambio ocurre cuando el jugador NO mira: Sam de pie, la cosa aparece, silencio.
            swingMoving = false;
            if (swingCreak) swingCreak.Stop();
            tension.SilenceCrickets(true);
            brother.StandAndStare(creature.transform.position);
            creature.Appear();
            tension.SetTarget(0.4f);
            hud.Say("", "Siempre he tenido mucha imaginación. Seguro fue eso.");

            while (GazeUtility.FlatAngleTo(eye, brother.transform.position) > 40f) yield return null;

            // ── 7 · LA REVELACIÓN (Cinemachine) ───────────────────────
            player.FaceTowards(creature.transform.position);
            tension.SetTarget(0.6f);
            yield return revealShot.Play(player, 3.5f);
            yield return hud.SayAndWait("", $"{brotherName} está de pie, mirando al otro lado del río.");
            yield return hud.SayAndWait("", "Algo parecido a un venado. Parado en sus patas traseras.");
            hud.Say("", "Otra vez mi imaginación.");

            // ── 8 · APARTAR LA MIRADA… Y VOLVER A MIRAR ──────────────
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
            tension.SetTarget(0.95f);
            hud.Say("???", $"…{Stretch(playerName)}…", 3f);
            yield return hud.SayAndWait("", "Sigue ahí. Pero ahora su boca se mueve.");
            yield return hud.SayAndWait("", "Está diciendo mi nombre.");
            yield return new WaitForSeconds(1.5f);

            // ── 9 · EL GRITO Y EL DIÁLOGO (literal del relato) ───────
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

            // ── 10 · EL REGRESO ───────────────────────────────────────
            Lock(false, false);
            hud.SetObjective("Vuelve a la casa");
            StartCoroutine(brother.RunAlong(brotherRunPath));
            if (houseReturnTrigger) houseReturnTrigger.gameObject.SetActive(true);
            if (climbTrigger) climbTrigger.gameObject.SetActive(true);

            // En paralelo: si por algún motivo no pasa por la curva, el final igual llega.
            var mimic = StartCoroutine(MimicOnClimb());

            // ── 11 · LA CENA Y EL EPÍLOGO ─────────────────────────────
            yield return WaitTrigger(houseReturnTrigger);
            StopCoroutine(mimic);
            Lock(true, true);
            hud.SetObjective(null);
            yield return hud.Fade(1f, 1.5f);
            yield return hud.ShowCard("Entramos y cenamos.\nNo dijimos nada.", 3.5f);
            yield return hud.ShowCard("Esa noche nos escribimos por mensaje,\npara que nadie más se enterara.", 3.5f);
            yield return hud.ShowCard($"Un año después, {brotherName} entró a mi cuarto.\n\n\"Investigué. Pudimos haber muerto si nos acercábamos.\nResultó que era un skinwalker.\"", 6f);
            yield return hud.ShowCard($"{brotherName} todavía tiene pesadillas.\nSolo habla de eso cuando él empieza la conversación.", 4.5f);
            yield return hud.ShowCard("Nunca volvimos a Wenatchee.", 3.5f);
            yield return hud.ShowCard("Relato original de u/lacoolestalpaca\nr/scarystories · 2020", 4f);
            finished = true;
            FirstPersonController.LockCursor(false);
            yield return hud.ShowCardStay("SOMEONE'S HOME\n<size=26>Relato 1 · Across the River</size>\n\n<size=20>[R] Volver a jugar</size>");
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
            tension.SilenceCrickets(false);
        }

        IEnumerator WaitTrigger(NarrativeTrigger trigger)
        {
            if (!trigger) yield break;
            bool fired = trigger.PlayerInside;
            void OnEntered(NarrativeTrigger _) => fired = true;
            trigger.Entered += OnEntered;
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
