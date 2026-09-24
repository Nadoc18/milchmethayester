using UnityEngine;
using UnityEngine.UI;
using MNLTHII.Managers;

namespace MNLTHII.UI
{
    /// <summary>
    /// LA BARRE DE CONTROLE DES PHASES AUTOMATIQUES.
    ///
    /// Pendant la phase des Tanks, celle du Yetzer Hara et la fin de tour, le joueur
    /// ne decide rien : il regarde. C'est bien la premiere fois, et c'est une salle
    /// d'attente la huitieme. Trois boutons lui rendent la main sur le temps, sans
    /// jamais lui rendre la main sur les regles :
    ///
    ///   ALLURE        normal / rapide / eclair, retenu d'une partie a l'autre.
    ///   SUIVANT       cet element a fini de m'apprendre quelque chose, passe au
    ///                 prochain. L'action se resout quand meme, entierement.
    ///   TOUT PASSER   je sais ce qui va arriver, rends-moi le plateau.
    ///
    /// Les raccourcis valent les boutons : ESPACE pour le suivant, ENTREE pour tout
    /// passer. Ces deux touches terminent le tour pendant la phase de depense, et la
    /// barre ne repond que hors de cette phase : elles ne peuvent donc jamais se
    /// marcher dessus.
    ///
    /// La barre se construit en code, sur son propre Canvas. Elle n'a pas besoin du
    /// HudBuilder, donc elle apparait sans que tu aies a reconstruire le HUD, et elle
    /// disparait entierement des que la main revient au joueur.
    ///
    /// Note d'optimisation : un seul Update, qui commence par un test de booleen.
    /// Hors phase automatique il ne fait rien d'autre. Les libelles ne sont reecrits
    /// que quand l'allure change.
    /// </summary>
    public class PhaseSkipBar : MonoBehaviour
    {
        public static PhaseSkipBar Instance;

        // =================================================================
        //  MISE EN PAGE
        // =================================================================
        private const float ButtonWidth = 250f;
        private const float ButtonHeight = 66f;
        private const float Spacing = 14f;
        private const float MarginX = 40f;
        private const float MarginY = 40f;

        private static readonly Color PanelColor = new Color(0.02f, 0.03f, 0.07f, 0.82f);
        private static readonly Color IdleColor = new Color(0.49f, 0.63f, 1f, 0.10f);
        private static readonly Color HoverColor = new Color(0.49f, 0.63f, 1f, 0.24f);
        private static readonly Color TextColor = new Color(0.90f, 0.94f, 1f);
        private static readonly Color HintColor = new Color(0.55f, 0.62f, 0.80f);

        /// <summary>Une allure, une couleur : le joueur voit du premier coup d'oeil ou il en est.</summary>
        private static readonly Color[] SpeedColors =
        {
            new Color(0.49f, 0.63f, 1f),      // normal : bleu calme
            new Color(1f, 0.78f, 0.36f),      // rapide : or
            new Color(1f, 0.42f, 0.29f)       // eclair : orange vif
        };

        // =================================================================
        //  ETAT
        // =================================================================
        private GameObject _root;
        private TMPro.TextMeshProUGUI _speedText;
        private Image _speedBackground;
        private bool _built;
        private bool _visible;
        private int _shownSpeed = -1;

        private string _labelNormal = "";
        private string _labelFast = "";
        private string _labelBlitz = "";

        // =================================================================
        //  CYCLE DE VIE
        // =================================================================
        /// <summary>
        /// S'installe toute seule au chargement de la scene de jeu. Comme pour
        /// PortalStatusVisual, le hook sceneLoaded est indispensable : sans lui la
        /// barre n'existerait que dans la premiere scene chargee, donc plus du tout
        /// apres un retour au menu principal ou un "rejouer".
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            Spawn();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
                                          UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            Spawn();
        }

        private static void Spawn()
        {
            if (Instance != null) return;
            new GameObject("Barre de phase (auto)").AddComponent<PhaseSkipBar>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // =================================================================
        //  BOUCLE
        // =================================================================
        private void Update()
        {
            bool running = PhasePace.Running;

            if (running != _visible)
            {
                _visible = running;

                if (running && !_built) Build();
                if (_root != null) _root.SetActive(running);

                if (running) RefreshSpeedLabel();
            }

            if (!running) return;

            // Les memes touches que le bouton d'a cote. Elles ne sont lues qu'ici,
            // donc jamais pendant la phase de depense ou elles terminent le tour.
            if (Input.GetKeyDown(KeyCode.Space)) PhasePace.SkipUnit();
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) PhasePace.SkipPhase();

            if (_shownSpeed != (int)PhasePace.Speed) RefreshSpeedLabel();
        }

        // =================================================================
        //  ACTIONS
        // =================================================================
        private void OnSpeedClicked()
        {
            PhasePace.Cycle();
            RefreshSpeedLabel();
        }

        private void OnNextClicked() { PhasePace.SkipUnit(); }

        private void OnSkipAllClicked() { PhasePace.SkipPhase(); }

        private void RefreshSpeedLabel()
        {
            int index = (int)PhasePace.Speed;
            _shownSpeed = index;

            if (_speedText != null)
            {
                switch (PhasePace.Speed)
                {
                    case PhaseSpeed.Fast: _speedText.text = _labelFast; break;
                    case PhaseSpeed.Blitz: _speedText.text = _labelBlitz; break;
                    default: _speedText.text = _labelNormal; break;
                }
            }

            if (_speedBackground != null && index >= 0 && index < SpeedColors.Length)
            {
                Color tint = SpeedColors[index];
                tint.a = 0.20f;
                _speedBackground.color = tint;
            }
        }

        // =================================================================
        //  CONSTRUCTION
        // =================================================================
        private void Build()
        {
            _built = true;

            // Les replis sont des symboles, pas des mots : ce fichier reste en ASCII
            // pur (tout l'hebreu vit dans hud_labels.json), et une cle manquante doit
            // laisser un bouton lisible plutot qu'un bouton vide.
            _labelNormal = HudLabelsRuntime.Get("paceNormal", "x1");
            _labelFast = HudLabelsRuntime.Get("paceFast", "x2");
            _labelBlitz = HudLabelsRuntime.Get("paceBlitz", "x4");

            GameObject canvasGo = new GameObject("Barre de phase",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Au-dessus du bandeau d'etape (60) : pendant une annonce de phase aussi,
            // le joueur doit pouvoir appuyer sur "tout passer".
            canvas.sortingOrder = 62;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _root = canvasGo;

            float panelWidth = ButtonWidth * 3f + Spacing * 2f + 28f;
            float panelHeight = ButtonHeight + 52f;

            Image panel = NewImage("Fond", canvasGo.transform, PanelColor, false);
            RectTransform panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(1f, 0f);
            panelRect.anchorMax = new Vector2(1f, 0f);
            panelRect.pivot = new Vector2(1f, 0f);
            panelRect.anchoredPosition = new Vector2(-MarginX, MarginY);
            panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);

            TMPro.TMP_FontAsset font = FindFont();

            // L'ordre de lecture est l'hebreu : le premier bouton est a DROITE.
            float x = -14f;

            _speedBackground = NewButton(panelRect, font, x, _labelNormal, OnSpeedClicked, out _speedText);
            x -= ButtonWidth + Spacing;

            TMPro.TextMeshProUGUI nextText;
            NewButton(panelRect, font, x, HudLabelsRuntime.Get("paceNext", ">"), OnNextClicked, out nextText);
            x -= ButtonWidth + Spacing;

            TMPro.TextMeshProUGUI skipText;
            NewButton(panelRect, font, x, HudLabelsRuntime.Get("paceSkipAll", ">>"), OnSkipAllClicked, out skipText);

            // Le rappel clavier. Non-RTL : il contient des noms de touches latins, et
            // en RTL "Space" s'afficherait a l'envers.
            TMPro.TextMeshProUGUI hint = NewText("Raccourcis", panelRect, font, 22f, HintColor);
            hint.isRightToLeftText = false;
            hint.alignment = TMPro.TextAlignmentOptions.Right;
            hint.text = HudLabelsRuntime.Get("paceHint", "Space / Enter");
            RectTransform hintRect = hint.rectTransform;
            hintRect.anchorMin = new Vector2(1f, 1f);
            hintRect.anchorMax = new Vector2(1f, 1f);
            hintRect.pivot = new Vector2(1f, 1f);
            hintRect.anchoredPosition = new Vector2(-14f, -6f);
            hintRect.sizeDelta = new Vector2(panelWidth - 28f, 30f);

            canvasGo.SetActive(false);
        }

        /// <summary>Un bouton du rail. Renvoie son fond, pour pouvoir le teinter ensuite.</summary>
        private Image NewButton(RectTransform parent, TMPro.TMP_FontAsset font, float x,
                                string label, UnityEngine.Events.UnityAction action,
                                out TMPro.TextMeshProUGUI text)
        {
            Image background = NewImage("Bouton", parent, IdleColor, true);
            RectTransform rect = background.rectTransform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(x, 12f);
            rect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);

            Button button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.85f, 1f, 1f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            // Le survol est porte par un second calque : teinter le bouton lui-meme
            // ecraserait la couleur d'allure qu'on y repose a chaque changement.
            Image hover = NewImage("Survol", rect, HoverColor, false);
            Stretch(hover.rectTransform);
            hover.gameObject.SetActive(false);
            AddHover(background.gameObject, hover.gameObject);

            text = NewText("Libelle", rect, font, 30f, TextColor);
            text.text = label;
            Stretch(text.rectTransform);

            button.onClick.AddListener(action);
            return background;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void AddHover(GameObject target, GameObject highlight)
        {
            PhaseSkipHover hover = target.AddComponent<PhaseSkipHover>();
            hover.highlight = highlight;
        }

        private static TMPro.TMP_FontAsset FindFont()
        {
            TMPro.TextMeshProUGUI sample = Object.FindFirstObjectByType<TMPro.TextMeshProUGUI>();
            return (sample != null) ? sample.font : null;
        }

        private static Image NewImage(string name, Transform parent, Color color, bool raycast)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);

            Image img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        private static TMPro.TextMeshProUGUI NewText(string name, Transform parent,
                                                     TMPro.TMP_FontAsset font, float size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);

            TMPro.TextMeshProUGUI text = go.AddComponent<TMPro.TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.isRightToLeftText = true;
            text.raycastTarget = false;
            text.enableWordWrapping = false;

            TextFeatures.Disable(text);
            return text;
        }
    }

    /// <summary>
    /// Le survol d'un bouton de la barre : il allume un calque, il ne teinte rien.
    /// Teinter le bouton effacerait la couleur d'allure qu'on lui repose.
    /// </summary>
    public class PhaseSkipHover : MonoBehaviour,
                                  UnityEngine.EventSystems.IPointerEnterHandler,
                                  UnityEngine.EventSystems.IPointerExitHandler
    {
        public GameObject highlight;

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (highlight != null) highlight.SetActive(true);
        }

        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (highlight != null) highlight.SetActive(false);
        }
    }
}
