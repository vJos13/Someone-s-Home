using UnityEngine;

namespace SomeonesHome
{
    /// <summary>
    /// Cuerpo visible del jugador en la vista isométrica (en primera persona se oculta).
    /// - Con Animator: le pasa Speed (m/s), Moving y Running (crear esos parámetros en el controller).
    /// - Sin Animator (el proyecto no tiene animaciones): alterna poses estáticas (quieto / caminando / corriendo)
    ///   con histéresis y les añade movimiento procedural: rebote por paso sincronizado con la distancia
    ///   recorrida (no patina), inclinación hacia delante según la velocidad, ladeo al girar y respiración.
    /// La velocidad sale de FirstPersonController.PlanarVelocity (desplazamiento real, contra una pared = 0).
    /// </summary>
    public class PlayerAvatar : MonoBehaviour
    {
        [SerializeField] FirstPersonController player;

        [Header("Animator (opcional)")]
        [SerializeField] Animator animator;
        [SerializeField] string speedParam = "Speed";
        [SerializeField] string movingParam = "Moving";
        [SerializeField] string runningParam = "Running";
        [SerializeField] float animatorDamp = 0.1f;

        [Header("Poses (sin Animator)")]
        [SerializeField] GameObject idlePose;
        [SerializeField] GameObject walkPose;
        [Tooltip("Vacío = usa la de caminar.")]
        [SerializeField] GameObject runPose;
        [Tooltip("m/s para pasar a caminar / para volver a quieto (histéresis).")]
        [SerializeField] float startMovingSpeed = 0.35f;
        [SerializeField] float stopMovingSpeed = 0.18f;

        [Header("Movimiento procedural")]
        [Tooltip("Metros por paso (el rebote va sincronizado con la distancia recorrida).")]
        [SerializeField] float strideLength = 0.75f;
        [SerializeField] float bobHeight = 0.045f;
        [SerializeField] float leanAtRun = 9f;
        [SerializeField] float turnRoll = 6f;
        [SerializeField] float breathAmount = 0.008f;
        [SerializeField] float smoothing = 10f;

        [Header("Tinte de ropa (diferenciar del modelo original)")]
        [SerializeField] string tintMaterialName = "M_Sam_Hoodie";
        [SerializeField] Color tint = new(0.42f, 0.2f, 0.17f);

        Vector3 baseLocalPos;
        Quaternion baseLocalRot;
        float phase, bob, lean, roll, breath;
        float lastYaw;
        bool moving;
        GameObject shown;
        Renderer[] renderers;
        int speedId, movingId, runningId;

        void Awake()
        {
            if (!player) player = GetComponentInParent<FirstPersonController>();
            baseLocalPos = transform.localPosition;
            baseLocalRot = transform.localRotation;
            renderers = GetComponentsInChildren<Renderer>(true);
            speedId = Animator.StringToHash(speedParam);
            movingId = Animator.StringToHash(movingParam);
            runningId = Animator.StringToHash(runningParam);
            ApplyTint();
            if (!animator) Show(idlePose);
            if (player) lastYaw = player.transform.eulerAngles.y;
        }

        void OnEnable()
        {
            if (!player) return;
            player.ViewModeChanged += OnViewMode;
            SetVisible(player.IsIsometric);
        }

        void OnDisable()
        {
            if (player) player.ViewModeChanged -= OnViewMode;
        }

        void OnViewMode(FirstPersonController.ViewMode mode) => SetVisible(mode == FirstPersonController.ViewMode.Isometric);

        void SetVisible(bool visible)
        {
            foreach (var r in renderers) if (r) r.enabled = visible;
        }

        void ApplyTint()
        {
            if (string.IsNullOrEmpty(tintMaterialName)) return;
            Material tinted = null;
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!mats[i] || !mats[i].name.StartsWith(tintMaterialName)) continue;
                    if (!tinted)
                    {
                        tinted = new Material(mats[i]) { name = mats[i].name + " (Alex)" };
                        if (tinted.HasProperty("_BaseColor")) tinted.SetColor("_BaseColor", tint);
                    }
                    mats[i] = tinted;
                    changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        void Show(GameObject pose)
        {
            if (!pose || pose == shown) return;
            shown = pose;
            foreach (var p in new[] { idlePose, walkPose, runPose })
                if (p) p.SetActive(p == pose);
        }

        void LateUpdate()
        {
            if (!player) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            float speed = player.PlanarVelocity.magnitude;
            bool grounded = player.IsGrounded;
            bool runningNow = player.IsSprinting && speed > player.WalkSpeed * 1.1f;

            moving = moving ? speed > stopMovingSpeed : speed > startMovingSpeed;

            if (animator)
            {
                animator.SetFloat(speedId, speed, animatorDamp, dt);
                animator.SetBool(movingId, moving);
                animator.SetBool(runningId, runningNow);
                return;
            }

            Show(!moving ? idlePose : runningNow && runPose ? runPose : walkPose ? walkPose : idlePose);

            // Rebote por paso: avanza con la distancia recorrida (si el cuerpo no avanza, no "camina").
            float k = 1f - Mathf.Exp(-smoothing * dt);
            float speed01 = Mathf.Clamp01(speed / Mathf.Max(0.1f, player.RunSpeed));
            if (moving && grounded)
            {
                phase += speed * dt / Mathf.Max(0.1f, strideLength) * Mathf.PI;
                bob = Mathf.Lerp(bob, Mathf.Abs(Mathf.Sin(phase)) * bobHeight * Mathf.Clamp01(speed / player.WalkSpeed), k * 2f);
            }
            else bob = Mathf.Lerp(bob, 0f, k);

            lean = Mathf.Lerp(lean, moving ? leanAtRun * speed01 : 0f, k);
            float yaw = player.transform.eulerAngles.y;
            float yawRate = Mathf.DeltaAngle(lastYaw, yaw) / dt;
            lastYaw = yaw;
            roll = Mathf.Lerp(roll, moving ? Mathf.Clamp(-yawRate / 180f, -1f, 1f) * turnRoll * speed01 : 0f, k);
            breath = moving ? Mathf.Lerp(breath, 0f, k) : Mathf.Sin(Time.time * 1.6f) * breathAmount;

            transform.localPosition = baseLocalPos + Vector3.up * (bob + breath);
            transform.localRotation = baseLocalRot * Quaternion.Euler(lean, 0f, roll);
        }
    }
}
