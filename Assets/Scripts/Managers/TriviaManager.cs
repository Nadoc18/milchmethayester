using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Events;
using Michsky.UI.Reach;
using MNLTHII;
using MNLTHII.Rules;

/// <summary>
/// Le Trivia (Halacha, Tanakh, Musar, Tefillah).
///
/// ROLE ACTUEL : le Trivia est la source de REVENU, pas l'autorisation d'agir.
/// Une question est posee en debut de chaque tour ; une bonne reponse rapporte
/// +25 Energie, une mauvaise ne rapporte rien. Ensuite, et dans les deux cas, le
/// joueur entre dans sa phase de depense et agit autant que son solde le permet.
///
/// Avant, une bonne reponse donnait directement l'action sur l'hexagone clique,
/// gratuitement, et le tour s'arretait la. Le savoir remplacait la strategie ;
/// desormais il la finance.
///
/// Les questions vivent dans Assets/Resources/trivia_questions.json (UTF-8),
/// pour que l'hebreu ne transite jamais par un fichier .cs.
/// </summary>
public class TriviaManager : MonoBehaviour
{
    public static TriviaManager Instance;

    [Header("Reach UI Elements")]
    public ModalWindowManager modalWindow;
    public TimerBar timerBar;
    public ButtonManager[] answerButtons;

    [Header("Choix du sujet")]
    [Tooltip("L'ecran des quatre sujets, construit par Milchemet > Construire le HUD. Absent, on tire une question au hasard comme avant.")]
    public MNLTHII.Managers.SubjectChoicePanel subjectPanel;

    [Header("Panneau de question dedie")]
    [Tooltip("Le panneau hebreu construit par Milchemet > Construire le HUD. S'il est assigne, il remplace la modale Reach.")]
    public MNLTHII.Managers.TriviaPanelController panel;

    [Tooltip("Duree pendant laquelle le verdict reste visible avant que le panneau se ferme.")]
    public float resultHoldSeconds = 0.9f;

    [Header("Texte de la question (TextMeshPro)")]
    [Tooltip("Champ TMP qui affiche l'enonce. Prioritaire sur le descriptionText de la modale.")]
    public TMPro.TextMeshProUGUI questionText;
    [Tooltip("Champ TMP optionnel pour la categorie (Halacha, Tanakh, Musar, Tefillah).")]
    public TMPro.TextMeshProUGUI categoryText;

    [Header("Configuration")]
    public string questionsResourceName = "trivia_questions";
    public float timeLimit = 10f;
    [Tooltip("Bascule automatiquement le champ en droite-a-gauche quand l'enonce contient de l'hebreu.")]
    public bool autoDetectRightToLeft = true;

    [Header("Etat (lecture seule)")]
    public int questionsLoaded = 0;
    public string currentCategory = "";

    private readonly List<TriviaQuestion> _bank = new List<TriviaQuestion>(64);
    private readonly List<int> _remaining = new List<int>(64);
    private TriviaQuestion _current;

    // =====================================================================
    //  OFFRE DU TOUR : une question deja tiree par sujet
    // =====================================================================
    /// <summary>
    /// Les quatre sujets, dans l'ordre de l'enum TriviaCategory. Ces cles doivent
    /// correspondre au champ "category" de trivia_questions.json a la lettre pres.
    /// </summary>
    private static readonly string[] CategoryKeys = { "Halacha", "Tanakh", "Musar", "Tefillah" };

    // Un sac par sujet, pour que le tirage sans repetition vaille SUJET PAR SUJET.
    // Un seul sac global aurait fini par ne plus proposer de Halacha pendant six
    // tours d'affilee, et l'ecran de choix aurait perdu la moitie de ses cartes.
    private readonly List<int>[] _byCategory = new List<int>[4];
    private readonly List<int>[] _remainingByCategory = new List<int>[4];

    // L'offre du tour : trois tableaux paralleles, alloues UNE fois.
    private readonly string[] _offerKeys = new string[4];
    private readonly int[] _offerRewards = new int[4];
    private readonly int[] _offerDifficulties = new int[4];
    private readonly int[] _offerBankIndex = new int[4];

    // Question imposee par le choix du joueur : DrawQuestion l'honore et se reinitialise.
    private int _forcedIndex = -1;

    // Gain de la question en cours, deduit de sa difficulte. Verse a la resolution.
    private int _currentReward = InteractionRules.TRIVIA_ENERGY_REWARD;

    // Un delegue par bouton, cree une seule fois. Auparavant chaque question
    // reabonnait quatre lambdas capturant l'index, soit quatre closures par tour.
    private UnityAction[] _answerCallbacks;

    // Titres deja en majuscules : ToUpper() allouait une string a chaque question.
    private string[] _categoryUpper;

    private bool _showTrivia = false;
    // Hexagone eventuellement associe a la question. Il n'a plus d'effet sur les
    // regles (le Trivia ne fait que verser de l'Energie) mais il reste expose pour
    // les scripts qui voudraient afficher un contexte.
    public HexCoord PendingHex { get; private set; }
    private float _questionStartTime;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        LoadQuestions();
        BindAnswerButtons();

        // Le panneau dedie ne connait aucune regle : il rend l'index choisi et
        // c'est ici que la reponse est jugee. Un seul delegue, cree au demarrage.
        if (panel != null) panel.SetAnswerHandler(AnswerQuestion);
        if (subjectPanel != null) subjectPanel.SetChoiceHandler(OnSubjectChosen);
    }

    /// <summary>Abonnement unique des boutons de reponse, pour la duree de la partie.</summary>
    private void BindAnswerButtons()
    {
        if (answerButtons == null) return;

        _answerCallbacks = new UnityAction[answerButtons.Length];

        for (int i = 0; i < answerButtons.Length; i++)
        {
            if (answerButtons[i] == null) continue;

            int answerIndex = i;
            _answerCallbacks[i] = delegate { AnswerQuestion(answerIndex); };

            answerButtons[i].onClick.RemoveAllListeners();
            answerButtons[i].onClick.AddListener(_answerCallbacks[i]);
        }
    }

    // =====================================================================
    //  CHARGEMENT DE LA BANQUE
    // =====================================================================
    private void LoadQuestions()
    {
        _bank.Clear();

        TextAsset asset = Resources.Load<TextAsset>(questionsResourceName);
        if (asset != null)
        {
            try
            {
                TriviaQuestionBank bank = JsonConvert.DeserializeObject<TriviaQuestionBank>(asset.text);
                if (bank != null && bank.questions != null)
                {
                    foreach (var q in bank.questions)
                    {
                        if (q != null && q.IsValid()) _bank.Add(q);
                    }
                }
            }
            catch (System.Exception e)
            {
                    Debug.LogErrorFormat("[Trivia] Lecture de {0}.json impossible : {1}", questionsResourceName, e.Message);
            }
        }
        else
        {
            Debug.LogWarningFormat("[Trivia] Resources/{0}.json introuvable.", questionsResourceName);
        }

        questionsLoaded = _bank.Count;

        _categoryUpper = new string[_bank.Count];
        for (int i = 0; i < _bank.Count; i++)
        {
            string category = _bank[i].category;
            _categoryUpper[i] = string.IsNullOrEmpty(category) ? "TRIVIA" : category.ToUpperInvariant();
        }

        BuildCategoryBags();
        ResetDraw();

        Debug.LogFormat("[Trivia] {0} question(s) chargee(s) : {1} Halacha, {2} Tanakh, {3} Moussar, {4} Tefila.",
                        questionsLoaded,
                        _byCategory[0].Count, _byCategory[1].Count,
                        _byCategory[2].Count, _byCategory[3].Count);
    }

    /// <summary>Range les index de la banque dans quatre sacs, un par sujet.</summary>
    private void BuildCategoryBags()
    {
        for (int c = 0; c < 4; c++)
        {
            if (_byCategory[c] == null) _byCategory[c] = new List<int>(16);
            else _byCategory[c].Clear();

            if (_remainingByCategory[c] == null) _remainingByCategory[c] = new List<int>(16);
            else _remainingByCategory[c].Clear();
        }

        for (int i = 0; i < _bank.Count; i++)
        {
            int slot = CategorySlot(_bank[i].category);
            if (slot < 0) continue;

            _byCategory[slot].Add(i);
            _remainingByCategory[slot].Add(i);
        }
    }

    /// <summary>Index de sujet a partir de la cle latine du JSON, ou -1.</summary>
    private static int CategorySlot(string category)
    {
        if (string.IsNullOrEmpty(category)) return -1;

        for (int i = 0; i < CategoryKeys.Length; i++)
            if (CategoryKeys[i] == category) return i;

        return -1;
    }

    /// <summary>Remet toutes les questions dans le sac (tirage sans repetition).</summary>
    private void ResetDraw()
    {
        _remaining.Clear();
        for (int i = 0; i < _bank.Count; i++) _remaining.Add(i);
    }

    private int _currentIndex = -1;

    private TriviaQuestion DrawQuestion()
    {
        if (_bank.Count == 0) return null;

        // Le joueur a choisi un sujet : la question est deja designee, et elle a
        // deja ete retiree du sac de son sujet par BuildTurnOffer.
        if (_forcedIndex >= 0 && _forcedIndex < _bank.Count)
        {
            int forced = _forcedIndex;
            _forcedIndex = -1;
            _currentIndex = forced;
            _remaining.Remove(forced);
            return _bank[forced];
        }

        if (_remaining.Count == 0) ResetDraw();

        int pick = Random.Range(0, _remaining.Count);
        int index = _remaining[pick];
        _remaining.RemoveAt(pick);

        _currentIndex = index;
        return _bank[index];
    }

    // =====================================================================
    //  POSER UNE QUESTION
    // =====================================================================
    /// <summary>
    /// Question de debut de tour. Elle ne porte sur aucun hexagone : elle ne decide
    /// que du revenu. TurnManager l'appelle, et recupere la main via Resolve.
    /// </summary>
    public void AskIncomeQuestion()
    {
        AskQuestion(null);
    }

    /// <summary>
    /// Le nouveau depart de tour : on montre d'abord les quatre sujets, chacun avec
    /// le gain de la question qui l'attend, et le joueur choisit.
    ///
    /// Sans ecran de choix branche, ou si la banque ne permet pas de composer une
    /// offre, on retombe sur l'ancien comportement - une question au hasard. Le jeu
    /// continue de tourner, il perd juste sa decision.
    /// </summary>
    public void BeginIncomePhase()
    {
        if (subjectPanel == null)
        {
            Debug.LogWarning("[Trivia] Pas d'ecran de choix du sujet (subjectPanel vide) : "
                           + "question tiree au hasard. Relance Milchemet > Construire le HUD.");
            AskIncomeQuestion();
            return;
        }

        if (!BuildTurnOffer())
        {
            Debug.LogWarning("[Trivia] Impossible de composer une offre de sujets : question au hasard.");
            AskIncomeQuestion();
            return;
        }

        // Un panneau sans racine n'affichera rien ET ne rappellera jamais : le tour
        // resterait bloque pour toujours, ce qui est pire qu'un saut. On prefere
        // retomber sur une question au hasard, en le disant.
        if (subjectPanel.panelRoot == null)
        {
            Debug.LogError("[Trivia] L'ecran de choix du sujet n'a pas de panelRoot : il ne peut "
                         + "rien afficher et ne rendrait jamais la main. Question tiree au hasard.");
            AskIncomeQuestion();
            return;
        }

        // Plateau verrouille : l'ecran de choix est modal, comme la question.
        if (GameManager.instance != null) GameManager.instance.canClickOrHover = false;

        Debug.LogFormat("[Trivia] Choix du sujet : {0} {1}, {2} {3}, {4} {5}, {6} {7}.",
                        _offerKeys[0], _offerRewards[0], _offerKeys[1], _offerRewards[1],
                        _offerKeys[2], _offerRewards[2], _offerKeys[3], _offerRewards[3]);

        subjectPanel.Show(_offerKeys, _offerRewards, _offerDifficulties);
    }

    /// <summary>
    /// Compose l'offre du tour : une question tiree dans chaque sujet, et le gain
    /// que sa difficulte commande.
    ///
    /// Le tirage a lieu MAINTENANT, avant l'affichage, et c'est tout l'interet : le
    /// chiffre montre au joueur est celui de la question qu'il aura vraiment. Tirer
    /// apres le choix aurait permis d'annoncer +85 puis de poser une question facile,
    /// ou l'inverse - et le pari n'aurait plus voulu dire grand-chose.
    ///
    /// Renvoie false si aucun sujet n'a pu etre servi.
    /// </summary>
    private bool BuildTurnOffer()
    {
        int served = 0;

        for (int c = 0; c < 4; c++)
        {
            _offerKeys[c] = "";
            _offerRewards[c] = 0;
            _offerDifficulties[c] = 1;
            _offerBankIndex[c] = -1;

            List<int> all = _byCategory[c];
            if (all == null || all.Count == 0) continue;

            List<int> left = _remainingByCategory[c];

            // Sac vide : on le remplit a nouveau. Une partie longue doit pouvoir
            // reposer une question plutot que de perdre un sujet en cours de route.
            if (left.Count == 0)
            {
                for (int i = 0; i < all.Count; i++) left.Add(all[i]);
            }

            int pick = Random.Range(0, left.Count);
            int index = left[pick];
            left.RemoveAt(pick);

            TriviaQuestion q = _bank[index];
            int difficulty = q.difficulty;
            if (difficulty < 1) difficulty = 1;
            if (difficulty > 4) difficulty = 4;

            _offerKeys[c] = CategoryKeys[c];
            _offerDifficulties[c] = difficulty;
            _offerRewards[c] = InteractionRules.GetTriviaReward(difficulty);
            _offerBankIndex[c] = index;

            served++;
        }

        return served > 0;
    }

    /// <summary>Le joueur a choisi un sujet : on pose SA question, pas une autre.</summary>
    private void OnSubjectChosen(int slot)
    {
        if (subjectPanel != null) subjectPanel.Hide();

        if (slot < 0 || slot >= 4 || _offerBankIndex[slot] < 0)
        {
            AskIncomeQuestion();
            return;
        }

        _forcedIndex = _offerBankIndex[slot];
        _offerBankIndex[slot] = -1;

        Debug.LogFormat("[Trivia] Sujet choisi : {0}, palier {1}, gain {2}.",
                        CategoryKeys[slot], _offerDifficulties[slot], _offerRewards[slot]);

        AskQuestion(null);
    }

    public void AskQuestion(HexCoord hexCoord)
    {
        // Il faut au moins un support d'affichage : le panneau dedie, la modale
        // Reach, ou a defaut le simple champ TMP.
        if (panel == null && modalWindow == null && questionText == null)
        {
            Debug.LogError("[Trivia] ETAPE DE QUESTION SAUTEE : aucun support d'affichage. "
                         + "Ni le panneau dedie (panel), ni la modale Reach (modalWindow), ni le champ "
                         + "questionText ne sont assignes sur le TriviaManager. Le tour continue avec une "
                         + "bonne reponse simulee - relance Milchemet > Construire le HUD pour recabler.");
            Resolve(true);
            return;
        }

        _current = DrawQuestion();
        if (_current == null)
        {
            Debug.LogError("[Trivia] ETAPE DE QUESTION SAUTEE : la banque est vide. "
                         + "Assets/Resources/trivia_questions.json est introuvable, illisible, ou aucune "
                         + "question n'y est valide. Bonne reponse simulee pour ne pas bloquer le tour.");
            Resolve(true);
            return;
        }

        PendingHex = hexCoord;
        _showTrivia = true;

        // Le gain de CETTE question, deduit de son palier. C'est exactement le
        // chiffre qui a ete montre sur l'ecran de choix.
        _currentReward = InteractionRules.GetTriviaReward(_current.difficulty);
        _questionStartTime = Time.time;
        currentCategory = _current.category;

        // On verrouille le plateau pour que le clic sur l'UI ne traverse pas.
        if (GameManager.instance != null) GameManager.instance.canClickOrHover = false;

        string categoryLabel = (_currentIndex >= 0 && _categoryUpper != null && _currentIndex < _categoryUpper.Length)
            ? _categoryUpper[_currentIndex]
            : "TRIVIA";

        // --- Champ TMP dedie : c'est lui qui porte l'enonce ---
        if (questionText != null)
        {
            ApplyTextDirection(questionText, _current.question);
            questionText.text = _current.question;
        }

        if (categoryText != null)
        {
            categoryText.text = categoryLabel;
        }

        // --- Modale Reach : titre ET enonce, systematiquement ---
        // On remplit les deux supports au lieu de choisir : si le champ TMP n'est pas
        // assigne, pas visible ou mal place, la description de la modale prend le relais.
        if (modalWindow != null)
        {
            modalWindow.titleText = categoryLabel;
            modalWindow.descriptionText = _current.question;
            modalWindow.UpdateUI();
        }

        // Trace de controle : si cette ligne montre l'enonce mais que l'ecran ne l'affiche
        // pas, le probleme est dans l'UI (champ non assigne, police sans glyphes, etc.).
        Debug.LogFormat("[Trivia] {0} : {1}", categoryLabel, _current.question);

        // --- Panneau hebreu dedie : il remplace entierement la modale Reach ---
        // On lui passe la CLE latine de la categorie, pas le libelle : c'est lui
        // qui la traduit. Envoyer du latin dans un champ droite-a-gauche affiche
        // le mot a l'envers, et c'est exactement le bug "AHCALAH" vu en jeu.
        if (panel != null)
        {
            panel.Show(_current.category, _current.question, _current.answers, _currentReward);
            return;
        }

        if (answerButtons == null) { OpenModal(); return; }

        for (int i = 0; i < answerButtons.Length; i++)
        {
            if (answerButtons[i] == null) continue;

            bool hasAnswer = i < _current.answers.Length;
            answerButtons[i].gameObject.SetActive(hasAnswer);
            if (!hasAnswer) continue;

            answerButtons[i].buttonText = _current.answers[i];
            answerButtons[i].UpdateUI();
            // Les listeners sont deja branches une fois pour toutes dans Awake.
        }

        OpenModal();
    }

    private void OpenModal()
    {
        if (modalWindow != null) modalWindow.OpenWindow();
    }

    /// <summary>
    /// Bascule le champ en droite-a-gauche des que l'enonce contient un caractere
    /// hebreu ou arabe. Balayage sans allocation, une seule fois par question.
    /// </summary>
    private void ApplyTextDirection(TMPro.TMP_Text field, string content)
    {
        if (!autoDetectRightToLeft || field == null || string.IsNullOrEmpty(content)) return;

        bool rtl = false;
        for (int i = 0; i < content.Length; i++)
        {
            // Comparaison sur le code numerique : aucun caractere non-ASCII dans ce
            // fichier .cs, donc aucun risque de corruption a la sauvegarde.
            int code = content[i];

            // Hebreu : U+0590..U+05FF    Arabe : U+0600..U+06FF
            if ((code >= 0x0590 && code <= 0x05FF) || (code >= 0x0600 && code <= 0x06FF))
            {
                rtl = true;
                break;
            }
        }

        if (field.isRightToLeftText != rtl) field.isRightToLeftText = rtl;
    }

    private void Update()
    {
        if (!_showTrivia) return;

        float timeLeft = timeLimit - (Time.time - _questionStartTime);

        if (panel != null) panel.SetTimeRemaining(timeLeft, timeLimit);

        if (timerBar != null)
        {
            timerBar.currentValue = Mathf.Max(0f, timeLeft);
            timerBar.timerValue = timeLimit;
            timerBar.UpdateUI();
        }

        if (timeLeft <= 0) AnswerQuestion(-1);
    }

    // =====================================================================
    //  REPONSE
    // =====================================================================
    private void AnswerQuestion(int answerIndex)
    {
        if (!_showTrivia) return;

        _showTrivia = false;

        // Avec le panneau dedie, on montre d'abord le verdict. Fermer aussitot
        // priverait le joueur de la seule chose qu'il a gagnee en repondant :
        // savoir s'il avait raison.
        if (panel != null && panel.IsOpen)
        {
            bool correct = (_current != null && answerIndex == _current.correctIndex);
            LogAnswer(answerIndex, correct);
            panel.ShowResult(answerIndex, (_current != null) ? _current.correctIndex : -1);
            StartCoroutine(CloseThenResolve(correct));
            return;
        }

        if (modalWindow != null) modalWindow.CloseWindow();

        // Sans modale pour la masquer, le champ TMP resterait affiche entre deux questions.
        if (questionText != null) questionText.text = string.Empty;
        if (categoryText != null) categoryText.text = string.Empty;

        bool isCorrect = (_current != null && answerIndex == _current.correctIndex);
        LogAnswer(answerIndex, isCorrect);

        Resolve(isCorrect);
    }

    private void LogAnswer(int answerIndex, bool isCorrect)
    {
        if (answerIndex == -1) Debug.Log("[Trivia] Temps ecoule : aucun revenu de Trivia ce tour.");
        else if (isCorrect) Debug.LogFormat("[Trivia] Bonne reponse ({0}, palier {1}) : +{2} Energie.",
                                            currentCategory,
                                            (_current != null) ? _current.difficulty : 1,
                                            _currentReward);
        else Debug.Log("[Trivia] Mauvaise reponse : aucun revenu de Trivia ce tour.");
    }

    /// <summary>
    /// Laisse le verdict a l'ecran, puis ferme et rend la main. WaitForSecondsRealtime
    /// et non WaitForSeconds : si un jour une pause met Time.timeScale a zero, le
    /// panneau resterait bloque a l'ecran pour toujours.
    /// </summary>
    private IEnumerator CloseThenResolve(bool isCorrect)
    {
        yield return new WaitForSecondsRealtime(resultHoldSeconds);

        if (panel != null) panel.Hide();
        Resolve(isCorrect);
    }

    /// <summary>
    /// Verse la recompense puis rend la main au joueur. Le tour ne s'arrete plus ici :
    /// c'est maintenant que commence la phase de depense.
    /// </summary>
    private void Resolve(bool isCorrect)
    {
        if (isCorrect) InteractionRules.GrantTriviaReward(_currentReward);

        if (MNLTHII.Managers.TurnManager.Instance != null)
            MNLTHII.Managers.TurnManager.Instance.OpenSpendingPhase();
        else if (GameManager.instance != null)
            GameManager.instance.canClickOrHover = true;
    }
}
