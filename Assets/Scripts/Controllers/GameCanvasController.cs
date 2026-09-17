using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using MNLTHII;

public class GameCanvasController : MonoBehaviour
{
    #region Static Fields
    // Singleton
    public static GameCanvasController instance;
    #endregion

    #region Serialize Fields

    [Space(4)]
    [Header("------- Text Mesh Pro ---------")]
    [FormerlySerializedAs("m_totalCommandPoints")][SerializeField] private TextMeshProUGUI _totalCommandPoints;
    [FormerlySerializedAs("m_totalthreshold")][SerializeField] private TextMeshProUGUI _totalThreshold;
    [FormerlySerializedAs("m_turnText")][SerializeField] private TextMeshProUGUI _turnText;
    [FormerlySerializedAs("m_totalCPText")][SerializeField] private TextMeshProUGUI _totalCPText;
    [FormerlySerializedAs("m_yourOrderLabel")][SerializeField] private TextMeshProUGUI _yourOrderLabel;
    [FormerlySerializedAs("m_commandPointsText")][SerializeField] private TextMeshProUGUI _commandPointsText;
    [FormerlySerializedAs("m_TypeText")][SerializeField] private TextMeshProUGUI _typeText;
    [FormerlySerializedAs("m_instructionPrompt")][SerializeField] private TextMeshProUGUI _instructionPrompt;
    [FormerlySerializedAs("m_titleAndLevel")][SerializeField] private TextMeshProUGUI _titleAndLevel;
    [FormerlySerializedAs("m_explanation")][SerializeField] private TextMeshProUGUI _explanation;
    [FormerlySerializedAs("m_stateOfGameText")][SerializeField] private TextMeshProUGUI _stateOfGameText;
    [FormerlySerializedAs("m_TypeTextForOrder")][SerializeField] private TextMeshProUGUI _typeTextForOrder;
    [FormerlySerializedAs("m_timerMinuteUnit")] public TextMeshProUGUI _timerMinuteUnit;
    [FormerlySerializedAs("m_timerMinuteTen")] public TextMeshProUGUI _timerMinuteTen;
    [FormerlySerializedAs("m_timerSecondesUnit")] public TextMeshProUGUI _timerSecondesUnit;
    [FormerlySerializedAs("m_timerSecondesTen")] public TextMeshProUGUI _timerSecondesTen;
    [FormerlySerializedAs("m_timerPoints")] public TextMeshProUGUI _timerPoints;

    [Space(4)]
    [Header("------- TMP List ---------")]
    [FormerlySerializedAs("m_zoneTexts")][SerializeField] private TextMeshProUGUI[] _zoneTexts;
    [Header("------- String List ---------")]
    [FormerlySerializedAs("m_ExplanationByBuilding")][SerializeField] private string[] _explanationByBuilding;
    [Space(4)]
    [Header("------- Images ---------")]
    [FormerlySerializedAs("m_hexImagesOrder")][SerializeField] private Image _hexImagesOrder;
    [FormerlySerializedAs("m_orderType")][SerializeField] private Image _orderType;
    [FormerlySerializedAs("m_gaugeCpHover")][SerializeField] private Image _gaugeCpHover;
    [FormerlySerializedAs("m_gaugeCpClick")][SerializeField] private Image _gaugeCpClick;
    [FormerlySerializedAs("m_BckgrndHexData")][SerializeField] private Image _bckgrndHexData;
    [Space(4)]
    [Header("------- Images List ---------")]
    [FormerlySerializedAs("m_hexImagesPrompt")][SerializeField] private Image[] _hexImagesPrompt;
    [FormerlySerializedAs("m_ZonesImagesPrompt")][SerializeField] private Image[] _zonesImagesPrompt;
    [Space(4)]
    [Header("------- Sprites List ---------")]
    [FormerlySerializedAs("m_hexSpritesPrompt")][SerializeField] private Sprite[] _hexSpritesPrompt;
    [FormerlySerializedAs("m_hexSpritesZonesPrompt")][SerializeField] private Sprite[] _hexSpritesZonesPrompt;
    [FormerlySerializedAs("m_hexSpritesOrder")][SerializeField] private Sprite[] _hexSpritesOrder;
    [FormerlySerializedAs("m_hexSpritesTypeOrderLogo")][SerializeField] private Sprite[] _hexSpritesTypeOrderLogo;
    [Space(4)]
    [Header("------- Sprites ---------")]
    [FormerlySerializedAs("m_backgrndForHover")][SerializeField] private Sprite _backgrndForHover;
    [FormerlySerializedAs("m_backgrndForClick")][SerializeField] private Sprite _backgrndForClick;
    [Space(4)]
    [Header("------- Animations ---------")]
    [FormerlySerializedAs("m_cameraAnimator")][SerializeField] Animator _cameraAnimator;
    [FormerlySerializedAs("m_GameStateAnimator")][SerializeField] Animator _gameStateAnimator;
    [FormerlySerializedAs("m_UiUXAnimator")][SerializeField] Animator _uiUXAnimator;
    [FormerlySerializedAs("m_HexDataAnimator")][SerializeField] Animator _hexDataAnimator;
    [Space(4)]
    [Header("------- Container ---------")]
    [FormerlySerializedAs("cpRequiredContainer")][SerializeField] GameObject _cpRequiredContainer;
    [Header("------- Audios ---------")]
    [FormerlySerializedAs("EndOfTurnSFX")][SerializeField] AudioClip _endOfTurnSFX;
    [FormerlySerializedAs("startTurnSFX")][SerializeField] AudioClip _startTurnSFX;
    #endregion

    #region Private Fields
    private AudioSource _audioSource;
    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            ReportStartupState();
        }
        else
        {
            // Destroy est DIFFERE a la fin de la frame : sans ce drapeau, le
            // doublon condamne executait quand meme son Start(), s'abonnait a
            // OnClickHexagonEvent, puis disparaissait de la hierarchie. C'est lui
            // qui recevait ensuite les clics, avec ses cases d'inspecteur vides,
            // d'ou la NullReferenceException a chaque clic.
            _condemned = true;
            Destroy(this.gameObject);
            return;
        }
    }

    /// <summary>
    /// Bilan des references au demarrage, imprime UNE fois.
    ///
    /// Il vit dans Awake et non dans Start : si Start echoue, c'est precisement le
    /// moment ou l'on a le plus besoin de savoir ce qui manque, et un diagnostic qui
    /// depend du code casse ne sert a rien. Il imprime aussi la scene chargee - la
    /// seule facon de decouvrir qu'on ne joue pas la scene qu'on croit.
    /// </summary>
    private void ReportStartupState()
    {
        int total = 0;
        int missing = 0;

        UnityEngine.Object[] watched =
        {
            _typeTextForOrder, _typeText, _bckgrndHexData, _yourOrderLabel,
            _orderType, _gaugeCpClick, _gaugeCpHover, _commandPointsText,
            _hexDataAnimator, _cameraAnimator, _turnText, _instructionPrompt
        };

        for (int i = 0; i < watched.Length; i++)
        {
            total++;
            if (watched[i] == null) missing++;
        }

        if (missing == 0)
        {
            Debug.LogFormat("[GameCanvas] Demarrage sain dans la scene \"{0}\" : {1} references en place.",
                            gameObject.scene.name, total);
            return;
        }

        Debug.LogError("[GameCanvas] " + missing + " reference(s) sur " + total
            + " manquent des le demarrage, dans la scene \"" + gameObject.scene.name + "\"."
            + "\n  Si le fichier .unity sur le disque les contient, c'est que la scene"
            + " chargee n'est pas celle du fichier : verifie quelle scene est ouverte."
            + "\n  _cameraAnimator    " + State(_cameraAnimator)
            + "\n  _typeTextForOrder  " + State(_typeTextForOrder)
            + "\n  _turnText          " + State(_turnText)
            + "\n  _instructionPrompt " + State(_instructionPrompt), this);
    }

    /// <summary>
    /// Vrai pour un doublon que Awake a condamne. Start() doit alors ne rien faire :
    /// surtout pas s'abonner a des evenements statiques dont personne ne le retirera.
    /// </summary>
    private bool _condemned;

    private void OnDestroy()
    {
        // OnClickHexagonEvent et OnStateOfGameChanged sont STATIQUES : ils survivent
        // a la destruction de cet objet, et meme a la sortie du mode Play quand le
        // rechargement de domaine est desactive. Sans ce desabonnement, chaque
        // partie empile un abonne fantome de plus.
        if (GameManager.OnClickHexagonEvent != null)
            GameManager.OnClickHexagonEvent.RemoveListener(RefreshHexClickedView);

        if (GameManager.OnStateOfGameChanged != null)
            GameManager.OnStateOfGameChanged.RemoveListener(StateOfGameView);

        // Le singleton doit relacher sa reference, sinon la partie suivante croit
        // encore avoir un canvas valide.
        if (instance == this) instance = null;
    }

    /// <summary>
    /// ON S'ABONNE D'ABORD. TOUT LE RESTE VIENT APRES.
    ///
    /// L'ordre de ces lignes n'est pas une question de style, c'est ce qui a casse
    /// le jeu : _cameraAnimator n'etait pas assigne, SetTrigger levait une
    /// exception, et Start mourait AVANT les deux AddListener. Le canvas restait
    /// donc abonne a rien, pour toute la partie. Plus aucun clic n'arrivait jusqu'a
    /// lui, et le symptome - "le clic ne fait plus rien" - ne ressemblait en rien a
    /// sa cause, une animation de camera decorative.
    ///
    /// La regle generale : dans un Start, ce dont depend le reste du jeu passe en
    /// premier, et tout ce qui peut echouer sur une reference vide passe apres, avec
    /// un garde. Un Start qui leve une exception laisse l'objet a moitie construit
    /// pour toujours, et Unity ne le rappellera jamais.
    /// </summary>
    private void Start()
    {
        // Doublon : on ne s'abonne a rien et on ne touche a rien.
        if (_condemned) return;

        // --- 1. Les abonnements, avant tout le reste ---
        if (GameManager.OnClickHexagonEvent != null)
            GameManager.OnClickHexagonEvent.AddListener(RefreshHexClickedView);
        else
            Debug.LogError("[GameCanvas] OnClickHexagonEvent est nul au demarrage : "
                         + "aucun GameManager dans la scene ? Les clics ne seront pas relayes.");

        if (GameManager.OnStateOfGameChanged != null)
            GameManager.OnStateOfGameChanged.AddListener(StateOfGameView);

        // --- 2. Le decor, qui a le droit de manquer ---
        _audioSource = GetComponent<AudioSource>();

        if (_cameraAnimator != null)
        {
            _cameraAnimator.SetTrigger("Play");
        }
        else
        {
            Debug.LogWarning("[GameCanvas] _cameraAnimator n'est pas assigne : pas d'animation "
                           + "d'ouverture. Le jeu fonctionne, c'est purement decoratif.");
        }

        StartCoroutine(WaitingForChallenge());
    }

    #endregion

    #region Public Functions
    public void RefreshTurnInfo(TurnData p_turn)
    {
        _instructionPrompt.text = "Make your <style=title>order count more</style> by clicking on a ";
        SetActiveFalseAllTexts(_zoneTexts);
        SetActiveFalseAllImages(_hexImagesPrompt);
        SetActiveFalseAllImages(_zonesImagesPrompt);
        for (int i = 0; i < p_turn.prompt.asset.Length; i++)
        {
            _instructionPrompt.text += "<style=title>" + p_turn.prompt.asset[i] + "</style>";
            if (i == p_turn.prompt.asset.Length - 1)
            {
                _instructionPrompt.text += " tile and/or in " + "<style=title>sector ";
            }
            else
            {
                _instructionPrompt.text += " or ";
            }
            switch (p_turn.prompt.asset[i])
            {

                case TypeOfHex.desert:
                    _hexImagesPrompt[i].sprite = _hexSpritesPrompt[0];
                    break;
                case TypeOfHex.plain:
                    _hexImagesPrompt[i].sprite = _hexSpritesPrompt[1];
                    break;
                case TypeOfHex.gas:
                    _hexImagesPrompt[i].sprite = _hexSpritesPrompt[2];
                    break;
                case TypeOfHex.crystal:
                    _hexImagesPrompt[i].sprite = _hexSpritesPrompt[3];
                    break;
                case TypeOfHex.hill:
                    _hexImagesPrompt[i].sprite = _hexSpritesPrompt[4];
                    break;
                case TypeOfHex.mountain:
                    _hexImagesPrompt[i].sprite = _hexSpritesPrompt[5];
                    break;

            }
            _hexImagesPrompt[i].transform.parent.gameObject.SetActive(true);
        }
        for (int i = 0; i < p_turn.prompt.zone.Length; i++)
        {
            _instructionPrompt.text += p_turn.prompt.zone[i] + "</style> ";
            if (i == p_turn.prompt.asset.Length - 1)
            {
                _instructionPrompt.text += "or <style=title>";
            }

            int _j = i;
            if (p_turn.prompt.zone.Length == 1)
            {
                _j++;
            }
            _zonesImagesPrompt[_j].transform.parent.gameObject.SetActive(true);
            _zonesImagesPrompt[_j].sprite = _hexSpritesZonesPrompt[p_turn.prompt.zone[i] - 1];

        }



        _turnText.text = "Turn  " + p_turn.num;

        _totalCPText.text = p_turn.cp.ToString();

    }

    public void RefreshHexHoveredView(Hexagon p_hex)
    {
        if (GameManager.instance.hexClicked != null && p_hex.positionInTheBoard.CompareHexCoord(GameManager.instance.hexClicked.positionInTheBoard))
        {
            RefreshHexClickedView(p_hex);
        }
        else
        {
            _typeText.text = p_hex.type.ToString();
            _typeTextForOrder.text = "";
            SwitchMouseInteractionView(TypeOfMouseInteraction.Hover);
            RefreshDataView(p_hex);
            _yourOrderLabel.alpha = 0.0f;
            _bckgrndHexData.sprite = _backgrndForHover;
            if (p_hex.type != TypeOfHex.Base && p_hex.type != TypeOfHex.portal)
            {
                if (p_hex.threshold > 0) _gaugeCpHover.fillAmount = (float)((p_hex.energy * 100) / p_hex.threshold) / 100; else _gaugeCpHover.fillAmount = 0;
                _gaugeCpClick.fillAmount = 0;
                _cpRequiredContainer.SetActive(true);
            }
            else
            {
                _cpRequiredContainer.SetActive(false);
                _gaugeCpHover.fillAmount = 0;
                _gaugeCpClick.fillAmount = 0;
            }
        }

    }
    public void RefresPawnHoveredView(PawnController p_pawn)
    {
        SwitchMouseInteractionView(TypeOfMouseInteraction.Hover);
        RefreshDataView(p_pawn);

    }
    public void RefreshHexHoveredView()
    {

        _yourOrderLabel.alpha = 0.0f;
        SwitchMouseInteractionView(TypeOfMouseInteraction.Hover);
        _commandPointsText.text = "-";
        _typeText.text = "-";
        if (!GameManager.instance.hexClicked)
            _hexDataAnimator.SetBool("Open", false);
    }
    public void SwitchMouseInteractionView(TypeOfMouseInteraction _mouseInteraction)
    {

        switch (_mouseInteraction)
        {
            case TypeOfMouseInteraction.Hover:
                _orderType.sprite = _hexSpritesTypeOrderLogo[0];
                break;
            case TypeOfMouseInteraction.Click:
                _orderType.sprite = _hexSpritesTypeOrderLogo[1];
                break;

        }

    }
    // --- Garde-fou de references, ajoute pour tracer la NRE au clic ---
    private bool _missingRefsReported;

    /// <summary>
    /// Verifie que les cases de l'inspecteur utilisees par le panneau d'ordre sont
    /// bien remplies, et le dit UNE SEULE FOIS en nommant celles qui manquent.
    ///
    /// Sans ca, une case vide se traduit par une NullReferenceException a chaque
    /// clic : elle donne un numero de ligne mais jamais le nom du champ fautif, et
    /// surtout elle interrompt la methode, donc plus rien ne s'affiche.
    ///
    /// Le log dit aussi si le composant qui recoit l'evenement est bien celui que
    /// designe instance. L'abonnement se fait dans Start() : un doublon condamne
    /// dans Awake() par Destroy(gameObject) a le temps de s'abonner avant de
    /// disparaitre, car Destroy est differe a la fin de la frame. C'est alors LUI
    /// qui recoit les clics, avec ses cases jamais remplies.
    /// </summary>
    /// <summary>
    /// Etat reel d'une reference serialisee : remplie, jamais remplie, ou detruite.
    ///
    /// UnityEngine.Object surcharge l'operateur == pour qu'un objet detruit se
    /// compare egal a null. Pratique au quotidien, catastrophique pour un
    /// diagnostic : les deux pannes deviennent indiscernables. ReferenceEquals
    /// court-circuite la surcharge et interroge la vraie reference managee.
    /// </summary>
    private static string State(UnityEngine.Object o)
    {
        if (ReferenceEquals(o, null)) return "VIDE      (la case n'a jamais recu de valeur)";
        if (o == null) return "DETRUIT   (l'objet a ete supprime pendant la partie)";
        return "ok";
    }

    /// <summary>Combien de GameCanvasController vivent reellement dans la scene.</summary>
    private static int CountControllers()
    {
        GameCanvasController[] all = FindObjectsByType<GameCanvasController>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        return (all != null) ? all.Length : 0;
    }

    private bool OrderViewRefsAreValid()
    {
        bool ok = _typeTextForOrder != null
                  && _typeText != null
                  && _bckgrndHexData != null
                  && _yourOrderLabel != null
                  && _orderType != null
                  && _gaugeCpClick != null
                  && _gaugeCpHover != null
                  && _commandPointsText != null
                  && _hexDataAnimator != null;

        if (ok) return true;
        if (_missingRefsReported) return false;

        _missingRefsReported = true;

        // On distingue VIDE de DETRUIT, et c'est toute la difference :
        //
        //   VIDE    - la case de l'inspecteur n'a jamais recu de valeur, ou la scene
        //             chargee a perdu ses donnees. Le probleme est en amont du jeu.
        //
        //   DETRUIT - la case etait remplie et l'objet vise a ete SUPPRIME pendant la
        //             partie. Le probleme est dans le jeu, et il y a un coupable a
        //             trouver : du code qui detruit le panneau de donnees.
        //
        // Le test : ReferenceEquals ignore la surcharge de == de UnityEngine.Object,
        // qui fait passer un objet detruit pour null. Sans cette precaution les deux
        // cas se ressemblent, et on cherche pendant une heure du mauvais cote.
        Debug.LogError("[GameCanvas] References manquantes sur l'objet \"" + name + "\""
            + "\n  instance officielle : " + (instance == this)
            + "\n  scene               : " + gameObject.scene.name
            + "\n  composants du type  : " + CountControllers()
            + "\n  --- panneau d'ordre ---"
            + "\n  _typeTextForOrder  " + State(_typeTextForOrder)
            + "\n  _typeText          " + State(_typeText)
            + "\n  _bckgrndHexData    " + State(_bckgrndHexData)
            + "\n  _yourOrderLabel    " + State(_yourOrderLabel)
            + "\n  _orderType         " + State(_orderType)
            + "\n  _gaugeCpClick      " + State(_gaugeCpClick)
            + "\n  _gaugeCpHover      " + State(_gaugeCpHover)
            + "\n  _commandPointsText " + State(_commandPointsText)
            + "\n  _hexDataAnimator   " + State(_hexDataAnimator)
            + "\n  --- temoins, hors panneau d'ordre ---"
            + "\n  _turnText          " + State(_turnText)
            + "\n  _instructionPrompt " + State(_instructionPrompt)
            + "\n  _stateOfGameText   " + State(_stateOfGameText)
            + "\n  _totalCPText       " + State(_totalCPText), this);

        return false;
    }

    public void RefreshHexClickedView(Hexagon p_hex)
    {
        if (p_hex)
        {
            if (!OrderViewRefsAreValid()) return;

            _typeTextForOrder.text = p_hex.type.ToString();
            _typeText.text = " ";
            _bckgrndHexData.sprite = _backgrndForClick;
            _yourOrderLabel.alpha = 1.0f;
            SwitchMouseInteractionView(TypeOfMouseInteraction.Click);

            RefreshDataView(p_hex);
            if (p_hex.threshold > 0) _gaugeCpClick.fillAmount = (float)((p_hex.energy * 100) / p_hex.threshold) / 100; else _gaugeCpClick.fillAmount = 0;
            _gaugeCpHover.fillAmount = 0;
        }

        else
        {
            if (!OrderViewRefsAreValid()) return;

            _yourOrderLabel.alpha = 0.0f;
            _hexDataAnimator.SetBool("Open", false);
        }


    }
    #endregion

    #region Private Functions

    private void RefreshDataView(Hexagon p_hex)
    {
        if (p_hex)
        {
            _hexDataAnimator.SetBool("Open", true);

            _commandPointsText.text = (p_hex.threshold - p_hex.commandPoints).ToString();

            // _typeText.text = p_hex.type.ToString();
            switch (p_hex.type)
            {

                case TypeOfHex.desert:
                    _explanation.text = _explanationByBuilding[0];
                    switch (p_hex.level + 1)
                    {
                        case 1:
                            _hexImagesOrder.sprite = _hexSpritesOrder[0];
                            _titleAndLevel.text = "Unit lvl.1";
                            break;
                        case 2:
                        case 3:
                            _hexImagesOrder.sprite = _hexSpritesOrder[1];
                            _titleAndLevel.text = "Unit lvl.2";
                            break;
                    }


                    break;
                case TypeOfHex.plain:
                    _explanation.text = _explanationByBuilding[0];
                    switch (p_hex.level + 1)
                    {
                        case 1:
                            _hexImagesOrder.sprite = _hexSpritesOrder[0];
                            _titleAndLevel.text = "Unit lvl.1";
                            break;
                        case 2:
                        case 3:
                            _hexImagesOrder.sprite = _hexSpritesOrder[1];
                            _titleAndLevel.text = "Unit lvl.2";
                            break;
                    }
                    break;
                case TypeOfHex.gas:
                    _explanation.text = _explanationByBuilding[1];
                    switch (p_hex.level + 1)
                    {
                        case 0:
                            _hexImagesOrder.sprite = _hexSpritesOrder[2];
                            break;
                        case 1:
                            _hexImagesOrder.sprite = _hexSpritesOrder[3];
                            _titleAndLevel.text = "Gas Factory lvl.1";
                            break;
                        case 2:
                        case 3:
                            _hexImagesOrder.sprite = _hexSpritesOrder[4];
                            _titleAndLevel.text = "Gas Factory lvl.2";
                            break;

                    }


                    break;
                case TypeOfHex.crystal:
                    _explanation.text = _explanationByBuilding[2];
                    switch (p_hex.level + 1)
                    {
                        case 0:
                            _hexImagesOrder.sprite = _hexSpritesOrder[5];
                            break;
                        case 1:
                            _hexImagesOrder.sprite = _hexSpritesOrder[6];
                            _titleAndLevel.text = "Crystal Factory lvl.1";
                            break;
                        case 2:
                        case 3:
                            _hexImagesOrder.sprite = _hexSpritesOrder[7];
                            _titleAndLevel.text = "Crystal Factory lvl.2";
                            break;

                    }
                    break;
                case TypeOfHex.hill:
                    _explanation.text = _explanationByBuilding[3];
                    switch (p_hex.level + 1)
                    {
                        case 0:
                            _hexImagesOrder.sprite = _hexSpritesOrder[8];
                            break;
                        case 1:
                            _hexImagesOrder.sprite = _hexSpritesOrder[9];
                            _titleAndLevel.text = "Bunker lvl.1";
                            break;
                        case 2:
                        case 3:
                            _hexImagesOrder.sprite = _hexSpritesOrder[10];
                            _titleAndLevel.text = "Bunker lvl.2";
                            break;

                    }
                    break;
                case TypeOfHex.mountain:
                    _explanation.text = _explanationByBuilding[4];
                    switch (p_hex.level + 1)
                    {
                        case 0:
                            _hexImagesOrder.sprite = _hexSpritesOrder[11];
                            break;
                        case 1:
                            _hexImagesOrder.sprite = _hexSpritesOrder[12];
                            _titleAndLevel.text = "Command Center lvl.1";
                            break;
                        case 2:
                        case 3:
                            _hexImagesOrder.sprite = _hexSpritesOrder[13];
                            _titleAndLevel.text = "Command Center lvl.2";
                            break;

                    }
                    break;
                case TypeOfHex.Base:
                    _explanation.text = _explanationByBuilding[5];
                    switch (p_hex.level + 1)
                    {
                        case 0:
                            _hexImagesOrder.sprite = _hexSpritesOrder[14];
                            break;
                        case 1:
                        case 2:
                            _hexImagesOrder.sprite = _hexSpritesOrder[15];
                            _titleAndLevel.text = "Base";
                            break;

                    }
                    break;
                case TypeOfHex.portal:
                    _explanation.text = _explanationByBuilding[6];
                    _hexImagesOrder.sprite = _hexSpritesOrder[16];
                    _titleAndLevel.text = "Portal";
                    break;



            }
        }
    }

    private void RefreshDataView(PawnController p_pawn)
    {

        //_hexDataAnimator.SetBool("Open", true);
        //_commandPointsText.text = p_hex.commandPoints.ToString();
        //_typeText.text = p_hex.type.ToString();


    }





    private void StateOfGameView(StateOfGame p_state)
    {
        switch (p_state)
        {
            case StateOfGame.endOfTurn:
                //Blocking all mouse interactions in endOfTurn time
                GameManager.instance.canClickOrHover = false;

                StopAllCoroutines();

                _stateOfGameText.text = "END OF TURN";
                _stateOfGameText.color = Color.white;
                _stateOfGameText.fontSize = 50;

                _gameStateAnimator.SetBool("GameState", true);
                _uiUXAnimator.SetBool("FadeIn", false);

                StartCoroutine(EndOfGame());
                //SFX
                _audioSource.clip = _endOfTurnSFX;
                _audioSource.Play();

                // for testing

                break;
            case StateOfGame.waitingForChallenge:
                StopAllCoroutines();
                _stateOfGameText.text = "CHALLENGE\n HASN’T STARTED YET";
                _stateOfGameText.color = Color.white;
                _stateOfGameText.fontSize = 35;
                _gameStateAnimator.SetBool("GameState", true);


                break;
            case StateOfGame.Interactions:
                //Blocking all mouse interactions in Interactions time
                GameManager.instance.canClickOrHover = false;
                StopAllCoroutines();
                StartCoroutine(EndOfGame());

                _uiUXAnimator.SetBool("FadeIn", false);

                GameManager.instance.m_stateOfGame = StateOfGame.Interactions;

                break;
            case StateOfGame.inGame:
                StopAllCoroutines();
                //Can do all mouse interactions 
                GameManager.instance.canClickOrHover = true;

                if (GameManager.instance.turn > 0)
                    _stateOfGameText.text = "TURN " + GameManager.instance.turn;
                else
                    _stateOfGameText.text = "Map";

                _stateOfGameText.color = Color.white;
                _stateOfGameText.fontSize = 50;

                _gameStateAnimator.SetBool("GameState", true);
                _uiUXAnimator.SetBool("FadeIn", true);

                StartCoroutine(EndOfGame());
                //SFX
                _audioSource.clip = _startTurnSFX;
                _audioSource.Play();
                GameManager.instance.m_stateOfGame = StateOfGame.inGame;
                break;

            case StateOfGame.victory:
                StopAllCoroutines();
                Color victoryColor;
                ColorUtility.TryParseHtmlString("#00ff1d", out victoryColor);
                _stateOfGameText.color = victoryColor;
                _stateOfGameText.text = "Victory";
                _stateOfGameText.fontSize = 64;
                _gameStateAnimator.SetBool("GameState", true);
                _uiUXAnimator.SetBool("FadeIn", true);
                break;

            case StateOfGame.defeat:
                StopAllCoroutines();
                Color defeatColor;
                ColorUtility.TryParseHtmlString("#ff0000", out defeatColor);
                _stateOfGameText.color = defeatColor;
                _stateOfGameText.text = "Defeat";
                _stateOfGameText.fontSize = 64;
                _gameStateAnimator.SetBool("GameState", true);
                _uiUXAnimator.SetBool("FadeIn", true);
                break;
            default:
                StartCoroutine(EndOfGame());
                break;
        }

    }


    #endregion

    #region Tools
    private void SetActiveFalseAllTexts(TextMeshProUGUI[] p_TMParray)
    {

        if (p_TMParray != null && p_TMParray.Length > 0)
            foreach (TextMeshProUGUI _tmp in p_TMParray)
            {
                _tmp.gameObject.SetActive(false);
            }


    }

    private void SetActiveFalseAllImages(Image[] p_Imgarray)
    {

        if (p_Imgarray != null && p_Imgarray.Length > 0)
            foreach (Image _img in p_Imgarray)
            {
                _img.transform.parent.gameObject.SetActive(false);
            }


    }

    //[Serializable]
    //public enum TypeOfMouseInteraction
    //{
    //   Hover,
    //   Click,
    //   None
    //}
    private IEnumerator EndOfGame()
    {



        if (GameManager.instance.m_stateOfGame != StateOfGame.waitingForChallenge)
        {
            yield return new WaitForSeconds(2.0f);
        }
        _gameStateAnimator.SetBool("GameState", false);

    }
    private IEnumerator WaitingForChallenge()
    {
        yield return new WaitForSeconds(3.0f);

        GameManager.instance.m_stateOfGame = StateOfGame.waitingForChallenge;
        StateOfGameView(StateOfGame.waitingForChallenge);

    }
    #endregion

}




