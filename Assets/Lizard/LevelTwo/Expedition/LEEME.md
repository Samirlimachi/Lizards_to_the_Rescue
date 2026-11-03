# LEVEL2 · Expedición de provisiones

Mapa de recolección en alcantarillas, con la paleta y las proporciones estilizadas de las referencias: paredes lilas, marcos lima, tuberías menta, agua turquesa y alimentos de colores. El trazado es propio; no es una reproducción verificada del mapa de Frog Sqwad.

## Recorrido

La balsa del Lobby carga LEVEL2. Desembarca, explora los ramales, reúne comida y deposítala físicamente en el bol central. Cuando se completa la cuota, vuelve a la balsa y pulsa E para regresar al Lobby. La salida requiere permanecer cerca durante tres segundos.

Ocho sectores: embarcadero, estación del bol, galería inundada, almacenes húmedos, depósito de carga, filtros, colectores elevados y despensa norte. Dos circuitos exteriores conectan los ramales con la despensa. Hay rampas de salida del agua, puentes, enganches de lengua, puntos de recuperación y enemigos de patrulla.

## Alimentos y objetivos

- 77 alimentos colocados, con seis modelos: manzana, plátano, maíz, brócoli, aguacate y pimiento.
- 14 cajas de provisiones: E las abre; una embestida de la forma gigante también puede abrirlas. Sus contenidos conservan los modelos de comida al tragarlos y escupirlos.
- 126 unidades de nutrición entre alimentos y cajas. Una cesta de manzanas repone una unidad cada diez segundos cuando se recoge la anterior, para permitir recuperarse de pérdidas.
- La cuota comienza en 40 unidades de nutrición (400 en el indicador), sube ocho por expedición y se limita a 76.
- Volver con la cuota completa concede 250 monedas una vez y avanza el día.
- Reserva de carga con placa de 12 kg y contrapeso marcado de 20 kg. Reservas opcionales protegidas por madera y membrana vulnerable al ácido.

## Controles existentes

WASD mueve; clic izquierdo usa la lengua; rueda o R/T modifica su longitud; E come o interactúa; G escupe comida; clic derecho lanza ácido. Las reasignaciones del jugador se conservan.

## Edición y comprobación

Escena: Assets/Scenes/LEVEL2.unity. Raíz activa: LEVEL2 · Expedición de provisiones.

El estado anterior permanece desactivado dentro de la escena y tiene una copia en Backups/Level2Expedition/LEVEL2_20261101_014647.unity. El constructor no elimina los recursos anteriores.

LevelTwoExpeditionBuilder reconstruye el mapa y archiva el anterior; Detail renueva solo el atrezo adicional. LevelTwoExpeditionValidation.Paths comprueba trece conexiones físicas sin guardar un NavMesh en el nivel. El playtest LevelTwoExpeditionPlaytest se inicia desde Lobby en Play y restaura las monedas y los indicadores de progreso al finalizar.

Los informes están en Captures/LEVEL2_Paths.txt y Captures/LEVEL2_Expedition_Playtest.txt. La validación de la expedición es local; la sincronización multijugador no está cubierta por esta prueba.
