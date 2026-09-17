using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RocketController : MonoBehaviour
{


    public Transform target;
    // Start is called before the first frame update
    public Vector3 velocity = Vector3.zero;
    Rigidbody rb;
    private float eulerYVelocity = 0.0f;
    private float y = 0.0f;
    void Start()
    {
     //   rb = this.gameObject.GetComponent<Rigidbody>(); 
      //  target = transform;
        
    }

    // Update is called once per frame
    void Update()
    {if (target != null)
        {
            //Vector3 targetDirection = target.position - transform.position;

            //// The step size is equal to speed times frame time.
            //float singleStep = 1.0f * Time.deltaTime;

            //// Rotate the forward vector towards the target direction by one step
            //Vector3 newDirection = Vector3.RotateTowards(-Vector3.left, targetDirection, singleStep, 0.0f);

            //transform.rotation = Quaternion.LookRotation(newDirection);

            Vector3 relativePos = target.position - transform.position;
           

            var targetAngles = Vector3.zero;
            if (Mathf.Abs(relativePos.x) > 0.01f || Mathf.Abs(relativePos.z) > 0.01f)
            {

                Quaternion rotation = Quaternion.LookRotation(new Vector2(relativePos.x, relativePos.z), Vector3.up);
                Debug.DrawLine(transform.position, relativePos, Color.green);

                var currentAngles = transform.rotation.eulerAngles;
                targetAngles = rotation.eulerAngles;
                y = Mathf.SmoothDampAngle(currentAngles.y, targetAngles.y, ref eulerYVelocity, 0.1f);
                transform.rotation = Quaternion.Euler(transform.rotation.x, y, transform.rotation.z);

                //  transform.rotation = Quaternion.Euler(rotation.x, rotation.y, rotation.z);
            }
        }
    }
    public void Shoot()
    {
     
        this.gameObject.GetComponent<Rigidbody>().AddForce( 40f * (new Vector3(target.position.x, transform.position.y, target.position.z) - transform.position));

        Destroy(this.gameObject, 0.8f);
    }
   
}
