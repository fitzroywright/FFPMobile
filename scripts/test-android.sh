#!/usr/bin/env bash
set -euo pipefail

case "${1:-}" in
  ""|--install) ;;
  *) echo "Usage: $0 [--install]" >&2; exit 2 ;;
esac
if (( $# > 1 )); then
  echo "Usage: $0 [--install]" >&2
  exit 2
fi

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
studio_jdk="/Applications/Android Studio.app/Contents/jbr/Contents/Home"
if [[ -z "${JAVA_HOME:-}" && -x "$studio_jdk/bin/java" ]]; then
  export JAVA_HOME="$studio_jdk"
fi
if [[ -z "${ANDROID_HOME:-}" && -d "$HOME/Library/Android/sdk" ]]; then
  export ANDROID_HOME="$HOME/Library/Android/sdk"
fi

modules=(app-field-officers app-warehouse app-security-post)
packages=(org.ffp.mobile.fieldofficers org.ffp.mobile.warehouse org.ffp.mobile.security)
tasks=(testDebugUnitTest)
for module in "${modules[@]}"; do
  tasks+=(":$module:assembleDebug")
done

cd "$root/android"
./gradlew "${tasks[@]}"

if [[ "${1:-}" == --install ]]; then
  adb=adb
  if [[ -n "${ANDROID_HOME:-}" ]]; then
    adb="$ANDROID_HOME/platform-tools/adb"
  fi
  if [[ -n "${ANDROID_SERIAL:-}" ]]; then
    serial="$ANDROID_SERIAL"
  else
    serials="$("$adb" devices | awk '$1 ~ /^emulator-/ && $2 == "device" { print $1 }')"
    if [[ -z "$serials" || "$serials" == *$'\n'* ]]; then
      echo "Start one emulator or set ANDROID_SERIAL before using --install." >&2
      exit 1
    fi
    serial="$serials"
  fi
  for i in "${!modules[@]}"; do
    module="${modules[$i]}"
    package="${packages[$i]}"
    "$adb" -s "$serial" install -r "$module/build/outputs/apk/debug/$module-debug.apk"
    "$adb" -s "$serial" shell am start -W -n "$package/.MainActivity"
  done
fi
