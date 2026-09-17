using UnityEngine;
using System.Collections;

namespace MNLTHII.Managers
{
    public class LaserFX : MonoBehaviour
    {
        public LineRenderer lineRenderer;
        
        public void ShootLaser(Vector3 start, Vector3 end, Color color, float duration)
        {
            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
                lineRenderer.material = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
            }
            
            lineRenderer.startWidth = 0.5f;
            lineRenderer.endWidth = 0.5f;
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
            
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            
            StartCoroutine(FadeOut(duration));
        }
        
        private IEnumerator FadeOut(float duration)
        {
            float elapsed = 0f;
            Color startColor = lineRenderer.startColor;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
                lineRenderer.startColor = new Color(startColor.r, startColor.g, startColor.b, alpha);
                lineRenderer.endColor = new Color(startColor.r, startColor.g, startColor.b, alpha);
                yield return null;
            }
            
            Destroy(gameObject);
        }
    }
}