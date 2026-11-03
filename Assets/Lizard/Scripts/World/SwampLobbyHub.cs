using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lizards
{
    [DefaultExecutionOrder(100)]
    public class SwampLobbyHub : MonoBehaviour
    {
        public LizardLobby roomMenu;
        public Transform boat, wardrobe, telephone, king, kingBelly;
        public TextMesh departureBoard, playersBoard;
        public string levelScene = "LEVEL2";
        public Vector3 spawn = new Vector3(0,.2f,-14);
        public bool Departing { get; private set; }
        float departureAt, nextHint;int outfit;
        Vector3 bellyScale;
        void Start(){if(kingBelly)bellyScale=kingBelly.localScale;}
        void Update()
        {
            if(kingBelly)kingBelly.localScale=Vector3.Scale(bellyScale,Vector3.one*(1+Mathf.Sin(Time.time*1.4f)*.012f));
            var p=LizardPlayer.All.FirstOrDefault(x=>x.IsControlled);
            if(playersBoard)playersBoard.text="JUGADORES\n"+string.Join("\n",LizardPlayer.All.Where(x=>x.IsControlled).Select(x=>"● "+x.displayName));
            if(!p)return;
            if(p.transform.position.y < -2){p.Body.position=spawn;p.Body.linearVelocity=Vector3.zero;if(p.Form==LizardForm.Small)p.Rescue("refugio");}
            if(Departing)
            {
                if(Vector3.Distance(p.transform.position,boat.position)>7){Departing=false;p.Message("Salida cancelada: vuelve a la balsa.");}
                else if(Time.time>=departureAt){Departing=false;SceneManager.LoadScene(levelScene);return;}
            }
            if(departureBoard)departureBoard.text=Departing?"LA BALSA SALE EN "+Mathf.CeilToInt(departureAt-Time.time)+"…":"EMBARCADERO\n[E] expedición a las alcantarillas\nPermanece en la balsa";
            if(LizardLobby.Visible)return;
            float boatDistance=Vector3.Distance(p.transform.position,boat.position);
            float wardrobeDistance=Vector3.Distance(p.transform.position,wardrobe.position);
            float phoneDistance=Vector3.Distance(p.transform.position,telephone.position);
            float kingDistance=Vector3.Distance(p.transform.position,king.position);
            // Cada punto se ofrece al sistema de avisos; solo el más cercano muestra "[E] …" y responde a la E.
            if(boatDistance<5.5f)InteractPrompts.Offer(boat,boat.position+Vector3.up*2.2f,Departing?"Salida en marcha…":"Salir hacia la estación "+(NextLevel=="LEVEL2"?"02":"01"),boatDistance);
            if(phoneDistance<3)InteractPrompts.Offer(telephone,telephone.position+Vector3.up*3.2f,"Abrir sala online",phoneDistance);
            // La tienda de sombreros la gestiona HatWardrobe (probador con vista previa).
            // El Jefe gestiona su propio aviso y diálogo (JefeController).
            if(!p.Input.Pressed(p.Input.Interact))return;
            if(InteractPrompts.IsActive(telephone))roomMenu.OpenRoomMenu();
            else if(InteractPrompts.IsActive(boat))BeginDeparture(p);

        }
        /// <summary>The boat starts the collection expedition. LEVEL1 remains available as its own scene.</summary>
        string NextLevel => Application.CanStreamedLevelBeLoaded("LEVEL2") ? "LEVEL2" : "LEVEL1";
        public bool BeginDeparture(LizardPlayer p)
        {
            levelScene = NextLevel;
            if(!Application.CanStreamedLevelBeLoaded(levelScene)){p.Message(levelScene+" no está disponible en la compilación.");return false;}
            if(roomMenu&&roomMenu.OnlineConnected){p.Message("La salida de expedición está disponible en modo local; la transición online aún necesita sincronización.");return false;}
            if(Departing)return true;
            Departing=true;departureAt=Time.time+3;Sfx.Play(Sfx.Help,boat.position);return true;
        }
        public void CycleHat(LizardPlayer p)
        {
            outfit=(outfit+1)%6;SwampHatFactory.Equip(p,outfit);
        }
    }
}
