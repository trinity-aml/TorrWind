# TorrWind 1.0.6

Языки: [English](README.md) | Русский

Гид для новичков: [Русский](docs/BEGINNER_GUIDE_RU.md) | [English](docs/BEGINNER_GUIDE.md)

План завершения: [Русский](docs/ROADMAP_RU.md) | [English](docs/ROADMAP.md)

TorrWind - desktop-клиент для Windows 10/11 x64, предназначенный для управления локальными и удаленными экземплярами TorrServer.

Репозиторий: https://github.com/trinity-aml/TorrWind  
Лицензия: GPL-3.0-only

TorrWind хранит настройки, журналы, скачанные бинарные файлы TorrServer, плейлисты, резервные копии и остальные рабочие файлы в рабочей папке приложения внутри `Data`. Для обычной portable-работы программа не использует `%ProgramData%` и `%AppData%`.

## Возможности

- Управление профилями локального и удаленного TorrServer.
- Скачивание и обновление локального TorrServer из GitHub Releases с проверкой SHA256, если digest есть в metadata релиза.
- Запуск локального TorrServer как процесса и опциональная установка службы Windows через `TorrWind.Service.exe`.
- Проверка и скачивание обновлений TorrWind из GitHub Releases в `Data/updates`.
- Добавление, удаление, drop и wipe торрентов и magnet-ссылок.
- Список файлов торрента, проигрывание выбранного файла, продолжение просмотра и плейлист от выбранного файла.
- Встроенный mpv-плеер с навигацией по M3U-плейлистам, настройками аудио/видео/субтитров и запуском внешнего плеера.
- Вкладка TorrServer Web UI как резервный интерфейс.
- Поиск через Torznab-совместимые индексеры, включая Jackett/Prowlarr-подобные endpoints.
- Редактор Runtime JSON для настроек TorrServer.
- Field-редактор расширенных runtime-настроек TorrServer.
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
.\scripts\publish-win-x64.ps1 -Version 1.0.6
```

Publish-скрипт скачивает последний Windows x64 mpv runtime от shinchiro, проверяет SHA256 digest из GitHub release, если он указан, и устанавливает runtime в `artifacts/publish/TorrWind/Runtime/mpv`. Перед release-упаковкой установите 7-Zip. Для офлайн-сборки передайте `-MpvRuntimeArchivePath <mpv-x86_64-...7z>`; чтобы собрать без встроенного mpv, используйте `-SkipMpvRuntime`.

Создать portable zip:

```powershell
.\scripts\package-win-x64.ps1 -Version 1.0.6
```

Собрать Inno Setup инсталлятор:

```powershell
.\scripts\build-installer.ps1 -Version 1.0.6
```

Собрать все release-артефакты и контрольные суммы:

```powershell
.\scripts\release-win-x64.ps1 -Version 1.0.6
```

Запустить unit tests:

```powershell
dotnet test TorrWind.sln
```

Результаты сохраняются в:

- `artifacts/publish/TorrWind`
- `artifacts/portable/TorrWind-1.0.6-win-x64-portable.zip`
- `artifacts/installer/TorrWind-1.0.6-win-x64.exe`
- `artifacts/TorrWind-1.0.6-SHA256SUMS.txt`

Папка publish и portable zip включают `README.md`, `README.ru.md`, `LICENSE` и папку `docs`.

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
pwsh ./scripts/package-win-x64.ps1 -Version 1.0.6
```

Скрипты упаковки также добавляют mpv через `scripts/install-mpv-runtime.ps1`. В Linux-среде сборки установите `p7zip`/`7z` или передайте `-MpvRuntimeArchivePath <mpv-x86_64-...7z>` для офлайн-сборки.

Собрать инсталлятор через Wine + Inno Setup:

```bash
pwsh ./scripts/build-installer.ps1 -Version 1.0.6
```

По умолчанию скрипт инсталлятора ищет Inno Setup в `~/.wine-inno` и `~/.wine`. Автоопределение можно переопределить:

```bash
pwsh ./scripts/build-installer.ps1 \
  -Version 1.0.6 \
  -WinePrefix "$HOME/.wine-inno" \
  -InnoCompilerPath "$HOME/.wine-inno/drive_c/InnoSetup6/ISCC.exe"
```

Собрать все release-артефакты:

```bash
pwsh ./scripts/release-win-x64.ps1 -Version 1.0.6
```

Запустить unit tests:

```bash
dotnet test TorrWind.sln
```

Если на Linux установлен только более новый .NET runtime, используйте `DOTNET_ROLL_FORWARD=Major dotnet test TorrWind.sln` или установите runtime .NET 8.

## Release workflow

GitHub Actions workflow: `.github/workflows/release.yml`.

Он запускается автоматически при публикации тега:

```bash
git tag v1.0.6
git push origin v1.0.6
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
Data/updates
```

GUI умеет:

- скачивать и обновлять TorrServer из GitHub releases `YouROK/TorrServer` и проверять SHA256, если она доступна;
- переключаться между скачанными локальными версиями;
- запускать и останавливать TorrServer как дочерний процесс;
- устанавливать, удалять, запускать, останавливать и опрашивать `TorrWindService`;
- применять runtime-настройки TorrServer из штатного экрана настроек или вкладки Runtime JSON, включая TMDB API и URL изображений.

Повышение прав запрашивается только для установки и удаления службы. Установленная служба работает от `LocalService`; запуск, остановка, проверка состояния, обычное редактирование настроек и работа с удалённым сервером не запрашивают права администратора.

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

Воспроизведение может использовать встроенный mpv-плеер или внешний плеер:

- встроенный mpv;
- системный плеер по умолчанию;
- VLC;
- MPC-HC;
- PotPlayer;
- пользовательский путь к исполняемому файлу.

TorrWind генерирует совместимые с TorrServer stream или M3U URL и открывает их в выбранном плеере. Release-сборки включают Windows x64 mpv runtime в `Runtime\mpv`; TorrWind также ищет `mpv.exe` в папке приложения, `mpv`, `tools\mpv`, а затем в `PATH`.

Встроенный mpv-плеер сам читает локальные M3U-файлы и скачивает HTTP(S) M3U/M3U8-плейлисты. Для сериалов плейлист показывается как список серий, есть кнопки-пиктограммы для перехода к предыдущей/следующей серии и возможность выбрать любую серию из списка. В плеере также доступны выбор аудиодорожки, видеодорожки, субтитров, соотношения сторон, задержки аудио и задержки субтитров.

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
- выбрать режим локального TorrServer: управление через GUI или `TorrWindService`;
- установить и опционально запустить `TorrWindService`, если выбран режим службы.

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
