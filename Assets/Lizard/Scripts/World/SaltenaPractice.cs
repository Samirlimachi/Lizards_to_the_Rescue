using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Lizards
{
    public sealed class SaltenaPractice : MonoBehaviour
    {
        public LizardPlayer helperPrefab;
        public LizardLobby room;
        LizardPlayer helper;
        FoodItem target;
        float attachAt;
        bool attached;
        void Update()
        {
            var keyboard=Keyboard.current;if(keyboard==null)return;
            if(keyboard.f2Key.wasPressedThisFrame&&room)room.OpenRoomMenu();
            if(keyboard.f5Key.wasPressedThisFrame&&!SharedFoodSync.Online)SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            if(keyboard.f4Key.wasPressedThisFrame&&!SharedFoodSync.Online)ToggleHelper();
            if(helper&&helper.gameObject.activeInHierarchy&&target&&!attached&&Time.time>=attachAt)
            {
                helper.Motor.enabled=false;helper.Body.constraints=RigidbodyConstraints.FreezeAll;
                attached=helper.Tongue.DebugAttach(target.GetComponent<Collider>());
            }
        }
        public void ToggleHelper()
        {
            if(helper){Destroy(helper.gameObject);helper=null;return;}
            var p=LizardPlayer.All.FirstOrDefault(p=>p&&p.IsControlled);if(!p||!helperPrefab)return;
            var food=FoodItem.All.Where(f=>f&&f.SharedPullAllowed).OrderBy(f=>Vector3.Distance(f.transform.position,p.transform.position)).FirstOrDefault();if(!food)return;
            Vector3 away=Vector3.ProjectOnPlane(food.transform.position-p.transform.position,Vector3.up).normalized;if(away.sqrMagnitude<.1f)away=Vector3.forward;
            Vector3 position=food.transform.position+away*2.4f;position.y=.05f;
            helper=Instantiate(helperPrefab,position,Quaternion.LookRotation(-away));helper.Input.controlled=false;helper.displayName="Ayudante de prueba";helper.startFood=0;
            target=food;attachAt=Time.time+.2f;attached=false;
            p.Message("El ayudante tira del otro lado. Sujeta la salteña y aléjate para partirla.");
        }
    }
}
