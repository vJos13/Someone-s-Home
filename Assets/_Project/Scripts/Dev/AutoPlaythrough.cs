using System.Collections;
using System.Linq;
using UnityEngine;

namespace SomeonesHome
{
    /// <summary>
    /// Juega el Relato 1 completo solo (para grabar un gameplay o probar de punta a punta):
    /// tareas, celular, río, regreso con acecho, noche y final bueno.
    /// Usa AutoPlaytest para caminar y mirar; sigue la historia con Level1Director.Beat.
    /// No está en la escena: se añade en Play desde código. Escribe "[Gameplay] ..." en la consola.
    /// </summary>
    [RequireComponent(typeof(AutoPlaytest))]
    public class AutoPlaythrough : MonoBehaviour
    {
        public string Step { get; private set; } = "inicio";
        public bool Finished { get; private set; }
        /// <summary>Si está activo, en la noche abre la puerta cuando "Tammy" llama (prueba el final malo).</summary>
        public bool badEnding;

        AutoPlaytest bot;
        FirstPersonController player;
        Interactor interactor;
        Door door;
        BrotherNPC brother;
        RiverCreature creature;
        Level1Director director;
        Relato1Props props;
        Phone phone;

        static readonly Vector3[] ToHill = { V(92, 66), V(91, 70), V(86, 80), V(76, 96), V(69, 104.5f) };
        static readonly Vector3[] RockPath =
        {
            V(68, 107.5f), V(72.9f, 108.5f), V(77.8f, 109.5f), V(82.8f, 110.5f), V(87.7f, 111.5f), V(92.6f, 112.5f),
            V(97.5f, 113.5f), V(99.6f, 114.1f), V(98.4f, 114.6f), V(93.6f, 116.2f), V(88.8f, 117.8f), V(84, 119.4f),
            V(80.5f, 120.8f), V(85, 122), V(89.5f, 123)
        };
        static Vector3 V(float x, float z) => new(x, 0f, z);

        /// <summary>Centros de las piedras del sendero (Roca_00 = arriba … última = abajo).</summary>
        static Vector3[] Stones(bool down)
        {
            var list = new System.Collections.Generic.List<Vector3>();
            var path = GameObject.Find("RockPath (sendero de rocas)");
            if (path)
                foreach (Transform c in path.transform)
                    if (c.name.StartsWith("Roca_") && c.GetComponent<BoxCollider>()) list.Add(V(c.position.x, c.position.z));
            if (!down) list.Reverse();
            return list.ToArray();
        }

        IEnumerator Start()
        {
            bot = GetComponent<AutoPlaytest>();
            player = GetComponent<FirstPersonController>();
            interactor = GetComponent<Interactor>();
            door = FindAnyObjectByType<Door>();
            brother = FindAnyObjectByType<BrotherNPC>(FindObjectsInactive.Include);
            creature = FindAnyObjectByType<RiverCreature>(FindObjectsInactive.Include);
            director = FindAnyObjectByType<Level1Director>();
            props = FindAnyObjectByType<Relato1Props>();
            phone = Phone.Instance;
            string B() => director.Beat;

            // ── Llegada ──
            Log("Intro");
            yield return new WaitUntil(() => !player.LookLocked);
            var tammy = GameObject.Find("Tammy");
            if (props.arrivalSpawn)
            {
                Log("Llegada: Tammy, Emily y Sam junto a la camioneta");
                if (tammy) yield return Look(tammy.transform.position + Vector3.up * 1.5f, 0f);
                yield return new WaitUntil(() => B() == "mochila" && !player.InputLocked);
                Log("Saco mi mochila de la camioneta");
                yield return UseAt(props.backpack, V(101.6f, 57.6f));
                Log("La puerta principal está trabada");
                yield return Walk(false, V(99.5f, 57f), V(97.5f, 52.2f), V(91.5f, 52.0f));
                yield return UseAt(props.frontDoor, V(90.2f, 52.4f));
                yield return new WaitForSeconds(2f);
                yield return TryUse(2f);
                yield return new WaitForSeconds(2f);
                Log("Entro por la puerta de atrás");
                yield return Walk(false, V(97.5f, 52.2f), V(98.3f, 57f), V(97.8f, 63.8f), V(93.2f, 64.6f), V(92.3f, 64.4f));
                yield return Look(door.transform.position + new Vector3(0.5f, 1.1f, 0f), 0.3f);
                if (!door.IsOpen) yield return TryUse(3f);
                yield return new WaitForSeconds(1.2f);
                yield return Walk(false, V(92f, 64.2f), V(92f, 60.5f));
                yield return UseAt(props.backpackDrop, V(93.2f, 59.4f));
                yield return new WaitUntil(() => B() == "explorar");
                Log("Exploro la casa: foto, reloj, ventana, nota del refri");
                yield return UseAt(props.inspect[0], V(94.6f, 61.0f));
                yield return UseAt(props.inspect[2], V(88.5f, 61.0f));
                yield return UseAt(props.inspect[3], V(87.0f, 61.3f));
                yield return Walk(false, V(86.3f, 60.4f), V(86.3f, 56.6f));
                yield return UseAt(props.inspect[1], V(85.9f, 54.9f));
                yield return new WaitUntil(() => B() == "cocina");
            }

            // ── Cocina ──
            Log("Cocina: mensajes de mamá y Tammy pide ayuda");
            if (tammy) yield return Look(tammy.transform.position + Vector3.up * 1.5f, 0f);
            yield return new WaitUntil(() => phone && phone.Unread);
            yield return new WaitForSeconds(1f);
            phone.Open(true);                                      // leer el mensaje
            yield return new WaitForSeconds(3f);
            phone.Open(false);
            var emily = GameObject.Find("Emily");
            yield return new WaitUntil(() => B() == "tareas" && !player.InputLocked);
            if (emily) yield return Look(emily.transform.position + Vector3.up * 1.4f, 0.6f);

            // ── Tarea 1: poner la mesa ──
            Log("Tarea: poner la mesa");
            if (props.arrivalSpawn) yield return Walk(false, V(86.5f, 56.6f), V(85.9f, 58.3f));
            else yield return Walk(false, V(89.8f, 56.3f), V(86.5f, 56.6f), V(85.9f, 58.3f));      // rodeando la mesa
            yield return UseAt(props.platesStack, V(85.7f, 59.7f));
            yield return Walk(false, V(86.2f, 60.5f), V(90.3f, 60.4f));
            yield return UseAt(props.table, V(90.0f, 58.5f), props.table.transform.position + new Vector3(0.4f, 0, 0));

            // ── Tarea 2: sacar la basura ──
            Log("Tarea: sacar la basura");
            yield return Walk(false, V(90.3f, 60.6f));
            yield return UseAt(props.trashBag, V(86.1f, 61.0f));
            Log("Abriendo la puerta trasera");
            yield return Walk(false, V(91.9f, 60.9f));
            yield return Look(door.transform.position + new Vector3(0.5f, 1.1f, 0f), 0.3f);
            yield return TryUse(3f);
            yield return new WaitForSeconds(1.2f);
            yield return Walk(false, V(92f, 64.2f));
            yield return UseAt(props.trashBin, V(93.6f, 64.3f));

            // ── El vistazo ──
            Log("El perro ladra… y se calla");
            yield return Look(props.trashBin.transform.position + Vector3.up * 0.6f, 0f);
            yield return new WaitUntil(() => props.stalker.Visible || B() == "llamar");
            if (props.stalker.Visible)
            {
                yield return new WaitForSeconds(1.5f);
                Log("Algo en el borde de la colina");
                bot.lookSpeed = 35f;
                yield return Look(props.stalker.HeadPosition, 3.5f);
                bot.lookSpeed = 70f;
                yield return Look(new Vector3(90f, 21f, 58f), 1.5f);           // aparto la vista (hacia la casa)
                yield return Look(props.glimpsePoint.position + Vector3.up * 2f, 1.5f);
            }

            // ── Llamar a Sam ──
            yield return new WaitUntil(() => phone.CallAvailable);
            yield return new WaitForSeconds(1.2f);
            Log("Llamando a Sam");
            phone.CallRequested = true;
            yield return new WaitUntil(() => B() == "campo");

            // ── Al río ──
            Log("Cruzando el campo");
            yield return Walk(false, ToHill);
            Log("En la colina: mirando hacia el río");
            yield return Look(new Vector3(95f, 6f, 135f), 2f);
            Log("Bajando por el sendero de rocas");
            bot.arriveRadius = 0.35f;
            yield return Walk(false, Stones(true));
            yield return Walk(false, V(85, 122), V(89.5f, 123));
            bot.arriveRadius = 0.7f;
            Log("En el columpio con Sam");
            yield return Look(brother.transform.position + Vector3.up * 1f, 0f);

            yield return new WaitUntil(() => B() == "voz");
            yield return new WaitForSeconds(0.8f);
            Log("Una voz llama desde el campo: me doy vuelta");
            yield return Look(new Vector3(88f, 21.5f, 107f), 0f);
            yield return new WaitUntil(() => creature.Current == RiverCreature.State.Standing);
            yield return new WaitForSeconds(2.5f);
            Log("Vuelvo a mirar a Sam");
            bot.LookAtPoint = brother.transform.position + Vector3.up * 1.3f;
            yield return new WaitUntil(() => player.InputLocked);
            bot.LookAtPoint = null;
            yield return new WaitUntil(() => !player.InputLocked);
            yield return Look(creature.LookTarget.position, 0f);
            yield return new WaitUntil(() => HudSays("Otra vez"));
            yield return new WaitForSeconds(1f);
            Log("Aparto la vista unos segundos…");
            yield return Look(new Vector3(84f, 9f, 110f), 0f);
            yield return new WaitUntil(() => creature.Current == RiverCreature.State.Speaking);
            yield return new WaitForSeconds(1f);
            Log("…y vuelvo a mirar: está diciendo mi nombre");
            bot.lookSpeed = 45f;
            yield return Look(creature.LookTarget.position, 0f);
            bot.lookSpeed = 70f;
            yield return new WaitUntil(() => B() == "regreso" && !player.InputLocked);

            // ── Regreso con acecho: mirar atrás de vez en cuando ──
            Log("Regresando… algo nos sigue");
            yield return Look(new Vector3(84f, 8f, 120f), 0.3f);
            var up = Stones(false);
            int half = up.Length / 2;
            bot.arriveRadius = 0.35f;
            yield return Walk(true, new[] { V(86, 122.4f), V(80.2f, 122f), V(79.3f, 121.4f) }.Concat(up.Take(half)).ToArray());
            yield return LookBack();
            yield return Walk(true, up.Skip(half).Concat(new[] { V(69, 104) }).ToArray());
            bot.arriveRadius = 0.7f;
            yield return LookBack();
            yield return Walk(true, V(76, 96), V(82, 88));
            yield return LookBack();
            yield return Walk(true, V(86, 80), V(90, 72));
            yield return LookBack();
            yield return Walk(true, V(92, 64.5f), V(92f, 60.5f));

            // ── La noche ──
            yield return new WaitUntil(() => B() == "noche" || B() == "mensajes");
            yield return new WaitUntil(() => !player.InputLocked);
            Log("Noche: no puedo dormir");
            yield return new WaitUntil(() => B() == "mensajes" && phone.Unread);
            yield return Look(props.samPhoneScreen.bounds.center, 0f);
            yield return new WaitForSeconds(1.5f);
            phone.Open(true);
            Log("Sam me escribe desde su saco");
            yield return new WaitUntil(() => B() == "ventana" || B() == "apagon");
            phone.Open(false);
            if (B() == "apagon")
            {
                Log("Se corta la luz: busco la linterna");
                yield return new WaitUntil(() => props.flashlightPickup.Available);
                yield return Walk(false, V(89.8f, 56.3f), V(86.5f, 56.6f));
                yield return UseAt(props.flashlightPickup, V(85.6f, 59.0f));
                Log("Al cobertizo con la linterna");
                yield return Walk(false, V(86.2f, 60.5f), V(90.3f, 60.6f), V(91.9f, 60.9f));
                yield return Look(door.transform.position + new Vector3(0.5f, 1.1f, 0f), 0.3f);
                if (!door.IsOpen) yield return TryUse(3f);
                yield return new WaitForSeconds(1.2f);
                yield return Walk(false, V(92f, 64.2f), V(88f, 64.8f));
                float t0 = Time.time;
                while (!props.stalker.Visible && Time.time - t0 < 8f) yield return null;
                if (props.stalker.Visible)
                {
                    Log("Algo parado en el campo");
                    yield return Look(props.stalker.HeadPosition, 2.5f);
                    yield return new WaitUntil(() => !props.stalker.Visible);
                }
                yield return Walk(false, V(84f, 65.4f), V(81.6f, 66.0f));
                yield return UseAt(props.breaker, V(79.0f, 66.1f));
                Log("Volvió la luz… y golpean el cobertizo");
                yield return new WaitForSeconds(2.5f);
                yield return Walk(true, V(81.6f, 66.0f));
                yield return new WaitForSeconds(1.5f);
                Log("¡Corre a la casa!");
                yield return Walk(true, V(86f, 64.9f), V(92f, 64.4f), V(92f, 60.5f));
                yield return new WaitUntil(() => B() == "ventana");
            }
            Log("Golpes en la ventana");
            yield return Walk(false, V(94.6f, 56.6f));
            yield return Look(props.windowOutside.position + Vector3.up * 2.1f, 0f);
            if (badEnding)
            {
                yield return new WaitUntil(() => B() == "voz_puerta" || B() == "asegurar");
                Log("Abro la puerta… (final malo)");
                yield return Walk(false, V(91.9f, 60.9f));
                yield return Look(door.transform.position + new Vector3(0.5f, 1.1f, 0f), 0.3f);
                yield return TryUse(3f);
                yield return new WaitUntil(() => B() == "fin_malo");
                Log("FIN MALO");
                Step = "fin";
                Finished = true;
                yield break;
            }
            yield return new WaitUntil(() => B() == "asegurar");
            Log("Asegurar la casa: cortina, cerrojo y luz");
            yield return UseAt(props.window, V(94.9f, 56.4f));
            yield return UseAt(props.deadbolt, V(92.5f, 61.4f));
            yield return UseAt(props.lightSwitch, V(92.9f, 61.6f));
            yield return new WaitUntil(() => B() == "acostarse");
            yield return new WaitForSeconds(2.5f);
            Log("Acostarse");
            yield return UseAt(props.sleepingBag, V(92.0f, 57.45f));

            // ── La mañana siguiente ──
            yield return new WaitUntil(() => B() == "manana" || B() == "epilogo");
            if (B() == "manana")
            {
                yield return new WaitUntil(() => !player.InputLocked);
                Log("La mañana siguiente: el saco de Sam está vacío");
                yield return Look(props.samBagFull ? props.samBagFull.transform.position : transform.position + transform.forward, 1f);
                yield return new WaitUntil(() => phone.Unread);
                yield return new WaitForSeconds(2f);
                phone.Open(true);
                yield return new WaitForSeconds(2f);
                phone.Open(false);
                Log("Salgo a buscar a Sam");
                yield return Walk(false, V(91.9f, 60.9f), V(92f, 64.2f), V(97.8f, 64.2f), V(98.6f, 60f));
                yield return new WaitUntil(() => B() == "rastro");
                Log("Sigo las huellas");
                yield return Look(props.trail[0].position, 0.5f);
                yield return Walk(false, V(98.3f, 62.8f), V(95f, 64.6f), V(92f, 64.7f), V(88f, 65f), V(84f, 65.6f), V(81.2f, 66.6f),
                                  V(80.8f, 70f), V(81.2f, 76f), V(81.8f, 79.5f), V(82f, 82.3f));
                yield return UseAt(props.collar, V(82f, 82.3f));
                yield return new WaitUntil(() => B() == "papa" && props.dadTruck.Available);
                Log("Llegó papá: a la camioneta");
                yield return Walk(false, V(81.5f, 75f), V(82.5f, 63f), V(82f, 52f), V(95.6f, 48.2f));
                yield return UseAt(props.dadTruck, V(95.6f, 48.2f));
            }

            Log("Epílogo");
            yield return new WaitUntil(() => B() == "fin" || B() == "fin_malo");
            Log(B() == "fin" ? "FIN" : "FIN MALO");
            Step = "fin";
            Finished = true;
        }

        bool HudSays(string s)
        {
            foreach (var t in FindAnyObjectByType<HUD>().GetComponentsInChildren<TMPro.TMP_Text>())
                if (t.name == "SubtituloTexto" && t.text.Contains(s)) return true;
            return false;
        }

        IEnumerator LookBack()
        {
            var s = props.stalker;
            if (s.Visible)
            {
                Log("Miro atrás…");
                yield return Look(s.HeadPosition, 1.2f);
                yield return new WaitForSeconds(0.5f);
            }
        }

        IEnumerator UseAt(SimpleInteractable target, Vector3 stand, Vector3? lookAt = null)
        {
            if (!target) { Debug.LogWarning("[Gameplay] Falta un objeto interactivo"); yield break; }
            yield return Walk(false, stand);
            Vector3 p = lookAt ?? target.GetComponent<Collider>().bounds.center;
            yield return Look(p, 0.25f);
            yield return TryUse(3f);
        }

        IEnumerator TryUse(float timeout)
        {
            float t0 = Time.time;
            while (!interactor.TryInteract())
            {
                if (Time.time - t0 > timeout)
                {
                    Debug.LogWarning($"[Gameplay] No pude interactuar (mirando {interactor.Current?.Prompt ?? "nada"}) en {transform.position}");
                    yield break;
                }
                yield return null;
            }
        }

        IEnumerator Walk(bool run, params Vector3[] pts)
        {
            bot.run = run;
            bot.LookAtPoint = null;
            bot.Go(pts);
            while (bot.Status == "walking") yield return null;
            if (bot.Status != "arrived") Debug.LogWarning($"[Gameplay] El bot terminó en estado {bot.Status}");
        }

        IEnumerator Look(Vector3 p, float hold)
        {
            bot.LookAtPoint = p;
            float t0 = Time.time;
            while (!bot.LookDone(p) && Time.time - t0 < 6f) yield return null;
            if (hold > 0f) yield return new WaitForSeconds(hold);
        }

        void Log(string s)
        {
            Step = s;
            Debug.Log("[Gameplay] " + s);
        }
    }
}
