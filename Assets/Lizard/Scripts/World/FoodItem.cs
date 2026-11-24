using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    public enum FoodState { Available, Grabbed, Consumed }

    /// <summary>
    /// Alimento con valor nutricional, tamaño y masa. Solo un jugador puede reclamarlo a la vez
    /// y solo puede consumirse una vez (el estado Consumed es definitivo).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class FoodItem : MonoBehaviour
    {
        public float nutrition = 1f;
        [Tooltip("Radio aproximado; se compara con la capacidad de la boca.")]
        public float size = 0.2f;
        public bool edible = true;

        public static readonly List<FoodItem> All = new();
        public static event Action<FoodItem, LizardPlayer> OnConsumed;

        public FoodState State { get; private set; } = FoodState.Available;
        public LizardPlayer Holder { get; private set; }
        public Rigidbody Body { get; private set; }
        readonly HashSet<LizardPlayer> grabbers = new();
        public int GrabberCount {get{grabbers.RemoveWhere(p=>!p);return grabbers.Count;}}
        public bool IsHeldBy(LizardPlayer player) => player && grabbers.Contains(player);
        public bool SharedPullAllowed => GetComponent<BreakableFood>() is BreakableFood b && b.isActiveAndEnabled && !b.Broken;

        float noPickupUntil, ownerNoPickupUntil;
        LizardPlayer ejectedBy;

        void Awake() { Body = GetComponent<Rigidbody>(); }

        // ---- Tamaño visible ----
        // La comida se veía muy pequeña al lado de la lagartija (~1,45 m). Al aparecer en juego se agranda su dibujo y su
        // colisión una sola vez: la pequeña ×1,6, la mediana ×1,3; la grande (salteña, sandía) se queda igual.
        // Tragar/arrastrar no cambia: eso lo decide 'size' (comparado con la boca), que no se toca.
        // 'enlarged' viaja con las copias (Instantiate): la comida escupida o partida no vuelve a crecer.
        public const float SmallFoodScale = 1.6f, MediumFoodScale = 1.3f;
        [SerializeField, HideInInspector] bool enlarged;

        void Start() => Enlarge();

        public static float ScaleFor(float size) => size <= 0.3f ? SmallFoodScale : size <= 0.5f ? MediumFoodScale : 1f;

        void Enlarge()
        {
            if (enlarged) return;
            enlarged = true;
            float k = ScaleFor(size);
            if (k <= 1.001f) return;
            var col = GetComponent<Collider>();
            float halfHeight = col ? col.bounds.extents.y : 0f;
            transform.localScale *= k;
            // Crece desde el suelo, no hacia dentro de él (si no, saltaría al empezar).
            transform.position += Vector3.up * halfHeight * (k - 1f);
            // Si al crecer quedó metida en una pared, se aparta ya mismo hacia su lado (así una fruta tras la membrana
            // de ácido no la atraviesa). Si toca otra comida, se separa suave en vez de salir disparada.
            if (col) PushOutOfWalls(col);
            if (Body) Body.maxDepenetrationVelocity = 1.5f;
        }

        static readonly Collider[] overlaps = new Collider[16];
        void PushOutOfWalls(Collider col)
        {
            var b = col.bounds;   // aún sin sincronizar: se estima con el factor
            float k = ScaleFor(size);
            Vector3 center = b.center + Vector3.up * b.extents.y * (k - 1f);
            int n = Physics.OverlapBoxNonAlloc(center, b.extents * k * 0.95f, overlaps, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            bool walls = false;
            for (int i = 0; i < n; i++) { var o = overlaps[i]; if (!o.transform.IsChildOf(transform) && !o.GetComponentInParent<FoodItem>() && (!o.attachedRigidbody || o.attachedRigidbody.isKinematic)) { walls = true; break; } }
            if (!walls) return;
            Physics.SyncTransforms();
            for (int i = 0; i < n; i++)
            {
                var o = overlaps[i];
                if (o.transform.IsChildOf(transform) || o.GetComponentInParent<FoodItem>() || (o.attachedRigidbody && !o.attachedRigidbody.isKinematic)) continue;
                if (Physics.ComputePenetration(col, transform.position, transform.rotation, o, o.transform.position, o.transform.rotation, out var dir, out var dist))
                    transform.position += dir * (dist + 0.01f);
            }
            if (Body) Body.position = transform.position;
        }
        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public bool FitsMouth(float capacity) => size <= capacity + 0.0001f;

        public bool CanBeEatenBy(LizardPlayer p)
        {
            if (!edible || State == FoodState.Consumed) return false;
            if (State == FoodState.Grabbed && (!IsHeldBy(p) || grabbers.Count > 1)) return false;
            if (Time.time < noPickupUntil) return false;
            if (p == ejectedBy && Time.time < ownerNoPickupUntil) return false;
            return true;
        }

        public bool CanBeGrabbedBy(LizardPlayer p) =>
            State != FoodState.Consumed && (State == FoodState.Available || IsHeldBy(p) || SharedPullAllowed);

        public bool TryGrab(LizardPlayer p)
        {
            if (!CanBeGrabbedBy(p)) return false;
            if (!p) return false;
            grabbers.Add(p);
            State = FoodState.Grabbed; if (!Holder) Holder = p;
            return true;
        }

        public void ReleaseGrab(LizardPlayer p)
        {
            if (!grabbers.Remove(p)) return;
            Holder = null;
            foreach (var other in grabbers) if (other) { Holder = other; break; }
            State = Holder ? FoodState.Grabbed : FoodState.Available;
        }

        /// <summary>Consumo atómico: devuelve el valor solo la primera vez.</summary>
        public bool TryConsume(LizardPlayer eater, out float value)
        {
            value = 0f;
            if (!eater && GetComponent<SharedFoodSync>()?.Replica == true) return false;
            if (eater && !CanBeEatenBy(eater)) return false;
            if (!eater && State == FoodState.Consumed) return false;
            value = nutrition;
            nutrition = 0f;
            State = FoodState.Consumed;
            Holder = null;
            grabbers.Clear();
            OnConsumed?.Invoke(this, eater);
            gameObject.SetActive(false);
            Destroy(gameObject);
            return true;
        }

        public void MarkEjected(LizardPlayer by, float delay, float ownerDelay)
        {
            ejectedBy = by;
            noPickupUntil = Time.time + delay;
            ownerNoPickupUntil = Time.time + ownerDelay;
        }

        public static float TotalInWorld()
        {
            float t = 0f;
            foreach (var f in All) if (f.State != FoodState.Consumed) t += f.nutrition;
            return t;
        }
    }
}
