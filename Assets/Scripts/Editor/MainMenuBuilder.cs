using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;
using Newtonsoft.Json.Linq;
using MNLTHII.Managers;

namespace MNLTHII.EditorTools
{
    /// <summary>
    /// Construit la scene du MENU PRINCIPAL, et la met en premier dans le build.
    ///
    /// Menu : Milchemet > Construire le menu principal
    ///
    /// Ce qu'il fait :
    ///   - propose d'enregistrer la scene ouverte, puis cree une scene neuve ;
    ///   - y pose une camera, un EventSystem, un Canvas 1920 x 1080 ;
    ///   - le titre, trois cartes de difficulte (Facile / Moyen / Difficile, en
    ///     miroir pour l'hebreu : Facile a droite) et le bouton "Nouvelle partie" ;
    ///   - ajoute MainMenuController et lui glisse toutes les references ;
    ///   - enregistre Assets/Scenes/MainMenu.unity ;
    ///   - met MainMenu en PREMIER dans la liste des scenes du build, et s'assure que
    ///     Game.unity y est aussi.
    ///
    /// Relancable sans risque : la scene du menu est simplement reecrite. La scene
    /// de jeu n'est jamais modifiee.
    ///
    /// Les libelles viennent de hud_labels.json, section mainMenu. Le .cs reste en
    /// pur ASCII.
    /// </summary>
    public static class MainMenuBuilder
    {
        private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string GameScenePath = "Assets/Scenes/Game.unity";

        private static readonly Color ColBackground = new Color32(0x06, 0x0A, 0x14, 0xFF);
        private static readonly Color ColText = new Color32(0xE6, 0xEC, 0xFF, 0xFF);
        private static readonly Color ColDim = new Color32(0x9A, 0xA6, 0xC8, 0xFF);
        private static readonly Color ColGold = new Color32(0xFF, 0xC6, 0x5C, 0xFF);
        private static readonly Color ColButton = new Color32(0xFF, 0xC6, 0x5C, 0x33);

        private static TMP_FontAsset _font;

        [MenuItem("Milchemet/Construire le menu principal", false, 11)]
        public static void Build()
        {
            // Rien n'est perdu : la scene ouverte est enregistree (si on le veut) avant
            // qu'on en cree une nouvelle.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            JObject labels = HudBuilder.LoadLabels();
            if (labels == null) return;
            JObject mm = labels["mainMenu"] as JObject;
            if (mm == null)
                Debug.LogWarning("[MainMenuBuilder] Section mainMenu absente de hud_labels.json : textes vides.");

            _font = HudBuilder.FindHebrewFont();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- camera ---
            GameObject camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            Camera cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = ColBackground;
            cam.orthographic = true;

            // --- EventSystem (meme module d'entree que le HUD du jeu) ---
            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));

            // --- Canvas ---
            GameObject canvasGo = new GameObject("Menu Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform root = NewRect("Menu", canvasGo.transform);
            Stretch(root);

            Image bg = root.gameObject.AddComponent<Image>();
            bg.color = ColBackground;
            bg.raycastTarget = false;

            MainMenuController ctrl = canvasGo.AddComponent<MainMenuController>();

            // --- titre ---
            TextMeshProUGUI title = NewText("Titre", root, HudBuilder.S(mm, "menuTitle"), 110f, ColGold, true);
            Place(title.rectTransform, new Vector2(0f, 330f), new Vector2(1600f, 160f));
            title.fontStyle = FontStyles.Bold;
            ctrl.titleText = title;

            TextMeshProUGUI subtitle = NewText("SousTitre", root, HudBuilder.S(mm, "menuSubtitle"), 44f, ColDim, true);
            Place(subtitle.rectTransform, new Vector2(0f, 200f), new Vector2(1400f, 70f));
            ctrl.subtitleText = subtitle;

            // --- trois cartes. En RTL on lit de droite a gauche : Facile a droite. ---
            string[] nameKeys = { "menuEasy", "menuMedium", "menuHard" };
            string[] bodyKeys = { "menuEasyBody", "menuMediumBody", "menuHardBody" };
            float[] xs = { 470f, 0f, -470f };

            ctrl.difficultyButtons = new Button[3];
            ctrl.difficultyBackgrounds = new Image[3];
            ctrl.difficultyOutlines = new UnityEngine.UI.Outline[3];
            ctrl.difficultyNames = new TextMeshProUGUI[3];
            ctrl.difficultyBodies = new TextMeshProUGUI[3];

            for (int i = 0; i < 3; i++)
            {
                RectTransform card = NewRect("Carte_" + nameKeys[i], root);
                Place(card, new Vector2(xs[i], -20f), new Vector2(420f, 300f));

                Image cardBg = card.gameObject.AddComponent<Image>();
                cardBg.color = ctrl.idleColor;

                UnityEngine.UI.Outline outline = card.gameObject.AddComponent<UnityEngine.UI.Outline>();
                outline.effectDistance = new Vector2(4f, -4f);
                outline.enabled = false;

                Button button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = cardBg;
                ColorBlock colors = button.colors;
                colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
                colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
                button.colors = colors;

                TextMeshProUGUI name = NewText("Nom", card, HudBuilder.S(mm, nameKeys[i]), 62f, ColText, true);
                Place(name.rectTransform, new Vector2(0f, 85f), new Vector2(380f, 90f));
                name.fontStyle = FontStyles.Bold;

                TextMeshProUGUI body = NewText("Texte", card, HudBuilder.S(mm, bodyKeys[i]), 30f, ColDim, true);
                Place(body.rectTransform, new Vector2(0f, -45f), new Vector2(370f, 160f));
                body.enableWordWrapping = true;
                body.alignment = TextAlignmentOptions.Top;

                ctrl.difficultyButtons[i] = button;
                ctrl.difficultyBackgrounds[i] = cardBg;
                ctrl.difficultyOutlines[i] = outline;
                ctrl.difficultyNames[i] = name;
                ctrl.difficultyBodies[i] = body;
            }

            // --- nouvelle partie ---
            RectTransform play = NewRect("NouvellePartie", root);
            Place(play, new Vector2(0f, -330f), new Vector2(560f, 120f));
            Image playBg = play.gameObject.AddComponent<Image>();
            playBg.color = ColButton;
            UnityEngine.UI.Outline playOutline = play.gameObject.AddComponent<UnityEngine.UI.Outline>();
            playOutline.effectColor = ColGold;
            playOutline.effectDistance = new Vector2(3f, -3f);
            Button playButton = play.gameObject.AddComponent<Button>();
            playButton.targetGraphic = playBg;

            TextMeshProUGUI playText = NewText("Texte", play, HudBuilder.S(mm, "menuNewGame"), 56f, ColGold, true);
            Stretch(playText.rectTransform);
            playText.fontStyle = FontStyles.Bold;

            ctrl.newGameButton = playButton;
            ctrl.newGameText = playText;

            // --- enregistrement ---
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, MenuScenePath))
            {
                Debug.LogErrorFormat("[MainMenuBuilder] Impossible d'enregistrer {0}.", MenuScenePath);
                return;
            }

            RegisterInBuild();

            Debug.Log("[MainMenuBuilder] Menu principal construit (" + MenuScenePath + ") et place en premier "
                    + "dans le build. Pour revenir au jeu : ouvre Assets/Scenes/Game.unity.");
        }

        /// <summary>MainMenu en premier, Game ensuite, les autres scenes gardees derriere.</summary>
        private static void RegisterInBuild()
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            scenes.RemoveAll(s => s.path == MenuScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(MenuScenePath, true));

            bool hasGame = false;
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path != GameScenePath) continue;
                hasGame = true;
                scenes[i].enabled = true;
            }
            if (!hasGame) scenes.Insert(1, new EditorBuildSettingsScene(GameScenePath, true));

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // =================================================================
        //  OUTILS
        // =================================================================
        private static RectTransform NewRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Centre ancre au milieu de l'ecran, position et taille en pixels de reference.</summary>
        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, string content,
                                               float size, Color color, bool rightToLeft)
        {
            RectTransform rect = NewRect(name, parent);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();

            if (_font != null) text.font = _font;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.isRightToLeftText = rightToLeft;
            text.raycastTarget = false;
            text.enableWordWrapping = false;

            HudBuilder.DisableFontFeatures(text);
            return text;
        }
    }
}
