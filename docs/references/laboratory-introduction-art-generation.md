# Графика первой комнаты

19 ноября 2026. Использован встроенный imagegen, без API/CLI. За основу взят последний эскиз из обсуждения с автором, сохранённый как laboratory-introduction-concept.png. Референс стилистики — готовые текстуры участка. Это собственная AI-графика проекта; отдельные детали изображённых семейных фотографий не являются утверждённой биографией персонажей.

## Файлы

- Assets/Golovomka/Resources/Art/LaboratoryIntroduction.png — фон без персонажа, с открытой механической дверью. Герой и копии рисуются отдельно.
- Assets/Golovomka/Resources/Art/LaboratoryIntroductionDoor.png — зарегистрированный источник закрытой двери; в игре берётся только участок (1350, 400, 122, 310), остальное изображение не используется.
- docs/references/laboratory-introduction-unity.png — снимок работающей игры.

Point, без mipmaps/сжатия, max 4096, NPOT None. Размер исходного AI-изображения не задаёт финальный тайл или PPU. Коллизии, перекрытия, персонажи и переключение двери задаются EchoMvp.Introduction.cs.

## Запрос для фона (дословно)

```text
Precise game asset cleanup edit. Preserve this image EXACTLY: same image dimensions, camera, framing, geometry, furniture positions, rug, family photos, sofa, desk, hidden remote on its extended tray, open left door, OPEN right mechanical doorway, visible strip of next room, colors and pixel texture treatment. Remove ONLY the police officer in the center. Reconstruct the previously occluded CENTER of the metal floor pressure panel underneath his feet as plain worn dark gray steel matching panel edges; preserve its outline and short side guides and exact position. No person, character, ghost, silhouettes, shadows from people, text or UI anywhere. This is an empty playable background plate for a Unity scene. Absolutely no layout redesign or additional objects. Keep mechanical right door fully open.
```

## Запрос для закрытой двери (дословно)

```text
Precise game sprite source edit. Preserve EXACT image dimensions 1599x984, camera, pixel style and every object and pixel position. Change ONLY the mechanical doorway on the RIGHT from OPEN to CLOSED by showing its worn gray metal sliding leaf filling the opening. The door is in the vertical dividing side wall around x1350..1465 y400..730. Respect the existing orthographic top-down three-quarter perspective: the leaf is seen obliquely on the side wall, a narrow long metal face, no front-facing wide door pasted into space. Use muted detailed worn steel, narrow vertical seams and restrained rivets, same material as existing frame and drive housing. Frame, rail, motor, dividing wall and opening size must remain unchanged. No handle, wood, window, hazard stripes, status light or wiring to floor. Keep the rest of image unchanged including empty room with NO characters, floor plate, hidden remote on extended under-desk tray, rug, photographs, furniture, open wooden entrance door on LEFT and the strip of next room. No text or UI. This full image is only a registered sprite source; only the small right door area will be used in-game.
```

## Ограничения

Фон и детали нарезаются при запуске, как в существующем окружении участка. Появление механизма безопасности показано уже завершённым. Дверь переключается между двумя состояниями; анимация хода створки не выполнена. Вид соседнего помещения — часть эскиза, не реализация бесшовного туннеля всех комнат. Плита при удержании темнеет; анимация её вертикального хода пока отсутствует. Лоток после подбора закрывается небольшой вставкой из его же текстуры. Финальная оценка чтения предметов и удобства — после прохождения автором.
