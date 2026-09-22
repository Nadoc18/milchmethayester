using UnityEngine;
using UnityEngine.UI;
using MNLTHII.Rules;

namespace MNLTHII.Managers
{
    /// <summary>
    /// LE MENU PRINCIPAL : choisir la difficulte, lancer une nouvelle partie.
    ///
    /// La scene est construite par Milchemet > Construire le menu principal, qui pose
    /// l'interface, glisse les references ici, et ajoute la scene en premier dans la
    /// liste du build. Ce composant ne fait que reagir aux clics :
    ///
    ///   - une des trois cartes (Facile / Moyen / Difficile) la selectionne ;
    ///   - "Nouvelle partie" enregistre le choix (GameDifficulty) et charge la scene
    ///     de jeu.
    ///
    /// Les boutons sont cables dans Awake, jamais par le builder : un abonnement pose
    /// depuis un script d'editeur n'est pas enregistre dans la scene.
    ///
    /// Les libelles hebreux arrivent de hud_labels.json (section mainMenu) : poses par
    /// le builder, ou relus au lancement s'ils sont vides.
    ///
    /// Note d'optimisation : aucun Update. Tout se passe dans les rappels de clic.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        /// <summary>Nom de la scene du menu, tel qu'enregistre par le builder.</summary>
        public const string MenuSceneName = "MainMenu";

        [Header("Scene de jeu")]
        public string gameSceneName = "Game";

        [Header("Textes")]
        public TMPro.TextMeshProUGUI titleText;
        public TMPro.TextMeshProUGUI subtitleText;

        [Header("Cartes de difficulte (Facile, Moyen, Difficile)")]
        public Button[] difficultyButtons = new Button[3];
        public Image[] difficultyBackgrounds = new Image[3];
        public UnityEngine.UI.Outline[] difficultyOutlines = new UnityEngine.UI.Outline[3];
        public TMPro.TextMeshProUGUI[] difficultyNames = new TMPro.TextMeshProUGUI[3];
        public TMPro.TextMeshProUGUI[] difficultyBodies = new TMPro.TextMeshProUGUI[3];

        [Header("Nouvelle partie")]
        public Button newGameButton;
        public TMPro.TextMeshProUGUI newGameText;

        [Header("Couleurs")]
        public Color idleColor = new Color(0.49f, 0.63f, 1f, 0.07f);
        public Color selectedColor = new Color(1f, 0.78f, 0.36f, 0.20f);
        public Color[] accentColors = new Color[]
        {
            new Color(0.24f, 0.88f, 0.82f),   // facile : cyan
            new Color(1f, 0.78f, 0.36f),      // moyen : or
            new Color(1f, 0.30f, 0.37f)       // difficile : rouge
        };

        private static readonly string[] NameKeys = { "menuEasy", "menuMedium", "menuHard" };
        private static readonly string[] BodyKeys = { "menuEasyBody", "menuMediumBody", "menuHardBody" };

        private int _selected = 1;
        private bool _loading;
        private UnityEngine.Events.UnityAction[] _cardCallbacks;
        private UnityEngine.Events.UnityAction _newGameCallback;

        private void Awake()
        {
            Time.timeScale = 1f;

            FillMissingLabels();

            _cardCallbacks = new UnityEngine.Events.UnityAction[3];
            for (int i = 0; i < 3; i++)
            {
                if (difficultyButtons == null || i >= difficultyButtons.Length || difficultyButtons[i] == null) continue;

                int slot = i;
                _cardCallbacks[i] = delegate { Select(slot); };
                difficultyButtons[i].onClick.AddListener(_cardCallbacks[i]);
            }

            _newGameCallback = StartNewGame;
            if (newGameButton != null) newGameButton.onClick.AddListener(_newGameCallback);

            Select((int)GameDifficulty.Current);
        }

        private void OnDestroy()
        {
            if (_cardCallbacks != null && difficultyButtons != null)
            {
                for (int i = 0; i < _cardCallbacks.Length && i < difficultyButtons.Length; i++)
                    if (difficultyButtons[i] != null && _cardCallbacks[i] != null)
                        difficultyButtons[i].onClick.RemoveListener(_cardCallbacks[i]);
            }

            if (newGameButton != null && _newGameCallback != null)
                newGameButton.onClick.RemoveListener(_newGameCallback);
        }

        private void FillMissingLabels()
        {
            SetIfEmpty(titleText, "menuTitle");
            SetIfEmpty(subtitleText, "menuSubtitle");
            SetIfEmpty(newGameText, "menuNewGame");

            for (int i = 0; i < 3; i++)
            {
                if (difficultyNames != null && i < difficultyNames.Length) SetIfEmpty(difficultyNames[i], NameKeys[i]);
                if (difficultyBodies != null && i < difficultyBodies.Length) SetIfEmpty(difficultyBodies[i], BodyKeys[i]);
            }
        }

        private static void SetIfEmpty(TMPro.TextMeshProUGUI text, string key)
        {
            if (text == null || !string.IsNullOrEmpty(text.text)) return;
            text.text = UI.HudLabelsRuntime.Get(key);
        }

        /// <summary>Selectionne une difficulte : 0 facile, 1 moyen, 2 difficile.</summary>
        public void Select(int index)
        {
            if (index < 0 || index > 2) index = 1;
            _selected = index;

            for (int i = 0; i < 3; i++)
            {
                bool on = (i == index);
                Color accent = (accentColors != null && i < accentColors.Length) ? accentColors[i] : Color.white;

                if (difficultyBackgrounds != null && i < difficultyBackgrounds.Length && difficultyBackgrounds[i] != null)
                    difficultyBackgrounds[i].color = on ? new Color(accent.r, accent.g, accent.b, selectedColor.a) : idleColor;

                if (difficultyOutlines != null && i < difficultyOutlines.Length && difficultyOutlines[i] != null)
                {
                    difficultyOutlines[i].enabled = on;
                    difficultyOutlines[i].effectColor = accent;
                }

                if (difficultyNames != null && i < difficultyNames.Length && difficultyNames[i] != null)
                    difficultyNames[i].color = accent;
            }
        }

        public void StartNewGame()
        {
            if (_loading) return;
            _loading = true;

            GameDifficulty.Current = (DifficultyLevel)_selected;
            Time.timeScale = 1f;

            if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
            {
                Debug.LogErrorFormat("[Menu] La scene '{0}' n'est pas dans la liste du build (File > Build Profiles).", gameSceneName);
                _loading = false;
                return;
            }

            UnityEngine.SceneManagement.SceneManager.LoadScene(gameSceneName);
        }
    }
}
