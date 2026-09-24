using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MNLTHII;
using MNLTHII.Rules;

namespace MNLTHII.Managers
{
    /// <summary>
    /// GDD V3 - Section 3 : tous les effets de batiments, appliques en fin de tour.
    ///   Bunker   : tire sur N cibles au hasard a portee 2.
    ///   Gaz      : soigne les allies a portee.
    ///   Cristal  : confere +20 PV Max aux allies a portee (sans cumul).
    ///   Montagne : repousse les ennemis (gere dans le pathfinding) et perd 10 PV par tour.
    ///   Base     : si elle tombe a 0 PV, c'est la defaite.
    /// </summary>
    public class BuildingManager : MonoBehaviour
    {
        public static BuildingManager Instance { get; private set; }

        // Tampons membres reutilises a chaque fin de tour : la version precedente
        // allouait une nouvelle List par bunker et par passe de montagnes.
        private readonly List<Hexagon> _bunkerBuffer = new List<Hexagon>(32);
        private readonly List<Hexagon> _mountainBuffer = new List<Hexagon>(32);
        private readonly List<PawnController> _targetBuffer = new List<PawnController>(24);

        // LES RYTHMES DE L'ETAPE DES BATIMENTS vivent maintenant dans PhasePace, avec
        // tous les autres : avant le premier tir (BunkerFrame, le temps que la camera
        // arrive), entre deux tirs (BunkerBetweenShots, assez pour voir chaque trait
        // partir), apres la salve (BunkerVolley), et sur chaque Cristal ou Centre
        // (BuildingShow).
        //
        // Ils passent par une PaceWait, donc ils suivent l'allure choisie par le
        // joueur ET ils se laissent interrompre : qui a compris ce que fait ce Bunker
        // peut passer au suivant sans attendre les six traits.
        //
        // Deux instances : ProcessBunkers, ProcessCrystalsSequential et
        // ProcessMountainsSequential s'enchainent mais HealAroundCrystals est appelee
        // DEPUIS l'une d'elles, et deux coroutines imbriquees ne peuvent pas partager
        // la meme echeance.
        private readonly PaceWait _pace = new PaceWait();
        private readonly PaceWait _paceInner = new PaceWait();

        /// <summary>Couleur du trait de tir d'un Bunker (celle du joueur).</summary>
        private static readonly Color BunkerShotColor = new Color(0.35f, 0.95f, 1f, 1f);

        // =================================================================
        //  L'ETAPE DES BATIMENTS SE REGARDE
        // =================================================================
        /// <summary>
        /// Avant, Cristaux et Centres de Commandement agissaient en une frame, sans que
        /// la camera bouge : le joueur voyait des PV changer quelque part et ne savait
        /// pas pourquoi. Maintenant on s'arrete sur CHAQUE batiment, le temps de lire ce
        /// qu'il fait.
        /// </summary>
        [Header("Rythme (OBSOLETE - voir PhasePace.BuildingShow)")]
        public float buildingShowDuration = 1.5f;

        /// <summary>Le trajet de la camera, avant l'allure. PaceWait applique l'allure.</summary>
        private static float Travel { get { return CameraDirector.RawTravel + PhasePace.CameraMargin; } }

        /// <summary>Y a-t-il seulement quelque chose a montrer cette fin de tour ?</summary>
        public bool HasAnythingToShow()
        {
            if (BoardController.instance == null || BoardController.instance.HexagonsInBoard == null) return false;

            List<Hexagon> hexes = BoardController.instance.HexagonsInBoard;
            for (int i = 0; i < hexes.Count; i++)
            {
                Hexagon hex = hexes[i];
                if (hex == null || hex.level < 1 || hex.currentHP <= 0) continue;
                if (hex.type == TypeOfHex.hill || hex.type == TypeOfHex.crystal || hex.type == TypeOfHex.mountain)
                    return true;
            }
            return false;
        }

        // Coordonnees des hexagones de Base, relevees une fois : elles ne bougent
        // jamais, et l'IA des Tanks les interroge pour chaque cible evaluee.
        private readonly List<HexCoord> _baseCoords = new List<HexCoord>(8);

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(this); return; }
        }

        public IEnumerator ProcessEndOfTurn()
        {
            if (BoardController.instance == null || BoardController.instance.HexagonsInBoard == null) yield break;

            yield return StartCoroutine(ProcessBunkers());
            yield return StartCoroutine(ProcessCrystalsSequential());
            yield return StartCoroutine(ProcessMountainsSequential());

            CameraDirector.ReleaseCamera();

            // Plus de ProcessGas : le Gaz est devenu une usine. Il ne fait plus rien en
            // fin de tour - il produit en DEBUT de tour, dans GrantPassiveIncome, la ou
            // le joueur peut voir son revenu tomber.
        }

        // ---------------------------------------------------------------
        //  COLLINE : Bunker (Niv2) et Forteresse (Niv3)
        // ---------------------------------------------------------------
        private IEnumerator ProcessBunkers()
        {
            List<Hexagon> hexes = BoardController.instance.HexagonsInBoard;

            _bunkerBuffer.Clear();
            for (int i = 0; i < hexes.Count; i++)
            {
                Hexagon hex = hexes[i];
                if (hex != null && hex.type == TypeOfHex.hill && hex.level >= 1) _bunkerBuffer.Add(hex);
            }

            EnergyManager energy = EnergyManager.Instance;

            for (int b = 0; b < _bunkerBuffer.Count; b++)
            {
                Hexagon bunker = _bunkerBuffer[b];
                if (bunker == null || bunker.currentHP <= 0) continue;

                List<PawnController> pawns = BoardController.instance.PawnsInBoard;
                List<PawnController> inRange = _targetBuffer;
                inRange.Clear();

                for (int i = 0; i < pawns.Count; i++)
                {
                    PawnController pawn = pawns[i];
                    if (pawn == null || !pawn.IsEnemy || pawn.hexcoord == null) continue;
                    if (BoardController.GetHexDistance(bunker.positionInTheBoard, pawn.hexcoord) <= InteractionRules.BUNKER_RANGE)
                        inRange.Add(pawn);
                }

                if (inRange.Count == 0)
                {
                    WearBunker(bunker, 0);
                    continue;
                }

                // Un Bunker est un "element" : le bouton "suivant" passe de l'un a
                // l'autre, comme il passe d'un Tank au Tank d'apres.
                PhasePace.BeginUnit();

                int shots = InteractionRules.GetBunkerTargets(bunker.level);
                int damage = InteractionRules.GetBunkerDamage(bunker.level);
                int shotCost = InteractionRules.GetBunkerShotCost(bunker.level);

                int fired = 0;
                int spent = 0;
                int kills = 0;

                // On va voir AVANT de tirer : la camera cadre le Bunker et sa premiere
                // cible, puis la salve part sous les yeux du joueur.
                PawnController firstTarget = SelectBunkerTarget(inRange, damage);
                if (firstTarget != null && CameraDirector.Instance != null
                    && (energy == null || energy.CanAfford(shotCost)))
                {
                    CameraDirector.FrameAction(bunker.transform.position, firstTarget.transform.position);
                    yield return _pace.For(PhasePace.BunkerFrame);
                }

                Vector3 muzzle = MuzzleOf(bunker);

                for (int s = 0; s < shots; s++)
                {
                    // UN TIR NE COUTE PLUS D'ENERGIE. C'etait le dernier prelevement
                    // passif du jeu - le seul endroit ou le solde baissait sans que le
                    // joueur ait clique - et il donnait a la defense un gout
                    // d'entretien. Le Bunker se paie desormais entierement a la pose,
                    // et en PV a chaque coup parti (voir InteractionRules).
                    //
                    // Le test est conserve pour le cas ou shotCost redeviendrait non
                    // nul : a zero, CanAfford est toujours vrai et cette branche ne
                    // coute rien.
                    if (shotCost > 0 && energy != null && !energy.CanAfford(shotCost))
                    {
                        if (fired == 0)
                            Debug.LogFormat("[Bunker] Plus assez d'Energie ({0} par tir) : ce bunker se tait.", shotCost);
                        break;
                    }

                    PawnController victim = SelectBunkerTarget(inRange, damage);
                    if (victim == null) break;

                    if (shotCost > 0)
                    {
                        if (energy != null && !energy.TrySpend(shotCost)) break;
                        spent += shotCost;
                    }

                    // LE TRAIT : du haut du Bunker au corps de la cible. C'est lui qui dit
                    // "ce Bunker tire sur CET ennemi" - sans lui, on ne voyait qu'un
                    // impact apparaitre quelque part.
                    float travel = ShotTracer.Fire(muzzle, CenterOf(victim), BunkerShotColor);

                    // L'impact tombe quand le trait ARRIVE, pas quand il part.
                    if (travel > 0f) yield return _pace.ForExact(travel);
                    if (victim == null) break;

                    if (FXManager.Instance != null)
                        FXManager.Instance.SpawnHitFX(victim.transform.position + Vector3.up);

                    victim.ApplyDamage(damage);

                    // Un ennemi abattu par un Bunker deleste son portail d'origine
                    // exactement comme s'il tombait sous le feu d'un Tank : tenir la
                    // ligne est une facon de gagner, pas seulement de ne pas perdre.
                    InteractionRules.RegisterKillIfDead(victim);

                    BoardController.instance.NotifyPawnDamaged(victim);
                    fired++;

                    // TIR CONCENTRE. La cible ne quitte la liste que MORTE.
                    //
                    // C'etait le defaut qui rendait le Bunker inoffensif : il retirait
                    // sa cible apres un seul tir, donc ses quatre tirs allaient a quatre
                    // ennemis differents. Dix degats sur trente points de vie, quatre
                    // fois : quatre blesses, zero mort. Et contre un ennemi seul, trois
                    // tirs sur quatre ne partaient meme pas, faute d'autre cible.
                    //
                    // Un Bunker doit vider son chargeur sur le meme adversaire jusqu'a
                    // ce qu'il tombe. C'est ce qui en fait un mur plutot qu'une
                    // nuisance, et c'est aussi ce qui alimente l'ebranlement des
                    // Shofars, qui ne compte que les morts.
                    if (victim.currentHP <= 0)
                    {
                        inRange.Remove(victim);
                        kills++;
                    }

                    // Chaque tir se voit : un trait, une pause, le suivant.
                    yield return _pace.For(PhasePace.BunkerBetweenShots);
                }

                if (fired > 0)
                {
                    if (FXManager.Instance != null) FXManager.Instance.PlayAttackSFX();
                    Debug.LogFormat("[Bunker] {0} tir(s) de {1} degats, {2} abattu(s), {3} Energie, "
                                  + "{4} PV d'usure ({5}/{6} restants).",
                                    fired, damage, kills, spent,
                                    fired * InteractionRules.BUNKER_WEAR_PER_SHOT,
                                    bunker.currentHP, bunker.maxHP);
                    yield return _pace.For(PhasePace.BunkerVolley);
                }

                WearBunker(bunker, fired);
            }

            CameraDirector.ReleaseCamera();
        }

        /// <summary>
        /// L'usure d'un Bunker : ce qu'il a TIRE, et rien d'autre.
        ///
        /// La decroissance passive vaut zero (voir InteractionRules.BUNKER_DECAY_L1) :
        /// un Bunker qui n'a eu personne a portee ne perd plus rien. Le test le garde
        /// quand meme dans le calcul, pour qu'un futur reglage a une valeur non nulle
        /// n'oblige a toucher a rien ici.
        ///
        /// Zero tir et zero decroissance : on sort tout de suite plutot que d'appeler
        /// ApplyDamage(0), qui declencherait le clignotement de degats et ferait croire
        /// au joueur que son Bunker vient d'encaisser quelque chose.
        /// </summary>
        private void WearBunker(Hexagon bunker, int shotsFired)
        {
            if (bunker == null || bunker.currentHP <= 0 || bunker.level < 1) return;

            int wear = InteractionRules.GetBunkerDecay(bunker.level)
                     + shotsFired * InteractionRules.BUNKER_WEAR_PER_SHOT;

            if (wear <= 0) return;

            bunker.ApplyDamage(wear);

            if (bunker.currentHP <= 0)
            {
                Debug.Log("[Bunker] Il a tire jusqu'a sa derniere munition, il s'effondre.");
                InteractionRules.DestroyBuilding(bunker);
            }
        }

        /// <summary>
        /// Choix de cible d'un Bunker. Deux regles, dans cet ordre :
        ///
        ///   1. ACHEVER ce qui peut l'etre ce tir-ci. Un ennemi blesse frappe encore a
        ///      pleine puissance : le blesser une deuxieme fois ne protege personne,
        ///      le tuer supprime ses degats et deleste son Portail.
        ///   2. Sinon, le PLUS AVANCE vers la Base. C'est lui qui frappera en premier.
        ///
        /// Le tir etait auparavant reparti au hasard entre les cibles a portee. C'etait
        /// le pire des deux mondes : aucune decision pour le joueur, puisque le Bunker
        /// est automatique, et des degats etales qui ne reduisaient jamais la menace du
        /// tour. Le hasard n'ajoutait pas de tension, seulement du bruit - et il brouillait
        /// l'instabilite des Portails, qui repose justement sur des morts previsibles.
        ///
        /// La cible reste dans le tampon tant qu'elle vit : le Bunker vide son
        /// chargeur sur elle jusqu'a ce qu'elle tombe, puis passe a la suivante. La
        /// regle inverse - un tir par ennemi - avait l'air prudente et rendait l'arme
        /// inutile : dix degats sur trente points de vie ne tuent jamais personne.
        /// </summary>
        // Tampon pour mesurer les modeles (bout du canon, centre de la cible).
        private readonly List<Renderer> _measure = new List<Renderer>(16);

        /// <summary>Le haut du Bunker : d'ou part le trait.</summary>
        private Vector3 MuzzleOf(Hexagon bunker)
        {
            Vector3 p = bunker.transform.position;

            bunker.GetComponentsInChildren(false, _measure);
            float top = float.MinValue;
            for (int i = 0; i < _measure.Count; i++)
            {
                Renderer r = _measure[i];
                if (r == null || r is ParticleSystemRenderer || r is SpriteRenderer) continue;
                if (r.GetComponentInParent<PawnController>() != null) continue;
                float y = r.bounds.max.y;
                if (y > top) top = y;
            }
            _measure.Clear();

            p.y = (top > float.MinValue) ? top : p.y + 0.6f;
            return p;
        }

        /// <summary>Le centre du modele de la cible : ou arrive le trait.</summary>
        private Vector3 CenterOf(PawnController pawn)
        {
            pawn.GetComponentsInChildren(false, _measure);
            bool found = false;
            Bounds b = new Bounds(pawn.transform.position, Vector3.zero);
            for (int i = 0; i < _measure.Count; i++)
            {
                Renderer r = _measure[i];
                if (r == null || r is ParticleSystemRenderer || r is SpriteRenderer) continue;
                if (r.GetComponent<ReadabilityDecal>() != null) continue;   // ombre et anneau au sol
                if (!found) { b = r.bounds; found = true; }
                else b.Encapsulate(r.bounds);
            }
            _measure.Clear();

            return found ? b.center : pawn.transform.position + Vector3.up * 0.5f;
        }

        private PawnController SelectBunkerTarget(List<PawnController> candidates, int damage)
        {
            int bestIndex = -1;
            bool bestCanFinish = false;
            int bestDistanceToBase = int.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                PawnController candidate = candidates[i];
                if (candidate == null || candidate.currentHP <= 0) continue;

                bool canFinish = candidate.currentHP <= damage;
                int distanceToBase = DistanceToBase(candidate.hexcoord);

                if (bestIndex < 0
                    || (canFinish && !bestCanFinish)
                    || (canFinish == bestCanFinish && distanceToBase < bestDistanceToBase))
                {
                    bestIndex = i;
                    bestCanFinish = canFinish;
                    bestDistanceToBase = distanceToBase;
                }
            }

            if (bestIndex < 0) return null;

            // On NE retire PAS la cible ici : c'est l'appelant qui le fait, et
            // seulement quand elle est morte. Retirer apres un tir revenait a
            // interdire au Bunker d'achever quoi que ce soit.
            return candidates[bestIndex];
        }

        // ---------------------------------------------------------------
        //  GAZ : soin des allies
        // ---------------------------------------------------------------
        /// <summary>
        /// Le soin, qui appartenait au Gaz et appartient maintenant au Cristal.
        ///
        /// Le Gaz est devenu l'usine : il produit l'Energie et c'est tout. Lui laisser
        /// le soin en plus aurait refait de lui le batiment qui fait tout, et la
        /// question "c'est quoi la difference avec le Cristal" serait revenue intacte.
        /// </summary>
        private IEnumerator HealAroundCrystals()
        {
            List<Hexagon> hexes = BoardController.instance.HexagonsInBoard;
            List<PawnController> pawns = BoardController.instance.PawnsInBoard;

            for (int h = 0; h < hexes.Count; h++)
            {
                Hexagon hex = hexes[h];
                if (hex == null || hex.type != TypeOfHex.crystal || hex.level < 1) continue;
                if (hex.currentHP <= 0) continue;

                PhasePace.BeginUnit();

                int heal = InteractionRules.GetCrystalHeal(hex.level);
                int range = InteractionRules.GetCrystalRange(hex.level);

                // On va VOIR le Cristal travailler : sans cela, des PV remontaient
                // quelque part sur la carte sans que rien ne dise d'ou ca venait.
                CameraDirector.FocusPoint(hex.transform.position);
                yield return _paceInner.For(Travel);

                if (FXManager.Instance != null)
                {
                    FXManager.Instance.SpawnCrystalBuffFX(hex.transform.position);
                    FXManager.Instance.PlayBuildingSFX();
                }

                bool healedSomeone = false;

                for (int i = 0; i < pawns.Count; i++)
                {
                    PawnController pawn = pawns[i];
                    if (pawn == null || pawn.IsEnemy || pawn.hexcoord == null) continue;
                    if (pawn.currentHP >= pawn.maxHP) continue;
                    if (BoardController.GetHexDistance(hex.positionInTheBoard, pawn.hexcoord) > range) continue;

                    int before = pawn.currentHP;
                    pawn.Heal(heal);

                    int given = pawn.currentHP - before;
                    if (given > 0)
                    {
                        MNLTHII.UI.DamagePopup.Show(pawn.transform.position + Vector3.up * 0.4f,
                                                    given, MNLTHII.UI.DamageKind.Healed);
                        healedSomeone = true;
                    }

                    if (FXManager.Instance != null) FXManager.Instance.SpawnEnergyBuffFX(pawn.transform.position);
                }

                if (!healedSomeone)
                {
                    // Personne a soigner : on le montre quand meme une demi-seconde,
                    // le joueur comprend que le Cristal est la et qu'il ne sert a rien
                    // ce tour-ci.
                    yield return _paceInner.For(0.45f);
                    continue;
                }

                yield return _paceInner.For(PhasePace.BuildingShow);
            }
        }

        // ---------------------------------------------------------------
        //  CRISTAL : +20 PV Max aux allies a portee, recalcule sans cumul
        // ---------------------------------------------------------------
        private IEnumerator ProcessCrystalsSequential()
        {
            yield return StartCoroutine(HealAroundCrystals());

            List<PawnController> pawns = BoardController.instance.PawnsInBoard;

            for (int i = 0; i < pawns.Count; i++)
            {
                PawnController pawn = pawns[i];
                if (pawn == null || pawn.IsEnemy || pawn.hexcoord == null) continue;

                bool supported = InteractionRules.HasCrystalSupport(pawn.hexcoord);
                int before = pawn.maxHP;
                pawn.SetCrystalBonus(supported, InteractionRules.CRYSTAL_BONUS_MAXHP);

                if (pawn.maxHP > before && FXManager.Instance != null)
                    FXManager.Instance.SpawnCrystalBuffFX(pawn.transform.position);
            }
        }

        // ---------------------------------------------------------------
        //  MONTAGNE : Centre de Commandement - il ne fond que s'il est abandonne
        // ---------------------------------------------------------------
        /// <summary>
        /// UN CENTRE DE COMMANDEMENT NE FOND PLUS : SA BULLE SE REFAIT.
        ///
        /// L'etape ne retire plus rien par defaut (MOUNTAIN_DECAY_PER_TURN vaut zero).
        /// Elle fait l'inverse : elle rend a la bulle ce qu'elle peut, a condition que
        /// le Centre n'ait rien encaisse pendant le tour. Frappe, il ne se refait pas -
        /// sinon un assaut lent n'aboutirait jamais et la tete de pont redeviendrait
        /// une fortification.
        ///
        /// ON NE S'ARRETE QUE SUR CE QUI A UNE NOUVELLE. Faire voyager la camera pour
        /// montrer un batiment intact, c'est prendre deux secondes au joueur pour lui
        /// apprendre qu'il ne s'est rien passe. Elle n'y va que quand la bulle remonte
        /// ou quand le Centre perd quelque chose.
        /// </summary>
        private IEnumerator ProcessMountainsSequential()
        {
            List<Hexagon> hexes = BoardController.instance.HexagonsInBoard;

            _mountainBuffer.Clear();
            for (int i = 0; i < hexes.Count; i++)
            {
                Hexagon hex = hexes[i];
                if (hex != null && hex.type == TypeOfHex.mountain && hex.level >= 1) _mountainBuffer.Add(hex);
            }

            for (int i = 0; i < _mountainBuffer.Count; i++)
            {
                Hexagon mountain = _mountainBuffer[i];
                if (mountain == null || mountain.currentHP <= 0) continue;

                // Une partie rechargee depuis un ancien fichier n'a pas de bulle :
                // on la lui rend plutot que de la laisser sans protection.
                if (mountain.shieldMax <= 0) InteractionRules.ResetShield(mountain);

                bool wasHit = mountain.attacked;
                mountain.attacked = false;

                int decay = InteractionRules.MOUNTAIN_DECAY_PER_TURN;
                bool canHeal = !wasHit && mountain.shieldHP < mountain.shieldMax;

                // Rien a raconter : intact, bulle pleine, ou frappe ce tour-ci (auquel
                // cas le joueur a DEJA vu les coups tomber pendant la phase ennemie).
                if (decay <= 0 && !canHeal) continue;

                PhasePace.BeginUnit();

                CameraDirector.FocusPoint(mountain.transform.position);
                yield return _paceInner.For(Travel);

                if (FXManager.Instance != null) FXManager.Instance.PlayBuildingSFX();

                if (canHeal)
                {
                    int before = mountain.shieldHP;

                    mountain.shieldHP += InteractionRules.MOUNTAIN_SHIELD_REGEN;
                    if (mountain.shieldHP > mountain.shieldMax) mountain.shieldHP = mountain.shieldMax;

                    int given = mountain.shieldHP - before;
                    if (given > 0)
                    {
                        MNLTHII.UI.DamagePopup.Show(mountain.transform.position + Vector3.up * 0.8f,
                                                    given, MNLTHII.UI.DamageKind.Healed);
                        if (FXManager.Instance != null)
                            FXManager.Instance.SpawnCrystalBuffFX(mountain.transform.position);
                    }
                }

                if (decay > 0)
                {
                    mountain.ApplyDamage(decay);

                    if (mountain.currentHP <= 0)
                    {
                        Debug.Log("[Montagne] Centre de Commandement epuise, il s'effondre.");
                        InteractionRules.DestroyBuilding(mountain);
                    }
                }

                yield return _paceInner.For(PhasePace.BuildingShow);
            }
        }

        // ---------------------------------------------------------------
        //  BASE : reservoir unique de 200 PV (Section 3)
        // ---------------------------------------------------------------
        // La Base occupe 7 hexagones mais ne possede qu'un seul stock de PV.
        [Header("Base")]
        [SerializeField] private int baseHP = InteractionRules.BASE_HP;
        private bool baseInitialized = false;

        public int GetBaseHP() { return Mathf.Max(0, baseHP); }
        public int GetBaseMaxHP() { return InteractionRules.BASE_HP; }

        /// <summary>A appeler une fois la carte construite.</summary>
        public void InitializeBase()
        {
            baseHP = InteractionRules.BASE_HP;
            baseInitialized = true;

            _baseCoords.Clear();
            if (BoardController.instance != null && BoardController.instance.HexagonsInBoard != null)
            {
                List<Hexagon> hexes = BoardController.instance.HexagonsInBoard;
                for (int i = 0; i < hexes.Count; i++)
                {
                    Hexagon hex = hexes[i];
                    if (hex != null && hex.type == TypeOfHex.Base && hex.positionInTheBoard != null)
                        _baseCoords.Add(hex.positionInTheBoard);
                }
            }

            SyncBaseHexes();
        }

        /// <summary>
        /// Remet les PV de la Base a leur valeur sauvegardee.
        ///
        /// InitializeBase remplit toujours le reservoir : c'est ce qu'il faut pour une
        /// partie neuve. En reprenant une partie, la Base a deja encaisse - et une Base
        /// qui repart a 200 PV effacerait toute la pression accumulee.
        /// </summary>
        public void RestoreBaseHP(int hp)
        {
            if (!baseInitialized) InitializeBase();

            baseHP = Mathf.Clamp(hp, 0, InteractionRules.BASE_HP);
            SyncBaseHexes();
        }

        /// <summary>
        /// Distance en cases jusqu'a l'hexagone de Base le plus proche.
        /// Sert a mesurer la menace que represente un ennemi.
        /// </summary>
        public int DistanceToBase(HexCoord coord)
        {
            if (coord == null || _baseCoords.Count == 0) return int.MaxValue;

            int best = int.MaxValue;
            for (int i = 0; i < _baseCoords.Count; i++)
            {
                int d = BoardController.GetHexDistance(coord, _baseCoords[i]);
                if (d < best) best = d;
            }
            return best;
        }

        // ---------------------------------------------------------------
        //  LES POINTS D'ANCRAGE
        // ---------------------------------------------------------------
        /// <summary>
        /// Vrai quand cette case est sous le commandement d'un Centre de Commandement
        /// debout - PAS de la Base.
        ///
        /// La distinction compte : c'est ce qui rend le Centre inutile pose chez soi.
        /// Son rayon doublerait celui de la Base, il fondrait en six tours, et il
        /// n'aurait rien apporte. Il ne paie que place devant.
        /// </summary>
        public bool HasCommandSupport(HexCoord coord)
        {
            if (coord == null || BoardController.instance == null) return false;

            List<Hexagon> hexes = BoardController.instance.HexagonsInBoard;
            if (hexes == null) return false;

            for (int i = 0; i < hexes.Count; i++)
            {
                Hexagon hex = hexes[i];
                if (hex == null || hex.type != TypeOfHex.mountain) continue;
                if (hex.level < 1 || hex.currentHP <= 0) continue;

                int radius = InteractionRules.GetMountainCommandRadius(hex.level);
                if (BoardController.GetHexDistance(coord, hex.positionInTheBoard) <= radius) return true;
            }

            return false;
        }

        /// <summary>
        /// Distance au point d'ancrage le plus proche : la Base, ou un Centre de
        /// Commandement debout.
        ///
        /// C'est CE nombre que le perimetre des Tanks en Garde doit lire. Avec la seule
        /// Base, un garde poste a cinq cases de chez lui ignorait l'ennemi plante devant
        /// lui : la carte etait coupee en deux, et tenir du terrain ailleurs que chez soi
        /// n'existait pas.
        ///
        /// Note d'optimisation : un parcours de la liste des hexagones, appele une fois
        /// par Tank et par tour dans le choix de cible - jamais dans Update.
        /// </summary>
        public int DistanceToCommandPoint(HexCoord coord)
        {
            int best = DistanceToBase(coord);

            if (coord == null || BoardController.instance == null) return best;

            List<Hexagon> hexes = BoardController.instance.HexagonsInBoard;
            if (hexes == null) return best;

            for (int i = 0; i < hexes.Count; i++)
            {
                Hexagon hex = hexes[i];
                if (hex == null || hex.type != TypeOfHex.mountain) continue;
                if (hex.level < 1 || hex.currentHP <= 0) continue;

                int d = BoardController.GetHexDistance(coord, hex.positionInTheBoard);
                if (d < best) best = d;
            }

            return best;
        }

        /// <summary>Tout degat inflige a un hexagone de Base passe par ici.</summary>
        public void DamageBase(int amount)
        {
            if (amount <= 0) return;
            if (!baseInitialized) InitializeBase();

            baseHP -= amount;
            if (baseHP < 0) baseHP = 0;
            Debug.LogFormat("[Base] {0} degats. PV restants : {1} / {2}", amount, baseHP, InteractionRules.BASE_HP);
            SyncBaseHexes();
        }

        /// <summary>Recopie le stock commun sur chaque hexagone de Base (barres de vie, FX).</summary>
        private void SyncBaseHexes()
        {
            if (BoardController.instance == null || BoardController.instance.HexagonsInBoard == null) return;

            List<Hexagon> hexes = BoardController.instance.HexagonsInBoard;

            for (int i = 0; i < hexes.Count; i++)
            {
                Hexagon hex = hexes[i];
                if (hex == null || hex.type != TypeOfHex.Base) continue;
                hex.maxHP = InteractionRules.BASE_HP;
                hex.energymax = InteractionRules.BASE_HP;
                hex.currentHP = baseHP;
                hex.energy = baseHP;
            }
        }

        public bool IsBaseDestroyed()
        {
            if (!baseInitialized) return false;
            return baseHP <= 0;
        }
    }
}
