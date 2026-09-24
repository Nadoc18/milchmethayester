using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Une case du plateau. Voir InteractionRules pour la convention de niveau :
/// pour les terrains, level 0 = "Niveau 1" du GDD (naturel).
///
/// Composant instancie 169 fois : toutes ses references sont resolues une seule
/// fois dans Awake, et l'aura est un objet persistant que l'on active/desactive
/// au lieu d'etre cree puis detruit a chaque rafraichissement.
/// </summary>
public class Hexagon : MonoBehaviour
{
    #region Public Field

    [Header("------- UX / UI ---------")]
    public GameObject hoverZoneFX;
    public GameObject hoverLockedFX;
    public GameObject hoverPatternFX;

    [Header("Energy Data ")]
    [Space(3)]
    public int energy;
    public int energymax;
    public int maxHP = 100;
    public int currentHP = 100;

    [Header("Type of ")]
    [Space(3)]
    public TypeOfHex type = TypeOfHex.None;
    public TypeOfPawn pawn = TypeOfPawn.None;

    [Header("CP Data (obsolete - remplace par l'Energie globale) ")]
    [Space(3)]
    public double commandPoints = 0;
    public double threshold = 0;

    [Header("Level and Model ")]
    [Space(3)]
    public int level = 1;
    public string buildingType;
    [Space(3)]
    public GameObject prefab;
    public GameObject pawnPrefab;

    [Header("Portail : nombre de tours ecoules depuis sa creation")]
    public int turnsAlive = 0;

    /// <summary>
    /// LA BULLE D'UN CENTRE DE COMMANDEMENT. Les degats ennemis y passent d'abord ;
    /// tant qu'elle tient, le batiment ne prend rien. Voir
    /// InteractionRules.FilterMountainDamage.
    ///
    /// Porte ici, sur l'hexagone, et non dans un gestionnaire a part comme l'etat des
    /// Shofars : un Centre ne survit pas a sa propre destruction - sa case redevient
    /// une montagne nue - donc il n'y a rien a conserver apres lui. Le bouclier d'un
    /// Shofar, lui, doit survivre au remplacement de l'objet par son epave, et c'est
    /// pour cela seulement qu'il vit dans PortalManager.
    /// </summary>
    [Header("Centre de Commandement : sa bulle")]
    public int shieldHP = 0;
    public int shieldMax = 0;

    public HexCoord positionInTheBoard;
    public HexCoord attackCoord;
    public Transform transformToAttack;
    public bool attacked = false;

#if UNITY_EDITOR
    [Header("Position (q,r,s) ")]
    [Space(3)]
    public Vector3Int BoardCoordforEditor;
    public Vector3Int AttackCoordforEditor;
#endif

    #endregion

    #region Caches

    // Resolus dans Awake : aucun GetComponentInChildren ni transform.Find(string)
    // pendant la partie.
    private Transform _transform;
    private MeshRenderer _meshRenderer;
    private MaterialPropertyBlock _propertyBlock;

    private SpriteRenderer _auraRenderer;
    private bool _auraBuilt;

    // Cumul des zones d'influence qui couvrent cette case : une case peut etre a la
    // fois dans le rayon d'un Gaz et d'un Cristal, la teinte doit le montrer.
    private Color _auraAccum;
    private int _auraCount;

    // Poids total et opacite la plus forte des zones posees sur cette case : une
    // zone "legere" (alpha < 1, ex. le rayon de commandement) se voit en
    // transparence, et une zone pleine qui la recouvre garde sa couleur.
    private float _auraWeight;
    private float _auraAlpha;

    private Coroutine _hitRoutine;
    private MNLTHII.UI.FloatingHealthBar _healthBar;

    // Une seule instance partagee : "new WaitForSeconds" dans une coroutine alloue
    // a chaque demarrage.
    private static readonly WaitForSeconds WaitHitFlash = new WaitForSeconds(0.1f);

    // Transform local du sprite d'aura, cale sur l'orientation des hexagones.
    private static readonly Vector3 AuraLocalPosition = new Vector3(0f, 0.35f, 0f);
    private static readonly Vector3 AuraLocalEuler = new Vector3(90f, 0f, 30f);
    private static readonly Vector3 AuraLocalScale = new Vector3(0.1f, 0.1f, 0.1f);

    // Identifiants de proprietes de shader, calcules une fois pour toutes.
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    #endregion

    public bool IsBuilt { get { return level > 0; } }

    private void Awake()
    {
        _transform = transform;
        _meshRenderer = GetComponentInChildren<MeshRenderer>();
        _propertyBlock = new MaterialPropertyBlock();
    }

    /// <summary>
    /// Start, et pas Awake : la fabrique ajoute ce composant PUIS renseigne le type et
    /// le niveau (Init, ou ReplaceWithDestroyedVisual). Au Start, la case sait enfin ce
    /// qu'elle est - terrain a calmer, ou batiment a souligner.
    /// </summary>
    private void Start()
    {
        MNLTHII.Managers.BoardReadability.NotifyHexReady(this);
    }

    public Hexagon Init(HexagonData p_hexData, GameObject p_prefab)
    {
        level = p_hexData.level;
        type = p_hexData.typeID;
        commandPoints = p_hexData.CP;

        energy = p_hexData.energy;
        if (p_hexData.energy > 0) energymax = p_hexData.energy;

        attackCoord = p_hexData.attack;
        threshold = p_hexData.threshold;

        positionInTheBoard = new HexCoord(p_hexData.to.q, p_hexData.to.r, p_hexData.to.s);
#if UNITY_EDITOR
        BoardCoordforEditor.x = p_hexData.to.q;
        BoardCoordforEditor.y = p_hexData.to.r;
        BoardCoordforEditor.z = p_hexData.to.s;
        if (p_hexData.attack != null)
        {
            AttackCoordforEditor.x = p_hexData.attack.q;
            AttackCoordforEditor.y = p_hexData.attack.r;
            AttackCoordforEditor.z = p_hexData.attack.s;
        }
#endif

        buildingType = CheckBuilding(p_hexData);
        prefab = p_prefab;
        attacked = p_hexData.attack_type != null;

        SyncHPFromRules();
        // La barre n'est PAS creee ici : Init() est appele pour les 169 cases pendant
        // la construction du plateau, et instancier un GameObject a ce moment-la faisait
        // planter la coroutine FillBoard. BoardController.RefreshStructureHealthBars()
        // s'en charge une fois le plateau termine.
        return this;
    }

    public void UpdateData(HexagonData p_hexData)
    {
        commandPoints = p_hexData.CP;
        energy = p_hexData.energy;
        threshold = p_hexData.threshold;
        attackCoord = p_hexData.attack;

        if (energymax <= 0) energymax = p_hexData.energy;
        attacked = p_hexData.attack_type != null;

        SyncHPFromRules();
    }

    /// <summary>
    /// Cale maxHP / currentHP / energy sur le bareme du GDD (Base 200, Portail 50 ou 100,
    /// Bunker 40 ou 80, Centre de Com. 30 ou 50). Les terrains nus n'ont pas de PV.
    /// </summary>
    public void SyncHPFromRules()
    {
        int hp = MNLTHII.Rules.InteractionRules.GetBuildingMaxHP(type, level);
        if (hp <= 0)
        {
            maxHP = 0;
            currentHP = 0;
            return;
        }

        maxHP = hp;
        if (currentHP <= 0 || currentHP > hp) currentHP = hp;
        if (energy <= 0 || energy > hp) energy = currentHP;
        else currentHP = energy;
        energymax = hp;
    }

    public void ApplyDamage(int amount)
    {
        if (amount <= 0) return;

        currentHP -= amount;
        if (currentHP < 0) currentHP = 0;
        energy = currentHP;

        FlashHit();
        RefreshHealthBar();

        // Le chiffre au-dessus de la structure. Il compte double sur les Shofars :
        // leur bouclier divise les degats par deux tant qu'il tient, et sans nombre
        // a l'ecran, frapper un Shofar protege et un Shofar ebranle donne exactement
        // la meme image. Toute la boucle centrale du jeu devenait invisible.
        //
        // amount est ici le degat REELLEMENT encaisse, apres filtrage du bouclier :
        // c'est bien ce qu'il faut montrer, pas ce qui avait ete envoye.
        MNLTHII.UI.DamagePopup.Show(transform.position, amount,
                                    (type == TypeOfHex.portal)
                                        ? MNLTHII.UI.DamageKind.DealtToEnemy
                                        : MNLTHII.UI.DamageKind.TakenByPlayer);
    }

    /// <summary>
    /// Affiche l'etat de la structure. Les Portails gardent leur barre en permanence,
    /// pour qu'on suive leur destruction d'un coup d'oeil ; les autres structures ne
    /// l'affichent qu'au moment ou elles encaissent.
    /// </summary>
    public void RefreshHealthBar()
    {
        if (maxHP <= 0) return;

        // Seules les structures qui se detruisent ont une barre. Les usines et les
        // Cristaux en font partie depuis qu'ils ont des PV : sans barre, le joueur ne
        // verrait pas son economie se faire grignoter.
        if (type != TypeOfHex.portal && type != TypeOfHex.hill && type != TypeOfHex.mountain
            && type != TypeOfHex.gas && type != TypeOfHex.crystal) return;

        if (_healthBar == null)
        {
            _healthBar = gameObject.AddComponent<MNLTHII.UI.FloatingHealthBar>();
            // Hauteur de la barre au-dessus de la structure.
            _healthBar.Init(1.3f);
        }

        bool persistent = (type == TypeOfHex.portal);
        _healthBar.ShowAndSetHealth(currentHP, maxHP, true, persistent);
    }

    public bool CompareAttackCoord(HexCoord coord2)
    {
        if (coord2 != null && attackCoord != null)
            if (attackCoord.q == coord2.q && attackCoord.r == coord2.r && attackCoord.s == coord2.s)
                return true;

        return false;
    }

    private string CheckBuilding(HexagonData _hexdata)
    {
        switch (_hexdata.typeID)
        {
            case TypeOfHex.Base: return "Base";
            case TypeOfHex.hill: if (_hexdata.level >= 1) return "Bunker"; break;
            case TypeOfHex.gas: if (_hexdata.level >= 1) return "Prod"; break;
            case TypeOfHex.portal: return "Portal";
            case TypeOfHex.crystal: if (_hexdata.level >= 1) return "Prod"; break;
            case TypeOfHex.mountain: if (_hexdata.level >= 1) return "CC"; break;
        }
        return "None";
    }

    // =====================================================================
    //  FEEDBACK DE DEGAT
    // =====================================================================
    /// <summary>
    /// Flash rouge via MaterialPropertyBlock : lire .material cree une copie
    /// du materiau (allocation + un draw call de plus), pas SetPropertyBlock.
    /// </summary>
    private void FlashHit()
    {
        if (_meshRenderer == null) return;

        if (_hitRoutine != null) StopCoroutine(_hitRoutine);
        _hitRoutine = StartCoroutine(HitFlashRoutine());
    }

    private IEnumerator HitFlashRoutine()
    {
        SetRendererColor(Color.red);
        yield return WaitHitFlash;
        SetRendererColor(Color.white);
        _hitRoutine = null;
    }

    private void SetRendererColor(Color color)
    {
        if (_meshRenderer == null) return;

        _meshRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetColor(ColorId, color);
        _propertyBlock.SetColor(BaseColorId, color);
        _meshRenderer.SetPropertyBlock(_propertyBlock);
    }

    /// <summary>Conserve pour compatibilite avec les anciens SendMessage("Hitted").</summary>
    public IEnumerator Hitted()
    {
        SetRendererColor(Color.red);
        yield return WaitHitFlash;
        SetRendererColor(Color.white);
    }

    // =====================================================================
    //  AURA DE ZONE D'INFLUENCE
    // =====================================================================
    /// <summary>
    /// L'aura est construite une seule fois puis simplement activee/desactivee.
    /// L'ancienne version creait un GameObject + un SpriteRenderer et les detruisait
    /// a chaque rafraichissement, soit jusqu'a plusieurs centaines d'objets par tour.
    /// </summary>
    /// <summary>
    /// Ajoute une zone d'influence sur cette case. Les couleurs se melangent :
    /// une case couverte par deux usines est plus lumineuse qu'une case couverte par une.
    /// </summary>
    public void AddAura(Color color)
    {
        if (color.a <= 0f) return;

        float w = color.a > 1f ? 1f : color.a;

        _auraAccum.r += color.r * w;
        _auraAccum.g += color.g * w;
        _auraAccum.b += color.b * w;
        _auraWeight += w;
        if (w > _auraAlpha) _auraAlpha = w;
        _auraCount++;

        ApplyAura();
    }

    /// <summary>Conserve pour compatibilite : equivaut a une zone unique.</summary>
    public void SetAura(Color color)
    {
        if (color.a <= 0f) { ClearAura(); return; }

        _auraAccum = new Color(color.r, color.g, color.b, 0f);
        _auraWeight = 1f;
        _auraAlpha = 1f;
        _auraCount = 1;
        ApplyAura();
    }

    private void ApplyAura()
    {
        if (_auraCount <= 0) return;
        if (!_auraBuilt) BuildAura();
        if (_auraRenderer == null) return;

        float inv = (_auraWeight > 0.0001f) ? 1f / _auraWeight : 1f;
        Color c = new Color(_auraAccum.r * inv, _auraAccum.g * inv, _auraAccum.b * inv, 1f);

        c.a = (_auraAlpha > 0f) ? _auraAlpha : 1f;

        _auraRenderer.color = c;
        if (!_auraRenderer.enabled) _auraRenderer.enabled = true;
    }

    public void ClearAura()
    {
        _auraAccum = new Color(0f, 0f, 0f, 0f);
        _auraWeight = 0f;
        _auraAlpha = 0f;
        _auraCount = 0;
        if (_auraRenderer != null && _auraRenderer.enabled) _auraRenderer.enabled = false;
    }

    private void BuildAura()
    {
        _auraBuilt = true;

        if (_transform == null) _transform = transform;

        GameObject go = new GameObject("AuraSprite");
        Transform t = go.transform;
        t.SetParent(_transform, false);
        t.localPosition = AuraLocalPosition;
        t.localRotation = Quaternion.Euler(AuraLocalEuler);
        t.localScale = AuraLocalScale;

        _auraRenderer = go.AddComponent<SpriteRenderer>();
        _auraRenderer.enabled = false;

        MNLTHII.BoardController board = MNLTHII.BoardController.instance;
        // GetAuraSprite fournit le sprite de l'inspecteur, ou un marqueur genere
        // a la volee si aucun n'a ete assigne : la zone reste toujours lisible.
        if (board != null) _auraRenderer.sprite = board.GetAuraSprite();
    }
}

// ---------------------------------------------------------------------------
// NOTE D'OPTIMISATION
//
// 1. L'aura etait un GameObject cree par Instantiate puis Destroy a chaque appel de
//    RefreshAllAuras. Avec 169 hexagones et plusieurs rafraichissements par tour, cela
//    representait des centaines d'allocations et autant de travail pour le GC.
//    Elle est desormais construite une fois (paresseusement, seulement pour les cases
//    reellement couvertes) et le rafraichissement se limite a un booleen 'enabled'.
// 2. transform.Find("AuraSprite") faisait une recherche par chaine dans la hierarchie
//    a chaque appel : la reference est maintenant gardee dans _auraRenderer.
// 3. Le flash de degat lisait Renderer.material, ce qui duplique le materiau
//    (allocation, rupture du batching, fuite jusqu'au dechargement de la scene).
//    Remplace par un MaterialPropertyBlock reutilise et des identifiants de propriete
//    obtenus une fois via Shader.PropertyToID.
// 4. Les WaitForSeconds des coroutines sont des instances statiques partagees.
// 5. GetComponentInChildren<MeshRenderer>() est fait dans Awake, plus a chaque degat.
// ---------------------------------------------------------------------------
