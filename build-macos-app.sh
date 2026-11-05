#!/usr/bin/env bash

# Собирает self-contained релиз для Apple Silicon и упаковывает его в стандартный
# macOS bundle dist/TaskManager.app. Скрипт запускается из любого каталога:
# корень проекта определяется по расположению самого файла, а временная сборка
# изолирована в отдельной папке внутри dist и удаляется при завершении.

set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_FILE="$PROJECT_DIR/TaskManager.csproj"
DIST_DIR="$PROJECT_DIR/dist"
APP_BUNDLE="$DIST_DIR/TaskManager.app"
BUNDLE_IDENTIFIER="com.elkhanmamedov.taskmanager"

# Создаёт временный каталог для publish и промежуточной структуры .app.
# Размещение внутри dist гарантирует, что финальное переименование приложения
# выполняется в пределах одной файловой системы и не затрагивает другие файлы.
create_build_directory() {
    mkdir -p "$DIST_DIR"
    mktemp -d "$DIST_DIR/.TaskManager-build.XXXXXX"
}

# Удаляет только временную папку, созданную этим запуском скрипта.
# Готовое dist/TaskManager.app и любые соседние файлы эта функция не затрагивает.
cleanup_build_directory() {
    if [[ -n "${BUILD_DIR:-}" && -d "$BUILD_DIR" ]]; then
        rm -rf "$BUILD_DIR"
    fi
}

# Читает версию проекта средствами MSBuild и приводит её к числовому формату,
# который принимает CFBundleVersion. Если версия проекта отсутствует или содержит
# суффикс prerelease, используется числовая часть; пустой результат заменяется 1.0.0.
read_bundle_version() {
    local project_version
    project_version="$(dotnet msbuild "$PROJECT_FILE" -getProperty:Version -nologo | tail -n 1)"
    project_version="$(printf '%s' "$project_version" | sed -E 's/[^0-9.].*$//')"

    if [[ ! "$project_version" =~ ^[0-9]+([.][0-9]+){0,3}$ ]]; then
        project_version="1.0.0"
    fi

    printf '%s' "$project_version"
}

# Создаёт обязательный Info.plist. Имя исполняемого файла соответствует apphost,
# который создаёт dotnet publish для сборки TaskManager с RID osx-arm64.
write_info_plist() {
    local plist_path="$1"
    local bundle_version="$2"

    cat > "$plist_path" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>TaskManager</string>
    <key>CFBundleDisplayName</key>
    <string>TaskManager</string>
    <key>CFBundleIdentifier</key>
    <string>$BUNDLE_IDENTIFIER</string>
    <key>CFBundleVersion</key>
    <string>$bundle_version</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleExecutable</key>
    <string>TaskManager</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon.icns</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
PLIST
}

# Удаляет временные артефакты при штатном завершении или ошибке. Обработчики
# сигналов сначала завершают процесс с соответствующим кодом; затем EXIT удаляет
# только временную папку этого запуска, не затрагивая другие данные в dist.
BUILD_DIR="$(create_build_directory)"
trap cleanup_build_directory EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

PUBLISH_DIR="$BUILD_DIR/TaskManager.app/Contents/MacOS"
RESOURCES_DIR="$BUILD_DIR/TaskManager.app/Contents/Resources"
mkdir -p "$PUBLISH_DIR" "$RESOURCES_DIR"

if [[ ! -f "$PROJECT_FILE" ]]; then
    printf 'Не найден файл проекта: %s\n' "$PROJECT_FILE" >&2
    exit 1
fi

if [[ ! -f "$PROJECT_DIR/Assets/AppIcon.icns" ]]; then
    printf 'Не найден готовый значок: %s\n' "$PROJECT_DIR/Assets/AppIcon.icns" >&2
    exit 1
fi

# Публикует независимое от установленного .NET runtime приложение под arm64.
# Все файлы publish сразу попадают в Contents/MacOS согласно структуре bundle.
dotnet publish "$PROJECT_FILE" \
    --configuration Release \
    --runtime osx-arm64 \
    --self-contained true \
    --output "$PUBLISH_DIR"

if [[ ! -f "$PUBLISH_DIR/TaskManager" ]]; then
    printf 'dotnet publish не создал исполняемый файл TaskManager в %s\n' "$PUBLISH_DIR" >&2
    exit 1
fi

# Копирует предоставленный пользователем ICNS без генерации или изменения файла.
cp "$PROJECT_DIR/Assets/AppIcon.icns" "$RESOURCES_DIR/AppIcon.icns"
write_info_plist "$BUILD_DIR/TaskManager.app/Contents/Info.plist" "$(read_bundle_version)"

# Делает apphost запускаемым, заменяет только ранее собранный TaskManager.app,
# затем выполняет локальную ad-hoc подпись без сертификата разработчика Apple.
chmod +x "$PUBLISH_DIR/TaskManager"
if [[ -e "$APP_BUNDLE" ]]; then
    rm -rf "$APP_BUNDLE"
fi
mv "$BUILD_DIR/TaskManager.app" "$APP_BUNDLE"
codesign --force --deep --sign - "$APP_BUNDLE"

# Печатает абсолютный путь к готовому приложению, пригодный для передачи пользователю.
printf 'Готовое приложение: %s\n' "$APP_BUNDLE"
