# Informe · Interfaz unificada tomando como referencia Encargos-9.apk

Rama de trabajo: `interfaz-referencia` · **no se ha publicado en `main`** (la página de descargas sigue con la versión anterior hasta que lo autorices).

## 1. Copia de seguridad
| Rama | Qué contiene |
|---|---|
| `respaldo-antes-interfaz` | El proyecto exactamente como estaba publicado antes de este cambio (para volver atrás). |
| `estable-1.0` | Punto de restauración anterior al rediseño premium (se conserva). |

## 2. Cómo se analizó la referencia
- La APK de referencia es una app web empaquetada (`assets/index.html`). Se abrió ese HTML en un navegador automatizado (Chromium) en tamaño teléfono, se entró con su acceso de demostración, se cargaron datos de ejemplo **solo en esa copia** y se capturaron todas sus pantallas, menús y hojas de ajustes.
- No se ha descompilado código nativo de la APK (no lo tiene: es HTML/JS). Solo se usaron capturas de lo que realmente se pudo observar.

## 3. Qué se reprodujo (teléfono y PC)
| Pantalla | Organización copiada de la referencia |
|---|---|
| Menú del perfil | Cabecera con usuario → accesos en cuadrícula (Pagos · Estadísticas · Ajustes · …) → Modo día/noche → Bloquear. |
| Encargos | Totales arriba (Total · Cobrado · Pendiente), consejo, tarjeta con estado + flecha, recuadros Total/Pagado/Saldo, Ganancia, barra de progreso y fila **Ficha · Editar · WhatsApp · ⋯**; tarjeta "Encargos completados" al final. |
| Clientes | Totales (Clientes · Por cobrar · A favor), tarjeta con Libras/Cobrado/Pendiente, "Pagó X de Y", barra y fila **Ficha · Cobrar · + Encargo**. |
| Nuevo encargo | Vista previa en vivo, pasos numerados (1 Cliente · 2 Pedido y envío · 3 Pago y estado · 4 Captura) con ✓ al completarse, atajos "Pagó todo / la mitad / Sin pago" y tarjeta de total con degradado. |
| Ajustes | Grupos de filas (Negocio y tarifas · WhatsApp · Notificaciones y alertas · Apariencia · Datos y seguridad); cada fila abre una hoja con botón "Cerrar". |
| Seguridad | Tarjeta de estado (Protección completa/parcial), grupo Acceso con interruptores, Protección de datos, Actividad y Zona de peligro. |
| Respaldo | Estado y contadores, Respaldo automático, Último respaldo y tarjetas de acción (Respaldar datos en azul, cifrado, importar, Excel) + Consejos. |
| Pendientes | Resumen grande Por cobrar / A favor, secciones con punto de color, tarjetas con "Debe", chips y fila **Cobrar · Ficha · Registrar pago**. |
| Fichas (encargo, cliente, editar) | Encabezado tipo hoja con botón "Cerrar"; etapas del pedido en círculos unidos por una línea. |
| Notas | Tarjeta "Te deben en notas", sección **Fijadas** y cuadrícula de notas. |

En PC se usa el mismo orden y estilo, aprovechando el ancho: tarjetas en 2 columnas (1 en ventana estrecha), ajustes y seguridad en dos columnas, las hojas de ajustes como ventanas emergentes.

## 4. Funciones conservadas
**No se modificó ni una línea de lógica**: el núcleo de cálculos (`core/src/main`, `windows/Encargos.Core`), almacenamiento, sincronización con Telegram, seguridad y utilidades quedan idénticos (comprobado con `git diff`: 0 cambios).
Se conservaron también las acciones que la referencia no tiene: Encargos completados, Sincronización y Diagnóstico, Exportar a Excel, Ocultar en recientes/capturas, Windows Hello/huella, metas, recibos (imagen/PDF/imprimir), cambiar fecha, nota interna, agradecer, avisar llegada y copias automáticas.

## 5. Archivos visuales modificados
Android (`android/src/main/java/com/agencia/encargos/nativo/ui/`): `Kit.kt` (piezas nuevas: botón de tarjeta, filas, grupos, hoja, pasos, etapas, tarjeta de total), `Root.kt` (menú del perfil, botón Cerrar), `Orders.kt`, `Clients.kt`, `NewOrder.kt`, `Admin.kt`, `Money.kt`, `Notes.kt`.
Windows (`windows/Encargos.Win/`): `RefKit.cs` (nuevo, mismas piezas), `Kit.cs` (solo `partial`), `MainWindow.cs` (menú del perfil, botón Cerrar), `Pages/Orders.cs`, `Clients.cs`, `NewOrder.cs`, `Admin.cs`, `Money.cs`, `Notes.cs`.
Pruebas y compilación: `tests/android_tour.py`, `.github/workflows/*.yml` (solo se añadió la rama).

## 6. Pruebas realizadas (GitHub Actions)
| Prueba | Resultado |
|---|---|
| Compilación APK firmada (Android) | ✅ |
| Compilación e instalador de Windows | ✅ |
| Pruebas funcionales del núcleo Kotlin (52) y C# + compatibilidad cruzada | ✅ |
| Recorrido automático en emulador Android, modo noche y día (crear acceso, entrar, crear un encargo de prueba 40 + 3 lb × $7 − $10, abrir todas las pantallas, menú del perfil, hoja de tarifas, menú ⋯) | ✅ sin cierres; la ficha muestra el total correcto $61.00 |
| Autoprueba de Windows: abre todas las pantallas en noche y día, fichas y diagnóstico | ✅ "RESULTADO: OK" |
| Revisión visual de capturas (desbordes, botones fuera de sitio) | ✅ se corrigieron "WhatsApp" y "Sincronización" recortados |

## 7. Lo que NO se pudo verificar
- Bot real de Telegram (por seguridad solo se probó sin bot real).
- Sonido, vibración, huella y Windows Hello en un dispositivo físico.
- Abrir WhatsApp de verdad, elegir fotos de la galería, guardar/abrir archivos con el selector del sistema (en el emulador no hay usuario que elija).
- Pantallas de PC en resoluciones distintas a la de la autoprueba (1028×749).
- **Supabase: no aplica.** Este proyecto no usa Supabase ni Firebase (decisión tuya); los datos están en el dispositivo y, opcionalmente, cifrados en tu bot de Telegram.

## 8. Publicar
Cuando lo apruebes, se fusiona `interfaz-referencia` en `main` y la APK y el instalador se actualizan solos en la página de descargas.
