# Golovomka

Unity 2D top-down pixel-art игра для Windows. Лаборатория захвачена тремя бандитами; полицейский Иван проходит препятствия с помощью копий своих предыдущих действий.

## Текущий MVP
Иван начинает у телефона в полицейском участке. Запись начинается в первой комнате лаборатории, где доступна одна копия. Демо заканчивается у правого прохода. Геймплей комнат 2–4 описан для будущей реализации в сценах Unity. [Текущий MVP](docs/mvp-echoes.md)

Текущая версия запускается из `Assets/Golovomka/Scenes/EchoMVP.unity` в Unity 6000.6.8f1 через Play Mode. Готовая Windows-сборка в `outputs/Golovomka-Echo-MVP` относится к более ранней версии. Для передачи нужна вся папка. [Инструкция к сборке](outputs/Golovomka-Echo-MVP/README.md).

## Документы
- [Правила работы](AGENTS.md).
- [Сюжет и объём](docs/game-context.md).
- [Копии действий](docs/puzzles/action-echoes.md).
- Комнаты: [1 — знакомство](docs/rooms/01-introduction.md), [2 — последовательность](docs/rooms/02-sequence.md), [3 — две копии](docs/rooms/03-two-echoes.md), [4 — пульт записи](docs/rooms/04-recording-console.md).
- [Текущий MVP и результаты проверок](docs/mvp-echoes.md).
- [Техническая основа](docs/project-setup.md).
- [Художественное направление](docs/art-direction.md).
- [Открытые вопросы](docs/open-questions.md).

Графика: участок и Иван — собственная графика с AI; окружение подвала — Modern Interiors Free v2.2, LimeZu. Текущая версия предназначена только для некоммерческого тестирования автором и знакомыми.
