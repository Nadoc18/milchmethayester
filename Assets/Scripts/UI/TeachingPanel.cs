using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MNLTHII.Managers
{
    /// <summary>
    /// L'ECRAN D'OUVERTURE DU TOUR : ce qu'on apprend avant de jouer.
    ///
    /// Deux enseignements courts, chacun avec sa source, puis parfois une question.
    /// Rien d'autre : l'ecran des consignes de jeu a ete retire. Il alourdissait
    /// l'ouverture du tour, et la seule partie qui servait vraiment - la legende des
    /// pictogrammes - vit desormais sur le bandeau permanent, ou on peut la consulter
    /// en jouant.
    /// L'ecran dit franchement pourquoi il est la : "apprends bien, tu en auras besoin".
    /// Cette phrase n'est pas une formule - elle est vraie, et c'est ce qui la rend
    /// supportable tour apres tour. Une bonne reponse fissure un Shofar, donc ce qu'on
    /// vient de lire sert vraiment a gagner.
    ///
    /// CE QU'IL REMPLACE
    ///
    /// La question de Trivia demandait de payer un peage avant d'avoir le droit de
    /// jouer. Celui-ci donne d'abord, et ne demande qu'ensuite - et seulement parfois.
    ///
    /// Note d'optimisation : un seul panneau, reutilise. Aucune allocation par tour en
    /// dehors des chaines deja presentes dans le fichier JSON ; les boutons de reponse
    /// sont construits une fois par le builder et recycles.
    /// </summary>
    public class TeachingPanel : MonoBehaviour
    {
        public static TeachingPanel Instance;

        /// <summary>Trois reponses au maximum : au-dela ce n'est plus une question, c'est une liste.</summary>
        public const int MaxAnswers = 3;

        // =================================================================
        //  REFERENCES
        // =================================================================
        [Header("Racine")]
        public GameObject panelRoot;
        public CanvasGroup group;

        [Header("Entete")]
        public TMPro.TextMeshProUGUI headerText;
        public TMPro.TextMeshProUGUI taglineText;

        [Header("L'enseignement")]
        public GameObject teachingRoot;
        public TMPro.TextMeshProUGUI sourceText;
        public TMPro.TextMeshProUGUI titleText;
        public TMPro.TextMeshProUGUI bodyText;
        public TMPro.TextMeshProUGUI counterText;
        public Button nextButton;
        public TMPro.TextMeshProUGUI nextText;

        [Header("La question")]
        public GameObject questionRoot;
        public TMPro.TextMeshProUGUI questionTitleText;
        public TMPro.TextMeshProUGUI situationText;
        public Button[] answerButtons = new Button[MaxAnswers];
        public Image[] answerBackgrounds = new Image[MaxAnswers];
        public TMPro.TextMeshProUGUI[] answerTexts = new TMPro.TextMeshProUGUI[MaxAnswers];

        [Header("Le resultat")]
        public GameObject resultRoot;
        public TMPro.TextMeshProUGUI resultTitleText;
        public TMPro.TextMeshProUGUI resultBodyText;

        // =================================================================
        //  REGLAGES
        // =================================================================
        [Header("Rythme")]
        public float fadeDuration = 0.3f;

        /// <summary>Temps de lecture minimal avant que "suivant" reponde.</summary>
        public float minReadSeconds = 0.8f;

        /// <summary>Combien de temps le resultat reste a l'ecran.</summary>
        public float resultHoldSeconds = 2.2f;

        [Header("Couleurs")]
        public Color idleColor = new Color(0.49f, 0.63f, 1f, 0.07f);
        public Color hoverColor = new Color(0.49f, 0.63f, 1f, 0.18f);
        public Color correctColor = new Color(0.36f, 0.92f, 0.56f);
        public Color wrongColor = new Color(1f, 0.36f, 0.42f);
        public Color neutralColor = new Color(0.78f, 0.84f, 1f);

        // =================================================================
        //  ETAT INTERNE
        // =================================================================
        private readonly Teaching[] _turn = new Teaching[4];
        private int _count;
        private int _shown;
        private int _questionIndex = -1;


        private bool _waitingNext;
        private int _answer = -2;              // -2 : rien encore ; -1 : annule
        private float _readyAt;

        private UnityEngine.Events.UnityAction[] _answerCallbacks;
        private UnityEngine.Events.UnityAction _nextCallback;

        private void Awake()
        {
            if (Instance == null) Instance = this;

            BindButtons();
            Hide();
        }

        private void BindButtons()
        {
            if (answerButtons != null)
            {
                _answerCallbacks = new UnityEngine.Events.UnityAction[answerButtons.Length];

                for (int i = 0; i < answerButtons.Length; i++)
                {
                    if (answerButtons[i] == null) continue;

                    int slot = i;
                    _answerCallbacks[i] = delegate { _answer = slot; };
                    answerButtons[i].onClick.AddListener(_answerCallbacks[i]);
                }
            }

            if (nextButton != null)
            {
                _nextCallback = delegate { _waitingNext = false; };
                nextButton.onClick.AddListener(_nextCallback);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            if (answerButtons != null && _answerCallbacks != null)
            {
                for (int i = 0; i < answerButtons.Length && i < _answerCallbacks.Length; i++)
                    if (answerButtons[i] != null && _answerCallbacks[i] != null)
                        answerButtons[i].onClick.RemoveListener(_answerCallbacks[i]);
            }

            if (nextButton != null && _nextCallback != null)
                nextButton.onClick.RemoveListener(_nextCallback);
        }

        public void Hide()
        {
            if (group != null) group.alpha = 0f;
            if (panelRoot != null && panelRoot.activeSelf) panelRoot.SetActive(false);
        }

        /// <summary>Survol d'une reponse : appele par les relais poses sur chaque carte.</summary>
        public void SetHovered(int slot, bool hovered)
        {
            if (answerBackgrounds == null || slot < 0 || slot >= answerBackgrounds.Length) return;

            Image bg = answerBackgrounds[slot];
            if (bg == null) return;

            bg.color = hovered ? hoverColor : idleColor;
        }

        // =================================================================
        //  LA SEQUENCE
        // =================================================================
        /// <summary>
        /// Joue l'ouverture du tour. La coroutine ne rend la main qu'une fois tout lu -
        /// TurnManager l'attend, puis ouvre la phase de depense.
        /// </summary>
        public IEnumerator Play(int turn)
        {
            TeachingManager source = TeachingManager.Instance;
            if (source == null) yield break;

            _count = source.DrawTurn(_turn, turn, out _questionIndex);
            if (_count <= 0) yield break;

            if (panelRoot != null) panelRoot.SetActive(true);

            if (headerText != null) headerText.text = source.Label("header");
            if (taglineText != null) taglineText.text = source.Label("tagline");
            if (nextText != null) nextText.text = source.Label("next");
            if (questionTitleText != null) questionTitleText.text = source.Label("questionTitle");

            yield return StartCoroutine(Fade(0f, 1f));

            // --- les enseignements, un par un ---
            for (_shown = 0; _shown < _count; _shown++)
            {
                ShowTeaching(_turn[_shown], _shown, _count);

                _waitingNext = true;
                _readyAt = Time.unscaledTime + minReadSeconds;

                // Le bouton ne repond pas tout de suite : sans ce delai, un joueur qui
                // enchaine les clics saute l'enseignement sans l'avoir vu paraitre.
                while (_waitingNext || Time.unscaledTime < _readyAt)
                {
                    if (Time.unscaledTime >= _readyAt && !_waitingNext) break;
                    yield return null;
                }
            }

            // --- la question, s'il y en a une ---
            if (_questionIndex >= 0 && _questionIndex < _count)
                yield return StartCoroutine(AskQuestion(source, _turn[_questionIndex]));

            yield return StartCoroutine(Fade(1f, 0f));
            Hide();
        }

        private void ShowTeaching(Teaching teaching, int index, int total)
        {
            if (teachingRoot != null) teachingRoot.SetActive(true);
            if (questionRoot != null) questionRoot.SetActive(false);
            if (resultRoot != null) resultRoot.SetActive(false);

            if (teaching == null) return;

            if (sourceText != null) sourceText.text = teaching.source;
            if (titleText != null) titleText.text = teaching.title;
            if (bodyText != null) bodyText.text = teaching.text;

            // Un nombre dans un champ RTL voit ses chiffres s'inverser : le compteur est
            // construit a part, en non-RTL, par le builder.
            if (counterText != null) counterText.SetText("{0}/{1}", index + 1, total);
        }

        private IEnumerator AskQuestion(TeachingManager source, Teaching teaching)
        {
            TeachingQuestion question = teaching.question;
            if (question == null || !question.IsValid()) yield break;

            if (teachingRoot != null) teachingRoot.SetActive(false);
            if (resultRoot != null) resultRoot.SetActive(false);
            if (questionRoot != null) questionRoot.SetActive(true);

            if (situationText != null) situationText.text = question.situation;

            int shown = 0;
            if (answerButtons != null)
            {
                for (int i = 0; i < answerButtons.Length; i++)
                {
                    bool visible = (question.answers != null && i < question.answers.Length);

                    if (answerButtons[i] != null) answerButtons[i].gameObject.SetActive(visible);
                    if (!visible) continue;

                    if (answerTexts != null && i < answerTexts.Length && answerTexts[i] != null)
                        answerTexts[i].text = question.answers[i];

                    if (answerBackgrounds != null && i < answerBackgrounds.Length && answerBackgrounds[i] != null)
                        answerBackgrounds[i].color = idleColor;

                    shown++;
                }
            }

            if (shown == 0) yield break;

            _answer = -2;
            while (_answer == -2) yield return null;

            bool right = (_answer == question.correct);

            // On marque la bonne reponse dans tous les cas : ce qui compte ici est
            // d'apprendre, pas de sanctionner.
            if (answerBackgrounds != null && question.correct < answerBackgrounds.Length
                && answerBackgrounds[question.correct] != null)
            {
                Color mark = correctColor;
                mark.a = 0.28f;
                answerBackgrounds[question.correct].color = mark;
            }

            if (!right && _answer >= 0 && answerBackgrounds != null
                && _answer < answerBackgrounds.Length && answerBackgrounds[_answer] != null)
            {
                Color mark = wrongColor;
                mark.a = 0.28f;
                answerBackgrounds[_answer].color = mark;
            }

            yield return new WaitForSecondsRealtime(0.55f);

            ShowResult(source, question, right);

            yield return new WaitForSecondsRealtime(resultHoldSeconds);
        }

        private void ShowResult(TeachingManager source, TeachingQuestion question, bool right)
        {
            if (questionRoot != null) questionRoot.SetActive(false);
            if (resultRoot != null) resultRoot.SetActive(true);

            Hexagon cracked = right ? source.GrantCrack() : null;

            if (resultTitleText != null)
            {
                resultTitleText.text = right
                    ? (cracked != null ? source.Label("rewardTitle") : source.Label("correctTitle"))
                    : source.Label("wrongTitle");

                resultTitleText.color = right ? correctColor : wrongColor;
            }

            if (resultBodyText == null) return;

            // L'explication passe AVANT la recompense quand la reponse est fausse :
            // c'est le seul moment ou le joueur veut savoir pourquoi.
            if (!right)
            {
                resultBodyText.text = question.explain;
                resultBodyText.color = neutralColor;
                return;
            }

            resultBodyText.text = (cracked != null)
                                  ? source.Label("rewardBody")
                                  : source.Label("noRewardBody");
            resultBodyText.color = neutralColor;
        }

        private IEnumerator Fade(float from, float to)
        {
            if (group == null) yield break;

            float duration = (fadeDuration > 0.02f) ? fadeDuration : 0.02f;
            float elapsed = 0f;

            group.alpha = from;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            group.alpha = to;
        }
    }
}
