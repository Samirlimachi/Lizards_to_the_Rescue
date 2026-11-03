# Colección de comida para LEVEL1

Seis modelos originales creados en Blender para este proyecto: manzana, plátano, sección de mazorca, brócoli, medio aguacate y pimiento. Son modelos reconstruidos a partir de la referencia visual, no archivos de Tripo ni extraídos de Frog Sqwad.

## Archivos

- `Models/`: seis FBX con geometría real y normales suavizadas.
- `Textures/`: atlas BaseColor de 1024 × 1024 por alimento, con UV sin solapamiento intencional.
- `Materials/`: materiales URP Lit con sus texturas asignadas.
- `Prefabs/`: modelos a escala de juego con Rigidbody, colliders y FoodItem.
- `Source/FoodCollection.blend`: archivo editable de Blender.

Una textura y un material por modelo. La mazorca contiene granos modelados; el brócoli contiene tallo, ramas y brotes; el aguacate incluye piel, borde, pulpa y hueso. No tienen animaciones ni esqueleto porque se manipulan como objetos rígidos. No se modificaron los controles ni el sistema de comida.

## Integración en la escena

LEVEL1 contiene cinco ejemplares de cada tipo, repartidos en los 30 puestos de comida que ya existían. Se conserva la nutrición original de esos puestos y las dos sandías grandes. La comida provisional se conserva inactiva en `__Comida provisional archivada`. Los modelos nuevos están bajo `COMIDA / colección modelada`.

Todos caben en la boca normal (radios de 0,19–0,27 m); tienen masas de 0,24–0,42 kg. Se agarran con la lengua y se comen con las acciones existentes. Las colisiones usan volúmenes simples en vez de las mallas detalladas para reducir el coste físico.

El generador de comida de emergencia `FoodSpawner` conserva su aspecto anterior. Esta colección sustituye los alimentos colocados de LEVEL1; no cambia la generación global del juego.

## Comer y escupir

El estómago recuerda el modelo y la escala de cada alimento tragado. La expulsión voluntaria devuelve un alimento completo, empezando por el último que se comió. Los golpes expulsan los mismos modelos con la cantidad exacta de nutrición descontada. Alimentar a un compañero también transfiere esa identidad. La comida inicial o añadida mediante depuración, que no procede de un objeto tragado, se representa con una manzana al expulsarla.

La capacidad del estómago sigue limitando la nutrición aceptada: escupir nunca recupera el exceso descartado al comer estando casi lleno. `FoodRegurgitationPlaytest.Run()` comprueba los seis modelos durante tres ciclos, el orden de expulsión, la conservación de valor, golpes, alimentación y protección contra recogida inmediata. Informe: `Captures/FoodRegurgitation_Playtest.txt`.

## Revisión

`FoodCollectionPlaytest.Begin()` ejecuta una prueba en Play: inventario de 30 instancias, materiales, colisiones, capacidad de boca, agarre y recogida física con la lengua, ingestión y nutrición para los seis modelos. Resultado en `Captures/FoodCollection_Playtest.txt`. No se probó multijugador en esta entrega.

El menú `Lizards > Comida > Importar modelos y colocar en LEVEL1` vuelve a configurar prefabs y sustituye únicamente comida provisional activa. Exige tener LEVEL1 abierta. No reconstruye el nivel.

La copia de seguridad previa incluye los cambios de escena que estaban sin guardar al empezar y está en `Backups/FoodModels/`.

El archivo unitypackage es un paquete de recursos para este proyecto, que ya tiene FoodItem y el resto del sistema Lizards. Para otro proyecto usa los FBX y PNG o instala también el sistema de interacción correspondiente.
