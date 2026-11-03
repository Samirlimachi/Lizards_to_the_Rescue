using UnityEngine;

namespace Lizards
{
    // Objetivo de nivel: llenar el depósito de comida (y, si hay, rescatar al compañero) para abrir la salida.
    public class LevelOneMission : MonoBehaviour
    {
        public FoodGoal depot;
        public LizardPlayer survivor;
        public Door exitDoor;
        public TextMesh board;
        public float requiredFood = 8;
        public int coinReward = 150;
        public string levelName = "Nivel 1";
        public bool Ready { get; private set; }
        public bool Completed { get; private set; }
        float nextMessage;
        float returnAt;
        void Update()
        {
            if(Completed&&Time.time>=returnAt&&UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.StartsWith("LEVEL")&&Application.CanStreamedLevelBeLoaded("Lobby"))
            {UnityEngine.SceneManagement.SceneManager.LoadScene("Lobby");return;}
            bool rescued = !survivor || survivor.Form != LizardForm.Small; // sin compañero que rescatar, solo cuenta la comida
            Ready = depot && depot.Delivered >= requiredFood && rescued;
            if (exitDoor) exitDoor.SetOpen(Ready);
            if (board) board.text = Completed ? $"{levelName.ToUpper()}\n¡COMPLETADO!" :
                $"{levelName.ToUpper()}\nComida: {(depot ? depot.Delivered : 0):0} / {requiredFood:0}" +
                (survivor ? $"\nCompañero: {(rescued ? "A SALVO" : "necesita comida [G]")}" : "") +
                $"\n{(Ready ? "Salida abierta →" : "Llena el bol de comida")}";
        }
        void OnTriggerEnter(Collider c)
        {
            var player = c.GetComponentInParent<LizardPlayer>();
            if (!player || !player.IsControlled || Completed) return;
            if (Ready)
            {
                Completed = true;
                returnAt=Time.time+5;
                if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "LEVEL1") { PlayerPrefs.SetInt("Lizards.Level1Done", 1); PlayerPrefs.Save(); } // desbloquea la estación 02
                var pocket=SwampPocket.Ensure();pocket.Coins+=coinReward;pocket.Save();
                player.Message($"¡{levelName} completado!");
                player.Message($"+{coinReward} monedas. Regreso al refugio en 5 segundos.");
                Fx.Burst(transform.position + Vector3.up * 2, new Color(0.25f, 1, 0.75f), 50, 5, .15f);
                Sfx.Play(Sfx.Help, transform.position);
            }
            else if (Time.time > nextMessage)
            {
                player.Message(survivor ? $"Rescata al compañero y entrega {requiredFood:0} de comida." : $"Llena el bol: faltan {Mathf.Max(0f, requiredFood - (depot ? depot.Delivered : 0)):0} de comida.");
                nextMessage = Time.time + 3;
            }
        }
    }
}
