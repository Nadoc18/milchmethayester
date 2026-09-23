using UnityEngine;
using UnityEngine.UI;

namespace MNLTHII.Managers
{
    /// <summary>
    /// Fait vivre le menu principal : la camera respire lentement devant la scene 3D
    /// (le Shofar et son ennemi d'un cote, la Base et son Tank de l'autre), et les
    /// hexagones decoratifs du plateau d'interface pulsent doucement, en vague depuis
    /// le centre.
    ///
    /// Pose par Milchemet > Construire le menu principal. Tous les reglages sont dans
    /// l'inspecteur.
    ///
    /// Note d'optimisation : un seul Update, sans allocation - quelques sinus et une
    /// affectation de couleur par hexagone decoratif (une quinzaine).
    /// </summary>
    public class MainMenuDiorama : MonoBehaviour
    {
        [Header("Camera")]
        public Transform cameraRig;
        [Tooltip("Point que la camera regarde : le milieu entre les deux groupes.")]
        public Vector3 focus = new Vector3(0f, 0.5f, 0f);
        [Tooltip("Balancement gauche-droite, en degres.")]
        public float swayYaw = 4f;
        [Tooltip("Duree d'un aller-retour du balancement, en secondes.")]
        public float swayPeriod = 16f;
        [Tooltip("Petite respiration verticale, en unites monde.")]
        public float bobHeight = 0.06f;

        [Header("Hexagones decoratifs")]
        public Image[] decor = new Image[0];
        public float[] decorAlpha = new float[0];
        public Vector2[] decorPosition = new Vector2[0];
        [Tooltip("Vitesse de la vague (cycles par seconde).")]
        public float pulseSpeed = 0.35f;
        [Tooltip("Part de l'opacite qui pulse (0 = fixe, 1 = s'eteint completement).")]
        [Range(0f, 1f)] public float pulseAmount = 0.55f;
        [Tooltip("Retard de la vague par pixel de distance au centre.")]
        public float waveDelayPerPixel = 0.0025f;

        private Vector3 _offset;
        private bool _hasRig;

        private void Start()
        {
            _hasRig = cameraRig != null;
            if (_hasRig) _offset = cameraRig.position - focus;
        }

        private void Update()
        {
            float t = Time.unscaledTime;

            if (_hasRig && cameraRig != null)
            {
                float period = (swayPeriod > 0.1f) ? swayPeriod : 0.1f;
                float yaw = Mathf.Sin(t * (2f * Mathf.PI / period)) * swayYaw;

                Vector3 off = Quaternion.Euler(0f, yaw, 0f) * _offset;
                off.y += Mathf.Sin(t * 0.7f) * bobHeight;

                cameraRig.position = focus + off;
                cameraRig.LookAt(focus);
            }

            if (decor == null) return;

            int count = decor.Length;
            if (decorAlpha == null || decorAlpha.Length < count) count = (decorAlpha == null) ? 0 : decorAlpha.Length;

            for (int i = 0; i < count; i++)
            {
                Image img = decor[i];
                if (img == null) continue;

                float dist = (decorPosition != null && i < decorPosition.Length) ? decorPosition[i].magnitude : 0f;
                float phase = (t * pulseSpeed - dist * waveDelayPerPixel) * 2f * Mathf.PI;
                float wave = 0.5f + 0.5f * Mathf.Sin(phase);

                Color c = img.color;
                c.a = decorAlpha[i] * (1f - pulseAmount + pulseAmount * wave);
                img.color = c;
            }
        }
    }
}
