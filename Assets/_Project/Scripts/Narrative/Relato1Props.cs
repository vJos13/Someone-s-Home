using UnityEngine;

namespace SomeonesHome
{
    /// <summary>
    /// Referencias de la escena para las tareas, el acecho y la noche del Relato 1
    /// (las llena Relato1ExtrasSetup en el editor; el director solo las usa).
    /// </summary>
    public class Relato1Props : MonoBehaviour
    {
        [Header("Llegada")]
        public Transform arrivalSpawn;
        public Transform tammyArrival, emilyArrival, samArrival;
        public Transform[] samRunOff;
        public SimpleInteractable backpack;
        public SimpleInteractable backpackDrop;
        public Transform backpackPlaced;
        public SimpleInteractable frontDoor;
        public SimpleInteractable[] inspect;          // foto, nota del refri, reloj, ventana al campo
        public AudioSource hallVoice;                  // Tammy desde el pasillo (de noche)

        [Header("Apagón")]
        public Flashlight flashlight;
        public SimpleInteractable flashlightPickup;
        public SimpleInteractable breaker;
        public Transform breakerLever;
        public Transform shedInside;
        public Transform fieldWatch;                   // el acechador en el campo (camino al cobertizo)
        public Transform houseCorner;                  // el acechador entre el cobertizo y la casa
        public AudioSource shedKnock;
        public AudioSource fieldWhisper;
        public AudioSource powerClick;

        [Header("Mañana")]
        public GameObject[] morningOnly;               // huellas, saco vacío, camioneta de papá
        public GameObject samBagFull;
        public Transform samMorning;                   // Sam junto a la ventana, mirando las huellas
        public Transform[] trail;                      // puntos de las huellas (ventana → puerta → cobertizo → campo)
        public SimpleInteractable collar;
        public SimpleInteractable dadTruck;
        public Transform samAtTruck;
        public AudioSource horn;

        [Header("Poner la mesa")]
        public SimpleInteractable platesStack;
        public SimpleInteractable table;
        public GameObject tableSetting;

        [Header("Sacar la basura")]
        public SimpleInteractable trashBag;
        public SimpleInteractable trashBin;
        public Transform trashBinTop;
        public DogBarker dog;
        public Transform glimpsePoint;

        [Header("Acecho")]
        public Stalker stalker;
        public AudioSource stalkerSteps;
        public Transform[] stalkerPoints;

        [Header("Noche")]
        public NightMode night;
        public GameObject[] nightOnly;
        public GameObject[] dayOnly;
        public Transform nightSpawn;
        public SimpleInteractable window;
        public GameObject curtainClosed;
        public SimpleInteractable lightSwitch;
        public Light[] houseLights;
        public Renderer[] lampBulbs;
        public SimpleInteractable deadbolt;
        public SimpleInteractable interiorDoor;
        public SimpleInteractable sleepingBag;
        public Renderer samPhoneScreen;
        public Light samPhoneLight;
        public Transform windowOutside;
        public Transform doorOutside;
        public AudioSource windowKnock;
        public AudioSource doorKnock;
        public AudioSource doorRattle;
        public AudioSource whisper;
    }
}
