using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    public class Door : MonoBehaviour
    {
        public Vector3 openOffset = new Vector3(0f, 4f, 0f);
        public float speed = 3f;
        Vector3 closed; bool open;
        void Awake() => closed = transform.position;
        public void SetOpen(bool o) => open = o;
        void FixedUpdate() => transform.position = Vector3.MoveTowards(transform.position, open ? closed + openOffset : closed, speed * Time.fixedDeltaTime);
    }
}
