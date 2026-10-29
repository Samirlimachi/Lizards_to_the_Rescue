using System;
using UnityEngine;

namespace Lizards
{
    public enum LizardForm { Normal, Small, Giant }

    /// <summary>Parámetros que cambian según la forma del personaje.</summary>
    [Serializable]
    public class FormStats
    {
        [Header("Colisión y masa")]
        public bool useSphere;
        public float colliderRadius = 0.4f;
        public float colliderHeight = 1.0f;
        public float mass = 5f;

        [Header("Movimiento")]
        public float moveSpeed = 6f;
        public float groundAccel = 50f;
        public float groundDecel = 60f;
        public float airAccel = 14f;
        public float jumpVelocity = 7f;
        public float turnSpeed = 720f;
        public bool holdOnSlopes = true;

        [Header("Visual y cámara")]
        public float visualScale = 1f;
        public float cameraDistance = 5f;
        public float cameraHeight = 1.1f;

        [Header("Lengua")]
        public bool tongueEnabled = true;
        public bool tongueGrapple = true;
        public bool tongueCanGrabPlayers = true;
        public float tongueRange = 12f;
        public float tongueMaxForce = 250f;
        [Tooltip("Objetos con masa menor o igual se acercan al jugador.")]
        public float tongueMaxLiftMass = 8f;
        [Tooltip("Objetos más pesados que esto no se pueden agarrar.")]
        public float tongueMaxGrabMass = 1000f;

        [Header("Boca")]
        public bool acidEnabled = true;
        [Tooltip("Tamaño (radio) máximo de comida que puede tragar.")]
        public float mouthCapacity = 0.35f;
        public bool canEject = true;

        public static FormStats Normal() => new FormStats();

        public static FormStats Small() => new FormStats
        {
            useSphere = true, colliderRadius = 0.22f, colliderHeight = 0.44f, mass = 1.5f,
            moveSpeed = 2.4f, groundAccel = 20f, groundDecel = 25f, airAccel = 4f, jumpVelocity = 3.2f, turnSpeed = 400f,
            visualScale = 0.45f, cameraDistance = 3.2f, cameraHeight = 0.6f,
            tongueEnabled = true, tongueGrapple = false, tongueCanGrabPlayers = false,
            tongueRange = 4f, tongueMaxForce = 25f, tongueMaxLiftMass = 1f, tongueMaxGrabMass = 1f,
            acidEnabled = false, mouthCapacity = 0.2f, canEject = false,
        };

        public static FormStats Giant() => new FormStats
        {
            useSphere = true, colliderRadius = 1.2f, colliderHeight = 2.4f, mass = 60f,
            moveSpeed = 9f, groundAccel = 9f, groundDecel = 2.5f, airAccel = 3f, jumpVelocity = 4.5f, turnSpeed = 200f,
            holdOnSlopes = false,
            visualScale = 2.4f, cameraDistance = 9.5f, cameraHeight = 2.4f,
            tongueEnabled = true, tongueGrapple = false, tongueCanGrabPlayers = true,
            tongueRange = 7f, tongueMaxForce = 160f, tongueMaxLiftMass = 3f, tongueMaxGrabMass = 30f,
            acidEnabled = false, mouthCapacity = 1.0f, canEject = true,
        };
    }

    /// <summary>Todos los valores de equilibrio del lagarto en un solo asset editable.</summary>
    [CreateAssetMenu(menuName = "Lizards/Lizard Config", fileName = "LizardConfig")]
    public class LizardConfig : ScriptableObject
    {
        [Header("Formas")]
        public FormStats normal = FormStats.Normal();
        public FormStats small = FormStats.Small();
        public FormStats giant = FormStats.Giant();

        [Header("Movimiento")]
        public float maxSlopeAngle = 50f;
        public float groundCheckDistance = 0.12f;
        public float coyoteTime = 0.12f;
        public float jumpBufferTime = 0.15f;
        public float fallGravityMultiplier = 1.8f;
        public float maxFallSpeed = 30f;
        public float killY = -15f;

        [Header("Golpes y recuperación")]
        public float knockbackForce = 6f;
        public float knockbackUp = 4f;
        public float stunTime = 0.35f;
        public float invulnerabilityTime = 1.2f;
        public bool strongImpactsEnabled = false;
        public float strongImpactSpeed = 16f;

        [Header("Comida")]
        public float maxFood = 14f;
        public float giantThreshold = 10f;
        [Tooltip("Por debajo de este valor el gigante vuelve a normal (histéresis).")]
        public float giantExitThreshold = 7f;
        public float smallRecoverFood = 1f;
        public float voluntaryEjectAmount = 1f;
        public float damageEjectAmount = 3f;
        public float feedAmount = 1f;
        public float pelletValue = 1f;
        public float pelletSize = 0.15f;
        public float pelletPickupDelay = 0.6f;
        public float pelletOwnerPickupDelay = 1.2f;
        public float eatRadius = 1.3f;
        public float swallowDistance = 0.9f;
        public float feedRadius = 2.5f;

        [Header("Gigante")]
        public float giantBreakSpeed = 3.5f;
        public float giantImpactImpulseScale = 0.6f;
        public float giantMaxImpactImpulse = 300f;
        public float giantEnemyCrushSpeed = 3f;

        [Header("Lengua")]
        public float tongueExtendSpeed = 40f;
        public float tongueRetractSpeed = 55f;
        public float tongueMinLength = 1.0f;
        public float tongueReelSpeed = 6f;
        public float tongueAutoReelSpeed = 8f;
        public float tongueSpring = 120f;
        public float tongueDamper = 10f;
        public float tongueElasticSlack = 0.4f;
        public float tongueBreakStretch = 1.6f;
        public float tongueLosGraceTime = 0.25f;
        public float tongueAimAssistRadius = 0.35f;
        [Tooltip("Objetos con más masa (o cinemáticos) actúan como ancla: el jugador va hacia ellos.")]
        public float tongueAnchorMass = 150f;
        public float tongueReleaseBoost = 1.1f;
        public float tongueMaxReleaseSpeed = 18f;
        public float tongueMaxBodySpeed = 25f;
        public float partnerLaunchForward = 9f;
        public float partnerLaunchUp = 7f;
        public float partnerLaunchMaxDistance = 2.5f;
        public bool tongueHoldToStay = true;

        [Header("Ácido")]
        public float maxAcid = 3f;
        public float acidCost = 1f;
        public float acidRegenPerSecond = 0.5f;
        public float acidCooldown = 0.45f;
        public float acidSpeed = 18f;
        public float acidArcUp = 1.5f;
        public float acidGravityScale = 0.5f;
        public float acidDamage = 1f;
        public float acidLifetime = 4f;
        public bool acidFriendlyFire = false;

        [Header("Ayuda")]
        public float helpCooldown = 2f;

        [Header("Cámara")]
        public float mouseSensitivity = 0.12f;
        public float stickSensitivity = 180f;
        public float minPitch = -30f;
        public float maxPitch = 70f;
        public float cameraCollisionRadius = 0.25f;
        public float cameraZoomLerp = 4f;
        public float cameraFollowSmooth = 0.06f;

        [Header("Sincronía labial (micrófono)")]
        public bool lipSyncEnabled = true;
        public float lipSyncSensitivity = 18f;
        public float lipSyncNoiseFloor = 0.01f;
        public float lipSyncSmoothing = 14f;
        public float maxJawAngle = 32f;

        [Header("Visual")]
        public float squashAmount = 0.18f;
        public float blinkMin = 2f;
        public float blinkMax = 5f;

        public FormStats Get(LizardForm f) => f == LizardForm.Small ? small : f == LizardForm.Giant ? giant : normal;

        static LizardConfig fallback;
        public static LizardConfig Fallback => fallback ? fallback : (fallback = CreateInstance<LizardConfig>());
    }
}
