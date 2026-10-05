# Requests — חיפוש, סינון ומיון

הוספת יכולת **חיפוש, סינון, מיון ו-pagination** ל-Requests, עם API ואכיפת הרשאות בצד השרת וממשק Angular.
נבנה על גבי ה-Backend הקיים (.NET 8 / ASP.NET Core / EF Core) **בלי החלפת טכנולוגיה**, תוך שמירה על ה-Clean Architecture המקורי.

## נקודות מפתח במימוש

- **הרשאות נאכפות בתוך ה-query, בשרת.** משתמש רגיל רואה רק Requests שבבעלותו (`OwnerId`) או שהוקצו אליו (`AssignedToUserId`); Administrator רואה הכל. תנאי ההרשאה מורכבים ל-`IQueryable` **לפני כל עיבוד נוסף של השאילתה** — סינון, מיון ו-pagination — ולכן אף פילטר, מיון או cursor לא יכולים "לדלוף" רשומות של משתמש אחר. הזהות והתפקיד נקראים מ-claims של JWT שאומת בצד השרת, ולא מקלט הבקשה.
- **מתוכנן לקנה מידה של מיליוני רשומות.** ה-repository שודרג מ-`GetAllAsync()` (שטען הכל לזיכרון) ל-`Query()` המחזיר `IQueryable<Request>`, כך שההרשאה + סינון + מיון + pagination **מורכבים לפני ה-materialization** ומאפשרים ל-relational provider לבצע את העבודה בצד השרת. ה-pagination מבוסס **Keyset (Cursor)** — ביצועים יציבים יותר בדפים עמוקים, במיוחד כאשר קיים אינדקס מתאים (ראו "החלטה טכנית מרכזית").
- **טיפול בקלט לא חוקי.** `RequestQueryValidator` אוסף את **כל** שגיאות הקלט יחד (לא נעצר בשגיאה הראשונה) ומחזיר `ValidationProblemDetails` (RFC 7807, HTTP 400) עם מפתח לכל שדה. cursor פגום/לא תואם למיון ממופה ל-HTTP 400 ברור.
- **חיפוש, סינון ומיון** לפי: Request Number (חיפוש חלקי), Status (יחיד או מרובה), טווח תאריכי יצירה, וסוג בקשה — עם מיון בכל השדות ושני הכיוונים.
- **ממשק Angular** עם טופס סינון, מיון, טבלת תוצאות, וחיווי טעינה / שגיאה / "אין תוצאות".
- **Dashboard (תוספת מעבר לנדרש).** טאב סקירה נוסף על מסך החיפוש, מבוסס `POST /api/requests/stats` — endpoint שמבצע aggregation ברמת ה-DB על ה-Authorized_Set של המשתמש (אדמין על הכל, משתמש רגיל רק על שלו). מציג שני doughnut charts (לפי Status ולפי RequestType), כרטיסי KPI ו-progress, עם drill-down מפרוסת גרף ישירות לחיפוש מסונן.

---

## 1. הרצת הפרויקט

### דרישות מקדימות
- .NET 8 SDK
- Node.js 20+ (כולל npm)

### Backend

**Visual Studio:** לפתוח את `Backend/Requests.sln` (ה-Startup Project הוא `Requests.Api`) ולהריץ עם F5 — נפתח ישירות ב-Swagger.

**Command Line:**
```bash
dotnet run --project Backend/src/Requests.Api/Requests.Api.csproj
```

| שירות | Visual Studio (IIS Express) | Command Line |
|---|---|---|
| API | `https://localhost:44301` | `http://localhost:60702` |
| Swagger (Development) | `https://localhost:44301/swagger` | `http://localhost:60702/swagger` |
| Health | `https://localhost:44301/health` | `http://localhost:60702/health` |

> פורט ה-HTTP (`60702`) תואם לכתובת ה-API שמוגדרת ב-Frontend. יש להריץ את ה-Backend לפני ה-Frontend.

### Frontend
```bash
cd Frontend/requests-web
npm ci
npm start
```
האפליקציה זמינה ב-`http://localhost:4200`. כתובת ה-API מוגדרת ב-`src/environments/environment.ts`.

### התחברות

חשבון מנהל נזרע אוטומטית בעת ההפעלה (מתוך `Seed:Admin`), כדי שתוכלו לחוות מיד את תצוגת ה-Administrator (רואה את כל ה-Requests):

| שדה | ערך |
|---|---|
| שם משתמש | `admin` |
| סיסמה | `admin` |

> ⚠️ החשבון מיועד להרצה מקומית בלבד ואינו מתאים לסביבת Production.

> משתמש רגיל יכול להירשם דרך מסך ההרשמה (`POST /api/auth/register`); הוא יראה רק Requests שבבעלותו או שהוקצו אליו — כך ניתן להשוות בין שתי התצוגות.

---

## 2. הרצת בדיקות

**Backend** (xUnit — 82 בדיקות): לוגיקה עסקית, חיפוש, סינון, מיון, keyset pagination, cursor, ואכיפת הרשאות (כולל כיסוי ייעודי ב-`RequestAuthorizationTests` — owner-only, assignee-only, לא-owner-ולא-assignee, אדמין רואה הכל, ואכיפה *לפני* הסינון כך שסינון לא דולף רשומות של משתמשים אחרים).
```bash
dotnet test Backend/tests/Requests.Tests/Requests.Tests.csproj
```

**Frontend** (Jasmine / Karma — 40 בדיקות):
```bash
cd Frontend/requests-web
# הרצה חד-פעמית ב-headless (מתאים ל-CI)
npm test -- --watch=false --browsers=ChromeHeadless
```
> למצב watch אינטראקטיבי: `npm test`.

---

## 3. טכנולוגיות שנבחרו ומדוע

| טכנולוגיה | מדוע |
|---|---|
| **.NET 8 / ASP.NET Core** (Backend) | ה-stack הקיים; Web API מבוסס controllers, ברור ובר-בדיקה. |
| **EF Core 8** | כתיבת queries באמצעות `IQueryable`, המאפשרת ל-relational provider לתרגם את ה-query ולבצע את העבודה בצד השרת. |
| **EF Core InMemory** | מאפשר הרצה עצמאית ללא מסד חיצוני (ראו "הנחות" ו"מה לא הספקתי"). |
| **JWT Bearer** | Authentication stateless; ה-claims נחתמים ומאומתים בצד השרת. |
| **Serilog** | לוגים מובנים ל-console, בלי חשיפת מידע רגיש. |
| **Angular 20** (Frontend) | standalone components, Signals, OnPush — ניהול state מקומי בלי ספריית state כבדה. |
| **PrimeNG** · **@ngx-translate** | רכיבי UI מוכנים (טבלה, multi-select, calendar) + i18n ללא מחרוזות קשיחות (תוספת מעבר לנדרש). |
| **Chart.js** (דרך PrimeNG `p-chart`) | גרפי ה-doughnut ב-Dashboard (תוספת מעבר לנדרש). |
| **xUnit · Jasmine/Karma** | סביבות הבדיקה המקובלות ל-.NET ול-Angular. |

---

## 4. הנחות

- המשתמש מזוהה באמצעות JWT; ה-id והתפקיד (admin) נקראים מ-claims שאומתו בצד השרת, לא מקלט הבקשה.
- משתמש רגיל רואה רק Requests שבבעלותו או שהוקצו אליו; Admin רואה הכל. ההרשאה נאכפת בתוך ה-query, לפני הסינון, המיון וה-pagination.
- תאריכים נשמרים ומטופלים כ-UTC; `createdFrom`/`createdTo` מנורמלים ל-UTC לפני ההשוואה.
- ה-pagination הוא forward-only (Keyset + Cursor), ללא ספירת סך-הכול וללא קפיצה לעמוד שרירותי.
- מיון לפי Status/RequestType הוא **אלפביתי לפי שם הערך** (כפי שמוצג ב-UI).
- **מסד הנתונים נשאר EF Core InMemory כפי שהגיע בפרויקט הבסיסי** (לא נדרש מסד רלציוני במבחן). הבחירה מאפשרת הרצה עצמאית ללא מסד חיצוני. קוד ה-query נכתב כך שיתורגם נקי למסד רלציוני; המעבר ל-SQL Server / PostgreSQL ידרוש התאמת ה-`DbContext` והוספת אינדקסים מתאימים, וכן בדיקות אינטגרציה מול ה-provider הרלציוני. האינדקסים המומלצים:
  ```
  (OwnerId, CreatedAt, Id)          — הרשאה + מיון ברירת מחדל
  (AssignedToUserId, CreatedAt, Id) — הרשאה + מיון ברירת מחדל
  (Status)                          — סינון לפי סטטוס
  (RequestType)                     — סינון לפי סוג
  (RequestNumber)                   — חיפוש לפי Request Number; האינדוקס תלוי באופן החיפוש וב-provider הרלציוני
  ```

---

## 5. החלטה טכנית מרכזית — Keyset Pagination במקום Offset

בחרתי ב-**Keyset Pagination** (Cursor) ולא ב-`Skip/Take` מבוסס מספר-עמוד.

**החלופה — Offset** (`page=10&pageSize=50`) פשוטה יותר, אך ככל שה-offset גדל, בסיס הנתונים נדרש לעבד ולדלג על מספר הולך וגדל של רשומות, והתוצאות עלולות לזוז כשמתווספים נתונים.

**הבחירה:** מכיוון שהתרחיש מניח מיליוני רשומות, הלקוח שולח Cursor המתאר מהיכן להמשיך, וה-query משתמש בתנאי `WHERE` על ערכי המיון של השורה האחרונה + `Id` כ-tie-breaker. **יתרונות:** ביצועים יציבים יותר בדפים עמוקים, ועמידות טובה יותר לשינויים בנתונים בין בקשות paging. **חיסרון:** אין קפיצה לעמוד ספציפי ואין ספירת סך-הכול.

האינדקסים המומלצים מפורטים בסעיף "הנחות" לעיל, וה-trade-offs של InMemory ושל מיון ה-enum מתועדים ישירות בהערות הקוד ב-`RequestQueryBuilder`.

---

## 6. מה לא הספקתי ואיך הייתי ממשיך

כל דרישות המבחן מומשו. הסעיף הזה מפרט **שיפורים אופציונליים מעבר לנדרש** — לא דרישות שהוחמצו — ואת הצעדים הבאים שהייתי עושה:

- **מסד רלציוני + בדיקת תרגום (מעבר לנדרש).** המבחן לא דרש מסד רלציוני, והפרויקט הבסיסי הגיע עם EF Core InMemory — נשארתי עם הבחירה הזו כדי לאפשר הרצה עצמאית ללא מסד חיצוני. חשוב לציין: ה-InMemory provider אינו מתרגם `EF.Functions.Like`, השוואות תאריכים ומיון מחרוזות כמו provider רלציוני, ולכן יכול להסתיר הבדלי התנהגות. קוד ה-query נכתב מראש כך שיתורגם נקי למסד רלציוני; כצעד הבא, לקראת היעד של "מיליוני רשומות", הייתי מוסיף smoke test מול SQLite in-memory (או Testcontainers עם SQL Server) שמוודא שה-query pipeline מתורגם ומחזיר תוצאות נכונות, ומגדיר את האינדקסים שבסעיף "הנחות".
- **הגנת login (מעבר לנדרש).** Rate limiting ו-account lockout לחסימת brute-force, בתוספת refresh tokens.
---

## חלק ב' — תכנון ארכיטקטורה ותקשורת בין שירותים

חלוקה אפשרית לפי Bounded Contexts, התועלות וה-trade-offs, ותקשורת אמינה לתרחיש ה-Notification
(Transactional Outbox, RabbitMQ, Idempotency, Retry/DLQ, והתמודדות עם כשלים):

- **[docs/architecture.html](docs/architecture.html)** — מסמך מעוצב עם תרשימים (נפתח בדפדפן; לרינדור התרשימים נדרש אינטרנט, או ייצוא ל-PDF).
- **[docs/architecture.md](docs/architecture.md)** — אותו תוכן ב-Markdown (מתרנדר עם תרשימים ב-GitHub / Azure DevOps).
