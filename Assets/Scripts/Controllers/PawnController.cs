using DG.Tweening;
using System;
using System.Collections;
using UnityEngine;
using MNLTHII;

/// <summary>
/// Posture d'un Tank allie. Le joueur la choisit d'un clic, elle persiste d'un tour
/// a l'autre, et c'est elle qui dicte le choix de cible automatique.
///
///   Garde  : ne s'eloigne jamais de la Base, intercepte ce qui approche.
///   Assaut : marche sur les Portails, ignore ce qui ne le gene pas.
///   Chasse : fonce sur l'ennemi le plus proche, ou qu'il soit.
///
/// C'est le seul vrai levier tactique du joueur sur ses unites : il ne pilote pas
/// chaque deplacement, il donne une intention et la resolution reste automatique.
/// </summary>
public enum PawnStance
{
    Guard = 0,
    Assault = 1,
    Hunt = 2
}

/// <summary>
/// Un pion sur le plateau. GDD V3 : deux familles seulement, les Tanks du joueur
/// (TypeOfPawn.unit) et les ennemis (TypeOfPawn.enemy). Les stats viennent
/// exclusivement de InteractionRules.ApplyStatsToPawn.
///
/// Jusqu'a une vingtaine d'instances simultanees : tous les composants et les
/// delegues DOTween sont resolus une fois dans Awake.
/// </summary>
public class PawnController : MonoBehaviour
{
    public Transform target;
    public Transform[] rocketSpawner;
    public TypeOfPawn typeOfPawn = TypeOfPawn.None;
    public int level = 0;
    public bool UnlockDisplacement = false;
    public bool UnlockRotation = false;
    public bool attacked = false;

    public HexCoord hexcoord;
    public HexCoord targetCoord;
    public HexCoord attackCoord;
    public HexCoord NextCoord;

    public int energy;
    public int energymax;
    public int maxHP = 100;
    public int currentHP = 100;

    /// <summary>PV max "nus", sans le bonus de Cristal. Sert a recalculer le buff sans cumul.</summary>
    public int baseMaxHP = 30;

    public int attackDamageMin = 10;
    public int attackDamageMax = 10;
    public int attackRange = 1;
    public int moveRange = 1;
    public float critChance = 0.0f;
    public float dodgeChance = 0.0f;

    public Vector3Int BoardCoordforEditor;
    public Vector3Int targetCoordforEditor;
    public Material transparent;
    public bool fadeIn = false;
    public bool fadeOut = false;
    public float fadeSpeed = 8.0f;

    [Tooltip("Nombre de clignotements joues quand l'unite attaque.")]
    public int attackFlashCount = 3;

    [Tooltip("Nombre de clignotements joues quand l'unite encaisse un coup.")]
    public int damageFlashCount = 2;

    // =====================================================================
    //  POSTURE (Tanks allies uniquement)
    // =====================================================================
    [Header("Posture")]
    [Tooltip("Intention donnee au Tank. Un clic sur l'unite la fait tourner.")]
    public PawnStance stance = PawnStance.Guard;

    /// <summary>
    /// Ordre "rejoins un Cristal" : pendant la phase des Tanks, ce Tank marche vers
    /// le Cristal construit le plus proche au lieu de suivre sa posture, puis reste
    /// a son contact (il tire encore sur ce qui passe a portee) jusqu'a ce qu'on le
    /// fasse evoluer. Voir TurnManager.ProcessPlayerUnitsSequential.
    /// </summary>
    [System.NonSerialized] public bool seekCrystal;

    /// <summary>
    /// LA CIBLE CHOISIE A LA MAIN, selon le role (voir TargetPicker) :
    ///   Garde  : la case autour de laquelle ce Tank monte la garde ;
    ///   Assaut : le Shofar qu'il attaque.
    /// Null : le Tank decide seul, comme avant.
    /// </summary>
    [System.NonSerialized] public HexCoord orderTargetCoord;

    /// <summary>L'ennemi traque (role Chasse). Null : il choisit sa proie lui-meme.</summary>
    [System.NonSerialized] public PawnController orderTargetPawn;

    /// <summary>
    /// Portail qui a deploye cet ennemi. Stocke en deux int plutot qu'en HexCoord :
    /// HexCoord est une classe, et un ennemi sur deux naitrait avec une allocation.
    /// </summary>
    [HideInInspector] public int originPortalQ = int.MinValue;
    [HideInInspector] public int originPortalR = int.MinValue;

    public bool HasOriginPortal { get { return originPortalQ != int.MinValue; } }

    /// <summary>Teintes de posture, melangees a la couleur d'origine du modele.</summary>
    // Les teintes vivent dans MNLTHII.UI.StanceStyle, partagees avec l'ecran de
    // choix et la carte d'unite. Trois definitions separees finissaient forcement
    // par diverger, et le joueur ne savait plus quelle couleur veut dire quoi.

    public void SetOriginPortal(HexCoord coord)
    {
        if (coord == null) return;
        originPortalQ = coord.q;
        originPortalR = coord.r;
    }

    /// <summary>Garde -> Assaut -> Chasse -> Garde. Gratuit, et jouable a tout moment.</summary>
    public void CycleStance()
    {
        if (IsEnemy) return;

        switch (stance)
        {
            case PawnStance.Guard: stance = PawnStance.Assault; break;
            case PawnStance.Assault: stance = PawnStance.Hunt; break;
            default: stance = PawnStance.Guard; break;
        }

        Debug.LogFormat("[Tank] Posture ({0},{1}) -> {2}",
                        hexcoord != null ? hexcoord.q : 0,
                        hexcoord != null ? hexcoord.r : 0,
                        stance);

        ApplyStanceTint();

        if (MNLTHII.Managers.FXManager.Instance != null)
            MNLTHII.Managers.FXManager.Instance.SpawnEnergyBuffFX(transform.position);
    }

    public void SetStance(PawnStance newStance)
    {
        if (IsEnemy) return;
        stance = newStance;
        ApplyStanceTint();
    }

    /// <summary>Couleur de repos du pion : couleur d'origine teintee par la posture.</summary>
    private Color GetRestingColor(int index)
    {
        Color original = _originalColors[index];
        if (IsEnemy) return original;

        Color tint = MNLTHII.UI.StanceStyle.TintOf((int)stance);

        // Multiplication composante par composante : le modele garde sa matiere,
        // seule sa dominante change. Aucune allocation, Color est un struct.
        return new Color(original.r * tint.r, original.g * tint.g, original.b * tint.b, original.a);
    }

    /// <summary>Repeint le pion selon sa posture courante.</summary>
    public void ApplyStanceTint()
    {
        SetAllRenderersColor(false);
    }

    #region Caches

    private Transform _transform;
    private AudioSource _audioSource;
    private BoardController _board;
    private MNLTHII.UI.FloatingHealthBar _healthBar;

    // Renderers et couleurs d'origine releves une seule fois : le flash de degat
    // ne fait plus aucun GetComponentsInChildren ni allocation de dictionnaire.
    private Renderer[] _renderers;
    private Color[] _originalColors;
    private int[] _colorPropertyIds;
    private MaterialPropertyBlock _propertyBlock;

    private Coroutine _blinkRoutine;
    private Coroutine _attackFlashRoutine;

    // Delegues mis en cache : sans cela, chaque OnStart / OnComplete de DOTween
    // alloue une closure a chaque deplacement de chaque unite.
    private TweenCallback _onMoveCompleteCached;
    private TweenCallback _onAttackSfxCached;
    private TweenCallback _onMoveSfxCached;
    private Hexagon _pendingDestination;

    private static readonly WaitForSeconds WaitBlink = new WaitForSeconds(0.2f);

    // Clignotement d'attaque : eteint puis rallume, tres court.
    private static readonly WaitForSeconds WaitFlashOff = new WaitForSeconds(0.04f);
    private static readonly WaitForSeconds WaitFlashOn = new WaitForSeconds(0.06f);

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    #endregion

    public bool IsEnemy { get { return typeOfPawn == TypeOfPawn.enemy; } }

    private void Awake()
    {
        _transform = transform;
        _board = BoardController.instance;
        _audioSource = GetComponent<AudioSource>();
        _propertyBlock = new MaterialPropertyBlock();

        _onMoveCompleteCached = OnMoveComplete;
        _onAttackSfxCached = PlayAttackSfx;
        _onMoveSfxCached = PlayMoveSfx;

        CacheRenderers();
    }

    private void Start()
    {
        if (_board == null) _board = BoardController.instance;
        fadeIn = true;
        if (_audioSource == null) _audioSource = GetComponent<AudioSource>();

        // Un Tank neuf affiche immediatement sa posture de depart (Garde).
        if (!IsEnemy) ApplyStanceTint();

        // Ombre de contact, anneau d'equipe, lisere, taille : ce qui le fait ressortir
        // du plateau. Sans effet si BoardReadability est desactive.
        MNLTHII.Managers.BoardReadability.NotifyPawnReady(this);

        // La pastille qui nomme son role (Garde / Assaut / Chasse) au-dessus de lui.
        if (!IsEnemy && GetComponent<MNLTHII.UI.StanceMarker>() == null)
            gameObject.AddComponent<MNLTHII.UI.StanceMarker>();
    }

    private void CacheRenderers()
    {
        _renderers = GetComponentsInChildren<Renderer>();
        int count = _renderers.Length;

        _originalColors = new Color[count];
        _colorPropertyIds = new int[count];

        for (int i = 0; i < count; i++)
        {
            Renderer r = _renderers[i];
            // sharedMaterial : lire .material dupliquerait le materiau pour chaque pion.
            Material shared = r.sharedMaterial;

            if (shared != null && shared.HasProperty(ColorId))
            {
                _colorPropertyIds[i] = ColorId;
                _originalColors[i] = shared.GetColor(ColorId);
            }
            else if (shared != null && shared.HasProperty(BaseColorId))
            {
                _colorPropertyIds[i] = BaseColorId;
                _originalColors[i] = shared.GetColor(BaseColorId);
            }
            else
            {
                _colorPropertyIds[i] = 0;
                _originalColors[i] = Color.white;
            }
        }
    }

    // =====================================================================
    //  COORDONNEES
    // =====================================================================
    public bool ComparePawnCoord(HexCoord coord2)
    {
        if (coord2 != null && hexcoord != null)
            if (hexcoord.q == coord2.q && hexcoord.r == coord2.r && hexcoord.s == coord2.s)
                return true;

        return false;
    }

    public bool CompareAttackCoord(HexCoord coord2)
    {
        if (coord2 != null && attackCoord != null)
            if (attackCoord.q == coord2.q && attackCoord.r == coord2.r && attackCoord.s == coord2.s)
                return true;

        return false;
    }

    public void Init(HexagonData p_hexData)
    {
        hexcoord = new HexCoord(p_hexData.from.q, p_hexData.from.r, p_hexData.from.s);
        level = p_hexData.level;

#if UNITY_EDITOR
        BoardCoordforEditor.x = p_hexData.from.q;
        BoardCoordforEditor.y = p_hexData.from.r;
        BoardCoordforEditor.z = p_hexData.from.s;
#endif
        if (p_hexData.to != null) targetCoord = new HexCoord(p_hexData.to.q, p_hexData.to.r, p_hexData.to.s);
        if (p_hexData.attack != null) attackCoord = new HexCoord(p_hexData.attack.q, p_hexData.attack.r, p_hexData.attack.s);
        if (p_hexData.nextto != null) NextCoord = new HexCoord(p_hexData.nextto.q, p_hexData.nextto.r, p_hexData.nextto.s);

        attacked = p_hexData.attack_type != null;

        // Les stats du GDD ecrasent toute valeur heritee du JSON.
        MNLTHII.Rules.InteractionRules.ApplyStatsToPawn(this);

        if (_healthBar == null) _healthBar = gameObject.AddComponent<MNLTHII.UI.FloatingHealthBar>();
        _healthBar.Init();

        // Elle s'affiche tout de suite pour un Tank, pas seulement au premier coup recu.
        if (!IsEnemy) RefreshHealthBar();
    }

    public void UpdateCoord(HexCoord _newCoord)
    {
        hexcoord = new HexCoord(_newCoord.q, _newCoord.r, _newCoord.s);
#if UNITY_EDITOR
        BoardCoordforEditor.x = _newCoord.q;
        BoardCoordforEditor.y = _newCoord.r;
        BoardCoordforEditor.z = _newCoord.s;
#endif
    }

    public void UpdateData(HexagonData p_hexData)
    {
        level = p_hexData.level;
#if UNITY_EDITOR
        BoardCoordforEditor.x = p_hexData.from.q;
        BoardCoordforEditor.y = p_hexData.from.r;
        BoardCoordforEditor.z = p_hexData.from.s;
#endif
        if (p_hexData.to != null) targetCoord = new HexCoord(p_hexData.to.q, p_hexData.to.r, p_hexData.to.s);
        if (p_hexData.attack != null) attackCoord = new HexCoord(p_hexData.attack.q, p_hexData.attack.r, p_hexData.attack.s);
        if (p_hexData.nextto != null) NextCoord = new HexCoord(p_hexData.nextto.q, p_hexData.nextto.r, p_hexData.nextto.s);

        attacked = p_hexData.attack_type != null;

#if UNITY_EDITOR
        if (targetCoord != null)
        {
            targetCoordforEditor.x = targetCoord.q;
            targetCoordforEditor.y = targetCoord.r;
            targetCoordforEditor.z = targetCoord.s;
        }
#endif
    }

    // =====================================================================
    //  DEGATS ET SOINS
    //  energy est tenu synchrone avec currentHP : BoardController.CheckPawnAfterAttack
    //  se base sur energy <= 0.
    // =====================================================================
    public void ApplyDamage(int amount)
    {
        if (amount <= 0) return;

        currentHP -= amount;
        if (currentHP < 0) currentHP = 0;
        energy = currentHP;

        TakeDamageVisual();

        // Le chiffre. Le flash dit "quelque chose s'est passe", la barre dit "il en
        // reste tant" : ni l'un ni l'autre ne repond a la seule question que le
        // joueur se pose, qui est COMBIEN. C'est le point de passage unique de tous
        // les degats subis par un pion - coup de Tank, tir de Bunker, attaque
        // ennemie - donc rien ne peut etre oublie.
        MNLTHII.UI.DamagePopup.Show(transform.position, amount,
                                    IsEnemy ? MNLTHII.UI.DamageKind.DealtToEnemy
                                            : MNLTHII.UI.DamageKind.TakenByPlayer);
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || currentHP <= 0) return;

        int before = currentHP;
        currentHP = Mathf.Min(maxHP, currentHP + amount);
        energy = currentHP;
        RefreshHealthBar();

        // Le soin reel, pas le soin demande : un Tank a plein ne gagne rien, et
        // afficher "+10" dessus serait un mensonge.
        MNLTHII.UI.DamagePopup.Show(transform.position, currentHP - before,
                                    MNLTHII.UI.DamageKind.Healed);
    }

    /// <summary>Applique ou retire le bonus de PV max du Cristal, sans jamais cumuler.</summary>
    public void SetCrystalBonus(bool active, int bonus)
    {
        int wanted = baseMaxHP + (active ? bonus : 0);
        if (wanted == maxHP) return;

        int delta = wanted - maxHP;
        maxHP = wanted;
        if (delta > 0) currentHP += delta;
        currentHP = Mathf.Clamp(currentHP, 1, maxHP);
        energy = currentHP;
        RefreshHealthBar();
    }

    public void RefreshHealthBar()
    {
        // La barre d'un TANK reste affichee : on doit pouvoir lire l'etat de son
        // armee sans attendre qu'elle se fasse frapper. Celle d'un ennemi s'efface
        // apres quelques secondes, sinon le plateau se couvre de barres rouges.
        if (_healthBar != null) _healthBar.ShowAndSetHealth(currentHP, maxHP, IsEnemy, !IsEnemy);
    }

    /// <summary>
    /// Feedback de degat : clignotement rouge, sans deformation d'echelle.
    /// L'unite s'eteint et se rallume deux fois, teintee en rouge.
    /// </summary>
    public void TakeDamageVisual()
    {
        if (_transform == null) return; // pion deja detruit

        if (_blinkRoutine != null) StopCoroutine(_blinkRoutine);
        _blinkRoutine = StartCoroutine(BlinkRedCoroutine());

        RefreshHealthBar();
    }

    /// <summary>
    /// Flash rouge sans allocation : MaterialPropertyBlock reutilise, tableaux de
    /// renderers et de couleurs preleves dans Awake.
    /// </summary>
    private IEnumerator BlinkRedCoroutine()
    {
        SetAllRenderersColor(true);

        for (int i = 0; i < damageFlashCount; i++)
        {
            SetRenderersVisible(false);
            yield return WaitFlashOff;
            SetRenderersVisible(true);
            yield return WaitFlashOn;
        }

        SetAllRenderersColor(false);
        _blinkRoutine = null;
    }

    private void SetAllRenderersColor(bool hit)
    {
        if (_renderers == null) return;

        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer r = _renderers[i];
            int propertyId = _colorPropertyIds[i];
            if (r == null || propertyId == 0) continue;

            r.GetPropertyBlock(_propertyBlock);
            // Au repos, la couleur rendue est celle de la posture : le flash de degat
            // ne peut donc pas "effacer" la teinte de posture en la restaurant.
            _propertyBlock.SetColor(propertyId, hit ? Color.red : GetRestingColor(i));
            r.SetPropertyBlock(_propertyBlock);
        }
    }

    /// <summary>Conserve pour compatibilite avec les anciens SendMessage("Hitted").</summary>
    public IEnumerator Hitted()
    {
        SetAllRenderersColor(true);
        yield return WaitBlink;
        SetAllRenderersColor(false);
    }

    // =====================================================================
    //  ANIMATIONS
    // =====================================================================
    /// <summary>
    /// Duree d'une orientation, a l'allure choisie. Le plancher evite un tween de
    /// duree nulle, que DOTween traite comme "pas de tween du tout".
    /// </summary>
    private static float LookSeconds()
    {
        float value = MNLTHII.Managers.PhasePace.Seconds(MNLTHII.Managers.PhasePace.PawnLook);
        return (value < 0.02f) ? 0.02f : value;
    }

    /// <summary>Duree d'un pas, a l'allure choisie. Toujours sous PhasePace.UnitMove.</summary>
    private static float MoveSeconds()
    {
        float value = MNLTHII.Managers.PhasePace.Seconds(MNLTHII.Managers.PhasePace.PawnMove);
        return (value < 0.02f) ? 0.02f : value;
    }

    public void ApplySequenceAnimation(TypeOfPawnInteractions p_pawnInteraction)
    {
        if (_board == null) _board = BoardController.instance;
        if (_board == null) return;

        switch (p_pawnInteraction)
        {
            case TypeOfPawnInteractions.target:
                if (targetCoord != null && !ComparePawnCoord(targetCoord))
                {
                    Hexagon destination = _board.getHexByCoord(targetCoord);
                    if (destination != null)
                    {
                        // Stocke dans un champ au lieu d'etre capture par une lambda.
                        _pendingDestination = destination;

                        // LE PAS DURAIT 1,5 SECONDE ALORS QUE LA PHASE N'EN ATTENDAIT
                        // QUE 0,85. Un pion etait donc encore en train de glisser quand
                        // le suivant commencait a jouer - et un Tank de rang 2, qui
                        // enchaine deux pas, lancait son second tween pendant que le
                        // premier courait encore : DOTween ecrasait l'un par l'autre et
                        // l'unite sautait une case a l'ecran.
                        //
                        // La duree vient maintenant de PhasePace, ou elle est reglee
                        // POUR RESTER SOUS l'attente d'un pas, et elle suit l'allure
                        // choisie par le joueur : accelerer l'attente sans accelerer le
                        // mouvement aurait juste deplace le meme bogue.
                        Sequence sequence = DOTween.Sequence();
                        sequence.Append(_transform.DOLookAt(destination.transform.position, LookSeconds()))
                                .SetEase(Ease.InOutCirc);
                        // Un pas n'est pas un coup : ce tween declenchait le son
                        // d'ATTAQUE, et le joueur entendait donc frapper a chaque
                        // deplacement. Il a maintenant le sien.
                        sequence.Append(_transform.DOMove(destination.transform.position, MoveSeconds()))
                                .SetEase(Ease.InCirc)
                                .OnStart(_onMoveSfxCached);
                        sequence.OnComplete(_onMoveCompleteCached);
                    }
                    UpdateCoord(targetCoord);
                    targetCoord = null;
                }
                break;

            case TypeOfPawnInteractions.attack:
                if (attackCoord != null)
                {
                    PlayAttackAnimation(_board.getHexByCoord(attackCoord));
                    attackCoord = null;
                }
                break;

            case TypeOfPawnInteractions.nextTo:
                if (NextCoord != null && !ComparePawnCoord(NextCoord))
                {
                    Hexagon destination = _board.getHexByCoord(NextCoord);
                    if (destination != null)
                    {
                        _pendingDestination = destination;

                        Sequence sequence = DOTween.Sequence();
                        sequence.Append(_transform.DOLookAt(destination.transform.position, LookSeconds()))
                                .SetEase(Ease.InCirc)
                                .OnStart(_onMoveSfxCached);
                        sequence.Append(_transform.DOMove(destination.transform.position, MoveSeconds()))
                                .SetEase(Ease.InCirc);
                        sequence.OnComplete(_onMoveCompleteCached);
                    }
                }
                break;
        }
    }

    /// <summary>Reparente le pion sur son hexagone d'arrivee une fois le deplacement termine.</summary>
    private void OnMoveComplete()
    {
        // Callback differe : le pion a pu mourir entre le depart du tween et son
        // arrivee. Comparer a null suffit, Unity renvoie true pour un objet detruit.
        if (_transform == null || _pendingDestination == null) return;

        _transform.parent = _pendingDestination.transform;
        _pendingDestination = null;
    }

    /// <summary>
    /// Animation d'attaque : l'unite s'oriente vers sa cible et clignote rapidement.
    /// On bascule Renderer.enabled plutot que SetActive sur le GameObject, sinon la
    /// coroutine qui doit rallumer l'unite serait elle-meme arretee par la desactivation.
    /// </summary>
    public void PlayAttackAnimation(Hexagon targetHex)
    {
        if (_transform == null) return;

        if (targetHex != null)
            _transform.DOLookAt(targetHex.transform.position, LookSeconds()).SetEase(Ease.OutQuad);

        if (_attackFlashRoutine != null) StopCoroutine(_attackFlashRoutine);
        _attackFlashRoutine = StartCoroutine(AttackFlashRoutine());
    }

    private IEnumerator AttackFlashRoutine()
    {
        int flashes = (attackFlashCount > 0) ? attackFlashCount : 1;

        for (int i = 0; i < flashes; i++)
        {
            SetRenderersVisible(false);
            yield return WaitFlashOff;
            SetRenderersVisible(true);
            yield return WaitFlashOn;
        }

        _attackFlashRoutine = null;
    }

    private void SetRenderersVisible(bool visible)
    {
        if (_renderers == null) return;

        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer r = _renderers[i];
            if (r != null) r.enabled = visible;
        }
    }

    /// <summary>Filet de securite : ne jamais laisser une unite invisible.</summary>
    private void OnDisable()
    {
        _attackFlashRoutine = null;
        SetRenderersVisible(true);
    }

    private void PlayAttackSfx()
    {
        MNLTHII.Managers.FXManager fx = MNLTHII.Managers.FXManager.Instance;
        if (fx != null) fx.PlayAttackSFX(_audioSource);
    }

    private void PlayMoveSfx()
    {
        MNLTHII.Managers.FXManager fx = MNLTHII.Managers.FXManager.Instance;
        if (fx != null) fx.PlayMoveSFX(_audioSource);
    }

    [Serializable]
    public enum TypeOfPawnInteractions
    {
        target,
        attack,
        nextTo
    }
}

// ---------------------------------------------------------------------------
// NOTE D'OPTIMISATION
//
// 1. BlinkRedCoroutine allouait a chaque coup recu un tableau (GetComponentsInChildren)
//    et un Dictionary<Renderer, Color>, et lisait Renderer.material, ce qui duplique
//    le materiau de chaque pion touche. Les renderers et leurs couleurs d'origine sont
//    releves une fois dans Awake depuis sharedMaterial, et le flash passe par un
//    MaterialPropertyBlock reutilise : zero allocation, batching preserve.
// 2. Les noms de proprietes de shader sont convertis une fois en identifiants entiers
//    (Shader.PropertyToID) au lieu d'etre resolus par chaine a chaque acces.
// 3. Les callbacks DOTween (OnStart / OnComplete) etaient des lambdas capturant une
//    variable locale, donc une classe de closure allouee a chaque deplacement de chaque
//    unite. Elles sont remplacees par deux TweenCallback mis en cache dans Awake, la
//    destination transitant par le champ _pendingDestination.
// 4. Invoke("DoAttackSFX", ...) reposait sur une resolution de methode par chaine :
//    remplace par un appel direct dans le callback OnStart.
// 5. transform, AudioSource et BoardController.instance sont mis en cache ; DOPunchScale
//    et DOMove operent sur le Transform cache plutot que sur la propriete transform.
// 6. WaitForSeconds est une instance statique partagee par tous les pions.
// ---------------------------------------------------------------------------
