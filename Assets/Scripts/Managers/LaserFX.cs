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

                // Shader.Find ne trouve, dans un build, que les shaders effectivement
                // inclus : un shader "Legacy" absent renvoie null, et un materiau sans
                // shader se voit en magenta. On passe donc par le shader de Resources
                // deja utilise par les traits de tir, avec un repli integre.
                Shader shader = Resources.Load<Shader>("Shaders/MNLTH_GroundDecal");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Unlit/Color");

                if (shader != null)
                {
                    _generated = new Material(shader) { name = "Laser" };
                    lineRenderer.material = _generated;
                }
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
        
        private Material _generated;

        private void OnDestroy()
        {
            if (_generated != null) { Destroy(_generated); _generated = null; }
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