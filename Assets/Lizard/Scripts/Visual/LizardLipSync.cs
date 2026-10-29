using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Abre la boca del lagarto según el volumen del micrófono local (sincronía labial por amplitud).
    /// En multijugador habrá que alimentarlo con el audio de voz de cada jugador (p. ej. Photon Voice).
    /// </summary>
    public class LizardLipSync : MonoBehaviour
    {
        const int Window = 512;

        LizardPlayer p;
        AudioClip clip;
        string device;
        readonly float[] buffer = new float[Window];

        public float Openness { get; private set; }
        public float RawLevel { get; private set; }
        public bool Active => clip != null;
        public string Status { get; private set; } = "apagado";

        void Awake() => p = GetComponent<LizardPlayer>();

        void Start()
        {
            if (p.Config.lipSyncEnabled && p.IsControlled) StartMic();
        }

        public void Toggle() { if (Active) StopMic(); else StartMic(); }

        public void StartMic()
        {
            if (Active) return;
            if (Microphone.devices.Length == 0) { Status = "sin micrófono"; return; }
            device = Microphone.devices[0];
            clip = Microphone.Start(device, true, 1, 16000);
            Status = "escuchando: " + device;
        }

        public void StopMic()
        {
            if (clip != null) Microphone.End(device);
            clip = null;
            Openness = 0f;
            Status = "apagado";
        }

        void OnDisable() => StopMic();

        void Update()
        {
            if (clip == null) return;
            int pos = Microphone.GetPosition(device);
            int start = pos - Window;
            if (start < 0) return;
            clip.GetData(buffer, start);
            float sum = 0f;
            for (int i = 0; i < Window; i++) sum += buffer[i] * buffer[i];
            RawLevel = Mathf.Sqrt(sum / Window);

            var cfg = p.Config;
            float target = Mathf.Clamp01((RawLevel - cfg.lipSyncNoiseFloor) * cfg.lipSyncSensitivity);
            Openness = Mathf.Lerp(Openness, target, 1f - Mathf.Exp(-Time.deltaTime * cfg.lipSyncSmoothing));
        }
    }
}
