using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using MNLTHII.Rules;

namespace MNLTHII.Managers
{
    /// <summary>Une question attachee a un enseignement. Facultative.</summary>
    public class TeachingQuestion
    {
        public string situation;
        public string[] answers;
        public int correct;
        public string explain;

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(situation)
                   && answers != null && answers.Length >= 2
                   && correct >= 0 && correct < answers.Length;
        }
    }

    /// <summary>Un enseignement : sa source, son titre, son texte.</summary>
    public class Teaching
    {
        public string source;
        public string title;
        public string text;
        public TeachingQuestion question;

        public bool IsValid() { return !string.IsNullOrEmpty(text); }
        public bool HasQuestion { get { return question != null && question.IsValid(); } }
    }

    /// <summary>
    /// Une consigne de jeu. Rien a voir avec un enseignement : ca explique comment on
    /// joue, pas ce qu'on affronte. Les deux ne doivent surtout pas se ressembler a
    /// l'ecran, sinon le joueur cesse de lire les deux.
    /// </summary>
    public class GameInstruction
    {
        public string title;
        public string text;

        /// <summary>Cle de couleur : gas, tank, danger, crack, attack.</summary>
        public string color;

        public bool IsValid() { return !string.IsNullOrEmpty(title); }
    }

    /// <summary>Une entree de legende : un pictogramme et ce qu'il designe.</summary>
    public class LegendEntry
    {
        /// <summary>Nom de IconKind, tel quel : Factory, Crystal, Tank...</summary>
        public string icon;
        public string name;

        public bool IsValid() { return !string.IsNullOrEmpty(icon) && !string.IsNullOrEmpty(name); }
    }

    public class TeachingBank
    {
        public Dictionary<string, string> ui;
        public List<Teaching> teachings;
        public List<GameInstruction> instructions;
        public List<LegendEntry> legend;
    }

    /// <summary>
    /// LES ENSEIGNEMENTS.
    ///
    /// Ce qui remplace la question de Trivia en ouverture de tour.
    ///
    /// POURQUOI LE CHANGEMENT
    ///
    /// La question etait un peage : chaque tour, avant de pouvoir jouer, il fallait
    /// repondre pour avoir le droit de depenser. Un peage devient penible au cinquieme
    /// tour, meme quand les questions sont bonnes - et l'Energie qu'il versait ne se
    /// trouvait nulle part sur la carte, donc rien ne pouvait la menacer.
    ///
    /// Maintenant l'Energie vient des usines, et l'ouverture du tour sert a autre
    /// chose : on y apprend quelque chose sur ce qu'on affronte. Deux enseignements
    /// par tour, courts, avec leur source. Et parfois - pas toujours - l'un d'eux porte
    /// une question.
    ///
    /// LA RECOMPENSE, ET POURQUOI CELLE-LA
    ///
    /// Une bonne reponse ne donne pas d'Energie : elle donne UN SADAK, une fissure, sur
    /// le Shofar le plus proche de ceder. C'est la meme jauge que celle qu'on remplit
    /// en tuant ses emissaires - donc comprendre le Yetzer Hara l'ebranle exactement
    /// comme le combattre. C'est le seul endroit du jeu ou le contenu et la mecanique
    /// disent la meme chose.
    ///
    /// Une mauvaise reponse ne punit pas. L'enseignement a ete lu, c'est deja ca -
    /// sinon on aurait juste remplace un peage par un autre.
    ///
    /// Note d'optimisation : la banque est lue une fois au demarrage. Le tirage se fait
    /// dans un sac melange sans remise, pour qu'on ne revoie pas deux fois le meme
    /// enseignement avant d'avoir fait le tour - un tableau d'index reutilise, aucune
    /// allocation par tour.
    /// </summary>
    public class TeachingManager : MonoBehaviour
    {
        public static TeachingManager Instance;

        [Header("Source")]
        [Tooltip("Nom du fichier dans Resources, sans l'extension.")]
        public string resourceName = "teachings";

        [Header("Rythme")]
        [Tooltip("Combien d'enseignements par tour.")]
        [Range(1, 4)] public int teachingsPerTurn = 2;

        [Tooltip("Sur combien de tours une question est posee. 2 = un tour sur deux.")]
        [Range(1, 6)] public int questionEveryTurns = 2;

        [Header("Apprendre a jouer")]
        /// <summary>
        /// Tours pendant lesquels TOUTES les consignes sont affichees. C'est le
        /// tutoriel : le joueur voit la boucle complete du jeu en cinq lignes.
        /// </summary>
        [Range(0, 5)] public int briefingTurns = 2;

        /// <summary>
        /// Jusqu'a quel tour on rappelle UNE consigne, en tournant. Passe ce tour,
        /// silence complet : un jeu qui explique encore au tour quinze est un jeu qui
        /// n'a pas su se faire comprendre.
        /// </summary>
        [Range(0, 30)] public int reminderUntilTurn = 8;

        [Header("Diagnostic")]
        public int loadedCount = 0;

        // =================================================================
        //  ETAT
        // =================================================================
        private readonly List<Teaching> _bank = new List<Teaching>(32);
        private readonly List<GameInstruction> _instructions = new List<GameInstruction>(8);
        private readonly List<LegendEntry> _legend = new List<LegendEntry>(12);

        /// <summary>Sac d'index melange : on ne repete pas avant d'avoir tout vu.</summary>
        private int[] _bag;
        private int _bagCursor;

        private readonly Dictionary<string, string> _ui = new Dictionary<string, string>(16);

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(this); return; }

            Load();
        }

        // =================================================================
        //  CHARGEMENT
        // =================================================================
        private void Load()
        {
            _bank.Clear();
            _ui.Clear();
            _instructions.Clear();
            _legend.Clear();

            TextAsset asset = Resources.Load<TextAsset>(resourceName);

            if (asset == null)
            {
                Debug.LogErrorFormat("[Enseignements] {0}.json est introuvable dans Resources.", resourceName);
                return;
            }

            try
            {
                TeachingBank bank = JsonConvert.DeserializeObject<TeachingBank>(asset.text);

                if (bank != null && bank.teachings != null)
                {
                    for (int i = 0; i < bank.teachings.Count; i++)
                    {
                        Teaching teaching = bank.teachings[i];
                        if (teaching != null && teaching.IsValid()) _bank.Add(teaching);
                    }
                }

                if (bank != null && bank.ui != null)
                {
                    foreach (KeyValuePair<string, string> pair in bank.ui) _ui[pair.Key] = pair.Value;
                }

                if (bank != null && bank.instructions != null)
                {
                    for (int i = 0; i < bank.instructions.Count; i++)
                    {
                        GameInstruction step = bank.instructions[i];
                        if (step != null && step.IsValid()) _instructions.Add(step);
                    }
                }

                if (bank != null && bank.legend != null)
                {
                    for (int i = 0; i < bank.legend.Count; i++)
                    {
                        LegendEntry entry = bank.legend[i];
                        if (entry != null && entry.IsValid()) _legend.Add(entry);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogErrorFormat("[Enseignements] Lecture de {0}.json impossible : {1}", resourceName, e.Message);
                return;
            }

            loadedCount = _bank.Count;
            BuildBag();

            Debug.LogFormat("[Enseignements] {0} enseignements charges, dont {1} avec une question.",
                            _bank.Count, CountWithQuestion());
        }

        private int CountWithQuestion()
        {
            int count = 0;
            for (int i = 0; i < _bank.Count; i++) if (_bank[i].HasQuestion) count++;
            return count;
        }

        /// <summary>Texte d'interface, lu dans la section "ui" du fichier.</summary>
        public string Label(string key)
        {
            string value;
            return _ui.TryGetValue(key, out value) ? value : "";
        }

        // =================================================================
        //  TIRAGE
        // =================================================================
        private void BuildBag()
        {
            if (_bank.Count == 0) { _bag = null; return; }

            if (_bag == null || _bag.Length != _bank.Count) _bag = new int[_bank.Count];

            for (int i = 0; i < _bag.Length; i++) _bag[i] = i;

            // Melange en place : aucune allocation.
            for (int i = 0; i < _bag.Length; i++)
            {
                int j = Random.Range(i, _bag.Length);
                int tmp = _bag[i];
                _bag[i] = _bag[j];
                _bag[j] = tmp;
            }

            _bagCursor = 0;
        }

        private Teaching Draw()
        {
            if (_bank.Count == 0) return null;
            if (_bag == null || _bagCursor >= _bag.Length) BuildBag();
            if (_bag == null) return null;

            return _bank[_bag[_bagCursor++]];
        }

        /// <summary>
        /// Tire les enseignements du tour et dit lequel portera la question.
        ///
        /// La question n'est pas systematique : c'est ce qui la garde interessante.
        /// Un tour sur questionEveryTurns, et seulement si l'un des enseignements tires
        /// en porte une.
        /// </summary>
        public int DrawTurn(Teaching[] output, int turn, out int questionIndex)
        {
            questionIndex = -1;
            if (output == null || _bank.Count == 0) return 0;

            int wanted = teachingsPerTurn;
            if (wanted > output.Length) wanted = output.Length;

            int count = 0;
            for (int i = 0; i < wanted; i++)
            {
                Teaching teaching = Draw();
                if (teaching == null) break;
                output[count++] = teaching;
            }

            if (count == 0) return 0;

            int every = (questionEveryTurns < 1) ? 1 : questionEveryTurns;
            if (turn % every != 0) return count;

            for (int i = 0; i < count; i++)
            {
                if (output[i].HasQuestion) { questionIndex = i; break; }
            }

            return count;
        }

        // =================================================================
        //  LES CONSIGNES DE JEU
        // =================================================================
        /// <summary>
        /// Remplit output avec les consignes a montrer ce tour-ci, et retourne combien.
        ///
        /// Trois regimes, dans cet ordre :
        ///   - les premiers tours : TOUT, c'est le tutoriel ;
        ///   - ensuite : UNE consigne, qui tourne, comme un rappel ;
        ///   - plus tard : rien du tout.
        ///
        /// Le dernier regime est le plus important. Une aide qui ne s'arrete jamais
        /// finit par etre sautee sans etre lue, et elle emporte avec elle l'attention
        /// que les enseignements meritent.
        /// </summary>
        public int DrawInstructions(GameInstruction[] output, int turn)
        {
            if (output == null || _instructions.Count == 0) return 0;

            if (turn <= briefingTurns)
            {
                int count = _instructions.Count;
                if (count > output.Length) count = output.Length;

                for (int i = 0; i < count; i++) output[i] = _instructions[i];
                return count;
            }

            if (turn > reminderUntilTurn) return 0;

            // Une seule, qui tourne. turn - briefingTurns pour commencer par la premiere.
            int index = (turn - briefingTurns - 1) % _instructions.Count;
            if (index < 0) index = 0;

            output[0] = _instructions[index];
            return 1;
        }

        /// <summary>La legende des pictogrammes, dans l'ordre du fichier.</summary>
        public int FillLegend(LegendEntry[] output)
        {
            if (output == null) return 0;

            int count = _legend.Count;
            if (count > output.Length) count = output.Length;

            for (int i = 0; i < count; i++) output[i] = _legend[i];
            return count;
        }

        // =================================================================
        //  LA RECOMPENSE
        // =================================================================
        /// <summary>
        /// Une bonne reponse fissure le Shofar le plus proche de ceder.
        ///
        /// On vise le plus fissure, et pas un au hasard : c'est le choix que le joueur
        /// ferait lui-meme, et ca lui evite un ecran de selection de plus a l'ouverture
        /// du tour. Retourne le Shofar touche, ou null s'il n'y en a plus.
        /// </summary>
        public Hexagon GrantCrack()
        {
            PortalManager portals = PortalManager.Instance;
            BoardController board = BoardController.instance;
            if (portals == null || board == null || board.HexagonsInBoard == null) return null;

            List<Hexagon> hexes = board.HexagonsInBoard;

            Hexagon best = null;
            int bestCracks = -1;

            for (int i = 0; i < hexes.Count; i++)
            {
                Hexagon hex = hexes[i];
                if (hex == null || hex.type != TypeOfHex.portal || hex.currentHP <= 0) continue;

                // Un Shofar deja sonne n'a pas besoin d'une fissure de plus : sa fenetre
                // est ouverte, la fissure serait perdue.
                if (portals.IsShieldDown(hex)) continue;

                int cracks = portals.GetInstability(hex);
                if (cracks <= bestCracks) continue;

                best = hex;
                bestCracks = cracks;
            }

            if (best == null) return null;

            portals.AddInstability(best, 1);
            return best;
        }
    }
}
