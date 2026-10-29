# Green & Tasty: автотести групового pre-order

**Напрям:** Напрям 1 «Груповий pre-order», рівні 1, 2 і 3 (перегляд учасників, підтвердження, видалення учасника).
Тестую власну реалізацію: бекенд з ДЗ5 (гілка `hw5-backend`) і екран Owner з ДЗ6 (гілка `hw6-frontend`), обидва розгорнуті в моєму AWS
(фронтенд на CloudFront, API на API Gateway + Lambda).

**Підхід:** BDD + Screenplay.
- Сценарії написані на Gherkin (Reqnroll), кроки нічого не знають про HTTP чи локатори, вони лише роздають акторам задачі й питання.
- Screenplay: `Actor` має здатності (`CallTheApi`, `BrowseTheWeb`, `JoinAsGuest`, `HaveAnAccount`), виконує задачі (`BookTable`, `JoinGroup`, `SubmitGroupPreOrder`, `RemoveGuestInTheApp`…) і відповідає на питання (`TheGroupSnapshot`, `TheRestaurantOrder`, `TheWaiterPreOrder`…).
- Для UI є page objects з локаторами (`Ui/Pages`), задачі Screenplay працюють через них.

**Стек:** C# / .NET 10, Reqnroll 3 + TUnit, Playwright for .NET (Chromium), Allure.Reqnroll.

Чому TUnit, а не NUnit: на воркшопі показували саме TUnit і Reqnroll. Перед тим як будувати фреймворк, я перевірив зв'язку `Reqnroll.TUnit` на пробному проєкті: сценарії (включно з рядками Scenario Outline) ідуть паралельно, теги Gherkin стають категоріями TUnit і по них працює фільтр, TRX і HTML-звіт є з коробки, Allure.Reqnroll пише результати так само, як із NUnit. Проблем не знайшов, тому лишив TUnit.

## Що покрито

52 сценарії: 36 API і 16 UI. Піраміда як у ДЗ: через UI йдуть основні сценарії (Owner бачить учасників і суми, підтверджує, видаляє гостя, офіціант бачить замовлення, наскрізний сценарій), через API йдуть авторизація, валідація й підготовка даних (гості, їхні кошики, особисте замовлення Owner).

| Рівень | Файл | API | UI | Що перевіряється |
|---|---|---|---|---|
| L1 | `Features/L1_ViewParticipants` | 9 | 6 | учасники, страви, кількості, суми по кожному й загальна; сума змінюється разом із кошиком; непідтверджений кошик і учасник без страв; порожня група; доступ лише для Owner (інший клієнт, офіціант, гість, анонім); неіснуюча група |
| L2 | `Features/L2_SubmitPreOrder` | 15 | 5 | успішний submit: лише підтверджені кошики, однакові страви об'єднані, order `SubmittedByCustomer`, група `Submitted`; офіціант бачить замовлення; кожна умова, за якої submit неможливий; повторний submit; submit не від Owner; власна сесія Owner |
| L3 | `Features/L3_RemoveParticipant` | 12 | 5 | видалення одного з кількох, підтвердженого, останнього учасника; учасник іншої групи (400); неіснуючий учасник (404); видалення не від Owner; наскрізний сценарій від кошиків гостей до екрана офіціанта |

Для кожного рівня є і позитивні, і негативні сценарії. Комбінації «хто × очікувана відповідь» зроблені через Scenario Outline / Examples.

### Теги

| Тег | Що означає |
|---|---|
| `@L1`, `@L2`, `@L3` | рівень ДЗ |
| `@api`, `@ui` | шар піраміди |
| `@smoke` | головний позитивний сценарій кожної фічі (8 сценаріїв) |
| `@e2e` | наскрізний сценарій рівня 3 |
| `@waiter` | сценарії, де офіціант працює в UI (для них окрема локація, див. нижче) |
| `@known-defect` | сценарії, які падають через знайдені дефекти (див. «Знайдені дефекти») |

## Структура

```
aqa/restaurant-aqa-framework/
├── Features/                 сценарії Gherkin, по теці на рівень (API і UI окремими файлами)
├── StepDefinitions/          кроки Reqnroll і хуки (підготовка стенду, докази для впалих тестів, прибирання)
├── Screenplay/
│   ├── Core/                 Actor, Cast, IAbility, ITask, IQuestion
│   ├── Abilities/            CallTheApi, BrowseTheWeb, JoinAsGuest, HaveAnAccount, TrackTestData
│   ├── Tasks/                задачі API (бронювання, запрошення, кошики, submit, видалення) і Tasks/Ui
│   └── Questions/            питання до API і до UI
├── Ui/Pages/                 page objects: вхід, бронювання клієнта, екран групи, дашборд і pre-order офіціанта
├── Api/                      моделі відповідей, журнал запитів
├── Support/                  налаштування (.env), дані прогону (локації, меню, офіціанти), унікальні дані, браузер
├── scripts/                  встановлення Chromium, генерація Allure, підсумок для CI
├── .env.example              шаблон налаштувань
└── RestaurantAqa.csproj
```

## Налаштування

Потрібні .NET 10 SDK і (для звіту Allure) Java 8+.

```bash
cd aqa/restaurant-aqa-framework
cp .env.example .env          # і вписати свої адреси
dotnet build
bash scripts/install-browsers.sh   # або: pwsh bin/Debug/net10.0/playwright.ps1 install chromium
```

Змінні в `.env` (кожну можна передати й змінною оточення, вона має пріоритет):

| Змінна | Що це |
|---|---|
| `BASE_URL` | адреса розгорнутого фронтенду (CloudFront) |
| `API_URL` | адреса API з назвою stage, наприклад `https://<id>.execute-api.us-west-2.amazonaws.com/develop` |
| `TEST_EMAIL_PREFIX`, `TEST_EMAIL_DOMAIN` | з них складаються e-mail тестових користувачів: `aqa.<run>.<роль>.<випадкове>@example.com` |
| `HEADLESS`, `SLOW_MO_MS` | режим браузера |
| `UI_TIMEOUT_MS`, `EXPECT_TIMEOUT_MS` | таймаути дій Playwright і перевірок `Expect` |
| `TRACE`, `VIDEO` | `on`, `off` або `retain-on-failure` |
| `TEST_PARALLELISM`, `UI_MAX_SESSIONS` | скільки сценаріїв іде одночасно (3) і скільки з них можуть тримати браузер (2) |

Паролів і облікових записів у налаштуваннях немає: усіх користувачів тести створюють самі.

## Запуск

Усі сценарії:

```bash
dotnet run
```

Підмножина за тегом чи рівнем (фільтр TUnit, теги Gherkin стають категоріями):

```bash
dotnet run -- --treenode-filter "/*/*/*/*[Category=L2]"                       # увесь рівень 2
dotnet run -- --treenode-filter "/*/*/*/*[Category=api]"                      # лише API
dotnet run -- --treenode-filter "/*/*/*/*[Category=smoke]"                    # smoke
dotnet run -- --treenode-filter "/*/*/*/*[(Category=L3)&(Category=ui)]"       # UI рівня 3
dotnet run -- --treenode-filter "/*/*/*/*[Category!=known-defect]"            # без сценаріїв з відомими дефектами
```

Те саме працює і через `dotnet test -- --treenode-filter ...` (у `global.json` увімкнено Microsoft.Testing.Platform).
Корисні опції: `--output Detailed` (кроки сценаріїв у консолі), `--report-trx`, `--maximum-parallel-tests N`.

## Звіти і матеріали для розбору

Усе складається в `TestResults/` (тека в `.gitignore`):

| Що | Де |
|---|---|
| результати Allure | `TestResults/allure-results` |
| HTML-звіт Allure | `bash scripts/allure-report.sh`, потім відкрити `TestResults/allure-report/index.html` (один файл, відкривається без сервера). Або `allure serve TestResults/allure-results` |
| TRX і HTML-звіт TUnit | тека з `--results-directory`, за замовчуванням `bin/Debug/net10.0/TestResults` |
| докази для впалих сценаріїв | `TestResults/artifacts/<сценарій>/`: скриншот сторінки (`*-failure.png`), Playwright trace (`*-trace.zip`), відео, якщо `VIDEO` увімкнено, і `api-log.txt` з усіма запитами сценарію |

Скриншот, trace і журнал запитів також прикріплені до сценарію в Allure (розділ Tear down). Trace відкривається командою
`pwsh bin/Debug/net10.0/playwright.ps1 show-trace <файл>` або на https://trace.playwright.dev.

## CI

Workflow `.github/workflows/aqa-tests.yml` (GitHub Actions):
- запускається на push у гілку `hw8-aqa`, якщо змінено `aqa/**` чи сам workflow, і вручну (`workflow_dispatch`) з вибором тегу або рівня (`all`, `smoke`, `api`, `ui`, `L1`, `L2`, `L3`, `e2e`, `waiter`, `known-defect`) чи власного фільтра TUnit;
- кроки: .NET 10 → build → Chromium для Playwright → тести → Allure-звіт → підсумок у Summary → артефакти;
- крок з тестами має `continue-on-error: true`, тож упалий сценарій не зупиняє пайплайн, звіт і артефакти збираються завжди (`if: always()`);
- адреси стенду беруться зі змінних репозиторію `vars.BASE_URL` і `vars.API_URL` (Settings → Secrets and variables → Actions → Variables). Секретів workflow не потребує;
- артефакти прогону: `allure-report` (готовий HTML), `test-results` (allure-results і TRX), `failure-evidence` (скриншоти, trace, відео й журнали API впалих сценаріїв).

## Тестові дані і прибирання

- Перед прогоном (`[BeforeTestRun]`) створюються дві локації, меню з чотирьох страв і два офіціанти (зареєстрований користувач + `POST /waiters`). Друга локація лише для сценаріїв `@waiter`: дашборд офіціанта робить по три запити на кожне його бронювання, тож там мають бути одиниці бронювань, а не всі бронювання прогону.
- Кожен сценарій реєструє власного Owner, бронює **свій** столик (тому паралельні сценарії не перетинаються в часі) на `now + 3 дні`, а гості приєднуються за справжнім запрошенням (`invite-token` → `/guest/invites/{token}` → `POST /guest/sessions`).
- Після сценарію бронювання скасовується. Після прогону видаляються страви і обидві локації з `force=true`, разом зі столиками й записами офіціантів.
- Що API видалити не дозволяє, лишається: користувачі Cognito, скасовані бронювання, групи, гостьові сесії й замовлення. Тому всі e-mail унікальні на кожен запуск і на тестовому домені `example.com`. Листів Cognito не надсилає: бекенд підтверджує користувача сам (`AdminConfirmSignUp`), автоверифікації пошти в пулі немає.
- Столики не видаляються після кожного сценарію, а лише разом із локацією наприкінці: інакше ламається дашборд офіціанта (дефект 6).

## Поведінка, не визначена в ДЗ

Де ДЗ не задає код чи текст, тести перевіряють фактичну поведінку мого бекенду з ДЗ5:

| Випадок | Відповідь |
|---|---|
| Група без учасників | `200 {"participants": [], "total": 0}`, як і вимагає ДЗ |
| `total` у списку учасників | сума по всіх учасниках, включно з непідтвердженими кошиками. На екрані Owner Grand Total рахується лише з підтверджених кошиків, бо саме вони підуть у ресторан |
| Список, submit або видалення від клієнта, який не є Owner, чи від офіціанта | `403`: `Only the reservation owner can perform this action.` (для submit: `Only the reservation owner can submit the group pre-order.`) |
| Те саме без токена або з токеном гостьової сесії | `401 {"message": "Unauthorized"}` від Cognito authorizer |
| Неіснуюча група | `404 Group reservation was not found` |
| Submit з неіснуючим бронюванням | `404 Reservation not found.` |
| Submit групи чужого бронювання (того самого Owner) | `400 Group reservation does not belong to this reservation.` |
| Жодного підтвердженого кошика | `400 None of the guest sessions has been submitted yet.` |
| Підтверджені кошики без страв | через API такого стану не досягти: гість не може підтвердити порожній кошик (`400 The cart is empty`). Тест перевіряє саме цю перешкоду і те, що група потім не підтверджується |
| Немає замовлення (особистого pre-order Owner) | `400 Pre-order for reservation '<id>' was not found.` Страви групи додаються в це замовлення, тому для успішного submit Owner спершу робить pre-order (`POST /orders`) |
| Повторний submit | `400 Group reservation was submitted.`, `errorCode: GroupReservationSubmitted`; склад замовлення не змінюється |
| Зміна кошика гостем після submit | `409 Group reservation was submitted.` |
| Видалення останнього учасника | `200`, у відповіді `participants: []` і `total: 0` (якщо Owner ще не відкривав свою сесію; інакше лишається лише сесія Owner) |
| Видалення власної сесії Owner | `400 Owner session cannot be removed from the group.` |
| Видалення після submit | `400 Group reservation was submitted.` |
| Учасник іншої групи / неіснуючий учасник | `400 Guest session does not belong to this group reservation.` / `404 Guest session with id <id> not found.` |
| Ліміт гостей | бронювання на N гостей приймає N − 1 гостьову сесію, одне місце займає Owner |

## Знайдені дефекти

Код ДЗ5 і ДЗ6 я не виправляв: сценарії, які натрапляють на дефект, падають і мають тег `@known-defect`.

1. **Після підтвердження Owner більше не може відкрити екран групи** (ДЗ5 + ДЗ6). Фронтенд знаходить групу через `GET /reservations/{id}/invite-token`, а бекенд для підтвердженої групи відповідає `400 Group reservation was submitted.` Після перезавантаження сторінки замість «Sent to Restaurant» Owner бачить «Couldn't load the group pre-order». Сценарій: *L2 UI. Submitted group stays visible and read-only after the page is reopened*.
2. **Коли всі місця в групі зайняті, Owner не може відкрити екран групи** (ДЗ5 + ДЗ6). Фронтенд бере дані групи з гостьового `GET /guest/invites/{token}`, а той для заповненої групи відповідає `400 GuestLimitExceeded`. Сценарій: *L1 UI. Owner opens the page when every seat in the group is taken*.
3. **Owner не може додати страви у власну сесію групи** (ДЗ5). `GET /reservations/{id}/guest-session-id` створює сесію Owner без токена, а маршрути кошика вимагають токен гостьової сесії. З токеном Owner `POST /guest/sessions/{ownerSessionId}/dishes` падає з `500 Internal Server Error` (замість 401/403), бо бекенд намагається розібрати JWT як base64. Тому картка «My Pre-Order» завжди порожня, а страви Owner потрапляють у замовлення лише через особистий pre-order. Сценарій: *L2 API. Owner adds dishes to their own session in the group*.
4. **Картка «My Pre-Order» з'являється із запізненням до 15 с** (ДЗ6). При першому відкритті сторінка паралельно просить список учасників і `guest-session-id`, який цю сесію і створює. Список часто приходить раніше, і картка Owner з'являється лише після фонового оновлення. Окремого сценарію немає, бо результат залежить від того, яка відповідь прийде першою; UI-кроки чекають картки до 25 с. Я згадував це обмеження ще у звіті до ДЗ6, тести його підтвердили.
5. **Відповідь DELETE іноді ще містить видаленого учасника** (ДЗ5). Після видалення бекенд збирає новий список через запит до GSI `GroupReservationId`, а глобальні індекси DynamoDB оновлюються з затримкою. Тому відповідь на `DELETE /guest/groups/{id}/sessions/{sessionId}` часом повертає старий список, і на екрані Owner гість після «… was removed from the reservation» лишається до наступного фонового оновлення. Проявляється не на кожному прогоні; в API це ловить сценарій *L3 API. Removing one participant of several* (крок про відповідь DELETE). Ті самі GSI-читання дають невелику затримку і для `GET …/sessions`, тож перевірки стану через GET повторюються кілька секунд.
6. **Дашборд офіціанта ламається, якщо стіл бронювання видалено** (код з архіву організаторів, не ДЗ5). `GET /reservations` для офіціанта віддає `500` на весь список, якщо стіл хоча б одного його бронювання (навіть скасованого) вже видалено. Стіл можна видалити, коли на ньому немає активних бронювань, тож це звичайна ситуація. Через це тести видаляють столики лише разом із локацією наприкінці прогону.

### Обмеження стенду

Lambda `api-handler` у моєму AWS має 128 MB пам'яті (так у `lambda_config.json` з архіву), а акаунт має низький ліміт одночасних викликів. Звідси два ефекти:
- приблизно з 10 паралельних запитів API Gateway відповідає `500 {"message": "Internal server error"}` без CORS-заголовків, і браузер бачить це як помилку CORS;
- холодний старт нового екземпляра .NET під навантаженням буває довшим за 29 с, тоді API Gateway віддає `504 Endpoint request timed out`.

Що з цим робить фреймворк:
- паралельно йдуть 3 сценарії (`TEST_PARALLELISM`), браузер одночасно тримають не більше двох (`UI_MAX_SESSIONS`);
- перед прогоном кілька екземплярів Lambda «прогріваються» паралельними легкими запитами;
- відповіді шлюзу `500`/`429`/`503` (до бекенду запит не доходив) повторюються з паузою, `504` повторюється лише для GET, бо запис міг уже виконатися. Кількість повторів друкується в кінці прогону;
- дашборд офіціанта, якщо частина запитів не пройшла, тест оновлює, не більше трьох разів, як зробив би й користувач.

Якщо 504 все ж трапився на підготовці даних, сценарій у звіті позначений як broken, а не failed: це збій стенду, а не перевірки. Підняти пам'ять Lambda чи ліміт викликів можна в AWS, але інфраструктуру я в цьому ДЗ не змінював.
