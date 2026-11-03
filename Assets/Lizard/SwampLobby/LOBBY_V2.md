# Lobby V2 — reconstrucción ampliada

Escena: Assets/Scenes/Lobby.unity. Está guardada y preparada para Play.

## Qué cambió

La distribución anterior se conserva desactivada y existe copia en Backups/Lobby. La nueva versión incluye piscina de aparición terracota, nenúfar y salida, selección de colores, tienda con forma de sombrero, cortinas y expositores, campana, expendedora roja, cabina violeta, carrito físico, plataforma con rampa, muelle elevado, balsa naranja, cuenco de entrega, guardián y trono con antena, arco enrejado, fábrica con ventiladores, tuberías, colinas, rocas modeladas, sauces y setas. Agua animada, espuma y partículas ambientales.

La entrada de la tienda se niveló y se verificó su altura de colisión. Los materiales, el color y la malla del terreno se ajustaron para evitar los reflejos y contornos gruesos de la versión anterior.

## Cómo jugar

- E cerca de la rueda: alternar color.
- E en la entrada de la tienda: alternar cinco sombreros y opción sin sombrero. Son muestras disponibles, no un sistema de desbloqueo por logros.
- E junto a la campana: tocarla.
- E en el teléfono: abrir la interfaz de sala existente. Jugar solo la cierra.
- Expendedora: flechas izquierda/derecha recorren artículos; arriba/abajo cambian fila. E compra. También hay botones físicos que se activan al engancharlos con la lengua.
- Recoger cápsula: E cerca o acercarla a la boca con la lengua. Tres espacios de inventario.
- 1 / 2 / 3: usar el artículo de ese espacio.
- Balsa: E y permanecer cerca tres segundos para ir a LEVEL1.
- Completar LEVEL1: rescatar a Pip, entregar ocho unidades y entrar en salida. Otorga 150 monedas y regresa al lobby tras cinco segundos.

Monedas iniciales: 400, guardadas localmente. Los precios, recompensas y duraciones son valores de este prototipo; no se verificaron contra los valores originales de Frog Sqwad.

## Artículos locales

1. Pelota rebotadora: proyectil físico que daña enemigos al chocar.
2. Granada: explosión tras tres segundos; todavía no tiene el armado cooperativo del original.
3. Rana hinchable: atrae patrullas cercanas y explota al terminar su duración. Las rutas se restauran al desaparecer.
4. Bolsa: limita la velocidad de caída durante doce segundos; no incluye un modelo de bolsa animado.
5. Palo selfie: toma una captura tras dos segundos y la guarda en Application.persistentDataPath; todavía no tiene el brazo/cámara del original.
6. Cama elástica: desplegable, manipulable y con rebote limitado por intervalo.
7. Chicle de barriga: aumenta capacidad y umbral gigante del jugador.
8. Chicle de lengua: aumenta el alcance.
9. Chicle cafeína: aumenta velocidad.

Los efectos de chicle modifican una copia de la configuración del jugador, no el asset compartido. El inventario y las monedas sobreviven al viaje entre escenas. Las mejoras del cuerpo, los cosméticos y todos los objetos desplegados no tienen todavía la persistencia completa del original.

## Verificación

Captures/Lobby_V2_Playtest.txt contiene 17 comprobaciones que pasaron en Play: avance de frames, movimiento en piscina, menú sin bloqueo, enlaces de botones de lengua, sombrero, teléfono, compra mediante E real del Input System, descuento de monedas, cápsula correcta, recogida en inventario, despliegue de cama elástica, cambio de velocidad sin mutar configuración compartida, apoyo en balsa, cuenta atrás, viaje a LEVEL1, persistencia del inventario, finalización de expedición, retorno al lobby y recompensa única. Algunos resultados se agrupan en una sola comprobación. La prueba restaura las monedas y la configuración de entrada del Editor.

Se comprobó el recorrido completo local. No se han validado todas las herramientas en una partida completa, la conexión con ocho clientes, sincronización de compras/objetos/cosméticos, rendimiento en plataforma objetivo, chat de voz, logros ni equivalencia exacta con las reglas de Frog Sqwad. La transición online sigue bloqueada explícitamente mientras falte sincronización del mundo.

## Fidelidad de la reconstrucción

Esta versión amplía los elementos visibles y documentados; no es una copia calcada ni usa mallas extraídas. Persisten diferencias de geometría, escala, distribución fina, interfaz, animación y efectos. Las partes ocultas en las referencias se aproximaron. Para continuar una reproducción exacta hace falta un recorrido completo y referencias de cada zona de la versión concreta del juego.

Fuentes consultadas:
- https://www.panicstations.com/frog-sqwad
- https://store.steampowered.com/news/posts/?enddate=1780417858&feed=steam_community_announcements (Item Showcase, 2 de junio de 2026)
- Anuncio oficial 100K Wishlists and Frogsmetics, capturas de la tienda y personalización.
- Capturas públicas del pantano y grabación aportada en el chat.

Para regenerar: Lizards > Lobby > Rehacer lobby detallado (referencias). La opción de construcción básica corresponde al primer prototipo y no conserva la composición V2.
