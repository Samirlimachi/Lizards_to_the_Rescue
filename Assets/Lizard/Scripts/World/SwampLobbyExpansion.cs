using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lizards
{
    [DefaultExecutionOrder(110)]
    public class SwampLobbyExpansion : MonoBehaviour
    {
        public Transform vending, colors, bell;
        public TextMesh vendingLabel;
        public UnityEngine.UI.Text inventoryLabel, coinsLabel;
        public int Selected { get; private set; }
        public static readonly string[] Names={"Pelota rebotadora","Granada","Rana hinchable","Bolsa paracaídas","Palo selfie","Cama elástica","Chicle de barriga","Chicle de lengua","Chicle cafeína"};
        public static readonly int[] Prices={40,60,75,30,50,50,40,40,40};
        int colorIndex;float next;
        void Awake(){SwampPocket.Ensure();}
        public void Browse(int delta){Selected=(Selected+delta+Names.Length)%Names.Length;}
        public bool Buy(LizardPlayer p)
        {
            var pocket=SwampPocket.Ensure();
            if(pocket.Coins<Prices[Selected]){p.Message($"Te faltan {Prices[Selected]-pocket.Coins} monedas para {Names[Selected]}.");Sfx.Play(Sfx.Hurt,vending.position,.3f);return false;}
            pocket.Coins-=Prices[Selected];pocket.Save();
            var capsule=GameObject.CreatePrimitive(PrimitiveType.Sphere);capsule.name="Cápsula / "+Names[Selected];capsule.transform.position=vending.position+new Vector3(-1,.65f,-1.9f);capsule.transform.localScale=Vector3.one*.52f;
            capsule.GetComponent<Renderer>().material=LizardMaterials.Lit(Color.HSVToRGB(Selected/9f,.55f,1),.65f);capsule.AddComponent<Rigidbody>().mass=.3f;capsule.AddComponent<TongueTarget>().kind=TongueTargetKind.Object;capsule.AddComponent<SwampCapsule>().item=Selected;
            p.Message("Recoge la cápsula con E o acércala con la lengua.");return true;
        }
        public void ChangeColor(LizardPlayer p)
        {
            var palette=new[]{new Color(.35f,.8f,.15f),new Color(1,.35f,.16f),new Color(.35f,.65f,1),new Color(.8f,.35f,.95f),new Color(1,.8f,.1f),new Color(1,.45f,.7f)};
            colorIndex=(colorIndex+1)%palette.Length;
            // Tiñe todo el personaje (cuerpo, cabeza, párpados, bola del gigante) sin perder barriga, boca ni ojos.
            var sunny=p.GetComponentInChildren<SunnyVisual>();
            if(sunny)sunny.SetSkinColor(palette[colorIndex]);
            else foreach(var renderer in p.GetComponentsInChildren<Renderer>())foreach(var material in renderer.materials)
            {if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",palette[colorIndex]);}
            p.Message("Color actualizado");
        }
        void Update()
        {
            var p=LizardPlayer.All.FirstOrDefault(x=>x.IsControlled);var pocket=SwampPocket.Ensure();
            if(coinsLabel)coinsLabel.text="● "+pocket.Coins+"   /   DÍA 1";
            if(inventoryLabel)inventoryLabel.text=string.Join("     ",Enumerable.Range(0,3).Select(i=>"["+(i+1)+"] "+(i<pocket.Items.Count?Names[pocket.Items[i]]:"vacío")));
            if(vendingLabel)vendingLabel.text=Names[Selected]+"\n● "+Prices[Selected]+" monedas\n← / → elegir · E comprar";
            if(!p||LizardLobby.Visible)return;
            float dv=Vector3.Distance(p.transform.position,vending.position),dc=Vector3.Distance(p.transform.position,colors.position),db=Vector3.Distance(p.transform.position,bell.position);
            // Cada máquina se ofrece al sistema de avisos; solo la más cercana muestra "[E] …" y responde.
            if(dv<4.4f)InteractPrompts.Offer(vending,vending.position+Vector3.up*3.6f,$"Comprar {Names[Selected]} · ● {Prices[Selected]}   (← → elegir)",dv);
            if(db<3f)InteractPrompts.Offer(bell,bell.position+Vector3.up*3.6f,"Tocar la campana",db);
            if(InteractPrompts.IsActive(vending))
            {
                var kb=Keyboard.current;if(kb!=null){if(kb.rightArrowKey.wasPressedThisFrame)Browse(1);if(kb.leftArrowKey.wasPressedThisFrame)Browse(-1);if(kb.upArrowKey.wasPressedThisFrame)Browse(-3);if(kb.downArrowKey.wasPressedThisFrame)Browse(3);}
                if(p.Input.Pressed(p.Input.Interact))Buy(p);
            }
            // La ruleta de color la gestiona ColorWheelStation (vista previa, confirmar, lamer muestras).
            else if(InteractPrompts.IsActive(bell)&&p.Input.Pressed(p.Input.Interact)){Sfx.Play(Sfx.Help,bell.position);var swing=FindAnyObjectByType<BellSwing>();if(swing)swing.Ring();Fx.Burst(bell.position,Color.yellow,10,2,.06f);p.Message("¡Ding! La tienda está abierta. Entra para probar sombreros.");}
        }
    }







}
