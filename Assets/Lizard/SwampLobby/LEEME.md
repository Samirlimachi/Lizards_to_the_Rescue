# Lobby — El refugio del pantano

Escena: Assets/Scenes/Lobby.unity. Queda al principio de las escenas habilitadas de Build Settings. Abre Lobby y pulsa Play.

Reconstrucción aproximada del conjunto visual del pantano de Frog Sqwad a partir de imágenes públicas y de la grabación aportada: laguna turquesa, balsa naranja/azul, guardián coronado, sauces, rocas, muelle, bazar y conducto. Geometría y shaders creados para este proyecto; no son los assets originales ni una réplica exacta.

## Interacciones

- WASD/ratón/Espacio: movimiento del Sunny existente.
- Balsa: acércate y pulsa E; espera tres segundos sin alejarte para cargar LEVEL1. El muelle y el suelo de la balsa tienen colisiones.
- Teléfono: E abre la interfaz de Photon existente. El menú ya no aparece automáticamente al entrar a este hub. Jugar solo cierra la interfaz.
- Bazar: E junto al mostrador alterna dos sombreros de prototipo y la opción sin sombrero. No hay compras ni economía; el cosmético no se guarda ni se sincroniza.
- Guardián: E cerca del frente muestra la misión de rescate.
- Comida: E para comer, G para expulsar. Hay cajas y barriles manipulables con la lengua, comida renovable y fuente de recuperación.
- Si el personaje cae bajo el agua, vuelve a la zona inicial.

## Validación y límites

Prueba en Play: avance de frames, movimiento W, menú inicialmente cerrado, equipar cosmético, apertura y cierre de interfaz de sala, paso por el muelle, apoyo en la balsa, cuenta atrás y carga efectiva de LEVEL1. Informe: Captures/Lobby_Playtest.txt.

La apertura del menú de salas se probó mediante llamada a su método, sin conectarse a servicios externos. No se verificaron varios clientes Photon. La salida hacia LEVEL1 se bloquea con mensaje si hay una sesión online activa, porque el cambio de escena y los objetos del nivel todavía requieren sincronización de mundo. El código de salas previo se conserva, con showOnStart y OpenRoomMenu añadidos para este hub.

El agua tiene un shader URP animado. El entorno usa SwampSurface mate; Sunny conserva su sombreado existente. No se han añadido chat de voz, tienda de pago ni persistencia de progreso.

El lobby anterior está respaldado en Backups/Lobby y sus objetos originales se conservan desactivados bajo __Original_Lobby. LEVEL1 se conservó.

El menú Lizards > Lobby > Construir refugio del pantano regenera la escena archivando lo anterior. Para seguir editando este resultado, trabaja directamente sobre Lobby.

Referencias: https://www.panicstations.com/frog-sqwad y la grabación proporcionada en este chat.
