using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Lizards
{
    /// <summary>
    /// Expedición del nivel 2: llenar el bol con la cuota del día y volver en la balsa.
    /// En multijugador el cliente maestro lleva la cuota y la cuenta atrás; cualquiera puede pedir la salida desde la balsa
    /// y al terminar todos reciben la recompensa y vuelven juntos al refugio.
    /// </summary>
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
        public float DepartRemaining=>Departing?Mathf.Max(0,departureAt-Time.time):0;
        float departureAt;
        bool OnlineReplica=>LevelNet.Online&&!LevelNet.Master;

        void Start()
        {
            Day=Mathf.Max(1,PlayerPrefs.GetInt("Lizards.ExpeditionDay",1));
            RequiredFood=Mathf.Min(maxQuota,baseQuota+(Day-1)*quotaPerDay);
            SetupBowl();
        }

        /// <summary>
        /// El embudo acumula: la comida se queda dentro y cuenta mientras esté en el hueco (si la sacan, se resta).
        /// El volumen que cuenta es el cilindro del hueco (radio del borde) desde la rejilla hasta algo por encima del
        /// aro, para que también cuente el montón que sobresale. Con efectos de "+10" y celebración al completar.
        /// </summary>
        void SetupBowl()
        {
            if(!bowl)return;
            bowl.accumulate=true;bowl.holeRadius=3.35f;
            var box=bowl.GetComponent<BoxCollider>();
            float rim=1.1f;   // el aro queda ~0,25 m por encima de la parte alta de la caja original
            if(box){float bottom=box.center.y-box.size.y*.5f;float oldTop=box.center.y+box.size.y*.5f;float top=oldTop+1.3f;rim=oldTop+.25f;box.size=new Vector3(box.size.x,top-bottom,box.size.z);box.center=new Vector3(box.center.x,(top+bottom)*.5f,box.center.z);}
            var fx=bowl.GetComponent<ProvisionBowlFx>()??bowl.gameObject.AddComponent<ProvisionBowlFx>();
            fx.rimHeight=rim;
            fx.Init(bowl,this);
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
            if(Departing&&!OnlineReplica)
            {
                // Solo: hay que quedarse en la balsa. En grupo la balsa espera a todos y sale al acabar la cuenta.
                if(!LevelNet.Online&&distance>6){Departing=false;player.Message("Vuelve a la balsa para regresar.");}
                else if(Time.time>=departureAt)
                {
                    if(LevelNet.Online){LevelNet.Local.RPC_ExpeditionComplete();}
                    else Complete(player);
                }
            }
            if(LizardLobby.Visible||!boat)return;
            if(distance<5.5f)InteractPrompts.Offer(boat,boat.position+Vector3.up*2,Departing?"La balsa sale enseguida…":Ready?"Regresar al pantano":"Primero llena el bol de provisiones",distance);
            if(InteractPrompts.IsActive(boat)&&player.Input.Pressed(player.Input.Interact))TryDepart(player);
        }
        public bool TryDepart(LizardPlayer player)
        {
            if(!Ready){player.Message("Aún faltan provisiones en el bol central.");return false;}
            if(!boat||Vector3.Distance(player.transform.position,boat.position)>5.5f)return false;
            if(Departing||Completed)return true;
            if(OnlineReplica){LevelNet.Local.RPC_RequestDepart();player.Message("¡Avisando al grupo! La balsa sale enseguida.");return true;}
            MasterStartDeparture();return true;
        }
        /// <summary>Empieza la cuenta atrás (solo, o el maestro en grupo).</summary>
        public void MasterStartDeparture()
        {
            if(Departing||Completed||!Ready)return;
            Departing=true;departureAt=Time.time+(LevelNet.Online?5:3);Sfx.Play(Sfx.Help,boat?boat.position:transform.position);
            foreach(var p in LizardPlayer.All)if(p&&p.IsControlled)p.Message(LevelNet.Online?"¡La balsa sale en 5 segundos! Todos vuelven juntos.":"¡Regreso en 3 segundos!");
        }
        /// <summary>Estado que manda el maestro.</summary>
        public void NetApply(int day,float required,bool departing,float remaining)
        {
            Day=day;RequiredFood=required;
            if(departing&&!Departing){Sfx.Play(Sfx.Help,boat?boat.position:transform.position);foreach(var p in LizardPlayer.All)if(p&&p.IsControlled)p.Message("¡La balsa sale en unos segundos! Todos vuelven juntos.");}
            Departing=departing;departureAt=Time.time+remaining;
        }
        /// <summary>Fin de la expedición en grupo: cada cliente cobra lo suyo; el maestro lleva a todos al refugio.</summary>
        public void NetComplete()
        {
            var player=LizardPlayer.All.FirstOrDefault(p=>p&&p.IsControlled);
            Complete(player);
        }
        void Complete(LizardPlayer player)
        {
            if(Completed)return;Completed=true;
            var pocket=SwampPocket.Ensure();pocket.Coins+=reward;pocket.Save();
            PlayerPrefs.SetInt("Lizards.ExpeditionDay",Day+1);PlayerPrefs.SetInt("Lizards.Level2Done",1);PlayerPrefs.Save();
            if(player)player.Message($"¡Expedición completada! +{reward} monedas");
            if(LevelNet.Online&&!LevelNet.Master)return;      // el maestro cambia la escena para todos
            if(Application.CanStreamedLevelBeLoaded("Lobby"))PcFrontEnd.Travel("Lobby");
        }
    }
}
