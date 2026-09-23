using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MNLTHII.Data;

namespace MNLTHII.Managers
{
    /// <summary>
    /// LA LISTE DES PARTIES A REPRENDRE, dans le menu principal.
    ///
    /// Le menu ne proposait qu'une chose : recommencer. Or une partie dure des
    /// dizaines de tours, et personne n'a envie de rejouer vingt tours pour revoir le
    /// vingt-et-unieme. Cet ecran montre les parties NON ACHEVEES - une ligne par
    /// partie, avec son tour, sa difficulte et ce qu'il en reste - et il suffit de
    /// cliquer une ligne pour y retourner.
    ///
    /// Tout est construit EN CODE, sur son propre Canvas. C'est volontaire : la scene
    /// du menu est produite par un script d'editeur, et il aurait fallu la
    /// reconstruire pour voir apparaitre le moindre bouton. Ici, l'ecran existe des
    /// que le jeu tourne.
    ///
    /// Les libelles hebreux viennent de Resources/hud_labels.json (section saves) :
    /// ce fichier .cs reste en pur ASCII.
    ///
    /// Note d'optimisation : le panneau est construit au premier affichage puis
    /// reutilise ; entre deux ouvertures, rien ne tourne (aucun Update).
    /// </summary>
    public class LoadGamePanel : MonoBehaviour
    {
        public static LoadGamePanel Instance;

        private const int MaxRows = SaveManager.MaxSlots;

        private static readonly Color ColGold = new Color(1f, 0.78f, 0.36f);
        private static readonly Color ColText = new Color(0.90f, 0.94f, 1f);
        private static readonly Color ColDim = new Color(0.62f, 0.70f, 0.86f);
        private static readonly Color ColRow = new Color(0.49f, 0.63f, 1f, 0.09f);
        private static readonly Color ColRowHot = new Color(1f, 0.78f, 0.36f, 0.18f);
        private static readonly Color ColDanger = new Color(1f, 0.30f, 0.37f);

        private GameObject _canvasObject;
        private RectTransform _list;
        private TMPro.TextMeshProUGUI _emptyText;
        private TMPro.TMP_FontAsset _font;
        private bool _built;
        private bool _loading;

        private readonly List<SaveGameFile> _files = new List<SaveGameFile>(MaxRows);
        private readonly List<RectTransform> _rows = new List<RectTransform>(MaxRows);
        private readonly List<TMPro.TextMeshProUGUI[]> _rowTexts = new List<TMPro.TextMeshProUGUI[]>(MaxRows);

        // =================================================================
        //  API
        // =================================================================
        public static void Open()
        {
            LoadGamePanel panel = Instance;
            if (panel == null)
            {
                GameObject go = new GameObject("LoadGamePanel (auto)");
                panel = go.AddComponent<LoadGamePanel>();
            }
            panel.Show();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Show()
        {
            if (!Build()) return;

            Refresh();
            _canvasObject.SetActive(true);
        }

        public void Hide()
        {
            if (_canvasObject != null) _canvasObject.SetActive(false);
        }

        // =================================================================
        //  CONTENU
        // =================================================================
        private void Refresh()
        {
            _files.Clear();
            List<SaveGameFile> found = SaveManager.List();
            for (int i = 0; i < found.Count && i < MaxRows; i++) _files.Add(found[i]);

            if (_emptyText != null) _emptyText.gameObject.SetActive(_files.Count == 0);

            for (int i = 0; i < _rows.Count; i++)
            {
                bool used = (i < _files.Count);
                _rows[i].gameObject.SetActive(used);
                if (!used) continue;

                SaveGameFile file = _files[i];
                TMPro.TextMeshProUGUI[] texts = _rowTexts[i];

                // Ligne 1 : le tour et la difficulte. C'est ce qui identifie une partie.
                texts[0].text = string.Format(Label("savesHeadFormat", "{0} - {1}"),
                                              file.turn, DifficultyName(file.difficulty));

                // Ligne 2 : ce qu'il reste sur le plateau. Deux parties au meme tour ne
                // se ressemblent pas ; celle ou il reste deux Shofars n'est pas celle ou
                // il en reste six.
                texts[1].text = string.Format(Label("savesDetailFormat", "{0} / {1} / {2}-{3}"),
                                              file.tanks, file.foes, file.portalsAlive, file.portalsTotal);

                // Ligne 3 : la date. En chiffres, donc jamais en RTL.
                texts[2].text = file.savedAt;
            }
        }

        private static string DifficultyName(int difficulty)
        {
            switch (difficulty)
            {
                case 0: return UI.HudLabelsRuntime.Get("menuEasy", "Easy");
                case 2: return UI.HudLabelsRuntime.Get("menuHard", "Hard");
                default: return UI.HudLabelsRuntime.Get("menuMedium", "Medium");
            }
        }

        private static string Label(string key, string fallback)
        {
            return UI.HudLabelsRuntime.Get(key, fallback);
        }

        // =================================================================
        //  ACTIONS
        // =================================================================
        private void Continue(int index)
        {
            if (_loading) return;
            if (index < 0 || index >= _files.Count) return;

            SaveGameFile file = _files[index];
            if (file == null) return;

            _loading = true;

            // La scene de jeu la trouvera ici : c'est LocalGameEngine qui la consomme.
            SaveManager.PendingLoad = file;
            Rules.GameDifficulty.Current = (Rules.DifficultyLevel)file.difficulty;
            Time.timeScale = 1f;

            string scene = (MainMenuController.Instance != null)
                ? MainMenuController.Instance.gameSceneName : "Game";

            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogErrorFormat("[Menu] La scene '{0}' n'est pas dans la liste du build.", scene);
                SaveManager.PendingLoad = null;
                _loading = false;
                return;
            }

            UnityEngine.SceneManagement.SceneManager.LoadScene(scene);
        }

        private void Forget(int index)
        {
            if (index < 0 || index >= _files.Count) return;

            SaveGameFile file = _files[index];
            if (file == null) return;

            SaveManager.Delete(file.id);
            Refresh();

            // Plus rien a reprendre : le bouton du menu doit s'eteindre lui aussi.
            if (MainMenuController.Instance != null) MainMenuController.Instance.RefreshLoadButton();
        }

        // =================================================================
        //  CONSTRUCTION
        // =================================================================
        private bool Build()
        {
            if (_built) return _canvasObject != null;
            _built = true;

            _font = FindFont();

            _canvasObject = new GameObject("Parties sauvegardees",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasObject.transform.SetParent(transform, false);

            Canvas canvas = _canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;   // au-dessus du menu

            CanvasScaler scaler = _canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform root = (RectTransform)_canvasObject.transform;

            // Le voile : il assombrit le menu et ferme le panneau quand on clique a cote.
            Image dim = NewImage("Voile", root, new Color(0.01f, 0.02f, 0.05f, 0.88f));
            Stretch(dim.rectTransform);
            dim.raycastTarget = true;
            Button dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(Hide);

            RectTransform panel = NewRect("Panneau", root);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(1180f, 860f);

            Image back = panel.gameObject.AddComponent<Image>();
            back.color = new Color(0.04f, 0.06f, 0.12f, 0.97f);
            back.raycastTarget = true;   // absorbe les clics : ils ne ferment pas le panneau

            TMPro.TextMeshProUGUI title = NewText("Titre", panel, Label("savesTitle", ""), 56f, ColGold, true);
            Place(title.rectTransform, new Vector2(0f, 360f), new Vector2(1060f, 80f));
            title.fontStyle = TMPro.FontStyles.Bold;

            TMPro.TextMeshProUGUI hint = NewText("Indice", panel, Label("savesHint", ""), 28f, ColDim, true);
            Place(hint.rectTransform, new Vector2(0f, 302f), new Vector2(1060f, 46f));

            _emptyText = NewText("Vide", panel, Label("savesEmpty", ""), 34f, ColDim, true);
            Place(_emptyText.rectTransform, new Vector2(0f, 40f), new Vector2(1000f, 60f));

            _list = NewRect("Liste", panel);
            Place(_list, new Vector2(0f, 240f), new Vector2(10f, 10f));

            for (int i = 0; i < MaxRows; i++) BuildRow(i);

            // Fermer.
            RectTransform closeRect = NewRect("Fermer", panel);
            Place(closeRect, new Vector2(0f, -368f), new Vector2(320f, 74f));

            Image closeBack = closeRect.gameObject.AddComponent<Image>();
            closeBack.color = ColRow;

            Button close = closeRect.gameObject.AddComponent<Button>();
            close.targetGraphic = closeBack;
            close.onClick.AddListener(Hide);

            TMPro.TextMeshProUGUI closeLabel = NewText("Libelle", closeRect, Label("savesClose", ""), 32f, ColText, true);
            Stretch(closeLabel.rectTransform);

            return true;
        }

        private void BuildRow(int index)
        {
            RectTransform row = NewRect("Partie_" + index, _list);
            Place(row, new Vector2(0f, -index * 95f), new Vector2(1020f, 84f));

            Image back = row.gameObject.AddComponent<Image>();
            back.color = ColRow;

            Button button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = back;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.6f, 1.6f, 1.6f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            button.colors = colors;

            int slot = index;
            button.onClick.AddListener(delegate { Continue(slot); });

            // Un lisere dore a droite : en hebreu, c'est le bord ou commence la lecture.
            Image edge = NewImage("Lisere", row, ColRowHot);
            RectTransform edgeRect = edge.rectTransform;
            edgeRect.anchorMin = new Vector2(1f, 0f);
            edgeRect.anchorMax = new Vector2(1f, 1f);
            edgeRect.pivot = new Vector2(1f, 0.5f);
            edgeRect.anchoredPosition = Vector2.zero;
            edgeRect.sizeDelta = new Vector2(6f, 0f);
            edge.raycastTarget = false;

            TMPro.TextMeshProUGUI head = NewText("Tour", row, "", 38f, ColGold, true);
            RectTransform headRect = head.rectTransform;
            headRect.anchorMin = new Vector2(1f, 0.5f);
            headRect.anchorMax = new Vector2(1f, 0.5f);
            headRect.pivot = new Vector2(1f, 0.5f);
            headRect.anchoredPosition = new Vector2(-32f, 17f);
            headRect.sizeDelta = new Vector2(640f, 42f);
            head.alignment = TMPro.TextAlignmentOptions.Right;
            head.fontStyle = TMPro.FontStyles.Bold;

            TMPro.TextMeshProUGUI detail = NewText("Detail", row, "", 26f, ColDim, true);
            RectTransform detailRect = detail.rectTransform;
            detailRect.anchorMin = new Vector2(1f, 0.5f);
            detailRect.anchorMax = new Vector2(1f, 0.5f);
            detailRect.pivot = new Vector2(1f, 0.5f);
            detailRect.anchoredPosition = new Vector2(-32f, -20f);
            detailRect.sizeDelta = new Vector2(640f, 38f);
            detail.alignment = TMPro.TextAlignmentOptions.Right;

            // La date : des chiffres, donc jamais en RTL - sinon 23/09 s'ecrit 90/32.
            TMPro.TextMeshProUGUI when = NewText("Date", row, "", 26f, ColDim, false);
            RectTransform whenRect = when.rectTransform;
            whenRect.anchorMin = new Vector2(0f, 0.5f);
            whenRect.anchorMax = new Vector2(0f, 0.5f);
            whenRect.pivot = new Vector2(0f, 0.5f);
            whenRect.anchoredPosition = new Vector2(120f, 0f);
            whenRect.sizeDelta = new Vector2(240f, 40f);
            when.alignment = TMPro.TextAlignmentOptions.Left;

            // Effacer cette partie. A gauche, loin du reste : on ne l'atteint pas par
            // megarde en visant la ligne.
            RectTransform killRect = NewRect("Effacer", row);
            killRect.anchorMin = new Vector2(0f, 0.5f);
            killRect.anchorMax = new Vector2(0f, 0.5f);
            killRect.pivot = new Vector2(0f, 0.5f);
            killRect.anchoredPosition = new Vector2(18f, 0f);
            killRect.sizeDelta = new Vector2(84f, 62f);

            Image killBack = killRect.gameObject.AddComponent<Image>();
            killBack.color = new Color(1f, 0.30f, 0.37f, 0.16f);

            Button kill = killRect.gameObject.AddComponent<Button>();
            kill.targetGraphic = killBack;
            kill.onClick.AddListener(delegate { Forget(slot); });

            TMPro.TextMeshProUGUI killLabel = NewText("X", killRect, "X", 30f, ColDanger, false);
            Stretch(killLabel.rectTransform);
            killLabel.fontStyle = TMPro.FontStyles.Bold;

            row.gameObject.SetActive(false);

            _rows.Add(row);
            _rowTexts.Add(new TMPro.TextMeshProUGUI[] { head, detail, when });
        }

        // =================================================================
        //  PETITS OUTILS
        // =================================================================
        private static TMPro.TMP_FontAsset FindFont()
        {
            TMPro.TextMeshProUGUI sample = Object.FindFirstObjectByType<TMPro.TextMeshProUGUI>();
            return (sample != null) ? sample.font : null;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            RectTransform rect = NewRect(name, parent);
            Image img = rect.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private TMPro.TextMeshProUGUI NewText(string name, Transform parent, string content,
                                              float size, Color color, bool rightToLeft)
        {
            RectTransform rect = NewRect(name, parent);

            TMPro.TextMeshProUGUI text = rect.gameObject.AddComponent<TMPro.TextMeshProUGUI>();
            if (_font != null) text.font = _font;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.isRightToLeftText = rightToLeft;
            text.raycastTarget = false;
            text.enableWordWrapping = false;

            // Crenage coupe : en hebreu, TMP decale les lettres du mauvais cote.
            UI.TextFeatures.Disable(text);
            return text;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
