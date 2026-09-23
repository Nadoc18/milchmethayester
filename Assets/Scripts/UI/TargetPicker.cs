using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MNLTHII;
using MNLTHII.Rules;

namespace MNLTHII.Managers
{
    /// <summary>
    /// "CHOISIS SA CIBLE" : l'etape qui suit le choix d'un role.
    ///
    /// Donner un role a un Tank ne suffisait pas a dire ce qu'il allait faire. En
    /// Chasse il partait vers l'ennemi que le calcul jugeait le plus interessant ; en
    /// Assaut vers le Shofar le plus proche ; en Garde il defendait autour du point
    /// d'ancrage le plus proche. Le joueur choisissait une intention, pas une cible -
    /// et ne pouvait donc pas faire de plan.
    ///
    /// Desormais, des qu'un role laisse un VRAI choix, le plateau passe en mode
    /// selection : les cibles possibles sont marquees d'un pictogramme pulsant, un
    /// bandeau dit quoi choisir, et le clic decide. Echap garde le comportement
    /// automatique.
    ///
    ///   Garde  : autour de quoi monter la garde (Base, Centres de Commandement)
    ///   Assaut : quel Shofar attaquer
    ///   Chasse : quel ennemi traquer
    ///
    /// Tout se construit en code : rien a reconstruire dans le HUD. Les libelles
    /// viennent de hud_labels.json (pickGuard, pickAssault, pickHunt, pickHint) ; ce
    /// fichier .cs reste en pur ASCII.
    ///
    /// Note d'optimisation : le composant n'est actif que pendant la selection. Les
    /// marqueurs sont crees a l'ouverture et detruits a la fermeture - quelques objets,
    /// une fois par choix de role.
    /// </summary>
    public class TargetPicker : MonoBehaviour
    {
        public static TargetPicker Instance;

        /// <summary>Vrai pendant la selection : le plateau ne repond a rien d'autre.</summary>
        public static bool Active { get { return Instance != null && Instance._active; } }

        private struct Candidate
        {
            public Hexagon hex;
            public PawnController pawn;
            public Transform marker;
            public Image icon;
        }

        private readonly List<Candidate> _candidates = new List<Candidate>(12);

        private bool _active;
        private PawnController _tank;
        private PawnStance _stance;

        private Canvas _canvas;
        private CanvasGroup _group;
        private TMPro.TextMeshProUGUI _title;
        private TMPro.TextMeshProUGUI _hint;

        private Camera _camera;
        private readonly RaycastHit[] _hits = new RaycastHit[4];

        private static readonly Color GuardColor = new Color(0.37f, 0.66f, 1f);
        private static readonly Color AssaultColor = new Color(1f, 0.54f, 0.24f);
        private static readonly Color HuntColor = new Color(0.48f, 0.88f, 0.42f);

        // =================================================================
        //  OUVERTURE
        // =================================================================
        /// <summary>
        /// Ouvre la selection pour ce Tank. Rend faux quand il n'y a rien a choisir
        /// (aucune cible, ou une seule : elle est alors prise d'office).
        /// </summary>
        public static bool Begin(PawnController tank, PawnStance stance)
        {
            if (tank == null || tank.IsEnemy) return false;

            TargetPicker picker = Instance;
            if (picker == null)
                picker = new GameObject("TargetPicker (auto)").AddComponent<TargetPicker>();

            return picker.Open(tank, stance);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            enabled = false;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private bool Open(PawnController tank, PawnStance stance)
        {
            Close();

            _tank = tank;
            _stance = stance;

            GatherCandidates();

            if (_candidates.Count == 0) return false;

            // Une seule possibilite : on la prend, sans deranger le joueur.
            if (_candidates.Count == 1)
            {
                Assign(_candidates[0]);
                ClearCandidates();
                return false;
            }

            _camera = Camera.main;

            BuildUI();
            ShowMarkers();

            _active = true;
            enabled = true;
            return true;
        }

        // =================================================================
        //  LES CIBLES POSSIBLES
        // =================================================================
        private void GatherCandidates()
        {
            ClearCandidates();

            BoardController board = BoardController.instance;
            if (board == null) return;

            if (_stance == PawnStance.Hunt)
            {
                List<PawnController> pawns = board.PawnsInBoard;
                if (pawns == null) return;

                for (int i = 0; i < pawns.Count; i++)
                {
                    PawnController enemy = pawns[i];
                    if (enemy == null || !enemy.IsEnemy || enemy.currentHP <= 0 || enemy.hexcoord == null) continue;

                    Candidate c = new Candidate();
                    c.pawn = enemy;
                    c.hex = board.getHexByCoord(enemy.hexcoord);
                    _candidates.Add(c);
                }
                return;
            }

            List<Hexagon> hexes = board.HexagonsInBoard;
            if (hexes == null) return;

            for (int i = 0; i < hexes.Count; i++)
            {
                Hexagon hex = hexes[i];
                if (hex == null || hex.positionInTheBoard == null || hex.currentHP <= 0) continue;

                bool keep;

                if (_stance == PawnStance.Assault)
                {
                    // Les Shofars encore debout.
                    keep = (hex.type == TypeOfHex.portal);
                }
                else
                {
                    // Garde : la Base, et chaque Centre de Commandement debout. La Base
                    // occupe plusieurs cases : une seule suffit comme point d'ancrage.
                    keep = (hex.type == TypeOfHex.mountain && hex.level >= 1);

                    if (!keep && hex.type == TypeOfHex.Base && !HasBaseCandidate()) keep = true;
                }

                if (!keep) continue;

                Candidate c = new Candidate();
                c.hex = hex;
                _candidates.Add(c);
            }
        }

        private bool HasBaseCandidate()
        {
            for (int i = 0; i < _candidates.Count; i++)
                if (_candidates[i].hex != null && _candidates[i].hex.type == TypeOfHex.Base) return true;
            return false;
        }

        private Color StanceColor()
        {
            switch (_stance)
            {
                case PawnStance.Assault: return AssaultColor;
                case PawnStance.Hunt: return HuntColor;
                default: return GuardColor;
            }
        }

        private MNLTHII.UI.IconKind StanceIcon()
        {
            switch (_stance)
            {
                case PawnStance.Assault: return MNLTHII.UI.IconKind.Portal;
                case PawnStance.Hunt: return MNLTHII.UI.IconKind.Enemy;
                default: return MNLTHII.UI.IconKind.Bunker;
            }
        }

        private string TitleKey()
        {
            switch (_stance)
            {
                case PawnStance.Assault: return "pickAssault";
                case PawnStance.Hunt: return "pickHunt";
                default: return "pickGuard";
            }
        }

        // =================================================================
        //  MARQUEURS SUR LE PLATEAU
        // =================================================================
        private void ShowMarkers()
        {
            Color color = StanceColor();
            Sprite sprite = MNLTHII.UI.IconLibrary.Get(StanceIcon());

            for (int i = 0; i < _candidates.Count; i++)
            {
                Candidate c = _candidates[i];

                Transform anchor = (c.pawn != null) ? c.pawn.transform
                                  : ((c.hex != null) ? c.hex.transform : null);
                if (anchor == null) continue;

                GameObject go = new GameObject("CiblePossible", typeof(Canvas));
                Canvas canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.sortingOrder = 110;

                RectTransform rect = go.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(64f, 64f);
                rect.position = anchor.position + Vector3.up * 2.1f;

                float scale = 0.85f / 64f;
                rect.localScale = new Vector3(scale, scale, scale);

                GameObject iconGo = new GameObject("Icone", typeof(RectTransform));
                RectTransform iconRect = (RectTransform)iconGo.transform;
                iconRect.SetParent(rect, false);
                iconRect.anchorMin = Vector2.zero;
                iconRect.anchorMax = Vector2.one;
                iconRect.offsetMin = Vector2.zero;
                iconRect.offsetMax = Vector2.zero;

                Image icon = iconGo.AddComponent<Image>();
                icon.sprite = sprite;
                icon.color = color;
                icon.raycastTarget = false;
                icon.preserveAspect = true;

                c.marker = rect;
                c.icon = icon;
                _candidates[i] = c;
            }
        }

        private void ClearCandidates()
        {
            for (int i = 0; i < _candidates.Count; i++)
                if (_candidates[i].marker != null) Destroy(_candidates[i].marker.gameObject);

            _candidates.Clear();
        }

        // =================================================================
        //  BANDEAU
        // =================================================================
        private void BuildUI()
        {
            if (_canvas == null)
            {
                GameObject canvasGo = new GameObject("Choix de cible",
                    typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
                canvasGo.transform.SetParent(transform, false);

                _canvas = canvasGo.GetComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 57;

                CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                _group = canvasGo.GetComponent<CanvasGroup>();
                _group.interactable = false;
                _group.blocksRaycasts = false;

                Image band = NewImage("Bande", canvasGo.transform, new Color(0.02f, 0.03f, 0.07f, 0.85f));
                RectTransform bandRect = band.rectTransform;
                bandRect.anchorMin = new Vector2(0f, 1f);
                bandRect.anchorMax = new Vector2(1f, 1f);
                bandRect.pivot = new Vector2(0.5f, 1f);
                bandRect.anchoredPosition = new Vector2(0f, -40f);
                bandRect.sizeDelta = new Vector2(0f, 150f);

                TMPro.TMP_FontAsset font = FindFont();

                _title = NewText("Titre", bandRect, font, 52f, Color.white);
                RectTransform titleRect = _title.rectTransform;
                titleRect.anchorMin = new Vector2(0.5f, 0.5f);
                titleRect.anchorMax = new Vector2(0.5f, 0.5f);
                titleRect.anchoredPosition = new Vector2(0f, 24f);
                titleRect.sizeDelta = new Vector2(1600f, 70f);
                _title.fontStyle = TMPro.FontStyles.Bold;

                _hint = NewText("Aide", bandRect, font, 28f, new Color(0.62f, 0.70f, 0.86f));
                RectTransform hintRect = _hint.rectTransform;
                hintRect.anchorMin = new Vector2(0.5f, 0.5f);
                hintRect.anchorMax = new Vector2(0.5f, 0.5f);
                hintRect.anchoredPosition = new Vector2(0f, -32f);
                hintRect.sizeDelta = new Vector2(1500f, 44f);
            }

            if (_title != null)
            {
                _title.text = MNLTHII.UI.HudLabelsRuntime.Get(TitleKey());
                _title.color = StanceColor();
            }

            if (_hint != null) _hint.text = MNLTHII.UI.HudLabelsRuntime.Get("pickHint");

            _canvas.gameObject.SetActive(true);
        }

        private static TMPro.TMP_FontAsset FindFont()
        {
            TMPro.TextMeshProUGUI sample = Object.FindFirstObjectByType<TMPro.TextMeshProUGUI>();
            return (sample != null) ? sample.font : null;
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);

            Image img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
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

            MNLTHII.UI.TextFeatures.Disable(text);
            return text;
        }

        // =================================================================
        //  SELECTION
        // =================================================================
        private void Update()
        {
            if (!_active) return;

            // Le Tank a disparu, ou la phase est finie : on renonce.
            TurnManager turns = TurnManager.Instance;
            if (_tank == null || _tank.currentHP <= 0 || turns == null || !turns.IsSpendingPhase)
            {
                Close();
                return;
            }

            // Les marqueurs respirent : impossible de les confondre avec le decor.
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 4f);
            for (int i = 0; i < _candidates.Count; i++)
            {
                Candidate c = _candidates[i];
                if (c.marker == null) continue;

                Transform anchor = (c.pawn != null) ? c.pawn.transform
                                  : ((c.hex != null) ? c.hex.transform : null);
                if (anchor != null) c.marker.position = anchor.position + Vector3.up * (2.1f + 0.12f * pulse);

                if (_camera != null) c.marker.rotation = _camera.transform.rotation;

                float scale = (0.85f * pulse) / 64f;
                c.marker.localScale = new Vector3(scale, scale, scale);
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                Debug.Log("[Cible] Choix annule : le Tank decidera seul.");
                Close();
                return;
            }

            if (!Input.GetMouseButtonDown(0)) return;

            Hexagon clicked = HexUnderCursor();
            if (clicked == null) return;

            for (int i = 0; i < _candidates.Count; i++)
            {
                Candidate c = _candidates[i];
                if (c.hex != clicked) continue;

                Assign(c);
                Close();
                return;
            }
        }

        private void Assign(Candidate c)
        {
            if (_tank == null) return;

            if (_stance == PawnStance.Hunt)
            {
                _tank.orderTargetPawn = c.pawn;
                _tank.orderTargetCoord = null;

                if (c.pawn != null)
                    Debug.LogFormat("[Cible] Le Tank traque un ennemi en ({0},{1}).",
                                    c.pawn.hexcoord.q, c.pawn.hexcoord.r);
            }
            else
            {
                _tank.orderTargetPawn = null;

                if (c.hex != null && c.hex.positionInTheBoard != null)
                {
                    HexCoord coord = c.hex.positionInTheBoard;
                    _tank.orderTargetCoord = new HexCoord(coord.q, coord.r, coord.s);

                    Debug.LogFormat("[Cible] Ordre donne sur la case ({0},{1}).", coord.q, coord.r);
                }
            }

            if (FXManager.Instance != null && c.hex != null)
                FXManager.Instance.SpawnCommunityFX(c.hex.transform.position);
        }

        private void Close()
        {
            _active = false;
            enabled = false;
            _tank = null;

            ClearCandidates();

            if (_canvas != null && _canvas.gameObject.activeSelf) _canvas.gameObject.SetActive(false);
        }

        // =================================================================
        //  RAYCAST (meme methode que l'info-bulle)
        // =================================================================
        private Hexagon HexUnderCursor()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null) return null;
            }

            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
            int count = Physics.RaycastNonAlloc(ray, _hits, 500f);
            if (count <= 0) return null;

            int nearest = 0;
            float best = _hits[0].distance;
            for (int i = 1; i < count; i++)
            {
                float d = _hits[i].distance;
                if (d < best) { best = d; nearest = i; }
            }

            Collider collider = _hits[nearest].collider;
            if (collider == null) return null;

            return collider.GetComponentInParent<Hexagon>();
        }
    }
}
