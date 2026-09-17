using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using EZCameraShake;
using UnityEngine.UI;

namespace MNLTHII
{
    [ExecuteInEditMode]
    public class BoardController : MonoBehaviour
{
    public System.Collections.Generic.List<Hexagon> HexagonsInBoard => hexagonsInBoard;
    public System.Collections.Generic.List<PawnController> PawnsInBoard => pawnsInBoard;
    [Header("Aura Visuals")]
    public Sprite auraHexSprite;

    /// <summary>
    /// Remplace le modele 3D d'un hexagone par celui de son niveau courant.
    /// Le pion eventuellement pose dessus est reparent sur le nouveau modele : sans cela
    /// il etait detruit avec l'ancien GameObject dont il est enfant.
    /// </summary>
    public Hexagon UpgradeHexVisual(Hexagon oldHex)
    {
        if (oldHex == null) return null;

        Hexagon newHexagon = MNLTHII.Factories.HexagonFactory.CreateNewHexagon(oldHex, false, m_HexagonPrefabs, this.transform);

        // Sauvegarde du pion pose sur la case avant de detruire l'ancien modele.
        PawnController pawnOnHex = oldHex.GetComponentInChildren<PawnController>();
        if (pawnOnHex != null) pawnOnHex.transform.parent = newHexagon.transform;

        hexagonsInBoard.Remove(oldHex);
        hexagonsInBoard.Add(newHexagon);

        // Le nouveau modele est un autre GameObject : sa barre de vie doit etre recreee.
        newHexagon.RefreshHealthBar();

        // Clignotement ancien / nouveau modele puis destruction, comme dans FillBoard.
        MNLTHII.Managers.FXManager fx = MNLTHII.Managers.FXManager.Instance;
        if (fx != null) fx.PlayEvolutionFX(newHexagon.gameObject, oldHex.gameObject);
        else Destroy(oldHex.gameObject);

        return newHexagon;
    }

    /// <summary>
    /// Fait apparaitre un pion sur un hexagone. GDD V3 : il n'y a plus de classes,
    /// uniquement des Tanks ("unit") et des ennemis ("enemy"), et toutes les stats
    /// viennent de InteractionRules.ApplyStatsToPawn. Une case occupee refuse le spawn.
    /// </summary>
    public PawnController SpawnUnitVisual(Hexagon parentHex, int level, string pawnTypeString = "unit")
    {
        if (parentHex == null) return null;

        if (getPawnByCoord(parentHex.positionInTheBoard) != null)
        {
            Debug.Log("[Board] Case deja occupee : creation annulee.");
            return null;
        }

        HexagonData dummyData = new HexagonData
        {
            type = pawnTypeString,
            typeID = parentHex.type,
            level = level,
            to = parentHex.positionInTheBoard,
            from = parentHex.positionInTheBoard
        };

        PawnController newPawn = MNLTHII.Factories.PawnFactory.CreateNewPawn(dummyData, m_HexagonPrefabs, this.getHexByCoord);
        if (newPawn != null)
        {
            newPawn.typeOfPawn = (pawnTypeString == "enemy") ? TypeOfPawn.enemy : TypeOfPawn.unit;
            newPawn.level = level;
            MNLTHII.Rules.InteractionRules.ApplyStatsToPawn(newPawn);
            newPawn.RefreshHealthBar();
            pawnsInBoard.Add(newPawn);
        }
        return newPawn;
    }

    /// <summary>Remplace un pion par son modele d'un autre niveau (evolution d'un Tank).</summary>
    public PawnController UpgradePawnVisual(PawnController pawn, int newLevel)
    {
        if (pawn == null) return null;

        Hexagon hex = getHexByCoord(pawn.hexcoord);
        string typeString = pawn.IsEnemy ? "enemy" : "unit";

        pawnsInBoard.Remove(pawn);
        Destroy(pawn.gameObject);
        pawnsInBoard.RemoveAll(item => item == null);

        if (hex == null) return null;
        return SpawnUnitVisual(hex, newLevel, typeString);
    }

    /// <summary>
    /// Remplace definitivement une structure par son modele detruit (prefab "_dstr"),
    /// avec fumee et explosion. Contrairement aux unites, l'epave n'est jamais effacee :
    /// un portail detruit reste visible sur le plateau jusqu'a la fin de la partie.
    /// </summary>
    public Hexagon ReplaceWithDestroyedVisual(Hexagon oldHex)
    {
        if (oldHex == null) return null;

        Hexagon destroyed = MNLTHII.Factories.HexagonFactory.CreateDestroyedHexagon(oldHex, m_HexagonPrefabs, this.transform);

        // Un pion pose sur la case survit a la destruction de la structure.
        PawnController pawnOnHex = oldHex.GetComponentInChildren<PawnController>();
        if (pawnOnHex != null) pawnOnHex.transform.parent = destroyed.transform;

        hexagonsInBoard.Remove(oldHex);
        hexagonsInBoard.Add(destroyed);

        // La case n'est plus un portail : elle ne compte plus pour la condition de victoire.
        destroyed.type = TypeOfHex.Destroyed;
        destroyed.level = 0;
        destroyed.currentHP = 0;
        destroyed.maxHP = 0;
        destroyed.energy = 0;
        destroyed.turnsAlive = 0;

        MNLTHII.Managers.FXManager fx = MNLTHII.Managers.FXManager.Instance;
        if (fx != null)
        {
            fx.PlayExplosionSFX();
            fx.SpawnDestructionFX(destroyed.transform.position);
        }
        if (CameraShaker.Instance != null) CameraShaker.Instance.ShakeOnce(4f, 4f, 0.1f, 0.9f);

        Destroy(oldHex.gameObject);
        return destroyed;
    }

    /// <summary>
    /// Cree les barres de vie des structures. A appeler une fois le plateau construit :
    /// instancier ces canvas pendant FillBoard perturbait la coroutine de construction.
    /// </summary>
    public void RefreshStructureHealthBars()
    {
        if (hexagonsInBoard == null) return;

        for (int i = 0; i < hexagonsInBoard.Count; i++)
        {
            Hexagon hex = hexagonsInBoard[i];
            if (hex == null) continue;

            // Seuls les Portails affichent leur barre en permanence des le depart.
            if (hex.type == TypeOfHex.portal) hex.RefreshHealthBar();
        }
    }

    /// <summary>A appeler apres avoir inflige des degats a un pion : gere sa destruction.</summary>
    public void NotifyPawnDamaged(PawnController pawn)
    {
        if (pawn == null) return;
        if (pawn.currentHP <= 0)
        {
            pawn.energy = 0;
            StartCoroutine(CheckPawnAfterAttack(pawn));
        }
    }

        #region Static Fields
        // Singleton
        
        public static int GetHexDistance(HexCoord a, HexCoord b)
        {
            return Mathf.Max(Mathf.Abs(a.q - b.q), Mathf.Abs(a.r - b.r), Mathf.Abs(a.s - b.s));
        }

        /// <summary>
        /// Un pas vers la cible. Les collines sont des obstacles naturels (GDD 3),
        /// et les Centres de Commandement repoussent uniquement les ennemis.
        /// </summary>
        public HexCoord GetNextStepTowards(HexCoord start, HexCoord end, bool isEnemy = false)
        {
            if (start == null || end == null) return null;
            if (start.CompareHexCoord(end)) return start;

            // Les six candidats sont ecrits dans un tampon membre reutilise : la version
            // precedente allouait un tableau plus six HexCoord a chaque pas de chaque unite.
            for (int i = 0; i < 6; i++)
            {
                HexCoord scratch = _stepScratch[i];
                scratch.q = start.q + NeighbourDQ[i];
                scratch.r = start.r + NeighbourDR[i];
                scratch.s = start.s + NeighbourDS[i];
            }

            HexCoord bestNeighbor = null;
            int minDistance = 2147483647;

            List<PawnController> pawnList = instance.pawnsInBoard;

            for (int n = 0; n < 6; n++)
            {
                HexCoord neighbor = _stepScratch[n];

                bool isBlocked = false;
                for (int i = 0; i < pawnList.Count; i++)
                {
                    PawnController pawn = pawnList[i];
                    if (pawn != null && pawn.hexcoord != null && pawn.hexcoord.CompareHexCoord(neighbor))
                    {
                        isBlocked = true;
                        break;
                    }
                }
                
                if (!isBlocked)
                {
                    Hexagon neighborHex = instance.getHexByCoord(neighbor);

                    // Colline : obstacle naturel des le Niveau 1 du GDD.
                    if (neighborHex == null || neighborHex.type == TypeOfHex.hill)
                    {
                        isBlocked = true;
                    }

                    // Centre de Commandement : repousse les ennemis a 1 case (Niv2) ou 2 cases (Niv3).
                    if (!isBlocked && isEnemy)
                    {
                        List<Hexagon> hexList = instance.hexagonsInBoard;
                        for (int i = 0; i < hexList.Count; i++)
                        {
                            Hexagon hex = hexList[i];
                            if (hex != null && hex.type == TypeOfHex.mountain && hex.level >= 1 && hex.currentHP > 0)
                            {
                                int hexDist = GetHexDistance(hex.positionInTheBoard, neighbor);
                                if (hexDist <= MNLTHII.Rules.InteractionRules.GetMountainRepel(hex.level))
                                {
                                    isBlocked = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                if (isBlocked) continue;

                int dist = GetHexDistance(neighbor, end);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestNeighbor = neighbor;
                }
            }

            // Une seule allocation, pour le vainqueur : le tampon sera reecrit au
            // prochain appel, on ne peut pas en rendre une reference vivante.
            if (bestNeighbor == null) return null;
            return new HexCoord(bestNeighbor.q, bestNeighbor.r, bestNeighbor.s);
        }

        // Tampon des six voisins, alloue une fois pour la duree de la partie.
        private readonly HexCoord[] _stepScratch = new HexCoord[6]
        {
            new HexCoord(0, 0, 0), new HexCoord(0, 0, 0), new HexCoord(0, 0, 0),
            new HexCoord(0, 0, 0), new HexCoord(0, 0, 0), new HexCoord(0, 0, 0)
        };

        private static readonly int[] NeighbourDQ = { 0, 1, 1, 0, -1, -1 };
        private static readonly int[] NeighbourDR = { -1, -1, 0, 1, 1, 0 };
        private static readonly int[] NeighbourDS = { 1, 0, -1, -1, 0, 1 };
    public static BoardController instance;
        #endregion

        #region Private Fields

        // That list keep the data from the server 
        private List<HexagonData> m_boardHexData = new List<HexagonData>();

        // Keep the pawnController to destroy and waiting after foreach loop finish to destroy them
        private List<PawnController> PawnToDestroy = new List<PawnController>();

        // Keep Hexagons to destroy and waiting after foreach loop finish to destroy them
        private List<Hexagon> HexToDestroy = new List<Hexagon>();

        // Keep Hexagons created and waiting after foreach loop finish to add them to the hexagonsInTheBoard list
        private List<Hexagon> HexsToAddAfterInteractions = new List<Hexagon>();

        // List<PawnController> WaitingPawnAttack = new List<PawnController>();

        // Can activate fillBoard function or not
        private bool canFillBoard = true;

        // AudioSource attached to this script
        public string[] jsonData;
        private AudioSource m_audioSource;

        // GameManager instance
        GameManager _instanceGM;

        private bool endOfTurn = false;

        private bool thereAreInteractions = false;
        #endregion

        #region  Serialize Fields
        [Space(4)]
        [Header("-------Hexagon Lists---------")]

        // List of Assets 
        [SerializeField] private GameObject[] m_HexagonPrefabs;

        // Hexagons in the current game board
        [SerializeField] private List<Hexagon> hexagonsInBoard;

        // Pawns in the current game board
        [SerializeField] private List<PawnController> pawnsInBoard;

        [Space(4)]
        [Header("------- Buttons For Testing ---------")]

        [SerializeField] private Button testBoardFromServerButton;
        [SerializeField] private Button findHexButton;
        [SerializeField] private Button clearMap;

        #endregion

        #region JS Methods

        // Appel historique vers ReactJS. N'existe qu'en build WebGL : sur toute autre
        // plateforme (Android/Quest, Windows...) l'import __Internal n'est pas resolu
        // et l'appel fait planter la build.
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void t(string t1);
#endif
        #endregion

        #region Unity Callbacks

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                Destroy(this.gameObject);
            }

        }
        // Start is called before the first frame update
        void Start()
        {

            m_audioSource = GetComponent<AudioSource>();
            _instanceGM = GameManager.instance;


        }

        #endregion

        //React
        public void UpdateBoard(string p_JsonData)
        {
            if (canFillBoard)
            {
                try {
                    m_boardHexData = JsonConvert.DeserializeObject<List<HexagonData>>(p_JsonData);
                    if (m_boardHexData != null) {
                        StartCoroutine(FillBoard(m_boardHexData));
                        Debug.Log("Board updated with JSON: " + p_JsonData);
                    }
                } catch (System.Exception e) {
                    Debug.LogError("Failed to parse board JSON: " + e.Message);
                }
            }
        }

        /// <summary>
        ///  construct the board game by data from server
        /// </summary>
        /// <param name="p_BoardData"></param>
        private IEnumerator FillBoard(List<HexagonData> p_BoardData)
        {
            if (endOfTurn)
                yield return new WaitForSeconds(3.0f);
            // Blocking FillBoard fct while it is working
            canFillBoard = false;

            thereAreInteractions = false;

            // Clean all zones view of hexagon ("Hover_Zone")
            CleanZones();

            // Update state of game and UI/UX
            UpdateStateOfGame(StateOfGame.Interactions);

            // Remove all doublons
            foreach (HexagonData _data in p_BoardData)
            {

                switch (_data.type)
                {

                    case "hex":
                        // Search Hexagon in the Board referring to this _data 
                        Hexagon _hex = getHexByCoord(_data.to);
                        // if _hex exist in the board
                        if (_hex)
                        {

                            // Check if I need to  change gameObject model and update data OR just update data 
                            // If the level is different or the type of Hex different -> model
                            if (_hex.level != _data.level || _hex.type != _data.typeID)
                            {

                                // Do not do this for Portal and Base
                                if (_hex.type != TypeOfHex.portal && _hex.type != TypeOfHex.Base)
                                {

                                    // Save pawn if there is one on this hexagon
                                    PawnController _pawnToKeep = _hex.GetComponentInChildren<PawnController>();
                                    if (_pawnToKeep)
                                        _pawnToKeep.transform.parent = null;

                                    // Remove this hexagon from the list of hexagons in the current game board
                                    hexagonsInBoard.Remove(_hex);



                                    // Create new Hexagon with new asset model  
                                    Hexagon _hexagonWithNewModel = CreateNewHexagon(_data, false);

                                    // Destroy GameObject model
                                    if (_hex.gameObject != null)
                                        // Destroy(_hex.gameObject);
                                        if (VFXManager.instance != null) VFXManager.instance.EvolutionFX(_hexagonWithNewModel.gameObject, _hex.gameObject);

                                    // SFX for Building
                                    if (_data.level > 0)
                                    {
                                        if (MNLTHII.Managers.FXManager.Instance != null) MNLTHII.Managers.FXManager.Instance.PlayBuildingSFX();
                                        yield return new WaitForSeconds(0.1f);
                                    }

                                    // If this hexagon had pawn
                                    if (_pawnToKeep)
                                        _pawnToKeep.transform.parent = _hexagonWithNewModel.transform;

                                    hexagonsInBoard.Add(_hexagonWithNewModel);
                                }
                                // Need to do pawn controller for Bunker???? 

                            }
                            else
                            {
                                // Update data of Hexagon
                                _hex.UpdateData(_data);
                            }
                        }
                        else
                        {
                            switch (_data.typeID)
                            {
                                case TypeOfHex.portal:
                                case TypeOfHex.Base:

                                    // if energy = 0 and the portal/base not attacked in this turn
                                    if (_data.energy <= 0 && _data.attack_type == null)
                                    {
                                        // Create Destroyed asset model for Portal or Base 
                                        Hexagon _newDestroyedPortalOrBase = CreateDestroyedHexagon(_data);

                                        // Only for Portal or Base need to save in the hexagonList 
                                        hexagonsInBoard.Add(_newDestroyedPortalOrBase);

                                    }
                                    else
                                    {
                                        // Create new Hexagon 
                                        Hexagon _newPortalOrBase = CreateNewHexagon(_data, false);

                                        // Save new hexagon in the hexagons list of current game board 
                                        hexagonsInBoard.Add(_newPortalOrBase);
                                    }
                                    break;
                                default:
                                    // Create new Hexagon 
                                    Hexagon _newHexagon = CreateNewHexagon(_data, false);

                                    // Save new hexagon in the hexagons list of current game board 
                                    hexagonsInBoard.Add(_newHexagon);
                                    break;

                            }

                        }
                        break;
                    case "enemy":
                    case "unit":

                        // Search Pawn in the Board referring to this _data 
                        PawnController _pawn = getPawnByCoord(_data.from);

                        // if _pawn exist in the board
                        if (_pawn)
                        {
                            // if  level is different change asset model
                            if (_pawn.level != _data.level)
                            {
                                // Destroy Game Object
                                if (_pawn.gameObject != null)
                                    Destroy(_pawn.gameObject);

                                // Remove ancient pawnController data
                                pawnsInBoard.Remove(_pawn);

                                // Create new pawnController data 
                                PawnController _newPawn = CreateNewPawn(_data);

                                // Add pawnController in the list of pawn in the current game 
                                pawnsInBoard.Add(_newPawn);


                            }
                            else
                            {
                                // Update data
                                _pawn.UpdateData(_data);
                            }
                        }
                        else
                        {
                            yield return new WaitForSeconds(0.02f);
                            // Create new pawn data
                            PawnController _newPawn = CreateNewPawn(_data);
                            if (VFXManager.instance != null) VFXManager.instance.EvolutionFX(_newPawn.gameObject, null);
                            // Add _newPawn in the list of pawn in the current game 
                            pawnsInBoard.Add(_newPawn);
                        }
                        break;
                }

            }

            // After all spawning it's time to move and attack
            StartCoroutine(DoInteractions());
        }

        private IEnumerator DoInteractions()
        {
            // Update  Patterns data and view
            UpdatePatterns();


            yield return new WaitForSeconds(0.1f);

            foreach (Hexagon p_bunker in hexagonsInBoard)
            {

                if (p_bunker.type == TypeOfHex.hill && p_bunker.level > 0)
                {
                    yield return new WaitForSeconds(0.1f);
                    DoAttackByHexagon(p_bunker.attackCoord, p_bunker);
                    thereAreInteractions = true;
                }
            }

            yield return new WaitForSeconds(0.5f);

            foreach (PawnController p_pawn in pawnsInBoard)
            {

                p_pawn.ApplySequenceAnimation(PawnController.TypeOfPawnInteractions.target);
                thereAreInteractions = true;
            }

            canFillBoard = true;
            RefreshAllAuras();

            // If There are interactions wait
            if (thereAreInteractions)
            {
                yield return new WaitForSeconds(6.0f);
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            t("start");
#endif

            // Update Destroyed and created hexagons and pawns in hexagonsInTheBoard and pawnsInTheBoard lists
            UpdateLists();

            // Update StateOfGame variable in the GameManager and StateOfGame view  
            UpdateStateOfGame(StateOfGame.inGame);

            // New Pattern in The Board 
            RefreshViewPatterns();

            // New Zones from prompt info send by the server to Game Manager
            UpdateAndRefreshViewZones();

            endOfTurn = true;
        }

        public void DoAttackByPawn(HexCoord p_hexCoord, PawnController p_pawnAttacker)
        {
            Hexagon _hexagonAttacked = getHexByCoord(p_hexCoord);
            PawnController _pawnAttacked = getPawnByCoord(p_hexCoord);


            if (_hexagonAttacked != null && _hexagonAttacked.level > 0)
            {
                // SFX 
                if (MNLTHII.Managers.FXManager.Instance != null) MNLTHII.Managers.FXManager.Instance.PlayAttackSFX();

                // Attack VFX
                if (MNLTHII.Managers.FXManager.Instance != null) MNLTHII.Managers.FXManager.Instance.SpawnAttackFX((_hexagonAttacked.transform.position - p_pawnAttacker.transform.parent.position) / 2);

                // Animations
                p_pawnAttacker.ApplySequenceAnimation(PawnController.TypeOfPawnInteractions.attack);

                // Check energy
                StartCoroutine(CheckHexagonAfterAttack(_hexagonAttacked));
                StartCoroutine(CheckPawnAfterAttack(p_pawnAttacker));
            }
            else if (_pawnAttacked != null)
            {
                //SFX
                if (MNLTHII.Managers.FXManager.Instance != null) MNLTHII.Managers.FXManager.Instance.PlayAttackSFX();

                // Animations
                p_pawnAttacker.ApplySequenceAnimation(PawnController.TypeOfPawnInteractions.attack);
                _pawnAttacked.ApplySequenceAnimation(PawnController.TypeOfPawnInteractions.attack);

                // Check energy
                StartCoroutine(CheckPawnAfterAttack(_pawnAttacked));
                StartCoroutine(CheckPawnAfterAttack(p_pawnAttacker));

                // Attack VFX
                if (MNLTHII.Managers.FXManager.Instance != null) MNLTHII.Managers.FXManager.Instance.SpawnAttackFX((_pawnAttacked.transform.parent.position - p_pawnAttacker.transform.parent.position) / 2);

            }
            //if(p_pawnAttacker != null  )
            // p_pawnAttacker.ApplySequenceAnimation(PawnController.TypeOfPawnInteractions.nextTo);
        }
        public void DoAttackByHexagon(HexCoord p_hexCoord, Hexagon p_hexagonAttacker)
        {
            Hexagon _hexagonAttacked = getHexByCoord(p_hexCoord);
            PawnController _pawnAttacked = getPawnByCoord(p_hexCoord);
            if (_hexagonAttacked != null && _hexagonAttacked.level > 0)
            {
                // SFX
                if (MNLTHII.Managers.FXManager.Instance != null) MNLTHII.Managers.FXManager.Instance.PlayAttackSFX();

                StartCoroutine(CheckHexagonAfterAttack(_hexagonAttacked));
                StartCoroutine(CheckHexagonAfterAttack(p_hexagonAttacker));
                // Attack VFX
                if (MNLTHII.Managers.FXManager.Instance != null) MNLTHII.Managers.FXManager.Instance.SpawnAttackFX((_hexagonAttacked.transform.position - p_hexagonAttacker.transform.position) / 2);
            }
            else if (_pawnAttacked != null)
            {

                // SFX
                if (MNLTHII.Managers.FXManager.Instance != null) MNLTHII.Managers.FXManager.Instance.PlayAttackSFX();

                // Animations
                StartCoroutine(CheckHexagonAfterAttack(p_hexagonAttacker));
                StartCoroutine(CheckPawnAfterAttack(_pawnAttacked));

                // Attack VFX
                if (MNLTHII.Managers.FXManager.Instance != null) MNLTHII.Managers.FXManager.Instance.SpawnAttackFX((_pawnAttacked.transform.parent.position - p_hexagonAttacker.transform.position) / 2);
            }
        }



        private Hexagon CreateNewHexagon(HexagonData p_hexData, bool p_afterDestroyed)
        {
            GameObject _hexagonPrefab;
            // prefab Asset
            int[] _hexPrefabsPath = PrefabsPath.GetHexagonPrefabs(p_hexData.typeID);
            // Instantiate Hexagon Prefab by Level 
            if (p_afterDestroyed)
            {
                _hexagonPrefab = Instantiate(m_HexagonPrefabs[_hexPrefabsPath[0]], new Vector3((Mathf.Sqrt(3) * p_hexData.to.q + Mathf.Sqrt(3) / 2 * p_hexData.to.r) * 0.55f, 0.0f, -((3.0f / 2 * p_hexData.to.r) * 0.55f)), Quaternion.Euler(0, 90, 0));

            }
            else
            {
                _hexagonPrefab = Instantiate(m_HexagonPrefabs[_hexPrefabsPath[p_hexData.level]], new Vector3((Mathf.Sqrt(3) * p_hexData.to.q + Mathf.Sqrt(3) / 2 * p_hexData.to.r) * 0.55f, 0.0f, -((3.0f / 2 * p_hexData.to.r) * 0.55f)), Quaternion.Euler(0, 90, 0));
            }


            Hexagon _newHexagon = _hexagonPrefab.AddComponent<Hexagon>();
            _newHexagon.Init(p_hexData, _hexagonPrefab);
            _newHexagon.transform.parent = transform;
            _newHexagon.gameObject.AddComponent<AudioSource>();
            return _newHexagon;
        }
        private Hexagon CreateNewHexagon(Hexagon p_hex, bool p_afterDestroyed)
        {
            GameObject _hexagonPrefab;
            // prefab Asset

            int[] _hexPrefabsPath = PrefabsPath.GetHexagonPrefabs(p_hex.type);
            // Instantiate Hexagon Prefab by Level 

            if (p_afterDestroyed)
            {
                _hexagonPrefab = Instantiate(m_HexagonPrefabs[_hexPrefabsPath[0]], new Vector3((Mathf.Sqrt(3) * p_hex.positionInTheBoard.q + Mathf.Sqrt(3) / 2 * p_hex.positionInTheBoard.r) * 0.55f, 0.0f, -((3.0f / 2 * p_hex.positionInTheBoard.r) * 0.55f)), Quaternion.Euler(0, 90, 0));

            }
            else
            {
                _hexagonPrefab = Instantiate(m_HexagonPrefabs[_hexPrefabsPath[p_hex.level]], new Vector3((Mathf.Sqrt(3) * p_hex.positionInTheBoard.q + Mathf.Sqrt(3) / 2 * p_hex.positionInTheBoard.r) * 0.55f, 0.0f, -((3.0f / 2 * p_hex.positionInTheBoard.r) * 0.55f)), Quaternion.Euler(0, 90, 0));
            }


            Hexagon _newHexagon = _hexagonPrefab.AddComponent<Hexagon>();
            if (p_afterDestroyed)
            {
                _newHexagon.level = 0;
            }
            else
            {
                _newHexagon.level = p_hex.level;
            }
            _newHexagon.type = p_hex.type;
            _newHexagon.positionInTheBoard = p_hex.positionInTheBoard;
            _newHexagon.commandPoints = p_hex.commandPoints;
            _newHexagon.energy = p_hex.energy;
            _newHexagon.attackCoord = p_hex.attackCoord;
#if UNITY_EDITOR
            _newHexagon.BoardCoordforEditor.x = p_hex.positionInTheBoard.q;
            _newHexagon.BoardCoordforEditor.y = p_hex.positionInTheBoard.r;
            _newHexagon.BoardCoordforEditor.z = p_hex.positionInTheBoard.s;
#endif
            // _newHexagon.attackCoord= p_hex.attackCoord;
            // _newHexagon.Init(p_hex, _hexagonPrefab);
            _newHexagon.transform.parent = transform;

            return _newHexagon;
        }
        private Hexagon CreateDestroyedHexagon(HexagonData p_hexData)
        {

            // prefab Asset
            int[] _hexDestroyedPrefabsPath = PrefabsPath.GetDestroyedHexagonPrefabs(p_hexData.typeID);
            // Instantiate Hexagon Prefab by Level 

            GameObject _hexagonPrefab = Instantiate(m_HexagonPrefabs[_hexDestroyedPrefabsPath[p_hexData.level]], new Vector3((Mathf.Sqrt(3) * p_hexData.to.q + Mathf.Sqrt(3) / 2 * p_hexData.to.r) * 0.55f, 0.0f, -((3.0f / 2 * p_hexData.to.r) * 0.55f)), Quaternion.Euler(0, 90, 0));

            Hexagon _newHexagon = _hexagonPrefab.AddComponent<Hexagon>();
            _newHexagon.Init(p_hexData, _hexagonPrefab);
            _newHexagon.transform.parent = transform;
            _newHexagon.energy = 0;

            return _newHexagon;
        }
        private Hexagon CreateDestroyedHexagon(Hexagon p_hex)
        {

            // prefab Asset
            int[] _hexDestroyedPrefabsPath = PrefabsPath.GetDestroyedHexagonPrefabs(p_hex.type);
            // Instantiate Hexagon Prefab by Level 

            GameObject _hexagonPrefab = Instantiate(m_HexagonPrefabs[_hexDestroyedPrefabsPath[p_hex.level]], new Vector3((Mathf.Sqrt(3) * p_hex.positionInTheBoard.q + Mathf.Sqrt(3) / 2 * p_hex.positionInTheBoard.r) * 0.55f, 0.0f, -((3.0f / 2 * p_hex.positionInTheBoard.r) * 0.55f)), Quaternion.Euler(0, 90, 0));
            //Fx Explosion   
            if (MNLTHII.Managers.FXManager.Instance != null) MNLTHII.Managers.FXManager.Instance.SpawnDestructionFX(_hexagonPrefab.transform.position);
            if (MNLTHII.Managers.FXManager.Instance != null) MNLTHII.Managers.FXManager.Instance.PlayExplosionSFX();
            Hexagon _newHexagon = _hexagonPrefab.AddComponent<Hexagon>();
            
            _newHexagon.level = p_hex.level;
            _newHexagon.type = p_hex.type;
            _newHexagon.positionInTheBoard = p_hex.positionInTheBoard;
            _newHexagon.commandPoints = p_hex.commandPoints;
            _newHexagon.energy = p_hex.energy;
            _newHexagon.transform.parent = transform;
            _newHexagon.energy = 0;

#if UNITY_EDITOR
            _newHexagon.BoardCoordforEditor.x = p_hex.positionInTheBoard.q;
            _newHexagon.BoardCoordforEditor.y = p_hex.positionInTheBoard.r;
            _newHexagon.BoardCoordforEditor.z = p_hex.positionInTheBoard.s;
#endif


            return _newHexagon;
        }

        /// <summary>
        /// Create new PawnController , init it and create prefab by level from data send by server
        /// </summary>
        /// <param name="p_hexData"></param>
        /// <returns></returns>
        private PawnController CreateNewPawn(HexagonData p_hexData)
        {

            TypeOfPawn _pawnType;
            Enum.TryParse(p_hexData.type, out _pawnType);
            int[] _pawnPrefabsPath = PrefabsPath.GetPawnPrefabs(_pawnType);
            GameObject _pawnPrefab;
            if (p_hexData.type == "enemy")
            {
                _pawnPrefab = Instantiate(m_HexagonPrefabs[_pawnPrefabsPath[p_hexData.level - 1]], Vector3.zero, Quaternion.identity);

                if (MNLTHII.Managers.FXManager.Instance != null) MNLTHII.Managers.FXManager.Instance.PlayEnemySpawnerSFX(null);


            }
            else
            {
                _pawnPrefab = Instantiate(m_HexagonPrefabs[_pawnPrefabsPath[p_hexData.level - 1]], Vector3.zero, Quaternion.identity);
                if (MNLTHII.Managers.FXManager.Instance != null) MNLTHII.Managers.FXManager.Instance.PlayUnitSpawnerSFX(null);
            }



            Hexagon _hex = getHexByCoord(p_hexData.from);

            if (_hex)
            {

                _pawnPrefab.transform.position = _hex.transform.position;
                _pawnPrefab.transform.parent = _hex.transform;
                if (p_hexData.type == "enemy")
                {
                    Vector3 relativePos = Vector3.zero - _pawnPrefab.transform.position;
                    _pawnPrefab.transform.rotation = Quaternion.LookRotation(relativePos);
                }

            }
            else
            {
                _pawnPrefab.transform.position = new Vector3((Mathf.Sqrt(3) * p_hexData.to.q + Mathf.Sqrt(3) / 2 * p_hexData.to.r) * 0.55f, 0.0f, -((3.0f / 2 * p_hexData.to.r) * 0.55f));
                if (p_hexData.type == "enemy")
                {
                    Vector3 relativePos = Vector3.zero - _pawnPrefab.transform.position;
                    _pawnPrefab.transform.rotation = Quaternion.LookRotation(relativePos);
                }
            }


            PawnController _pawnController = _pawnPrefab.GetComponent<PawnController>();

            Hexagon _hexTo = getHexByCoord(p_hexData.to);

            _pawnController.typeOfPawn = _pawnType;
            _pawnController.Init(p_hexData);
            if (_hexTo)
                _pawnController.target = _hexTo.transform;
            if (p_hexData.attack_type != null)
                _pawnController.attacked = true;
            else
                _pawnController.attacked = false;
            switch (_pawnController.typeOfPawn)
            {
                case TypeOfPawn.unit:
                    if (MNLTHII.Managers.FXManager.Instance != null) MNLTHII.Managers.FXManager.Instance.SpawnCommunityFX(_pawnController.transform.position);
                    break;
                case TypeOfPawn.enemy:
                    if (MNLTHII.Managers.FXManager.Instance != null) MNLTHII.Managers.FXManager.Instance.SpawnEnemyFX(_pawnController.transform.position);
                    break;
            }

            return _pawnController;
        }


        /// <summary>
        /// Create new pawnController, init it and create prefab by level from another PawnController 
        /// </summary>
        /// <param name="p_pawn"></param>
        /// <returns></returns>
        private PawnController CreateNewPawn(PawnController p_pawn)
        {
            TypeOfPawn _pawnType;
            Enum.TryParse(p_pawn.typeOfPawn.ToString(), out _pawnType);
            int[] _pawnPrefabsPath = PrefabsPath.GetPawnPrefabs(_pawnType);

            GameObject _pawnPrefab = Instantiate(m_HexagonPrefabs[_pawnPrefabsPath[p_pawn.level - 1]], Vector3.zero, Quaternion.identity);




            Hexagon _hex = getHexByCoord(p_pawn.hexcoord);

            _pawnPrefab.transform.position = _hex.transform.position;
            _pawnPrefab.transform.parent = _hex.transform;



            PawnController _pawnController = _pawnPrefab.GetComponent<PawnController>();



            _pawnController.typeOfPawn = _pawnType;


            return _pawnController;
        }

        /// <summary>
        /// Recherche a plat. List.Find(item => ...) capturait hexCoord dans une closure,
        /// donc une allocation a chaque appel, et cette methode est appelee des dizaines
        /// de fois par tour (pathfinding, regles, factories).
        /// </summary>
        public Hexagon getHexByCoord(HexCoord hexCoord)
        {
            if (hexCoord == null || hexagonsInBoard == null) return null;

            int q = hexCoord.q, r = hexCoord.r, s = hexCoord.s;

            for (int i = 0; i < hexagonsInBoard.Count; i++)
            {
                Hexagon hex = hexagonsInBoard[i];
                if (hex == null) continue;

                HexCoord c = hex.positionInTheBoard;
                if (c != null && c.q == q && c.r == r && c.s == s) return hex;
            }
            return null;
        }

        public PawnController getPawnByCoord(HexCoord pawnCoord)
        {
            if (pawnCoord == null || pawnsInBoard == null) return null;

            int q = pawnCoord.q, r = pawnCoord.r, s = pawnCoord.s;

            for (int i = 0; i < pawnsInBoard.Count; i++)
            {
                PawnController pawn = pawnsInBoard[i];
                if (pawn == null) continue;

                HexCoord c = pawn.hexcoord;
                if (c != null && c.q == q && c.r == r && c.s == s) return pawn;
            }
            return null;
        }

        /// <summary>Cle entiere compacte pour un couple (q, r). Valide pour |q| et |r| < 256.</summary>
        public static int HexKey(int q, int r)
        {
            return ((q + 256) << 9) | (r + 256);
        }


        //public bool CheckDetroyed(Hexagon _hex)
        //{




        //}



        // Index spatial reutilise par RefreshAllAuras. Il est reconstruit au debut de
        // chaque appel, donc jamais perime, et evite le balayage complet du plateau
        // pour chaque batiment.
        private readonly Dictionary<int, Hexagon> _auraIndex = new Dictionary<int, Hexagon>(256);

        [Header("Marqueurs de zone")]
        [Tooltip("Anneaux colores autour des batiments. Eteints : ils encombraient le plateau.")]
        public bool showAuras = false;

        private static readonly Color AuraMountain = new Color(1f, 0.3f, 0f);
        private static readonly Color AuraHill = Color.yellow;
        private static readonly Color AuraCrystal = Color.cyan;

        // Marqueur genere une seule fois si auraHexSprite n'est pas renseigne.
        private static Sprite _proceduralAuraSprite;

        /// <summary>
        /// Sprite du marqueur de zone. Renvoie celui de l'inspecteur s'il existe,
        /// sinon un anneau genere en memoire : sans cela, aucune zone n'etait visible
        /// tant que le champ auraHexSprite restait vide.
        /// </summary>
        public Sprite GetAuraSprite()
        {
            if (auraHexSprite != null) return auraHexSprite;
            if (_proceduralAuraSprite == null) _proceduralAuraSprite = CreateRingSprite(128);
            return _proceduralAuraSprite;
        }

        /// <summary>
        /// Anneau blanc a bord adouci, interieur legerement rempli. Un disque plutot
        /// qu'un hexagone : le marqueur reste correct quelle que soit l'orientation
        /// du prefab de la case.
        /// </summary>
        private static Sprite CreateRingSprite(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            Color32[] pixels = new Color32[size * size];
            float half = size * 0.5f;

            const float innerEdge = 0.74f;
            const float outerEdge = 0.97f;
            const float feather = 0.05f;
            const float fillAlpha = 0.28f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = (x + 0.5f - half) / half;
                    float py = (y + 0.5f - half) / half;
                    float d = Mathf.Sqrt(px * px + py * py);

                    float a;
                    if (d > outerEdge + feather) a = 0f;
                    else if (d > outerEdge) a = Mathf.InverseLerp(outerEdge + feather, outerEdge, d);
                    else if (d > innerEdge) a = 1f;
                    else a = fillAlpha;

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        /// <summary>
        /// Peint les zones d'influence. L'ancienne version etait en O(n^2) : pour chacun
        /// des 169 hexagones elle reparcourait les 169 autres, soit ~28 500 tests de
        /// distance par appel. On indexe une fois le plateau puis on ne visite que les
        /// cases reellement comprises dans le rayon (19 au maximum pour un rayon de 2).
        /// </summary>
        public void RefreshAllAuras()
        {
            if (hexagonsInBoard == null) return;

            _auraIndex.Clear();

            // Marqueurs de zone ETEINTS par defaut.
            //
            // Ils dessinaient un anneau colore autour de chaque batiment. Sur une carte
            // ou presque chaque case est batie, ca faisait un tapis de cercles ou plus
            // rien ne se lisait - et la moitie d'entre eux ne voulait plus rien dire :
            // le Gaz est devenu une usine, il n'a plus aucun rayon d'effet.
            //
            // Le champ reste la : remets showAuras a vrai dans l'inspecteur si tu veux
            // les revoir. On efface d'abord, pour que basculer le reglage en cours de
            // partie fasse bien disparaitre ce qui etait deja peint.
            if (!showAuras)
            {
                for (int i = 0; i < hexagonsInBoard.Count; i++)
                    if (hexagonsInBoard[i] != null) hexagonsInBoard[i].ClearAura();

                return;
            }

            for (int i = 0; i < hexagonsInBoard.Count; i++)
            {
                Hexagon hex = hexagonsInBoard[i];
                if (hex == null) continue;

                hex.ClearAura();

                HexCoord c = hex.positionInTheBoard;
                if (c != null) _auraIndex[HexKey(c.q, c.r)] = hex;
            }

            for (int i = 0; i < hexagonsInBoard.Count; i++)
            {
                Hexagon hex = hexagonsInBoard[i];
                if (hex == null || hex.level < 1) continue;

                int radius;
                Color auraColor;

                switch (hex.type)
                {
                    case TypeOfHex.mountain: auraColor = AuraMountain; radius = (hex.level >= 2) ? 2 : 1; break;
                    case TypeOfHex.hill: auraColor = AuraHill; radius = 2; break;
                    // Plus d'anneau pour le Gaz : une usine n'a pas de rayon d'effet,
                    // elle produit sur place. Le dessiner etait un mensonge visuel.
                    case TypeOfHex.gas: continue;
                    case TypeOfHex.crystal: auraColor = AuraCrystal; radius = (hex.level >= 2) ? 2 : 1; break;
                    default: continue;
                }

                if (radius <= 0) continue;

                HexCoord center = hex.positionInTheBoard;
                if (center == null) continue;

                PaintAura(center.q, center.r, radius, auraColor);
            }
        }

        /// <summary>Parcours du disque hexagonal centre sur (q, r), le centre exclu.</summary>
        private void PaintAura(int q, int r, int radius, Color color)
        {
            for (int dq = -radius; dq <= radius; dq++)
            {
                int minDr = Mathf.Max(-radius, -dq - radius);
                int maxDr = Mathf.Min(radius, -dq + radius);

                for (int dr = minDr; dr <= maxDr; dr++)
                {
                    if (dq == 0 && dr == 0) continue;

                    Hexagon neighbour;
                    if (_auraIndex.TryGetValue(HexKey(q + dq, r + dr), out neighbour) && neighbour != null)
                        neighbour.AddAura(color);
                }
            }
        }

        public void ClearMap()
        {
            foreach (Hexagon _hex in hexagonsInBoard)
            {
                if (_hex.gameObject != null)
                    Destroy(_hex.gameObject);
            }
            hexagonsInBoard.Clear();
            foreach (PawnController _pawn in pawnsInBoard)
            {
                if (_pawn.gameObject != null)
                    Destroy(_pawn.gameObject);
            }
            pawnsInBoard.Clear();
        }


        //public IEnumerator DestroyPawn(PawnController _pawn, float time)
        //{

        //}
        //public IEnumerator DestroyHex(Hexagon _hex, float time)
        //{

        //}

        //public IEnumerator Spawn(int[] prefabs, Hexagon _hexData, float time, bool destroy)
        //{




        //}




        private void CleanPawn()
        {
            List<PawnController> _pawnToRemove = new List<PawnController>();

            foreach (PawnController _pawn in pawnsInBoard)
            {

                if (_pawn != null && _pawn.energy <= 0 && _pawn.typeOfPawn != TypeOfPawn.Bunker)
                {
                    if (_pawn)
                        Debug.Log("Clean " + _pawn.typeOfPawn + " : q" + _pawn.hexcoord.q + " , r: " + _pawn.hexcoord.r + " , s: " + _pawn.hexcoord.s);
                    if (_pawn)
                        if (MNLTHII.Managers.FXManager.Instance != null) MNLTHII.Managers.FXManager.Instance.SpawnDestructionFX(_pawn.transform.position);
                    if (_pawn)
                        Destroy(_pawn.gameObject);
                    if (_pawn)
                        _pawnToRemove.Add(_pawn);
                    // pawnsInBoard.Remove(_pawn);
                }

            }
            pawnsInBoard.RemoveAll(item => item == null);

            foreach (PawnController _pawn in _pawnToRemove)
            {

                pawnsInBoard.Remove(_pawn);
            }
            _pawnToRemove.Clear();
        }


        private void CleanHexagon()
        {
            List<Hexagon> _hexagonToRemove = new List<Hexagon>();
            hexagonsInBoard.RemoveAll(item => item == null);
            foreach (Hexagon _hex in hexagonsInBoard)
            {

                if (_hex != null && _hex.energy <= 0 && _hex.level > 0)
                {
                    Debug.Log("Clean " + _hex.type + " : q" + _hex.positionInTheBoard.q + " , r: " + _hex.positionInTheBoard.r + " , s: " + _hex.positionInTheBoard.s);




                    PawnController _pawnToKeep = _hex.GetComponentInChildren<PawnController>();
                    if (_pawnToKeep)
                        _pawnToKeep.transform.parent = null;
                    // Destroy(_attackerHex.gameObject);
                    Hexagon hexagonDestroyedAttached = CreateDestroyedHexagon(_hex);

                    if (hexagonDestroyedAttached.type != TypeOfHex.Base && hexagonDestroyedAttached.type != TypeOfHex.portal)
                    {
                        if (MNLTHII.Managers.FXManager.Instance != null) MNLTHII.Managers.FXManager.Instance.SpawnDestructionFX(_hex.transform.position);
                        if (MNLTHII.Managers.FXManager.Instance != null) MNLTHII.Managers.FXManager.Instance.PlayExplosionSFX();
                        Destroy(hexagonDestroyedAttached.gameObject, 4.0f);
                        Hexagon _newHexagon = CreateNewHexagon(_hex, true);
                        hexagonsInBoard.Add(_newHexagon);
                        if (_pawnToKeep)
                            _pawnToKeep.transform.parent = _newHexagon.transform;
                    }
                    else
                    {
                        if (_pawnToKeep)
                            _pawnToKeep.transform.parent = hexagonDestroyedAttached.transform;
                        Debug.Log("Keep The destroy Base or Portal");
                        hexagonsInBoard.Add(hexagonDestroyedAttached);
                    }
                    _hexagonToRemove.Add(_hex);
                    if (_hex.gameObject)
                        Destroy(_hex.gameObject);
                }
                // pawnsInBoard.Remove(_pawn);

            }


            foreach (Hexagon _hex in _hexagonToRemove)
            {

                hexagonsInBoard.Remove(_hex);
            }
            _hexagonToRemove.Clear();
        }

        /// <summary>
        /// Erase and Respawn
        /// </summary>
        public void eraseAndRespawn()
        {

            foreach (PawnController _pawnToErase in pawnsInBoard)
            {
                if (_pawnToErase.gameObject != null && _pawnToErase.typeOfPawn != TypeOfPawn.Bunker)
                    Destroy(_pawnToErase.gameObject);
                if (_pawnToErase != null && _pawnToErase.typeOfPawn != TypeOfPawn.Bunker)
                    Destroy(_pawnToErase);
            }
            pawnsInBoard.RemoveAll(item => item.typeOfPawn != TypeOfPawn.Bunker);


            foreach (HexagonData _boardCaseData in m_boardHexData)
            {
                switch (_boardCaseData.type)
                {

                    case "unit":
                    case "enemy":
                        if (_boardCaseData.energy > 0)
                        {
                            PawnController _pawn = getPawnByCoord(_boardCaseData.from);

                            // Pawn already in the board
                            if (_pawn)
                            {
                                if (_pawn.level != _boardCaseData.level)
                                {
                                    // Destroy Game Object
                                    if (_pawn.gameObject != null)
                                        Destroy(_pawn.gameObject);
                                    //
                                    pawnsInBoard.Remove(_pawn);
                                    PawnController _newPawn = CreateNewPawn(_boardCaseData);
                                    pawnsInBoard.Add(_newPawn);

                                }
                                else
                                {
                                    _pawn.UpdateData(_boardCaseData);
                                }
                            }
                            else
                            {

                                PawnController _newPawn = CreateNewPawnAfterErase(_boardCaseData);
                                pawnsInBoard.Add(_newPawn);
                            }
                        }
                        break;

                }

            }
            pawnsInBoard.RemoveAll(item => item == null);
        }





        #region Private Functions

        private IEnumerator CheckHexagonAfterAttack(Hexagon p_hexagon)
        {
            if (p_hexagon.energy <= 0 && (p_hexagon.level > 0 || p_hexagon.type == TypeOfHex.hill || p_hexagon.type == TypeOfHex.Base))
            {
                yield return new WaitForSeconds(0.5f);
                PawnController _pawnToKeep = p_hexagon.GetComponentInChildren<PawnController>();
                if (_pawnToKeep) _pawnToKeep.transform.parent = null;

                if (CameraShaker.Instance != null) CameraShaker.Instance.ShakeOnce(3f, 3f, 0.1f, 0.8f);
                AudioSource _audioSource = GetComponent<AudioSource>();
                if (MNLTHII.Managers.FXManager.Instance != null) MNLTHII.Managers.FXManager.Instance.PlayExplosionSFX(); {
                }

                Hexagon hexagonDestroyedAttached = MNLTHII.Factories.HexagonFactory.CreateDestroyedHexagon(p_hexagon, m_HexagonPrefabs, transform);

                if (hexagonDestroyedAttached.type != TypeOfHex.Base && hexagonDestroyedAttached.type != TypeOfHex.portal)
                {
                    Destroy(hexagonDestroyedAttached.gameObject, 4.0f);
                    Hexagon _newHexagon = MNLTHII.Factories.HexagonFactory.CreateNewHexagon(hexagonDestroyedAttached, true, m_HexagonPrefabs, this.transform);
                    
                    hexagonsInBoard.Remove(p_hexagon);
                    hexagonsInBoard.Add(_newHexagon);

                    if (_pawnToKeep) _pawnToKeep.transform.parent = _newHexagon.transform;
                }
                else
                {
                    if (_pawnToKeep) _pawnToKeep.transform.parent = hexagonDestroyedAttached.transform;
                    hexagonsInBoard.Remove(p_hexagon);
                    hexagonsInBoard.Add(hexagonDestroyedAttached);
                }
                
                Destroy(p_hexagon.gameObject);
                hexagonsInBoard.RemoveAll(item => item == null);
            }
        }

        public IEnumerator CheckPawnAfterAttack(PawnController p_pawn)
        {
            if (p_pawn == null) yield break;
            if (p_pawn.energy <= 0)
            {
                yield return new WaitForSeconds(0.5f);
                if (p_pawn == null) yield break;
                int[] _pawnPrefabsPath = PrefabsPath.GetDestroyedtPawnPrefabs(p_pawn.typeOfPawn);
                
                int levelIndex = p_pawn.level - 1;
                if (levelIndex < 0) levelIndex = 0;
                
                if (_pawnPrefabsPath != null && levelIndex < _pawnPrefabsPath.Length)
                {
                    GameObject _pawnPrefab = Instantiate(m_HexagonPrefabs[_pawnPrefabsPath[levelIndex]], p_pawn.transform.position, Quaternion.Euler(0, 90, 0));
                    
                    if (CameraShaker.Instance != null) CameraShaker.Instance.ShakeOnce(3f, 3f, 0.1f, 0.8f);
                    if (MNLTHII.Managers.FXManager.Instance != null) MNLTHII.Managers.FXManager.Instance.SpawnDestructionFX(_pawnPrefab.transform.position);

                    AudioSource _audioSource = GetComponent<AudioSource>();
                    if (MNLTHII.Managers.FXManager.Instance != null) MNLTHII.Managers.FXManager.Instance.PlayExplosionSFX(); {
                    }

                    Destroy(_pawnPrefab, 4.0f);
                }
                
                pawnsInBoard.Remove(p_pawn);
                Destroy(p_pawn.gameObject);
                pawnsInBoard.RemoveAll(item => item == null);
            }
        }
        #endregion

        #region Tools
        /// <summary>
        /// Update HexagonInBoard and PawnsInBoard
        /// </summary>
        private void UpdateLists()
        {
            // Clean PawnInBoard
            foreach (PawnController _pawmTodestroy in PawnToDestroy)
            {
                pawnsInBoard.Remove(_pawmTodestroy);
                Destroy(_pawmTodestroy);
            }
            pawnsInBoard.RemoveAll(item => item == null);

            // Clean hexagonsInBoard
            foreach (Hexagon _hexToDestroy in HexToDestroy)
            {
                hexagonsInBoard.Remove(_hexToDestroy);
                Destroy(_hexToDestroy);
            }
            hexagonsInBoard.RemoveAll(item => item == null);

            // Update hexagonsInBoard
            foreach (Hexagon _hexToAdd in HexsToAddAfterInteractions)
            {
                hexagonsInBoard.Add(_hexToAdd);
            }


        }



        /// <summary>
        /// SetActive(false) for all "Hover_Zone" of Hexagon gameObject 
        /// </summary>
        private void CleanZones()
        {
            foreach (Hexagon _hex in hexagonsInBoard)
            {
                Debug.Log("Clean map for zones");

                Transform _zoneFX;
                if (_hex)
                {
                    _zoneFX = _hex.gameObject.transform.Find("Hover_Zone");
                    if (_zoneFX)
                        _zoneFX.gameObject.SetActive(false);
                }
            }

        }

        /// <summary>
        /// Update from server new zones and refresh view
        /// </summary>
        private void UpdateAndRefreshViewZones()
        {
            foreach (int _zone in _instanceGM.zonesPrompt)
            {
                foreach (HexCoord _hex in _instanceGM.zonesCoord[_zone])
                {
                    
                    Hexagon _hexToHighlight = getHexByCoord(_hex);
                    if (_hexToHighlight != null && _hexToHighlight.hoverZoneFX != null)
                    {
                        _hexToHighlight.hoverZoneFX.SetActive(true);
                    }
                }
            }
        }

        /// <summary>
        /// Add new Click in pattern
        /// </summary>
        private void UpdatePatterns()
        {
            if (_instanceGM.hexClicked != null)
            {   // Add this Hexagon to the list of Pattern 
                _instanceGM.PatternsCoord.Add(_instanceGM.hexClicked.positionInTheBoard);
                GameCanvasController _instanceGCC = GameCanvasController.instance;
                // Remove hex_locked view 
                if (_instanceGM.hexClicked != null && _instanceGM.hexClicked.hoverLockedFX != null) _instanceGM.hexClicked.hoverLockedFX.SetActive(false);
                // Add Pattern Hexagon view
                if (_instanceGM.hexClicked != null && _instanceGM.hexClicked.hoverPatternFX != null) _instanceGM.hexClicked.hoverPatternFX.SetActive(true);

                // Garde : l'ancien GameCanvas est en cours de retrait, et ce seul
                // appel n'etait pas protege. Le jour ou le canvas disparait, une
                // NullReferenceException tomberait ici a chaque motif trace - loin
                // de sa cause, et incomprehensible.
                if (_instanceGCC != null)
                    _instanceGCC.SwitchMouseInteractionView(TypeOfMouseInteraction.Hover);
            }

        }
        /// <summary>
        /// New Patterns View
        /// </summary>
        private void RefreshViewPatterns()
        {
            Debug.Log("Refresh ");
            foreach (HexCoord _hexPattern in _instanceGM.PatternsCoord)
            {
                Debug.Log("List not empty");
                Hexagon _hex = getHexByCoord(_hexPattern);
                if (_hex != null && _hex.hoverPatternFX != null) _hex.hoverPatternFX.SetActive(true);
            }
            _instanceGM.hexClicked = null;

            _instanceGM.materials = null;
        }


        /// <summary>
        ///  Change StateOfGame variable from GameManager and change UI/UX for this stateOfGame 
        /// </summary>
        /// <param name="p_stateOfGame"></param>
        public void UpdateStateOfGame(StateOfGame p_stateOfGame)
        {
            GameManager.OnStateOfGameChanged.Invoke(p_stateOfGame);

        }



        /// <summary>
        /// CreateNewPawnAfterErase
        /// </summary>
        /// <param name="p_hexData"></param>
        /// <returns></returns>
        private PawnController CreateNewPawnAfterErase(HexagonData p_hexData)
        {
            TypeOfPawn _pawnType;
            Enum.TryParse(p_hexData.type, out _pawnType);
            int[] _pawnPrefabsPath = PrefabsPath.GetPawnPrefabs(_pawnType);
            GameObject _pawnPrefab;
            if (p_hexData.type == "enemy")
            {
                _pawnPrefab = Instantiate(m_HexagonPrefabs[_pawnPrefabsPath[p_hexData.level - 1]], Vector3.zero, Quaternion.identity);



            }
            else
            {
                _pawnPrefab = Instantiate(m_HexagonPrefabs[_pawnPrefabsPath[p_hexData.level - 1]], Vector3.zero, Quaternion.identity);

            }

            Hexagon _hex;
            if (p_hexData.nextto != null)
            {
                _hex = getHexByCoord(p_hexData.nextto);
            }
            else
            {
                _hex = getHexByCoord(p_hexData.to);
            }
            if (_hex)
            {

                _pawnPrefab.transform.position = _hex.transform.position;
                _pawnPrefab.transform.parent = _hex.transform;
                if (p_hexData.type == "enemy")
                {
                    Vector3 relativePos = Vector3.zero - _pawnPrefab.transform.position;
                    _pawnPrefab.transform.rotation = Quaternion.LookRotation(relativePos);
                }

            }
            else
            {
                if (p_hexData.nextto != null)
                {

                    _pawnPrefab.transform.position = new Vector3((Mathf.Sqrt(3) * p_hexData.nextto.q + Mathf.Sqrt(3) / 2 * p_hexData.nextto.r) * 0.55f, 0.0f, -((3.0f / 2 * p_hexData.nextto.r) * 0.55f));
                }
                else
                {
                    _pawnPrefab.transform.position = new Vector3((Mathf.Sqrt(3) * p_hexData.to.q + Mathf.Sqrt(3) / 2 * p_hexData.to.r) * 0.55f, 0.0f, -((3.0f / 2 * p_hexData.to.r) * 0.55f));
                }
                if (p_hexData.type == "enemy")
                {

                    Vector3 relativePos = Vector3.zero - _pawnPrefab.transform.position;
                    _pawnPrefab.transform.rotation = Quaternion.LookRotation(relativePos);
                }
            }

            PawnController _pawnController = _pawnPrefab.GetComponent<PawnController>();

            Hexagon _hexTo = getHexByCoord(p_hexData.to);

            _pawnController.typeOfPawn = _pawnType;
            _pawnController.Init(p_hexData);
            if (p_hexData.nextto != null)
            {
                _pawnController.UpdateCoord(p_hexData.nextto);
            }
            else
            {
                _pawnController.UpdateCoord(p_hexData.to);
            }

            if (p_hexData.type != "enemy")
            {
                _pawnController.UnlockRotation = true;
            }


            _pawnController.attacked = false;
            _pawnController.target = null;
            _pawnController.NextCoord = null;


            switch (_pawnController.typeOfPawn)
            {
                case TypeOfPawn.unit:
                    if (MNLTHII.Managers.FXManager.Instance != null) MNLTHII.Managers.FXManager.Instance.SpawnCommunityFX(_pawnController.transform.position);
                    break;
                case TypeOfPawn.enemy:
                    if (MNLTHII.Managers.FXManager.Instance != null) MNLTHII.Managers.FXManager.Instance.SpawnEnemyFX(_pawnController.transform.position);
                    break;
            }

            return _pawnController;
        }
        #endregion

        #region ToErase
        #region toErase 






























































        //#if !UNITY_EDITOR
        //#endif






        #endregion


        //public IEnumerator AsyncIntercations(PawnController _pawn)
        //{




















































































        //}

        #endregion
















    }
}








