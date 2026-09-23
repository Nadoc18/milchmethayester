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
        public static MainMenuController Instance;

        /// <summary>Nom de la scene du menu, tel qu'enregistre par le builder.</summary>
        public const string MenuSceneName = "MainMenu";

        [Header("Scene de jeu")]
        public string gameSceneName = "Game";

        [Header("Textes")]
        public TMPro.TextMeshProUGUI titleText;
        public TMPro.TextMeshProUGUI subtitleText;

        [Header("Titre en image")]
        [Tooltip("Image du titre, dans Resources. Vide, ou fichier absent : le titre "
               + "ecrit en TextMeshPro est garde, et rien ne change.")]
        public string titleSpriteResource = "Photos/titre_milchemet";
        [Tooltip("Largeur du titre a l'ecran, en pixels de reference (1920 x 1080).")]
        public float titleWidth = 1150f;
        [Tooltip("Hauteur du centre du titre, en pixels de reference.")]
        public float titleY = 452f;
        [Tooltip("Un titre dessine se suffit a lui-meme : le sous-titre passe sous les "
               + "hexagones de difficulte et fait desordre. Decoche pour le garder.")]
        public bool hideSubtitleWithImage = true;
        public Image titleImage;

        [Header("Cartes de difficulte (Facile, Moyen, Difficile)")]
        public Button[] difficultyButtons = new Button[3];
        public Image[] difficultyBackgrounds = new Image[3];
        public UnityEngine.UI.Outline[] difficultyOutlines = new UnityEngine.UI.Outline[3];
        public TMPro.TextMeshProUGUI[] difficultyNames = new TMPro.TextMeshProUGUI[3];
        public TMPro.TextMeshProUGUI[] difficultyBodies = new TMPro.TextMeshProUGUI[3];

        [Header("Plateau hexagonal (menu 3D)")]
        [Tooltip("Contour hexagonal de chaque carte : pleine couleur quand la difficulte est choisie.")]
        public Image[] difficultyFrames = new Image[0];
        [Tooltip("Une seule ligne sous le plateau : la description de la difficulte choisie.")]
        public TMPro.TextMeshProUGUI selectedBodyText;
        public string[] bodyLabels = new string[3];
        [Range(0f, 1f)] public float frameIdleAlpha = 0.35f;

        [Header("Nouvelle partie")]
        public Button newGameButton;
        public TMPro.TextMeshProUGUI newGameText;

        [Header("Reprendre une partie")]
        [Tooltip("Laisse vide : le bouton est fabrique au lancement, a partir de celui "
               + "de la nouvelle partie, sur la case libre juste en dessous.")]
        public Button loadGameButton;
        public TMPro.TextMeshProUGUI loadGameText;

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
        private UnityEngine.Events.UnityAction _loadGameCallback;

        private Image _loadFill;
        private Image _loadFrame;

        private void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;

            FillMissingLabels();
            InstallTitleImage();

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

            // Le bouton "reprendre une partie". Il n'est pas dans la scene : la scene
            // est produite par un script d'editeur, et il aurait fallu la reconstruire
            // pour le voir apparaitre. Il est donc fabrique ici, a partir du bouton de
            // nouvelle partie, sur la case decorative juste en dessous - meme forme,
            // meme famille, aucune reconstruction a faire.
            if (loadGameButton == null && newGameButton != null) BuildLoadButton();

            _loadGameCallback = OpenLoadGames;
            if (loadGameButton != null) loadGameButton.onClick.AddListener(_loadGameCallback);

            RefreshLoadButton();

            Select((int)GameDifficulty.Current);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            if (loadGameButton != null && _loadGameCallback != null)
                loadGameButton.onClick.RemoveListener(_loadGameCallback);

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

                if (difficultyFrames != null && i < difficultyFrames.Length && difficultyFrames[i] != null)
                    difficultyFrames[i].color = new Color(accent.r, accent.g, accent.b, on ? 1f : frameIdleAlpha);
            }

            if (selectedBodyText != null)
            {
                string body = (bodyLabels != null && index < bodyLabels.Length) ? bodyLabels[index] : null;
                if (string.IsNullOrEmpty(body)) body = UI.HudLabelsRuntime.Get(BodyKeys[index]);
                selectedBodyText.text = body;
            }
        }

        // =================================================================
        //  LE TITRE DESSINE
        // =================================================================
        /// <summary>
        /// Remplace le titre ecrit par l'image du titre.
        ///
        /// POURQUOI UNE IMAGE PLUTOT QU'UN TEXTE
        ///
        /// Un titre de jeu n'est pas du texte : c'est un dessin. Aucun reglage de
        /// TextMeshPro ne donne l'or travaille, la bande de lumiere et le nid
        /// d'abeille grave dans les lettres - et surtout, un texte se re-rend a chaque
        /// resolution, alors qu'un dessin reste exactement le meme partout.
        ///
        /// Pose ici, au lancement, et non par le constructeur de scene : le menu est
        /// produit par un script d'editeur, et il aurait fallu le reconstruire pour
        /// voir l'image. Si le fichier n'est pas la, le titre ecrit reste : rien ne
        /// casse, on voit juste l'ancien titre.
        /// </summary>
        private void InstallTitleImage()
        {
            if (titleImage != null) return;
            if (string.IsNullOrEmpty(titleSpriteResource)) return;

            Sprite sprite = Resources.Load<Sprite>(titleSpriteResource);
            if (sprite == null)
            {
                Debug.LogFormat("[Menu] Pas d'image de titre a Resources/{0} : le titre ecrit est garde.",
                                titleSpriteResource);
                return;
            }

            Transform parent = (titleText != null) ? titleText.transform.parent : transform;

            GameObject go = new GameObject("TitreImage", typeof(RectTransform));
            go.layer = 5;

            RectTransform rect = go.transform as RectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, titleY);

            float width = (titleWidth > 100f) ? titleWidth : 1150f;
            float ratio = (sprite.rect.height > 1f) ? (sprite.rect.width / sprite.rect.height) : 5.86f;
            rect.sizeDelta = new Vector2(width, width / ratio);

            titleImage = go.AddComponent<Image>();
            titleImage.sprite = sprite;
            titleImage.preserveAspect = true;
            titleImage.raycastTarget = false;

            // Derriere tout le reste : le titre ne doit jamais avaler un clic de bouton.
            rect.SetAsFirstSibling();

            if (titleText != null) titleText.gameObject.SetActive(false);
            if (hideSubtitleWithImage && subtitleText != null) subtitleText.gameObject.SetActive(false);
        }

        // =================================================================
        //  REPRENDRE UNE PARTIE
        // =================================================================
        /// <summary>
        /// Fabrique le bouton "reprendre une partie" en clonant celui de la nouvelle partie.
        ///
        /// Cloner plutot que dessiner : le bouton herite ainsi de l'hexagone, du
        /// contour, de la zone cliquable inscrite et de l'animation de survol, sans que
        /// rien n'ait a etre redit. La case decorative qu'il remplace disparait - le
        /// plateau garde exactement le meme nombre de cases.
        /// </summary>
        private void BuildLoadButton()
        {
            RectTransform source = newGameButton.transform as RectTransform;
            if (source == null) return;

            Transform board = source.parent;
            if (board == null) return;

            // La case juste sous le centre. Si le plateau a change, on se contente de
            // descendre d'une hauteur d'hexagone : le bouton existe toujours.
            Transform cell = board.Find("Case_0_1");
            if (cell == null) cell = board.Find("Case_-1_1");

            Vector2 position;
            if (cell != null)
            {
                RectTransform cellRect = cell as RectTransform;
                position = (cellRect != null) ? cellRect.anchoredPosition
                                              : source.anchoredPosition + new Vector2(0f, -source.sizeDelta.y);
                Destroy(cell.gameObject);
            }
            else
            {
                position = source.anchoredPosition + new Vector2(0f, -source.sizeDelta.y * 0.82f);
            }

            // worldPositionStays = false : le clone garde sa position LOCALE, donc les
            // ancres du plateau restent valables et la case se pose ou on lui dit.
            GameObject clone = Instantiate(source.gameObject, board, false);
            clone.name = "ChargerPartie";

            RectTransform rect = clone.transform as RectTransform;
            if (rect != null)
            {
                rect.anchoredPosition = position;

                // Taille d'une case ordinaire : la nouvelle partie reste le bouton le
                // plus gros, c'est elle qu'on doit voir en premier.
                if (difficultyButtons != null && difficultyButtons.Length > 0 && difficultyButtons[0] != null)
                {
                    RectTransform model = difficultyButtons[0].transform as RectTransform;
                    if (model != null) rect.sizeDelta = model.sizeDelta;
                }
            }

            Button button = clone.GetComponent<Button>();
            if (button == null) { Destroy(clone); return; }

            // Le clone a herite des abonnements du modele : ils lanceraient une
            // nouvelle partie.
            button.onClick.RemoveAllListeners();
            loadGameButton = button;

            Color accent = (accentColors != null && accentColors.Length > 0)
                ? accentColors[0] : new Color(0.24f, 0.88f, 0.82f);

            Transform fill = clone.transform.Find("Fond");
            if (fill != null)
            {
                _loadFill = fill.GetComponent<Image>();
                if (_loadFill != null) _loadFill.color = new Color(accent.r, accent.g, accent.b, 0.16f);
            }

            Transform frame = clone.transform.Find("Contour");
            if (frame != null)
            {
                _loadFrame = frame.GetComponent<Image>();
                if (_loadFrame != null) _loadFrame.color = accent;
            }

            loadGameText = clone.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (loadGameText != null)
            {
                loadGameText.text = UI.HudLabelsRuntime.Get("menuLoadGame");
                loadGameText.color = accent;
                loadGameText.fontSize *= 0.82f;   // le libelle est plus long que celui de la nouvelle partie
                loadGameText.enableWordWrapping = true;
                UI.TextFeatures.Disable(loadGameText);
            }
        }

        /// <summary>
        /// Eteint le bouton quand il n'y a rien a reprendre. Un bouton qui ouvre une
        /// liste vide fait perdre un clic et laisse croire que la sauvegarde ne marche
        /// pas ; un bouton eteint dit la meme chose sans qu'on ait a cliquer.
        /// </summary>
        public void RefreshLoadButton()
        {
            if (loadGameButton == null) return;

            bool any = SaveManager.HasAny();
            loadGameButton.interactable = any;

            float alpha = any ? 1f : 0.28f;

            if (loadGameText != null)
            {
                Color c = loadGameText.color;
                loadGameText.color = new Color(c.r, c.g, c.b, alpha);
            }
            if (_loadFrame != null)
            {
                Color c = _loadFrame.color;
                _loadFrame.color = new Color(c.r, c.g, c.b, any ? 1f : 0.25f);
            }
            if (_loadFill != null)
            {
                Color c = _loadFill.color;
                _loadFill.color = new Color(c.r, c.g, c.b, any ? 0.16f : 0.05f);
            }
        }

        /// <summary>Ouvre la liste des parties non achevees.</summary>
        public void OpenLoadGames()
        {
            if (_loading) return;
            LoadGamePanel.Open();
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
