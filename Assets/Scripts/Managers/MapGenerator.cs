using UnityEngine;
using MNLTHII;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MNLTHII.Managers
{
    /// <summary>
    /// LE PLATEAU. TOUJOURS LE MEME, ET DESSINE A LA MAIN.
    ///
    /// POURQUOI ON A ARRETE DE LE TIRER AU HASARD
    ///
    /// L'ancienne version melangeait un sac de terrains et les distribuait : chaque
    /// partie donnait une carte differente, et aucune n'etait jouable de la meme
    /// facon. Trois consequences, toutes mauvaises :
    ///
    ///   - on ne pouvait pas apprendre le terrain, donc pas batir de plan. Le joueur
    ///     decouvrait a chaque partie si la chance lui avait donne un Cristal pres de
    ///     la Base ou a l'autre bout ;
    ///   - les sites se collaient les uns aux autres. Une usine de Gaz coincee entre
    ///     deux collines est indefendable, et une colline collee a la Base ne couvre
    ///     rien de ce qui compte ;
    ///   - le hasard remplacait la strategie. Perdre parce que les Cristaux sont sortis
    ///     loin n'apprend rien.
    ///
    /// UN PLATEAU D'ECHECS. Les pieces sont toujours a la meme place ; ce qui change
    /// d'une partie a l'autre, c'est CE QU'ON EN FAIT. Le joueur peut etudier la carte,
    /// preparer des ouvertures, et decouvrir que le meme terrain se joue en defense
    /// autour des collines, en economie par les usines lointaines, ou en offensive par
    /// une tete de pont sur un Centre de Commandement.
    ///
    /// LA CARTE (rayon 7, 169 cases), a symetrie d'ordre 6 - aucun joueur n'est
    /// avantage par un cote :
    ///
    ///   - 6 Shofars aux six pointes ;
    ///   - la Base au centre, sur 7 cases ;
    ///   - 6 collines a 3 cases du centre, UNE PAR AXE, exactement sur le couloir qui
    ///     va de la Base au Shofar de cet axe. Un Bunker y porte a 2 cases : il couvre
    ///     donc le couloir depuis le bord de la Base jusqu'a mi-chemin du Shofar ;
    ///   - 6 collines avancees au bord du plateau, entre deux Shofars : des positions
    ///     de siege, exposees, pour qui veut pousser ;
    ///   - 6 usines de Gaz a 4 cases, chacune couverte par une colline interieure :
    ///     l'economie sure, celle qu'on peut defendre ;
    ///   - 6 usines de Gaz au bord, a 7 cases : l'economie risquee, sous le nez des
    ///     Shofars ;
    ///   - 6 Cristaux a 5 cases, en avant de la ligne des collines : pour faire evoluer
    ///     un Tank, il faut donc sortir de chez soi ;
    ///   - 6 Centres de Commandement a 5 cases, jamais colles a la Base (la Base joue
    ///     deja ce role chez soi) : ce sont des tetes de pont.
    ///
    /// REGLE DE VOISINAGE : aucun site (colline, montagne, gaz, cristal, Shofar, Base)
    /// n'en touche un autre. Chaque site est entoure UNIQUEMENT de desert et de plaine,
    /// donc de cases ou les Tanks et les ennemis circulent. Sans cette regle, un site
    /// pouvait naitre injouable - ni defendable, ni attaquable.
    ///
    /// Le desert et la plaine sont poses par une regle fixe, elle aussi symetrique :
    /// deux parties donnent exactement le meme dessin.
    ///
    /// Note d'optimisation : plus aucun tirage, plus aucune re-tentative. La carte est
    /// construite en un seul passage sur les 169 cases.
    /// </summary>
    public static class MapGenerator
    {
        public const int BoardRadius = 7;

        // Les six directions cubiques. Multipliees par le rayon, elles donnent les
        // six pointes du plateau ; utilisees telles quelles, les six voisins d'une case.
        private static readonly int[,] Directions =
        {
            {  1,  0, -1 },
            {  1, -1,  0 },
            {  0, -1,  1 },
            { -1,  0,  1 },
            { -1,  1,  0 },
            {  0,  1, -1 }
        };

        // La Base : la case centrale et ses six voisines.
        private static readonly int[,] BaseOffsets =
        {
            {  0,  0,  0 },
            {  1,  0, -1 },
            {  1, -1,  0 },
            {  0, -1,  1 },
            { -1,  0,  1 },
            { -1,  1,  0 },
            {  0,  1, -1 }
        };

        /// <summary>
        /// LES SIX SITES, donnes par UNE case chacun. Les cinq autres copies sont
        /// obtenues par rotation de 60 degres : la carte est donc forcement symetrique,
        /// et il n'y a qu'une ligne a changer pour deplacer les six exemplaires d'un
        /// site.
        ///
        /// Ces positions ont ete choisies pour que rien ne se touche et que chaque site
        /// ait un role clair (voir le commentaire de la classe).
        /// </summary>
        private static readonly int[,] SiteSeeds =
        {
            { -3,  0,  3 },   // colline interieure : sur le couloir Base -> Shofar
            { -7,  2,  5 },   // colline avancee : position de siege, au bord
            { -4,  2,  2 },   // gaz sur : couvert par la colline interieure
            { -7,  4,  3 },   // gaz expose : au bord, sous le nez des Shofars
            { -5,  1,  4 },   // cristal : en avant de la ligne des collines
            { -5,  4,  1 }    // centre de commandement : tete de pont
        };

        private static readonly TypeOfHex[] SiteTypes =
        {
            TypeOfHex.hill,
            TypeOfHex.hill,
            TypeOfHex.gas,
            TypeOfHex.gas,
            TypeOfHex.crystal,
            TypeOfHex.mountain
        };

        public static string GenerateIntelligentBoard()
        {
            int radius = BoardRadius;

            Dictionary<int, TypeOfHex> special = BuildSpecialSites();

            List<HexagonData> map = new List<HexagonData>(256);

            int portals = 0, bases = 0;

            for (int q = -radius; q <= radius; q++)
            {
                int r1 = Mathf.Max(-radius, -q - radius);
                int r2 = Mathf.Min(radius, -q + radius);

                for (int r = r1; r <= r2; r++)
                {
                    int s = -q - r;
                    HexCoord coord = new HexCoord(q, r, s);

                    TypeOfHex typeID;

                    if (IsCorner(q, r, s, radius)) { typeID = TypeOfHex.portal; portals++; }
                    else if (IsBase(q, r, s)) { typeID = TypeOfHex.Base; bases++; }
                    else
                    {
                        TypeOfHex site;
                        typeID = special.TryGetValue(Key(q, r, s), out site) ? site : GroundAt(q, r, s);
                    }

                    int level = (typeID == TypeOfHex.portal || typeID == TypeOfHex.Base) ? 1 : 0;
                    int hp = MNLTHII.Rules.InteractionRules.GetBuildingMaxHP(typeID, level);

                    map.Add(new HexagonData
                    {
                        from = coord,
                        to = coord,
                        type = "hex",
                        typeID = typeID,
                        level = level,
                        energy = hp,
                        CP = 0,
                        threshold = 0
                    });
                }
            }

            Debug.LogFormat("[MapGenerator] Plateau fixe : {0} cases, {1} Shofars, {2} cases de Base, "
                          + "{3} sites (collines, gaz, cristaux, centres).",
                            map.Count, portals, bases, special.Count);

            return JsonConvert.SerializeObject(map);
        }

        // =================================================================
        //  LES SITES
        // =================================================================
        /// <summary>
        /// Chaque graine, tournee cinq fois de 60 degres. La rotation cubique d'un
        /// soixantieme de tour est (q, r, s) -> (-r, -s, -q) : elle garde la distance au
        /// centre, donc les six copies sont a la meme distance de la Base.
        /// </summary>
        private static Dictionary<int, TypeOfHex> BuildSpecialSites()
        {
            Dictionary<int, TypeOfHex> sites = new Dictionary<int, TypeOfHex>(64);

            for (int i = 0; i < SiteSeeds.GetLength(0); i++)
            {
                int q = SiteSeeds[i, 0];
                int r = SiteSeeds[i, 1];
                int s = SiteSeeds[i, 2];

                for (int turn = 0; turn < 6; turn++)
                {
                    sites[Key(q, r, s)] = SiteTypes[i];

                    int nq = -r, nr = -s, ns = -q;
                    q = nq; r = nr; s = ns;
                }
            }

            return sites;
        }

        // =================================================================
        //  LE SOL : desert ou plaine, toujours pareil
        // =================================================================
        /// <summary>
        /// Le dessin du sol. Il ne change rien au jeu - desert et plaine se jouent
        /// exactement de la meme facon - mais il doit etre STABLE (le joueur reconnait
        /// sa carte) et SYMETRIQUE (aucun secteur ne doit avoir l'air different).
        ///
        /// On calcule donc le motif sur le REPRESENTANT de la famille de rotation : les
        /// six cases qui se correspondent d'un secteur a l'autre recoivent forcement le
        /// meme terrain.
        /// </summary>
        private static TypeOfHex GroundAt(int q, int r, int s)
        {
            int rq = q, rr = r;

            // Le representant : la plus petite des six rotations, dans l'ordre q puis r.
            int cq = q, cr = r, cs = s;
            for (int turn = 0; turn < 5; turn++)
            {
                int nq = -cr, nr = -cs, ns = -cq;
                cq = nq; cr = nr; cs = ns;

                if (cq < rq || (cq == rq && cr < rr)) { rq = cq; rr = cr; }
            }

            int ring = Mathf.Max(Mathf.Abs(q), Mathf.Max(Mathf.Abs(r), Mathf.Abs(s)));
            int hash = Mathf.Abs(rq * 7 + rr * 13 + ring * 5) % 5;

            return (hash < 2) ? TypeOfHex.desert : TypeOfHex.plain;
        }

        private static bool IsCorner(int q, int r, int s, int radius)
        {
            for (int i = 0; i < 6; i++)
            {
                if (q == Directions[i, 0] * radius
                    && r == Directions[i, 1] * radius
                    && s == Directions[i, 2] * radius) return true;
            }
            return false;
        }

        private static bool IsBase(int q, int r, int s)
        {
            for (int i = 0; i < BaseOffsets.GetLength(0); i++)
            {
                if (q == BaseOffsets[i, 0] && r == BaseOffsets[i, 1] && s == BaseOffsets[i, 2]) return true;
            }
            return false;
        }

        /// <summary>Cle entiere compacte d'une case : |q| et |r| restent bien sous 256.</summary>
        private static int Key(int q, int r, int s)
        {
            return ((q + 256) << 9) | (r + 256);
        }

        // =================================================================
        //  DEPLOIEMENT DE DEPART
        // =================================================================
        /// <summary>
        /// Deploiement de depart. Les deux compteurs sont a zero (voir
        /// InteractionRules.INITIAL_PLAYER_TANKS et INITIAL_ENEMIES) : la partie commence
        /// sur un plateau vide, et tout ce qui s'y trouvera ensuite aura ete decide.
        /// </summary>
        public static void SpawnInitialUnits(BoardController board)
        {
            if (board == null || board.HexagonsInBoard == null) return;

            HexCoord[] playerSpawns =
            {
                new HexCoord(2, -1, -1),
                new HexCoord(-1, 2, -1),
                new HexCoord(-1, -1, 2)
            };

            int tanks = Mathf.Min(MNLTHII.Rules.InteractionRules.INITIAL_PLAYER_TANKS, playerSpawns.Length);

            for (int i = 0; i < tanks; i++)
            {
                Hexagon hex = board.getHexByCoord(playerSpawns[i]);
                if (MNLTHII.Rules.InteractionRules.IsHexWalkable(hex)) board.SpawnUnitVisual(hex, 1, "unit");
            }

            SpawnInitialEnemies(board);
        }

        /// <summary>
        /// Ennemis Niveau 1 poses a mi-chemin sur des axes bien separes, et rattaches au
        /// Portail de leur axe : leur mort compte donc dans l'instabilite de ce Portail,
        /// exactement comme celle d'un ennemi qu'il aurait deploye lui-meme.
        /// </summary>
        private static void SpawnInitialEnemies(BoardController board)
        {
            int count = MNLTHII.Rules.InteractionRules.INITIAL_ENEMIES;
            if (count <= 0) return;

            int distance = MNLTHII.Rules.InteractionRules.INITIAL_ENEMY_DISTANCE;
            int step = Mathf.Max(1, 6 / count);   // un axe sur deux pour trois ennemis

            int spawned = 0;

            for (int i = 0; i < count; i++)
            {
                int d = (i * step) % 6;

                HexCoord wanted = new HexCoord(Directions[d, 0] * distance,
                                               Directions[d, 1] * distance,
                                               Directions[d, 2] * distance);

                Hexagon hex = FindFreeSpawnHex(board, wanted);
                if (hex == null) continue;

                PawnController enemy = board.SpawnUnitVisual(hex, 1, "enemy");
                if (enemy == null) continue;

                // Rattachement au Portail du meme axe, a la pointe du plateau.
                enemy.SetOriginPortal(new HexCoord(Directions[d, 0] * BoardRadius,
                                                   Directions[d, 1] * BoardRadius,
                                                   Directions[d, 2] * BoardRadius));

                if (MNLTHII.Managers.FXManager.Instance != null)
                    MNLTHII.Managers.FXManager.Instance.SpawnEnemyFX(enemy.transform.position);

                spawned++;
            }

            Debug.LogFormat("[MapGenerator] {0} ennemi(s) deja en marche au premier tour.", spawned);
        }

        /// <summary>
        /// La case voulue, ou sa premiere voisine libre : les sites (collines, usines...)
        /// ne se marchent pas, et une case occupee refuserait le deploiement.
        /// </summary>
        private static Hexagon FindFreeSpawnHex(BoardController board, HexCoord wanted)
        {
            Hexagon hex = board.getHexByCoord(wanted);
            if (IsFreeForEnemy(board, hex)) return hex;

            for (int n = 0; n < 6; n++)
            {
                HexCoord neighbour = new HexCoord(wanted.q + Directions[n, 0],
                                                  wanted.r + Directions[n, 1],
                                                  wanted.s + Directions[n, 2]);

                Hexagon candidate = board.getHexByCoord(neighbour);
                if (IsFreeForEnemy(board, candidate)) return candidate;
            }
            return null;
        }

        private static bool IsFreeForEnemy(BoardController board, Hexagon hex)
        {
            if (!MNLTHII.Rules.InteractionRules.IsHexWalkable(hex)) return false;
            return board.getPawnByCoord(hex.positionInTheBoard) == null;
        }
    }
}
