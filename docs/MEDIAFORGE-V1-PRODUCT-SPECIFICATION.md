# MediaForge V1 Product Specification
## חלק 1 — חוויית ההפעלה הראשונית (First-Run & Onboarding)

**סטטוס:** Product / UX / UI Specification  
**Scope:** First launch בלבד  
**Implementation:** אין קוד במסגרת מסמך זה

---

## 1. מטרת החוויה

חוויית ההפעלה הראשונה צריכה להכניס משתמש חדש ל-MediaForge במהירות, בלי אשף התקנה כבד ובלי עומס הגדרות.

הזרם מורכב מארבעה מסכים:

1. Welcome Splash
2. Disclaimer & Acceptance
3. Quick Setup
4. Feature Tour

בסיום מסך 4 המשתמש נכנס ישירות ל-**Home / Command Center**.

---

# 2. מסך 1 — Welcome Splash

## 2.1 תיאור הפעולה

בהפעלה הראשונה נפתח חלון desktop מודרני המבוסס על WinUI 3.

הרקע מוגדר ל-Dark Mode עם בסיס חזותי כהה סביב `#0B0D10` ושכבת Mica עדינה. חומר ה-Mica מושפע באופן עדין מסביבת Windows של המשתמש, אך החלון אינו נראה כמו חלון שקוף.

במרכז החלון:

**MediaForge**

ומתחת:

**ברוכים הבאים**

ב-LTR הנוסח יהיה באנגלית, וב-RTL בעברית.

## 2.2 מנגנון זיהוי שפה

לפני הצגת מסך ההמשך, המערכת מזהה את שפת ברירת המחדל של Windows / Regional Settings.

כללים:

- Windows בעברית → ברירת מחדל `he-IL` + RTL.
- Windows באנגלית → `en-US` + LTR.
- Windows בשפה שאינה בין 20 שפות היעד → fallback ל-`en-US` + LTR.
- המשתמש עדיין יוכל לבחור שפה אחרת במסך Quick Setup.

הזיהוי מתבצע לפני טעינת ה-shell המלא כדי שהמסך הבא לא יופיע לרגע בשפה הלא נכונה.

## 2.3 Motion

רצף ה-Opening:

- חלון: Fade-in + Scale עדין.
- Mica: התייצבות חומרית קצרה.
- Logo: Fade + Scale.
- Text: Fade-up קצר.

אין:

- Bounce
- Flash
- Spinner גדול
- Progress bar
- Marketing content

משך יעד: כ-0.7–1.0 שניות.

## 2.4 Visual Reference

[Screen 1 — Welcome Splash](../mediaforge_v1_part1_mockups/MediaForge_V1_Screen_1_Welcome_Splash.png)

---

# 3. מסך 2 — Disclaimer & Acceptance

## 3.1 מטרת המסך

לפני השימוש הראשון המשתמש חייב לקבל הצהרה ברורה לגבי שימוש חוקי ואחריות המשתמש.

המסך צריך להרגיש קצר, נקי ומובן, ולא כמו EULA ארוך.

## 3.2 מבנה

המסך בנוי סביב כרטיס Fluent מרכזי:

- רוחב יעד: 620–720px.
- Radius: 20–24px.
- Padding: 32–40px.
- רקע: elevated dark surface.
- border: subtle.

בתוך הכרטיס:

### Heading

**Use MediaForge responsibly.**

### טקסט

הטקסט מסביר בקצרה:

- MediaForge וכלי open-source משולבים כגון yt-dlp ו-FFmpeg משמשים למטרות טכניות.
- השימוש בתוכנה מיועד לשימוש אישי וחוקי.
- המשתמש אחראי לוודא שיש לו זכות להוריד, לעבד או לשמור את התוכן.
- המשתמש אחראי לעמידה בזכויות יוצרים ובתנאי השירות של ספקי התוכן.
- שילוב רכיב open-source אינו מעניק למשתמש זכויות כלשהן בתוכן.

**הערה:** הנוסח המשפטי הסופי יאושר בנפרד לפני release.

## 3.3 Acceptance Control

בתחתית הכרטיס:

`☐ אני מבין ומסכים לתנאי השימוש והאחריות של MediaForge`

הכפתור:

`המשך →`

מצב התחלתי:

- Disabled.
- opacity נמוכה.
- צבע muted.
- אין אפשרות להתקדם.

לאחר סימון ה-Checkbox:

- Check animation.
- הכפתור עובר ל-active state.
- מעבר מהיר בין Disabled → Enabled.
- ה-CTA הופך ל-primary.

אין אפשרות לעקוף את המסך באמצעות לחיצה על Enter כשה-checkbox אינו מאושר.

## 3.4 Accessibility

- ה-checkbox נגיש באמצעות keyboard.
- Focus ring ברור.
- התווית כולה ניתנת להפעלה.
- מצב Disabled אינו מסתמך רק על צבע.

## 3.5 Visual Reference

[Screen 2 — Disclaimer & Acceptance](../mediaforge_v1_part1_mockups/MediaForge_V1_Screen_2_Disclaimer_Acceptance.png)

---

# 4. מסך 3 — Quick Setup

## 4.1 מטרת המסך

מסך זה מאפשר למשתמש לבחור את תצורת הבסיס של MediaForge לפני הכניסה למוצר.

המטרה היא לבצע את ההגדרות המרכזיות בפעולה אחת ברורה, בלי לפתוח את Settings.

## 4.2 Layout

המסך מחולק לקטגוריות בעלות היררכיה ברורה:

### A. Appearance
### B. Default Format
### C. Auto-Updates
### D. Language

המסך צריך להיראות מאוורר ולא כמו טופס.

## 4.3 Appearance — Theme

רכיב:

**Segmented Toggle**

אפשרויות:

- Light
- Dark

ברירת מחדל:

**Dark**

הבחירה מקבלת active pill.

המעבר בין Light ↔ Dark:

- background transition
- text contrast transition
- surface transition

אין reload.

## 4.4 Default Format

רכיב:

**Pill Buttons**

אפשרויות:

- MP3 (Audio)
- MP4 (Video)

ברירת מחדל:

**MP3**

ה-format שנבחר משפיע על ברירת המחדל במסכי ההורדה העתידיים, אך אינו מונע בחירת פורמט אחר בהמשך.

## 4.5 Auto-Updates

רכיב:

**Toggle Switch**

Label:

**Auto-Updates**

Description קצר:

**Check for and install MediaForge updates automatically.**

ברירת מחדל:

**ON**

המנגנון מיועד לעדכונים דרך GitHub API / GitHub Releases בהתאם למדיניות העדכונים של המוצר.

המשתמש יכול לכבות את האפשרות מבלי להשפיע על הורדות.

## 4.6 Manual Language Override

רכיב:

**Dropdown**

20 שפות:

1. English — `en-US`
2. עברית — `he-IL`
3. Español — `es-ES`
4. Français — `fr-FR`
5. Deutsch — `de-DE`
6. Italiano — `it-IT`
7. Português (Brasil) — `pt-BR`
8. Português (Portugal) — `pt-PT`
9. Nederlands — `nl-NL`
10. Polski — `pl-PL`
11. Türkçe — `tr-TR`
12. Svenska — `sv-SE`
13. Dansk — `da-DK`
14. Norsk — `nb-NO`
15. Suomi — `fi-FI`
16. Українська — `uk-UA`
17. 日本語 — `ja-JP`
18. 한국어 — `ko-KR`
19. 简体中文 — `zh-CN`
20. العربية — `ar-SA`

## 4.7 Runtime Language Switch

כאשר המשתמש משנה שפה:

- אין Restart.
- אין סגירת חלון.
- אין Flash לבן.
- resource context מתחלף בזמן ריצה.
- הטקסטים מתעדכנים.
- Layout direction מתחלף.

לדוגמה:

English / LTR → עברית / RTL

האנימציה:

- fade קצר
- translate אופקי עדין
- reflow של layout

## 4.8 RTL Rules

כאשר נבחרת עברית או ערבית:

- alignment עובר ל-Start/End.
- navigation hierarchy מתהפכת.
- controls נשארים סמנטיים.
- directional icons מתהפכים רק כאשר כיוון הפעולה מחייב זאת.
- URLs ו-technical strings נשארים LTR.
- מספרים מוצגים בפורמט טבעי לשפה.

## 4.9 Completion Action

CTA יחיד:

`המשך →`

הכפתור מופעל כאשר למסך יש ערכי ברירת מחדל תקפים, ולכן אין צורך למלא שדות חובה.

## 4.10 Visual Reference

[Screen 3 — Quick Setup](../mediaforge_v1_part1_mockups/MediaForge_V1_Screen_3_Quick_Setup.png)

---

# 5. מסך 4 — Feature Tour

## 5.1 מטרת המסך

Feature Tour קצר שמציג למשתמש את שלושת היכולות המרכזיות לפני הכניסה הראשונה.

הוא אינו tutorial ארוך.

יש בדיוק **3 slides**.

## 5.2 Slide 1 — Smart Paste

### Visual

המחשה של שורת ההדבקה המרכזית.

המשתמש מבין:

**Paste a link → MediaForge resolves it.**

### Copy

**Paste a link. MediaForge handles the rest.**

## 5.3 Slide 2 — Advanced Download Queue

### Visual

המחשה של Queue עם:

- מספר הורדות
- progress
- speed
- ETA
- pause
- resume
- retry

### Copy

**Manage every download in one powerful queue.**

## 5.4 Slide 3 — Media Explorer

### Visual

המחשה של סייר המדיה:

- library
- folders
- artwork
- media items
- local organization

### Copy

**Keep your media organized, beautiful and easy to find.**

## 5.5 Navigation

בתחתית:

Pagination Dots:

**● ○ ○**

אחרי מעבר:

**○ ● ○**

ובסוף:

**○ ○ ●**

Controls:

- Back
- Next

בשקופית השלישית:

`בוא נתחיל ←`

ב-LTR:

`Let's Start →`

## 5.6 Final Transition

לחיצה על CTA האחרון מפעילה:

- Feature Tour fade-out.
- background crossfade.
- Command Center fade-in.
- Hero composer עולה עם focus.

אין מסך ביניים נוסף.

## 5.7 Skip Policy

ה-Feature Tour אינו מוצג מחדש בכל פתיחה.

המשתמש יכול לסגור אותו בעתיד דרך Help / About / onboarding מחדש, אך אחרי השלמה הוא מסומן כבוצע.

## 5.8 Visual Reference

[Screen 4 — Feature Tour](../mediaforge_v1_part1_mockups/MediaForge_V1_Screen_4_Feature_Tour.png)

---

# 6. Full User Flow

1. Application Launch
2. Welcome Splash
3. Automatic Language Detection
4. Disclaimer & Acceptance
5. Quick Setup
6. Feature Tour — Slide 1
7. Feature Tour — Slide 2
8. Feature Tour — Slide 3
9. Let's Start
10. Home / Command Center

---

# 7. First-Run State Rules

## 7.1 First Launch

החוויה המלאה מוצגת רק כאשר אין onboarding state שמור.

## 7.2 Returning User

משתמש שכבר סיים onboarding נכנס ישירות ל-Home.

## 7.3 Interrupted Onboarding

אם האפליקציה נסגרת במהלך התהליך:

- עם הפעלה מחדש חוזרים למסך האחרון שהושלם.
- אין איפוס להגדרות שכבר נשמרו.

## 7.4 Language Persistence

השפה שנבחרה ב-Quick Setup הופכת לשפה המועדפת.

שינוי ידני עתידי ב-Settings גובר על זיהוי Windows.

---

# 8. Accessibility Requirements

כל ארבעת המסכים חייבים לתמוך ב:

- Keyboard navigation.
- Visible focus.
- Screen reader labels.
- High Contrast.
- Reduced Motion.
- Logical Start / End layout.
- Color-independent status.

Reduced Motion:

במקום transitions מורכבים יש להשתמש ב-opacity transition קצרה.

---

# 9. Responsive Rules

Reference:

**1440 × 900**

המסכים צריכים להישאר שימושיים ב:

**1180 × 760**

בחלון צר יותר:

- שתי עמודות יכולות להפוך לטור יחיד.
- cards מתכווצים.
- text wrapping נשלט.
- CTA נשאר גלוי.
- אין horizontal scrolling של כל ה-window.

---

# 10. Visual Acceptance Criteria

החלק נחשב מאושר כאשר:

### Welcome Splash
- Mica visible.
- Dark Mode.
- Logo centered.
- Fade & Scale smooth.
- Language detected before next screen.

### Disclaimer
- Central Fluent Card.
- Checkbox required.
- Continue disabled before acceptance.
- Continue activates immediately after acceptance.

### Quick Setup
- Theme selector.
- MP3/MP4 selector.
- Auto-Updates toggle.
- 20-language dropdown.
- Runtime RTL/LTR switching.
- No restart.

### Feature Tour
- Exactly 3 slides.
- Smart Paste.
- Advanced Download Queue.
- Media Explorer.
- Pagination dots.
- Next / Back.
- Final Let's Start CTA.
- Direct transition to Command Center.

---

# 11. Design Tokens — Part 1

**Base Background:** `#0B0D10`  
**Window Material:** Mica  
**Card Radius:** 20px target  
**Hero Radius:** 24px target  
**Primary Button Height:** 44–48px  
**Standard Control Radius:** 10–14px  
**Reference Window:** 1440 × 900  
**Minimum Target:** 1180 × 760

### Motion

- Micro: 120–180ms
- Standard: 180–240ms
- Page: 240–360ms
- Hero: up to 450ms

---

# 12. Product Principle for Part 1

**First Run = 4 screens, 1 clear decision per screen, zero technical friction.**

The user should reach the Command Center feeling that MediaForge is already configured and ready for work.



---

# חלק 2 — מסך הבית (Home)

**עיקרון:** מסך אחד, נקי, ללא Sidebar וללא עומס.

## 1. ברכת שלום
- בחלק העליון ובמרכז המסך.
- טקסט גדול וחגיגי.
- עברית: **שלום**.
- אנגלית: **Hello**.
- ללא תת-כותרת וללא טקסט עזר.

## 2. ריבועים של תיקיות ראשיות
- במרכז המסך.
- כרטיסים מרובעים, מודרניים ומרווחים.
- ברירת מחדל:
  - **מוזיקה**
  - **סרטונים**
  - **הורדות**
- לחיצה על כרטיס פותחת את התיקייה המתאימה.

## 3. הוספת תיקייה ראשית
- כרטיס מרובע נוסף לצד התיקיות.
- במרכזו סימן **+** גדול ונקי.
- לחיצה מאפשרת להוסיף תיקייה ראשית חדשה של המשתמש.
- לאחר ההוספה, הכרטיס החדש מופיע במסך הבית.

## 4. Settings
- כפתור גלגל שיניים יחיד.
- קטן, אלגנטי ונקי.
- ממוקם בפינה.
- לחיצה אחת פותחת את הגדרות התוכנה.

## כללי עיצוב
- **Dark Mica** בסגנון Windows 11.
- בסיס כהה סביב **#0B0D10**.
- ללא Sidebar.
- ללא Help.
- ללא Favorites.
- ללא History.
- ללא Dashboard.
- ללא Quick Tip.
- ללא URL Composer.
- ללא טקסטים מיותרים.
- ללא חלונות קופצים כחלק ממסך הבית הרגיל.

## מבנה סופי
**שלום → תיקיות ראשיות → + הוסף תיקייה → Settings**

זהו המבנה המחייב של Home ב-MediaForge V1.
