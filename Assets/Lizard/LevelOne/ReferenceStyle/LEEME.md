# LEVEL1 — dirección de arte basada en el video

Referencia: `Grabación de pantalla 2026-11-01 010855.mp4`, duración 2:37. Se revisaron la entrada industrial (0:25), el almacén y sus objetos (0:50–1:00), el canal y los conductos (1:10–1:50) y los alimentos junto al personaje (2:20–2:35).

## Cambios

- Materiales específicos de LEVEL1: paredes violetas, baldosas oliva, colectores menta, accesos lima, rojo de seguridad y amarillo en señalización. Sin reflejos metálicos realistas ni contornos gruesos en el entorno.
- Bóveda interior, tres marcos de acceso, tomas circulares con rejas, bajantes, uniones de tubería, cableado de colores, válvulas y fluorescentes.
- Cajas con tablones y esquinas metálicas, bidones con bandas y tapas, barreras plásticas, conos y rejillas. Modelos FBX creados en Blender, con color por vértice y un material común. El importador de estos modelos corrige el espacio de color de sus colores de vértice.
- Baldosas, pilares y relieves con cantos biselados. Plataforma lateral de mantenimiento con rampa y barandilla.
- Carteles más pequeños, apartados del centro del recorrido, con textos resumidos y letras blancas. El cartel de misión sigue vinculado a la misión.
- Compuertas con nervios y franjas inferiores de seguridad. Las piezas siguen a la puerta cuando se abre.
- Agua turquesa animada y líneas de espuma en el canal.

## Escala de los alimentos

Se conservan cinco ejemplares de cada uno. Dimensión máxima de cada modelo en LEVEL1:

| Modelo | Dimensión máxima |
| --- | --- |
| Manzana | 0,34 m |
| Plátano | 0,47 m |
| Mazorca | 0,40 m |
| Brócoli | 0,43 m |
| Aguacate | 0,40 m |
| Pimiento | 0,39 m |

Son ajustes de las instancias de esta escena: no sustituyen los FBX ni modifican los prefabs compartidos. Los colliders escalan con el objeto y FoodItem.size se actualiza. Al escupir se conservan el tamaño, el material y el valor del alimento tragado.

## Organización

Los añadidos están bajo `ESTÉTICA · referencia de video`, dentro del entorno existente. La cartelería está agrupada en `SEÑAL / …`. Se conservan los Rigidbody, masas, colliders y componentes interactivos de cajas, barriles y puertas existentes. El contrapeso tiene una marca amarilla para distinguirlo.

Menú de reconstrucción artística: `Lizards > Level 1 > Aplicar estilo del video de referencia`. Opera únicamente con LEVEL1 abierta y fuera de Play. No ejecuta el constructor original del nivel.

## Validación

La escena se guardó y volvió a abrir para verificar que persisten los materiales y tamaños. La prueba de LEVEL1 pasó movimiento mediante entrada W, contrapeso, compuerta, ácido, rotura, alimentación, rescate, depósito y salida. También se probó tragar y escupir los seis modelos ya colocados: cada uno mantiene su identidad, escala, material y nutrición. No se verificó el modo online en esta entrega.

La disposición del nivel sigue siendo la del proyecto; este trabajo adapta su dirección de arte y añade elementos, sin afirmar que reproduzca el mapa del video. La copia previa de escena está en `Backups/Level1Style/`.
