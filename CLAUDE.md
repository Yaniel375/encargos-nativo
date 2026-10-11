# Encargos nativo — guía para Claude Code

Apps nativas de **Encargos** (gestión de encargos, clientes, pagos y notas):
- `android/` — Android, Kotlin + Jetpack Compose (UI en `android/src/main/java/com/agencia/encargos/nativo/ui/`).
- `windows/` — Windows, WinUI 3 / C# (`Encargos.Win/` interfaz, `Encargos.Core/` lógica, `Encargos.Tests/`).
- `core/` — lógica compartida en Kotlin (cálculos, sincronización). Tests: `core/src/test/kotlin/.../Funcional.kt`.
- `tests/android_tour.py` — recorrido automático en emulador (capturas). `.github/workflows/` — CI (android, windows, core).

## Reglas del dueño (obligatorias)
- Escribir siempre **en español**, claro y sin tecnicismos innecesarios.
- **No publicar** (fusionar en `main`) sin que el usuario lo diga. Antes de cada cambio grande, crear una rama `respaldo-antes-<tema>` desde `main` y trabajar en una rama aparte.
- **No cambiar cálculos** ni la lógica de negocio salvo que se pida o sea un error demostrado. Conservar todas las funciones.
- **Sin Supabase ni Firebase.** La sincronización es con el bot de Telegram del usuario y los datos viajan **cifrados** (Telegram nunca recibe datos de clientes en texto plano).
- **Secretos:** el token del bot nunca va en el código, la interfaz, los logs, la consola ni los instaladores; se configura dentro de la app. No usar el bot real en pruebas. No borrar mensajes ni respaldos reales. No reemplazar datos reales por datos de demostración.
- No afirmar que algo se probó si no se probó; decir claramente qué no se pudo verificar.

## Sistema visual (referencia: la pantalla Inicio)
- Android: piezas compartidas en `ui/Kit.kt` y `ui/Theme.kt` (paleta `W`, `NIGHT`/`DAY`, Manrope, `SectionCard`, `Kpi`, `Primary`/`Secondary`, `NavRow`, `RowGroup`, `Sheet`, `AlertDialog` propio, `AppSwitch`, `ToggleRow`, `SettingsGroup`, `AppMenu`/`MenuItem`, `OptionRow`, `ProfilePic`). Usarlas siempre en vez de crear estilos nuevos.
- PC: `Kit.cs` y `RefKit.cs` (mismas piezas: `Card`, `IconCard`, `Kpi`, `Btn`, `NavRow`, `Group`, `Toggle`, `SettingsGroup`, `MenuItemBtn`/`AppFlyout`, `OptionRow`) y `MainWindow.Dialog(...)` para todas las ventanas.
- Sin emojis en la interfaz: iconos de la app (Material en Android, Segoe MDL2 en PC).
- Mantener Android y PC con la misma apariencia y funciones.

## Compilar y probar en local
- Android: abrir `android/` en Android Studio (JDK 17) o `gradle :app:assembleDebug`; probar en un teléfono real por USB.
- Windows: `dotnet build windows/Encargos.Win` (necesita .NET SDK y Windows App SDK). La autoprueba se activa con la variable `ENCARGOS_SELFTEST=<carpeta>` y usa una carpeta de datos aparte.
- Tests del núcleo: Kotlin (`core`) y C# (`windows/Encargos.Tests`).

## Estado actual (11/10/2026)
- Publicado en `main` (CI de Android, Windows y núcleo en verde): unificación visual, ventanas, ficha del encargo como la del cliente, menús, acceso con PIN, foto de perfil, auditoría de Ajustes, recibo nuevo con el estilo de la app (Android `util/Util.kt` → `Receipt` y PC `Media.ReceiptView`) y versión de PC unificada con Android (interruptores y grupos de Ajustes incluidos).
- Las ramas `recibo` y `pc-unificar` ya están fusionadas; no hay trabajo pendiente en ellas.
- Sin verificar en dispositivo real: bot de Telegram real, sonido, vibración, huella/Windows Hello, WhatsApp, selector de archivos y fotos.
