using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using MNLTHII.Rules;

namespace MNLTHII.Managers
{
    /// <summary>
    /// LA VISITE DU PLATEAU, AU DEBUT D'UNE PARTIE NEUVE.
    ///
    /// CE QU'ELLE RESOUT
    ///
    /// Le plateau est fixe et riche : des collines sur le couloir de la Base, des
    /// usines couvertes et des usines exposees, des Cristaux en avant, des Centres de
    /// Commandement loin de chez soi. Un joueur qui arrive dessus sans rien savoir
    /// voit un tapis d'hexagones colores et clique au hasard - et le jeu, qui est un
    /// jeu de position, devient un jeu de hasard.
    ///
    /// La camera passe donc sur CHAQUE type de case avant le premier tour, et dit,
    /// avec les vrais chiffres des regles : ce que c'est, ce que coute chaque niveau,
    /// et ce que ce niveau rapporte. Puis un dernier ecran explique ce qu'est une
    /// bonne strategie - pas une liste de regles, mais l'ordre dans lequel les faire.
    ///
    /// POURQUOI LES CHIFFRES SONT LUS DANS LES REGLES
    ///
    /// Tous les nombres affiches viennent de InteractionRules et de GameDifficulty :
    /// une presentation qui recopierait les chiffres a la main deviendrait fausse au
    /// premier reequilibrage, et une explication fausse est pire que pas
    /// d'explication. Les phrases, elles, vivent dans hud_labels.json (section intro)
    /// avec leurs {0}, {1} : ce fichier .cs reste en pur ASCII.
    ///
    /// ELLE NE SE JOUE QU'UNE FOIS PAR PARTIE NEUVE. Reprendre une partie
    /// sauvegardee ne la rejoue pas : le joueur connait deja son plateau. Un clic
    /// avance, Echap saute tout.
    ///
    /// Note d'optimisation : construite en code, sur son propre Canvas (rien a
    /// reconstruire dans le HUD), et detruite a la fin de la visite. Aucun Update.
    /// </summary>
    public class IntroTour : MonoBehaviour
    {
        public static IntroTour Instance;

        /// <summary>Vrai pendant la visite : le plateau ne repond a rien.</summary>
        public static bool Running { get; private set; }

        [Header("Rythme")]
        [Tooltip("Temps laisse a la camera pour arriver sur la case avant l'explication.")]
        public float travelWait = 0.8f;
        public float fadeDuration = 0.22f;
        [Tooltip("Delai avant qu'un clic puisse passer a la suite : sans lui, le clic "
               + "qui a ferme l'ecran precedent ferme aussi celui-ci.")]
        public float minimumBeforeNext = 0.35f;

        private static readonly Color Gold = new Color(1f, 0.78f, 0.36f);
        private static readonly Color Cyan = new Color(0.24f, 0.88f, 0.82f);
        private static readonly Color Text = new Color(0.88f, 0.92f, 1f);
        private static readonly Color Dim = new Color(0.62f, 0.70f, 0.86f);
        private static readonly Color Panel = new Color(0.03f, 0.05f, 0.11f, 0.95f);

        private GameObject _canvasObject;
        private CanvasGroup _group;
        private Image _photo;
        private TMPro.TextMeshProUGUI _title;
        private TMPro.TextMeshProUGUI _body;
        private TMPro.TextMeshProUGUI _hint;
        private TMPro.TextMeshProUGUI _counter;
        private TMPro.TMP_FontAsset _font;

        private GameObject _strategyRoot;
        private TMPro.TextMeshProUGUI _strategyTitle;
        private TMPro.TextMeshProUGUI _strategyBody;
        private TMPro.TextMeshProUGUI _strategyHint;

        private bool _built;
        private bool _skipped;
        private int _step;
        private int _stepCount = 12;

        // =================================================================
        //  ENTREE
        // =================================================================
        public static IEnumerator Play()
        {
            IntroTour self = Instance;
            if (self == null)
            {
                GameObject go = new GameObject("IntroTour (auto)");
                self = go.AddComponent<IntroTour>();
            }

            yield return self.Run();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Running = false;
        }

        // =================================================================
        //  LA VISITE
        // =================================================================
        private IEnumerator Run()
        {
            if (!Build()) yield break;

            Running = true;
            _skipped = false;
            _step = 0;

            if (GameManager.instance != null) GameManager.instance.canClickOrHover = false;

            // Ouverture : on ne part pas sur une case, on annonce ce qui va se passer.
            yield return Stop(null, null, "introOpenTitle", "introOpenBody");

            yield return Stop(TypeOfHex.Base, "Photos/Base", "introBaseTitle", "introBaseBody",
                              InteractionRules.BASE_HP,
                              InteractionRules.BASE_INCOME_PER_TURN);

            yield return Stop(TypeOfHex.plain, "Photos/Plain", "introGroundTitle", "introGroundBody");

            // La portee de construction vient tot : c'est la regle qui decide de TOUT
            // ce qui suit. Savoir ce que coute un Bunker ne sert a rien tant qu'on ne
            // sait pas ou on a le droit d'en poser un.
            yield return Stop(null, "Photos/tank_1", "introRangeTitle", "introRangeBody",
                              InteractionRules.BUILD_RANGE_FROM_BASE);

            yield return Stop(TypeOfHex.gas, "Photos/gas_1", "introGasTitle", "introGasBody",
                              InteractionRules.COST_GAS_L1, InteractionRules.GAS_INCOME_L1,
                              InteractionRules.COST_GAS_L2, InteractionRules.GAS_INCOME_L2,
                              InteractionRules.SPAWN_ACCEL_PER_INCOME);

            yield return Stop(TypeOfHex.hill, "Photos/Bunker1", "introBunkerTitle", "introBunkerBody",
                              InteractionRules.COST_BUNKER_L1, InteractionRules.BUNKER_DAMAGE_L1,
                              InteractionRules.BUNKER_TARGETS_L1, InteractionRules.BUNKER_RANGE,
                              InteractionRules.COST_BUNKER_L2, InteractionRules.BUNKER_DAMAGE_L2,
                              InteractionRules.BUNKER_TARGETS_L2,
                              // {7} l'usure par tir, {8} les PV du rang 1 : ensemble,
                              // ils disent combien de salves il a dans le ventre. La
                              // decroissance passive n'est plus annoncee parce qu'elle
                              // n'existe plus.
                              InteractionRules.BUNKER_WEAR_PER_SHOT, InteractionRules.BUNKER_HP_L1);

            yield return Stop(TypeOfHex.crystal, "Photos/Crystal_1", "introCrystalTitle", "introCrystalBody",
                              InteractionRules.COST_CRYSTAL_L1, InteractionRules.CRYSTAL_HEAL_L1,
                              InteractionRules.COST_CRYSTAL_L2, InteractionRules.CRYSTAL_HEAL_L2,
                              InteractionRules.CRYSTAL_BONUS_MAXHP, InteractionRules.TANK_EVOLVE_COST);

            yield return Stop(TypeOfHex.mountain, "Photos/command_1", "introCommandTitle", "introCommandBody",
                              InteractionRules.COST_MOUNTAIN_L1, InteractionRules.MOUNTAIN_REPEL_L1,
                              InteractionRules.MOUNTAIN_COMMAND_RADIUS_L1,
                              InteractionRules.COST_MOUNTAIN_L2, InteractionRules.MOUNTAIN_REPEL_L2,
                              InteractionRules.MOUNTAIN_COMMAND_RADIUS_L2,
                              // {6} la bulle, {7} ce qu'elle reprend par tour de calme.
                              // Le Centre ne fond plus tout seul : on vient le lui
                              // prendre, et il faut d'abord percer.
                              InteractionRules.MOUNTAIN_SHIELD_L1,
                              InteractionRules.MOUNTAIN_SHIELD_REGEN);

            yield return Stop(TypeOfHex.portal, "Photos/Portal", "introPortalTitle", "introPortalBody",
                              InteractionRules.PORTAL_COUNT,
                              InteractionRules.PORTAL_HP_L1, InteractionRules.PORTAL_HP_L2,
                              InteractionRules.PORTAL_SPAWN_INTERVAL,
                              InteractionRules.PORTAL_EVOLVE_AFTER_TURNS,
                              InteractionRules.PORTAL_KILLS_TO_BREAK_SHIELD,
                              InteractionRules.PORTAL_SHIELD_DOWN_TURNS);

            // Les deux etapes du Tanya. Elle vient APRES le Shofar : on ne peut
            // parler de retourner un Shofar qu'a qui vient d'apprendre ce que c'est.
            yield return Stop(null, "Photos/Portal2", "introTurnTitle", "introTurnBody",
                              InteractionRules.COST_TURN_PORTAL,
                              InteractionRules.TURNED_PORTAL_INCOME);

            yield return Stop(null, "Photos/tank_2", "introTankTitle", "introTankBody",
                              InteractionRules.TANK_CREATION_COST, 30, 10,
                              InteractionRules.TANK_EVOLVE_COST, 60, 20,
                              InteractionRules.TANK_STANCE_COST);

            // Les chiffres de l'ennemi dependent de la difficulte choisie : on montre
            // ceux de CETTE partie, pas ceux du bareme de reference.
            int hp = GameDifficulty.EnemyHpPercent;
            int dmg = GameDifficulty.EnemyDamagePercent;
            yield return Stop(null, "Photos/enemy_2", "introEnemyTitle", "introEnemyBody",
                              GameDifficulty.Scale(20, hp), GameDifficulty.Scale(10, dmg),
                              GameDifficulty.Scale(40, hp), GameDifficulty.Scale(15, dmg),
                              GameDifficulty.Scale(80, hp), GameDifficulty.Scale(30, dmg));

            CameraDirector.ReleaseCamera();

            // Echap saute la VISITE, pas la strategie : c'est l'ecran qui vaut le plus,
            // il tient en huit lignes, et un joueur presse est justement celui qui en a
            // le plus besoin. Un deuxieme Echap le ferme aussi.
            _skipped = false;
            yield return Strategy();

            yield return Fade(_group.alpha, 0f);

            Running = false;
            if (_canvasObject != null) Destroy(_canvasObject);
            Destroy(gameObject);
        }

        /// <summary>Une etape : la camera va sur la case, la carte explique, on attend.</summary>
        private IEnumerator Stop(TypeOfHex? type, string photoResource, string titleKey, string bodyKey,
                                 params object[] args)
        {
            if (_skipped) yield break;

            _step++;

            Hexagon hex = type.HasValue ? FindHex(type.Value) : null;
            if (hex != null)
            {
                CameraDirector.FocusPoint(hex.transform.position);
                yield return Wait(travelWait);
                if (_skipped) yield break;
            }

            string title = MNLTHII.UI.HudLabelsRuntime.Get(titleKey);
            string body = MNLTHII.UI.HudLabelsRuntime.Get(bodyKey);

            // Une phrase absente du JSON ne doit pas sortir une carte vide.
            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(body)) yield break;

            if (args != null && args.Length > 0 && !string.IsNullOrEmpty(body))
            {
                // Un libelle mal numerote ne doit pas arreter la partie.
                try { body = string.Format(body, args); }
                catch (System.Exception e)
                {
                    Debug.LogWarningFormat("[Intro] Libelle '{0}' mal forme : {1}", bodyKey, e.Message);
                }
            }

            if (_title != null) _title.text = title;
            if (_body != null) _body.text = body;
            if (_counter != null) _counter.SetText("{0} / {1}", _step, _stepCount);

            SetPhoto(photoResource);

            yield return Fade(_group.alpha, 1f);
            yield return WaitForClick();
            yield return Fade(1f, 0f);
        }

        // =================================================================
        //  L'ECRAN DE STRATEGIE
        // =================================================================
        private IEnumerator Strategy()
        {
            if (_strategyRoot == null) yield break;

            string title = MNLTHII.UI.HudLabelsRuntime.Get("introStrategyTitle");
            string body = MNLTHII.UI.HudLabelsRuntime.Get("introStrategyBody");
            if (string.IsNullOrEmpty(body)) yield break;

            if (_strategyTitle != null) _strategyTitle.text = title;
            if (_strategyBody != null) _strategyBody.text = body;
            if (_strategyHint != null)
                _strategyHint.text = MNLTHII.UI.HudLabelsRuntime.Get("introStrategyClose");

            if (_canvasObject != null) _canvasObject.SetActive(true);
            _strategyRoot.SetActive(true);

            yield return Fade(0f, 1f);
            yield return WaitForClick();

            _strategyRoot.SetActive(false);
        }

        // =================================================================
        //  ATTENTES
        // =================================================================
        private IEnumerator WaitForClick()
        {
            float waited = 0f;
            while (true)
            {
                waited += Time.unscaledDeltaTime;

                if (Input.GetKeyDown(KeyCode.Escape)) { _skipped = true; yield break; }

                if (waited >= minimumBeforeNext
                    && (Input.GetMouseButtonDown(0)
                        || Input.GetKeyDown(KeyCode.Space)
                        || Input.GetKeyDown(KeyCode.Return))) yield break;

                yield return null;
            }
        }

        private IEnumerator Wait(float seconds)
        {
            float waited = 0f;
            while (waited < seconds)
            {
                waited += Time.unscaledDeltaTime;
                if (Input.GetKeyDown(KeyCode.Escape)) { _skipped = true; yield break; }
                yield return null;
            }
        }

        private IEnumerator Fade(float from, float to)
        {
            if (_group == null) yield break;

            if (fadeDuration <= 0.01f) { _group.alpha = to; yield break; }

            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / fadeDuration);
                _group.alpha = Mathf.Lerp(from, to, k * k * (3f - 2f * k));
                yield return null;
            }
            _group.alpha = to;
        }

        // =================================================================
        //  LE PLATEAU
        // =================================================================
        /// <summary>
        /// Une case de ce type, la plus proche du centre : c'est celle que le joueur
        /// rencontrera en premier, et la camera la cadre sans sortir du plateau.
        /// </summary>
        private static Hexagon FindHex(TypeOfHex type)
        {
            BoardController board = BoardController.instance;
            if (board == null || board.HexagonsInBoard == null) return null;

            System.Collections.Generic.List<Hexagon> hexes = board.HexagonsInBoard;

            Hexagon best = null;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < hexes.Count; i++)
            {
                Hexagon hex = hexes[i];
                if (hex == null || hex.type != type || hex.positionInTheBoard == null) continue;

                HexCoord c = hex.positionInTheBoard;
                int distance = (Mathf.Abs(c.q) + Mathf.Abs(c.r) + Mathf.Abs(c.s)) / 2;
                if (distance < bestDistance) { bestDistance = distance; best = hex; }
            }

            return best;
        }

        private void SetPhoto(string resource)
        {
            if (_photo == null) return;

            Sprite sprite = string.IsNullOrEmpty(resource) ? null : Resources.Load<Sprite>(resource);

            _photo.sprite = sprite;
            _photo.gameObject.SetActive(sprite != null);
        }

        // =================================================================
        //  CONSTRUCTION
        // =================================================================
        private bool Build()
        {
            if (_built) return _canvasObject != null;
            _built = true;

            _font = FindFont();

            _canvasObject = new GameObject("Presentation du plateau",
                typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            _canvasObject.transform.SetParent(transform, false);

            Canvas canvas = _canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 75;

            CanvasScaler scaler = _canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _group = _canvasObject.GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;

            RectTransform root = (RectTransform)_canvasObject.transform;

            BuildCard(root);
            BuildStrategy(root);

            return true;
        }

        /// <summary>
        /// La carte du bas. En bas, et pas au milieu : la case dont on parle est au
        /// centre de l'ecran, et une carte posee dessus cacherait justement ce qu'on
        /// est en train de montrer.
        /// </summary>
        private void BuildCard(RectTransform root)
        {
            RectTransform card = NewRect("Carte", root);
            card.anchoredPosition = new Vector2(0f, -318f);
            card.sizeDelta = new Vector2(1580f, 340f);

            Image back = card.gameObject.AddComponent<Image>();
            back.color = Panel;
            back.raycastTarget = false;

            Image top = NewImage("Filet", card, Gold);
            RectTransform topRect = top.rectTransform;
            topRect.anchorMin = new Vector2(0f, 1f);
            topRect.anchorMax = new Vector2(1f, 1f);
            topRect.pivot = new Vector2(0.5f, 1f);
            topRect.anchoredPosition = Vector2.zero;
            topRect.sizeDelta = new Vector2(0f, 3f);

            // La photo a GAUCHE : en hebreu la lecture part de la droite, donc le texte
            // occupe la droite et l'image ferme la ligne.
            RectTransform photoSlot = NewRect("Photo", card);
            photoSlot.anchorMin = new Vector2(0f, 0.5f);
            photoSlot.anchorMax = new Vector2(0f, 0.5f);
            photoSlot.pivot = new Vector2(0f, 0.5f);
            photoSlot.anchoredPosition = new Vector2(38f, 0f);
            photoSlot.sizeDelta = new Vector2(262f, 262f);

            _photo = photoSlot.gameObject.AddComponent<Image>();
            _photo.preserveAspect = true;
            _photo.raycastTarget = false;

            _title = NewText("Titre", card, 54f, Gold, TMPro.TextAlignmentOptions.Right);
            RectTransform titleRect = _title.rectTransform;
            titleRect.anchorMin = new Vector2(1f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(1f, 1f);
            titleRect.anchoredPosition = new Vector2(-44f, -30f);
            titleRect.sizeDelta = new Vector2(1180f, 66f);
            _title.fontStyle = TMPro.FontStyles.Bold;

            _body = NewText("Texte", card, 31f, Text, TMPro.TextAlignmentOptions.TopRight);
            RectTransform bodyRect = _body.rectTransform;
            bodyRect.anchorMin = new Vector2(1f, 1f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.pivot = new Vector2(1f, 1f);
            bodyRect.anchoredPosition = new Vector2(-44f, -108f);
            bodyRect.sizeDelta = new Vector2(1180f, 200f);
            _body.lineSpacing = 22f;

            _counter = NewText("Compteur", card, 24f, Dim, TMPro.TextAlignmentOptions.Left);
            _counter.isRightToLeftText = false;   // des chiffres : jamais en RTL
            RectTransform countRect = _counter.rectTransform;
            countRect.anchorMin = new Vector2(0f, 0f);
            countRect.anchorMax = new Vector2(0f, 0f);
            countRect.pivot = new Vector2(0f, 0f);
            countRect.anchoredPosition = new Vector2(38f, 16f);
            countRect.sizeDelta = new Vector2(180f, 34f);

            _hint = NewText("Indice", root, 26f, Dim, TMPro.TextAlignmentOptions.Center);
            RectTransform hintRect = _hint.rectTransform;
            hintRect.anchoredPosition = new Vector2(0f, -508f);
            hintRect.sizeDelta = new Vector2(1200f, 40f);
            _hint.text = MNLTHII.UI.HudLabelsRuntime.Get("introSkip");
        }

        /// <summary>Le dernier ecran : plein cadre, rien d'autre a regarder.</summary>
        private void BuildStrategy(RectTransform root)
        {
            _strategyRoot = NewRect("Strategie", root).gameObject;
            RectTransform rect = (RectTransform)_strategyRoot.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image dim = _strategyRoot.AddComponent<Image>();
            dim.color = new Color(0.01f, 0.02f, 0.05f, 0.94f);
            dim.raycastTarget = false;

            RectTransform panel = NewRect("Panneau", rect);
            panel.sizeDelta = new Vector2(1500f, 760f);

            Image back = panel.gameObject.AddComponent<Image>();
            back.color = new Color(0.04f, 0.06f, 0.12f, 0.98f);
            back.raycastTarget = false;

            Image rule = NewImage("Filet", panel, Cyan);
            RectTransform ruleRect = rule.rectTransform;
            ruleRect.anchorMin = new Vector2(0.5f, 1f);
            ruleRect.anchorMax = new Vector2(0.5f, 1f);
            ruleRect.pivot = new Vector2(0.5f, 1f);
            ruleRect.anchoredPosition = new Vector2(0f, -116f);
            ruleRect.sizeDelta = new Vector2(1180f, 2f);

            _strategyTitle = NewText("Titre", panel, 60f, Gold, TMPro.TextAlignmentOptions.Center);
            RectTransform titleRect = _strategyTitle.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -34f);
            titleRect.sizeDelta = new Vector2(1300f, 74f);
            _strategyTitle.fontStyle = TMPro.FontStyles.Bold;

            _strategyBody = NewText("Texte", panel, 32f, Text, TMPro.TextAlignmentOptions.TopRight);
            RectTransform bodyRect = _strategyBody.rectTransform;
            bodyRect.anchorMin = new Vector2(1f, 1f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.pivot = new Vector2(1f, 1f);
            bodyRect.anchoredPosition = new Vector2(-60f, -150f);
            bodyRect.sizeDelta = new Vector2(1380f, 520f);
            _strategyBody.lineSpacing = 46f;

            _strategyHint = NewText("Indice", panel, 34f, Cyan, TMPro.TextAlignmentOptions.Center);
            RectTransform hintRect = _strategyHint.rectTransform;
            hintRect.anchorMin = new Vector2(0.5f, 0f);
            hintRect.anchorMax = new Vector2(0.5f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.anchoredPosition = new Vector2(0f, 34f);
            hintRect.sizeDelta = new Vector2(600f, 48f);
            _strategyHint.fontStyle = TMPro.FontStyles.Bold;

            _strategyRoot.SetActive(false);
        }

        // =================================================================
        //  PETITS OUTILS
        // =================================================================
        private static TMPro.TMP_FontAsset FindFont()
        {
            TMPro.TextMeshProUGUI sample = Object.FindFirstObjectByType<TMPro.TextMeshProUGUI>();
            return (sample != null) ? sample.font : null;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            RectTransform rect = NewRect(name, parent);
            Image img = rect.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private TMPro.TextMeshProUGUI NewText(string name, Transform parent, float size,
                                              Color color, TMPro.TextAlignmentOptions align)
        {
            RectTransform rect = NewRect(name, parent);

            TMPro.TextMeshProUGUI text = rect.gameObject.AddComponent<TMPro.TextMeshProUGUI>();
            if (_font != null) text.font = _font;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.isRightToLeftText = true;
            text.raycastTarget = false;
            text.enableWordWrapping = true;

            // Crenage coupe : en hebreu RTL, TMP decale les lettres du mauvais cote.
            MNLTHII.UI.TextFeatures.Disable(text);
            return text;
        }
    }
}
