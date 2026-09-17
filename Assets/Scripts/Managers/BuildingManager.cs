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

        // Une salve de Bunker enchaine jusqu'a six tirs : trop courte, elle devient un
        // seul eclair et on ne compte plus rien.
        private static readonly WaitForSeconds WaitBunkerVolley = new WaitForSeconds(0.8f);

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
            ProcessCrystals();
            ProcessMountains();

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

                if (inRange.Count == 0) continue;

                int shots = InteractionRules.GetBunkerTargets(bunker.level);
                int damage = InteractionRules.GetBunkerDamage(bunker.level);
                int shotCost = InteractionRules.GetBunkerShotCost(bunker.level);

                int fired = 0;
                int spent = 0;
                int kills = 0;

                for (int s = 0; s < shots; s++)
                {
                    // Chaque tir se paie. Un Bunker sans cible ne coute rien ; un Bunker
                    // qui vide son chargeur coute cher. Des qu'on ne peut plus payer,
                    // il se tait.
                    if (energy != null && !energy.CanAfford(shotCost))
                    {
                        if (fired == 0)
                            Debug.LogFormat("[Bunker] Plus assez d'Energie ({0} par tir) : ce bunker se tait.", shotCost);
                        break;
                    }

                    PawnController victim = SelectBunkerTarget(inRange, damage);
                    if (victim == null) break;

                    if (energy != null && !energy.TrySpend(shotCost)) break;
                    spent += shotCost;

                    // Le trait de laser a ete retire : il ne s'affichait pas. Le tir se
                    // lit desormais a l'impact sur la cible et au chiffre de degats.
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
                    if (victim.currentHP <= 0) inRange.Remove(victim);
                    else if (kills == 0 && CameraDirector.Instance != null)
                    {
                        // Premier tir de la salve : on va voir. Le Bunker tire pendant
                        // la phase de fin de tour, sans projecteur ni carte d'unite -
                        // sans camera, le joueur ne saurait jamais qu'il a servi.
                        CameraDirector.FrameAction(bunker.transform.position, victim.transform.position);
                    }

                    if (victim.currentHP <= 0) kills++;
                }

                if (fired > 0)
                {
                    if (FXManager.Instance != null) FXManager.Instance.PlayAttackSFX();
                    Debug.LogFormat("[Bunker] {0} tir(s) de {1} degats, {2} abattu(s), {3} Energie depensee.",
                                    fired, damage, kills, spent);
                    yield return WaitBunkerVolley;
                }
            }

            CameraDirector.ReleaseCamera();
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
        private void HealAroundCrystals()
        {
            List<Hexagon> hexes = BoardController.instance.HexagonsInBoard;
            List<PawnController> pawns = BoardController.instance.PawnsInBoard;

            for (int h = 0; h < hexes.Count; h++)
            {
                Hexagon hex = hexes[h];
                if (hex == null || hex.type != TypeOfHex.crystal || hex.level < 1) continue;
                if (hex.currentHP <= 0) continue;

                int heal = InteractionRules.GetCrystalHeal(hex.level);
                int range = InteractionRules.GetCrystalRange(hex.level);

                for (int i = 0; i < pawns.Count; i++)
                {
                    PawnController pawn = pawns[i];
                    if (pawn == null || pawn.IsEnemy || pawn.hexcoord == null) continue;
                    if (pawn.currentHP >= pawn.maxHP) continue;
                    if (BoardController.GetHexDistance(hex.positionInTheBoard, pawn.hexcoord) > range) continue;

                    pawn.Heal(heal);
                    if (FXManager.Instance != null) FXManager.Instance.SpawnEnergyBuffFX(pawn.transform.position);
                }
            }
        }

        // ---------------------------------------------------------------
        //  CRISTAL : +20 PV Max aux allies a portee, recalcule sans cumul
        // ---------------------------------------------------------------
        private void ProcessCrystals()
        {
            HealAroundCrystals();

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
        //  MONTAGNE : Centre de Commandement, -10 PV par tour
        // ---------------------------------------------------------------
        private void ProcessMountains()
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
                if (mountain == null) continue;

                mountain.ApplyDamage(InteractionRules.MOUNTAIN_DECAY_PER_TURN);

                if (mountain.currentHP <= 0)
                {
                    Debug.Log("[Montagne] Centre de Commandement epuise, il s'effondre.");
                    InteractionRules.DestroyBuilding(mountain);
                }
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
