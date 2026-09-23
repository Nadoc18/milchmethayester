using UnityEngine;
using UnityEngine.UI;
using MNLTHII;
using MNLTHII.Rules;

namespace MNLTHII.Managers
{
    /// <summary>Ce qu'un clic sur cet hexagone declencherait, et ce que ca couterait.</summary>
    public enum HexActionKind
    {
        None,          // rien a faire ici
        TankCreate,    // plaine ou desert libre
        TankEvolve,    // un Tank allie, avec un Cristal a portee
        StanceChange,  // un Tank allie qui ne peut pas evoluer : on change sa posture
        Build,         // colline, gaz, cristal, montagne
        MaxLevel,      // deja au niveau maximum
        Occupied       // un ennemi occupe la case
    }

    /// <summary>
    /// Resultat d'une interrogation : de quoi remplir une info-bulle sans rien
    /// deviner. Une struct, donc aucune allocation a chaque survol.
    /// </summary>
    public struct HexAction
    {
        public HexActionKind kind;
        public int cost;
        public int targetLevel;
        public bool affordable;

        /// <summary>
        /// Posture que le clic donnera, quand kind vaut StanceChange. Sans elle
        /// l'info-bulle annoncait "changer de posture" sans dire laquelle : le joueur
        /// devait cliquer pour decouvrir, et donc cliquer trois fois pour choisir.
        /// </summary>
        public int nextStance;

        /// <summary>
        /// Posture suivante dans le cycle, dans le meme ordre que PawnController.
        /// CycleStance : Garde, Assaut, Chasse. Les deux doivent rester d'accord,
        /// sinon l'info-bulle annonce une posture et le clic en donne une autre.
        /// </summary>
        public static int NextStanceIndex(PawnStance current)
        {
            switch (current)
            {
                case PawnStance.Guard: return (int)PawnStance.Assault;
                case PawnStance.Assault: return (int)PawnStance.Hunt;
                default: return (int)PawnStance.Guard;
            }
        }

        /// <summary>
        /// Rejoue EXACTEMENT la decision de InteractionRules.ApplyPlayerAction, sans
        /// rien modifier. C'est la seule facon d'etre certain que l'info-bulle ne
        /// ment pas : si la regle change, les deux changent ensemble.
        /// </summary>
        public static HexAction Resolve(Hexagon hex)
        {
            HexAction result = new HexAction();
            result.kind = HexActionKind.None;

            if (hex == null) return result;

            BoardController board = BoardController.instance;
            PawnController occupant = (board != null) ? board.getPawnByCoord(hex.positionInTheBoard) : null;

            if (occupant != null)
            {
                if (occupant.typeOfPawn == TypeOfPawn.enemy)
                {
                    result.kind = HexActionKind.Occupied;
                    return result;
                }

                // Un clic sur un Tank a nous ouvre desormais l'ecran de choix, qui
                // montre les trois postures ET l'evolution ensemble. L'info-bulle ne
                // peut donc plus annoncer UNE action precise : elle annonce l'ecran.
                //
                // nextStance vaut -1 volontairement - aucune posture n'est promise,
                // et le sous-titre reste vide plutot que de mentir.
                if (occupant.typeOfPawn == TypeOfPawn.unit)
                {
                    result.kind = HexActionKind.StanceChange;
                    result.affordable = true;
                    result.nextStance = -1;
                    return result;
                }

                bool canEvolve = occupant.level < InteractionRules.MAX_TANK_LEVEL
                                 && InteractionRules.HasCrystalSupport(hex.positionInTheBoard);

                // Evolution hors budget : le clic fera tourner la posture. On doit
                // l'annoncer, sinon l'info-bulle promet une evolution que le clic ne
                // donnera pas - et une info-bulle qui ment est pire que pas d'info-bulle.
                if (canEvolve)
                {
                    EnergyManager wallet = EnergyManager.Instance;
                    if (wallet == null || !wallet.CanAfford(InteractionRules.TANK_EVOLVE_COST))
                        canEvolve = false;
                }

                if (!canEvolve)
                {
                    // Gratuit : le clic fait simplement tourner la posture.
                    result.kind = HexActionKind.StanceChange;
                    result.affordable = true;
                    result.nextStance = NextStanceIndex(occupant.stance);
                    return result;
                }

                result.kind = HexActionKind.TankEvolve;
                result.cost = InteractionRules.TANK_EVOLVE_COST;
                result.targetLevel = 2;
            }
            else
            {
                switch (hex.type)
                {
                    // Le desert ne donne rien : aucune action, aucune evolution.
                    case TypeOfHex.plain:
                        result.kind = HexActionKind.TankCreate;
                        result.cost = InteractionRules.TANK_CREATION_COST;
                        result.targetLevel = 1;
                        break;

                    case TypeOfHex.hill:
                    case TypeOfHex.gas:
                    case TypeOfHex.crystal:
                    case TypeOfHex.mountain:
                        if (hex.level >= InteractionRules.MAX_TERRAIN_LEVEL)
                        {
                            result.kind = HexActionKind.MaxLevel;
                            return result;
                        }
                        result.kind = HexActionKind.Build;
                        result.targetLevel = hex.level + 1;
                        result.cost = InteractionRules.GetBuildCost(hex.type, result.targetLevel);
                        break;

                    default:
                        return result;
                }
            }

            EnergyManager energy = EnergyManager.Instance;
            result.affordable = (result.cost <= 0)
                                || (energy != null && energy.CanAfford(result.cost));
            return result;
        }
    }

    /// <summary>
    /// L'info-bulle de cout, au survol d'un hexagone pendant la phase de depense.
    ///
    /// Sans elle le joueur clique a l'aveugle : il ne connait ni les prix ni ce que
    /// fait chaque terrain. C'est le manque le plus grave de l'interface actuelle.
    ///
    /// Comme HudController, ce composant ne cree rien : tu construis le panneau,
    /// tu glisses les references, et il le remplit. Aucun texte en dur dans ce
    /// fichier - les libelles hebreux sont des champs de l'inspecteur, pour que le
    /// .cs reste en pur ASCII et survive a n'importe quel re-enregistrement.
    /// </summary>
    public class HexTooltipController : MonoBehaviour
    {
        [Header("Mode")]
        /// <summary>
        /// Decoche (par defaut) : le panneau ne s'ouvre plus au survol - c'etait trop
        /// genant, il recouvrait le plateau a chaque mouvement de souris. Il s'ouvre
        /// par le bouton "details" du menu de case, et reste ouvert jusqu'a Echap ou un
        /// clic a cote.
        /// Coche : l'ancien comportement, au survol. C'est aussi ce qui se passe si le
        /// menu de case n'existe pas encore (HUD pas reconstruit).
        /// </summary>
        public bool showOnHover = false;

        [Tooltip("Coche : le panneau de details s'ouvre au centre de l'ecran. Decoche : a l'endroit du clic.")]
        public bool detailsCentered = true;

        [Tooltip("Le voile sombre plein ecran derriere les details, comme pour le choix du Tank.")]
        public GameObject detailsDim;

        [Header("Panneau")]
        [Tooltip("La racine a montrer ou masquer. Laisse vide pour utiliser cet objet.")]
        public GameObject panelRoot;
        [Tooltip("Optionnel : le panneau suit le curseur. Sinon il reste ou tu l'as pose.")]
        public bool followCursor = true;
        public Vector2 cursorOffset = new Vector2(24f, -24f);

        [Header("Contenu")]
        public TMPro.TextMeshProUGUI titleText;
        public TMPro.TextMeshProUGUI subtitleText;
        public TMPro.TextMeshProUGUI costText;
        public TMPro.TextMeshProUGUI afterBalanceText;
        [Tooltip("Optionnel : masque tout le bloc de cout quand l'action est gratuite.")]
        public GameObject costRoot;

        [Header("Pictogramme")]
        /// <summary>
        /// Le symbole de ce qu'on survole. Il change avec la case, et il porte la
        /// couleur de l'element : on sait ce qu'on regarde avant d'avoir lu le titre.
        /// </summary>
        public Image kindIcon;

        [Header("Photo de l'element")]
        /// <summary>
        /// Un bandeau en haut de l'info-bulle : la photo de ce que le clic va produire.
        /// Les images viennent de ElementPhotos (sur l'objet des gestionnaires). Sans
        /// photo pour cette case, le bandeau disparait et l'info-bulle reprend sa
        /// taille d'avant.
        /// </summary>
        public GameObject photoRoot;
        public Image photoImage;
        [Tooltip("Recadre la photo pour remplir le bandeau sans la deformer.")]
        public AspectRatioFitter photoFitter;
        [Tooltip("Le bloc de texte, pousse vers le bas quand une photo est affichee.")]
        public RectTransform contentRoot;
        public float photoHeight = 230f;

        [Tooltip("Coche : la photo montre ce que le clic va PRODUIRE (le batiment ameliore). " +
                 "Decoche : ce qui EST sur la case maintenant (le gaz brut, puis l'usine Niveau 1...).")]
        public bool photoShowsResult = false;

        [Tooltip("Le bandeau s'adapte a la forme de la photo, entre ces deux hauteurs.")]
        public float minPhotoHeight = 140f;

        // Nom neuf (ex-maxPhotoHeight) pour que cette valeur s'applique : les photos
        // sont CARREES, et a pleine largeur un carre ferait 470 de haut - l'info-bulle
        // deviendrait plus haute que l'ecran. 260 : un carre bien lisible, centre, sur
        // le fond sombre du bandeau.
        [Tooltip("Hauteur maximale du bandeau. Une photo carree s'affiche a cette taille, centree.")]
        public float photoMaxHeight = 260f;

        [Header("Evolutions")]
        /// <summary>
        /// La rangee des niveaux possibles, chacun avec sa photo : avant construction,
        /// niveau 1, niveau 2 (1-2 pour un Tank, 1-3 pour un ennemi). Le niveau actuel
        /// est cercle de cyan, le prochain d'or : on voit d'un coup d'oeil ou on en est
        /// et ce que la prochaine depense apporte.
        /// </summary>
        public RectTransform evolutionRoot;
        public GameObject[] evolutionSlots = new GameObject[3];
        public Image[] evolutionFrames = new Image[3];
        public UnityEngine.UI.Outline[] evolutionOutlines = new UnityEngine.UI.Outline[3];
        public Image[] evolutionThumbs = new Image[3];
        public TMPro.TextMeshProUGUI[] evolutionLabels = new TMPro.TextMeshProUGUI[3];
        public float evolutionHeight = 160f;
        public string evolutionLevelFormat = "{0}";
        public string evolutionNaturalLabel = "";
        public Color evolutionCurrentColor = new Color(0.25f, 0.88f, 0.82f);
        public Color evolutionNextColor = new Color(1f, 0.78f, 0.36f);
        public Color evolutionOtherColor = new Color(0.49f, 0.55f, 0.69f, 0.6f);

        [Header("Ce que le batiment APPORTE")]
        [Tooltip("Une phrase, sous les statistiques. Laisse vide pour ne rien afficher.")]
        public TMPro.TextMeshProUGUI effectText;

        [Header("Lignes de detail (3 au maximum)")]
        public TMPro.TextMeshProUGUI[] detailLabels = new TMPro.TextMeshProUGUI[3];
        public TMPro.TextMeshProUGUI[] detailValues = new TMPro.TextMeshProUGUI[3];

        [Header("Libelles - a saisir en hebreu ici, jamais dans le code")]
        public string labelTankCreate = "";
        public string labelTankEvolve = "";
        public string labelStanceChange = "";

        [Tooltip("Garde, Assaut, Chasse - dans l'ordre de l'enum PawnStance. Repris de unit.stances dans hud_labels.json.")]
        public string[] stanceNames = new string[3];
        public string labelBunker = "";
        public string labelGas = "";
        public string labelCrystal = "";
        public string labelMountain = "";
        public string labelMaxLevel = "";
        public string labelOccupied = "";
        public string labelFree = "";

        /// <summary>
        /// UNE PHRASE PAR BATIMENT, ET POURQUOI ELLE EST INDISPENSABLE.
        ///
        /// L'info-bulle affichait deja les chiffres : soin 10, revenu par tour 5. Un
        /// joueur qui a fini une partie entiere ne savait toujours pas ce que le Gaz
        /// apporte par rapport au Cristal - parce qu'une statistique dit COMBIEN, pas
        /// A QUOI CA SERT. Ces lignes disent ce que le batiment change pour lui.
        /// </summary>
        public string effectBunker = "";
        public string effectGas = "";
        public string effectCrystal = "";
        public string effectMountain = "";
        public string effectTankCreate = "";
        public string effectTankEvolve = "";

        [Header("Couleurs des pictogrammes")]
        public Color iconGas = new Color(0.42f, 0.92f, 0.62f);
        public Color iconCrystal = new Color(0.30f, 0.94f, 0.86f);
        public Color iconBunker = new Color(1f, 0.82f, 0.36f);
        public Color iconCommand = new Color(1f, 0.54f, 0.24f);
        public Color iconTank = new Color(0.37f, 0.66f, 1f);
        public Color iconDanger = new Color(1f, 0.36f, 0.42f);

        [Space(6)]
        [Tooltip("Sous-titre, avec {0} pour le niveau vise. Exemple : ... {0}")]
        public string subtitleLevelFormat = "{0}";

        [Space(6)]
        public string labelShotsPerTurn = "";
        public string labelRange = "";
        public string labelShotCost = "";
        public string labelHeal = "";
        public string labelIncome = "";
        public string labelMaxHpBonus = "";
        public string labelDamage = "";
        public string labelHitPoints = "";
        public string labelCommandRadius = "";
        public string labelMoveBonus = "";
        public string labelDecay = "";

        [Header("Confirmation (bouton 'utiliser l'energie' du menu)")]
        [Tooltip("Hauteur de la rangee Confirmer / Annuler, ajoutee sous le panneau.")]
        public float confirmRowHeight = 84f;
        public Color confirmColor = new Color(1f, 0.78f, 0.36f, 0.22f);
        public Color confirmDisabledColor = new Color(0.49f, 0.55f, 0.69f, 0.08f);
        public Color confirmCancelColor = new Color(0.49f, 0.63f, 1f, 0.08f);

        [Header("Couleurs")]
        public Color affordableColor = new Color(1f, 0.78f, 0.36f);
        public Color tooExpensiveColor = new Color(1f, 0.30f, 0.37f);
        public Color freeColor = new Color(0.24f, 0.88f, 0.82f);

        [Header("Raycast")]
        [SerializeField] private LayerMask boardLayerMask = ~0;
        [SerializeField] private float rayMaxDistance = 500f;
        [Tooltip("Intervalle entre deux sondages, en secondes. 0.05 est instantane a l'oeil.")]
        public float probeInterval = 0.05f;

        // --- caches chauds ---
        private Camera _camera;
        private RectTransform _rect;
        private readonly RaycastHit[] _hits = new RaycastHit[4];
        private Hexagon _current;
        private float _nextProbe;
        private float _baseHeight = -1f;
        private bool _warnedNoPhotoSlot;
        private bool _detailsOpen;
        private int _closedFrame = -1;

        // --- confirmation d'une construction ---
        private RectTransform _confirmRow;
        private Button _confirmButton;
        private Button _confirmCancelButton;
        private Image _confirmBg;
        private TMPro.TextMeshProUGUI _confirmText;
        private TMPro.TextMeshProUGUI _confirmCost;
        private TMPro.TextMeshProUGUI _confirmCancelText;
        private bool _confirmActive;
        private Hexagon _confirmHex;
        private UnityEngine.Events.UnityAction _onConfirm;
        private UnityEngine.Events.UnityAction _onConfirmCancel;

        /// <summary>
        /// Vrai pendant la frame ou les details viennent d'etre fermes par un clic : ce
        /// clic-la ne doit pas aussi ouvrir le menu de la case qui est dessous.
        /// </summary>
        public bool ClosedThisFrame { get { return _closedFrame == Time.frameCount; } }

        /// <summary>Vrai quand le panneau de details est ouvert (mode clic).</summary>
        public bool DetailsOpen { get { return _detailsOpen; } }

        /// <summary>
        /// Instance unique, pour que les autres panneaux puissent savoir ce qui est
        /// survole sans relancer un raycast de leur cote.
        /// </summary>
        public static HexTooltipController Instance { get; private set; }

        /// <summary>L'hexagone sous le curseur, ou null. Lecture seule.</summary>
        public Hexagon HoveredHex { get { return ClickMode ? _hoverProbe : _current; } }

        // En mode clic, le panneau ne suit plus la souris, mais d'autres panneaux ont
        // encore besoin de savoir ce qui est survole - le panneau d'etat d'un Shofar
        // en premier. On continue donc de sonder, sans rien afficher.
        private Hexagon _hoverProbe;

        private bool ClickMode { get { return !showOnHover && HexActionMenu.Instance != null; } }

        private void OnDestroy()
        {
            if (_confirmButton != null && _onConfirm != null) _confirmButton.onClick.RemoveListener(_onConfirm);
            if (_confirmCancelButton != null && _onConfirmCancel != null) _confirmCancelButton.onClick.RemoveListener(_onConfirmCancel);
            if (Instance == this) Instance = null;
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;

            if (panelRoot == null) panelRoot = gameObject;
            _rect = panelRoot.transform as RectTransform;

            // La hauteur construite, sans photo. La photo s'y ajoute quand il y en a une.
            if (_rect != null) _baseHeight = _rect.sizeDelta.y;

            // Camera.main declenche un FindGameObjectsWithTag : resolue une seule fois.
            _camera = Camera.main;

            Hide();
        }

        private void Update()
        {
            // Mode clic : pas de sondage au survol, seulement la gestion de la fermeture.
            // Sans menu de case (HUD pas reconstruit), on garde le survol : sinon ces
            // informations deviendraient inaccessibles.
            if (ClickMode)
            {
                UpdateDetails();
                ProbeHoverOnly();
                return;
            }

            // Hors phase de depense, l'info-bulle n'a rien a dire.
            TurnManager turns = TurnManager.Instance;

            // Menu de case ouvert : l'info-bulle se tait, sinon elle se poserait
            // par-dessus les deux boutons.
            HexActionMenu menu = HexActionMenu.Instance;
            bool menuOpen = (menu != null && menu.IsOpen);

            if (turns == null || !turns.IsSpendingPhase || menuOpen)
            {
                // On oublie l'hexagone survole : sinon Hide() serait rappele a chaque
                // frame, et HoveredHex mentirait aux panneaux qui le lisent.
                if (_current != null) { _current = null; Hide(); }
                return;
            }

            if (followCursor && _rect != null && _current != null)
                _rect.position = ClampToScreen((Vector2)Input.mousePosition);

            // Vingt sondages par seconde : imperceptible, et vingt fois moins de
            // travail physique qu'un raycast a chaque frame.
            if (Time.unscaledTime < _nextProbe) return;
            _nextProbe = Time.unscaledTime + probeInterval;

            Probe();
        }

        private void Probe()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null) return;
            }

            Hexagon found = HexUnderCursor();

            if (found == _current) return;     // rien n'a change
            _current = found;

            if (found == null) { Hide(); return; }

            Fill(found);
        }

        /// <summary>Sondage sans allocation : tampon de hits membre, CompareTag, pas de string.</summary>
        private Hexagon HexUnderCursor()
        {
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);

            int count = Physics.RaycastNonAlloc(ray, _hits, rayMaxDistance, boardLayerMask);
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

            Transform parent = collider.transform.parent;
            if (parent != null && parent.CompareTag("Hexagon")) return parent.GetComponent<Hexagon>();

            // Le curseur est sur un PION (un ennemi qui vole au-dessus de sa case, un
            // Tank) et non sur la case elle-meme : son collider n'est pas l'enfant direct
            // de l'hexagone. On remonte jusqu'a la case qui le porte - sans quoi
            // survoler un ennemi n'affichait rien, ni texte ni photo.
            return collider.GetComponentInParent<Hexagon>();
        }

        // =================================================================
        //  REMPLISSAGE
        // =================================================================
        private void Fill(Hexagon hex)
        {
            HexAction action = HexAction.Resolve(hex);

            if (action.kind == HexActionKind.None) { Hide(); return; }

            panelRoot.SetActive(true);

            if (titleText != null) titleText.text = TitleFor(hex, action.kind);

            if (subtitleText != null)
            {
                bool showLevel = (action.kind == HexActionKind.Build || action.kind == HexActionKind.TankEvolve);
                bool showStance = (action.kind == HexActionKind.StanceChange);

                subtitleText.gameObject.SetActive(showLevel || showStance);

                if (showLevel)
                {
                    subtitleText.SetText(subtitleLevelFormat, action.targetLevel + 1);
                }
                else if (showStance)
                {
                    // Affectation directe d'une chaine deja prete : aucune
                    // concatenation, donc aucune allocation au survol.
                    subtitleText.text = (stanceNames != null
                                         && action.nextStance >= 0
                                         && action.nextStance < stanceNames.Length)
                                        ? stanceNames[action.nextStance]
                                        : string.Empty;
                }
            }

            FillCost(action);
            FillDetails(hex, action);
            FillEffect(hex, action);
            FillIcon(hex, action);
            FillPhoto(hex, action);
        }

        /// <summary>
        /// L'info-bulle suit le curseur, mais ne sort jamais de l'ecran : trop bas, elle
        /// remonte ; trop a droite, elle passe de l'autre cote du curseur. Avec la photo
        /// elle est plus haute, et c'est la que le probleme se voyait.
        /// </summary>
        private Vector2 ClampToScreen(Vector2 mouse)
        {
            Vector2 pos = mouse + cursorOffset;

            // Taille reelle a l'ecran (le canevas peut etre mis a l'echelle).
            Vector3 scale = _rect.lossyScale;
            float width = _rect.rect.width * scale.x;
            float height = _rect.rect.height * scale.y;

            if (pos.x + width > Screen.width) pos.x = mouse.x - cursorOffset.x - width;
            if (pos.x < 0f) pos.x = 0f;

            if (pos.y - height < 0f) pos.y = height;
            if (pos.y > Screen.height) pos.y = Screen.height;

            return pos;
        }

        /// <summary>
        /// La photo de ce que le clic va produire, et la place qu'elle prend. Pas de
        /// photo : le bandeau disparait et le texte remonte, rien ne reste vide.
        /// </summary>
        private void FillPhoto(Hexagon hex, HexAction action)
        {
            Sprite photo = PhotoFor(hex, action);
            bool show = (photo != null && photoImage != null);

            // Une photo existe mais l'info-bulle n'a pas de bandeau pour l'afficher :
            // le HUD date d'avant les photos. On le dit une fois, clairement.
            if (photo != null && photoImage == null && !_warnedNoPhotoSlot)
            {
                _warnedNoPhotoSlot = true;
                Debug.LogWarning("[InfoBulle] Une photo est disponible mais l'info-bulle n'a pas de bandeau photo. "
                               + "Lance Milchemet > Construire le HUD, puis Ctrl+S.");
            }

            if (photoRoot != null && photoRoot.activeSelf != show) photoRoot.SetActive(show);

            float offset = 0f;

            if (show)
            {
                photoImage.sprite = photo;

                Rect r = photo.rect;
                float aspect = (r.height > 0f) ? r.width / r.height : 1f;

                // La photo ENTIERE, jamais recadree. Le bandeau prend la hauteur qui
                // correspond a la forme de l'image a pleine largeur ; seule une image
                // tres haute est bornee (photoMaxHeight), et elle est alors montree en
                // entier avec des bandes sur les cotes plutot que coupee.
                if (photoFitter != null)
                {
                    photoFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                    photoFitter.aspectRatio = aspect;
                }

                float width = (_rect != null) ? _rect.rect.width : 470f;
                offset = Mathf.Clamp(width / aspect, minPhotoHeight, photoMaxHeight);

                RectTransform frame = photoRoot != null ? photoRoot.transform as RectTransform : null;
                if (frame != null) frame.sizeDelta = new Vector2(frame.sizeDelta.x, offset);
            }

            // La rangee des evolutions, juste sous la photo.
            offset += FillEvolution(hex, action, offset);

            if (contentRoot != null) contentRoot.anchoredPosition = new Vector2(0f, -offset);

            // La rangee Confirmer / Annuler, quand le panneau sert de confirmation.
            float extra = _confirmActive ? confirmRowHeight : 0f;

            if (_rect != null && _baseHeight > 0f)
                _rect.sizeDelta = new Vector2(_rect.sizeDelta.x, _baseHeight + offset + extra);
        }

        /// <summary>
        /// Remplit la rangee des niveaux et rend la hauteur qu'elle occupe (0 si rien a
        /// montrer). Une photo manquante est remplacee par le pictogramme blanc de
        /// l'element, en sourdine : la rangee reste lisible meme incomplete.
        /// </summary>
        private float FillEvolution(Hexagon hex, HexAction action, float top)
        {
            if (evolutionRoot == null) return 0f;

            MNLTHII.UI.PhotoSubject subject;
            int first, last, current;

            if (!EvolutionFor(hex, action, out subject, out first, out last, out current))
            {
                if (evolutionRoot.gameObject.activeSelf) evolutionRoot.gameObject.SetActive(false);
                return 0f;
            }

            if (!evolutionRoot.gameObject.activeSelf) evolutionRoot.gameObject.SetActive(true);
            evolutionRoot.anchoredPosition = new Vector2(0f, -top);

            int count = last - first + 1;

            for (int i = 0; i < 3; i++)
            {
                bool visible = i < count;

                if (evolutionSlots != null && i < evolutionSlots.Length && evolutionSlots[i] != null
                    && evolutionSlots[i].activeSelf != visible)
                    evolutionSlots[i].SetActive(visible);

                if (!visible) continue;

                int level = first + i;
                Color tint = (level == current) ? evolutionCurrentColor
                           : (level == current + 1) ? evolutionNextColor
                           : evolutionOtherColor;

                if (evolutionThumbs != null && i < evolutionThumbs.Length && evolutionThumbs[i] != null)
                {
                    Image thumb = evolutionThumbs[i];

                    // Le "niveau 0" d'un Tank, c'est la case libre ou il sera pose.
                    Sprite photo = (subject == MNLTHII.UI.PhotoSubject.Tank && level == 0)
                                   ? TerrainPhoto(hex)
                                   : MNLTHII.UI.ElementPhotos.Get(subject, level);

                    if (photo != null)
                    {
                        thumb.sprite = photo;
                        thumb.color = Color.white;
                    }
                    else
                    {
                        thumb.sprite = MNLTHII.UI.IconLibrary.Get(IconFor(subject));
                        thumb.color = new Color(tint.r, tint.g, tint.b, 0.45f);
                    }
                }

                if (evolutionOutlines != null && i < evolutionOutlines.Length && evolutionOutlines[i] != null)
                    evolutionOutlines[i].effectColor = tint;

                if (evolutionFrames != null && i < evolutionFrames.Length && evolutionFrames[i] != null)
                    evolutionFrames[i].color = new Color(tint.r, tint.g, tint.b, (level == current) ? 0.16f : 0.05f);

                if (evolutionLabels != null && i < evolutionLabels.Length && evolutionLabels[i] != null)
                {
                    TMPro.TextMeshProUGUI label = evolutionLabels[i];
                    label.color = tint;

                    // Un seul chiffre : il ne peut pas etre inverse par le RTL.
                    if (level == 0) label.text = evolutionNaturalLabel;
                    else label.SetText(evolutionLevelFormat, level);
                }
            }

            return evolutionHeight;
        }

        /// <summary>
        /// Quels niveaux montrer pour cette case, et lequel est l'actuel.
        ///   batiment : 0 (avant construction), 1, 2
        ///   Tank     : 1, 2   (une plaine libre : aucun actuel, le prochain est 1)
        ///   ennemi   : 1, 2, 3
        /// </summary>
        private static bool EvolutionFor(Hexagon hex, HexAction action,
                                         out MNLTHII.UI.PhotoSubject subject,
                                         out int first, out int last, out int current)
        {
            // Tank : 0 = la case libre (desert ou plaine), puis les niveaux 1 et 2.
            subject = MNLTHII.UI.PhotoSubject.Tank;
            first = 0; last = 2; current = 0;

            BoardController board = BoardController.instance;
            PawnController pawn = (board != null) ? board.getPawnByCoord(hex.positionInTheBoard) : null;

            switch (action.kind)
            {
                case HexActionKind.TankCreate:
                    return true;

                case HexActionKind.TankEvolve:
                case HexActionKind.StanceChange:
                    current = (pawn != null && pawn.level > 0) ? pawn.level : 1;
                    return true;

                case HexActionKind.Occupied:
                    subject = MNLTHII.UI.PhotoSubject.Enemy;
                    first = 1; last = 3;
                    current = (pawn != null && pawn.level > 0) ? pawn.level : 1;
                    return true;

                case HexActionKind.Build:
                case HexActionKind.MaxLevel:
                    if (!BuildingSubject(hex.type, out subject)) return false;
                    first = 0; last = 2;
                    current = hex.level;
                    return true;
            }

            return false;
        }

        private static bool BuildingSubject(TypeOfHex type, out MNLTHII.UI.PhotoSubject subject)
        {
            subject = MNLTHII.UI.PhotoSubject.Bunker;

            switch (type)
            {
                case TypeOfHex.hill: subject = MNLTHII.UI.PhotoSubject.Bunker; return true;
                case TypeOfHex.gas: subject = MNLTHII.UI.PhotoSubject.GasFactory; return true;
                case TypeOfHex.crystal: subject = MNLTHII.UI.PhotoSubject.CrystalFactory; return true;
                case TypeOfHex.mountain: subject = MNLTHII.UI.PhotoSubject.CommandCenter; return true;
            }
            return false;
        }

        private static MNLTHII.UI.IconKind IconFor(MNLTHII.UI.PhotoSubject subject)
        {
            switch (subject)
            {
                case MNLTHII.UI.PhotoSubject.Tank: return MNLTHII.UI.IconKind.Tank;
                case MNLTHII.UI.PhotoSubject.Enemy: return MNLTHII.UI.IconKind.Enemy;
                case MNLTHII.UI.PhotoSubject.Bunker: return MNLTHII.UI.IconKind.Bunker;
                case MNLTHII.UI.PhotoSubject.GasFactory: return MNLTHII.UI.IconKind.Factory;
                case MNLTHII.UI.PhotoSubject.CrystalFactory: return MNLTHII.UI.IconKind.Crystal;
                case MNLTHII.UI.PhotoSubject.CommandCenter: return MNLTHII.UI.IconKind.Command;
            }
            return MNLTHII.UI.IconKind.None;
        }

        /// <summary>
        /// Quelle photo pour quelle action : ce que la case DEVIENDRA si on clique (le
        /// batiment au niveau vise, le Tank qu'on pose), ou ce qui l'occupe deja.
        /// </summary>
        private Sprite PhotoFor(Hexagon hex, HexAction action)
        {
            BoardController board = BoardController.instance;
            PawnController pawn = (board != null) ? board.getPawnByCoord(hex.positionInTheBoard) : null;

            switch (action.kind)
            {
                case HexActionKind.TankCreate:
                {
                    // La case TELLE QU'ELLE EST (desert, plaine) ; sans photo, le Tank.
                    Sprite ground = TerrainPhoto(hex);
                    return (ground != null) ? ground : MNLTHII.UI.ElementPhotos.Get(MNLTHII.UI.PhotoSubject.Tank, 1);
                }

                case HexActionKind.TankEvolve:
                    // Montrer ce qui EST la : le Tank a son niveau actuel. En mode
                    // "resultat", le Tank Niveau 2 qu'on obtiendra.
                    return MNLTHII.UI.ElementPhotos.Get(MNLTHII.UI.PhotoSubject.Tank,
                                                        photoShowsResult ? 2
                                                        : ((pawn != null && pawn.level > 0) ? pawn.level : 1));

                case HexActionKind.StanceChange:
                    return MNLTHII.UI.ElementPhotos.Get(MNLTHII.UI.PhotoSubject.Tank,
                                                        (pawn != null && pawn.level > 0) ? pawn.level : 1);

                case HexActionKind.Occupied:
                    return MNLTHII.UI.ElementPhotos.Get(MNLTHII.UI.PhotoSubject.Enemy,
                                                        (pawn != null && pawn.level > 0) ? pawn.level : 1);

                case HexActionKind.Build:
                    // Par defaut la case TELLE QU'ELLE EST : un gaz brut montre gas_0, une
                    // usine de gaz Niveau 1 montre gas_1. En mode "resultat", ce que le
                    // clic va construire.
                    return BuildingPhoto(hex.type, photoShowsResult ? action.targetLevel : hex.level);

                case HexActionKind.MaxLevel:
                    return BuildingPhoto(hex.type, hex.level);
            }

            return null;
        }

        /// <summary>
        /// La photo de la case nue : desert_0 / plaine_0, ou le terrain au niveau 0
        /// (gaz brut...) si un Tank se tient sur une case constructible.
        /// </summary>
        private static Sprite TerrainPhoto(Hexagon hex)
        {
            if (hex == null) return null;

            switch (hex.type)
            {
                case TypeOfHex.desert: return MNLTHII.UI.ElementPhotos.Get(MNLTHII.UI.PhotoSubject.Desert, 0);
                case TypeOfHex.plain: return MNLTHII.UI.ElementPhotos.Get(MNLTHII.UI.PhotoSubject.Plain, 0);
            }
            return BuildingPhoto(hex.type, 0);
        }

        private static Sprite BuildingPhoto(TypeOfHex type, int level)
        {
            switch (type)
            {
                case TypeOfHex.hill: return MNLTHII.UI.ElementPhotos.Get(MNLTHII.UI.PhotoSubject.Bunker, level);
                case TypeOfHex.gas: return MNLTHII.UI.ElementPhotos.Get(MNLTHII.UI.PhotoSubject.GasFactory, level);
                case TypeOfHex.crystal: return MNLTHII.UI.ElementPhotos.Get(MNLTHII.UI.PhotoSubject.CrystalFactory, level);
                case TypeOfHex.mountain: return MNLTHII.UI.ElementPhotos.Get(MNLTHII.UI.PhotoSubject.CommandCenter, level);
            }
            return null;
        }

        /// <summary>
        /// Le pictogramme d'entete, et sa couleur. Les memes que dans la legende de
        /// l'ecran d'ouverture : un joueur qui a lu la legende reconnait la case sans
        /// lire un mot.
        /// </summary>
        private void FillIcon(Hexagon hex, HexAction action)
        {
            if (kindIcon == null) return;

            MNLTHII.UI.IconKind kind = MNLTHII.UI.IconKind.None;
            Color tint = Color.white;

            switch (action.kind)
            {
                case HexActionKind.TankCreate:
                case HexActionKind.TankEvolve:
                case HexActionKind.StanceChange:
                    kind = MNLTHII.UI.IconKind.Tank;
                    tint = iconTank;
                    break;

                case HexActionKind.Build:
                    switch (hex.type)
                    {
                        case TypeOfHex.hill: kind = MNLTHII.UI.IconKind.Bunker; tint = iconBunker; break;
                        case TypeOfHex.gas: kind = MNLTHII.UI.IconKind.Factory; tint = iconGas; break;
                        case TypeOfHex.crystal: kind = MNLTHII.UI.IconKind.Crystal; tint = iconCrystal; break;
                        case TypeOfHex.mountain: kind = MNLTHII.UI.IconKind.Command; tint = iconCommand; break;
                    }
                    break;

                case HexActionKind.Occupied:
                    kind = MNLTHII.UI.IconKind.Enemy;
                    tint = iconDanger;
                    break;
            }

            bool show = (kind != MNLTHII.UI.IconKind.None);

            if (kindIcon.gameObject.activeSelf != show) kindIcon.gameObject.SetActive(show);
            if (!show) return;

            kindIcon.sprite = MNLTHII.UI.IconLibrary.Get(kind);
            kindIcon.color = tint;
        }

        /// <summary>
        /// La phrase du bas : ce que ce batiment APPORTE. Vide, la ligne disparait -
        /// une zone de texte vide qui garde sa place fait croire a un bug d'affichage.
        /// </summary>
        private void FillEffect(Hexagon hex, HexAction action)
        {
            if (effectText == null) return;

            string phrase = "";

            switch (action.kind)
            {
                case HexActionKind.Build:
                    switch (hex.type)
                    {
                        // Bunker et Centre : texte a jour lu dans le JSON (usure, deux
                        // zones), sinon celui pose par le HudBuilder.
                        case TypeOfHex.hill: phrase = MNLTHII.UI.HudLabelsRuntime.Get("bunkerEffect", effectBunker); break;
                        case TypeOfHex.gas: phrase = effectGas; break;
                        case TypeOfHex.crystal: phrase = effectCrystal; break;
                        case TypeOfHex.mountain: phrase = MNLTHII.UI.HudLabelsRuntime.Get("mountainEffect", effectMountain); break;
                    }
                    break;

                // Deja au maximum : on dit quand meme ce que le batiment FAIT.
                case HexActionKind.MaxLevel:
                    switch (hex.type)
                    {
                        case TypeOfHex.hill: phrase = MNLTHII.UI.HudLabelsRuntime.Get("bunkerEffect", effectBunker); break;
                        case TypeOfHex.gas: phrase = effectGas; break;
                        case TypeOfHex.crystal: phrase = effectCrystal; break;
                        case TypeOfHex.mountain: phrase = MNLTHII.UI.HudLabelsRuntime.Get("mountainEffect", effectMountain); break;
                    }
                    break;

                case HexActionKind.TankCreate: phrase = effectTankCreate; break;
                case HexActionKind.TankEvolve: phrase = effectTankEvolve; break;
            }

            bool show = !string.IsNullOrEmpty(phrase);

            if (effectText.gameObject.activeSelf != show) effectText.gameObject.SetActive(show);
            if (show) effectText.text = phrase;
        }

        private void FillCost(HexAction action)
        {
            bool free = action.cost <= 0;

            if (costRoot != null) costRoot.SetActive(!free);

            if (costText != null)
            {
                if (free) costText.text = labelFree;
                else costText.SetText("{0}", action.cost);

                costText.color = free ? freeColor
                               : (action.affordable ? affordableColor : tooExpensiveColor);
            }

            if (afterBalanceText == null) return;

            EnergyManager energy = EnergyManager.Instance;
            bool show = !free && energy != null && action.affordable;

            afterBalanceText.gameObject.SetActive(show);
            if (show) afterBalanceText.SetText("{0}", energy.CurrentEnergy - action.cost);
        }

        /// <summary>Jusqu'a trois lignes chiffrees, choisies selon ce que fait l'element.</summary>
        private void FillDetails(Hexagon hex, HexAction action)
        {
            ClearDetails();

            int level = action.targetLevel;

            // Batiment au maximum : ses chiffres a lui, au niveau ou il est.
            if (action.kind == HexActionKind.MaxLevel) level = hex.level;

            switch (action.kind)
            {
                case HexActionKind.Build:
                case HexActionKind.MaxLevel:
                    switch (hex.type)
                    {
                        case TypeOfHex.hill:
                            SetDetail(0, labelShotsPerTurn, InteractionRules.GetBunkerTargets(level));
                            SetDetail(1, labelRange, InteractionRules.BUNKER_RANGE);
                            SetDetail(2, labelShotCost, InteractionRules.GetBunkerShotCost(level));
                            break;

                        // L'USINE : tout son interet tient en un nombre, son revenu.
                        // Les PV sont la deuxieme ligne parce qu'ils disent combien de
                        // temps elle tiendra quand l'ennemi viendra la chercher.
                        case TypeOfHex.gas:
                            SetDetail(0, labelIncome, InteractionRules.GetGasIncome(level));
                            SetDetail(1, labelHitPoints, InteractionRules.GetBuildingMaxHP(hex.type, level));
                            break;

                        // LE SOUTIEN : aucun revenu, que du militaire.
                        case TypeOfHex.crystal:
                            SetDetail(0, labelHeal, InteractionRules.GetCrystalHeal(level));
                            SetDetail(1, labelMaxHpBonus, InteractionRules.CRYSTAL_BONUS_MAXHP);
                            SetDetail(2, labelRange, InteractionRules.GetCrystalRange(level));
                            break;

                        case TypeOfHex.mountain:
                            // Les deux zones dessinees sur le plateau : la pleine
                            // (infranchissable) et la legere (commandement). Le +1 case
                            // de mouvement est dit dans la phrase du bas.
                            SetDetail(0, MNLTHII.UI.HudLabelsRuntime.Get("repelRadius", labelMoveBonus),
                                      InteractionRules.GetMountainRepel(level));
                            SetDetail(1, labelCommandRadius, InteractionRules.GetMountainCommandRadius(level));
                            SetDetail(2, labelDecay, InteractionRules.MOUNTAIN_DECAY_PER_TURN);
                            break;
                    }
                    break;

                case HexActionKind.TankCreate:
                    SetDetail(0, labelHitPoints, 30);
                    SetDetail(1, labelDamage, 10);
                    SetDetail(2, labelRange, 1);
                    break;

                case HexActionKind.TankEvolve:
                    SetDetail(0, labelHitPoints, 60);
                    SetDetail(1, labelDamage, 20);
                    SetDetail(2, labelRange, 2);
                    break;
            }
        }

        private void SetDetail(int index, string label, int value)
        {
            if (string.IsNullOrEmpty(label)) return;
            if (detailLabels == null || index >= detailLabels.Length) return;

            if (detailLabels[index] != null)
            {
                detailLabels[index].gameObject.SetActive(true);
                detailLabels[index].text = label;
            }
            if (detailValues != null && index < detailValues.Length && detailValues[index] != null)
            {
                detailValues[index].gameObject.SetActive(true);
                detailValues[index].SetText("{0}", value);
            }
        }

        private void ClearDetails()
        {
            if (detailLabels != null)
                for (int i = 0; i < detailLabels.Length; i++)
                    if (detailLabels[i] != null) detailLabels[i].gameObject.SetActive(false);

            if (detailValues != null)
                for (int i = 0; i < detailValues.Length; i++)
                    if (detailValues[i] != null) detailValues[i].gameObject.SetActive(false);
        }

        private string TitleFor(Hexagon hex, HexActionKind kind)
        {
            switch (kind)
            {
                case HexActionKind.TankCreate: return labelTankCreate;
                case HexActionKind.TankEvolve: return labelTankEvolve;
                case HexActionKind.StanceChange: return labelStanceChange;
                case HexActionKind.MaxLevel: return labelMaxLevel;
                case HexActionKind.Occupied: return labelOccupied;

                case HexActionKind.Build:
                    switch (hex.type)
                    {
                        case TypeOfHex.hill: return labelBunker;
                        case TypeOfHex.gas: return labelGas;
                        case TypeOfHex.crystal: return labelCrystal;
                        case TypeOfHex.mountain: return labelMountain;
                    }
                    return string.Empty;
            }
            return string.Empty;
        }

        private void Hide()
        {
            if (_detailsOpen) _closedFrame = Time.frameCount;

            _current = null;
            _detailsOpen = false;
            _confirmActive = false;
            _confirmHex = null;
            if (_confirmRow != null && _confirmRow.gameObject.activeSelf) _confirmRow.gameObject.SetActive(false);
            if (panelRoot != null) panelRoot.SetActive(false);
            if (detailsDim != null && detailsDim.activeSelf) detailsDim.SetActive(false);
        }

        // =================================================================
        //  MODE CLIC : le panneau de details
        // =================================================================
        /// <summary>
        /// Ouvre les details d'une case, la ou l'on a clique. Appele par le bouton
        /// "details" du menu de case. Faux s'il n'y a rien a montrer.
        /// </summary>
        public bool ShowDetails(Hexagon hex)
        {
            _confirmActive = false;
            _confirmHex = null;
            if (_confirmRow != null && _confirmRow.gameObject.activeSelf) _confirmRow.gameObject.SetActive(false);

            return OpenDetails(hex);
        }

        /// <summary>
        /// Le meme panneau, en CONFIRMATION : on y voit tous les niveaux (l'actuel en
        /// cyan, celui qu'on va atteindre en or), et deux boutons en bas - Confirmer,
        /// avec le prix, et Annuler. Appele par "utiliser l'energie" du menu de case,
        /// pour une construction. Faux si le panneau ne peut pas s'ouvrir : l'appelant
        /// agit alors directement, comme avant.
        /// </summary>
        public bool ShowConfirm(Hexagon hex)
        {
            if (hex == null || panelRoot == null) return false;
            if (!EnsureConfirmRow()) return false;

            _confirmActive = true;
            _confirmHex = hex;

            if (!OpenDetails(hex))
            {
                _confirmActive = false;
                _confirmHex = null;
                return false;
            }

            // OpenDetails ne remet pas _confirmActive a faux : Fill l'a deja compte
            // dans la hauteur du panneau.
            _confirmActive = true;
            _confirmHex = hex;
            FillConfirm(hex);
            _confirmRow.gameObject.SetActive(true);
            _confirmRow.SetAsLastSibling();
            return true;
        }

        private bool OpenDetails(Hexagon hex)
        {
            if (hex == null || panelRoot == null) return false;

            _current = hex;
            Fill(hex);

            if (!panelRoot.activeSelf) { _current = null; return false; }

            _detailsOpen = true;
            if (detailsDim != null) detailsDim.SetActive(true);

            if (_rect != null)
                _rect.position = detailsCentered ? CenteredPosition() : ClampToScreen((Vector2)Input.mousePosition);

            return true;
        }

        /// <summary>
        /// Position du coin haut-gauche (le pivot du panneau) pour que le panneau soit
        /// au milieu de l'ecran, quelle que soit sa hauteur du moment (avec ou sans photo).
        /// </summary>
        private Vector2 CenteredPosition()
        {
            Vector3 scale = _rect.lossyScale;
            float width = _rect.rect.width * scale.x;
            float height = _rect.rect.height * scale.y;

            Vector2 pivot = _rect.pivot;
            float x = Screen.width * 0.5f - width * (0.5f - pivot.x);
            float y = Screen.height * 0.5f - height * (0.5f - pivot.y);
            return new Vector2(x, y);
        }

        // -----------------------------------------------------------------
        //  RANGEE DE CONFIRMATION (construite une fois, au premier besoin)
        // -----------------------------------------------------------------
        /// <summary>
        /// Construite en code, pas par le HudBuilder : pas besoin de reconstruire le
        /// HUD. Les textes sont des copies du titre du panneau, donc ils gardent sa
        /// police hebraique et ses reglages RTL.
        /// </summary>
        private bool EnsureConfirmRow()
        {
            if (_confirmRow != null) return true;
            if (panelRoot == null || titleText == null) return false;

            GameObject rowGo = new GameObject("Confirmation", typeof(RectTransform));
            rowGo.layer = panelRoot.layer;
            _confirmRow = (RectTransform)rowGo.transform;
            _confirmRow.SetParent(panelRoot.transform, false);
            _confirmRow.anchorMin = new Vector2(0f, 0f);
            _confirmRow.anchorMax = new Vector2(1f, 0f);
            _confirmRow.pivot = new Vector2(0.5f, 0f);
            _confirmRow.anchoredPosition = Vector2.zero;
            _confirmRow.sizeDelta = new Vector2(0f, confirmRowHeight);

            // RTL : l'action principale a droite, l'annulation a gauche.
            _confirmButton = MakeConfirmButton("Confirmer", 0.40f, 1f, confirmColor, out _confirmBg);
            Image cancelBg;
            _confirmCancelButton = MakeConfirmButton("Annuler", 0f, 0.36f, confirmCancelColor, out cancelBg);

            _confirmText = MakeConfirmLabel(_confirmButton.transform, true, TMPro.TextAlignmentOptions.Right,
                                            new Vector2(70f, 0f), new Vector2(-16f, 0f));
            _confirmCost = MakeConfirmLabel(_confirmButton.transform, false, TMPro.TextAlignmentOptions.Left,
                                            new Vector2(14f, 0f), new Vector2(-150f, 0f));
            _confirmCancelText = MakeConfirmLabel(_confirmCancelButton.transform, true, TMPro.TextAlignmentOptions.Center,
                                                  new Vector2(8f, 0f), new Vector2(-8f, 0f));

            _onConfirm = OnConfirmClicked;
            _onConfirmCancel = OnConfirmCancelled;
            _confirmButton.onClick.AddListener(_onConfirm);
            _confirmCancelButton.onClick.AddListener(_onConfirmCancel);

            rowGo.SetActive(false);
            return true;
        }

        private Button MakeConfirmButton(string name, float xMin, float xMax, Color color, out Image background)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = panelRoot.layer;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(_confirmRow, false);
            rect.anchorMin = new Vector2(xMin, 0f);
            rect.anchorMax = new Vector2(xMax, 1f);
            rect.offsetMin = new Vector2(18f, 16f);
            rect.offsetMax = new Vector2(-18f, -10f);

            background = go.AddComponent<Image>();
            background.color = color;

            UnityEngine.UI.Outline edge = go.AddComponent<UnityEngine.UI.Outline>();
            edge.effectColor = new Color(color.r, color.g, color.b, 0.6f);
            edge.effectDistance = new Vector2(1f, 1f);

            Button button = go.AddComponent<Button>();
            button.targetGraphic = background;
            return button;
        }

        private TMPro.TextMeshProUGUI MakeConfirmLabel(Transform parent, bool rightToLeft,
                                                       TMPro.TextAlignmentOptions align,
                                                       Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject go = Instantiate(titleText.gameObject, parent, false);
            go.name = "Libelle";

            TMPro.TextMeshProUGUI text = go.GetComponent<TMPro.TextMeshProUGUI>();
            RectTransform rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            text.text = "";
            text.isRightToLeftText = rightToLeft;
            text.alignment = align;
            text.fontSize = 26f;
            text.fontStyle = TMPro.FontStyles.Bold;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            return text;
        }

        private void FillConfirm(Hexagon hex)
        {
            HexAction action = HexAction.Resolve(hex);

            bool usable = action.kind == HexActionKind.Build;
            bool canPay = usable && action.affordable;

            if (_confirmButton != null) _confirmButton.interactable = canPay;
            if (_confirmBg != null) _confirmBg.color = canPay ? confirmColor : confirmDisabledColor;

            if (_confirmText != null)
            {
                // "Confirmer - niveau {0}" : le niveau qu'on va ATTEINDRE (1 ou 2).
                _confirmText.SetText(MNLTHII.UI.HudLabelsRuntime.Get("confirmUpgrade", "{0}"), action.targetLevel);
                _confirmText.color = canPay ? affordableColor : tooExpensiveColor;
            }

            if (_confirmCost != null)
            {
                _confirmCost.SetText("{0}", action.cost);
                _confirmCost.color = action.affordable ? affordableColor : tooExpensiveColor;
            }

            if (_confirmCancelText != null)
            {
                _confirmCancelText.text = MNLTHII.UI.HudLabelsRuntime.Get("confirmCancel", "X");
                _confirmCancelText.color = new Color(0.90f, 0.93f, 1f);
            }
        }

        private void OnConfirmClicked()
        {
            Hexagon hex = _confirmHex;
            Hide();

            TurnManager turns = TurnManager.Instance;
            if (hex != null && turns != null) turns.ExecuteSpendingAction(hex);
        }

        private void OnConfirmCancelled()
        {
            Hide();
        }

        public void HideDetails()
        {
            if (_detailsOpen || (panelRoot != null && panelRoot.activeSelf)) Hide();
        }

        /// <summary>
        /// Sondage silencieux pour HoveredHex (vingt fois par seconde, sans rien
        /// afficher). Rien pendant une vue immersive ni hors phase de depense.
        /// </summary>
        private void ProbeHoverOnly()
        {
            TurnManager turns = TurnManager.Instance;
            if (turns == null || !turns.IsSpendingPhase || ImmersiveCamera.BlocksBoardInput)
            {
                _hoverProbe = null;
                return;
            }

            if (Time.unscaledTime < _nextProbe) return;
            _nextProbe = Time.unscaledTime + probeInterval;

            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null) return;
            }

            _hoverProbe = HexUnderCursor();
        }

        private void UpdateDetails()
        {
            if (!_detailsOpen) return;

            TurnManager turns = TurnManager.Instance;
            HexActionMenu menu = HexActionMenu.Instance;

            // Le menu s'est rouvert (clic sur une autre case), la phase est finie, ou la
            // camera est descendue : le panneau n'a plus rien a faire la.
            if (turns == null || !turns.IsSpendingPhase
                || (menu != null && menu.IsOpen)
                || ImmersiveCamera.BlocksBoardInput
                || _current == null)
            {
                Hide();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape)) { Hide(); return; }

            // Un clic hors du panneau le ferme.
            if ((Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
                && _rect != null
                && !RectTransformUtility.RectangleContainsScreenPoint(_rect, Input.mousePosition, null))
            {
                Hide();
            }
        }
    }
}

// ---------------------------------------------------------------------------
// NOTE D'OPTIMISATION
//
// 1. Le sondage tourne vingt fois par seconde, pas a chaque frame : vingt fois
//    moins de travail physique, et l'oeil ne voit aucune difference sur un survol.
// 2. Physics.RaycastNonAlloc ecrit dans un tampon membre de quatre elements, avec
//    masque de couches et distance maximale : cout borne, zero allocation par
//    sondage. Physics.RaycastAll aurait alloue un tableau a chaque appel.
// 3. Le panneau n'est rempli QUE si l'hexagone survole a change (comparaison de
//    reference). Glisser le long d'une meme case ne coute donc rien.
// 4. HexAction est une struct : la interroger ne cree aucun dechet, meme vingt fois
//    par seconde.
// 5. SetText("{0}", value) ecrit dans le buffer interne de TMP ; .text = x.ToString()
//    aurait alloue une string a chaque survol.
// 6. Camera.main est resolue une fois dans Awake : elle fait un
//    FindGameObjectsWithTag a chaque acces.
// 7. CompareTag ne cree pas de string, contrairement a tag == "Hexagon".
// ---------------------------------------------------------------------------
