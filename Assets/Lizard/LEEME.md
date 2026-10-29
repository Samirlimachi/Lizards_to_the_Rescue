# Lizards to the Rescue: personaje jugable

## Puesta en marcha
1. Abre el proyecto en Unity 6000.5.6f1 y espera a que compile.
2. Menú **Lizards → Construir escena de prueba**. Esto crea:
   - `LizardConfig.asset`: todos los valores de equilibrio.
   - `Prefabs/Lagarto.prefab`.
   - Los materiales en `Materials/`.
   - `Scenes/LizardTestZone.unity`, que se añade a Build Settings.
3. Pulsa Play. Haz clic para capturar el ratón y pulsa Esc para liberarlo.
4. Opcional: en Play, ejecuta **Lizards → Ejecutar autoprueba**. Los resultados PASS/FAIL salen en la consola.

## Controles (reasignables con **Tab**)
| Acción | Teclado y ratón | Mando |
|---|---|---|
| Moverse / mirar | WASD / ratón | Stick izquierdo / derecho |
| Saltar | Espacio | A / Cruz |
| Lengua (mantener para seguir enganchado) | Clic izquierdo | RT |
| Acortar / alargar la lengua | R / F o rueda | RB / LB |
| Comer / interactuar | E | X / Cuadrado |
| Expulsar comida / alimentar | G | Y / Triángulo |
| Escupir ácido | Clic derecho | LT |
| Pedir ayuda | H | Cruceta arriba |

Teclas de depuración (fijas):
- **F1, F2, F3**: forma normal, renacuajo o gigante.
- **F4 / F5**: ±1 de comida.
- **F6**: recargar el ácido.
- **F7**: recibir un golpe.
- **F8**: crear comida.
- **F9**: reiniciar la escena.
- **F10**: mostrar u ocultar el panel de depuración.
- **F11**: encender o apagar el micrófono.
- **F12**: cambiar de personaje controlado.

## Cómo probar cada habilidad (zonas de la escena, avanzando en +Z)
1. **Movimiento**: escalones, una rampa de 25° (se sube), una de 60° (no se sube) y plataformas con hueco.
2. **Lengua**: puntos amarillos sobre el foso para balancearse y una pared morada adhesiva. Mantén el clic, acorta o alarga con R/F y suelta para salir con impulso.
3. **Arrastre**:
   - Las cajas ligeras vienen hacia ti.
   - La caja mediana se arrastra.
   - La caja de 70 kg necesita dos lenguas o al gigante.
   - La bola de 400 kg te atrae a ti hacia ella.
4. **Comida**: frutas pequeñas que se pueden comer y una sandía demasiado grande para tragar, que hay que llevar a la meta dorada. También hay un generador de comida.
5. **Gigante**: come hasta llenar la barra. Después:
   - El paso estrecho no deja pasar al gigante.
   - El muro marrón se rompe embistiéndolo.
   - La placa azul necesita 40 kg (el gigante o la roca) para abrir la puerta.
6. **Peligros**: pinchos, una superficie de lava y un enemigo que patrulla. Si tienes comida, pierdes parte; si estás vacío, te conviertes en renacuajo.
7. **Rescate**:
   - El compañero renacuajo pide ayuda.
   - Agárralo con la lengua para transportarlo, suéltalo cerca para lanzarlo y pulsa G para alimentarlo.
   - Para recuperarte en solitario, usa el anillo azul o el generador de comida.
   - Con F12 controlas al compañero.
8. **Ácido**: un enemigo morado (3 impactos) y un muro pálido que se disuelve con 2 impactos.
9. **Techo bajo**: si comes dentro del túnel, la transformación se aplaza hasta que sales. Las esquinas sirven para probar el crecimiento junto a paredes.

## Arquitectura
Hay un componente por sistema, todos coordinados por `LizardPlayer`:
- `LizardMotor`: movimiento.
- `LizardForms`: estados, colisión y masa, con comprobación de espacio.
- `LizardStomach`: comida, con registro contable.
- `LizardHealth`: daño e invulnerabilidad.
- `LizardTongue`: lengua.
- `LizardAcid`: ácido.
- `LizardVisual`: animación procedural.
- `LizardLipSync`: voz a mandíbula.
- `LizardCameraRig`: cámara.
- `LizardInput`: acciones reasignables.

Todos los números están en `LizardConfig`, con valores separados por forma.

## Estado
**Terminado y verificado por la autoprueba** (tras ejecutarla en Unity):
- La comida no se duplica.
- La protección entre golpes funciona.
- La cadena de golpes lleva de normal a vacío y de vacío a renacuajo.
- El renacuajo se recupera alimentado o con el punto de rescate.
- El crecimiento se aplaza bajo techo.
- Las habilidades se restauran al cambiar de forma.
- La lengua se suelta cuando el objetivo desaparece o se consume.
- La distancia de cámara se ajusta en las tres formas.

**Terminado, pero solo se puede comprobar jugando**:
- La sensación del movimiento.
- El balanceo.
- Los empujes del gigante.
- Las placas de peso.
- Las roturas.
- El ácido.
- El lanzamiento de compañeros.

**Provisional**:
- Modelo hecho de volúmenes redondeados (primitivas con UV) en una jerarquía preparada para animar, con mandíbula articulada. No es un modelo esculpido ni tiene huesos ni skinning.
- Animaciones procedurales por código, sin clips del Animator.
- Sonidos sintetizados por código.
- Partículas sencillas.
- Interfaz en IMGUI.
- Sincronía labial por volumen: mueve la mandíbula y no distingue fonemas.

**Pendiente**:
- **Red con Photon Fusion 2**: no está integrada ni probada entre clientes, así que no hay multijugador terminado. Las mutaciones ya pasan por métodos centrales (`TryEat`, `TryGrab`/`TryConsume`, `TakeHit`, `RequestForm`, `ReceiveFood`) para convertirlas en RPC o en estado `[Networked]` con autoridad del servidor o del host.
- **Voz en red** (Photon Voice), para que el lip-sync funcione con la voz de cada jugador.
- **Segundo jugador local con mando.**
- **Modelo definitivo con rig y blend shapes.**
- **Ampliaciones posibles** (no implementadas a propósito): nadar, correr y doble salto.
