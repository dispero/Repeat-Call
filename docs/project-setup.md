# Техническая основа

Unity 6000.6.8f1, Universal 2D/URP, Input System, uGUI, Windows x64. Сверяй версию и пакеты с `ProjectSettings/ProjectVersion.txt` и `Packages/manifest.json`.

## Сцены и переходы

`Assets/Golovomka/Scenes/Game.unity` в Play Mode через `EchoMvp` загружает `PoliceStation.unity` и `Laboratory.unity` и переключает их корни. Все три входят в сборку. Точки появления задают `PoliceStationLayout` и `LaboratoryRoom`. Вход в подвал начинает запись, возврат в участок сбрасывает попытку. Отсутствующие сцены и обязательные ссылки дают ошибки Console. `EchoMvpTools.Prepare()` пересоздаёт основную сцену и настройки. При обычных правках редактируй существующие сцены.

`LaboratoryRoom` на корне комнаты:

- `Room Number` — уникальный номер записи (1–5). У отсека 5 запись отключена.
- `Entrance` — вход и точка возврата по R. У перехода может быть отдельная точка `Arrival`.
- `Camera Area` — границы по триггеру `BoxCollider2D`. Без него берутся `Camera Size` и центр объекта. `Preview Width` добавляет запас справа.
- `Passages` — E у `Marker` в пределах `Interaction Radius` либо пересечение середины `Crossing Door` по `Cross To Right`. `Destination` выбирает комнату, участок, сообщение или завершение. Для комнаты назначь `Target Room`. `Sets Return Point` требует пульт и запоминает вход новой комнаты, сохраняя текущую позицию Ивана. Настраивай переходы в обе стороны.

## Графика и камера

- Ручной кадр в `Game`: отключи `Auto Fit Room` у `EchoMvp`, настрой `Main Camera`. Участок виден целиком. В подвале `EchoMvp.Viewport.cs` держит высоту и плавно следует по X за пределами центральной зоны (0,4 ширины, сглаживание 0,22 с). Переходы непрерывны, R мгновенно переносит камеру.
- `Ivan.prefab` в `Assets/Golovomka/Characters/Prefabs`. Масштаб — Transform, скорость — `Move Speed`, в комнате 1 масштаб Ивана и копии ×0,65. В Editor виден `IvanIdlePreview`, в игре — кадры `CharacterWalk`. Атлас `IvanUnifiedWalk` в `Characters/Sprites` содержит 16 кадров для основных направлений и четыре статичные диагональные позы. Атласы тира `IvanPistolPoses` и `IvanPistolWalk` содержат 48 кадров суммарно.
- PixelPerfectCamera отключён (временные 16 PPU, 480×270). Point, сетка движения 1/16. HUD 1280×720, подписи мира в пределах viewport 0,14–0,82 по вертикали.
- Лицензия Modern Interiors Free v2.2 (LimeZu) — `Assets/Golovomka/Resources/Art/LICENSE-LimeZu.txt`, некоммерческое использование.

## Столкновения

Коллизии соответствуют видимым стенам, мебели и дверям. Хитбокс ног 0,44 × 0,28 проверяется вдоль каждого шага. Пол и триггеры проходимы. Для контура участка предусмотрены отдельные коллайдеры. В лаборатории слои «Стены» и «Задняя стена» используют `TilemapCollider2D`. Мебель настраивается через Edit Collider. У `SlidingDoor` один подвижный `BoxCollider2D`: триггер при открытой двери и пока Иван в проёме, препятствие после закрытия и выхода. Маска проёма отвечает за вид. После правок проверь проходы в `Game`.

## Сохранение

`EchoHistory` пишет `echoes-mvp-v3.json` в `Application.persistentDataPath` и сохраняет `.bak` при замене. Данные: завершённые записи и события, цикл, этапы знакомства с пультом, комната возврата. Продолжение начинается у телефона.

При миграции v3 комнаты 3 и 4 становятся 4 и 5 с переносом точки возврата и сдвигом кадров на ширину вставленной комнаты. Для форматов 3–5 сохраняются точка возврата и последняя запись по остальным комнатам, старые записи тира сбрасываются из-за переноса мишеней. `room = 0` означает общий трек. Новая локальная запись удаляет весь старый общий трек, если в нём есть кадры её комнаты.

Формат сохранения 7 хранит состояние пистолета в кадрах. При загрузке формата 6 копии в тире по-прежнему держат пистолет на протяжении всей записи.

## Проверка

Unity CLI — `%LOCALAPPDATA%/Unity/bin/unity.exe`. Перед запуском сверь Editor. Ниже `<проект>` — полный путь. Команда сборки показывает прежнюю папку вывода, готовая папка указана в [README](../README.md).

```text
unity recompile --project-path <проект> --format json
unity command script eval --project-path <проект> --code "return EchoMvpTools.Verify();" --format json
unity command build run --project-path <проект> --target StandaloneWindows64 --output-path outputs/Golovomka-Echo-MVP/Golovomka.exe --confirm true --format json
unity command build status --project-path <проект> --format json
```

Сборка асинхронна, проверяй результат через `build status`. `EchoMvpTools.Verify()` проверяет интерполяцию, конец записи, лимиты, сохранение и резервную копию. В отдельном Play Mode `EchoIntroductionVerification.VerifyPlay()` подставляет состояния для переходов, возвратов, кнопок, тира и миграции. Сохранение проверки — `work/intro-verification-save.json`.
