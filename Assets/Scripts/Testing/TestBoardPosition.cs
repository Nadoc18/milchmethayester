/*using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TestBoardPosition : MonoBehaviour
{

    [SerializeField]  Button m_testHexPosButton;
    [SerializeField] Vector2Int m_idxHexToTest;
    BoardController _boardController;
    // Start is called before the first frame update
    void Start()
    {
        _boardController = BoardController.instance;
#if UNITY_EDITOR
        m_testHexPosButton.onClick.AddListener(() => TestHexPosition(m_idxHexToTest));
#endif
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void TestHexPosition(Vector2Int p_idx)
    {
     
        BoardLine _line = _boardController.Lines[p_idx.y];
        Debug.Log(_line.hexagonsInBoard[p_idx.x].positionInTheBoard);


}

}
*/