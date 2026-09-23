using UnityEngine;
using UnityEngine.EventSystems;

namespace MNLTHII.Managers
{
    /// <summary>
    /// Un hexagone-bouton du menu principal qui grossit un peu au survol.
    ///
    /// Note d'optimisation : le composant ne tourne (Update) que pendant la petite
    /// animation d'agrandissement ou de retour, puis se desactive.
    /// </summary>
    public class MainMenuHexButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public float hoverScale = 1.08f;
        [Tooltip("Vitesse de l'animation (plus grand = plus rapide).")]
        public float speed = 12f;

        private float _target = 1f;
        private Transform _t;

        private void Awake()
        {
            _t = transform;
            enabled = false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _target = hoverScale;
            enabled = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _target = 1f;
            enabled = true;
        }

        private void OnDisable()
        {
            // Rien a faire : l'echelle reste ou elle est, et OnEnable repart de la.
        }

        private void Update()
        {
            float s = _t.localScale.x;
            s = Mathf.Lerp(s, _target, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));

            if (Mathf.Abs(s - _target) < 0.001f)
            {
                s = _target;
                enabled = false;
            }

            _t.localScale = new Vector3(s, s, 1f);
        }
    }
}
