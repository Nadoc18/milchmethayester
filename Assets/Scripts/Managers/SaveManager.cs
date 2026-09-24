using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using MNLTHII.Data;
using MNLTHII.Rules;

namespace MNLTHII.Managers
{
    /// <summary>
    /// LA SAUVEGARDE LOCALE DES PARTIES.
    ///
    /// CE QU'ELLE RESOUT
    ///
    /// Une partie de Milchemet HaYetzer se joue en dizaines de tours. Tant que rien
    /// n'etait ecrit sur le disque, fermer le jeu effacait tout : on ne rejouait
    /// jamais que l'ouverture. Ici, la partie en cours est ecrite TOUTE SEULE au
    /// debut de chaque phase de depense, dans un fichier JSON, et le menu principal
    /// propose de reprendre n'importe quelle partie non achevee.
    ///
    /// POURQUOI AU DEBUT DE LA PHASE DE DEPENSE, ET PAS AILLEURS
    ///
    /// C'est le seul moment du tour ou le jeu est VRAIMENT au repos : aucune
    /// coroutine en vol, aucune camera en voyage, aucun ennemi a mi-chemin entre
    /// deux cases. C'est aussi l'instant ou l'on veut revenir - la main au joueur,
    /// le revenu du tour deja verse. Reprendre une partie ne redonne donc pas le
    /// revenu une seconde fois : on ne rejoue pas le debut du tour, on reprend
    /// exactement la ou le joueur avait la main.
    ///
    /// CE QUI EST ECRIT
    ///
    /// Les 169 cases (type, niveau, PV), les pions avec leurs ORDRES (garder quoi,
    /// traquer qui), l'Energie, les PV de la Base, l'etat de chaque Shofar. Tout le
    /// reste - camera, animations, apercu de menace, auras - se recalcule au
    /// chargement, donc n'a aucune raison d'occuper le fichier.
    ///
    /// OU C'EST ECRIT
    ///
    /// Ce gestionnaire ne sait PAS ou les parties sont rangees : il produit et relit
    /// du JSON, et confie le rangement a un ISaveStore. Par defaut c'est
    /// LocalSaveStore, qui ecrit dans Application.persistentDataPath/parties/.
    ///
    /// EN WEBGL, LES PARTIES IRONT SUR LE SITE QNIGAME. Ce n'est pas encore branche.
    /// Le jour ou ca le sera, il n'y aura RIEN a changer ici : il suffira d'ecrire un
    /// QniGameSaveStore (trois methodes) et de le poser dans SaveManager.Store au
    /// demarrage. Tout le reste - capture, reconstruction, liste du menu - continue
    /// de marcher tel quel, parce que rien d'autre ne parle du rangement.
    ///
    /// Note d'optimisation : rien ne tourne en continu. Une capture par tour, une
    /// liste au menu. Les 169 cases sont ecrites avec des noms de champs d'une ou
    /// deux lettres (voir SaveGameData) : le fichier fait quelques kilo-octets.
    /// </summary>
    public static class SaveManager
    {
        /// <summary>
        /// Au-dela, la plus vieille partie non touchee est effacee. Six, parce que
        /// c'est ce que la liste du menu montre d'un coup d'oeil sans defilement : une
        /// liste qu'il faut faire defiler pour retrouver sa partie ne rend pas service.
        /// </summary>
        public const int MaxSlots = 6;

        private const int FormatVersion = 1;

        /// <summary>
        /// OU LES PARTIES SONT RANGEES. Par defaut : des fichiers, a cote du jeu.
        ///
        /// C'est le SEUL point a changer pour ranger les parties ailleurs - sur le
        /// site qnigame en WebGL, par exemple. Poser son magasin ici, une fois au
        /// demarrage, suffit : rien d'autre dans le jeu ne sait ou vivent les parties.
        /// </summary>
        public static ISaveStore Store
        {
            get
            {
                if (_store == null) _store = new LocalSaveStore();
                return _store;
            }
            set { _store = value; }
        }

        private static ISaveStore _store;

        /// <summary>
        /// La partie que le menu a demande de reprendre. Lue une seule fois par
        /// LocalGameEngine au chargement de la scene de jeu, puis remise a null.
        /// </summary>
        public static SaveGameFile PendingLoad;

        /// <summary>
        /// Identifiant de la partie en cours. Il ne change pas d'un tour a l'autre :
        /// une partie occupe UN fichier, qui est reecrit. Sans cela, vingt tours
        /// donneraient vingt parties dans la liste du menu.
        /// </summary>
        public static string CurrentGameId;

        // =================================================================
        //  CYCLE DE VIE D'UNE PARTIE
        // =================================================================
        /// <summary>Une nouvelle partie commence : elle aura son propre fichier.</summary>
        public static void BeginNewGame()
        {
            CurrentGameId = "partie_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Debug.LogFormat("[Sauvegarde] Nouvelle partie : {0}", CurrentGameId);
        }

        /// <summary>
        /// Ecrit l'etat courant. Appele au debut de chaque phase de depense ; sans
        /// bruit, sans interrompre quoi que ce soit.
        /// </summary>
        public static void AutoSave()
        {
            if (string.IsNullOrEmpty(CurrentGameId)) BeginNewGame();

            SaveGameFile file = Capture();
            if (file == null) return;

            Write(file);
            Prune();
        }

        /// <summary>
        /// La partie est finie (victoire ou defaite) : son fichier disparait. Le menu
        /// ne propose QUE des parties non achevees - reprendre une partie perdue
        /// n'aurait aucun sens.
        /// </summary>
        public static void ForgetCurrentGame()
        {
            if (string.IsNullOrEmpty(CurrentGameId)) return;

            Delete(CurrentGameId);
            CurrentGameId = null;
        }

        // =================================================================
        //  CAPTURE
        // =================================================================
        private static SaveGameFile Capture()
        {
            BoardController board = BoardController.instance;
            if (board == null || board.HexagonsInBoard == null) return null;

            SaveGameFile file = new SaveGameFile();
            file.version = FormatVersion;
            file.id = CurrentGameId;
            file.savedAtTicks = DateTime.Now.Ticks;
            file.savedAt = DateTime.Now.ToString("dd/MM HH:mm");
            file.difficulty = (int)GameDifficulty.Current;

            TurnManager turns = TurnManager.Instance;
            file.turn = (turns != null) ? turns.currentTurn : 1;

            file.energy = (EnergyManager.Instance != null) ? EnergyManager.Instance.CurrentEnergy : 0;

            BuildingManager buildings = BuildingManager.Instance;
            file.baseHP = (buildings != null) ? buildings.GetBaseHP() : InteractionRules.BASE_HP;
            file.baseMaxHP = InteractionRules.BASE_HP;

            PortalManager portals = PortalManager.Instance;

            // --- les cases ---
            List<Hexagon> hexes = board.HexagonsInBoard;
            for (int i = 0; i < hexes.Count; i++)
            {
                Hexagon hex = hexes[i];
                if (hex == null || hex.positionInTheBoard == null) continue;

                bool dead = (hex.type == TypeOfHex.Destroyed);

                SavedHex saved = new SavedHex();
                saved.q = hex.positionInTheBoard.q;
                saved.r = hex.positionInTheBoard.r;
                saved.t = (int)hex.type;
                saved.l = hex.level;
                saved.hp = hex.currentHP;
                saved.ta = hex.turnsAlive;
                saved.sh = hex.shieldHP;
                saved.dead = dead;
                file.hexes.Add(saved);

                if (hex.type == TypeOfHex.portal)
                {
                    file.portalsAlive++;

                    SavedPortal portalState = new SavedPortal();
                    portalState.q = saved.q;
                    portalState.r = saved.r;
                    if (portals != null)
                    {
                        portalState.kills = portals.GetInstability(hex);
                        portalState.shieldDown = portals.GetShieldDownTurns(hex);
                        portalState.calm = portals.GetCalmTurns(hex);
                    }
                    file.portals.Add(portalState);
                }

                if (dead) file.portalsTotal++;
            }

            // Les epaves comptent dans le total : "3/6 Shofars" ne veut rien dire si le
            // denominateur retrecit a mesure qu'on gagne.
            file.portalsTotal += file.portalsAlive;

            // --- les pions ---
            List<PawnController> pawns = board.PawnsInBoard;
            if (pawns != null)
            {
                for (int i = 0; i < pawns.Count; i++)
                {
                    PawnController pawn = pawns[i];
                    if (pawn == null || pawn.hexcoord == null) continue;
                    if (pawn.currentHP <= 0) continue;

                    SavedPawn saved = new SavedPawn();
                    saved.q = pawn.hexcoord.q;
                    saved.r = pawn.hexcoord.r;
                    saved.foe = pawn.IsEnemy;
                    saved.lvl = pawn.level;
                    saved.hp = pawn.currentHP;
                    saved.st = (int)pawn.stance;
                    saved.crys = pawn.seekCrystal;

                    if (pawn.orderTargetCoord != null)
                    {
                        saved.hasOrder = true;
                        saved.oq = pawn.orderTargetCoord.q;
                        saved.or = pawn.orderTargetCoord.r;
                    }

                    if (pawn.orderTargetPawn != null && pawn.orderTargetPawn.hexcoord != null)
                    {
                        saved.hasPrey = true;
                        saved.pq = pawn.orderTargetPawn.hexcoord.q;
                        saved.pr = pawn.orderTargetPawn.hexcoord.r;
                    }

                    if (pawn.HasOriginPortal)
                    {
                        saved.hasOrigin = true;
                        saved.gq = pawn.originPortalQ;
                        saved.gr = pawn.originPortalR;
                    }

                    file.pawns.Add(saved);

                    if (pawn.IsEnemy) file.foes++;
                    else file.tanks++;
                }
            }

            // --- compteurs et vague annoncee ---
            if (portals != null)
            {
                file.enemiesSpawned = portals.totalEnemiesSpawned;
                file.enemiesKilled = portals.totalEnemiesKilled;

                HexCoord surge = portals.AnnouncedSurge;
                if (surge != null)
                {
                    file.hasSurge = true;
                    file.surgeQ = surge.q;
                    file.surgeR = surge.r;
                }
                file.lastSurgeTurn = portals.LastSurgeTurn;
            }

            return file;
        }

        // =================================================================
        //  RECONSTRUCTION
        // =================================================================
        /// <summary>
        /// Le plateau sauvegarde, au format que BoardController.UpdateBoard attend.
        ///
        /// On repasse par le MEME chemin qu'une partie neuve : c'est ce qui garantit
        /// que les modeles 3D, les barres de vie et les epaves sont crees exactement
        /// comme d'habitude. Une structure a zero PV se recharge en epave toute seule,
        /// puisque c'est deja ce que fait FillBoard.
        /// </summary>
        public static string BuildBoardJson(SaveGameFile file)
        {
            if (file == null || file.hexes == null) return null;

            List<HexagonData> map = new List<HexagonData>(file.hexes.Count);

            for (int i = 0; i < file.hexes.Count; i++)
            {
                SavedHex saved = file.hexes[i];
                if (saved == null) continue;

                TypeOfHex type = (TypeOfHex)saved.t;
                int level = saved.l;
                int hp = saved.hp;

                // Une epave est rechargee comme le Shofar qu'elle etait, a zero PV :
                // c'est la seule facon d'obtenir son modele de ruine. Son type reel
                // (Destroyed) est remis juste apres, dans RestoreInto.
                if (saved.dead)
                {
                    type = TypeOfHex.portal;
                    level = 1;
                    hp = 0;
                }

                HexCoord coord = new HexCoord(saved.q, saved.r, -saved.q - saved.r);

                HexagonData data = new HexagonData();
                data.from = coord;
                data.to = coord;
                data.type = "hex";
                data.typeID = type;
                data.level = level;
                data.energy = hp;
                data.CP = 0;
                data.threshold = 0;
                map.Add(data);
            }

            return JsonConvert.SerializeObject(map);
        }

        /// <summary>
        /// Remet la partie dans l'etat du fichier, une fois le plateau construit.
        /// A appeler apres UpdateBoard, quand les 169 cases existent.
        /// </summary>
        public static void RestoreInto(SaveGameFile file, BoardController board)
        {
            if (file == null || board == null) return;

            CurrentGameId = file.id;
            GameDifficulty.Current = (DifficultyLevel)file.difficulty;

            RestoreHexes(file, board);
            RestorePawns(file, board);

            if (EnergyManager.Instance != null) EnergyManager.Instance.ResetEnergy(file.energy);

            if (BuildingManager.Instance != null)
            {
                BuildingManager.Instance.InitializeBase();
                BuildingManager.Instance.RestoreBaseHP(file.baseHP);
            }

            RestorePortals(file, board);

            board.RefreshAllAuras();
            board.RefreshStructureHealthBars();

            // Les cordons ennemi-Shofar : ils n'existent que pour les ennemis nes d'un
            // deploiement. Ceux-la sortent d'un fichier, donc il faut les recenser.
            if (PortalTether.Instance != null) PortalTether.Instance.Refresh();

            Debug.LogFormat("[Sauvegarde] Partie reprise : tour {0}, {1} Energie, {2} Tank(s), "
                          + "{3} ennemi(s), {4} Shofar(s) debout.",
                            file.turn, file.energy, file.tanks, file.foes, file.portalsAlive);
        }

        private static void RestoreHexes(SaveGameFile file, BoardController board)
        {
            if (file.hexes == null) return;

            for (int i = 0; i < file.hexes.Count; i++)
            {
                SavedHex saved = file.hexes[i];
                if (saved == null) continue;

                Hexagon hex = board.getHexByCoord(new HexCoord(saved.q, saved.r, -saved.q - saved.r));
                if (hex == null) continue;

                if (saved.dead)
                {
                    // Exactement ce que fait BoardController.ReplaceWithDestroyedVisual :
                    // la case n'est plus un Shofar, elle ne compte plus pour la victoire.
                    hex.type = TypeOfHex.Destroyed;

                    // Le niveau distingue la RUINE (0) du Shofar RETOURNE (1). Le
                    // forcer a zero effacerait un retournement paye 120 - et avec lui
                    // le rang de fin de partie, qui se compte sur ces cases-la.
                    hex.level = (saved.l >= 1) ? 1 : 0;

                    hex.currentHP = 0;
                    hex.maxHP = 0;
                    hex.energy = 0;
                    hex.turnsAlive = 0;

                    // La case a ete construite comme un Shofar (c'est ce qui donne le bon
                    // modele de ruine) et habillee comme tel. Maintenant qu'elle a repris
                    // son vrai type, on la fait recalmer comme un terrain : sans cela, une
                    // epave garderait l'eclat d'un Shofar debout.
                    BoardReadability.NotifyHexReady(hex);

                    // Retourne : il lui faut son modele lumineux, pas l'epave.
                    if (hex.level >= 1) board.UpgradeHexVisual(hex);
                    continue;
                }

                hex.turnsAlive = saved.ta;

                if (hex.maxHP > 0 && saved.hp > 0)
                {
                    int hp = (saved.hp > hex.maxHP) ? hex.maxHP : saved.hp;
                    hex.currentHP = hp;
                    hex.energy = hp;
                }

                // LA BULLE D'UN CENTRE DE COMMANDEMENT.
                //
                // ResetShield d'abord, pour retrouver le maximum du rang ; la valeur
                // sauvegardee ensuite. Un fichier ecrit avant que la bulle existe porte
                // zero - on lui rend alors une bulle pleine plutot que de rendre au
                // joueur une tete de pont a nu qu'il croyait protegee.
                if (hex.type == TypeOfHex.mountain && hex.level >= 1)
                {
                    MNLTHII.Rules.InteractionRules.ResetShield(hex);

                    if (saved.sh > 0)
                        hex.shieldHP = (saved.sh > hex.shieldMax) ? hex.shieldMax : saved.sh;
                }
            }
        }

        private static void RestorePawns(SaveGameFile file, BoardController board)
        {
            if (file.pawns == null) return;

            for (int i = 0; i < file.pawns.Count; i++)
            {
                SavedPawn saved = file.pawns[i];
                if (saved == null) continue;

                Hexagon hex = board.getHexByCoord(new HexCoord(saved.q, saved.r, -saved.q - saved.r));
                if (hex == null) continue;

                PawnController pawn = board.SpawnUnitVisual(hex, saved.lvl, saved.foe ? "enemy" : "unit");
                if (pawn == null) continue;

                // ApplyStatsToPawn vient de remettre les PV au maximum : c'est ce qu'il
                // faut pour un pion neuf, pas pour un pion qui a deja encaisse.
                int hp = saved.hp;
                if (hp > pawn.maxHP) hp = pawn.maxHP;
                if (hp < 1) hp = 1;
                pawn.currentHP = hp;
                pawn.energy = hp;
                pawn.RefreshHealthBar();

                if (!saved.foe)
                {
                    pawn.SetStance((PawnStance)saved.st);
                    pawn.seekCrystal = saved.crys;
                    if (saved.hasOrder)
                        pawn.orderTargetCoord = new HexCoord(saved.oq, saved.or, -saved.oq - saved.or);
                }

                if (saved.hasOrigin)
                    pawn.SetOriginPortal(new HexCoord(saved.gq, saved.gr, -saved.gq - saved.gr));
            }

            // Deuxieme passage : la proie d'un chasseur est un AUTRE pion. Elle ne peut
            // etre retrouvee qu'une fois tout le monde repose sur le plateau.
            for (int i = 0; i < file.pawns.Count; i++)
            {
                SavedPawn saved = file.pawns[i];
                if (saved == null || saved.foe || !saved.hasPrey) continue;

                PawnController hunter = board.getPawnByCoord(new HexCoord(saved.q, saved.r, -saved.q - saved.r));
                if (hunter == null) continue;

                PawnController prey = board.getPawnByCoord(new HexCoord(saved.pq, saved.pr, -saved.pq - saved.pr));
                if (prey != null && prey.IsEnemy) hunter.orderTargetPawn = prey;
            }
        }

        private static void RestorePortals(SaveGameFile file, BoardController board)
        {
            PortalManager portals = PortalManager.Instance;
            if (portals == null) return;

            portals.ResetPortalState();
            portals.RestoreTotals(file.enemiesSpawned, file.enemiesKilled);

            if (file.portals != null)
            {
                for (int i = 0; i < file.portals.Count; i++)
                {
                    SavedPortal saved = file.portals[i];
                    if (saved == null) continue;

                    portals.RestorePortal(new HexCoord(saved.q, saved.r, -saved.q - saved.r),
                                          saved.kills, saved.shieldDown, saved.calm);
                }
            }

            HexCoord surge = file.hasSurge
                ? new HexCoord(file.surgeQ, file.surgeR, -file.surgeQ - file.surgeR)
                : null;

            portals.RestoreSurge(surge, file.lastSurgeTurn);
        }

        // =================================================================
        //  RANGEMENT : on delegue, on ne sait pas ou
        // =================================================================
        /// <summary>Les parties non achevees, la plus recente d'abord.</summary>
        public static List<SaveGameFile> List()
        {
            List<SaveGameFile> found = new List<SaveGameFile>(MaxSlots);

            List<string> raw = Store.ReadAll();
            if (raw == null) return found;

            for (int i = 0; i < raw.Count; i++)
            {
                SaveGameFile file = Parse(raw[i]);
                if (file != null) found.Add(file);
            }

            // Tri a la main : Sort avec une lambda alloue un comparateur, et la liste
            // fait six elements au plus.
            for (int i = 1; i < found.Count; i++)
            {
                SaveGameFile current = found[i];
                int j = i - 1;
                while (j >= 0 && found[j].savedAtTicks < current.savedAtTicks)
                {
                    found[j + 1] = found[j];
                    j--;
                }
                found[j + 1] = current;
            }

            return found;
        }

        public static bool HasAny()
        {
            List<SaveGameFile> all = List();
            return all != null && all.Count > 0;
        }

        public static void Delete(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            Store.Delete(id);
        }

        private static void Write(SaveGameFile file)
        {
            if (file == null || string.IsNullOrEmpty(file.id)) return;

            string json;
            try
            {
                json = JsonConvert.SerializeObject(file);
            }
            catch (Exception e)
            {
                Debug.LogWarningFormat("[Sauvegarde] Serialisation impossible : {0}", e.Message);
                return;
            }

            Store.Write(file.id, json);
        }

        /// <summary>Au-dela de MaxSlots parties, la plus ancienne s'efface.</summary>
        private static void Prune()
        {
            List<SaveGameFile> all = List();
            if (all == null || all.Count <= MaxSlots) return;

            for (int i = MaxSlots; i < all.Count; i++)
            {
                if (all[i] == null || all[i].id == CurrentGameId) continue;
                Delete(all[i].id);
            }
        }

        private static SaveGameFile Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;

            try
            {
                SaveGameFile file = JsonConvert.DeserializeObject<SaveGameFile>(json);
                if (file == null || file.version != FormatVersion || string.IsNullOrEmpty(file.id)) return null;
                return file;
            }
            catch (Exception e)
            {
                Debug.LogWarningFormat("[Sauvegarde] Partie illisible, ignoree : {0}", e.Message);
                return null;
            }
        }
    }

    // =====================================================================
    //  OU RANGER LES PARTIES
    // =====================================================================
    /// <summary>
    /// UN MAGASIN DE PARTIES. Trois gestes : tout relire, ecrire, effacer.
    ///
    /// SaveManager ne connait que ca. C'est ce qui permettra de ranger les parties
    /// sur le SITE QNIGAME en WebGL sans toucher une ligne du jeu : il suffira
    /// d'ecrire un QniGameSaveStore qui implemente ces trois methodes et de le poser
    /// dans SaveManager.Store au demarrage.
    ///
    /// POURQUOI TROIS METHODES QUI RENDENT LA MAIN TOUT DE SUITE, ALORS QU'UN SITE
    /// REPOND LENTEMENT
    ///
    /// Parce qu'un magasin distant n'a pas besoin d'attendre pour rendre service :
    ///   - ReadAll rend ce qu'il a DEJA en memoire, et se rafraichit en fond (une
    ///     requete au chargement du menu suffit : la liste ne change pas toute seule
    ///     pendant qu'on la regarde) ;
    ///   - Write part sans attendre la reponse. Le tour ne doit jamais s'arreter
    ///     parce qu'un serveur est lent, et si l'envoi echoue, le tour suivant
    ///     renvoie de toute facon l'etat complet - une sauvegarde perdue est
    ///     rattrapee une minute plus tard.
    ///
    /// C'est aussi vrai pour les fichiers : rien ici ne bloque une frame.
    /// </summary>
    public interface ISaveStore
    {
        /// <summary>Toutes les parties rangees, en JSON brut. Jamais null.</summary>
        List<string> ReadAll();

        /// <summary>Ecrit (ou reecrit) une partie sous cet identifiant.</summary>
        void Write(string id, string json);

        /// <summary>Efface cette partie.</summary>
        void Delete(string id);
    }

    /// <summary>
    /// LES PARTIES SUR LA MACHINE : un fichier .json par partie, dans
    /// Application.persistentDataPath/parties/.
    ///
    /// Si le disque refuse (dossier protege, plateforme sans systeme de fichiers),
    /// tout bascule sur PlayerPrefs - le meme JSON, range ailleurs. Ce n'est pas la
    /// solution prevue pour le navigateur, seulement un filet : en WebGL les parties
    /// iront sur le site qnigame, par un autre magasin.
    /// </summary>
    public class LocalSaveStore : ISaveStore
    {
        private const string FolderName = "parties";
        private const string PrefIndexKey = "mnlth_parties";
        private const string PrefGamePrefix = "mnlth_partie_";

        private bool _checked;
        private bool _useFiles;

        public List<string> ReadAll()
        {
            List<string> found = new List<string>(8);

            if (UseFiles())
            {
                try
                {
                    string folder = Folder();
                    if (!Directory.Exists(folder)) return found;

                    string[] paths = Directory.GetFiles(folder, "*.json");
                    for (int i = 0; i < paths.Length; i++)
                    {
                        string json = ReadText(paths[i]);
                        if (!string.IsNullOrEmpty(json)) found.Add(json);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarningFormat("[Sauvegarde] Lecture du dossier impossible : {0}", e.Message);
                }

                return found;
            }

            string index = PlayerPrefs.GetString(PrefIndexKey, "");
            if (string.IsNullOrEmpty(index)) return found;

            string[] ids = index.Split(';');
            for (int i = 0; i < ids.Length; i++)
            {
                if (string.IsNullOrEmpty(ids[i])) continue;
                string json = PlayerPrefs.GetString(PrefGamePrefix + ids[i], "");
                if (!string.IsNullOrEmpty(json)) found.Add(json);
            }

            return found;
        }

        public void Write(string id, string json)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(json)) return;

            if (UseFiles())
            {
                try
                {
                    string folder = Folder();
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                    File.WriteAllText(PathOf(id), json);
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogWarningFormat("[Sauvegarde] Fichier refuse ({0}) : bascule sur PlayerPrefs.", e.Message);
                    _useFiles = false;
                }
            }

            PlayerPrefs.SetString(PrefGamePrefix + id, json);

            string index = PlayerPrefs.GetString(PrefIndexKey, "");
            if (index.IndexOf(id, StringComparison.Ordinal) < 0)
            {
                index = string.IsNullOrEmpty(index) ? id : (index + ";" + id);
                PlayerPrefs.SetString(PrefIndexKey, index);
            }
            PlayerPrefs.Save();
        }

        public void Delete(string id)
        {
            if (string.IsNullOrEmpty(id)) return;

            if (UseFiles())
            {
                try
                {
                    string path = PathOf(id);
                    if (File.Exists(path)) File.Delete(path);
                }
                catch (Exception e)
                {
                    Debug.LogWarningFormat("[Sauvegarde] Effacement impossible : {0}", e.Message);
                }
                return;
            }

            PlayerPrefs.DeleteKey(PrefGamePrefix + id);

            string index = PlayerPrefs.GetString(PrefIndexKey, "");
            if (!string.IsNullOrEmpty(index))
            {
                string[] ids = index.Split(';');
                System.Text.StringBuilder rebuilt = new System.Text.StringBuilder(128);
                for (int i = 0; i < ids.Length; i++)
                {
                    if (string.IsNullOrEmpty(ids[i]) || ids[i] == id) continue;
                    if (rebuilt.Length > 0) rebuilt.Append(';');
                    rebuilt.Append(ids[i]);
                }
                PlayerPrefs.SetString(PrefIndexKey, rebuilt.ToString());
            }
            PlayerPrefs.Save();
        }

        private static string ReadText(string path)
        {
            try { return File.ReadAllText(path); }
            catch (Exception) { return null; }
        }

        private static string Folder()
        {
            return Path.Combine(Application.persistentDataPath, FolderName);
        }

        private static string PathOf(string id)
        {
            return Path.Combine(Folder(), id + ".json");
        }

        /// <summary>
        /// Le disque est-il utilisable ? On ne le demande qu'une fois. Repondre non
        /// n'est PAS une fatalite : PlayerPrefs prend le relais, et en WebGL c'est un
        /// autre magasin (le site) qui sera pose a la place de celui-ci.
        /// </summary>
        private bool UseFiles()
        {
            if (_checked) return _useFiles;
            _checked = true;

            try
            {
                string folder = Folder();
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                _useFiles = true;
            }
            catch (Exception e)
            {
                Debug.LogWarningFormat("[Sauvegarde] Pas d'acces disque ({0}) : les parties iront dans PlayerPrefs.",
                                       e.Message);
                _useFiles = false;
            }

            return _useFiles;
        }
    }
}
