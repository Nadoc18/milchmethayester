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
                    case TypeOfHex.plain:
                    case TypeOfHex.desert:
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

        /// <summary>
        /// Instance unique, pour que les autres panneaux puissent savoir ce qui est
        /// survole sans relancer un raycast de leur cote.
        /// </summary>
        public static HexTooltipController Instance { get; private set; }

        /// <summary>L'hexagone sous le curseur, ou null. Lecture seule.</summary>
        public Hexagon HoveredHex { get { return _current; } }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;

            if (panelRoot == null) panelRoot = gameObject;
            _rect = panelRoot.transform as RectTransform;

            // Camera.main declenche un FindGameObjectsWithTag : resolue une seule fois.
            _camera = Camera.main;

            Hide();
        }

        private void Update()
        {
            // Hors phase de depense, l'info-bulle n'a rien a dire.
            TurnManager turns = TurnManager.Instance;
            if (turns == null || !turns.IsSpendingPhase)
            {
                // On oublie l'hexagone survole : sinon Hide() serait rappele a chaque
                // frame, et HoveredHex mentirait aux panneaux qui le lisent.
                if (_current != null) { _current = null; Hide(); }
                return;
            }

            if (followCursor && _rect != null && _current != null)
                _rect.position = (Vector2)Input.mousePosition + cursorOffset;

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
            if (parent == null || !parent.CompareTag("Hexagon")) return null;

            return parent.GetComponent<Hexagon>();
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
                        case TypeOfHex.hill: phrase = effectBunker; break;
                        case TypeOfHex.gas: phrase = effectGas; break;
                        case TypeOfHex.crystal: phrase = effectCrystal; break;
                        case TypeOfHex.mountain: phrase = effectMountain; break;
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

            switch (action.kind)
            {
                case HexActionKind.Build:
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
                            // Le rayon infranchissable n'est PAS affiche : deux rayons
                            // sur la meme carte se confondraient. La phrase du bas dit
                            // qu'il barre le passage, sans chiffre.
                            SetDetail(0, labelCommandRadius, InteractionRules.GetMountainCommandRadius(level));
                            SetDetail(1, labelMoveBonus, InteractionRules.MOUNTAIN_MOVE_BONUS);
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
            _current = null;
            if (panelRoot != null) panelRoot.SetActive(false);
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
