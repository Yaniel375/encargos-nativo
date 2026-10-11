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
- PC · **Pantalla y resolución** (Ajustes → Apariencia, `windows/Encargos.Win/Display.cs`): tamaño de ventana (recordar / maximizada / pantalla completa), resoluciones fijas o a medida, tamaño del contenido (automático o 75–200 %), atajos F11, Esc, Ctrl +/−/0. La ventana ya no se abre más grande que la pantalla. Se guarda solo en el PC (claves `encargos_win_fit`, `encargos_win_size`, `encargos_win_last`). Autoprueba OK en GitHub (pantalla 1024×768 al 100 %); sin probar en pantallas reales con escala distinta del 100 %. Respaldo previo: rama `respaldo-antes-resolucion`.
- PC · acabado unificado: contenido pegado al menú lateral (hasta 1400 de ancho, sin centrar), avisos de la barra superior por fuera del botón (con número), un solo título por pantalla (en la barra, con `Screen.Right` al lado; sin «Cerrar» en las fichas, se vuelve con la flecha), plurales con `K.Plural`, `Grid2(..., fillLast)` para botones de fichas. Respaldo previo: rama `respaldo-antes-acabado`.
- **Desde el 11/10/2026 solo se trabaja en Android** (indicación del usuario); PC queda como está publicada.
- Android · acabado unificado: barra inferior opaca y barra superior casi opaca con franja arriba (el contenido no se ve detrás), fichas con la misma flecha de volver (sin «Cerrar»), plurales con `plural()` en `ui/Kit.kt`, consejo de Encargos alineado, variación de Estadísticas dentro del `Kpi` (parámetro `extra`), iconos en Recordar pago/Agradecer, «Abonar» en Pendientes, sin «Tu negocio, en orden» (barra, acceso y recibo). Recorrido del emulador: 0 pasos fallidos. Respaldo previo: rama `respaldo-antes-acabado-android`.
- PC igualada con Android (11/10/2026, pedido del usuario): sin «Tu negocio, en orden» (barra lateral, barra superior, acceso y recibo), «Abonar» en Pendientes; barra lateral animada al elegir sección (`Fx.NavIn`/`Fx.NavOut` en `Aurora.cs`: fondo desde la izquierda, barrita que crece, icono con salto). Respaldo previo: rama `respaldo-antes-pc-como-android`.
- Android · **Notas → En venta** (`ui/Sale.kt`, núcleo `Encargos.saveSaleItem/sellSaleItem/undoLastSale/saleSummary`): artículos con varias fotos, precio, precio de compra, peso (lb), tipo de envío y costo por libra (sale de Ajustes → Costo por libra), otros gastos; costo por unidad = compra + peso × costo/lb + otros; resumen con valor en venta, inversión, inversión total, por ganar, ganancia neta y cobrado (dinero en verde); «Vendí» guarda la venta con su costo de ese momento (con deshacer); compartir con fotos. Se guardan como notas con `kind = "sale"` (entran en respaldos y sincronización sin cambiar el formato; la pestaña Notas los oculta). En PC se ven como notas normales. Pruebas del núcleo 74/74. Respaldo previo: rama `respaldo-antes-articulos`.
- Sin verificar en dispositivo real: bot de Telegram real, sonido, vibración, huella/Windows Hello, WhatsApp, selector de archivos y fotos.
