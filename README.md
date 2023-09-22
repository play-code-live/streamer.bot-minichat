:no_entry: [DEPRECATED] Проект больше не публикуется в открытый репозиторий github. Все версии и инструкции доступны на странице документации https://docs.play-code.live/minichat

# MiniChat + Streamer.bot = ♥️

[![GitHub All Releases](https://img.shields.io/github/downloads/play-code-live/streamer.bot-minichat/total.svg)](https://github.com/play-code-live/streamer.bot-minichat/releases) [![GitHub release](https://img.shields.io/github/release/play-code-live/streamer.bot-minichat.svg)](https://github.com/play-code-live/streamer.bot-minichat/releases)

Интеграция, позволяющая обрабатывать в Streamer.bot все события внешних платформ, совместимых с [MiniChat](https://discord.gg/S3mNeDCTTF).

Работает на основе функции `Custom Triggers` в Streamer.bot 0.2.0+. Автоматически добавляет неизвестные события по мере их появления.

## Установка

> **Очень важно!** Автоматический запуск действия интеграции зависит от триггера старта Streamer.bot, который появился только в версии streamer.bot-0.2.0-b3. Поэтому интеграция совместима только с 3 бета и выше.

1. Загрузите свежую версию файла импорта **install.sb** со [страницы релизов](https://github.com/play-code-live/streamer.bot-minichat/releases)
2. Откройте Streamer.bot и нажмите кнопку Import
3. Перенесите загруженный файл (install.sb) в область Import String и нажмите **Import**
4. Перезагрузите Streamer.bot, или нажмите на Action `-- MiniChat Integration`, в области Triggers найдите `Streamer.bot Started`, нажмите на него правой кнопкой и выберите `Test Trigger`

## Обработка событий

Все события, которые обрабатываются со стороны MiniChat, попадают в область `Custom` области Triggers и сопровождаются дополнительными аргументами в зависимости от типа события.

> На текущий момент по всем событиям приходят только `%user%` и `%userName%`. В дальнейшем список параметров будет дополнен.
