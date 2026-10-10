# Encargos nativo (Android + Windows)

Dos apps nativas que conservan **todas** las funciones de Encargos y el **mismo sistema de Telegram**
(respaldo único `ENCARGOS_BACKUP_LATEST`, archivo `encargos-latest.enc` cifrado con AES‑256‑GCM, código de enlace `ENC1`).
Funcionan junto a la app web actual: se enlazan con el mismo código y ven los mismos datos.

| Carpeta | Qué es |
|---|---|
| `core/` | Núcleo en Kotlin (cálculos, respaldos, seguridad y sincronización). Lo usa Android. |
| `android/` | App Android: Kotlin + Jetpack Compose + Room. Paquete `com.agencia.encargos.nativo` (se instala junto a la app actual, no la reemplaza). |
| `windows/Encargos.Core` | El mismo núcleo traducido a C# (.NET 8). |
| `windows/Encargos.Win` | App Windows: C# + WinUI 3, datos en SQLite, secretos con DPAPI, Windows Hello. |
| `windows/Encargos.Tests` | Pruebas del núcleo C# y "dispositivo de prueba" para las pruebas cruzadas. |
| `tests/` | Simulador de la Bot API de Telegram y pruebas cruzadas Android ↔ PC. **Nunca usan el bot real.** |

## Compilar con GitHub Actions (gratis)

Cada vez que se sube código a `main`, GitHub compila solo:

* **Android (APK)** → pestaña *Actions* → *Android (APK)* → artefacto `Encargos-Android`.
* **Windows (instalador)** → *Windows (instalador)* → artefacto `Encargos-Windows` (`Encargos_Windows_Setup_x.y.z.exe`).
* **Núcleos y pruebas cruzadas** → comprueba que Kotlin y C# calculan y sincronizan igual.

### Firma del APK (secretos de GitHub, nunca en el repositorio)

En *Settings → Secrets and variables → Actions* crea:

| Secreto | Valor |
|---|---|
| `ENCARGOS_KEYSTORE_B64` | el archivo `.keystore` en base64 (`base64 -w0 encargos.keystore`) |
| `ENCARGOS_KEYSTORE_PASSWORD` | contraseña del almacén |
| `ENCARGOS_KEY_ALIAS` | alias de la clave (por ejemplo `encargos`) |
| `ENCARGOS_KEY_PASSWORD` | contraseña de la clave (si es distinta) |

Sin estos secretos el APK se firma con una clave de prueba (sirve para probar, pero no para actualizar después).

## Seguridad

* El token del bot **no** está en el código, ni en el APK, ni en el instalador. Cada usuario lo escribe en la app.
* Android lo guarda cifrado con Android Keystore; Windows con DPAPI (solo tu usuario de Windows puede leerlo).
* Los errores y registros ocultan cualquier token (`<oculto>`).
* Telegram solo recibe datos cifrados (AES‑256‑GCM; la llave nunca sale de tus dispositivos salvo dentro del código ENC1 que tú copias).
* Sin contraseña por defecto: se crea en el primer inicio. PIN y contraseña con PBKDF2‑SHA256 + secreto del equipo.

## Dónde quedan los datos

* Android: base de datos Room de la app (no se copia a la nube de Google: `allowBackup=false`).
* Windows: `%LOCALAPPDATA%\EncargosNativo` (no se borra al desinstalar). Copias automáticas en `Documentos\Encargos`.

## Compilar en tu propio PC (alternativa a GitHub)

* **Android**: abre la carpeta raíz con Android Studio (Koala o más reciente) → *Build → Build APK(s)*.
  Sin las variables `ENCARGOS_KEYSTORE*` se firma con la clave de depuración.
* **Windows**: instala Visual Studio 2022 con la carga "Desarrollo de aplicaciones WinUI" (.NET 8), abre
  `windows/Encargos.Win/Encargos.Win.csproj` y compila en *Release x64*. Para el instalador, Inno Setup 6 con `windows/installer.iss`.

## Estado de verificación (honesto)

* Núcleo Kotlin: compilado y probado (vectores + 34 pruebas cruzadas con la app web actual y Telegram simulado).
* App Android (Compose) y app Windows (WinUI 3): código completo, **aún sin compilar** porque este entorno no tiene
  los SDK de Android/.NET. La primera compilación en GitHub Actions puede mostrar errores pequeños que se corrigen ahí.
