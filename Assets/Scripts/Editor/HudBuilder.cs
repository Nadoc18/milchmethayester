using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using Newtonsoft.Json.Linq;
using MNLTHII.Rules;
using MNLTHII.Managers;

namespace MNLTHII.EditorTools
{
    /// <summary>
    /// Construit toute la hierarchie du HUD en un coup, et la cable.
    ///
    /// Menu : Milchemet > Construire le HUD
    ///
    /// Ce qu'il fait :
    ///   - cree (ou reutilise) un Canvas correctement regle ;
    ///   - pose le bandeau permanent, en miroir pour l'hebreu : energie a droite,
    ///     tour et rail au centre, objectifs a gauche, actions en bas ;
    ///   - pose le panneau d'etat d'un Shofar et l'info-bulle de cout ;
    ///   - pose le panneau de question (phase 1) et la carte d'unite, commune aux
    ///     phases 3 et 4 ;
    ///   - ajoute HudController, PortalPanelController, HexTooltipController,
    ///     TriviaPanelController et UnitActionCard, et glisse TOUTES les
    ///     references a ta place - y compris le lien vers le TriviaManager ;
    ///   - lit les libelles hebreux dans Assets/Resources/hud_labels.json ;
    ///   - met Font Features sur "Nothing" sur chaque texte : le crenage de TMP
    ///     est concu pour un flux gauche-droite et deforme l'hebreu.
    ///
    /// L'HEBREU NE VIT QUE DANS LE JSON. Un fichier .cs qui contient de l'hebreu
    /// finit un jour re-enregistre dans un autre encodage et le projet ne compile
    /// plus : c'est exactement ce qui est arrive a GameCanvasController.cs, reste
    /// en Windows-1252. Ici le .cs est en pur ASCII et le texte arrive du JSON.
    ///
    /// Relancable sans risque : l'ancien HUD est supprime et reconstruit. Modifie
    /// le JSON, relance, et tes libelles sont a jour.
    /// </summary>
    public static class HudBuilder
    {
        private const string RootName = "HUD";
        private const string CanvasName = "HUD Canvas";
        private const int SortingOrder = 5;
        private const string LabelsPath = "Assets/Resources/hud_labels.json";

        // Reference 1920 x 1080. Les valeurs des maquettes (1440 x 900) sont
        // transposees au prorata.
        private const float RefWidth = 1920f;
        private const float RefHeight = 1080f;

        // --- palette, identique aux maquettes ---
        private static readonly Color ColText = new Color32(0xE6, 0xEC, 0xFF, 0xFF);
        private static readonly Color ColDim = new Color32(0x7E, 0x8C, 0xB0, 0xFF);
        private static readonly Color ColGold = new Color32(0xFF, 0xC6, 0x5C, 0xFF);
        private static readonly Color ColCyan = new Color32(0x3D, 0xE0, 0xD0, 0xFF);
        private static readonly Color ColOrange = new Color32(0xFF, 0x6B, 0x4A, 0xFF);
        private static readonly Color ColRed = new Color32(0xFF, 0x4D, 0x5E, 0xFF);
        private static readonly Color ColPanel = new Color32(0x0A, 0x0F, 0x1C, 0xE0);
        private static readonly Color ColLine = new Color32(0x7E, 0xA0, 0xFF, 0x24);
        private static readonly Color ColIdle = new Color32(0x5D, 0x6A, 0x8C, 0xFF);

        // =================================================================
        //  LISIBILITE
        // =================================================================
        /// <summary>
        /// Multiplicateur applique a TOUTES les tailles de texte du HUD.
        ///
        /// Les tailles d'origine ont ete posees pour un ecran de bureau a 1920 de
        /// large. Sur un telephone, le meme rapport donne des libelles qu'on ne lit
        /// pas : la surface est physiquement dix fois plus petite, et l'oeil est a
        /// trente centimetres, pas a soixante.
        ///
        /// Une molette unique plutot que soixante nombres a retoucher : une valeur a
        /// changer ici, tout suit, et rien ne peut se desaccorder au passage.
        /// </summary>
        private const float TextScale = 1.35f;

        /// <summary>
        /// Plancher absolu, applique APRES le multiplicateur.
        ///
        /// Les plus petits libelles - les notes de bas de panneau, les unites - etaient
        /// a 12 ou 13. Multiplies par 1.35 ils restent sous 18, et sous 18 un texte
        /// n'est plus lu sur telephone : il est devine. Mieux vaut une note un peu
        /// grosse qu'une note decorative.
        /// </summary>
        private const float MinFontSize = 19f;

        private static TMP_FontAsset _font;

        [MenuItem("Milchemet/Construire le HUD", false, 10)]
        public static void Build()
        {
            JObject labels = LoadLabels();
            if (labels == null) return;

            _font = FindHebrewFont();

            Canvas canvas = EnsureCanvas();
            if (canvas == null) return;

            // Reconstruction propre : on efface l'ancien HUD s'il existe.
            Transform existing = canvas.transform.Find(RootName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            RectTransform root = NewRect(RootName, canvas.transform);
            Stretch(root);

            HudController hud = canvas.gameObject.GetComponent<HudController>();
            if (hud == null) hud = canvas.gameObject.AddComponent<HudController>();

            BuildEnergy(root, hud, labels);
            BuildCenter(root, hud, labels);
            BuildObjectives(root, hud, labels);
            BuildEndTurn(root, hud, labels);
            BuildHowToPlay(root, canvas);
            BuildPortalPanel(root, canvas, labels);
            BuildTooltip(root, canvas, labels);
            BuildUnitCard(root, canvas, labels);
            BuildTrivia(root, canvas, labels);
            BuildSubjectChoice(root, canvas, labels);
            BuildTankChoice(root, canvas, labels);
            BuildAdvisor(root, canvas, labels);
            BuildTeaching(root, canvas);
            BuildTurnAnnounce(root, canvas, labels);
            BuildGameOver(root, canvas, labels);

            ApplyFormats(hud, labels);
            EnsureCameraDirector();
            EnsurePortalTether();

            EditorUtility.SetDirty(canvas.gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);

            Debug.Log("[HudBuilder] HUD construit et cable. Pense a enregistrer la scene (Ctrl+S).");
            Selection.activeGameObject = root.gameObject;
        }

        // =================================================================
        //  LIBELLES
        // =================================================================
        private static JObject LoadLabels()
        {
            if (!File.Exists(LabelsPath))
            {
                Debug.LogErrorFormat("[HudBuilder] {0} est introuvable. Cree-le avant de construire le HUD.", LabelsPath);
                return null;
            }

            try
            {
                return JObject.Parse(File.ReadAllText(LabelsPath, Encoding.UTF8));
            }
            catch (System.Exception e)
            {
                Debug.LogErrorFormat("[HudBuilder] {0} est illisible : {1}", LabelsPath, e.Message);
                return null;
            }
        }

        private static string S(JObject o, string key, string fallback = "")
        {
            JToken t = o != null ? o[key] : null;
            return (t != null) ? t.ToString() : fallback;
        }

        /// <summary>
        /// Cherche une police TMP contenant l'hebreu. Celle des maquettes est Rubik ;
        /// Heebo et Assistant sont des replis acceptables. Sans rien de tout cela, on
        /// prend la police par defaut et on previent : l'hebreu sortira en carrees.
        ///
        /// Le graisse compte : si le projet contient a la fois "Rubik-Regular SDF" et
        /// "Rubik-Bold SDF", il faut imperativement partir du Regular. Retenir le Bold
        /// comme police de base rendrait tout le HUD gras, et FontStyles.Bold n'aurait
        /// plus rien a ajouter - les titres cesseraient de se detacher.
        /// </summary>
        private static TMP_FontAsset FindHebrewFont()
        {
            string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");

            TMP_FontAsset fallback = null;    // n'importe quoi, en dernier recours
            TMP_FontAsset hebrew = null;      // hebraique, mais graisse non ideale
            TMP_FontAsset regular = null;     // hebraique ET en graisse normale

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                TMP_FontAsset asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (asset == null) continue;

                if (fallback == null) fallback = asset;

                string lower = asset.name.ToLowerInvariant();

                bool isHebrew = lower.Contains("rubik") || lower.Contains("heebo")
                                || lower.Contains("assistant") || lower.Contains("hebrew")
                                || lower.Contains("secular");
                if (!isHebrew) continue;

                if (hebrew == null) hebrew = asset;

                bool heavy = lower.Contains("bold") || lower.Contains("black")
                             || lower.Contains("semibold") || lower.Contains("medium")
                             || lower.Contains("light") || lower.Contains("thin")
                             || lower.Contains("italic");

                if (!heavy && regular == null) regular = asset;
            }

            TMP_FontAsset chosen = (regular != null) ? regular : hebrew;

            if (chosen != null)
            {
                Debug.LogFormat("[HudBuilder] Police retenue : {0}", chosen.name);
                return chosen;
            }

            Debug.LogWarning("[HudBuilder] Aucune police hebraique trouvee (Rubik, Heebo...). "
                           + "Le HUD utilisera la police par defaut et l'hebreu sortira en carrees. "
                           + "Genere un TMP Font Asset Rubik avec la plage "
                           + "0020-007E,00B7,00D7,05B0-05F4,2013-2014,2192 puis relance.");
            return fallback;
        }

        // =================================================================
        //  CANVAS
        // =================================================================
        /// <summary>
        /// Le HUD vit sur SON PROPRE Canvas, jamais sur un Canvas existant.
        ///
        /// C'est delibere : reutiliser le GameCanvas voudrait dire toucher a son
        /// CanvasScaler, et toute l'UI de trivia deja en place se redimensionnerait.
        /// On ne casse rien : on cherche uniquement un Canvas nomme CanvasName, et
        /// s'il n'existe pas on en cree un neuf, a cote.
        /// </summary>
        private static Canvas EnsureCanvas()
        {
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);

            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i].name == CanvasName && canvases[i].transform.parent == null)
                {
                    Configure(canvases[i]);
                    EnsureEventSystem();
                    return canvases[i];
                }
            }

            GameObject go = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Configure(canvas);

            EnsureEventSystem();
            return canvas;
        }

        /// <summary>Sans EventSystem, aucun bouton ne repond. On n'en cree un que s'il manque.</summary>
        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;

            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }

        private static void Configure(Canvas canvas)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Au-dessus d'un Canvas laisse a 0, pour que le bandeau reste lisible.
            // Si un jour la fenetre de trivia passe dessous, baisse ce chiffre.
            canvas.overrideSorting = false;
            canvas.sortingOrder = SortingOrder;

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        // =================================================================
        //  BLOCS
        // =================================================================
        /// <summary>Energie : en RTL, elle ouvre la lecture, donc en haut a DROITE.</summary>
        private static void BuildEnergy(RectTransform root, HudController hud, JObject labels)
        {
            RectTransform block = NewRect("Energie", root);
            AnchorTopRight(block, new Vector2(-56f, -40f), new Vector2(300f, 150f));

            TextMeshProUGUI label = NewText("Label", block, S(labels, "energy"), 20f, ColDim,
                                            TextAlignmentOptions.TopRight, true);
            AnchorTopRight(label.rectTransform, Vector2.zero, new Vector2(300f, 28f));

            TextMeshProUGUI value = NewText("Valeur", block, "0", 56f, ColGold,
                                            TextAlignmentOptions.TopRight, false);
            value.fontStyle = FontStyles.Bold;
            AnchorTopRight(value.rectTransform, new Vector2(0f, -34f), new Vector2(300f, 64f));

            TextMeshProUGUI delta = NewText("Gain", block, "", 22f, new Color32(0x7F, 0xE8, 0xA0, 0xFF),
                                            TextAlignmentOptions.TopRight, true);
            AnchorTopRight(delta.rectTransform, new Vector2(0f, -102f), new Vector2(300f, 30f));

            hud.energyText = value;
            hud.energyDeltaText = delta;
        }

        /// <summary>Tour et rail des phases, au centre. Le rail se lit de droite a gauche.</summary>
        private static void BuildCenter(RectTransform root, HudController hud, JObject labels)
        {
            RectTransform block = NewRect("TourEtPhases", root);
            AnchorTopCenter(block, new Vector2(0f, -36f), new Vector2(900f, 120f));

            TextMeshProUGUI turn = NewText("Tour", block, "1", 20f, new Color32(0xA8, 0xB6, 0xD8, 0xFF),
                                           TextAlignmentOptions.Top, false);
            AnchorTopCenter(turn.rectTransform, Vector2.zero, new Vector2(400f, 28f));

            RectTransform rail = NewRect("Rail", block);
            AnchorTopCenter(rail, new Vector2(0f, -40f), new Vector2(880f, 62f));

            HorizontalLayoutGroup layout = rail.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            // Le rail se lit de droite a gauche : la premiere phase est a droite.
            layout.reverseArrangement = true;

            JArray phases = labels["phases"] as JArray;
            HudController.PhaseStep[] steps = new HudController.PhaseStep[5];
            string[] phaseNames = new string[5];

            for (int i = 0; i < 5; i++)
            {
                string name = (phases != null && i < phases.Count) ? phases[i].ToString() : "";
                phaseNames[i] = name;

                RectTransform step = NewRect("Phase" + i, rail);

                VerticalLayoutGroup v = step.gameObject.AddComponent<VerticalLayoutGroup>();
                v.spacing = 9f;
                v.childAlignment = TextAnchor.UpperCenter;
                v.childControlWidth = true;
                v.childControlHeight = false;
                v.childForceExpandWidth = true;

                Image bar = NewImage("Trait", step, ColIdle);
                LayoutElement barLayout = bar.gameObject.AddComponent<LayoutElement>();
                barLayout.preferredHeight = 3f;

                TextMeshProUGUI text = NewText("Nom", step, name, 19f, ColIdle,
                                               TextAlignmentOptions.Top, true);
                LayoutElement textLayout = text.gameObject.AddComponent<LayoutElement>();
                textLayout.preferredHeight = 28f;

                steps[i] = new HudController.PhaseStep();
                steps[i].bar = bar;
                steps[i].label = text;
            }

            hud.turnText = turn;
            hud.phaseSteps = steps;
            hud.phaseLabels = phaseNames;
        }

        /// <summary>Shofars restants et PV de la Base : en RTL, ils ferment la lecture, donc a GAUCHE.</summary>
        private static void BuildObjectives(RectTransform root, HudController hud, JObject labels)
        {
            RectTransform block = NewRect("Objectifs", root);
            AnchorTopLeft(block, new Vector2(56f, -40f), new Vector2(480f, 120f));

            // --- ligne des shofars ---
            RectTransform portalRow = NewRect("Shofars", block);
            AnchorTopLeft(portalRow, Vector2.zero, new Vector2(480f, 34f));

            TextMeshProUGUI portalCount = NewText("Compte", portalRow, "6/6", 24f, ColOrange,
                                                  TextAlignmentOptions.Left, false);
            portalCount.fontStyle = FontStyles.Bold;
            AnchorTopLeft(portalCount.rectTransform, Vector2.zero, new Vector2(70f, 32f));

            RectTransform dots = NewRect("Pastilles", portalRow);
            AnchorTopLeft(dots, new Vector2(80f, -6f), new Vector2(160f, 20f));

            HorizontalLayoutGroup dotLayout = dots.gameObject.AddComponent<HorizontalLayoutGroup>();
            dotLayout.spacing = 9f;
            dotLayout.childAlignment = TextAnchor.MiddleLeft;
            dotLayout.childControlWidth = false;
            dotLayout.childControlHeight = false;

            Image[] portalDots = new Image[6];
            for (int i = 0; i < 6; i++)
            {
                Image dot = NewImage("Pastille" + i, dots, ColOrange);
                dot.rectTransform.sizeDelta = new Vector2(13f, 13f);
                dot.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                portalDots[i] = dot;
            }

            TextMeshProUGUI portalLabel = NewText("Libelle", portalRow, S(labels, "portals"), 20f, ColDim,
                                                  TextAlignmentOptions.Left, true);
            AnchorTopLeft(portalLabel.rectTransform, new Vector2(262f, 0f), new Vector2(200f, 32f));

            // --- ligne de la Base ---
            RectTransform baseRow = NewRect("Base", block);
            AnchorTopLeft(baseRow, new Vector2(0f, -46f), new Vector2(480f, 34f));

            TextMeshProUGUI baseHp = NewText("PV", baseRow, "200", 24f, ColCyan,
                                             TextAlignmentOptions.Left, false);
            baseHp.fontStyle = FontStyles.Bold;
            AnchorTopLeft(baseHp.rectTransform, Vector2.zero, new Vector2(70f, 32f));

            Image track = NewImage("Fond", baseRow, new Color32(0x7E, 0xA0, 0xFF, 0x22));
            AnchorTopLeft(track.rectTransform, new Vector2(80f, -12f), new Vector2(224f, 9f));

            Image fill = NewImage("Remplissage", track.rectTransform, ColCyan);
            Stretch(fill.rectTransform);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            // En RTL, la jauge se vide vers la gauche : elle se remplit depuis la droite.
            fill.fillOrigin = (int)Image.OriginHorizontal.Right;
            fill.fillAmount = 1f;

            TextMeshProUGUI baseLabel = NewText("Libelle", baseRow, S(labels, "base"), 20f, ColDim,
                                                TextAlignmentOptions.Left, true);
            AnchorTopLeft(baseLabel.rectTransform, new Vector2(322f, 0f), new Vector2(150f, 32f));

            hud.portalDots = portalDots;
            hud.portalCountText = portalCount;
            hud.baseFill = fill;
            hud.baseHpText = baseHp;
        }

        private static void BuildEndTurn(RectTransform root, HudController hud, JObject labels)
        {
            RectTransform block = NewRect("FinDeTour", root);
            AnchorBottomCenter(block, new Vector2(0f, 54f), new Vector2(360f, 72f));

            Image bg = block.gameObject.AddComponent<Image>();
            bg.color = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.12f);

            UnityEngine.UI.Outline outline = block.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.55f);
            outline.effectDistance = new Vector2(1.5f, 1.5f);

            Button button = block.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;

            TextMeshProUGUI text = NewText("Texte", block, S(labels, "endTurn"), 24f,
                                           new Color32(0xBF, 0xF6, 0xEF, 0xFF),
                                           TextAlignmentOptions.Center, true);
            Stretch(text.rectTransform);

            hud.endTurnButton = button;
            hud.endTurnRoot = block.gameObject;
        }

        /// <summary>
        /// COMMENT ON JOUE : un bouton du bandeau, et la page qu'il ouvre.
        ///
        /// TROISIEME MISE EN PAGE, ET LA BONNE RAISON
        ///
        /// Les consignes ont d'abord defile a chaque ouverture de tour - trop lourd
        /// pour un moment qui doit rester court. Puis la legende a ete posee en
        /// permanence sur le bandeau - trop petite pour etre lue, assez grande pour
        /// encombrer.
        ///
        /// Derriere un bouton, la contrainte disparait : l'aide ne vole de place a
        /// personne, donc elle peut enfin etre GRANDE et COMPLETE. C'est la seule forme
        /// ou les trois exigences tiennent ensemble.
        /// </summary>
        private static void BuildHowToPlay(RectTransform root, Canvas canvas)
        {
            HowToPlayPanel ctrl = canvas.gameObject.GetComponent<HowToPlayPanel>();
            if (ctrl == null) ctrl = canvas.gameObject.AddComponent<HowToPlayPanel>();

            BuildHelpButton(root, ctrl);
            BuildHelpPage(root, canvas, ctrl);
        }

        /// <summary>
        /// Le bouton, en bas a gauche. A l'oppose du bouton de fin de tour : on ne doit
        /// jamais ouvrir l'aide en croyant finir son tour.
        /// </summary>
        private static void BuildHelpButton(RectTransform root, HowToPlayPanel ctrl)
        {
            RectTransform block = NewRect("BoutonAide", root);
            block.anchorMin = new Vector2(0f, 0f);
            block.anchorMax = new Vector2(0f, 0f);
            block.pivot = new Vector2(0f, 0f);
            block.anchoredPosition = new Vector2(40f, 40f);
            block.sizeDelta = new Vector2(280f, 66f);

            Image bg = block.gameObject.AddComponent<Image>();
            bg.color = new Color(ColGold.r, ColGold.g, ColGold.b, 0.12f);

            UnityEngine.UI.Outline edge = block.gameObject.AddComponent<UnityEngine.UI.Outline>();
            edge.effectColor = new Color(ColGold.r, ColGold.g, ColGold.b, 0.50f);
            edge.effectDistance = new Vector2(1.5f, 1.5f);

            Button button = block.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;

            // PAS de onClick.AddListener ici. Un abonnement pose depuis l'editeur n'est
            // pas enregistre dans la scene : il aurait disparu au lancement et le bouton
            // serait reste muet. On confie la reference au panneau, qui s'abonne dans son
            // Awake comme tous les autres boutons du projet.
            ctrl.openButton = button;

            Image icon = NewImage("Icone", block, ColGold);
            AnchorCenterRight(icon.rectTransform, new Vector2(-16f, 0f), new Vector2(32f, 32f));
            icon.preserveAspect = true;
            Bind(icon, MNLTHII.UI.IconKind.Energy);

            TextMeshProUGUI label = NewText("Libelle", block, "", 22f, ColGold,
                                            TextAlignmentOptions.Right, true);
            AnchorCenterRight(label.rectTransform, new Vector2(-58f, 0f), new Vector2(200f, 40f));

            // Le libelle vient de teachings.json, comme tout le reste de cette page.
            // Il est pose ici en dur DANS LE BUILDER a partir du meme fichier, pour que
            // le bouton ne soit pas muet avant que TeachingManager ait charge.
            label.text = ReadTeachingLabel("helpButton");
        }

        /// <summary>La page : les consignes en haut, la legende en bas.</summary>
        private static void BuildHelpPage(RectTransform root, Canvas canvas, HowToPlayPanel ctrl)
        {
            const float PageW = 1120f;
            const float PageH = 880f;
            const float Pad = 44f;

            RectTransform panel = NewRect("CommentJouer", root);
            Stretch(panel);

            CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = true;

            Image dim = panel.gameObject.AddComponent<Image>();
            dim.color = new Color32(0x03, 0x05, 0x0B, 0xE0);
            dim.raycastTarget = true;

            RectTransform page = NewRect("Page", panel);
            AnchorCenter(page, Vector2.zero, new Vector2(PageW, PageH));

            Image pageBg = page.gameObject.AddComponent<Image>();
            pageBg.color = new Color32(0x0B, 0x11, 0x20, 0xF2);
            pageBg.raycastTarget = false;

            UnityEngine.UI.Outline frame = page.gameObject.AddComponent<UnityEngine.UI.Outline>();
            frame.effectColor = new Color(ColGold.r, ColGold.g, ColGold.b, 0.34f);
            frame.effectDistance = new Vector2(1f, 1f);

            Image accent = NewImage("Liseret", page, ColGold);
            AnchorTopRight(accent.rectTransform, Vector2.zero, new Vector2(PageW, 3f));

            TextMeshProUGUI header = NewText("Titre", page, "", 34f, ColGold,
                                             TextAlignmentOptions.TopRight, true);
            header.fontStyle = FontStyles.Bold;
            AnchorTopRight(header.rectTransform, new Vector2(-Pad, -28f), new Vector2(760f, 50f));

            TextMeshProUGUI tagline = NewText("SousTitre", page, "", 18f, ColDim,
                                              TextAlignmentOptions.TopRight, true);
            AnchorTopRight(tagline.rectTransform, new Vector2(-Pad, -78f), new Vector2(760f, 30f));

            Image rule = NewImage("Filet", page, ColLine);
            AnchorTopCenter(rule.rectTransform, new Vector2(0f, -116f), new Vector2(PageW - Pad * 2f, 1f));

            // --- les cinq consignes ---
            GameObject[] stepRoots = new GameObject[HowToPlayPanel.MaxSteps];
            Image[] stepIcons = new Image[HowToPlayPanel.MaxSteps];
            TextMeshProUGUI[] stepTitles = new TextMeshProUGUI[HowToPlayPanel.MaxSteps];
            TextMeshProUGUI[] stepBodies = new TextMeshProUGUI[HowToPlayPanel.MaxSteps];

            const float StepH = 98f;
            const float IconW = 58f;
            float textW = PageW - Pad * 2f - IconW - 22f;

            for (int i = 0; i < HowToPlayPanel.MaxSteps; i++)
            {
                RectTransform row = NewRect("Etape" + i, page);
                AnchorTopCenter(row, new Vector2(0f, -136f - i * StepH),
                                new Vector2(PageW - Pad * 2f, StepH - 10f));

                // Une seule colonne de pictogrammes, a droite, toujours au meme x :
                // l'oeil descend en ligne droite au lieu de chercher chaque ligne.
                Image icon = NewImage("Icone", row, ColGold);
                AnchorCenterRight(icon.rectTransform, Vector2.zero, new Vector2(IconW, IconW));
                icon.preserveAspect = true;
                Bind(icon, StepIcon(i));

                TextMeshProUGUI title = NewText("Titre", row, "", 25f, ColText,
                                                TextAlignmentOptions.TopRight, true);
                title.fontStyle = FontStyles.Bold;
                AnchorTopRight(title.rectTransform, new Vector2(-(IconW + 22f), -2f), new Vector2(textW, 36f));

                TextMeshProUGUI body = NewText("Texte", row, "", 19f, ColDim,
                                               TextAlignmentOptions.TopRight, true);
                body.enableWordWrapping = true;
                body.lineSpacing = 6f;
                AnchorTopRight(body.rectTransform, new Vector2(-(IconW + 22f), -40f), new Vector2(textW, 48f));

                stepRoots[i] = row.gameObject;
                stepIcons[i] = icon;
                stepTitles[i] = title;
                stepBodies[i] = body;
            }

            // --- la legende, sous les consignes ---
            HudLegend legend = BuildLegendBlock(page, canvas, PageW, Pad);

            // --- fermer ---
            RectTransform close = NewRect("Fermer", page);
            AnchorBottomCenter(close, new Vector2(0f, 26f), new Vector2(300f, 62f));

            Image closeBg = close.gameObject.AddComponent<Image>();
            closeBg.color = new Color(ColGold.r, ColGold.g, ColGold.b, 0.16f);

            UnityEngine.UI.Outline closeEdge = close.gameObject.AddComponent<UnityEngine.UI.Outline>();
            closeEdge.effectColor = new Color(ColGold.r, ColGold.g, ColGold.b, 0.55f);
            closeEdge.effectDistance = new Vector2(1.5f, 1.5f);

            Button closeButton = close.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = closeBg;

            TextMeshProUGUI closeLabel = NewText("Libelle", close, "", 23f, ColGold,
                                                 TextAlignmentOptions.Center, true);
            Stretch(closeLabel.rectTransform);

            // --- cablage ---
            ctrl.panelRoot = panel.gameObject;
            ctrl.group = group;
            ctrl.headerText = header;
            ctrl.taglineText = tagline;
            ctrl.stepRoots = stepRoots;
            ctrl.stepIcons = stepIcons;
            ctrl.stepTitles = stepTitles;
            ctrl.stepBodies = stepBodies;
            ctrl.legend = legend;
            ctrl.closeButton = closeButton;
            ctrl.closeText = closeLabel;

            panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// Le bloc de legende : deux colonnes de cinq, sous les consignes.
        ///
        /// Deux colonnes et non dix lignes : un bloc compact se balaie d'un regard,
        /// une colonne de dix se lit une entree a la fois.
        /// </summary>
        private static HudLegend BuildLegendBlock(RectTransform page, Canvas canvas, float pageW, float pad)
        {
            const int Cols = 2;
            const float CellH = 52f;

            int count = HudLegend.MaxEntries;
            int rows = (count + Cols - 1) / Cols;
            float cellW = (pageW - pad * 2f) / Cols;

            Image rule = NewImage("Filet_Legende", page, ColLine);
            AnchorTopCenter(rule.rectTransform, new Vector2(0f, -640f), new Vector2(pageW - pad * 2f, 1f));

            TextMeshProUGUI header = NewText("Legende_Titre", page, "", 18f, ColDim,
                                             TextAlignmentOptions.TopRight, true);
            AnchorTopRight(header.rectTransform, new Vector2(-pad, -664f), new Vector2(600f, 28f));

            HudLegend legend = canvas.gameObject.GetComponent<HudLegend>();
            if (legend == null) legend = canvas.gameObject.AddComponent<HudLegend>();

            GameObject[] roots = new GameObject[count];
            Image[] icons = new Image[count];
            TextMeshProUGUI[] names = new TextMeshProUGUI[count];

            // Meme ordre que la section "legend" de teachings.json.
            MNLTHII.UI.IconKind[] order =
            {
                MNLTHII.UI.IconKind.Factory, MNLTHII.UI.IconKind.Crystal,
                MNLTHII.UI.IconKind.Bunker,  MNLTHII.UI.IconKind.Command,
                MNLTHII.UI.IconKind.Tank,    MNLTHII.UI.IconKind.Portal,
                MNLTHII.UI.IconKind.Crack,   MNLTHII.UI.IconKind.Energy,
                MNLTHII.UI.IconKind.Enemy,   MNLTHII.UI.IconKind.Base
            };

            Color[] tints =
            {
                new Color(0.42f, 0.92f, 0.62f), new Color(0.30f, 0.94f, 0.86f),
                ColGold,                        new Color(1f, 0.54f, 0.24f),
                new Color(0.37f, 0.66f, 1f),    ColOrange,
                new Color(1f, 0.66f, 0.30f),    ColCyan,
                ColRed,                         ColText
            };

            for (int i = 0; i < count; i++)
            {
                int row = i / Cols;
                int col = i % Cols;

                // La premiere colonne est la plus a DROITE : la lecture hebraique
                // commence de ce cote, et la legende doit suivre la meme regle que le
                // reste de l'interface.
                float x = (Cols - 1 - col) * cellW - (pageW - pad * 2f) * 0.5f + cellW * 0.5f;
                float y = -694f - row * CellH;

                RectTransform cell = NewRect("Entree" + i, page);
                AnchorTopCenter(cell, new Vector2(x, y), new Vector2(cellW - 12f, CellH - 6f));

                Image icon = NewImage("Icone", cell, tints[i]);
                AnchorCenterRight(icon.rectTransform, new Vector2(-8f, 0f), new Vector2(36f, 36f));
                icon.preserveAspect = true;
                Bind(icon, order[i]);

                TextMeshProUGUI name = NewText("Nom", cell, "", 20f, ColDim,
                                               TextAlignmentOptions.Right, true);
                AnchorCenterRight(name.rectTransform, new Vector2(-54f, 0f),
                                  new Vector2(cellW - 76f, CellH - 12f));

                roots[i] = cell.gameObject;
                icons[i] = icon;
                names[i] = name;
            }

            legend.headerText = header;
            legend.entryRoots = roots;
            legend.entryIcons = icons;
            legend.entryNames = names;
            legend.iconColors = tints;

            return legend;
        }

        /// <summary>
        /// Le pictogramme de chaque consigne, dans l'ordre du fichier :
        /// usine, Tank, danger, fissure, attaque.
        ///
        /// Cette table vit ici et pas dans le JSON : l'ordre des cinq consignes est la
        /// boucle du jeu elle-meme, il ne se reordonne pas au gre d'une traduction.
        /// </summary>
        private static MNLTHII.UI.IconKind StepIcon(int index)
        {
            switch (index)
            {
                case 0: return MNLTHII.UI.IconKind.Factory;
                case 1: return MNLTHII.UI.IconKind.Tank;
                case 2: return MNLTHII.UI.IconKind.Enemy;
                case 3: return MNLTHII.UI.IconKind.Crack;
                case 4: return MNLTHII.UI.IconKind.Portal;
            }
            return MNLTHII.UI.IconKind.None;
        }

        /// <summary>
        /// Lit un libelle de teachings.json au moment de la construction.
        ///
        /// Le bouton d'aide est le seul element dont le texte doit exister AVANT que le
        /// jeu tourne : il est visible des la premiere image, et un bouton muet dans un
        /// coin n'invite personne a cliquer.
        /// </summary>
        private static string ReadTeachingLabel(string key)
        {
            const string path = "Assets/Resources/teachings.json";

            if (!File.Exists(path)) return "";

            try
            {
                JObject root = JObject.Parse(File.ReadAllText(path, Encoding.UTF8));
                JObject ui = (root != null) ? root["ui"] as JObject : null;
                return S(ui, key);
            }
            catch (System.Exception e)
            {
                Debug.LogWarningFormat("[HudBuilder] Lecture de {0} impossible : {1}", path, e.Message);
                return "";
            }
        }

        /// <summary>L'info-bulle de cout. Masquee au depart : elle ne parait qu'au survol.</summary>
        private static void BuildTooltip(RectTransform root, Canvas canvas, JObject labels)
        {
            JObject t = labels["tooltip"] as JObject;

            RectTransform panel = NewRect("InfoBulle", root);
            // Plus haut qu'avant : la phrase qui dit ce que le batiment APPORTE occupe
            // deux a trois lignes sous les statistiques.
            AnchorTopLeft(panel, new Vector2(400f, -400f), new Vector2(470f, 440f));
            panel.pivot = new Vector2(0f, 1f);

            Image bg = panel.gameObject.AddComponent<Image>();
            bg.color = ColPanel;
            bg.raycastTarget = false;

            UnityEngine.UI.Outline outline = panel.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = ColLine;
            outline.effectDistance = new Vector2(1f, 1f);

            Image kindIcon = NewImage("Icone", panel, ColText);
            AnchorTopRight(kindIcon.rectTransform, new Vector2(-26f, -24f), new Vector2(34f, 34f));
            kindIcon.preserveAspect = true;
            Bind(kindIcon, MNLTHII.UI.IconKind.Tank);   // remplace a chaque survol

            TextMeshProUGUI title = NewText("Titre", panel, "", 26f, ColText, TextAlignmentOptions.TopRight, true);
            AnchorTopRight(title.rectTransform, new Vector2(-70f, -22f), new Vector2(280f, 46f));
            title.fontStyle = FontStyles.Bold;

            TextMeshProUGUI cost = NewText("Cout", panel, "0", 30f, ColGold, TextAlignmentOptions.TopLeft, false);
            AnchorTopLeft(cost.rectTransform, new Vector2(26f, -22f), new Vector2(120f, 50f));
            cost.fontStyle = FontStyles.Bold;

            TextMeshProUGUI subtitle = NewText("SousTitre", panel, "", 19f, ColDim, TextAlignmentOptions.TopRight, true);
            AnchorTopRight(subtitle.rectTransform, new Vector2(-26f, -72f), new Vector2(360f, 36f));

            // Trois lignes de detail : libelle a droite, valeur a gauche.
            TextMeshProUGUI[] detailLabels = new TextMeshProUGUI[3];
            TextMeshProUGUI[] detailValues = new TextMeshProUGUI[3];

            for (int i = 0; i < 3; i++)
            {
                float y = -122f - i * 40f;

                TextMeshProUGUI label = NewText("Detail" + i + "_Libelle", panel, "", 19f, ColDim,
                                                TextAlignmentOptions.TopRight, true);
                AnchorTopRight(label.rectTransform, new Vector2(-26f, y), new Vector2(270f, 36f));

                TextMeshProUGUI value = NewText("Detail" + i + "_Valeur", panel, "", 19f, ColText,
                                                TextAlignmentOptions.TopLeft, false);
                AnchorTopLeft(value.rectTransform, new Vector2(26f, y), new Vector2(150f, 36f));

                detailLabels[i] = label;
                detailValues[i] = value;
            }

            TextMeshProUGUI after = NewText("SoldeApres", panel, "", 19f, new Color32(0x7F, 0xE8, 0xA0, 0xFF),
                                            TextAlignmentOptions.TopLeft, false);
            AnchorTopLeft(after.rectTransform, new Vector2(26f, -256f), new Vector2(180f, 36f));

            TextMeshProUGUI afterLabel = NewText("SoldeApres_Libelle", panel, S(t, "afterBalance"), 19f, ColDim,
                                                 TextAlignmentOptions.TopRight, true);
            AnchorTopRight(afterLabel.rectTransform, new Vector2(-26f, -256f), new Vector2(270f, 36f));

            Image rule = NewImage("Filet", panel, ColLine);
            AnchorTopRight(rule.rectTransform, new Vector2(-26f, -294f), new Vector2(418f, 1f));

            // LA PHRASE. Les chiffres au-dessus disent COMBIEN ; celle-ci dit a quoi le
            // batiment sert. Sans elle, un joueur peut finir une partie entiere sans
            // avoir compris la difference entre le Gaz et le Cristal.
            TextMeshProUGUI effect = NewText("Effet", panel, "", 17f, ColDim,
                                             TextAlignmentOptions.TopRight, true);
            effect.enableWordWrapping = true;
            effect.lineSpacing = 8f;
            AnchorTopRight(effect.rectTransform, new Vector2(-26f, -308f), new Vector2(418f, 112f));

            HexTooltipController tooltip = canvas.gameObject.GetComponent<HexTooltipController>();
            if (tooltip == null) tooltip = canvas.gameObject.AddComponent<HexTooltipController>();

            tooltip.panelRoot = panel.gameObject;
            tooltip.titleText = title;
            tooltip.subtitleText = subtitle;
            tooltip.costText = cost;
            tooltip.afterBalanceText = after;
            tooltip.detailLabels = detailLabels;
            tooltip.detailValues = detailValues;
            tooltip.effectText = effect;
            tooltip.kindIcon = kindIcon;

            tooltip.labelTankCreate = S(t, "tankCreate");
            tooltip.labelTankEvolve = S(t, "tankEvolve");
            tooltip.labelStanceChange = S(t, "stanceChange");
            tooltip.labelBunker = S(t, "bunker");
            tooltip.labelGas = S(t, "gas");
            tooltip.labelCrystal = S(t, "crystal");
            tooltip.labelMountain = S(t, "mountain");
            tooltip.labelMaxLevel = S(t, "maxLevel");
            tooltip.labelOccupied = S(t, "occupied");
            tooltip.labelFree = S(t, "free");

            tooltip.labelShotsPerTurn = S(t, "shotsPerTurn");
            tooltip.labelRange = S(t, "range");
            tooltip.labelShotCost = S(t, "shotCost");
            tooltip.labelHeal = S(t, "heal");
            tooltip.labelIncome = S(t, "income");
            tooltip.labelMaxHpBonus = S(t, "maxHpBonus");
            tooltip.labelDamage = S(t, "damage");
            tooltip.labelHitPoints = S(t, "hitPoints");
            tooltip.labelCommandRadius = S(t, "commandRadius");
            tooltip.labelMoveBonus = S(t, "moveBonus");
            tooltip.labelDecay = S(t, "decay");

            JObject eff = (t != null) ? t["effects"] as JObject : null;
            tooltip.effectBunker = S(eff, "bunker");
            tooltip.effectGas = S(eff, "gas");
            tooltip.effectCrystal = S(eff, "crystal");
            tooltip.effectMountain = S(eff, "mountain");
            tooltip.effectTankCreate = S(eff, "tankCreate");
            tooltip.effectTankEvolve = S(eff, "tankEvolve");

            tooltip.subtitleLevelFormat = S(labels, "subtitleLevelFormat", "{0}");

            // Memes noms de posture que la carte d'unite : une seule traduction, au
            // meme endroit. Deux listes finiraient par diverger.
            tooltip.stanceNames = ReadArray(labels["unit"] as JObject, "stances", 3);

            panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// Le panneau d'etat d'un Shofar. Sous le bloc d'energie, a droite : en
        /// hebreu la droite ouvre la lecture, et c'est l'information la plus
        /// importante de la phase de depense.
        /// </summary>
        private static void BuildPortalPanel(RectTransform root, Canvas canvas, JObject labels)
        {
            JObject p = labels["portal"] as JObject;

            const float W = 420f;
            const float Pad = 26f;
            const float Inner = W - Pad * 2f;

            RectTransform panel = NewRect("PanneauShofar", root);
            AnchorTopRight(panel, new Vector2(-56f, -236f), new Vector2(W, 392f));

            Image bg = panel.gameObject.AddComponent<Image>();
            bg.color = ColPanel;
            bg.raycastTarget = false;

            UnityEngine.UI.Outline frame = panel.gameObject.AddComponent<UnityEngine.UI.Outline>();
            frame.effectColor = new Color(ColOrange.r, ColOrange.g, ColOrange.b, 0.24f);
            frame.effectDistance = new Vector2(1f, 1f);

            // Lisere du haut : sa couleur dit l'etat du bouclier d'un seul coup d'oeil.
            Image accent = NewImage("Liseret", panel, ColOrange);
            AnchorTopRight(accent.rectTransform, Vector2.zero, new Vector2(W, 2f));

            // --- entete ---
            TextMeshProUGUI title = NewText("Nom", panel, "", 20f, ColText,
                                            TextAlignmentOptions.TopRight, true);
            title.fontStyle = FontStyles.Bold;
            AnchorTopRight(title.rectTransform, new Vector2(-Pad, -22f), new Vector2(250f, 38f));

            TextMeshProUGUI level = NewText("Niveau", panel, "", 15f, ColDim,
                                            TextAlignmentOptions.TopLeft, true);
            AnchorTopLeft(level.rectTransform, new Vector2(Pad, -26f), new Vector2(120f, 32f));

            // --- points de vie ---
            TextMeshProUGUI hp = NewText("PV", panel, "", 15f, ColOrange,
                                          TextAlignmentOptions.TopRight, false);
            hp.fontStyle = FontStyles.Bold;
            AnchorTopRight(hp.rectTransform, new Vector2(-Pad, -74f), new Vector2(100f, 30f));

            Image track = NewImage("Jauge_Fond", panel, new Color32(0x7E, 0xA0, 0xFF, 0x21));
            AnchorTopRight(track.rectTransform, new Vector2(-(Pad + 110f), -80f),
                           new Vector2(Inner - 110f, 7f));

            Image hpFill = NewImage("Jauge", track.rectTransform, ColOrange);
            Stretch(hpFill.rectTransform);
            hpFill.type = Image.Type.Filled;
            hpFill.fillMethod = Image.FillMethod.Horizontal;
            // En RTL la jauge se vide vers la gauche : elle se remplit depuis la droite.
            hpFill.fillOrigin = (int)Image.OriginHorizontal.Right;
            hpFill.fillAmount = 1f;

            // Le plancher du contrecoup : sous ce trait, tuer des ennemis ne fait
            // plus rien. C'est ce qui interdit la partie 100 % defensive, donc c'est
            // exactement le genre de regle qui doit etre VISIBLE.
            float floorRatio = InteractionRules.PORTAL_BACKLASH_FLOOR_PERCENT / 100f;
            Image floorMark = NewImage("Plancher", track.rectTransform, new Color32(0xFF, 0xFF, 0xFF, 0x73));
            AnchorTopRight(floorMark.rectTransform,
                           new Vector2(-(Inner - 110f) * floorRatio, 5f),
                           new Vector2(2f, 17f));

            TextMeshProUGUI note = NewText("Note", panel, S(p, "floorNote"), 12f,
                                            new Color32(0x6E, 0x7C, 0x9E, 0xFF),
                                            TextAlignmentOptions.TopRight, true);
            AnchorTopRight(note.rectTransform, new Vector2(-Pad, -100f), new Vector2(Inner, 56f));
            note.enableWordWrapping = true;

            // --- separateur ---
            Image rule = NewImage("Filet", panel, ColLine);
            AnchorTopRight(rule.rectTransform, new Vector2(-Pad, -166f), new Vector2(Inner, 1f));

            // --- instabilite ---
            TextMeshProUGUI instLabel = NewText("Instabilite_Libelle", panel, S(p, "instability"), 14f, ColDim,
                                                 TextAlignmentOptions.TopRight, true);
            AnchorTopRight(instLabel.rectTransform, new Vector2(-Pad, -184f), new Vector2(230f, 32f));

            TextMeshProUGUI instValue = NewText("Instabilite_Valeur", panel, "", 15f, ColOrange,
                                                 TextAlignmentOptions.TopLeft, false);
            instValue.fontStyle = FontStyles.Bold;
            AnchorTopLeft(instValue.rectTransform, new Vector2(Pad, -184f), new Vector2(110f, 32f));

            RectTransform segRow = NewRect("Instabilite_Segments", panel);
            AnchorTopRight(segRow, new Vector2(-Pad, -222f), new Vector2(Inner, 7f));

            HorizontalLayoutGroup segLayout = segRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            segLayout.spacing = 4f;
            segLayout.childControlWidth = true;
            segLayout.childControlHeight = true;
            segLayout.childForceExpandWidth = true;
            segLayout.childForceExpandHeight = true;
            // Les morts se comptent de droite a gauche, comme tout le reste.
            segLayout.reverseArrangement = true;

            int needed = InteractionRules.PORTAL_KILLS_TO_BREAK_SHIELD;
            Image[] segments = new Image[needed];
            for (int i = 0; i < needed; i++)
                segments[i] = NewImage("Segment" + i, segRow, ColLine);

            // --- encadre du bouclier ---
            RectTransform box = NewRect("Bouclier", panel);
            AnchorTopRight(box, new Vector2(-Pad, -248f), new Vector2(Inner, 92f));

            Image boxBg = box.gameObject.AddComponent<Image>();
            boxBg.color = new Color32(0x7E, 0xA0, 0xFF, 0x0D);
            boxBg.raycastTarget = false;

            UnityEngine.UI.Outline boxLine = box.gameObject.AddComponent<UnityEngine.UI.Outline>();
            boxLine.effectColor = ColLine;
            boxLine.effectDistance = new Vector2(1f, 1f);

            TextMeshProUGUI shieldTitle = NewText("Titre", box, "", 16f, ColDim,
                                                   TextAlignmentOptions.TopRight, true);
            shieldTitle.fontStyle = FontStyles.Bold;
            AnchorTopRight(shieldTitle.rectTransform, new Vector2(-18f, -14f), new Vector2(300f, 32f));

            TextMeshProUGUI shieldBody = NewText("Corps", box, "", 13f,
                                                  new Color32(0x8A, 0x97, 0xB8, 0xFF),
                                                  TextAlignmentOptions.TopRight, true);
            AnchorTopRight(shieldBody.rectTransform, new Vector2(-18f, -50f), new Vector2(330f, 32f));

            // --- evolution ---
            RectTransform evolveRow = NewRect("Evolution", panel);
            AnchorTopRight(evolveRow, new Vector2(-Pad, -352f), new Vector2(Inner, 32f));

            TextMeshProUGUI evolveLabel = NewText("Libelle", evolveRow, S(p, "evolveLabel"), 14f, ColDim,
                                                   TextAlignmentOptions.TopRight, true);
            AnchorTopRight(evolveLabel.rectTransform, Vector2.zero, new Vector2(250f, 32f));

            TextMeshProUGUI evolveValue = NewText("Valeur", evolveRow, "", 14f, ColGold,
                                                   TextAlignmentOptions.TopLeft, true);
            evolveValue.fontStyle = FontStyles.Bold;
            AnchorTopLeft(evolveValue.rectTransform, Vector2.zero, new Vector2(140f, 32f));

            // --- cablage ---
            PortalPanelController ctrl = canvas.gameObject.GetComponent<PortalPanelController>();
            if (ctrl == null) ctrl = canvas.gameObject.AddComponent<PortalPanelController>();

            ctrl.panelRoot = panel.gameObject;
            ctrl.accentBar = accent;
            ctrl.titleText = title;
            ctrl.levelText = level;
            ctrl.hpText = hp;
            ctrl.hpFill = hpFill;
            ctrl.instabilityText = instValue;
            ctrl.instabilitySegments = segments;
            ctrl.shieldBox = boxBg;
            ctrl.shieldTitleText = shieldTitle;
            ctrl.shieldBodyText = shieldBody;
            ctrl.evolveRow = evolveRow.gameObject;
            ctrl.evolveText = evolveValue;

            JArray dirs = (p != null) ? p["directions"] as JArray : null;
            string[] names = new string[6];
            for (int i = 0; i < 6; i++)
                names[i] = (dirs != null && i < dirs.Count) ? dirs[i].ToString() : "";
            ctrl.directionNames = names;

            ctrl.levelFormat = S(p, "levelFormat", "{0}");
            ctrl.hpFormat = S(p, "hpFormat", "{0}/{1}");
            ctrl.instabilityFormat = S(p, "instabilityFormat", "{0}/{1}");
            ctrl.evolveFormat = S(p, "evolveFormat", "{0}");

            ctrl.shieldIntactTitle = S(p, "shieldIntactTitle");
            ctrl.shieldIntactBody = S(p, "shieldIntactBody");
            ctrl.shieldBrokenTitle = S(p, "shieldBrokenTitle");
            ctrl.shieldBrokenBodyFormat = S(p, "shieldBrokenBodyFormat", "{0}");
            ctrl.surgeTitle = S(p, "surgeTitle");
            ctrl.surgeBodyFormat = S(p, "surgeBodyFormat", "{0}");

            panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// La carte d'unite, en bas au centre - phases 3 et 4.
        ///
        /// Un seul objet pour les deux phases : la maquette impose la meme grammaire
        /// des deux cotes, seule la couleur change. Le joueur apprend a lire en
        /// phase 3 et n'a plus rien a apprendre en phase 4.
        /// </summary>
        private static void BuildUnitCard(RectTransform root, Canvas canvas, JObject labels)
        {
            JObject u = labels["unit"] as JObject;

            const float W = 680f;
            const float Pad = 26f;
            const float Inner = W - Pad * 2f;

            RectTransform card = NewRect("CarteUnite", root);
            // Au-dessus du bouton de fin de tour, qui occupe y 54 a 126.
            AnchorBottomCenter(card, new Vector2(0f, 150f), new Vector2(W, 268f));

            Image bg = card.gameObject.AddComponent<Image>();
            bg.color = ColPanel;
            bg.raycastTarget = false;

            UnityEngine.UI.Outline frame = card.gameObject.AddComponent<UnityEngine.UI.Outline>();
            frame.effectColor = ColLine;
            frame.effectDistance = new Vector2(1f, 1f);

            Image accent = NewImage("Liseret", card, ColCyan);
            AnchorTopRight(accent.rectTransform, Vector2.zero, new Vector2(W, 2f));

            // --- entete ---
            TextMeshProUGUI title = NewText("Nom", card, "", 22f, ColText,
                                            TextAlignmentOptions.TopRight, true);
            title.fontStyle = FontStyles.Bold;
            AnchorTopRight(title.rectTransform, new Vector2(-Pad, -18f), new Vector2(300f, 40f));

            TextMeshProUGUI subtitle = NewText("SousTitre", card, "", 16f, ColCyan,
                                               TextAlignmentOptions.TopLeft, true);
            AnchorTopLeft(subtitle.rectTransform, new Vector2(Pad, -22f), new Vector2(280f, 34f));

            TextMeshProUGUI hp = NewText("PV", card, "", 15f, ColText,
                                          TextAlignmentOptions.TopRight, false);
            hp.fontStyle = FontStyles.Bold;
            AnchorTopRight(hp.rectTransform, new Vector2(-Pad, -64f), new Vector2(100f, 30f));

            Image track = NewImage("Jauge_Fond", card, new Color32(0x7E, 0xA0, 0xFF, 0x21));
            AnchorTopRight(track.rectTransform, new Vector2(-(Pad + 110f), -70f),
                           new Vector2(Inner - 110f, 7f));

            Image hpFill = NewImage("Jauge", track.rectTransform, ColCyan);
            Stretch(hpFill.rectTransform);
            hpFill.type = Image.Type.Filled;
            hpFill.fillMethod = Image.FillMethod.Horizontal;
            hpFill.fillOrigin = (int)Image.OriginHorizontal.Right;
            hpFill.fillAmount = 1f;

            // --- quatre lignes ---
            // En hebreu la ligne se lit de droite a gauche : le libelle ouvre a
            // DROITE, puis vient le nombre, puis l'unite tout a gauche.
            TextMeshProUGUI[] rowLabels = new TextMeshProUGUI[4];
            TextMeshProUGUI[] rowValues = new TextMeshProUGUI[4];
            TextMeshProUGUI[] rowUnits = new TextMeshProUGUI[4];

            for (int i = 0; i < 4; i++)
            {
                float y = -104f - i * 38f;

                TextMeshProUGUI label = NewText("Ligne" + i + "_Libelle", card, "", 14f, ColDim,
                                                TextAlignmentOptions.TopRight, true);
                AnchorTopRight(label.rectTransform, new Vector2(-Pad, y), new Vector2(240f, 32f));

                TextMeshProUGUI value = NewText("Ligne" + i + "_Valeur", card, "", 15f, ColText,
                                                TextAlignmentOptions.TopLeft, false);
                AnchorTopLeft(value.rectTransform, new Vector2(Pad + 168f, y), new Vector2(180f, 32f));

                TextMeshProUGUI unit = NewText("Ligne" + i + "_Unite", card, "", 13f, ColDim,
                                               TextAlignmentOptions.TopLeft, true);
                AnchorTopLeft(unit.rectTransform, new Vector2(Pad, y), new Vector2(160f, 32f));

                rowLabels[i] = label;
                rowValues[i] = value;
                rowUnits[i] = unit;
            }

            // --- le chiffre flottant, au-dessus de la carte ---
            // Il est volontairement HORS de la carte : c'est l'impact, pas une donnee
            // de fiche. Le joueur doit le voir sans avoir a lire.
            RectTransform flash = NewRect("Impact", root);
            AnchorBottomCenter(flash, new Vector2(0f, 452f), new Vector2(420f, 120f));

            TextMeshProUGUI flashValue = NewText("Valeur", flash, "", 54f, ColCyan,
                                                 TextAlignmentOptions.Top, false);
            flashValue.fontStyle = FontStyles.Bold;
            AnchorTopCenter(flashValue.rectTransform, Vector2.zero, new Vector2(420f, 80f));

            TextMeshProUGUI flashCaption = NewText("Legende", flash, "", 15f, ColDim,
                                                   TextAlignmentOptions.Top, true);
            AnchorTopCenter(flashCaption.rectTransform, new Vector2(0f, -84f), new Vector2(420f, 34f));

            // --- cablage ---
            UnitActionCard ctrl = canvas.gameObject.GetComponent<UnitActionCard>();
            if (ctrl == null) ctrl = canvas.gameObject.AddComponent<UnitActionCard>();

            ctrl.panelRoot = card.gameObject;
            ctrl.accentBar = accent;
            ctrl.titleText = title;
            ctrl.subtitleText = subtitle;
            ctrl.hpText = hp;
            ctrl.hpFill = hpFill;
            ctrl.rowLabels = rowLabels;
            ctrl.rowValues = rowValues;
            ctrl.rowUnits = rowUnits;
            ctrl.flashRoot = flash.gameObject;
            ctrl.flashValueText = flashValue;
            ctrl.flashCaptionText = flashCaption;

            ctrl.tankNameFormat = S(u, "tankNameFormat", "{0}");
            ctrl.enemyName = S(u, "enemyName");
            ctrl.levelFormat = S(u, "levelFormat", "{0}");
            ctrl.hpFormat = S(u, "hpFormat", "{0}/{1}");
            ctrl.stanceNames = ReadArray(u, "stances", 3);
            ctrl.directionNames = ReadArray(labels["portal"] as JObject, "directions", 6);

            ctrl.rowTargetLabel = S(u, "rowTarget");
            ctrl.rowDamageLabel = S(u, "rowDamage");
            ctrl.rowRangeLabel = S(u, "rowRange");
            ctrl.rowMobilityLabel = S(u, "rowMobility");
            ctrl.rowOriginLabel = S(u, "rowOrigin");

            ctrl.unitHex = S(u, "unitHex");
            ctrl.unitHexPerTurn = S(u, "unitHexPerTurn");

            ctrl.targetBase = S(u, "targetBase");
            ctrl.targetEnemy = S(u, "targetEnemy");
            ctrl.targetTank = S(u, "targetTank");

            ctrl.captionDamage = S(u, "captionDamage");
            ctrl.captionShielded = S(u, "captionShielded");
            ctrl.captionBaseHp = S(u, "captionBaseHp");
            ctrl.captionMove = S(u, "captionMove");
            ctrl.damageReducedFormat = S(u, "damageReducedFormat", "{0} {1}");

            flash.gameObject.SetActive(false);
            card.gameObject.SetActive(false);
        }

        /// <summary>
        /// Le panneau de question - phase 1.
        ///
        /// Plein cadre et opaque, volontairement. C'est le seul moment ou il n'y a
        /// qu'un objet a l'ecran et qu'une decision a prendre ; laisser le plateau
        /// visible derriere ne ferait que disperser le regard. Le voile capte aussi
        /// les clics, pour qu'aucun ne traverse vers une case.
        /// </summary>
        private static void BuildTrivia(RectTransform root, Canvas canvas, JObject labels)
        {
            JObject t = labels["trivia"] as JObject;

            const float ColWidth = 940f;

            RectTransform panel = NewRect("PanneauQuestion", root);
            Stretch(panel);

            Image dim = panel.gameObject.AddComponent<Image>();
            dim.color = new Color32(0x03, 0x05, 0x0B, 0xDB);
            dim.raycastTarget = true;     // le voile absorbe les clics

            // --- categorie, a droite : en hebreu, la droite ouvre la lecture ---
            TextMeshProUGUI category = NewText("Categorie", panel, "", 44f, ColText,
                                               TextAlignmentOptions.TopRight, true);
            category.fontStyle = FontStyles.Bold;
            AnchorCenterRight(category.rectTransform, new Vector2(ColWidth * 0.5f, 296f),
                              new Vector2(520f, 58f));

            // --- le gain, annonce AVANT la reponse ---
            TextMeshProUGUI rewardLabel = NewText("Tarif_Libelle", panel, S(t, "rewardLabel"),
                                                  18f, ColDim, TextAlignmentOptions.TopLeft, true);
            AnchorCenterLeft(rewardLabel.rectTransform, new Vector2(-ColWidth * 0.5f, 314f),
                             new Vector2(220f, 26f));

            TextMeshProUGUI rewardValue = NewText("Tarif_Valeur", panel, "", 36f, ColGold,
                                                  TextAlignmentOptions.TopLeft, false);
            rewardValue.fontStyle = FontStyles.Bold;
            AnchorCenterLeft(rewardValue.rectTransform, new Vector2(-ColWidth * 0.5f, 280f),
                             new Vector2(220f, 46f));

            // --- l'enonce : l'objet principal de l'ecran ---
            TextMeshProUGUI question = NewText("Question", panel, "", 46f, ColText,
                                               TextAlignmentOptions.TopRight, true);
            AnchorCenter(question.rectTransform, new Vector2(0f, 150f), new Vector2(ColWidth, 200f));
            question.enableWordWrapping = true;

            // --- les quatre reponses ---
            Button[] buttons = new Button[4];
            TextMeshProUGUI[] answerTexts = new TextMeshProUGUI[4];
            Image[] answerBgs = new Image[4];

            TriviaPanelController ctrl = canvas.gameObject.GetComponent<TriviaPanelController>();
            if (ctrl == null) ctrl = canvas.gameObject.AddComponent<TriviaPanelController>();

            for (int i = 0; i < 4; i++)
            {
                float y = -20f - i * 106f;

                RectTransform row = NewRect("Reponse" + i, panel);
                AnchorCenter(row, new Vector2(0f, y), new Vector2(ColWidth, 92f));

                Image rowBg = row.gameObject.AddComponent<Image>();
                rowBg.color = new Color32(0x7E, 0xA0, 0xFF, 0x10);

                UnityEngine.UI.Outline rowLine = row.gameObject.AddComponent<UnityEngine.UI.Outline>();
                rowLine.effectColor = ColLine;
                rowLine.effectDistance = new Vector2(1f, 1f);

                Button button = row.gameObject.AddComponent<Button>();
                button.targetGraphic = rowBg;
                // Transition None : le survol est gere par TriviaPanelController, qui
                // teinte le fond. Laisser aussi la transition par defaut ferait deux
                // effets concurrents sur la meme image.
                button.transition = Selectable.Transition.None;

                TriviaAnswerHover hover = row.gameObject.AddComponent<TriviaAnswerHover>();
                hover.panel = ctrl;
                hover.index = i;

                TextMeshProUGUI text = NewText("Texte", row, "", 26f, ColText,
                                               TextAlignmentOptions.Right, true);
                AnchorTopRight(text.rectTransform, new Vector2(-34f, -24f), new Vector2(860f, 46f));

                buttons[i] = button;
                answerTexts[i] = text;
                answerBgs[i] = rowBg;
            }

            // --- le chrono ---
            TextMeshProUGUI timerLabel = NewText("Chrono_Libelle", panel, S(t, "timerLabel"),
                                                 18f, ColDim, TextAlignmentOptions.TopRight, true);
            AnchorCenterRight(timerLabel.rectTransform, new Vector2(ColWidth * 0.5f, -432f),
                              new Vector2(340f, 36f));

            TextMeshProUGUI timerValue = NewText("Chrono_Valeur", panel, "", 22f, ColCyan,
                                                 TextAlignmentOptions.TopLeft, true);
            timerValue.fontStyle = FontStyles.Bold;
            AnchorCenterLeft(timerValue.rectTransform, new Vector2(-ColWidth * 0.5f, -432f),
                             new Vector2(260f, 38f));

            Image timerTrack = NewImage("Chrono_Fond", panel, new Color32(0x7E, 0xA0, 0xFF, 0x1E));
            AnchorCenter(timerTrack.rectTransform, new Vector2(0f, -472f), new Vector2(ColWidth, 5f));

            Image timerFill = NewImage("Chrono_Jauge", timerTrack.rectTransform, ColCyan);
            Stretch(timerFill.rectTransform);
            timerFill.type = Image.Type.Filled;
            timerFill.fillMethod = Image.FillMethod.Horizontal;
            // En RTL le temps s'ecoule vers la gauche : la jauge se vide depuis la gauche.
            timerFill.fillOrigin = (int)Image.OriginHorizontal.Right;
            timerFill.fillAmount = 1f;

            // --- cablage ---
            ctrl.panelRoot = panel.gameObject;
            ctrl.dimmer = dim;
            ctrl.categoryText = category;
            ctrl.rewardLabelText = rewardLabel;
            ctrl.rewardValueText = rewardValue;
            ctrl.questionText = question;
            ctrl.answerButtons = buttons;
            ctrl.answerTexts = answerTexts;
            ctrl.answerBackgrounds = answerBgs;
            ctrl.timerLabelText = timerLabel;
            ctrl.timerValueText = timerValue;
            ctrl.timerFill = timerFill;

            ctrl.rewardLabel = S(t, "rewardLabel");
            ctrl.timerLabel = S(t, "timerLabel");
            ctrl.rewardFormat = S(t, "rewardFormat", "+{0}");
            ctrl.secondsFormat = S(t, "secondsFormat", "{0}");

            // Les categories : deux tableaux paralleles, dans le meme ordre.
            JObject cats = (t != null) ? t["categories"] as JObject : null;
            if (cats != null)
            {
                int count = cats.Count;
                string[] keys = new string[count];
                string[] names = new string[count];

                int k = 0;
                foreach (System.Collections.Generic.KeyValuePair<string, JToken> pair in cats)
                {
                    keys[k] = pair.Key;
                    names[k] = (pair.Value != null) ? pair.Value.ToString() : "";
                    k++;
                }

                ctrl.categoryKeys = keys;
                ctrl.categoryNames = names;
            }

            // On branche le panneau sur le TriviaManager de la scene, s'il existe :
            // sans ce lien, le jeu continuerait d'ouvrir l'ancienne modale Reach.
            // Objets desactives compris : le TriviaManager peut vivre sur un canvas
            // qu'on vient d'eteindre en attendant de le supprimer.
            TriviaManager trivia = Object.FindFirstObjectByType<TriviaManager>(FindObjectsInactive.Include);
            if (trivia != null)
            {
                trivia.panel = ctrl;

                // Le verdict doit rester lisible : trop court, le joueur ne sait pas
                // s'il avait raison, et c'est la seule chose qu'il a gagnee en repondant.
                trivia.resultHoldSeconds = 1.7f;

                EditorUtility.SetDirty(trivia);
                Debug.Log("[HudBuilder] Le panneau de question est branche sur le TriviaManager de la scene.");
            }
            else
            {
                Debug.LogWarning("[HudBuilder] Aucun TriviaManager dans la scene : glisse le panneau "
                               + "dans son champ \"panel\" quand il sera la.");
            }

            panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// L'ecran de choix du sujet - la premiere decision de chaque tour.
        ///
        /// Quatre cartes cote a cote, lues de droite a gauche. Chacune porte le nom du
        /// sujet, son palier de difficulte en pastilles, et le gain que ce palier
        /// commande. Le gain n'est pas decoratif : c'est celui de la question deja
        /// tiree pour ce sujet.
        /// </summary>
        private static void BuildSubjectChoice(RectTransform root, Canvas canvas, JObject labels)
        {
            JObject sc = labels["subjectChoice"] as JObject;
            JObject tr = labels["trivia"] as JObject;

            const float CardW = 340f;
            const float CardH = 340f;
            const float Gap = 26f;

            RectTransform panel = NewRect("ChoixDuSujet", root);
            Stretch(panel);

            Image dim = panel.gameObject.AddComponent<Image>();
            dim.color = new Color32(0x03, 0x05, 0x0B, 0xDB);
            dim.raycastTarget = true;

            TextMeshProUGUI title = NewText("Titre", panel, S(sc, "title"), 40f, ColText,
                                            TextAlignmentOptions.Top, true);
            title.fontStyle = FontStyles.Bold;
            AnchorCenter(title.rectTransform, new Vector2(0f, 292f), new Vector2(1000f, 56f));

            TextMeshProUGUI subtitle = NewText("SousTitre", panel, S(sc, "subtitle"), 19f, ColDim,
                                               TextAlignmentOptions.Top, true);
            AnchorCenter(subtitle.rectTransform, new Vector2(0f, 238f), new Vector2(1000f, 32f));

            SubjectChoicePanel ctrl = canvas.gameObject.GetComponent<SubjectChoicePanel>();
            if (ctrl == null) ctrl = canvas.gameObject.AddComponent<SubjectChoicePanel>();

            Button[] buttons = new Button[4];
            Image[] backgrounds = new Image[4];
            Image[] accents = new Image[4];
            TextMeshProUGUI[] names = new TextMeshProUGUI[4];
            TextMeshProUGUI[] rewards = new TextMeshProUGUI[4];
            TextMeshProUGUI[] tiers = new TextMeshProUGUI[4];
            Image[] dots = new Image[16];

            // Quatre cartes centrees. En hebreu la lecture ouvre a DROITE : le premier
            // sujet est la carte la plus a droite, d'ou le signe du decalage.
            float span = CardW * 4f + Gap * 3f;
            float firstCenter = span * 0.5f - CardW * 0.5f;

            for (int i = 0; i < 4; i++)
            {
                float x = firstCenter - i * (CardW + Gap);

                RectTransform card = NewRect("Sujet" + i, panel);
                AnchorCenter(card, new Vector2(x, -30f), new Vector2(CardW, CardH));

                Image bg = card.gameObject.AddComponent<Image>();
                bg.color = new Color32(0x7E, 0xA0, 0xFF, 0x10);

                UnityEngine.UI.Outline edge = card.gameObject.AddComponent<UnityEngine.UI.Outline>();
                edge.effectColor = ColLine;
                edge.effectDistance = new Vector2(1f, 1f);

                Button button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = bg;
                // Le survol est teinte par SubjectChoicePanel : deux effets concurrents
                // sur la meme image se combattraient.
                button.transition = Selectable.Transition.None;

                SubjectCardHover hover = card.gameObject.AddComponent<SubjectCardHover>();
                hover.panel = ctrl;
                hover.slot = i;

                Image accent = NewImage("Liseret", card, ColCyan);
                AnchorTopRight(accent.rectTransform, Vector2.zero, new Vector2(CardW, 3f));

                TextMeshProUGUI name = NewText("Nom", card, "", 32f, ColText,
                                               TextAlignmentOptions.Top, true);
                name.fontStyle = FontStyles.Bold;
                AnchorTopCenter(name.rectTransform, new Vector2(0f, -32f), new Vector2(CardW - 24f, 58f));

                // Les pastilles : le gain dit COMBIEN, les pastilles disent POURQUOI.
                // Sans elles le joueur croit a un tirage au sort, alors que c'est une
                // echelle de difficulte.
                RectTransform dotRow = NewRect("Paliers", card);
                AnchorTopCenter(dotRow, new Vector2(0f, -104f), new Vector2(140f, 11f));

                HorizontalLayoutGroup dotLayout = dotRow.gameObject.AddComponent<HorizontalLayoutGroup>();
                dotLayout.spacing = 7f;
                dotLayout.childAlignment = TextAnchor.MiddleCenter;
                dotLayout.childControlWidth = true;
                dotLayout.childControlHeight = true;
                dotLayout.childForceExpandWidth = true;
                dotLayout.childForceExpandHeight = true;
                dotLayout.reverseArrangement = true;

                for (int d = 0; d < 4; d++)
                    dots[i * 4 + d] = NewImage("Palier" + d, dotRow, ColLine);

                TextMeshProUGUI tier = NewText("Difficulte", card, "", 16f, ColDim,
                                               TextAlignmentOptions.Top, true);
                AnchorTopCenter(tier.rectTransform, new Vector2(0f, -132f), new Vector2(CardW - 24f, 34f));

                TextMeshProUGUI reward = NewText("Gain", card, "", 58f, ColGold,
                                                 TextAlignmentOptions.Top, false);
                reward.fontStyle = FontStyles.Bold;
                AnchorTopCenter(reward.rectTransform, new Vector2(0f, -196f), new Vector2(CardW - 24f, 100f));

                buttons[i] = button;
                backgrounds[i] = bg;
                accents[i] = accent;
                names[i] = name;
                rewards[i] = reward;
                tiers[i] = tier;
            }

            // --- cablage ---
            ctrl.panelRoot = panel.gameObject;
            ctrl.titleText = title;
            ctrl.subtitleText = subtitle;
            ctrl.cardButtons = buttons;
            ctrl.cardBackgrounds = backgrounds;
            ctrl.cardAccents = accents;
            ctrl.cardNames = names;
            ctrl.cardRewards = rewards;
            ctrl.cardDifficultyLabels = tiers;
            ctrl.cardDots = dots;

            ctrl.title = S(sc, "title");
            ctrl.subtitle = S(sc, "subtitle");
            ctrl.rewardFormat = S(sc, "rewardFormat", "+{0}");
            ctrl.difficultyNames = ReadArray(sc, "difficulties", 4);

            // Les noms des sujets viennent de trivia.categories : une seule traduction,
            // au meme endroit, partagee avec le panneau de question.
            JObject cats = (tr != null) ? tr["categories"] as JObject : null;
            if (cats != null)
            {
                int count = cats.Count;
                string[] keys = new string[count];
                string[] display = new string[count];

                int k = 0;
                foreach (System.Collections.Generic.KeyValuePair<string, JToken> pair in cats)
                {
                    keys[k] = pair.Key;
                    display[k] = (pair.Value != null) ? pair.Value.ToString() : "";
                    k++;
                }

                ctrl.categoryKeys = keys;
                ctrl.categoryNames = display;
            }

            TriviaManager trivia = Object.FindFirstObjectByType<TriviaManager>(FindObjectsInactive.Include);
            if (trivia != null)
            {
                trivia.subjectPanel = ctrl;
                EditorUtility.SetDirty(trivia);
            }

            panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// L'ecran de choix d'un Tank : ses trois postures cote a cote, plus
        /// l'evolution en quatrieme carte quand elle est possible.
        ///
        /// POURQUOI UN GROUPE HORIZONTAL ET PAS QUATRE POSITIONS FIXES
        ///
        /// Cet ecran s'ouvre tantot avec quatre cartes, tantot avec trois - a la
        /// creation il n'y a rien a faire evoluer. A positions fixes, les trois cartes
        /// restantes resteraient collees a droite avec un trou a gauche. Le groupe
        /// horizontal ignore un enfant desactive et recentre le reste tout seul.
        ///
        /// reverseArrangement place la premiere carte a DROITE : en hebreu la lecture
        /// ouvre de ce cote, et la premiere posture doit etre la premiere lue.
        /// </summary>
        private static void BuildTankChoice(RectTransform root, Canvas canvas, JObject labels)
        {
            JObject tc = labels["tankChoice"] as JObject;
            JObject un = labels["unit"] as JObject;

            const float CardW = 330f;
            const float CardH = 420f;
            const float Gap = 24f;

            RectTransform panel = NewRect("ChoixDuTank", root);
            Stretch(panel);

            Image dim = panel.gameObject.AddComponent<Image>();
            dim.color = new Color32(0x03, 0x05, 0x0B, 0xDB);
            // Le voile absorbe les clics : pendant le choix, rien ne doit atteindre le
            // plateau, sinon un clic a cote poserait un second Tank.
            dim.raycastTarget = true;

            TextMeshProUGUI title = NewText("Titre", panel, S(tc, "createTitle"), 40f, ColText,
                                            TextAlignmentOptions.Top, true);
            title.fontStyle = FontStyles.Bold;
            AnchorCenter(title.rectTransform, new Vector2(0f, 300f), new Vector2(1000f, 56f));

            TextMeshProUGUI subtitle = NewText("SousTitre", panel, S(tc, "createSubtitle"), 19f, ColDim,
                                               TextAlignmentOptions.Top, true);
            AnchorCenter(subtitle.rectTransform, new Vector2(0f, 250f), new Vector2(1100f, 32f));

            TankChoicePanel ctrl = canvas.gameObject.GetComponent<TankChoicePanel>();
            if (ctrl == null) ctrl = canvas.gameObject.AddComponent<TankChoicePanel>();

            // --- la rangee de cartes ---
            RectTransform row = NewRect("Cartes", panel);
            AnchorCenter(row, new Vector2(0f, -10f), new Vector2(1600f, CardH));

            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = Gap;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.reverseArrangement = true;

            int slots = TankChoicePanel.MaxOptions;

            Button[] buttons = new Button[slots];
            Image[] backgrounds = new Image[slots];
            Image[] accents = new Image[slots];
            Image[] icons = new Image[slots];
            TextMeshProUGUI[] names = new TextMeshProUGUI[slots];
            TextMeshProUGUI[] bodies = new TextMeshProUGUI[slots];
            TextMeshProUGUI[] costs = new TextMeshProUGUI[slots];

            for (int i = 0; i < slots; i++)
            {
                RectTransform card = NewRect("Option" + i, row);
                card.sizeDelta = new Vector2(CardW, CardH);

                LayoutElement size = card.gameObject.AddComponent<LayoutElement>();
                size.preferredWidth = CardW;
                size.preferredHeight = CardH;
                size.flexibleWidth = 0f;
                size.flexibleHeight = 0f;

                Image bg = card.gameObject.AddComponent<Image>();
                bg.color = new Color32(0x7E, 0xA0, 0xFF, 0x10);

                UnityEngine.UI.Outline edge = card.gameObject.AddComponent<UnityEngine.UI.Outline>();
                edge.effectColor = ColLine;
                edge.effectDistance = new Vector2(1f, 1f);

                Button button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = bg;
                // Le survol est teinte par TankChoicePanel : deux effets concurrents sur
                // la meme image se combattraient.
                button.transition = Selectable.Transition.None;

                TankCardHover hover = card.gameObject.AddComponent<TankCardHover>();
                hover.panel = ctrl;
                hover.slot = i;

                Image accent = NewImage("Liseret", card, ColCyan);
                AnchorTopRight(accent.rectTransform, Vector2.zero, new Vector2(CardW, 3f));

                // Le pictogramme, teinte par TankChoicePanel avec la couleur de la
                // posture. Trois chevrons de couleurs differentes se distinguent plus
                // vite que trois mots hebreux, surtout quand on joue vite.
                Image cardIcon = NewImage("Icone", card, ColText);
                AnchorTopCenter(cardIcon.rectTransform, new Vector2(0f, -22f), new Vector2(44f, 44f));
                cardIcon.preserveAspect = true;
                Bind(cardIcon, (i >= 3) ? MNLTHII.UI.IconKind.Crystal : MNLTHII.UI.IconKind.Tank);

                TextMeshProUGUI name = NewText("Nom", card, "", 30f, ColText,
                                               TextAlignmentOptions.Top, true);
                name.fontStyle = FontStyles.Bold;
                AnchorTopCenter(name.rectTransform, new Vector2(0f, -74f), new Vector2(CardW - 24f, 54f));

                Image rule = NewImage("Filet", card, ColLine);
                AnchorTopCenter(rule.rectTransform, new Vector2(0f, -136f), new Vector2(CardW - 72f, 1f));

                // Le texte qui dit ce que la posture FAIT. C'est la seule raison d'etre
                // de cet ecran : sans lui, choisir entre trois mots hebreux serait un
                // tirage au sort.
                TextMeshProUGUI body = NewText("Explication", card, "", 19f, ColDim,
                                               TextAlignmentOptions.Top, true);
                body.enableWordWrapping = true;
                body.lineSpacing = 8f;
                AnchorTopCenter(body.rectTransform, new Vector2(0f, -152f), new Vector2(CardW - 40f, 160f));

                // Le prix n'est PAS en RTL : un nombre dans un champ RTL voit ses
                // chiffres s'inverser, et 15 devient 51.
                TextMeshProUGUI cost = NewText("Cout", card, "", 40f, ColGold,
                                               TextAlignmentOptions.Bottom, false);
                cost.fontStyle = FontStyles.Bold;
                AnchorBottomCenter(cost.rectTransform, new Vector2(0f, 26f), new Vector2(CardW - 24f, 58f));

                buttons[i] = button;
                backgrounds[i] = bg;
                accents[i] = accent;
                icons[i] = cardIcon;
                names[i] = name;
                bodies[i] = body;
                costs[i] = cost;
            }

            // --- annuler ---
            RectTransform cancel = NewRect("Annuler", panel);
            AnchorCenter(cancel, new Vector2(0f, -268f), new Vector2(280f, 62f));

            Image cancelBg = cancel.gameObject.AddComponent<Image>();
            cancelBg.color = new Color32(0x7E, 0xA0, 0xFF, 0x0C);

            UnityEngine.UI.Outline cancelEdge = cancel.gameObject.AddComponent<UnityEngine.UI.Outline>();
            cancelEdge.effectColor = ColLine;
            cancelEdge.effectDistance = new Vector2(1f, 1f);

            Button cancelButton = cancel.gameObject.AddComponent<Button>();
            cancelButton.targetGraphic = cancelBg;

            TextMeshProUGUI cancelText = NewText("Libelle", cancel, S(tc, "cancel"), 22f, ColDim,
                                                 TextAlignmentOptions.Center, true);
            Stretch(cancelText.rectTransform);

            // --- cablage ---
            ctrl.panelRoot = panel.gameObject;
            ctrl.titleText = title;
            ctrl.subtitleText = subtitle;
            ctrl.cardButtons = buttons;
            ctrl.cardBackgrounds = backgrounds;
            ctrl.cardAccents = accents;
            ctrl.cardIcons = icons;
            ctrl.cardNames = names;
            ctrl.cardBodies = bodies;
            ctrl.cardCosts = costs;
            ctrl.cancelButton = cancelButton;
            ctrl.cancelText = cancelText;

            ctrl.createTitle = S(tc, "createTitle");
            ctrl.createSubtitle = S(tc, "createSubtitle");
            ctrl.changeTitle = S(tc, "changeTitle");
            ctrl.changeSubtitle = S(tc, "changeSubtitle");
            ctrl.cancelLabel = S(tc, "cancel");
            ctrl.freeLabel = S(tc, "free");
            ctrl.currentLabel = S(tc, "current");
            ctrl.evolveName = S(tc, "evolveName");
            ctrl.evolveBody = S(tc, "evolveBody");

            // Les noms des postures viennent de unit.stances : la carte d'unite et cet
            // ecran doivent dire le meme mot pour la meme chose.
            ctrl.stanceNames = ReadArray(un, "stances", 3);
            ctrl.stanceBodies = ReadArray(tc, "bodies", 3);

            panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// LE CONSEILLER : au plus trois cartes, en bas a droite, pendant la phase de
        /// depense.
        ///
        /// POURQUOI UN GROUPE VERTICAL AVEC AJUSTEMENT DE TAILLE
        ///
        /// Le nombre de conseils varie d'un tour a l'autre : trois quand la situation
        /// est tendue, un seul quand tout va bien, zero quand il n'y a rien d'utile a
        /// dire. A hauteur fixe, un seul conseil laisserait deux trous et le panneau
        /// aurait l'air casse. Le groupe vertical ignore une carte desactivee et
        /// l'ajusteur ramene le fond a la hauteur reellement occupee.
        ///
        /// Le pivot est en bas a droite : le panneau grandit donc VERS LE HAUT, et son
        /// bord bas ne bouge jamais - l'oeil retrouve les cartes au meme endroit.
        /// </summary>
        private static void BuildAdvisor(RectTransform root, Canvas canvas, JObject labels)
        {
            JObject ad = labels["advisor"] as JObject;
            JObject un = labels["unit"] as JObject;

            const float PanelW = 470f;
            const float CardW = 438f;
            const float CardH = 104f;

            RectTransform panel = NewRect("Conseiller", root);
            panel.anchorMin = new Vector2(1f, 0f);
            panel.anchorMax = new Vector2(1f, 0f);
            panel.pivot = new Vector2(1f, 0f);
            panel.anchoredPosition = new Vector2(-56f, 40f);
            panel.sizeDelta = new Vector2(PanelW, 400f);

            Image bg = panel.gameObject.AddComponent<Image>();
            bg.color = ColPanel;
            bg.raycastTarget = false;

            UnityEngine.UI.Outline frame = panel.gameObject.AddComponent<UnityEngine.UI.Outline>();
            frame.effectColor = ColLine;
            frame.effectDistance = new Vector2(1f, 1f);

            VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 14, 16);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            AdvisorPanel ctrl = canvas.gameObject.GetComponent<AdvisorPanel>();
            if (ctrl == null) ctrl = canvas.gameObject.AddComponent<AdvisorPanel>();

            // --- entete ---
            TextMeshProUGUI header = NewText("Entete", panel, S(ad, "header"), 18f, ColCyan,
                                             TextAlignmentOptions.TopRight, true);
            header.fontStyle = FontStyles.Bold;
            header.rectTransform.sizeDelta = new Vector2(CardW, 30f);

            LayoutElement headerSize = header.gameObject.AddComponent<LayoutElement>();
            headerSize.preferredWidth = CardW;
            headerSize.preferredHeight = 30f;
            headerSize.flexibleWidth = 0f;
            headerSize.flexibleHeight = 0f;

            int slots = AdvisorPanel.MaxCards;

            Button[] buttons = new Button[slots];
            GameObject[] roots = new GameObject[slots];
            Image[] backgrounds = new Image[slots];
            Image[] accents = new Image[slots];
            TextMeshProUGUI[] titles = new TextMeshProUGUI[slots];
            TextMeshProUGUI[] reasons = new TextMeshProUGUI[slots];
            TextMeshProUGUI[] costs = new TextMeshProUGUI[slots];

            for (int i = 0; i < slots; i++)
            {
                RectTransform card = NewRect("Conseil" + i, panel);
                card.sizeDelta = new Vector2(CardW, CardH);

                LayoutElement size = card.gameObject.AddComponent<LayoutElement>();
                size.preferredWidth = CardW;
                size.preferredHeight = CardH;
                size.flexibleWidth = 0f;
                size.flexibleHeight = 0f;

                Image cardBg = card.gameObject.AddComponent<Image>();
                cardBg.color = new Color32(0x7E, 0xA0, 0xFF, 0x10);

                Button button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = cardBg;
                // Le survol est teinte par AdvisorPanel : deux effets concurrents sur
                // la meme image se combattraient.
                button.transition = Selectable.Transition.None;

                AdvisorCardHover hover = card.gameObject.AddComponent<AdvisorCardHover>();
                hover.panel = ctrl;
                hover.slot = i;

                // Le liseret porte l'urgence : rouge quand il faut agir ce tour-ci,
                // couleur de posture sinon. C'est lisible avant d'avoir lu un mot.
                Image accent = NewImage("Liseret", card, ColCyan);
                AnchorTopRight(accent.rectTransform, Vector2.zero, new Vector2(CardW, 3f));

                TextMeshProUGUI title = NewText("Titre", card, "", 21f, ColText,
                                                TextAlignmentOptions.TopRight, true);
                title.fontStyle = FontStyles.Bold;
                AnchorTopRight(title.rectTransform, new Vector2(-14f, -12f), new Vector2(280f, 34f));

                // Le prix n'est PAS en RTL : un nombre dans un champ RTL voit ses
                // chiffres s'inverser, et 15 devient 51.
                TextMeshProUGUI cost = NewText("Cout", card, "", 22f, ColGold,
                                               TextAlignmentOptions.TopLeft, false);
                cost.fontStyle = FontStyles.Bold;
                AnchorTopLeft(cost.rectTransform, new Vector2(14f, -12f), new Vector2(110f, 34f));

                // La raison est le coeur de la carte : sans elle, le conseiller dit quoi
                // faire mais le joueur n'apprend jamais pourquoi, et il en depend a vie.
                TextMeshProUGUI reason = NewText("Raison", card, "", 15f, ColDim,
                                                 TextAlignmentOptions.TopRight, true);
                reason.enableWordWrapping = true;
                reason.lineSpacing = 6f;
                AnchorTopRight(reason.rectTransform, new Vector2(-14f, -50f), new Vector2(CardW - 28f, 46f));

                buttons[i] = button;
                roots[i] = card.gameObject;
                backgrounds[i] = cardBg;
                accents[i] = accent;
                titles[i] = title;
                reasons[i] = reason;
                costs[i] = cost;
            }

            // --- cablage ---
            ctrl.panelRoot = panel.gameObject;
            ctrl.headerText = header;
            ctrl.cardButtons = buttons;
            ctrl.cardRoots = roots;
            ctrl.cardBackgrounds = backgrounds;
            ctrl.cardAccents = accents;
            ctrl.cardTitles = titles;
            ctrl.cardReasons = reasons;
            ctrl.cardCosts = costs;

            ctrl.header = S(ad, "header");
            ctrl.titleCreateTank = S(ad, "titleCreateTank");
            ctrl.titleChangeStance = S(ad, "titleChangeStance");
            ctrl.titleEvolve = S(ad, "titleEvolve");
            ctrl.titleBunker = S(ad, "titleBunker");
            ctrl.titleCrystal = S(ad, "titleCrystal");
            ctrl.titleCommand = S(ad, "titleCommand");
            ctrl.titleBuild = S(ad, "titleBuild");

            ctrl.reasons = ReadArray(ad, "reasons", MNLTHII.Rules.AdvisorRules.REASON_COUNT);
            ctrl.stanceNames = ReadArray(un, "stances", 3);

            panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// L'ECRAN D'ENSEIGNEMENT, qui remplace la question de Trivia.
        ///
        /// Ses textes ne viennent PAS de hud_labels.json mais de teachings.json, lu au
        /// demarrage par TeachingManager. La raison est simple : les enseignements et
        /// leurs libelles forment un tout, et les separer en deux fichiers garantissait
        /// qu'un jour l'un serait traduit sans l'autre. Le builder n'a donc ici aucun
        /// libelle a recopier - il pose la mise en page, et c'est tout.
        ///
        /// Trois etats occupent la meme surface : l'enseignement, la question, le
        /// resultat. Un seul est actif a la fois, ce qui evite un panneau qui grandit et
        /// retrecit entre les etapes.
        /// </summary>
        private static void BuildTeaching(RectTransform root, Canvas canvas)
        {
            RectTransform panel = NewRect("Enseignement", root);
            Stretch(panel);

            CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = true;

            Image dim = panel.gameObject.AddComponent<Image>();
            dim.color = new Color32(0x03, 0x05, 0x0B, 0xEB);
            dim.raycastTarget = true;

            TeachingPanel ctrl = canvas.gameObject.GetComponent<TeachingPanel>();
            if (ctrl == null) ctrl = canvas.gameObject.AddComponent<TeachingPanel>();

            // --- entete ---
            TextMeshProUGUI header = NewText("Titre", panel, "", 34f, ColText,
                                             TextAlignmentOptions.Top, true);
            header.fontStyle = FontStyles.Bold;
            AnchorCenter(header.rectTransform, new Vector2(0f, 372f), new Vector2(1000f, 50f));

            // LA PHRASE. Elle dit pourquoi cet ecran existe, et elle ne ment pas :
            // une bonne reponse fissure vraiment un Shofar.
            TextMeshProUGUI tagline = NewText("Accroche", panel, "", 20f, ColCyan,
                                              TextAlignmentOptions.Top, true);
            AnchorCenter(tagline.rectTransform, new Vector2(0f, 326f), new Vector2(1100f, 34f));

            Image ruleTop = NewImage("Filet_Haut", panel, ColLine);
            AnchorCenter(ruleTop.rectTransform, new Vector2(0f, 292f), new Vector2(880f, 1f));

            // =====================================================
            //  1. L'enseignement
            // =====================================================
            RectTransform teaching = NewRect("Texte", panel);
            AnchorCenter(teaching, new Vector2(0f, -20f), new Vector2(1040f, 560f));

            // La source en premier, et en petit : elle donne son autorite au texte sans
            // lui voler la vedette.
            TextMeshProUGUI source = NewText("Source", teaching, "", 19f, ColGold,
                                             TextAlignmentOptions.Top, true);
            AnchorTopCenter(source.rectTransform, new Vector2(0f, -12f), new Vector2(1000f, 34f));

            TextMeshProUGUI title = NewText("Sujet", teaching, "", 40f, ColText,
                                            TextAlignmentOptions.Top, true);
            title.fontStyle = FontStyles.Bold;
            AnchorTopCenter(title.rectTransform, new Vector2(0f, -56f), new Vector2(1000f, 62f));

            TextMeshProUGUI body = NewText("Corps", teaching, "", 25f, ColText,
                                           TextAlignmentOptions.Top, true);
            body.enableWordWrapping = true;
            body.lineSpacing = 16f;
            AnchorTopCenter(body.rectTransform, new Vector2(0f, -140f), new Vector2(940f, 300f));

            // Le compteur n'est PAS en RTL : "1/2" dans un champ RTL devient "2/1".
            TextMeshProUGUI counter = NewText("Compteur", teaching, "", 18f, ColDim,
                                              TextAlignmentOptions.Bottom, false);
            AnchorBottomCenter(counter.rectTransform, new Vector2(0f, 96f), new Vector2(200f, 32f));

            RectTransform next = NewRect("Suivant", teaching);
            AnchorBottomCenter(next, new Vector2(0f, 20f), new Vector2(300f, 66f));

            Image nextBg = next.gameObject.AddComponent<Image>();
            nextBg.color = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.14f);

            UnityEngine.UI.Outline nextEdge = next.gameObject.AddComponent<UnityEngine.UI.Outline>();
            nextEdge.effectColor = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.55f);
            nextEdge.effectDistance = new Vector2(1.5f, 1.5f);

            Button nextButton = next.gameObject.AddComponent<Button>();
            nextButton.targetGraphic = nextBg;

            TextMeshProUGUI nextLabel = NewText("Libelle", next, "", 24f,
                                                new Color32(0xBF, 0xF6, 0xEF, 0xFF),
                                                TextAlignmentOptions.Center, true);
            Stretch(nextLabel.rectTransform);

            // =====================================================
            //  2. La question
            // =====================================================
            RectTransform question = NewRect("Question", panel);
            AnchorCenter(question, new Vector2(0f, -20f), new Vector2(1040f, 560f));

            TextMeshProUGUI questionTitle = NewText("Intitule", question, "", 24f, ColCyan,
                                                    TextAlignmentOptions.Top, true);
            questionTitle.fontStyle = FontStyles.Bold;
            AnchorTopCenter(questionTitle.rectTransform, new Vector2(0f, -8f), new Vector2(900f, 40f));

            TextMeshProUGUI situation = NewText("Situation", question, "", 26f, ColText,
                                                TextAlignmentOptions.Top, true);
            situation.enableWordWrapping = true;
            situation.lineSpacing = 14f;
            AnchorTopCenter(situation.rectTransform, new Vector2(0f, -62f), new Vector2(920f, 170f));

            Button[] answerButtons = new Button[TeachingPanel.MaxAnswers];
            Image[] answerBackgrounds = new Image[TeachingPanel.MaxAnswers];
            TextMeshProUGUI[] answerTexts = new TextMeshProUGUI[TeachingPanel.MaxAnswers];

            const float AnswerW = 880f;
            const float AnswerH = 86f;

            for (int i = 0; i < TeachingPanel.MaxAnswers; i++)
            {
                RectTransform card = NewRect("Reponse" + i, question);
                AnchorTopCenter(card, new Vector2(0f, -252f - i * (AnswerH + 14f)),
                                new Vector2(AnswerW, AnswerH));

                Image bg = card.gameObject.AddComponent<Image>();
                bg.color = new Color32(0x7E, 0xA0, 0xFF, 0x12);

                UnityEngine.UI.Outline edge = card.gameObject.AddComponent<UnityEngine.UI.Outline>();
                edge.effectColor = ColLine;
                edge.effectDistance = new Vector2(1f, 1f);

                Button button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = bg;
                // Le survol est teinte par TeachingPanel : deux effets concurrents sur
                // la meme image se combattraient.
                button.transition = Selectable.Transition.None;

                TeachingAnswerHover hover = card.gameObject.AddComponent<TeachingAnswerHover>();
                hover.panel = ctrl;
                hover.slot = i;

                TextMeshProUGUI label = NewText("Texte", card, "", 24f, ColText,
                                                TextAlignmentOptions.Center, true);
                label.enableWordWrapping = true;
                Stretch(label.rectTransform);

                answerButtons[i] = button;
                answerBackgrounds[i] = bg;
                answerTexts[i] = label;
            }

            // =====================================================
            //  3. Le resultat
            // =====================================================
            RectTransform result = NewRect("Resultat", panel);
            AnchorCenter(result, new Vector2(0f, -20f), new Vector2(1040f, 400f));

            TextMeshProUGUI resultTitle = NewText("Verdict", result, "", 46f, ColText,
                                                  TextAlignmentOptions.Top, true);
            resultTitle.fontStyle = FontStyles.Bold;
            AnchorTopCenter(resultTitle.rectTransform, new Vector2(0f, -40f), new Vector2(900f, 70f));

            TextMeshProUGUI resultBody = NewText("Explication", result, "", 24f, ColDim,
                                                 TextAlignmentOptions.Top, true);
            resultBody.enableWordWrapping = true;
            resultBody.lineSpacing = 14f;
            AnchorTopCenter(resultBody.rectTransform, new Vector2(0f, -128f), new Vector2(900f, 180f));

            // --- cablage ---
            ctrl.panelRoot = panel.gameObject;
            ctrl.group = group;
            ctrl.headerText = header;
            ctrl.taglineText = tagline;

            ctrl.teachingRoot = teaching.gameObject;
            ctrl.sourceText = source;
            ctrl.titleText = title;
            ctrl.bodyText = body;
            ctrl.counterText = counter;
            ctrl.nextButton = nextButton;
            ctrl.nextText = nextLabel;

            ctrl.questionRoot = question.gameObject;
            ctrl.questionTitleText = questionTitle;
            ctrl.situationText = situation;
            ctrl.answerButtons = answerButtons;
            ctrl.answerBackgrounds = answerBackgrounds;
            ctrl.answerTexts = answerTexts;

            ctrl.resultRoot = result.gameObject;
            ctrl.resultTitleText = resultTitle;
            ctrl.resultBodyText = resultBody;

            EnsureTeachingManager();
            EnsureIconOverrides();

            question.gameObject.SetActive(false);
            result.gameObject.SetActive(false);
            panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// Marque une Image comme portant tel pictogramme, SANS y ecrire le sprite.
        ///
        /// Le sprite est resolu au lancement par IconBinder, qui passe par IconLibrary
        /// et donc par tes propres dessins s'il y en a. Ecrire le sprite ici l'aurait
        /// fige dans la scene : deposer de vrais pictogrammes aurait exige de tout
        /// reconstruire pour les voir.
        /// </summary>
        private static void Bind(Image image, MNLTHII.UI.IconKind kind)
        {
            if (image == null) return;

            MNLTHII.UI.IconBinder binder = image.gameObject.GetComponent<MNLTHII.UI.IconBinder>();
            if (binder == null) binder = image.gameObject.AddComponent<MNLTHII.UI.IconBinder>();

            binder.kind = kind;
            binder.Apply();
        }

        /// <summary>
        /// Pose la boite a pictogrammes s'il manque. C'est la que tu deposeras tes
        /// fichiers : dix champs, une seule fois, et toute l'interface suit.
        /// </summary>
        private static void EnsureIconOverrides()
        {
            MNLTHII.UI.IconOverrides existing =
                Object.FindFirstObjectByType<MNLTHII.UI.IconOverrides>(FindObjectsInactive.Include);
            if (existing != null) return;

            LocalGameEngine engine = Object.FindFirstObjectByType<LocalGameEngine>(FindObjectsInactive.Include);

            GameObject host = (engine != null) ? engine.gameObject : new GameObject("Pictogrammes");
            host.AddComponent<MNLTHII.UI.IconOverrides>();

            EditorUtility.SetDirty(host);
            Debug.LogFormat("[HudBuilder] Boite a pictogrammes posee sur \"{0}\" : depose tes fichiers dedans.", host.name);
        }

        /// <summary>
        /// Pose le lecteur d'enseignements s'il manque, sur l'objet des gestionnaires.
        /// Sans lui l'ecran serait construit et resterait vide.
        /// </summary>
        private static void EnsureTeachingManager()
        {
            TeachingManager existing = Object.FindFirstObjectByType<TeachingManager>(FindObjectsInactive.Include);
            if (existing != null) return;

            LocalGameEngine engine = Object.FindFirstObjectByType<LocalGameEngine>(FindObjectsInactive.Include);

            GameObject host = (engine != null) ? engine.gameObject : new GameObject("Enseignements");
            host.AddComponent<TeachingManager>();

            EditorUtility.SetDirty(host);
            Debug.LogFormat("[HudBuilder] Lecteur d'enseignements installe sur \"{0}\".", host.name);
        }

        /// <summary>
        /// L'ouverture d'un tour. Un numero, le revenu du tour, et c'est tout.
        ///
        /// Le panneau porte un CanvasGroup parce que le fondu doit passer par UNE
        /// valeur alpha pour l'ensemble. Faire varier la couleur de chaque texte
        /// forcerait TextMeshPro a reconstruire son maillage a chaque frame du fondu.
        /// </summary>
        private static void BuildTurnAnnounce(RectTransform root, Canvas canvas, JObject labels)
        {
            JObject ta = labels["turnAnnounce"] as JObject;

            RectTransform panel = NewRect("OuvertureDuTour", root);
            Stretch(panel);

            CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            // Le voile absorbe les clics : pendant l'ouverture, rien ne doit atteindre
            // le plateau, meme un clic parti trop tot.
            group.blocksRaycasts = true;

            Image dim = panel.gameObject.AddComponent<Image>();
            dim.color = new Color32(0x03, 0x05, 0x0B, 0xE6);
            dim.raycastTarget = true;

            Image ruleTop = NewImage("Filet_Haut", panel, ColLine);
            AnchorCenter(ruleTop.rectTransform, new Vector2(0f, 104f), new Vector2(520f, 1f));

            TextMeshProUGUI turnLabel = NewText("Libelle", panel, S(ta, "turnLabel"), 22f, ColDim,
                                                TextAlignmentOptions.Top, true);
            AnchorCenter(turnLabel.rectTransform, new Vector2(0f, 74f), new Vector2(700f, 44f));

            TextMeshProUGUI turnNumber = NewText("Numero", panel, "", 120f, ColCyan,
                                                 TextAlignmentOptions.Top, false);
            turnNumber.fontStyle = FontStyles.Bold;
            AnchorCenter(turnNumber.rectTransform, new Vector2(0f, -26f), new Vector2(700f, 190f));

            Image ruleBottom = NewImage("Filet_Bas", panel, ColLine);
            AnchorCenter(ruleBottom.rectTransform, new Vector2(0f, -112f), new Vector2(520f, 1f));

            // En hebreu la ligne se lit de droite a gauche : le libelle ouvre a droite,
            // le nombre vient ensuite, donc a sa gauche.
            TextMeshProUGUI incomeLabel = NewText("Revenu_Libelle", panel, S(ta, "incomeLabel"),
                                                  20f, ColDim, TextAlignmentOptions.Top, true);
            AnchorCenter(incomeLabel.rectTransform, new Vector2(88f, -176f), new Vector2(240f, 40f));

            TextMeshProUGUI incomeValue = NewText("Revenu_Valeur", panel, "", 28f, ColGold,
                                                  TextAlignmentOptions.Top, false);
            incomeValue.fontStyle = FontStyles.Bold;
            AnchorCenter(incomeValue.rectTransform, new Vector2(-92f, -178f), new Vector2(190f, 46f));

            TurnAnnouncePanel ctrl = canvas.gameObject.GetComponent<TurnAnnouncePanel>();
            if (ctrl == null) ctrl = canvas.gameObject.AddComponent<TurnAnnouncePanel>();

            ctrl.panelRoot = panel.gameObject;
            ctrl.group = group;
            ctrl.turnLabelText = turnLabel;
            ctrl.turnNumberText = turnNumber;
            ctrl.incomeLabelText = incomeLabel;
            ctrl.incomeValueText = incomeValue;
            ctrl.ruleTop = ruleTop;
            ctrl.ruleBottom = ruleBottom;

            ctrl.turnLabel = S(ta, "turnLabel");
            ctrl.incomeLabel = S(ta, "incomeLabel");
            ctrl.numberFormat = S(ta, "numberFormat", "{0}");
            ctrl.incomeFormat = S(ta, "incomeFormat", "+{0}");

            // Le rythme est impose ici, et pas seulement par le defaut du champ : une
            // valeur deja enregistree dans la scene ne change pas quand on modifie le
            // defaut dans le code. Sans cette ligne, l'ancienne cadence trop rapide
            // survivrait a toutes les reconstructions.
            ctrl.fadeInDuration = 0.35f;
            ctrl.holdDuration = 1.7f;
            ctrl.fadeOutDuration = 0.45f;

            panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// Installe le cadreur de camera s'il manque.
        ///
        /// Il ne vit pas sur le Canvas : ce n'est pas de l'interface, c'est de la mise
        /// en scene. Sa place est sur le PIVOT de la camera - le parent - et surtout
        /// pas sur la camera elle-meme, qui porte un Animator dans cette scene : tout
        /// ce qu'un script ecrirait sur son transform serait ecrase a la frame
        /// suivante par le systeme d'animation.
        ///
        /// Si la camera n'a pas de parent, on pose quand meme le composant sur elle et
        /// on previent : le cadrage fonctionnera, sauf si un Animator le combat.
        /// </summary>
        /// <summary>
        /// Pose le gestionnaire de cordons s'il manque.
        ///
        /// Il n'a aucune reference a cabler et ne se voit pas dans la hierarchie : sans
        /// cette ligne, on livrerait un projet ou l'eclair entre un ennemi et son Shofar
        /// n'existe que si quelqu'un a pense a ajouter le composant a la main.
        /// </summary>
        private static void EnsurePortalTether()
        {
            PortalTether existing = Object.FindFirstObjectByType<PortalTether>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Debug.Log("[HudBuilder] Cordons ennemi-Shofar deja presents.");
                return;
            }

            // Sur l'objet des gestionnaires. Attention : PortalManager, BuildingManager
            // et compagnie n'existent PAS dans la scene enregistree - LocalGameEngine
            // les ajoute lui-meme au demarrage. Chercher PortalManager ici ne trouverait
            // donc rien et creerait un objet orphelin ; c'est LocalGameEngine qu'il faut
            // viser, puisque c'est lui qui porte tout le reste.
            LocalGameEngine engine = Object.FindFirstObjectByType<LocalGameEngine>(FindObjectsInactive.Include);

            GameObject host = (engine != null)
                ? engine.gameObject
                : new GameObject("Cordons Shofar");

            host.AddComponent<PortalTether>();

            EditorUtility.SetDirty(host);
            Debug.LogFormat("[HudBuilder] Cordons ennemi-Shofar installes sur \"{0}\".", host.name);
        }

        private static void EnsureCameraDirector()
        {
            CameraDirector existing = Object.FindFirstObjectByType<CameraDirector>();
            if (existing != null)
            {
                Debug.Log("[HudBuilder] Cadreur de camera deja present.");
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[HudBuilder] Aucune camera taguee MainCamera : le cadrage n'a pas ete installe.");
                return;
            }

            Transform pivot = cam.transform.parent;

            if (pivot == null)
            {
                pivot = cam.transform;
                Debug.LogWarning("[HudBuilder] La camera n'a pas de parent. Le cadreur est pose sur elle, "
                               + "mais si elle porte un Animator il ecrasera les mouvements : "
                               + "mets-la sous un objet vide et relance.");
            }

            CameraDirector director = pivot.gameObject.AddComponent<CameraDirector>();
            director.pivot = pivot;

            // L'apercu de menace lit la cadence du cadreur pour se synchroniser : on
            // aligne les deux ici, d'un seul endroit.
            ThreatPreview preview = Object.FindFirstObjectByType<ThreatPreview>(FindObjectsInactive.Include);
            if (preview != null)
            {
                preview.tourHoldPerGhost = 1.3f;
                EditorUtility.SetDirty(preview);
            }

            EditorUtility.SetDirty(pivot.gameObject);
            Debug.LogFormat("[HudBuilder] Cadreur de camera installe sur \"{0}\".", pivot.name);
        }

        /// <summary>
        /// L'ecran de fin : victoire ou defaite.
        ///
        /// Une seule mise en page pour les deux. Seuls le titre, la couleur et la
        /// phrase changent - deux ecrans distincts auraient coute deux fois le travail
        /// pour rendre le jeu moins lisible.
        ///
        /// Trois chiffres sous le titre. Une fin sans bilan n'apprend rien : c'est la
        /// difference entre "j'ai perdu" et "j'ai perdu au tour 14 avec deux Shofars
        /// encore ouverts", et c'est la seconde phrase qui donne envie de rejouer.
        /// </summary>
        private static void BuildGameOver(RectTransform root, Canvas canvas, JObject labels)
        {
            JObject go = labels["gameOver"] as JObject;

            const float PanelW = 820f;
            const float PanelH = 560f;

            RectTransform screen = NewRect("FinDePartie", root);
            Stretch(screen);

            CanvasGroup group = screen.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            Image dim = screen.gameObject.AddComponent<Image>();
            dim.color = new Color32(0x03, 0x05, 0x0B, 0xEB);
            dim.raycastTarget = true;

            // --- la carte centrale ---
            RectTransform panel = NewRect("Carte", screen);
            AnchorCenter(panel, Vector2.zero, new Vector2(PanelW, PanelH));

            Image bg = panel.gameObject.AddComponent<Image>();
            bg.color = ColPanel;
            bg.raycastTarget = false;

            UnityEngine.UI.Outline frame = panel.gameObject.AddComponent<UnityEngine.UI.Outline>();
            frame.effectColor = ColLine;
            frame.effectDistance = new Vector2(1f, 1f);

            Image accent = NewImage("Liseret", panel, ColCyan);
            AnchorTopRight(accent.rectTransform, Vector2.zero, new Vector2(PanelW, 3f));

            TextMeshProUGUI title = NewText("Titre", panel, "", 64f, ColCyan,
                                            TextAlignmentOptions.Top, true);
            title.fontStyle = FontStyles.Bold;
            AnchorTopCenter(title.rectTransform, new Vector2(0f, -56f), new Vector2(PanelW - 60f, 106f));

            TextMeshProUGUI subtitle = NewText("SousTitre", panel, "", 21f, ColDim,
                                               TextAlignmentOptions.Top, true);
            AnchorTopCenter(subtitle.rectTransform, new Vector2(0f, -176f), new Vector2(PanelW - 100f, 44f));

            Image rule = NewImage("Filet", panel, ColLine);
            AnchorTopCenter(rule.rectTransform, new Vector2(0f, -240f), new Vector2(PanelW - 120f, 1f));

            // --- les trois chiffres, lus de droite a gauche ---
            TextMeshProUGUI[] statLabels = new TextMeshProUGUI[3];
            TextMeshProUGUI[] statValues = new TextMeshProUGUI[3];

            const float ColumnW = 240f;
            float firstX = ColumnW;      // colonne 0 a droite, comme la lecture

            for (int i = 0; i < 3; i++)
            {
                float x = firstX - i * ColumnW;

                RectTransform column = NewRect("Bilan" + i, panel);
                AnchorTopCenter(column, new Vector2(x, -278f), new Vector2(ColumnW, 140f));

                TextMeshProUGUI value = NewText("Valeur", column, "", 46f, ColText,
                                                TextAlignmentOptions.Top, false);
                value.fontStyle = FontStyles.Bold;
                AnchorTopCenter(value.rectTransform, Vector2.zero, new Vector2(ColumnW, 78f));

                TextMeshProUGUI label = NewText("Libelle", column, "", 16f, ColDim,
                                                TextAlignmentOptions.Top, true);
                AnchorTopCenter(label.rectTransform, new Vector2(0f, -82f), new Vector2(ColumnW, 34f));

                statValues[i] = value;
                statLabels[i] = label;
            }

            // --- reprise ---
            RectTransform replay = NewRect("Rejouer", panel);
            AnchorBottomCenter(replay, new Vector2(0f, 44f), new Vector2(340f, 78f));

            Image replayBg = replay.gameObject.AddComponent<Image>();
            replayBg.color = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.12f);

            UnityEngine.UI.Outline replayEdge = replay.gameObject.AddComponent<UnityEngine.UI.Outline>();
            replayEdge.effectColor = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.55f);
            replayEdge.effectDistance = new Vector2(1.5f, 1.5f);

            Button replayButton = replay.gameObject.AddComponent<Button>();
            replayButton.targetGraphic = replayBg;

            TextMeshProUGUI replayText = NewText("Texte", replay, "", 22f,
                                                 new Color32(0xBF, 0xF6, 0xEF, 0xFF),
                                                 TextAlignmentOptions.Center, true);
            Stretch(replayText.rectTransform);

            // --- cablage ---
            GameOverPanel ctrl = canvas.gameObject.GetComponent<GameOverPanel>();
            if (ctrl == null) ctrl = canvas.gameObject.AddComponent<GameOverPanel>();

            ctrl.panelRoot = screen.gameObject;
            ctrl.group = group;
            ctrl.dimmer = dim;
            ctrl.accentBar = accent;
            ctrl.titleText = title;
            ctrl.subtitleText = subtitle;
            ctrl.statLabels = statLabels;
            ctrl.statValues = statValues;
            ctrl.replayButton = replayButton;
            ctrl.replayText = replayText;

            ctrl.victoryTitle = S(go, "victoryTitle");
            ctrl.victorySubtitle = S(go, "victorySubtitle");
            ctrl.defeatTitle = S(go, "defeatTitle");
            ctrl.defeatSubtitle = S(go, "defeatSubtitle");
            ctrl.replayLabel = S(go, "replayLabel");

            ctrl.statTurnsLabel = S(go, "statTurns");
            ctrl.statPortalsLabel = S(go, "statPortals");
            ctrl.statKillsLabel = S(go, "statKills");
            ctrl.portalsFormat = S(go, "portalsFormat", "{0}/{1}");

            screen.gameObject.SetActive(false);
        }

        /// <summary>Lit un tableau de chaines du JSON, complete a la taille voulue.</summary>
        private static string[] ReadArray(JObject o, string key, int size)
        {
            string[] result = new string[size];
            JArray array = (o != null) ? o[key] as JArray : null;

            for (int i = 0; i < size; i++)
                result[i] = (array != null && i < array.Count) ? array[i].ToString() : "";

            return result;
        }

        private static void ApplyFormats(HudController hud, JObject labels)
        {
            hud.energyDeltaFormat = S(labels, "energyDeltaFormat", "+{0}");
            hud.portalCountFormat = S(labels, "portalCountFormat", "{0}/{1}");
            hud.turnFormat = "{0}";
        }

        // =================================================================
        //  PRIMITIVES
        // =================================================================
        private static RectTransform NewRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            RectTransform rect = NewRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// rightToLeft : vrai pour tout texte hebreu, faux pour les nombres.
        /// Un nombre en RTL voit ses chiffres s'inverser - 12/50 devient 50/12.
        /// </summary>
        private static TextMeshProUGUI NewText(string name, Transform parent, string content,
                                               float size, Color color,
                                               TextAlignmentOptions align, bool rightToLeft)
        {
            RectTransform rect = NewRect(name, parent);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();

            if (_font != null) text.font = _font;
            text.text = content;

            float scaled = size * TextScale;
            if (scaled < MinFontSize) scaled = MinFontSize;
            text.fontSize = scaled;
            text.color = color;
            text.alignment = align;
            text.isRightToLeftText = rightToLeft;
            text.raycastTarget = false;
            text.enableWordWrapping = false;

            DisableFontFeatures(text);

            return text;
        }

        /// <summary>
        /// Font Features sur "Nothing", pas sur "kern".
        ///
        /// TextMeshPro active le crenage par defaut. Sur du latin c'est souhaitable ;
        /// sur de l'hebreu en RTL ca ne l'est pas. Les paires de crenage des polices
        /// hebraiques sont pensees pour un flux gauche-droite, et TMP les applique
        /// apres avoir inverse l'ordre des glyphes : les decalages tombent du mauvais
        /// cote de la lettre. Resultat, des mots qui se resserrent ou se decollent au
        /// hasard, et les lettres finales qui viennent mordre la suivante.
        ///
        /// On vide donc la liste des features actives plutot que d'aller la decocher
        /// a la main sur chaque TextMeshProUGUI.
        ///
        /// Passage par SerializedObject et non par l'API publique : le nom de ce
        /// champ a change entre les versions de TMP (m_enableKerning avant, une liste
        /// m_ActiveFontFeatures ensuite). En cherchant les deux, le constructeur
        /// compile et fonctionne quelle que soit la version installee, au lieu de
        /// casser a la prochaine mise a jour du paquet.
        /// </summary>
        private static void DisableFontFeatures(TextMeshProUGUI text)
        {
            if (text == null) return;

            SerializedObject so = new SerializedObject(text);
            bool touched = false;

            // TMP recent : liste de features OpenType actives. Vide = "Nothing".
            SerializedProperty features = so.FindProperty("m_ActiveFontFeatures");
            if (features != null && features.isArray)
            {
                features.ClearArray();
                touched = true;
            }

            // TMP ancien : un simple booleen de crenage.
            SerializedProperty kerning = so.FindProperty("m_enableKerning");
            if (kerning == null) kerning = so.FindProperty("m_EnableKerning");
            if (kerning != null && kerning.propertyType == SerializedPropertyType.Boolean)
            {
                kerning.boolValue = false;
                touched = true;
            }

            if (touched) so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void AnchorTopRight(RectTransform rect, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        private static void AnchorTopLeft(RectTransform rect, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        private static void AnchorTopCenter(RectTransform rect, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        private static void AnchorBottomCenter(RectTransform rect, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        private static void AnchorCenter(RectTransform rect, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        /// <summary>Ancrage centre horizontalement, aligne sur le bord DROIT du bloc.</summary>
        private static void AnchorCenterRight(RectTransform rect, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        /// <summary>Ancrage centre horizontalement, aligne sur le bord GAUCHE du bloc.</summary>
        private static void AnchorCenterLeft(RectTransform rect, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }
    }
}
