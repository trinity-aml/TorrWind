# TorrWind 1.0.0

Языки: [English](README.md) | Русский

TorrWind - desktop-клиент для Windows 10/11 x64, предназначенный для управления локальными и удаленными экземплярами TorrServer.

Репозиторий: https://github.com/trinity-aml/TorrWind  
Лицензия: GPL-3.0-only

TorrWind хранит настройки, журналы, скачанные бинарные файлы TorrServer, плейлисты, резервные копии и остальные рабочие файлы в рабочей папке приложения внутри `Data`. Для обычной portable-работы программа не использует `%ProgramData%` и `%AppData%`.

## Возможности

- Управление профилями локального и удаленного TorrServer.
- Скачивание и обновление локального TorrServer из GitHub Releases.
- Запуск локального TorrServer как процесса и опциональная установка службы Windows через `TorrWind.Service.exe`.
- Добавление, удаление, drop и wipe торрентов и magnet-ссылок.
- Список файлов торрента, проигрывание выбранного файла, продолжение просмотра и плейлист от выбранного файла.
- Запуск внешнего плеера для MVP-версии.
- Вкладка TorrServer Web UI как резервный интерфейс.
- Поиск через Torznab-совместимые индексеры, включая Jackett/Prowlarr-подобные endpoints.
- Редактор Runtime JSON для настроек TorrServer.
- Настройки кеша в памяти или на диске. Для новых профилей по умолчанию используется memory cache 64 МБ.
- Диагностический отчет, журналы GUI/службы, импорт/экспорт настроек и экспорт support bundle.
- Локализация через JSON-файлы в `locales`.

## Требования

Целевая среда выполнения:

- Windows 10/11 x64
- .NET 8 desktop runtime включается в self-contained release-сборки

Разработка на Windows:

- .NET 8 SDK
- PowerShell 7 или Windows PowerShell
- Inno Setup 6 для сборки инсталлятора

Разработка на Linux:

- .NET 8 SDK
- PowerShell 7
- Wine + Inno Setup 6 в Wine prefix для сборки инсталлятора

## Сборка на Windows

Восстановить зависимости и собрать решение:

```powershell
dotnet restore
dotnet build TorrWind.sln
```

Опубликовать self-contained Windows x64 файлы:

```powershell
.\scripts\publish-win-x64.ps1 -Version 1.0.0
```

Создать portable zip:

```powershell
.\scripts\package-win-x64.ps1 -Version 1.0.0
```

Собрать Inno Setup инсталлятор:

```powershell
.\scripts\build-installer.ps1 -Version 1.0.0
```

Собрать все release-артефакты и контрольные суммы:

```powershell
.\scripts\release-win-x64.ps1 -Version 1.0.0
```

Результаты сохраняются в:

- `artifacts/publish/TorrWind`
- `artifacts/portable/TorrWind-1.0.0-win-x64-portable.zip`
- `artifacts/installer/TorrWind-1.0.0-win-x64.exe`
- `artifacts/TorrWind-1.0.0-SHA256SUMS.txt`

## Сборка на Linux

TorrWind является Windows desktop-приложением, но репозиторий можно собирать и упаковывать из Linux с включенным таргетингом под Windows.

Собрать решение:

```bash
DOTNET_CLI_HOME="$PWD/.dotnet" \
NUGET_PACKAGES="$PWD/.nuget/packages" \
DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
dotnet build TorrWind.sln -m:1 -p:UseSharedCompilation=false -p:NuGetAudit=false
```

Создать portable Windows x64 zip:

```bash
pwsh ./scripts/package-win-x64.ps1 -Version 1.0.0
```

Собрать инсталлятор через Wine + Inno Setup:

```bash
pwsh ./scripts/build-installer.ps1 -Version 1.0.0
```

По умолчанию скрипт инсталлятора ищет Inno Setup в `~/.wine-inno` и `~/.wine`. Автоопределение можно переопределить:

```bash
pwsh ./scripts/build-installer.ps1 \
  -Version 1.0.0 \
  -WinePrefix "$HOME/.wine-inno" \
  -InnoCompilerPath "$HOME/.wine-inno/drive_c/InnoSetup6/ISCC.exe"
```

Собрать все release-артефакты:

```bash
pwsh ./scripts/release-win-x64.ps1 -Version 1.0.0
```

## Release workflow

GitHub Actions workflow: `.github/workflows/release.yml`.

Он запускается автоматически при публикации тега:

```bash
git tag v1.0.0
git push origin v1.0.0
```

Также workflow можно запустить вручную из GitHub Actions, указав версию. Workflow:

- устанавливает .NET 8 и Inno Setup на `windows-latest`;
- запускает `scripts/release-win-x64.ps1`;
- загружает инсталлятор, portable zip и SHA256SUMS как workflow artifacts;
- создает или обновляет GitHub Release с этими файлами.

## Первичная настройка

1. Скачайте portable zip или инсталлятор из release.
2. Запустите `TorrWind.exe`.
3. Откройте `Настройки -> TorrServer`.
4. Нажмите `Проверить последнюю`, `Загрузить релизы` или `Скачать TorrServer`, чтобы скачать бинарный файл локального TorrServer.
5. Используйте `Запустить локальный` для TorrServer, управляемого GUI, или настройки `Сервис` для установки/запуска `TorrWindService`.
6. Откройте `Библиотека` и добавьте `.torrent`, magnet-ссылку или результат поиска.
7. Выберите файл торрента и используйте `Открыть плеер`, `Продолжить` или `Плейлист от выбранного`.

## Локальный TorrServer

Файлы локального TorrServer хранятся в:

```text
Data/TorrServer
Data/TorrServer/versions
Data/TorrServer/cache
```

GUI умеет:

- скачивать и обновлять TorrServer из GitHub releases `YouROK/TorrServer`;
- переключаться между скачанными локальными версиями;
- запускать и останавливать TorrServer как дочерний процесс;
- устанавливать, удалять, запускать, останавливать и опрашивать `TorrWindService`;
- применять runtime-настройки TorrServer из штатного экрана настроек или вкладки Runtime JSON.

Для установки, удаления, запуска и остановки службы может потребоваться повышение прав. Обычное редактирование настроек и работа с удаленным сервером не требуют прав администратора.

## Удаленный TorrServer

Откройте `Настройки -> Серверы` и добавьте профиль:

- имя;
- базовый URL, например `http://192.168.1.2:8090`;
- опционально имя пользователя и пароль;
- опционально `ignore certificate errors`;
- режим read-only, если TorrWind не должен изменять сервер.

Удаленные серверы можно использовать для библиотеки, генерации URL/плейлистов воспроизведения, Web UI, диагностики и поиска через выбранный TorrServer.

## Поисковые индексеры

Откройте `Настройки -> Индексеры` и добавьте Torznab-совместимых провайдеров.

Типичные URL:

```text
http://127.0.0.1:9117/api/v2.0/indexers/all/results/torznab
http://192.168.1.2:9696/api/v1/indexer/all/results/torznab
http://192.168.1.2:5002
```

TorrWind нормализует распространенные Jackett/Prowlarr/JacPro-подобные URL, поддерживает API keys, фильтры категорий, тайм-ауты и отключение проверки ошибок сертификата.

## Воспроизведение

В MVP-версии используется внешний плеер:

- системный плеер по умолчанию;
- VLC;
- MPC-HC;
- PotPlayer;
- пользовательский путь к исполняемому файлу.

TorrWind генерирует совместимые с TorrServer stream или M3U URL и передает их выбранному внешнему плееру. Встроенный LibVLC-плеер запланирован на следующий этап.

## Кеш и runtime-настройки

Для новых локальных профилей по умолчанию используется:

- memory cache mode;
- размер кеша 64 МБ;
- буфер предзагрузки 50%;
- опережающий кеш 95%;
- тайм-аут отключения торрента 30 секунд;
- 25 torrent connections.

Кеш можно переключить в disk mode и указать папку внутри `Data/TorrServer/cache` или другой выбранный пользователем путь.

## Диагностика и журналы

Диагностику можно скопировать, сохранить или упаковать в support bundle. Чувствительные значения, включая пароли, API keys, tokens и secrets, очищаются.

Журналы хранятся в:

```text
Data/logs/gui.jsonl
Data/logs/service.jsonl
```

Когда TorrWind запускает локальный TorrServer, stdout/stderr попадают в ту же систему журналирования.

## Интеграция с Windows

Инсталлятор может:

- создать ярлык на рабочем столе;
- запускать TorrWind вместе с Windows;
- связать `.torrent` файлы;
- зарегистрировать обработчик `magnet:`;
- установить и опционально запустить `TorrWindService`.

`TorrWind.exe` принимает:

```powershell
.\TorrWind.exe --minimized
.\TorrWind.exe "C:\Downloads\movie.torrent"
.\TorrWind.exe "magnet:?xt=urn:btih:..."
.\TorrWind.exe "https://example.org/file.torrent"
```

TorrWind работает как один GUI-экземпляр. Последующие shell-запуски передаются уже открытому экземпляру.

## Лицензия

GPL-3.0-only. См. `LICENSE`.
