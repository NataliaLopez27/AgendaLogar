# AgendaLogar — Requisitos y casos de uso

> Versión 0.1 (borrador) · Noviembre 2026
> Estado: pendiente de revisión. Las suposiciones están marcadas con **[S]** y las dudas abiertas están en la sección 9.

---

## 1. Introducción

### 1.1 Objetivo

AgendaLogar sustituye a los Excel ("cuadrantes") con los que la gestoría organiza la presentación de los **modelos fiscales** de sus clientes. El sistema permite:

- Que el **administrador** reparta cada trimestre (y cada año) los clientes entre los empleados.
- Que cada **empleado** vea solo los clientes que tiene asignados, separados en **personas físicas** y **sociedades**.
- Que el empleado marque cada modelo como **presentado** e indique su **importe**.
- Que el administrador vea en todo momento cómo va el trabajo.

### 1.2 Situación actual

Hoy existe un Excel por empleado, trimestre y tipo de cliente (por ejemplo, `CUADRANTE NURIA.xlsx` para el 3T 2026 de personas físicas):

- Cada fila es un cliente y cada columna un modelo fiscal. Una `X` significa que el modelo le corresponde a ese cliente.
- Las altas y bajas se escriben a mano en el nombre o en la celda, por ejemplo `( A 1-1-2026)`, `BAJA 30-10-2026` o `X(A22-7-24)`.
- Las notas también se escriben en las celdas, por ejemplo `NO`, `arrendamiento` u `OJO CAMBIAR NUMERO CUENTA EN EL MODELO 111`.
- **No se registra** qué está presentado ni con qué importe.
- Cada trimestre se copia el Excel anterior y se retoca a mano.

Volumen aproximado: **4 empleados** y **unos 380 clientes**.

### 1.3 Glosario

| Término | Significado |
|---|---|
| Cliente | Persona física o sociedad a la que la gestoría presenta impuestos. |
| Modelo | Declaración fiscal (303, 111, 130…). |
| Periodo | Un trimestre (1T–4T) de un año, o el ejercicio anual. |
| Asignación | Relación "en el periodo P, el cliente C lo lleva el empleado E". |
| Presentación | Un modelo concreto de un cliente en un periodo, con su estado e importe. |

---

## 2. Actores

| Actor | Descripción |
|---|---|
| **Empleado** | Guillermo, Javi, Marisa, Nuria… Solo ve y gestiona lo que tiene asignado. |
| **Administrador** | El responsable de la gestoría. Gestiona clientes, empleados, periodos y asignaciones. Además puede tener clientes asignados como cualquier empleado. |

> **[S] Recomendación:** en lugar de dos cuentas separadas, que el administrador tenga **una sola cuenta con rol de administrador** que incluya también la vista "Mis asignaciones". Así no tiene que cerrar sesión para cambiar de una a otra. Si prefieres dos cuentas, el sistema también lo permite sin cambios.

---

## 3. Modelos fiscales

### 3.1 Trimestrales

| Modelo | Concepto | Personas físicas | Sociedades |
|---|---|:-:|:-:|
| 130 | Pago fraccionado IRPF (estimación directa) | ✔ | |
| 131 | Pago fraccionado IRPF (módulos) | ✔ | |
| 111 | Retenciones trabajo / profesionales | ✔ | ✔ |
| 115 | Retenciones alquileres | ✔ | ✔ |
| 303 | IVA | ✔ | ✔ |
| 349 | Operaciones intracomunitarias | ✔ | ✔ |
| 202 | Pago fraccionado Impuesto de Sociedades (abril, octubre, diciembre) | | ✔ |

### 3.2 Anuales

| Modelo | Concepto | Resume a |
|---|---|---|
| 390 | Resumen anual IVA | 303 |
| 190 | Resumen anual retenciones | 111 |
| 180 | Resumen anual alquileres | 115 |
| 347 | Operaciones con terceros | — |
| 100 | Declaración de la Renta | — |
| 200 | Impuesto de Sociedades **[S]** | — |

Los modelos se guardan en una tabla configurable, de modo que se pueden añadir modelos nuevos sin tocar el código.

---

## 4. Requisitos funcionales

### 4.1 Acceso

- **RF-01** El sistema exige iniciar sesión con usuario y contraseña.
- **RF-02** Cada usuario tiene un rol: `EMPLEADO` o `ADMIN`.
- **RF-03** Un usuario puede cambiar su propia contraseña.
- **RF-04** Un usuario desactivado no puede iniciar sesión.

### 4.2 Vista del empleado

- **RF-10** El empleado tiene dos secciones: **Trimestral** y **Anual**.
- **RF-11** Cada sección se divide en dos pestañas: **Personas físicas** y **Sociedades**. Si un empleado no tiene clientes de un tipo, la pestaña no aparece.
- **RF-12** Por defecto se muestra el periodo en curso. El empleado puede consultar periodos anteriores.
- **RF-13** El listado muestra solo los clientes asignados al empleado en ese periodo. Cada cliente aparece con los modelos que le corresponden, igual que en el cuadrante actual.
- **RF-14** Al **pulsar sobre un cliente** del listado se abre su **ficha del periodo**. En ella, para cada modelo, el empleado puede indicar:
  1. **Hecho**: la declaración está realizada.
  2. **Listo para mandar**: está revisada y preparada para enviar.
  3. **Importe**: campo numérico con el resultado.

  El sistema guarda automáticamente la fecha y el usuario de cada cambio.
- **RF-15** El empleado puede corregir el importe o volver a un estado anterior mientras el periodo esté abierto.
- **RF-15b** En el listado, cada cliente muestra de un vistazo su estado: *Pendiente*, *Hecho* o *Listo para mandar*. El estado del cliente es el del modelo menos avanzado.
- **RF-16** El empleado puede añadir una **observación** a un modelo o a un cliente en el periodo.
- **RF-17** El empleado ve su progreso, por ejemplo "45 de 67 listos para mandar", y puede filtrar por estado y buscar por nombre.

### 4.3 Gestión de clientes (administrador)

- **RF-20** Dar de alta un cliente: nombre o razón social, NIF/CIF, tipo (física o sociedad), fecha de alta y notas.
- **RF-21** Editar los datos de un cliente.
- **RF-22** Dar de **baja** a un cliente con fecha de baja. El cliente no se borra, para conservar el histórico, y deja de aparecer en los periodos posteriores a esa fecha.
- **RF-23** Reactivar a un cliente dado de baja.
- **RF-24** Configurar qué **modelos** lleva cada cliente, cada uno con su **fecha de alta y de baja** (por ejemplo, "baja en IVA el 31-3-25").
- **RF-25** Listar y buscar clientes, filtrando por tipo, estado y empleado habitual.
- **RF-26** Eliminar definitivamente un cliente, solo si no tiene presentaciones registradas.

### 4.4 Gestión de empleados (administrador)

- **RF-30** Crear, editar y desactivar usuarios.
- **RF-31** Restablecer la contraseña de un usuario.

### 4.5 Periodos y asignaciones (administrador)

- **RF-40** Crear un **periodo trimestral** (año + trimestre) o un **periodo anual** (año).
- **RF-41** Al crear un periodo, ofrecer **copiar las asignaciones del periodo anterior** del mismo tipo. Los clientes dados de baja se excluyen y los clientes nuevos quedan "sin asignar".
- **RF-42** Asignar y reasignar clientes a empleados, uno a uno o varios a la vez.
- **RF-43** Ver los clientes **sin asignar** de un periodo.
- **RF-44** Al crear un periodo, el sistema genera las presentaciones pendientes de cada cliente según los modelos que tenga activos en ese periodo.
- **RF-45** **Cerrar** un periodo. A partir de ahí queda en solo lectura para los empleados, y el administrador puede reabrirlo.

### 4.6 Seguimiento e informes (administrador)

- **RF-50** Panel con el progreso de cada empleado en el periodo: presentados, pendientes y porcentaje.
- **RF-51** Ver el detalle de cualquier empleado, con la misma vista que tiene él.
- **RF-52** Ver el histórico de un cliente: todos sus periodos, modelos, importes y quién los presentó.
- **RF-53** *(Deseable)* Exportar un periodo a Excel.

### 4.7 Migración

- **RF-60** Importar la carga inicial de clientes y sus modelos desde los cuadrantes Excel actuales. Puede ser un script de un solo uso.

---

## 5. Requisitos no funcionales

- **RNF-01** Aplicación **web**, en español, usable desde ordenador y móvil.
- **RNF-02** Contraseñas guardadas con hash seguro (bcrypt o argon2) y conexión por HTTPS.
- **RNF-03** Control de acceso en el servidor: un empleado nunca puede leer ni modificar asignaciones de otro.
- **RNF-04** Protección de datos (RGPD): se tratan NIF y datos fiscales de terceros. Acceso restringido y copias de seguridad periódicas.
- **RNF-05** Trazabilidad: guardar quién y cuándo marcó o modificó cada presentación.
- **RNF-06** El histórico de periodos se conserva indefinidamente.
- **RNF-07** Pensado para menos de 10 usuarios simultáneos y unos 500 clientes. No hacen falta requisitos de escalabilidad especiales.

---

## 6. Modelo de datos (propuesta)

```
Usuario (id, nombre, usuario, hash_password, rol, activo)

Cliente (id, nombre, nif, tipo[FISICA|SOCIEDAD], fecha_alta, fecha_baja, notas)

Modelo (codigo, descripcion, periodicidad[TRIMESTRAL|ANUAL],
        aplica_fisica, aplica_sociedad, orden)

ClienteModelo (cliente_id, modelo_codigo, fecha_alta, fecha_baja, notas)

Periodo (id, anio, trimestre[1-4|null=anual], estado[ABIERTO|CERRADO])

Asignacion (periodo_id, cliente_id, usuario_id)

Presentacion (id, periodo_id, cliente_id, modelo_codigo,
              estado[PENDIENTE|HECHO|LISTO_PARA_MANDAR],
              importe, fecha_hecho, fecha_listo, observaciones,
              modificado_por, modificado_en)
```

---

## 7. Casos de uso

### Diagrama resumen

```
            ┌──────────────────────── AgendaLogar ────────────────────────┐
            │                                                              │
 Empleado ──┼─ CU-01 Iniciar sesión          CU-02 Cambiar contraseña      │
            │  CU-03 Ver trimestral          CU-04 Ver anual               │
            │  CU-05 Ficha cliente: estado+importe  CU-06 Corregir         │
            │  CU-07 Añadir observación                                    │
            │                                                              │
 Admin ─────┼─ (todo lo del empleado) +                                    │
 (hereda)   │  CU-10 Gestionar empleados     CU-11 Alta cliente            │
            │  CU-12 Editar cliente          CU-13 Baja/reactivar cliente  │
            │  CU-14 Configurar modelos      CU-15 Crear periodo           │
            │  CU-16 Asignar clientes        CU-17 Cerrar/reabrir periodo  │
            │  CU-18 Panel de seguimiento    CU-19 Histórico cliente       │
            │  CU-20 Exportar a Excel                                      │
            └──────────────────────────────────────────────────────────────┘
```

### CU-01 Iniciar sesión
- **Actor:** Empleado, Administrador
- **Flujo principal:**
  1. El usuario introduce usuario y contraseña.
  2. El sistema valida las credenciales y que el usuario esté activo.
  3. Redirige a "Mis asignaciones · Trimestral" del periodo en curso.
- **Alternativo:** si las credenciales son incorrectas o el usuario está desactivado, se muestra un mensaje de error genérico.

### CU-02 Cambiar contraseña
- **Actor:** Empleado, Administrador
- **Flujo:** introduce la contraseña actual y la nueva dos veces, y el sistema la actualiza.

### CU-03 Ver mis asignaciones trimestrales
- **Actor:** Empleado
- **Precondición:** sesión iniciada.
- **Flujo principal:**
  1. El empleado abre **Trimestral**. Por defecto se muestra el periodo en curso.
  2. Elige la pestaña **Personas físicas** o **Sociedades**.
  3. El sistema muestra sus clientes asignados con una columna por modelo. Cada celda indica si el modelo no aplica, está pendiente o está presentado (con importe).
  4. Puede cambiar de periodo, buscar por nombre y filtrar por pendientes.
- **Alternativo:** si no tiene nada asignado en ese periodo, se muestra un mensaje informativo.

### CU-04 Ver mis asignaciones anuales
- Igual que CU-03, con los modelos anuales (390, 190, 180, 347, 100, 200).

### CU-05 Actualizar el estado e importe de un cliente
- **Actor:** Empleado
- **Precondición:** periodo abierto y cliente asignado al empleado.
- **Flujo principal:**
  1. En el listado (por ejemplo, *Trimestral → Personas físicas*), el empleado **pulsa sobre un cliente**.
  2. Se abre la ficha del cliente para ese periodo, con los modelos que le corresponden.
  3. Para cada modelo marca **Hecho** y/o **Listo para mandar**.
  4. Introduce el **importe**. Puede ser 0 o negativo (a devolver o compensar).
  5. Opcionalmente añade una observación.
  6. Guarda. El sistema registra estado, importe, fecha y usuario, y vuelve al listado con el estado actualizado.
- **Alternativo:** si el importe no es válido, se muestra un error y no se guarda nada.
- **Regla:** "Listo para mandar" implica "Hecho". Si se marca el primero, el segundo se marca automáticamente.

### CU-06 Corregir estado o importe
- **Actor:** Empleado
- **Precondición:** periodo abierto.
- **Flujo:** reabre la ficha del cliente, modifica el importe o vuelve a un estado anterior, y el sistema registra el cambio (RNF-05).

### CU-07 Añadir observación
- **Actor:** Empleado
- **Flujo:** añade o edita una nota libre en un modelo o cliente del periodo, por ejemplo "falta factura de octubre".

### CU-10 Gestionar empleados
- **Actor:** Administrador
- **Flujo:** crear usuario (nombre, usuario, contraseña inicial, rol), editarlo, desactivarlo o restablecer su contraseña.
- **Regla:** no se puede desactivar a un empleado con asignaciones en un periodo abierto sin reasignarlas antes.

### CU-11 Dar de alta cliente
- **Actor:** Administrador
- **Flujo principal:**
  1. Introduce nombre, NIF, tipo y fecha de alta.
  2. Marca los modelos que lleva, cada uno con su fecha de alta.
  3. Opcionalmente lo asigna a un empleado en el periodo abierto actual.
- **Alternativo:** si el NIF está duplicado, se muestra un aviso.

### CU-12 Editar cliente
- **Actor:** Administrador
- **Flujo:** modifica los datos básicos o las notas del cliente.

### CU-13 Dar de baja o reactivar cliente
- **Actor:** Administrador
- **Flujo:** indica la fecha de baja. El cliente deja de generarse en los periodos posteriores, pero su histórico se conserva. Para reactivarlo se borra la fecha de baja.

### CU-14 Configurar modelos de un cliente
- **Actor:** Administrador
- **Flujo:** añade o quita modelos a un cliente con fecha de alta o baja (por ejemplo, "alta en 115 el 17-07-2026" o "baja IVA 31-3-25").

### CU-15 Crear periodo
- **Actor:** Administrador
- **Flujo principal:**
  1. Elige el tipo (trimestral o anual), el año y, si es trimestral, el trimestre.
  2. El sistema propone **copiar las asignaciones** del periodo anterior del mismo tipo.
  3. El sistema genera las presentaciones pendientes según los modelos activos de cada cliente.
  4. Muestra un resumen: clientes asignados, sin asignar y dados de baja desde el periodo anterior.
- **Alternativo:** si el periodo ya existe, se muestra un error.

### CU-16 Asignar clientes a empleados
- **Actor:** Administrador
- **Flujo:** en la vista del periodo, filtra (por ejemplo, "sin asignar"), selecciona uno o varios clientes y elige el empleado.

### CU-17 Cerrar o reabrir periodo
- **Actor:** Administrador
- **Flujo:** cierra el periodo, que pasa a ser de solo lectura para los empleados. Si quedan pendientes, el sistema avisa antes de cerrar.

### CU-18 Panel de seguimiento
- **Actor:** Administrador
- **Flujo:** muestra, por empleado, los presentados, los pendientes y el porcentaje en el periodo elegido. Se puede entrar al detalle de cada empleado.

### CU-19 Histórico de cliente
- **Actor:** Administrador
- **Flujo:** muestra la ficha del cliente con todos los periodos, los modelos, los importes y quién los presentó.

### CU-20 Exportar periodo a Excel *(deseable)*
- **Actor:** Administrador
- **Flujo:** descarga un .xlsx con el formato de cuadrante, por empleado o completo.

---

## 8. Propuesta de issues (backlog)

### Hito 0 — Arranque
1. Elegir stack y crear la estructura del proyecto (backend, frontend y base de datos).
2. Configurar el entorno de desarrollo, el linter y el CI básico.
3. Crear el esquema de base de datos y las migraciones (sección 6).
4. Cargar el catálogo inicial de modelos fiscales (seed).

### Hito 1 — Autenticación
5. CU-01 Inicio y cierre de sesión.
6. Roles y control de acceso en el servidor (RNF-03).
7. CU-02 Cambiar contraseña.
8. CU-10 Gestión de empleados.

### Hito 2 — Clientes
9. CU-11 / CU-12 Alta y edición de clientes.
10. CU-13 Baja y reactivación de clientes.
11. CU-14 Modelos por cliente con fechas de alta y baja.
12. RF-25 Listado y búsqueda de clientes.
13. RF-60 Script de importación desde los Excel actuales.

### Hito 3 — Periodos y asignaciones
14. CU-15 Crear periodo y generar presentaciones.
15. RF-41 Copiar asignaciones del periodo anterior.
16. CU-16 Asignación individual y masiva.
17. CU-17 Cerrar y reabrir periodo.

### Hito 4 — Vista del empleado
18. CU-03 Vista trimestral (pestañas físicas/sociedades).
19. CU-04 Vista anual.
20. CU-05 / CU-06 Ficha del cliente: Hecho, Listo para mandar e importe.
21. CU-07 Observaciones.
22. RF-17 Progreso, filtros y búsqueda.

### Hito 5 — Seguimiento
23. CU-18 Panel de administrador.
24. CU-19 Histórico de cliente.
25. RNF-05 Registro de cambios (auditoría).

### Hito 6 — Extras y despliegue
26. CU-20 Exportar a Excel.
27. Despliegue, HTTPS y copias de seguridad.

---

## 9. Dudas abiertas

1. **Modelo 200:** ¿entra en la sección anual de sociedades? (Se ha supuesto que sí.)
2. **Modelo 202:** solo se presenta en abril, octubre y diciembre. ¿Se muestra en el 1T, 3T y 4T, o en todos los trimestres?
3. **Modelo 349:** ¿es solo trimestral, sin resumen anual?
4. **Hecho / Listo para mandar / Importe:** ¿van **por cada modelo** del cliente (supuesto: el 303 con su importe, el 111 con el suyo…) o **uno para el cliente entero**?
5. **Estados:** ¿hace falta algún estado más, como "Presentado" (ya enviado a Hacienda) o "Falta documentación"?
6. **Guillermo:** su cuadrante no separa por tipo. ¿Son todos personas físicas o hay que clasificarlos?
7. **Cuentas del administrador:** ¿una cuenta con ambas vistas (recomendado) o dos cuentas?
8. **Stack tecnológico:** ¿alguna preferencia de lenguaje o framework? ¿Dónde se va a alojar?
