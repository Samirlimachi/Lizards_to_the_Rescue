using System.Linq;
using UnityEngine;

namespace Lizards
{
    [DefaultExecutionOrder(110)]
    public sealed class ExpeditionSupplyCrate : MonoBehaviour
    {
        public FoodItem[] foods;
        public int count=3;
        public GameObject closedVisual,openVisual;
        public Collider closedCollider;
        public bool Opened { get; private set; }
        /// <summary>Se abre (aquí o por red). LevelNet lo usa para avisar al resto de jugadores.</summary>
        public event System.Action OnOpened;
        string netKey;
        void Awake()
        {
            // Clave estable en todos los clientes: la posición inicial de la caja en la escena.
            var p=transform.position;netKey=$"caja{Mathf.RoundToInt(p.x*10)}_{Mathf.RoundToInt(p.y*10)}_{Mathf.RoundToInt(p.z*10)}";
        }
        void Update()
        {
            if(Opened||LizardLobby.Visible)return;
            var p=LizardPlayer.All.FirstOrDefault(x=>x&&x.IsControlled);if(!p)return;
            float distance=Vector3.Distance(p.CenterPosition,transform.position);
            if(distance>2.4f)return;
            InteractPrompts.Offer(transform,transform.position+Vector3.up*.9f,"Abrir caja de provisiones",distance);
            if(InteractPrompts.IsActive(transform)&&p.Input.Pressed(p.Input.Interact))Open();
        }
        void OnCollisionEnter(Collision collision)
        {
            var p=collision.collider.GetComponentInParent<LizardPlayer>();
            if(p&&p.IsControlled&&p.Form==LizardForm.Giant&&collision.relativeVelocity.magnitude>2)Open();
        }
        public bool Open()
        {
            if(Opened||foods==null||foods.Length==0)return false;
            Opened=true;
            if(closedVisual)closedVisual.SetActive(false);
            if(closedCollider)closedCollider.enabled=false;
            if(openVisual)openVisual.SetActive(true);
            var body=GetComponent<Rigidbody>();if(body)body.mass=1.5f;
            for(int i=0;i<count;i++)
            {
                var position=transform.TransformPoint(new Vector3((i%3-1)*.23f,.85f+(i/3)*.18f,0));
                var food=Instantiate(foods[i%foods.Length],position,Quaternion.Euler(0,(i*137)%360,0));
                // Misma clave en todos los clientes: la comida de la caja es la misma comida compartida para todos.
                var sync=food.GetComponent<SharedFoodSync>();if(sync)sync.Bind(netKey+"-"+i);
                food.Body.linearVelocity=transform.up*1.8f+new Vector3((i%3-1)*.3f,0,((i*7)%3-1)*.3f);
            }
            Sfx.Play(Sfx.Stick,transform.position,.5f);
            Fx.Burst(transform.position+Vector3.up*.5f,new Color(.85f,.63f,.33f),8,1,.06f);
            OnOpened?.Invoke();
            return true;
        }
    }
}
