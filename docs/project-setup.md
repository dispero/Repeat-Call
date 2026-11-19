# Техническая основа

Unity 6000.6.8f1, Universal 2D / URP, Input System, uGUI. Цель сборки — Windows x64. Версии сверять с `ProjectSettings/ProjectVersion.txt` и `Packages/manifest.json`.

## Запуск и сборка

Открыть `Assets/Golovomka/Scenes/EchoMVP.unity` и включить Play Mode. Основная сцена дополнительно загружает `PoliceStation.unity`, `Room01.unity` и `Room02.unity`. Все четыре входят в список сборки.

Готовая папка Windows — `outputs/Golovomka-Echo-MVP`. Для передачи нужна вся папка. Архив не создаётся. [Инструкция к готовой сборке](../outputs/Golovomka-Echo-MVP/README.md).

`EchoMvpTools.Prepare()` пересоздаёт основную сцену и её исходные настройки, включая хитбоксы программных комнат. Для обычного редактирования сохранять существующие сцены.

Unity CLI установлен в `%LOCALAPPDATA%/Unity/bin/unity.exe`. Указывать полный путь проекта и сверять версию открытого Editor.

```text
unity recompile --project-path <полный путь проекта> --format json
unity command script eval --project-path <полный путь проекта> --code "return EchoMvpTools.Verify();" --format json
unity command build run --project-path <полный путь проекта> --target StandaloneWindows64 --output-path outputs/Golovomka-Echo-MVP/Golovomka.exe --confirm true --format json
unity command build status --project-path <полный путь проекта> --format json
```

Сборка асинхронна. Результат проверять через `build status`.

## Графика и камера

- Ортографическая камера в `EchoMvp.Viewport.cs` вписывает комнату целиком с максимальным равномерным масштабом. PixelPerfectCamera отключён. Его временные параметры — 16 PPU и 480×270. Спрайты используют Point, движение привязано к сетке 1/16.
- HUD использует CanvasScaler 1280×720 и расположен поверх комнаты. Подписи мира ограничены по вертикали viewport 0,14–0,82.
- Герой и копии используют `Resources/Art/IvanOfficerWalk.png`. Импорт Default, Point, Read/Write, без mipmaps и сжатия. Нарезка и выравнивание кадров по подошвам — в `EchoMvp.Officer.cs`.
- В прототипе используется Modern Interiors Free v2.2 от LimeZu. Лицензия для некоммерческого использования лежит в `Assets/Golovomka/Resources/Art/LICENSE-LimeZu.txt`.

## Столкновения мебели

Участок и комнаты 1–2 редактируются в собственных сценах. Для программных раскладок комнат 3–4 открыть **Golovomka → Edit Furniture Hitboxes**, выбрать комнату в `FurnitureLayout` и нажать «Показать в Scene». Центральная ручка перемещает область, крайние меняют размер. Работать вне Play Mode. Ctrl+Z отменяет правку, Ctrl+S сохраняет сцену.

Пустой список означает отсутствие блокирующей мебели. Если компонент отсутствует или отключён, программная раскладка использует области из кода. Проверять проходы после правок в `EchoMVP`.

## Сохранение прогресса

`EchoHistory` пишет `echoes-mvp-v3.json` в `Application.persistentDataPath`. Сохраняются завершённые записи, номер цикла, этапы знакомства с пультом, открытые возможности, пройденные комнаты и завершение демо. При замене файла создаётся резервная копия `.bak`. Продолжение начинается у телефона.

Старые записи с `room = 0` читаются как общие. Сохранение с пройденной комнатой открывает этапы знакомства с пультом. При добавлении локальной записи прежний общий маршрут, содержащий кадры этой комнаты, удаляется целиком, включая кадры других комнат.

## Проверка

`EchoMvpTools.Verify()` проверяет интерполяцию, конец записи, лимиты, сохранение и резервную копию.

`EchoIntroductionVerification.VerifyPlay()` запускать в отдельном сеансе Play Mode. Сценарий подставляет тестовые состояния, оставляет первую комнату для снимка и пишет в `work/intro-verification-save.json`.
