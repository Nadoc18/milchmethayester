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
    ///   - 6 usines de Gaz a 4 cases, chacune couverte par une colline interieure :
    ///     l'economie, celle qu'on peut defendre ;
    ///   - 6 Cristaux a 5 cases, en avant de la ligne des collines : pour faire evoluer
    ///     un Tank, il faut donc sortir de chez soi ;
    ///   - 6 Centres de Commandement a 5 cases, jamais colles a la Base (la Base joue
    ///     deja ce role chez soi) : ce sont des tetes de pont ;
    ///   - et AUTOUR DE TOUT CELA, deux anneaux de desert nu.
    ///
    /// LE SOL, LUI, N'A PLUS QU'UNE FRONTIERE : plaine partout a l'interieur, desert
    /// partout dans les deux anneaux exterieurs. Le desert est donc exactement, et
    /// uniquement, ce que le Yetzer a desseche - voir GroundAt.
    ///
    /// LE GLACIS. Les deux derniers anneaux (72 cases, 43 % du plateau) ne portent
    /// plus rien : ni usine, ni colline, seulement du desert et les six Shofars.
    ///
    /// Il y avait la, avant, 6 usines exposees et 6 collines avancees. Elles
    /// promettaient une "economie risquee" et des "positions de siege" - en pratique
    /// elles offraient surtout de quoi s'installer au pied du Yetzer : un Bunker a
    /// deux cases d'un Shofar fauchait ses emissaires a leur sortie, sans risque et
    /// sans fin. On ne batit pas de forteresse chez lui.
    ///
    /// Ce qui reste est une bande morte qu'il faut TRAVERSER. Elle donne au joueur le
    /// temps de voir venir - l'ennemi marche trois tours a decouvert avant d'atteindre
    /// la premiere ligne - et elle fait de l'assaut un vrai depart : on quitte tout ce
    /// qu'on a construit, on n'emporte que des Tanks.
    ///
    /// Elle plafonne aussi l'economie : six usines au lieu de douze.
    ///
    /// REGLE DE VOISINAGE : aucun site (colline, montagne, gaz, cristal, Shofar, Base)
    /// n'en touche un autre. Chaque site est entoure UNIQUEMENT de desert et de plaine,
    /// donc de cases ou les Tanks et les ennemis circulent. Sans cette regle, un site
    /// pouvait naitre injouable - ni defendable, ni attaquable.
    ///
    /// Deux parties donnent exactement le meme dessin.
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
            { -4,  2,  2 },   // gaz : couvert par la colline interieure
            { -5,  1,  4 },   // cristal : en avant de la ligne des collines
            { -5,  4,  1 }    // centre de commandement : tete de pont
        };

        private static readonly TypeOfHex[] SiteTypes =
        {
            TypeOfHex.hill,
            TypeOfHex.gas,
            TypeOfHex.crystal,
            TypeOfHex.mountain
        };

        /// <summary>
        /// Premier anneau du glacis : a partir de lui, plus aucun site, rien que du
        /// desert et les Shofars. Le rayon du plateau etant 7, "6" laisse deux anneaux
        /// nus tout autour.
        /// </summary>
        public const int GlacisRing = 6;

        // =================================================================
        //  CE QUE LE GLACIS A EFFACE - ET QUI REVIENDRA
        // =================================================================
        //
        // LA TERRE REFLEURIT. Le desert des deux derniers anneaux n'est pas un decor :
        // c'est ce que le Yetzer a sterilise. Quand un Shofar tombe, SA part du
        // plateau - le sixieme qui lui fait face - redevient ce qu'elle etait avant
        // lui : la plaine reparait, et les deux sites que le glacis avait effaces sur
        // cet axe ressortent, intacts et a construire.
        //
        // C'est la seule recompense du jeu qui ne soit pas un chiffre. Fermer un
        // Shofar ne fait pas monter un compteur : ca rend une terre. Et cette terre
        // sert - une usine de plus, une colline a deux cases du Shofar voisin, et des
        // plaines ou poser des Tanks pour la suite de l'offensive. Le premier Shofar
        // abattu rend donc le deuxieme plus facile : la fin de partie s'accelere au
        // lieu de s'etaler.
        //
        // Ces deux familles vivent ICI, et nulle part ailleurs : le generateur se
        // souvient de ce qu'il a efface, et LandBloom vient le lui redemander.

        /// <summary>Les sites que le glacis retire, et que la victoire rend.</summary>
        private static readonly int[,] GlacisSiteSeeds =
        {
            { -7,  2,  5 },   // colline avancee : a 2 cases d'un Shofar, position de siege
            { -7,  4,  3 }    // gaz expose : l'economie du bout du monde
        };

        private static readonly TypeOfHex[] GlacisSiteTypes =
        {
            TypeOfHex.hill,
            TypeOfHex.gas
        };

        private static Dictionary<int, TypeOfHex> _glacisSites;

        /// <summary>
        /// Le site que le glacis a efface sur cette case, ou None. Rendu par LandBloom
        /// quand le Shofar de ce secteur tombe.
        /// </summary>
        public static TypeOfHex GlacisSiteAt(int q, int r, int s)
        {
            if (_glacisSites == null)
            {
                _glacisSites = new Dictionary<int, TypeOfHex>(16);

                for (int i = 0; i < GlacisSiteSeeds.GetLength(0); i++)
                {
                    int sq = GlacisSiteSeeds[i, 0];
                    int sr = GlacisSiteSeeds[i, 1];
                    int ss = GlacisSiteSeeds[i, 2];

                    for (int turn = 0; turn < 6; turn++)
                    {
                        _glacisSites[Key(sq, sr, ss)] = GlacisSiteTypes[i];

                        int nq = -sr, nr = -ss, ns = -sq;
                        sq = nq; sr = nr; ss = ns;
                    }
                }
            }

            TypeOfHex found;
            return _glacisSites.TryGetValue(Key(q, r, s), out found) ? found : TypeOfHex.None;
        }

        /// <summary>
        /// Le sol de cette case SANS la regle du glacis : ce qu'elle serait si le
        /// Yetzer ne l'avait pas desseche. C'est ce que LandBloom repose.
        /// </summary>
        public static TypeOfHex GroundWithoutGlacis(int q, int r, int s)
        {
            return NaturalGround(q, r, s);
        }

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
        //  LE SOL : UNE SEULE FRONTIERE, ET ELLE SE VOIT
        // =================================================================
        /// <summary>
        /// LE DESERT N'EXISTE QUE DANS LE GLACIS. Partout ailleurs, c'est de la plaine.
        ///
        /// CE QUE C'ETAIT, ET POURQUOI CA NE MARCHAIT PAS
        ///
        /// Le sol interieur etait tire d'un hachage : deux cases sur cinq en desert,
        /// reparties sur les anneaux 3, 4 et 5. Trente cases mortes eparpillees au
        /// milieu de chez soi.
        ///
        /// C'etait stable et symetrique - les deux qualites qu'on cherchait - mais
        /// c'etait ARBITRAIRE. Aucun joueur ne peut deviner un modulo. Il voyait un
        /// damier de sable et d'herbe sans logique, decouvrait au clic que celle-ci
        /// porte un Tank et pas celle-la, et n'en tirait aucune regle. Une contrainte
        /// qu'on ne peut pas anticiper n'est pas une contrainte, c'est du bruit.
        ///
        /// Pire, elle ABIMAIT LE SYMBOLE. Le desert, dans ce jeu, c'est ce que le Yetzer
        /// a desseche - c'est tout le sens du glacis et de la floraison. Mais il y en
        /// avait aussi au pied de la Base, sans raison, ce qui rendait le signe illisible.
        ///
        /// MAINTENANT IL N'Y A QU'UNE FRONTIERE :
        ///
        ///   anneaux 0 a 5   plaine, partout, on y pose ses Tanks
        ///   anneaux 6 et 7  desert, partout, on ne fait que passer
        ///
        /// Le desert est devenu exactement et uniquement l'oeuvre du Yetzer. Le joueur
        /// lit la carte d'un coup d'oeil, et la floraison qui suit la chute d'un Shofar
        /// prend enfin tout son sens : elle rend de la PLAINE, pas encore du sable.
        ///
        /// (Cela corrigeait au passage une incoherence : l'ancienne floraison reposait
        /// cinq cases de desert par secteur. Le Yetzer tombait, et sa terre restait
        /// morte pour moitie.)
        /// </summary>
        private static TypeOfHex GroundAt(int q, int r, int s)
        {
            int ring = Mathf.Max(Mathf.Abs(q), Mathf.Max(Mathf.Abs(r), Mathf.Abs(s)));

            // LE GLACIS : les deux derniers anneaux sont du desert, sans exception.
            // Pas de plaine non plus - une plaine porterait un Tank, et on pourrait
            // donc s'installer a la porte du Shofar. On traverse, on ne s'installe pas.
            //
            // Jusqu'a ce que ce Shofar tombe : voir LandBloom, qui repose alors le sol
            // naturel ci-dessous sur tout son secteur.
            if (ring >= GlacisRing) return TypeOfHex.desert;

            return NaturalGround(q, r, s);
        }

        /// <summary>
        /// Le sol tel qu'il serait sans le Yetzer : de la plaine, partout.
        ///
        /// C'est aussi ce que LandBloom repose quand un Shofar tombe - d'ou le fait
        /// que cette methode reste separee de GroundAt au lieu d'etre repliee dedans :
        /// l'une decrit la carte telle qu'elle est, l'autre telle qu'elle devrait etre.
        /// </summary>
        private static TypeOfHex NaturalGround(int q, int r, int s)
        {
            return TypeOfHex.plain;
        }

        /// <summary>
        /// Les six pointes du plateau, ou vivent les Shofars. Rendues telles quelles
        /// meme si le Shofar est tombe : elles decoupent le plateau en six secteurs, et
        /// un secteur ne bouge pas parce que son Shofar est mort - c'est justement lui
        /// qui va refleurir.
        /// </summary>
        public static void GetPortalCorner(int index, out int q, out int r, out int s)
        {
            int i = ((index % 6) + 6) % 6;
            q = Directions[i, 0] * BoardRadius;
            r = Directions[i, 1] * BoardRadius;
            s = Directions[i, 2] * BoardRadius;
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
