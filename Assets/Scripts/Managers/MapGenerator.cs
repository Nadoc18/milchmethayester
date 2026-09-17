using UnityEngine;
using MNLTHII;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MNLTHII.Managers
{
    /// <summary>
    /// Generation procedurale du plateau.
    ///
    /// Disposition : un hexagone de rayon 7 (169 cases).
    ///   - 6 Portails, un a chacune des six pointes du plateau ;
    ///   - la Base au centre, sur 7 cases ;
    ///   - tout le reste tire dans un sac de terrains melange, avec anti-agregation
    ///     pour eviter les grosses taches d'un meme type.
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

        private static readonly TypeOfHex[] TerrainTypes =
        {
            TypeOfHex.plain,
            TypeOfHex.desert,
            TypeOfHex.mountain,
            TypeOfHex.hill,
            TypeOfHex.gas,
            TypeOfHex.crystal
        };

        public static string GenerateIntelligentBoard()
        {
            int radius = BoardRadius;

            HashSet<string> portals = BuildCornerSet(radius);
            HashSet<string> bases = BuildBaseSet();

            // Toutes les cases du plateau, pour savoir combien de terrains il faut tirer.
            List<HexCoord> allCoords = new List<HexCoord>(256);
            for (int q = -radius; q <= radius; q++)
            {
                int r1 = Mathf.Max(-radius, -q - radius);
                int r2 = Mathf.Min(radius, -q + radius);
                for (int r = r1; r <= r2; r++)
                {
                    allCoords.Add(new HexCoord(q, r, -q - r));
                }
            }

            int terrainCount = 0;
            for (int i = 0; i < allCoords.Count; i++)
            {
                string key = Key(allCoords[i]);
                if (!portals.Contains(key) && !bases.Contains(key)) terrainCount++;
            }

            List<TypeOfHex> terrainBag = BuildShuffledBag(terrainCount);

            Dictionary<string, TypeOfHex> assignedTypes = new Dictionary<string, TypeOfHex>(256);
            List<HexagonData> map = new List<HexagonData>(allCoords.Count);

            for (int i = 0; i < allCoords.Count; i++)
            {
                HexCoord coord = allCoords[i];
                string key = Key(coord);

                TypeOfHex typeID;

                if (portals.Contains(key)) typeID = TypeOfHex.portal;
                else if (bases.Contains(key)) typeID = TypeOfHex.Base;
                else typeID = DrawTerrain(terrainBag, assignedTypes, coord);

                assignedTypes[key] = typeID;

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

            Debug.LogFormat("[MapGenerator] Plateau genere : {0} cases, {1} portails aux pointes, {2} cases de Base.",
                            map.Count, portals.Count, bases.Count);

            return JsonConvert.SerializeObject(map);
        }

        /// <summary>Les six pointes du plateau : (N,0,-N), (N,-N,0), (0,-N,N), etc.</summary>
        private static HashSet<string> BuildCornerSet(int radius)
        {
            HashSet<string> set = new HashSet<string>();

            for (int i = 0; i < 6; i++)
            {
                int q = Directions[i, 0] * radius;
                int r = Directions[i, 1] * radius;
                int s = Directions[i, 2] * radius;
                set.Add(string.Format("{0},{1},{2}", q, r, s));
            }
            return set;
        }

        private static HashSet<string> BuildBaseSet()
        {
            HashSet<string> set = new HashSet<string>();

            for (int i = 0; i < BaseOffsets.GetLength(0); i++)
            {
                set.Add(string.Format("{0},{1},{2}", BaseOffsets[i, 0], BaseOffsets[i, 1], BaseOffsets[i, 2]));
            }
            return set;
        }

        /// <summary>
        /// Sac de terrains equilibre puis melange. La taille est calculee a partir du
        /// nombre reel de cases libres : pas de sac vide en fin de generation.
        /// </summary>
        private static List<TypeOfHex> BuildShuffledBag(int needed)
        {
            int perType = Mathf.CeilToInt(needed / (float)TerrainTypes.Length);

            List<TypeOfHex> bag = new List<TypeOfHex>(perType * TerrainTypes.Length);
            for (int i = 0; i < perType; i++)
            {
                for (int t = 0; t < TerrainTypes.Length; t++) bag.Add(TerrainTypes[t]);
            }

            for (int i = 0; i < bag.Count; i++)
            {
                int j = Random.Range(i, bag.Count);
                TypeOfHex tmp = bag[i];
                bag[i] = bag[j];
                bag[j] = tmp;
            }
            return bag;
        }

        /// <summary>
        /// Tire un terrain en evitant de coller deux cases identiques. Dix essais, puis
        /// on accepte le dernier candidat : cela suffit a casser les grosses taches sans
        /// jamais bloquer la generation.
        /// </summary>
        private static TypeOfHex DrawTerrain(List<TypeOfHex> bag, Dictionary<string, TypeOfHex> assigned, HexCoord coord)
        {
            if (bag.Count == 0) return TypeOfHex.plain;

            int chosenIndex = -1;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                int testIndex = Random.Range(0, bag.Count);
                TypeOfHex testType = bag[testIndex];

                if (attempt == 9 || CountSameNeighbours(assigned, coord, testType) == 0)
                {
                    chosenIndex = testIndex;
                    break;
                }
            }

            if (chosenIndex < 0) chosenIndex = 0;

            TypeOfHex result = bag[chosenIndex];
            bag.RemoveAt(chosenIndex);
            return result;
        }

        private static int CountSameNeighbours(Dictionary<string, TypeOfHex> assigned, HexCoord coord, TypeOfHex type)
        {
            int count = 0;

            for (int i = 0; i < 6; i++)
            {
                int nq = coord.q + Directions[i, 0];
                int nr = coord.r + Directions[i, 1];
                int ns = coord.s + Directions[i, 2];

                TypeOfHex neighbourType;
                if (assigned.TryGetValue(string.Format("{0},{1},{2}", nq, nr, ns), out neighbourType)
                    && neighbourType == type)
                {
                    count++;
                }
            }
            return count;
        }

        private static string Key(HexCoord c)
        {
            return string.Format("{0},{1},{2}", c.q, c.r, c.s);
        }

        /// <summary>
        /// Deploiement de depart : les trois Tanks du joueur autour de la Base, et une
        /// premiere menace deja en marche.
        ///
        /// Pourquoi des ennemis des le tour 1 : les Portails ne deploient qu'en fin de
        /// tour, si bien que le plateau restait desesperement vide au debut. Le joueur
        /// cliquait, terminait son tour, et rien ne se passait - au point de croire que
        /// le jeu ne fonctionnait pas. Trois ennemis deja en route a cinq cases donnent
        /// une menace lisible immediatement, sans rien enlever au temps d'installation.
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

            for (int i = 0; i < playerSpawns.Length; i++)
            {
                Hexagon hex = board.getHexByCoord(playerSpawns[i]);
                if (hex != null && hex.type != TypeOfHex.hill) board.SpawnUnitVisual(hex, 1, "unit");
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
        /// La case voulue, ou sa premiere voisine libre. Les collines sont des obstacles
        /// naturels et une case occupee refuserait le deploiement : sans ce repli, un
        /// tirage de terrain malchanceux supprimait silencieusement un ennemi de depart.
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
            if (hex == null) return false;
            if (hex.type == TypeOfHex.hill) return false;      // obstacle naturel
            if (hex.type == TypeOfHex.portal) return false;
            if (hex.type == TypeOfHex.Base) return false;
            return board.getPawnByCoord(hex.positionInTheBoard) == null;
        }
    }
}
