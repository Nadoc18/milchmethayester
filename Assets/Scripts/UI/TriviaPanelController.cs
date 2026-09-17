using System;
using UnityEngine;
using UnityEngine.UI;

namespace MNLTHII.Managers
{
    /// <summary>
    /// Le panneau de question - phase 1, le Revenu.
    ///
    /// C'est le seul moment de la partie ou il n'y a QU'UN objet a l'ecran et QU'UNE
    /// decision a prendre. Le plateau est verrouille et assombri derriere. Tout ce
    /// qui n'aide pas a repondre doit disparaitre : d'ou un voile plein cadre, une
    /// question enorme, quatre reponses, et rien d'autre.
    ///
    /// Quatre choses que la maquette impose, et qui sont ici des contraintes dures :
    ///
    ///   1. La categorie est affichee EN HEBREU. Le JSON des questions la stocke en
    ///      latin ("Halacha", "Tanakh") : passer du latin dans un champ marque
    ///      isRightToLeftText l'affiche a l'envers - c'est exactement le bug
    ///      "AHCALAH" vu en jeu. La traduction vient de hud_labels.json.
    ///
    ///   2. Le gain est annonce AVANT la reponse. Le joueur doit savoir sur quoi il
    ///      joue pendant qu'il reflechit, pas apres.
    ///
    ///   3. Un seul etat de survol, dans le cyan du joueur. Pas deux nuances, pas
    ///      d'animation : la lecture prime.
    ///
    ///   4. Les chiffres (le gain, le chrono) vivent dans des champs NON RTL. Un
    ///      nombre passe en droite-a-gauche voit ses chiffres s'inverser.
    ///
    /// Ce composant ne cree rien et ne decide rien : il affiche ce qu'on lui donne
    /// et signale l'index choisi. La regle du jeu reste dans TriviaManager.
    ///
    /// Note d'optimisation : zero allocation par frame. Les quatre delegues de
    /// bouton sont construits une fois dans Awake et jamais recrees - un
    /// AddListener(() => X(i)) dans une boucle alloue une closure par question.
    /// Le chrono n'ecrit son texte que quand la SECONDE entiere change, pas a
    /// chaque frame, et via SetText(format, valeur) qui n'alloue pas de string.
    /// </summary>
    public class TriviaPanelController : MonoBehaviour
    {
        // =================================================================
        //  REFERENCES
        // =================================================================
        [Header("Racine")]
        public GameObject panelRoot;
        public Image dimmer;

        [Header("Entete")]
        public TMPro.TextMeshProUGUI categoryText;
        public TMPro.TextMeshProUGUI rewardLabelText;
        public TMPro.TextMeshProUGUI rewardValueText;

        [Header("Question")]
        public TMPro.TextMeshProUGUI questionText;

        [Header("Reponses")]
        public Button[] answerButtons = new Button[4];
        public TMPro.TextMeshProUGUI[] answerTexts = new TMPro.TextMeshProUGUI[4];
        public Image[] answerBackgrounds = new Image[4];

        [Header("Chrono")]
        public TMPro.TextMeshProUGUI timerLabelText;
        public TMPro.TextMeshProUGUI timerValueText;
        public Image timerFill;

        // =================================================================
        //  LIBELLES ET FORMATS - remplis depuis hud_labels.json
        // =================================================================
        [Header("Libelles")]
        public string rewardLabel = "";
        public string timerLabel = "";
        public string rewardFormat = "+{0}";
        public string secondsFormat = "{0}";

        // Traduction des categories : deux tableaux paralleles plutot qu'un
        // Dictionary. Quatre entrees, une comparaison de chaines par question :
        // le hachage couterait plus cher que la boucle.
        public string[] categoryKeys = new string[0];
        public string[] categoryNames = new string[0];

        // =================================================================
        //  COULEURS
        // =================================================================
        [Header("Couleurs")]
        public Color answerIdleColor = new Color(0.49f, 0.63f, 1f, 0.06f);
        public Color answerHoverColor = new Color(0.24f, 0.88f, 0.82f, 0.16f);
        public Color answerCorrectColor = new Color(0.30f, 0.85f, 0.55f, 0.28f);
        public Color answerWrongColor = new Color(1f, 0.30f, 0.37f, 0.26f);
        public Color timerCalmColor = new Color(0.24f, 0.88f, 0.82f);
        public Color timerUrgentColor = new Color(1f, 0.30f, 0.37f);

        [Tooltip("Sous ce nombre de secondes, le chrono passe au rouge.")]
        public float urgentBelowSeconds = 3f;

        // =================================================================
        //  ETAT INTERNE
        // =================================================================
        private Action<int> _handler;

        // Delegues construits UNE fois : pas de closure allouee par question.
        private UnityEngine.Events.UnityAction[] _callbacks;

        private int _lastWholeSecond = -1;
        private bool _locked;          // une reponse a ete donnee : on n'en prend plus

        public bool IsOpen { get { return panelRoot != null && panelRoot.activeSelf; } }

        private void Awake()
        {
            BindButtons();
            Hide();
        }

        /// <summary>
        /// Abonne les quatre boutons, une fois pour toutes. La capture de l'index se
        /// fait ici, a la construction, et non a chaque affichage de question.
        /// </summary>
        private void BindButtons()
        {
            if (answerButtons == null) return;

            _callbacks = new UnityEngine.Events.UnityAction[answerButtons.Length];

            for (int i = 0; i < answerButtons.Length; i++)
            {
                if (answerButtons[i] == null) continue;

                int index = i;                          // copie locale, capturee une fois
                _callbacks[i] = delegate { Choose(index); };
                answerButtons[i].onClick.AddListener(_callbacks[i]);
            }
        }

        private void OnDestroy()
        {
            if (answerButtons == null || _callbacks == null) return;

            for (int i = 0; i < answerButtons.Length && i < _callbacks.Length; i++)
                if (answerButtons[i] != null && _callbacks[i] != null)
                    answerButtons[i].onClick.RemoveListener(_callbacks[i]);
        }

        // =================================================================
        //  API
        // =================================================================
        /// <summary>Qui recoit l'index de la reponse cliquee. TriviaManager s'y branche.</summary>
        public void SetAnswerHandler(Action<int> handler)
        {
            _handler = handler;
        }

        /// <summary>
        /// Ouvre le panneau. categoryKey est la cle latine du JSON des questions :
        /// elle est traduite ici, elle n'est jamais affichee telle quelle.
        /// </summary>
        public void Show(string categoryKey, string question, string[] answers, int reward)
        {
            _locked = false;
            _lastWholeSecond = -1;

            if (categoryText != null) categoryText.text = TranslateCategory(categoryKey);

            if (rewardLabelText != null) rewardLabelText.text = rewardLabel;
            if (rewardValueText != null) rewardValueText.SetText(rewardFormat, reward);
            if (timerLabelText != null) timerLabelText.text = timerLabel;

            if (questionText != null) questionText.text = question;

            int count = (answers != null) ? answers.Length : 0;

            for (int i = 0; i < answerButtons.Length; i++)
            {
                bool used = i < count;

                if (answerButtons[i] != null)
                {
                    answerButtons[i].gameObject.SetActive(used);
                    answerButtons[i].interactable = used;
                }

                if (used && answerTexts != null && i < answerTexts.Length && answerTexts[i] != null)
                    answerTexts[i].text = answers[i];

                if (used && answerBackgrounds != null && i < answerBackgrounds.Length
                    && answerBackgrounds[i] != null)
                    answerBackgrounds[i].color = answerIdleColor;
            }

            if (timerFill != null)
            {
                timerFill.fillAmount = 1f;
                timerFill.color = timerCalmColor;
            }

            if (panelRoot != null) panelRoot.SetActive(true);
        }

        /// <summary>
        /// Avance le chrono. Appele depuis l'Update de TriviaManager : d'ou le soin
        /// a ne rien reecrire tant que la seconde affichee n'a pas change.
        /// </summary>
        public void SetTimeRemaining(float remaining, float limit)
        {
            if (limit <= 0f) return;

            if (remaining < 0f) remaining = 0f;

            if (timerFill != null) timerFill.fillAmount = remaining / limit;

            bool urgent = remaining <= urgentBelowSeconds;
            if (timerFill != null)
                timerFill.color = urgent ? timerUrgentColor : timerCalmColor;

            int whole = Mathf.CeilToInt(remaining);
            if (whole == _lastWholeSecond) return;      // rien de neuf a ecrire
            _lastWholeSecond = whole;

            if (timerValueText != null)
            {
                timerValueText.SetText(secondsFormat, whole);
                timerValueText.color = urgent ? timerUrgentColor : timerCalmColor;
            }
        }

        /// <summary>
        /// Montre le verdict : la bonne reponse en vert, et la mauvaise en rouge si
        /// le joueur s'est trompe. On verrouille les boutons - une question ne se
        /// repond qu'une fois, meme en cliquant vite.
        /// </summary>
        public void ShowResult(int chosenIndex, int correctIndex)
        {
            _locked = true;

            for (int i = 0; i < answerButtons.Length; i++)
                if (answerButtons[i] != null) answerButtons[i].interactable = false;

            if (answerBackgrounds == null) return;

            if (correctIndex >= 0 && correctIndex < answerBackgrounds.Length
                && answerBackgrounds[correctIndex] != null)
                answerBackgrounds[correctIndex].color = answerCorrectColor;

            if (chosenIndex >= 0 && chosenIndex != correctIndex
                && chosenIndex < answerBackgrounds.Length
                && answerBackgrounds[chosenIndex] != null)
                answerBackgrounds[chosenIndex].color = answerWrongColor;
        }

        public void Hide()
        {
            _locked = false;
            if (panelRoot != null && panelRoot.activeSelf) panelRoot.SetActive(false);
        }

        // =================================================================
        //  SURVOL - appele par les relais poses sur chaque bouton
        // =================================================================
        public void SetHovered(int index, bool hovered)
        {
            if (_locked) return;
            if (answerBackgrounds == null || index < 0 || index >= answerBackgrounds.Length) return;

            Image bg = answerBackgrounds[index];
            if (bg == null) return;

            bg.color = hovered ? answerHoverColor : answerIdleColor;
        }

        // =================================================================
        //  INTERNE
        // =================================================================
        private void Choose(int index)
        {
            if (_locked) return;
            _locked = true;

            if (_handler != null) _handler(index);
        }

        /// <summary>
        /// Cle latine du JSON vers le nom hebreu a afficher. Si la cle est inconnue,
        /// on renvoie une chaine vide plutot que le latin : mieux vaut un titre
        /// absent qu'un titre imprime a l'envers.
        /// </summary>
        private string TranslateCategory(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            if (categoryKeys == null || categoryNames == null) return "";

            for (int i = 0; i < categoryKeys.Length && i < categoryNames.Length; i++)
                if (categoryKeys[i] == key) return categoryNames[i];

            return "";
        }
    }
}
