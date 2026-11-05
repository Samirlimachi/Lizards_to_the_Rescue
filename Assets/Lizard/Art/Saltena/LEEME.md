# Salteña rompible

Modelo original: Meshy_AI_Golden_Crimped_Dumpling_20261103_texture.fbx, facilitado por el usuario. La corteza conserva sus UV y su textura; los cortes tienen caras cerradas y material de relleno. Los seis trozos son mallas distintas, no copias reducidas de la salteña entera. Los FBX y los cascos convexos de colisión están en Models y Colliders.

## Jugar y probar

Escena: Assets/Scenes/SaltenaTest.unity. Pulsa Play. Hay tres salteñas y un muro de impactos a la derecha.

LEVEL2 también incluye tres salteñas: junto al bol central, en el depósito y en el almacén húmedo. La escena previa está respaldada en Backups/Saltena/LEVEL2_before_saltena_20261105.unity.

- Clic izquierdo: sujeta la salteña con la lengua. Muévete para arrastrarla; R acorta la lengua y T la alarga.
- Dos jugadores pueden sujetar la misma salteña. Al separarse en direcciones opuestas y mantener tensión durante 0,65 segundos, se parte en seis trozos.
- Un solo tirón o dos tirones desde el mismo lado no la parten. Los contactos suaves tampoco.
- Un golpe contra un muro, suelo o estructura puede partirla si supera 4,2 m/s de velocidad relativa, 3,2 m/s de velocidad normal al contacto y 2,5 de impulso. Se ignoran golpes durante los primeros 0,8 segundos para permitir que se asiente al aparecer. Las colisiones con jugadores y otras comidas no la rompen.
- E come los trozos. G devuelve el trozo real, conservando su forma y nutrición.
- F4 activa o retira un ayudante local que tira del otro lado. F5 reinicia la sala en modo local. F2 abre el menú de conexión existente.

La salteña completa aporta cuatro unidades de nutrición y pesa 2,4 kg. Los trozos reparten ambas cantidades según su volumen y corrigen el último residuo de redondeo. Todos caben en la boca normal. La comida habitual conserva su agarre exclusivo.

## Integración

Prefab principal: Assets/Lizard/Resources/Saltena/Saltena_Whole.prefab. Los seis prefabs de trozos están en la misma carpeta. Se puede arrastrar el prefab principal a otra escena. Para varias copias en una escena multijugador, asigna un id distinto al componente SharedFoodSync de cada instancia; debe coincidir en todos los clientes.

BreakableFood tiene los parámetros de golpes, tensión y duración en el Inspector. FoodItem mantiene los agarres compartidos de alimentos rompibles. SharedFoodSync y LizardNetSync.Food usan el master de Photon Fusion Shared para simular el alimento y comunicar poses, rotura, consumo, entrega y reaparición al escupir. Los RPC verifican la autoridad y el consumo reserva cada alimento una sola vez. Los clientes que llegan tarde reciben los alimentos presentes y los identificadores retirados.

La sala usa el prefab de red SunnyRed existente. La conexión online requiere la configuración de Photon del proyecto. Los ensayos automatizados de física se realizan con dos personajes locales; la prueba con dos equipos y conexión real se debe distinguir de esos ensayos.

Validación local: 25 comprobaciones correctas, incluyendo tensión opuesta, agarre por dos lenguas, golpes físicos reales contra el muro, contactos suaves, seis modelos distintos, comida, escupido y conservación de nutrición y masa. No se validó una sesión Photon con dos equipos.

Fuente editable Blender: SourceAssets/Saltena/Saltena_Fractured.blend. Constructor: Lizards > Salteña > Crear recursos y sala de prueba. El constructor recrea la sala de prueba; no lo uses para guardar cambios manuales de esa sala. El informe de pruebas está en Captures/Saltena_Playtest.txt.
