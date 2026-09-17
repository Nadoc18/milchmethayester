using UnityEngine;

namespace MNLTHII.Managers
{
    public class FocusBouncer : MonoBehaviour
    {
        public float bounceSpeed = 2f;
        public float bounceHeight = 0.5f;

        private void Update()
        {
            if (transform.parent != null) {
                float bounceY = 5f + Mathf.Sin(Time.time * bounceSpeed) * bounceHeight;
                transform.localPosition = new Vector3(0, bounceY, 0);
            }
        }
    }
}