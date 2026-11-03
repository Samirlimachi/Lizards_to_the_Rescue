using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Lizards
{
    [DefaultExecutionOrder(120)]
    public sealed class LevelTwoExpedition : MonoBehaviour
    {
        public FoodGoal bowl;
        public Transform boat,extractionBeacon;
        public TMP_Text bowlBoard;
        public Text quotaLabel,dayLabel,statusLabel;
        public Image quotaFill;
        public float baseQuota=40,quotaPerDay=8,maxQuota=76;
        public int reward=250;
        public Vector3 arrival=new Vector3(0,.3f,-31);
        public int Day {get;private set;}
        public float RequiredFood {get;private set;}
        public bool Ready=>bowl&&bowl.Delivered+.0001f>=RequiredFood;
        public bool Departing {get;private set;}
        public bool Completed {get;private set;}
        float departureAt;
        void Start()
        {
            Day=Mathf.Max(1,PlayerPrefs.GetInt("Lizards.ExpeditionDay",1));
            RequiredFood=Mathf.Min(maxQuota,baseQuota+(Day-1)*quotaPerDay);
        }
        void Update()
        {
            if(Completed)return;
            float delivered=bowl?bowl.Delivered:0;
            if(dayLabel)dayLabel.text="ALCANTARILLAS · DÍA "+Day;
            if(quotaLabel)quotaLabel.text=$"PROVISIONES  {delivered*10:0} / {RequiredFood*10:0}";
            if(quotaFill)quotaFill.fillAmount=RequiredFood>0?Mathf.Clamp01(delivered/RequiredFood):0;
            if(statusLabel)statusLabel.text=Departing?"REGRESO EN "+Mathf.CeilToInt(departureAt-Time.time)+"…":Ready?"CUOTA LISTA · VUELVE A LA BALSA":"BUSCA COMIDA · LLENA EL BOL CENTRAL";
            if(bowlBoard)bowlBoard.text=Ready?"¡CUOTA COMPLETA!\nREGRESA A LA BALSA":$"BOL DE PROVISIONES\n{delivered*10:0} / {RequiredFood*10:0}";
            if(extractionBeacon)extractionBeacon.localScale=Vector3.one*(Ready?1+.08f*Mathf.Sin(Time.time*3):.65f);
            var player=LizardPlayer.All.FirstOrDefault(p=>p&&p.IsControlled);if(!player)return;
            if(player.transform.position.y< -5)
            {
                player.Body.position=arrival;player.Body.linearVelocity=Vector3.zero;
                if(player.Form==LizardForm.Small)player.Rescue("muelle de expedición");
            }
            float distance=boat?Vector3.Distance(player.transform.position,boat.position):float.MaxValue;
            if(Departing)
            {
                if(distance>6){Departing=false;player.Message("Vuelve a la balsa para regresar.");}
                else if(Time.time>=departureAt)Complete(player);
            }
            if(LizardLobby.Visible||!boat)return;
            if(distance<5.5f)InteractPrompts.Offer(boat,boat.position+Vector3.up*2,Ready?"Regresar al pantano":"Primero llena el bol de provisiones",distance);
            if(InteractPrompts.IsActive(boat)&&player.Input.Pressed(player.Input.Interact))TryDepart(player);
        }
        public bool TryDepart(LizardPlayer player)
        {
            if(!Ready){player.Message("Aún faltan provisiones en el bol central.");return false;}
            if(!boat||Vector3.Distance(player.transform.position,boat.position)>5.5f)return false;
            if(Departing||Completed)return true;
            Departing=true;departureAt=Time.time+3;Sfx.Play(Sfx.Help,boat.position);return true;
        }
        void Complete(LizardPlayer player)
        {
            if(Completed)return;Completed=true;
            var pocket=SwampPocket.Ensure();pocket.Coins+=reward;pocket.Save();
            PlayerPrefs.SetInt("Lizards.ExpeditionDay",Day+1);PlayerPrefs.SetInt("Lizards.Level2Done",1);PlayerPrefs.Save();
            player.Message($"¡Expedición completada! +{reward} monedas");
            if(Application.CanStreamedLevelBeLoaded("Lobby"))SceneManager.LoadScene("Lobby");
        }
    }
}
