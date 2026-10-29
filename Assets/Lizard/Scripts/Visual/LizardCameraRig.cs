using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Cámara en tercera persona con colisiones. Sigue la posición del jugador (nunca su rotación, así que es
    /// estable mientras el gigante rueda) y ajusta distancia y altura según la forma.
    /// </summary>
    public class LizardCameraRig : MonoBehaviour
    {
        public LizardPlayer target;
        public float yaw, pitch = 15f;

        Camera cam;
        Vector3 pivot, pivotVel;
        float distance = 5f, height = 1.1f, shake;

        public Transform CameraTransform => cam ? cam.transform : transform;
        public float CurrentDistance => distance;

        void Awake()
        {
            cam = GetComponentInChildren<Camera>();
            if (!cam) cam = gameObject.AddComponent<Camera>();
        }

        void Start() => SetTarget(target);

        public void SetTarget(LizardPlayer t)
        {
            if (target && target.CameraRig == this) target.CameraRig = null;
            target = t;
            if (!t) return;
            t.CameraRig = this;
            var s = t.Stats;
            distance = s.cameraDistance; height = s.cameraHeight;
            pivot = t.transform.position + Vector3.up * height;
            yaw = t.transform.eulerAngles.y;
        }

        public void Shake(float amount) => shake = Mathf.Max(shake, amount);

        void LateUpdate()
        {
            if (!target) return;
            var cfg = target.Config; var s = target.Stats;
            float dt = Time.deltaTime;

            bool locked = Cursor.lockState == CursorLockMode.Locked;
            Vector2 mouse = locked ? target.Input.LookMouse : Vector2.zero;
            Vector2 stick = target.Input.LookStickValue;
            yaw += mouse.x * cfg.mouseSensitivity + stick.x * cfg.stickSensitivity * dt;
            pitch -= mouse.y * cfg.mouseSensitivity + stick.y * cfg.stickSensitivity * dt * 0.7f;
            pitch = Mathf.Clamp(pitch, cfg.minPitch, cfg.maxPitch);

            float zl = 1f - Mathf.Exp(-dt * cfg.cameraZoomLerp);
            float wantDist = s.cameraDistance;
            height = Mathf.Lerp(height, s.cameraHeight, zl);

            Vector3 follow = target.transform.position + Vector3.up * height;
            pivot = Vector3.SmoothDamp(pivot, follow, ref pivotVel, cfg.cameraFollowSmooth);

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 back = rot * Vector3.back;

            // Colisión: acerca la cámara al instante, la aleja suavemente.
            float allowed = wantDist;
            foreach (var h in Physics.SphereCastAll(pivot, cfg.cameraCollisionRadius, back, wantDist, ~0, QueryTriggerInteraction.Ignore))
            {
                var b = h.collider.attachedRigidbody;
                if (b && (b == target.Body || !b.isKinematic)) continue;
                if (h.distance <= 0f) continue;
                allowed = Mathf.Min(allowed, Mathf.Max(0.3f, h.distance - 0.05f));
            }
            distance = allowed < distance ? allowed : Mathf.Lerp(distance, allowed, zl);

            Vector3 offset = Vector3.zero;
            if (shake > 0f)
            {
                offset = new Vector3(Mathf.PerlinNoise(Time.time * 30f, 0f) - 0.5f, Mathf.PerlinNoise(0f, Time.time * 30f) - 0.5f, 0f) * shake;
                shake = Mathf.MoveTowards(shake, 0f, dt * 1.5f);
            }
            CameraTransform.SetPositionAndRotation(pivot + back * distance + rot * offset, rot);

            if (target.Input.controlled && UnityEngine.InputSystem.Mouse.current != null &&
                UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame && !LizardHUD.MenuOpen)
            {
                Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            }
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            }
        }
    }
}
