# LEVEL1 — Estación de bombeo

Escena: Assets/Scenes/LEVEL1.unity. Abre la escena y pulsa Play. Usa el prefab Sunny existente y sus mecánicas actuales.

## Recorrido

1. Muelle: movimiento, fruta, cajas, barriles y fuente de recuperación.
2. Canal: enganches amarillos, piedras, plataforma móvil y pasarela lateral. El canal es poco profundo y tiene escalón de salida.
3. Almacén: arrastra la caja naranja de 20 kg a la placa de 12 kg; mantenla encima para sostener la compuerta abierta.
4. Patio: comida para crecer, pallets rompibles con gigante, almacén lateral protegido por una membrana vulnerable a dos impactos de ácido y una babosa patrullera. Hay espacio para pasar sin luchar.
5. Refugio: come y alimenta a Pip con G estando cerca. Entrega ocho unidades de comida en el aro amarillo: arrastra las sandías o expulsa comida con G dentro del aro, alejándote primero de Pip para evitar alimentarlo accidentalmente. Hay una cesta renovable para evitar agotar los recursos.
6. Salida: se abre al completar rescate y cuota. Entra para completar el nivel.

## Controles existentes

WASD: moverse. Ratón: mirar. Espacio: saltar. Clic izquierdo mantenido: lengua. R/T o rueda: longitud. E: comer. G: alimentar/expulsar. Q: catapultar compañero. Clic derecho: ácido. C/Ctrl: deslizarse. H: ayuda. Tab: controles. Esc: liberar el cursor; clic para capturarlo.

La arquitectura es estática. Cajas, barriles, botes, frutas y sandías tienen física; puertas, plataforma, enemigo y objetivos tienen comportamiento. Las paredes no son destruibles salvo los obstáculos señalados.

## Verificación

Sin scripts perdidos ni errores de compilación. Prueba automatizada en Play: avance de frames, movimiento mediante entrada W, contrapeso y apertura física de puerta, rotura de tres pallets, consumo, disolución, rescate alimentando, consumo de alimento por trigger del depósito, desbloqueo y finalización al entrar en salida. Resultado en Captures/LEVEL1_Playtest.txt. Las pruebas de ácido y pallets llaman sus métodos de impacto directamente; no equivalen a una partida manual completa apuntando y recorriendo el nivel.

No se ha verificado una sesión de Photon con varios clientes: los sistemas nuevos de misión y objetos funcionan localmente. La sincronización de mundo compartido necesita trabajo específico de red antes de usarlos online.

La escena previa se respaldó en Backups/LEVEL1 y se conserva desactivada bajo __Original_LEVEL1. Los cambios anteriores del proyecto se conservaron.

Se añadió com.unity.pipeline 0.9.1-exp.1 para controlar y verificar este Editor. No se modificó el proyecto gamejamplus-26.

El menú Lizards > Level 1 > Construir estación de rescate regenera un nivel y archiva los objetos que había. Úsalo solo si quieres regenerarlo; las ediciones de la escena actual deben hacerse directamente en LEVEL1.
