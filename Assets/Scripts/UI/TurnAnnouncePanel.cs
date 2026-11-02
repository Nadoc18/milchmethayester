using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MNLTHII.Managers
{
    /// <summary>
    /// L'ouverture d'un tour.
    ///
    /// A quoi sert un ecran qui ne demande rien au joueur : a decouper le temps.
    /// Sans lui, la partie est un flux continu ou le joueur ne sait plus s'il vient
    /// de jouer ou s'il regarde le Yetzer Hara jouer. Un tour doit COMMENCER quelque
    /// part, visiblement, et c'est ici. C'est aussi le seul endroit ou le revenu
    /// passif - la Base et les Cristaux - est annonce comme un evenement plutot que
    /// de faire monter un compteur en silence dans un coin de l'ecran.
    ///
    /// Il est bref par construction : une seconde et demie, pas d'interaction, pas de
    /// bouton a cliquer. Un ecran d'ouverture qu'il faut congedier devient une corvee
    /// des le troisieme tour.
    ///
    /// Tous les libelles hebreux viennent de l'inspecteur, donc de hud_labels.json.
    /// Ce fichier .cs reste en pur ASCII.
    ///
    /// Note d'optimisation : une seule coroutine, et elle ne tourne que pendant
    /// l'animation. Les fondus passent par un CanvasGroup - une seule valeur alpha
    /// pour tout le panneau - et non par la couleur de chaque texte, ce qui forcerait
    /// la reconstruction du maillage de chaque TextMeshPro a chaque frame.
    /// WaitForSecondsRealtime et non WaitForSeconds : si le jeu se met en pause,
    /// l'ouverture ne doit pas rester figee a l'ecran pour toujours.
    /// </summary>
    public class TurnAnnouncePanel : MonoBehaviour
    {
        public static TurnAnnouncePanel Instance;

        // =================================================================
        //  REFERENCES
        // =================================================================
        [Header("Racine")]
        public GameObject panelRoot;
        public CanvasGroup group;

        [Header("Textes")]
        public TMPro.TextMeshProUGUI turnLabelText;     // le mot "tour"
        public TMPro.TextMeshProUGUI turnNumberText;    // le numero, en grand
        public TMPro.TextMeshProUGUI incomeLabelText;   // le mot "revenu"
        public TMPro.TextMeshProUGUI incomeValueText;   // le montant verse

        [Header("Decor")]
        public Image ruleTop;
        public Image ruleBottom;

        [Tooltip("Le fond plein ecran. Opaque a l'ouverture de la partie (il cache le plateau qui se construit), translucide aux tours suivants.")]
        public Image curtain;
        [Tooltip("Les textes seuls, pour les faire apparaitre sur un rideau deja opaque.")]
        public CanvasGroup content;

        [Range(0f, 1f)] public float veilAlpha = 0.78f;

        /// <summary>
        /// L'OUVERTURE DE LA PARTIE. Avant, on voyait le plateau se construire, PUIS
        /// l'annonce du tour 1 venait par-dessus : l'ordre etait a l'envers. Maintenant
        /// LocalGameEngine leve ce drapeau avant que la scene ne s'affiche ; le panneau
        /// se reveille rideau ferme, annonce le tour, et le rideau se leve sur un
        /// plateau deja pret. Les deux secondes et demie de construction du plateau
        /// passent derriere le rideau au lieu de s'etaler sous les yeux du joueur.
        /// </summary>
        public static bool OpeningPending;

        /// <summary>L'ouverture vient d'annoncer ce tour : StartTurn ne le refait pas.</summary>
        private static bool _skipNext;

        // =================================================================
        //  LIBELLES
        // =================================================================
        [Header("Libelles")]
        public string turnLabel = "";
        public string incomeLabel = "";
        public string numberFormat = "{0}";
        public string incomeFormat = "+{0}";

        // =================================================================
        //  RYTHME
        // =================================================================
        // OBSOLETES : voir PhasePace.TurnAnnounce*. L'annonce d'ouverture suit
        // l'allure choisie comme le reste, mais elle n'est PAS sautable : elle
        // n'appartient pas a une phase automatique, c'est le debut du tour du joueur.
        [Header("Rythme (OBSOLETE - voir PhasePace)")]
        public float fadeInDuration = 0.28f;
        public float holdDuration = 0.95f;
        public float fadeOutDuration = 0.32f;

        private readonly MNLTHII.Managers.PaceWait _pace = new MNLTHII.Managers.PaceWait();

        private void Awake()
        {
            if (Instance == null) Instance = this;

            if (panelRoot == null)
                Debug.LogWarning("[TurnAnnounce] panelRoot n'est pas renseigne : l'ouverture ne s'affichera pas.");

            if (group == null && panelRoot != null) group = panelRoot.GetComponent<CanvasGroup>();

            if (OpeningPending) ShowCurtain();
            else Hide();
        }

        /// <summary>Rideau ferme, textes invisibles : le plateau derriere ne se voit pas.</summary>
        private void ShowCurtain()
        {
            if (panelRoot == null) return;
            panelRoot.SetActive(true);
            if (group != null) group.alpha = 1f;
            if (curtain != null) curtain.color = new Color(curtain.color.r, curtain.color.g, curtain.color.b, 1f);
            if (content != null) content.alpha = 0f;
        }

        /// <summary>
        /// L'annonce d'ouverture : sur le rideau opaque, le numero du tour apparait,
        /// tient, puis tout se leve ensemble sur le plateau. Pas de revenu ici : il
        /// n'a pas encore ete verse.
        /// </summary>
        public IEnumerator PlayOpening(int turn)
        {
            OpeningPending = false;
            if (panelRoot == null) yield break;

            ShowCurtain();

            if (turnLabelText != null) turnLabelText.text = turnLabel;
            if (turnNumberText != null) turnNumberText.SetText(numberFormat, turn);
            if (incomeLabelText != null) incomeLabelText.gameObject.SetActive(false);
            if (incomeValueText != null) incomeValueText.gameObject.SetActive(false);

            // Les textes montent sur le rideau...
            yield return FadeGroup(content, 0f, 1f, MNLTHII.Managers.PhasePace.Seconds(MNLTHII.Managers.PhasePace.TurnAnnounceFadeIn));
            yield return _pace.For(MNLTHII.Managers.PhasePace.TurnAnnounceHold);
            // ...puis le rideau se leve, textes compris.
            yield return FadeGroup(group, 1f, 0f, MNLTHII.Managers.PhasePace.Seconds(MNLTHII.Managers.PhasePace.TurnAnnounceFadeOut) * 1.6f);

            _skipNext = true;
            Hide();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Joue l'ouverture et ne rend la main qu'a la fin. TurnManager l'attend avec
        /// un yield return : le tour ne commence vraiment qu'une fois l'ecran parti.
        /// </summary>
        public IEnumerator Play(int turn, int passiveIncome)
        {
            if (panelRoot == null) yield break;

            // L'ouverture vient de l'annoncer : on ne le dit pas deux fois.
            if (_skipNext) { _skipNext = false; yield break; }

            // Aux tours suivants le rideau est un voile : le plateau reste visible
            // derriere, assombri.
            if (curtain != null) curtain.color = new Color(curtain.color.r, curtain.color.g, curtain.color.b, veilAlpha);
            if (content != null) content.alpha = 1f;

            if (turnLabelText != null) turnLabelText.text = turnLabel;
            if (turnNumberText != null) turnNumberText.SetText(numberFormat, turn);

            bool hasIncome = passiveIncome > 0;

            if (incomeLabelText != null)
            {
                incomeLabelText.text = incomeLabel;
                incomeLabelText.gameObject.SetActive(hasIncome);
            }

            if (incomeValueText != null)
            {
                incomeValueText.SetText(incomeFormat, passiveIncome);
                incomeValueText.gameObject.SetActive(hasIncome);
            }

            panelRoot.SetActive(true);

            yield return Fade(0f, 1f, MNLTHII.Managers.PhasePace.Seconds(MNLTHII.Managers.PhasePace.TurnAnnounceFadeIn));
            yield return _pace.For(MNLTHII.Managers.PhasePace.TurnAnnounceHold);
            yield return Fade(1f, 0f, MNLTHII.Managers.PhasePace.Seconds(MNLTHII.Managers.PhasePace.TurnAnnounceFadeOut));

            Hide();
        }

        /// <summary>
        /// Fondu sur le CanvasGroup. Temps NON mis a l'echelle : une ouverture de tour
        /// doit avoir la meme duree quel que soit l'etat de Time.timeScale.
        /// </summary>
        private IEnumerator Fade(float from, float to, float duration)
        {
            yield return FadeGroup(group, from, to, duration);
        }

        private IEnumerator FadeGroup(CanvasGroup target, float from, float to, float duration)
        {
            if (target == null) yield break;

            if (duration <= 0f) { target.alpha = to; yield break; }

            float elapsed = 0f;
            target.alpha = from;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;

                float t = elapsed / duration;
                if (t > 1f) t = 1f;

                // Lissage en cosinus : une interpolation lineaire sur un fondu se voit,
                // elle demarre et s'arrete sec.
                t = t * t * (3f - 2f * t);

                target.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }

            target.alpha = to;
        }

        public void Hide()
        {
            if (group != null) group.alpha = 0f;
            if (panelRoot != null && panelRoot.activeSelf) panelRoot.SetActive(false);
        }
    }
}
