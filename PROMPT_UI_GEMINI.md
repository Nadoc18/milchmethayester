# Prompt Gemini — maquette complète de l'interface de מלחמת היצר

## Comment s'en servir (lis ça d'abord)

Une seule image ne peut pas contenir vingt écrans lisibles. Fais **une génération par écran**, toujours en **1920 × 1080**, en collant à chaque fois :

1. le **BLOC A — style global** (identique à chaque fois, c'est lui qui garantit que tous les écrans se ressemblent) ;
2. **un seul** des blocs d'écran (B1, B2, …) ;
3. le **BLOC C — règles de rendu** (à la fin, identique à chaque fois).

Fais d'abord B1 (menu) et B4 (HUD) : ce sont les deux écrans de référence. Quand ils te plaisent, garde l'image du HUD et dis à Gemini pour chaque écran suivant « *same style and frames as this reference image* » en la joignant — c'est ce qui donne un ensemble cohérent.

Pour les icônes et la police, il y a deux blocs à part (D et E), à générer séparément.

Les textes hébreux sont **exacts** (ce sont ceux du jeu, `hud_labels.json` et `teachings.json`). Les modèles d'image se trompent souvent sur l'hébreu : vérifie chaque mot, et si une lettre est fausse, regénère cet écran seul en ajoutant « *the Hebrew text must be exactly: …* ».

---

## BLOC A — Style global (à coller en premier, à chaque fois)

```
You are designing the complete user interface of "Milchemet HaYetzer" (מלחמת היצר, "The War of the Inclination"), a Hebrew-language, right-to-left, turn-based hexagonal tactics game for WebGL and mobile (landscape). The player defends a Base at the center of a hex board against the Yetzer Hara (the evil inclination) that spawns enemies from six gates ("Shofars") at the corners. The player builds gas factories (energy income), bunkers (auto-turrets), crystals (heal and upgrade), command centers (shield bubble), and places Tanks in one of three roles: Guard, Assault, Hunt. Each turn has five phases. Winning means closing all six gates. Between turns the player reads a short Jewish ethical teaching (Musar) and answers a question about it.

VISUAL STYLE — "clean military sci-fi, night blue, hexagonal":
- Background: deep navy #0B1630 to #0A1428 with a soft red glow on the left (the Yetzer) and a soft cyan glow on the right (the player). Never black, never grey.
- Panels and frames: dark navy plates (#0E1B36 → #0A1428 subtle vertical gradient) with CHAMFERED corners (45° cut, about 12 px at 1080p), a THIN 2 px luminous border and a soft outer glow of the border color. Border colors carry meaning: STEEL (#C9D6F2, white-blue) = neutral; CYAN (#5BC8F5) = what belongs to the player (Base, Energy, main action); RED (#FF5C6C) = what threatens (gates, enemies, threat). Gold (#FFC65C) only for rare highlights (teachings, rewards).
- Primary button: filled electric-blue gradient (#3FB2F5 → #1A76CC), white chamfered border, glow. Secondary buttons: dark plate with steel border. Danger/threat buttons: dark plate with red border.
- Hexagons (pointy-top) are the signature shape: used for menu buttons, badges, counters and dots. Thin circuit-board lines may decorate large plates (like the game logo).
- Typography: a bold geometric Hebrew sans-serif (Rubik Bold / Heebo Black style). All text is Hebrew, RIGHT-TO-LEFT: labels sit on the RIGHT of their value, reading order starts at the top-right. Numbers stay left-to-right. Text is BIG and readable on a phone: body text never under 24 px at 1080p, labels 28–32 px, titles 48–72 px, touch targets at least 64 px tall.
- Two accent rules: everything "ours" is cyan, everything "theirs" is red. Grey (#6E7C9C) means inactive / upcoming.
- No Latin text anywhere except numbers and the key names "Esc", "Tab", "Space / Enter" where quoted.
- Flat 2D UI, crisp vector edges, no photos, no 3D scene in the mockup. The board area, when shown, is an empty navy backdrop so the UI can be cut into separate assets afterwards.
```

---

## BLOC B — Les écrans (un seul par génération)

### B1 — Menu principal (1920 × 1080)

```
SCREEN: MAIN MENU.
Background: the navy backdrop with red glow left, cyan glow right.
Top center: the game logo — a wide chamfered navy plate (about 980 × 300 px) with thin circuit lines etched on it, three small hexagonal nodes on its border (top-center, bottom-left, bottom-right), and the title in two colors, Hebrew, bold, large (≈ 96 px): "מלחמת" in red on the right, "היצר" in cyan on the left. A small subtitle under the plate, grey-blue (≈ 30 px): "בחר רמת קושי".
Center: SEVEN pointy-top hexagons (≈ 200 px wide each) arranged as a honeycomb:
  - Top row, three hexagons, reading right to left: "קל" (right), "בינוני" (center, SELECTED: filled cyan with a white glow border), "קשה" (left). Unselected hexagons: dark navy fill, steel border.
  - Middle row, two hexagons: "איך משחקים" (left), "טעינת משחק" (right, slightly dimmed because there is no saved game).
  - Bottom row, three hexagons: a decorative dim RED-outlined hexagon (left), the MAIN button "משחק חדש" (center, filled bright cyan gradient, strong glow, the biggest and brightest element after the logo), a decorative dim CYAN-outlined hexagon (right).
Under the honeycomb, one line of grey-blue text describing the selected difficulty: "אתגר מאוזן. מתאים למשחק ראשון."
Also show, as a small inset at the bottom-left of the image, the same three difficulty descriptions for the other two choices: "יותר אנרגיה בהתחלה, פחות אויבים, והם חלשים יותר." and "הקצב המלא של היצר. לשחקנים מנוסים."
```

### B2 — Page « Comment on joue » (plein écran, ouverte depuis le menu ou le jeu)

```
SCREEN: HOW TO PLAY, full-screen overlay on the navy backdrop (95% opaque).
Header, top center, big: "איך משחקים". Tagline under it, cyan: "הוראות המשחק".
Five instruction rows stacked in a large steel-bordered plate (each row: a 58 px white pictogram on the RIGHT tinted with the row color, a bold title, then one line of body text, text right-aligned):
 1. (green tint, factory icon) title "אנרגיה: מתקני גז" — body "כל האנרגיה שלך באה ממתקני הגז. בנה אחד מיד בתור הראשון, וכל מה שלא הוצאת נשאר לך לתור הבא."
 2. (blue tint, tank icon) title "טנקים: הכוח היחיד שסוגר שער" — body "רק טנק בהסתערות הולך אל שער ומכה בו. טנק בשמירה נשאר ליד הבסיס או ליד מרכז פיקוד, וטנק בציד רודף אחרי אויבים."
 3. (red tint, enemy icon) title "הגן על מה שבנית" — body "האויב יוצא מהשערים ומכוון אל המתקנים ואל הבסיס. בונקר יורה על כל מי שמתקרב, ומרכז פיקוד מקים בועה שהאויב לא עובר."
 4. (orange tint, crack icon) title "כל אויב שנופל מסדק את השער ששלח אותו" — body "הסדקים מצוירים על השער. כשהם מלאים — המגן שלו נשבר. גם תשובה נכונה כאן, בתחילת התור, מוסיפה סדק."
 5. (cyan tint, gate icon) title "כשהמגן נשבר: עכשיו" — body "ארבעה תורות בלבד: השער פגיע ולא פורס אויבים. אם טנק בהסתערות לא יגיע בזמן — המגן חוזר."
Below: a legend block titled "מה זה מה" with ten small white pictograms and their names in a row, right to left: "מפעל גז", "גביש", "בונקר", "מרכז פיקוד", "טנק", "שער", "סדק", "אנרגיה", "יצר הרע", "הבסיס".
Bottom center: a primary blue button "סגור".
```

### B3 — Liste des parties sauvegardées

```
SCREEN: SAVED GAMES panel, centered on a dimmed board (dark veil 88%), panel ≈ 1180 × 860 px, steel border.
Title (gold, 56 px): "משחקים שלא הסתיימו". Hint under it: "לחץ על משחק כדי להמשיך".
Four rows (dark plates with steel border, 84 px tall), each row from right to left: a bold head line "תור 12 · בינוני · המבצר", a detail line "טנקים 6 · אויבים 4 · שערים 5/6", and on the far left a small date "02/11 14:30" plus a red "X" delete control.
Also show the empty state as a small inset: "אין משחק שמור · התחל משחק חדש".
Bottom: primary blue button "סגירה".
```

### B4 — Le HUD en jeu (l'écran de référence)

```
SCREEN: IN-GAME HUD over an empty navy board area. Show the fixed interface only.
TOP-RIGHT, Energy (steel-bordered plate ≈ 400 × 165 px): on its left a hexagonal badge with a white lightning bolt on cyan; on the right the label "אנרגיה" (small, top) and the value "120" (big, cyan, bold, bottom). Below the plate, in green, a transient gain "+20".
TOP-CENTER, Phases (steel-bordered plate ≈ 960 × 138 px) with a small hexagonal badge straddling its top edge containing the turn number "3" and the word "תור". Inside the plate, five phase names read RIGHT TO LEFT: "פתיחה", "הוצאה", "טנקים", "יצר הרע", "סוף התור". Each has an underline bar: the first two are DONE (cyan bar, cyan dimmed label), the third "טנקים" is CURRENT (cyan bar, label white and bold), the fourth "יצר הרע" will be RED when it is current (show it grey here), the fifth is UPCOMING (grey bar, grey label).
TOP-LEFT, three stacked plates ≈ 408 px wide:
  1. Gates, RED border: label "שערים" top-right, value "5/6" top-left in red, bold; a row of six small pointy hexagons at the bottom, five red and one dark/empty.
  2. Base, CYAN border: label "בסיס" top-right, value "182" top-left in cyan, bold; a thin cyan gauge below, 91% full, emptying towards the left.
  3. Tanks, STEEL border: label "טנקים" right, value "6/10" left. Also show a variant inset where the value is "10/10" in red (the army is full).
BOTTOM-CENTER: the primary filled blue button ≈ 480 × 100 px: "סיים תור".
BOTTOM-LEFT, two stacked buttons ≈ 365 × 76 px: red-bordered "לראות את האיום" (top) and steel-bordered "איך משחקים" (bottom).
A small hint near the end-turn button in grey: "רווח" (the Space key), and near the threat button: "Tab".
```

### B5 — Ouverture du tour (rideau)

```
SCREEN: TURN OPENING. Full-screen navy backdrop (opaque, with the red/cyan glows). Centered: a big dark pointy-top hexagon (≈ 360 px, steel border) containing the small word "תור" on top and the number "4" in cyan, bold, ≈ 110 px. Two thin steel lines above and below. Under the hexagon, right to left: "מהבסיס" in grey and "+20" in cyan bold.
Also show a second variant of the same screen where the backdrop is a 78% dark veil over a faint board instead of being opaque.
```

### B6 — Bandeaux de phase et barre d'allure

```
SCREEN: PHASE BANNERS + PACE BAR, over the HUD of B4 (keep the HUD, dimmed 30%).
A banner slides from the top center (dark plate, steel border, ≈ 900 × 140 px): title bold "שלב הטנקים", body "הטנקים שלך פועלים לפי התפקיד שנתת להם · רווח ליחידה הבאה · אנטר לדלג על הכל".
Show the three other banners as smaller insets: "שלב ההוצאה / בנה, הצב טנקים, שדרג · כשסיימת: לחץ על סיים תור", "שלב יצר הרע / האויבים מתקדמים ותוקפים · כל אויב שנופל מסדק את השער שלו" (RED border), "שלב המבנים / מתקני הגז משלמים · בונקרים יורים · גבישים מרפאים · בועות הפיקוד מתמלאות".
At the bottom center, replacing the end-turn button during automatic phases, a PACE BAR (dark plate): three toggle chips "רגיל" · "מהיר" · "בזק" (one selected, cyan), then two buttons "ליחידה הבאה" (primary blue) and "דלג על הכל" (secondary), and the key hint "Space / Enter" in grey.
```

### B7 — Carte d'enseignement (Musar)

```
SCREEN: TEACHING CARD, full-screen overlay on the navy backdrop (96% opaque), content ≈ 1400 px wide, very readable.
Header top center: "היצר" (bold, white). Tagline under it in cyan: "למד היטב · עוד תצטרך את זה". A thin steel rule.
Source line, small, gold: "סוכה נב".
Title, bold, 60 px: "הלך, אורח, בעל הבית".
Body, 37 px, generous line spacing, right-aligned: "אמרו חכמים שיצר הרע בתחילה נקרא הלך, אחר כך אורח, ולבסוף איש — בעל הבית. הוא אינו משתלט בבת אחת. הוא מתחיל בדבר קטן שנראה חסר חשיבות, ושאין שום סיבה לסרב לו."
Bottom: a small grey counter "1/2" (numbers left-to-right) and a primary blue button "הבא".
```

### B8 — La question, les réponses, le résultat

```
SCREEN: QUESTION. Same overlay as the teaching card.
Top, cyan bold: "ומה זה אצלך?".
Situation text, 39 px, right-aligned: "התחלת פעם אחת לצאת מהסדר חמש דקות לפני הזמן, כי באמת היית חייב. היום אתה יוצא כל יום, בלי לשים לב שבכלל התחלת."
Three answer cards stacked (dark plates with steel border, ≈ 1200 × 96 px, text centered 36 px): "הלך", "אורח", "בעל הבית". Show the second card in a HOVER state (lighter plate).
Show the RESULT state as a second panel on the same image: title "נכון" in green (and, inset, "לא" in red), explanation in grey-blue 36 px: "מה שהיה מוצדק פעם אחת קנה לו דריסת רגל. זה בדיוק הסדר שהגמרא מתארת.", then a reward line with a crack icon: "סדק נוסף" bold, and "הבנת אותו — וזה מסדק אותו בדיוק כמו שמפילים את שליחיו · השער הקרוב ביותר להישבר: 2/3 סדקים". Then the primary button "המשך".
```

### B9 — Le menu de case et le panneau de détails

```
SCREEN: HEX ACTION MENU + DETAILS PANEL, over the board area.
Left half: a small vertical menu that opens beside a clicked hex (dark plate, steel border, ≈ 440 × 300 px) with three stacked buttons 76 px tall: "מבט מקרוב" (dark), "פרטים" (dark), and the primary blue "השתמש באנרגיה" with the price "30" and a small lightning icon on its left. Show four refused variants of that last button as small insets, dark and greyed, labelled: "אין מספיק אנרגיה", "מחוץ לטווח", "הצבא מלא · 10/10", "כבר ברמה המרבית".
Right half: the DETAILS panel (dark plate, steel border, ≈ 560 × 900 px) for a gas factory: a 230 px photo band at the top (just a framed placeholder), a white pictogram, the title "מתקן גז", subtitle "רמה 2", a row of three small evolution thumbnails labelled "טבעי" · "רמה 1" · "רמה 2" (the current one highlighted cyan, the next one gold), then stat rows right-aligned with their values on the left: "הכנסה לתור 15", "חיים 40", a cost line with the lightning icon "30" and "היתרה אחרי 90" , and the effect sentence in 28 px: "המתקן שלך: כל האנרגיה שלך מגיעה מכאן · האויב יבוא להרוס אותו · שמור עליו · שים לב: כל 60 הכנסה ממתקנים מזרזת את השערים בתור אחד". Bottom: two buttons "אישור · רמה 2" (primary blue) and "ביטול" (secondary).
```

### B10 — Choix du rôle d'un Tank

```
SCREEN: TANK ROLE CHOICE, full-screen overlay on the navy backdrop.
Title: "בניית טנק" (also show the alternative title inset: "שינוי טנק"). Subtitle: "בחר את תפקיד הטנק".
A row of FIVE cards (dark plates, steel border, ≈ 330 × 420 px each, 24 px gaps), each with a 44 px white pictogram tinted, a bold name, a body text 26 px, and a price chip at the bottom:
 1. "שמירה" (blue, bunker/shield icon) — "נשאר ליד הבסיס — או ליד מרכז פיקוד שתבחר — ותוקף כל אויב שמתקרב · לא יוצא לשדה" — chip "חינם" (cyan).
 2. "הסתערות" (orange, gate icon) — "צועד אל השערים כדי לסגור אותם · אויב בדרך לא מסיט אותו · הדרך היחידה לנצח" — chip "חינם".
 3. "ציד" (red, enemy icon) — "רודף אחרי אויבים בכל המפה · לא נוגע בשערים" — chip "חינם".
 4. "שדרוג לרמה 2" (violet, crystal icon) — "דרגה 2: 60 חיים, 20 נזק, טווח 2 ושתי משבצות תנועה · אפשר רק בטווח של גביש" — chip "60" with the lightning icon (gold = affordable; show an inset in red = too expensive).
 5. "התקרב לגביש" (cyan, crystal icon) — "הטנק נוסע אל הגביש הקרוב ונשאר לידו · שם אפשר לשדרג אותו לדרגה 2" — chip "חינם".
One card shows a small tag "התפקיד הנוכחי" (grey) and another "בדרך".
Bottom: secondary button "ביטול" and a small secondary button "מבט מהטנק".
```

### B11 — Choix de la cible (le HUD est effacé)

```
SCREEN: TARGET PICKER. The whole HUD is HIDDEN. Only the board (empty navy) and, at the top center, a wide dark band (≈ full width, 200 px tall, 88% opaque) with a big bold title in orange: "איזה שער לתקוף?" and under it in light blue-grey 34 px: "לחץ על מטרה מסומנת · Esc כדי שהטנק יחליט לבד". Show the two alternative titles as insets: "אחרי איזה אויב לרדוף?" (red) and "על מה לשמור?" (blue). On the board area, draw three small floating pointy-hexagon markers with a white gate icon, pulsing, to show selectable targets.
```

### B12 — Le Conseiller

```
SCREEN: ADVISOR PANEL, docked at the right side of the board (dark plate, steel border, ≈ 520 × 620 px), header "מה כדאי לעשות עכשיו".
Three advice cards stacked (dark plates, ≈ 480 × 170 px), each: a bold title on the right, a reason text 26 px, a price chip with lightning icon on the left:
 1. (RED border — urgent) "בנה טנק" — "המגן של השער נשבר · זה הזמן לסגור אותו" — "40".
 2. (steel) "שדרג טנק" — "יש גביש ליד הטנק הזה · שדרוג מכפיל את הנזק שלו" — "60".
 3. (steel) "בנה גביש" — "אין גביש בשטח · בלי גביש אין שדרוג, והצבא מוגבל · טנק דרגה 2 מכה כפול וצועד מהר יותר" — "60".
Show one card in HOVER state (lighter). Other possible titles to show as a small list inset: "שנה תפקיד", "בנה בונקר", "בנה מרכז פיקוד", "בנה מתקן גז", "שלח טנק אל הגביש".
```

### B13 — Panneau d'un Shofar (porte ennemie)

```
SCREEN: GATE PANEL, docked top-right under the Energy plate (dark plate, RED border, ≈ 520 × 400 px).
Title: "השער המזרחי", subtitle "רמה 1". A red health gauge with the value "38/50" and a thin WHITE floor line on the gauge at 20% with the note (small, grey): "הקו הלבן הוא הרצפה: הנזק החוזר לעולם לא יורד מתחתיו · כל מה שהורדת מתחת לקו נשאר".
A cracks row: label "סדקים", four small crack pips, two lit orange, value "2/3".
State block A: title "המגן שלם", body "הנזק מתחלק לשניים · הפל 3 מהאויבים שלו, או ענה נכון בתחילת התור, כדי לשבור אותו".
Show state block B as an inset (red): "המגן נשבר" — "נזק מלא · לא פורס · נותרו 4 תורות · שלח טנק בהסתערות עכשיו".
A footer line: "עולה לרמה 2 בעוד" "7 תורות", and a surge alert chip in red: "גל מתקרב" "4 אויבים · עד רמה 2".
```

### B14 — Carte d'unité (combat et déplacement)

```
SCREEN: UNIT ACTION CARD, docked bottom-center above the end-turn area (dark plate, ≈ 900 × 270 px), shown during automatic phases.
Player variant (CYAN accent line on top): title "טנק הסתערות" with "רמה 2" and health "48/60"; rows right-aligned: "מטרה — השער המזרחי", "נזק — 20", "טווח — 2 משבצת", "ניידות — 3 משבצת בתור". A floating number "-20" in cyan with the caption "נזק".
Enemy variant as an inset (RED accent): title "יצר הרע" "רמה 2" "30/40"; rows "מטרה — הבסיס", "נזק — 15", "טווח — 1 משבצת", "שער המוצא — השער הצפוני־מזרחי"; floating "-15" in red with caption "חיים לבסיס". Another small inset shows the shielded damage caption "נזק · המגן שלם" with "20 ← 10" and the move caption "מתקדם".
```

### B15 — Fin de partie

```
SCREEN: GAME OVER, full-screen overlay on the navy backdrop, centered card ≈ 1060 × 730 px, steel border with a CYAN accent line on top for victory.
Victory: big title "ניצחת" in cyan (≈ 90 px), subtitle "כל השערים נסגרו. יצר הטוב גבר.". Three stat tiles in a row: "תורות 27", "שערים שנסגרו 6/6", "אויבים שחוסלו 42". A rank block: a hexagonal medal, rank name in gold "צדיק וטוב לו", rank body 28 px "לא נותר מה לכפות · כל שער נהפך, והחושך עצמו נעשה אור". Bottom: primary blue button "משחק חדש".
Defeat inset (RED accent): title "הפסדת" in red, subtitle "הבסיס נפל. יצר הרע השתלט.". Other rank names to show small: "בינוני" — "כפית את היצר בכל תור ולא נתת לו לפעול · זו מידת כל אדם, ואין למעלה ממנה בקושי", "צדיק ורע לו" — "חלק מן היצר הפכת לטוב · החלק שנותר כפוי תחתיך, אך עודנו מה שהיה".
```

### B16 — La visite d'introduction et l'écran de stratégie

```
SCREEN: INTRO TOUR CARD, a side card (dark plate, gold border, ≈ 760 × 620 px) placed at the right of an empty board, the HUD is HIDDEN. A photo band placeholder on top (framed), the title in gold 54 px "מתקן גז", body 31 px right-aligned:
"רמה 1 · 30 אנרגיה · מכניס 10 בכל תור
רמה 2 · 45 אנרגיה · מכניס 15 בכל תור
כל האנרגיה שלך מגיעה מכאן · האויב יבוא להרוס אותו
אבל היצר ניזון מהשפע שלך: כל 60 אנרגיה של הכנסה מקצרת בתור אחד את מחזור השערים"
A counter bottom-left "5 / 12", and a centered hint under the card in grey: "לחץ להמשך · Esc לדילוג". On the board area, draw a bouncing gold chevron arrow pointing down at one hex: the hex the card talks about.
Second panel on the same image: the STRATEGY screen (centered plate, cyan border, ≈ 1300 × 820 px): title gold 60 px "איך משחקים טוב", eight short lines 32 px, right-aligned (use the first four): "כל הלוח הפנימי הוא מישור ופתוח לך מההתחלה · מדבר יש רק בשתי הטבעות החיצוניות", "קודם כלכלה — אבל בזהירות: כל 60 אנרגיה של הכנסה מזרזת את כל השערים בתור אחד", "יש רק שישה מתקני גז בהתחלה · לאבד אחד זה כבר לא זניח, שמור עליהם", "אי אפשר להחזיק שישה צירים · בחר שניים, בצר אותם, ותן לשאר לבוא אליך", and a primary button "מתחילים".
```

### B17 — Petits éléments flottants et alertes

```
SCREEN: FLOATING ELEMENTS sheet on the navy backdrop, each element isolated with its name in small grey Latin letters under it:
- Damage numbers: "-20" cyan (player hit), "-15" red (enemy hit), "+20" green (healed).
- Energy gain flight: a small cyan "+15" with a lightning icon.
- Crack popups above a gate: "סדק 2/3" orange; "המגן נשבר! · 4 תורות לתקוף" red bold; "גל מתקרב!" red.
- "הבסיס נפגע!" red; "הבועה נשברה!" cyan-red.
- Threat toggle chip states: "לראות את האיום" (red border, off) and "להסתיר את האיום" (filled red, on).
- Immersive view banner (top): "מבט מהטנק" title, hint "גרור כדי להסתובב · גלגלת לזום · Esc ליציאה", button "חזרה למפה".
- Stance markers above tanks: three tiny hexagonal badges with the shield / gate / enemy icons and the words "שמירה", "הסתערות", "ציד".
- The hex selection arrow: a bouncing gold chevron.
- Land bloom banner: "הארץ פורחת" — "שער נפל · האדמה שהוא ייבש חוזרת לחיים · הצב שם טנק או בנה מרכז פיקוד כדי לבנות עליה".
```

---

## BLOC C — Règles de rendu (à coller en dernier, à chaque fois)

```
RENDER RULES:
- Output one 1920 × 1080 image, flat UI, sharp edges, no blur on text, no watermark.
- All Hebrew strings must be reproduced EXACTLY as written above, character by character, right-to-left, including the "·" separators and the final letters (ך ם ן ף ץ). Do not translate, do not add words, do not invent text.
- Numbers are left-to-right. Keys "Esc", "Tab", "Space / Enter" stay in Latin letters.
- Keep every frame, button and card as a self-contained rectangle with an EMPTY center, so it can be cut out and used as a 9-slice sprite: chamfered corners, 2 px border, glow outside, dark fill inside, nothing drawn across the middle except the text.
- Consistent lighting: borders glow softly outward; filled blue buttons have a slight top highlight.
- Leave the board area as plain navy backdrop; do not draw a 3D scene.
```

---

## BLOC D — Les icônes (génération séparée)

```
Design a set of 13 flat pictograms for a Hebrew sci-fi hex-tactics game. Output ONE sheet, each icon in its own 256 × 256 cell on a transparent (or plain #0B1630) background, PURE WHITE silhouettes, no gradients, no shadows, no outlines, bold enough to read at 20 px, same visual weight, same stroke thickness, centered with 10% margin. No left/right meaning (the UI is right-to-left). Label each cell in small grey Latin text:
1 ENERGY — a bold lightning bolt (the most used icon).
2 FACTORY — a gas factory / fuel drop with a small tower.
3 CRYSTAL — a faceted crystal / diamond.
4 TANK — a stylized hover tank seen from above, pointing up.
5 BUNKER — a shield.
6 COMMAND — a five-point star inside a thin hexagon.
7 PORTAL — a hexagonal ring (a gate), open in the middle.
8 CRACK — a jagged crack / fracture line.
9 ENEMY — a downward triangle with a hollow eye (the Yetzer Hara).
10 BASE — a massive filled square with a notch, "the thing we hold".
11 STANCE GUARD — a shield with a small base dot.
12 STANCE ASSAULT — a hexagonal ring with an arrow entering it.
13 STANCE HUNT — a crosshair.
Icons 2–6 (the four buildings and the tank) must look like one family. Icons 4 and 9 (tank vs enemy) must be clearly opposite in shape so they are distinguishable without color.
```

## BLOC E — La police (à demander en texte, pas en image)

```
Recommend a free Hebrew typeface (Google Fonts, SIL Open Font License) for a sci-fi tactics game UI that must stay readable at 24 px on phones and look strong at 90 px in titles, with full final-letter support and tabular numerals. Compare: Rubik (Bold/ExtraBold), Heebo (Black), Secular One, Assistant (ExtraBold), Varela Round, Miriam Libre, Frank Ruhl Libre. Recommend one for titles and one for body, and explain the pairing. Note that the game uses TextMeshPro with kerning disabled for Hebrew.
```

Mon avis pour te faire gagner du temps : **Rubik ExtraBold** pour les titres et **Heebo Medium** pour le corps — tous deux gratuits, complets en hébreu, avec des chiffres tabulaires, et déjà présents dans ton projet pour Rubik. Secular One a plus de caractère pour un logo mais fatigue en texte courant.

---

## Ce que tu récupères, et ce qu'on en fait

Pour chaque écran validé, découpe les éléments dans `Assets/Art/Sprites` comme tu l'as déjà fait (fond, cadres, boutons, hexagones, jauges, pastilles, traits), **en 2× de la maquette** (donc 3840 × 2160 si tu peux générer en haute définition, sinon agrandis proprement) et avec le centre des cadres vide. Les textes ne sont jamais dans les images : ils viennent du jeu. Dès que les images sont là, je branche écran par écran, sans rien reconstruire d'autre.
