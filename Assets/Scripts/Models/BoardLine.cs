/*using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoardLine : MonoBehaviour
{

    public List<Hexagon> hexagonsInBoard;
    private int[] m_firstIdxLine = new int[2];
    private int[] m_LastIdxLine = new int[2];
    private int m_lineLength = 9;
    private bool isEvenLine = false;
    GameObject m_board;
   
 


    // Start is called before the first frame update
    void Start()
    {
        m_board = GameObject.Find("Board");

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public BoardLine Initialize(GameObject[] p_hexagonPrefab,  int p_lineLength, bool p_isEvenLine, int p_lineNum,int  p_hexByLine,int p_IdxIntheBoardServer)
    {
        BoardController _boardController = BoardController.instance;
        hexagonsInBoard = new List<Hexagon>();

        int _XIdx = 0;
        for (int i = -(p_hexByLine/2) ; p_isEvenLine?i < p_hexByLine/2: i <= p_hexByLine / 2; i++)
            {
            // get data for this case from server
         //   HexagonData hexData =_boardController.boardFromServer[p_IdxIntheBoardServer];
            // get prefabs by state
            string[] _hexPrefabsPath =PrefabsPath.GetHexagonPrefabs(hexData.state);
            // Instantiate Hexagon Prefab by Level 
            GameObject _hexagonPrefab = Instantiate(Resources.Load<GameObject>(_hexPrefabsPath[hexData.hexLevel]) as GameObject, new Vector3(p_isEvenLine?0.5f + i * 1.0f: i * 1.0f, 0.0f, p_lineNum * 1.0f), Quaternion.Euler(0, 90, 0));
            // Instantiate PawnPrefab by Type 
        
            // Give data to the case from the server
            Hexagon _newHexagon = _hexagonPrefab.AddComponent<Hexagon>();
            _newHexagon.Init(hexData, _hexagonPrefab);
            _newHexagon.transform.parent = transform;
         //   _newHexagon.positionInTheBoard = new Vector2Int(hexData.x, hexData.y);
            _newHexagon.size = new Vector2Int(1, 1);
            _newHexagon.state = hexData.state;
            hexagonsInBoard.Add(_newHexagon);
            if (hexData.pawn != TypeOfPawn.None)
            {
                string[] _pawnPrefabsPath = PrefabsPath.GetPawnPrefabs(hexData.pawn);
                GameObject _pawnPrefab = Instantiate(Resources.Load<GameObject>(_pawnPrefabsPath[hexData.pawnLevel]) as GameObject, Vector3.zero, Quaternion.identity);
               _pawnPrefab.transform.position= new Vector3(_newHexagon.transform.position.x,
                                                                                             _newHexagon.transform.position.y + 0.3f,
                                                                                              _newHexagon.transform.position.z) ;
                _pawnPrefab.transform.parent = _newHexagon.transform;
            }

            p_IdxIntheBoardServer++;
        }
        _boardController.m_idxBoardFromServer= p_IdxIntheBoardServer;
        return this;


    }
}*/
