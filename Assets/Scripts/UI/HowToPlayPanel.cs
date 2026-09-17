using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MNLTHII.Managers
{
    /// <summary>
    /// COMMENT ON JOUE - a la demande.
    ///
    /// Les consignes du jeu et la legende des pictogrammes, derriere un bouton du
    /// bandeau. On l'ouvre quand on en a besoin, et le reste du temps il n'existe pas.
    ///
    /// POURQUOI CETTE FORME-LA, APRES DEUX ESSAIS RATES
    ///
    /// Premier essai : les consignes passaient a chaque ouverture de tour, apres
    /// l'enseignement. Elles alourdissaient un moment qui doit rester court, et un
    /// joueur qui les connait deja les subissait.
    ///
    /// Deuxieme essai : la legende posee en permanence sur le bandeau. Trop petite pour
    /// etre lue, et assez grande pour encombrer - le pire des deux.
    ///
    /// Troisieme : un bouton. Le joueur decide quand il veut de l'aide, donc l'aide peut
    /// enfin etre COMPLETE et GRANDE, puisqu'elle ne vole de place a personne. C'est la
    /// seule forme ou les trois contraintes tiennent ensemble.
    ///
    /// Note d'optimisation : rempli une seule fois, au premier affichage. Ouvrir et
    /// fermer ensuite ne fait qu'activer un objet.
    /// </summary>
    public class HowToPlayPanel : MonoBehaviour
    {
        public static HowToPlayPanel Instance;

        /// <summary>Cinq consignes : la boucle entiere du jeu tient en cinq lignes.</summary>
        public const int MaxSteps = 5;

        // =================================================================
        //  REFERENCES
        // =================================================================
        [Header("Racine")]
        public GameObject panelRoot;
        public CanvasGroup group;

        [Header("Entete")]
        public TMPro.TextMeshProUGUI headerText;
        public TMPro.TextMeshProUGUI taglineText;

        [Header("Les consignes")]
        public GameObject[] stepRoots = new GameObject[MaxSteps];
        public Image[] stepIcons = new Image[MaxSteps];
        public TMPro.TextMeshProUGUI[] stepTitles = new TMPro.TextMeshProUGUI[MaxSteps];
        public TMPro.TextMeshProUGUI[] stepBodies = new TMPro.TextMeshProUGUI[MaxSteps];

        [Header("La legende")]
        [Tooltip("Le bloc de pictogrammes. Il se remplit tout seul.")]
        public HudLegend legend;

        [Header("Ouvrir")]
        /// <summary>
        /// Le bouton du bandeau. Il est cable ICI, au demarrage - et surtout PAS dans
        /// le builder.
        ///
        /// Un onClick.AddListener pose depuis un script d'editeur cree un abonnement
        /// NON PERSISTANT : Unity ne l'enregistre pas dans la scene, et il a disparu
        /// des qu'on appuie sur Play. Le bouton etait donc construit, visible, et
        /// parfaitement muet. C'est exactement ce qui vient d'arriver.
        ///
        /// Le builder se contente maintenant de renseigner ce champ, et l'abonnement
        /// est refait a chaque lancement - comme pour tous les autres boutons du projet.
        /// </summary>
        public Button openButton;

        [Header("Fermer")]
        public Button closeButton;
        public TMPro.TextMeshProUGUI closeText;

        // =================================================================
        //  REGLAGES
        // =================================================================
        [Header("Couleurs des consignes")]
        /// <summary>
        /// Chaque consigne porte la couleur de ce dont elle parle, et ce sont les MEMES
        /// que sur le plateau et dans le reste de l'interface : le joueur relie la ligne
        /// a ce qu'il verra en jouant, sans qu'on le lui dise.
        /// </summary>
        public Color colorGas = new Color(0.42f, 0.92f, 0.62f);
        public Color colorTank = new Color(0.37f, 0.66f, 1f);
        public Color colorDanger = new Color(1f, 0.36f, 0.42f);
        public Color colorCrack = new Color(1f, 0.66f, 0.30f);
        public Color colorAttack = new Color(0.30f, 0.94f, 0.86f);

        public float fadeDuration = 0.18f;

        // =================================================================
        //  ETAT
        // =================================================================
        private readonly GameInstruction[] _steps = new GameInstruction[MaxSteps];
        private bool _filled;
        private Coroutine _fade;

        public bool IsOpen { get { return panelRoot != null && panelRoot.activeSelf; } }

        private void Awake()
        {
            if (Instance == null) Instance = this;

            if (openButton != null) openButton.onClick.AddListener(Toggle);
            if (closeButton != null) closeButton.onClick.AddListener(Hide);

            Hide();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            if (openButton != null) openButton.onClick.RemoveListener(Toggle);
            if (closeButton != null) closeButton.onClick.RemoveListener(Hide);
        }

        // =================================================================
        //  API
        // =================================================================
        /// <summary>Le bouton du bandeau appelle ceci : ouvre, ou referme si c'est ouvert.</summary>
        public void Toggle()
        {
            if (IsOpen) Hide();
            else Show();
        }

        public void Show()
        {
            Fill();

            if (panelRoot != null) panelRoot.SetActive(true);

            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(Fade(0f, 1f));
        }

        public void Hide()
        {
            if (_fade != null) { StopCoroutine(_fade); _fade = null; }

            if (group != null) group.alpha = 0f;
            if (panelRoot != null && panelRoot.activeSelf) panelRoot.SetActive(false);
        }

        // =================================================================
        //  REMPLISSAGE
        // =================================================================
        /// <summary>
        /// Une seule fois. Le contenu ne change jamais en cours de partie : le refaire
        /// a chaque ouverture serait une vingtaine d'affectations de texte pour rien.
        /// </summary>
        private void Fill()
        {
            if (_filled) return;

            TeachingManager source = TeachingManager.Instance;
            if (source == null) return;

            _filled = true;

            if (headerText != null) headerText.text = source.Label("instructionsHeader");
            if (taglineText != null) taglineText.text = source.Label("instructionsTagline");
            if (closeText != null) closeText.text = source.Label("close");

            // turn = 1 : le briefing complet, c'est-a-dire toutes les consignes. Ici on
            // les veut TOUTES, toujours - c'est une page d'aide, pas un tutoriel qui
            // dose ce qu'il montre.
            int count = source.DrawInstructions(_steps, 1);

            for (int i = 0; i < MaxSteps; i++)
            {
                bool visible = (i < count && _steps[i] != null);

                if (stepRoots != null && i < stepRoots.Length && stepRoots[i] != null)
                    stepRoots[i].SetActive(visible);

                if (!visible) continue;

                Color tint = ColorFor(_steps[i].color);

                if (stepIcons != null && i < stepIcons.Length && stepIcons[i] != null)
                    stepIcons[i].color = tint;

                if (stepTitles != null && i < stepTitles.Length && stepTitles[i] != null)
                {
                    stepTitles[i].text = _steps[i].title;
                    stepTitles[i].color = tint;
                }

                if (stepBodies != null && i < stepBodies.Length && stepBodies[i] != null)
                    stepBodies[i].text = _steps[i].text;
            }

            if (legend != null) legend.Fill();
        }

        private Color ColorFor(string key)
        {
            if (string.IsNullOrEmpty(key)) return Color.white;

            switch (key)
            {
                case "gas": return colorGas;
                case "tank": return colorTank;
                case "danger": return colorDanger;
                case "crack": return colorCrack;
                case "attack": return colorAttack;
            }

            return Color.white;
        }

        private IEnumerator Fade(float from, float to)
        {
            if (group == null) yield break;

            float duration = (fadeDuration > 0.02f) ? fadeDuration : 0.02f;
            float elapsed = 0f;

            group.alpha = from;

            while (elapsed < duration)
            {
                // Temps NON mis a l'echelle : cette page peut s'ouvrir alors que le jeu
                // est en pause, et elle doit repondre quand meme.
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            group.alpha = to;
            _fade = null;
        }
    }
}
