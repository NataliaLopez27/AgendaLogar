# AgendaLogar — Requisitos y casos de uso

> Versión 0.3 (borrador) · Noviembre 2026
> Estado: pendiente de revisión. Las suposiciones están marcadas con **[S]** y las dudas abiertas están en la sección 10.

---

## 1. Introducción

### 1.1 Objetivo

AgendaLogar sustituye a los Excel ("cuadrantes") con los que la gestoría organiza la presentación de los **modelos fiscales** de sus clientes. El sistema permite:

- Que el **administrador** reparta cada trimestre (y cada año) los clientes entre los empleados.
- Que cada **empleado** vea solo los clientes que tiene asignados, separados en **personas físicas** y **sociedades**.
- Que el empleado marque cada cliente como **Hecho** o **Listo para mandar** e indique su **importe**.
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
| Asignación | Relación "en el periodo P, el cliente C lo lleva el empleado E", con su estado (Pendiente / Hecho / Listo para mandar) e importe. |

---

## 2. Actores

| Actor | Descripción |
|---|---|
| **Empleado** | Guillermo, Javi, Marisa, Nuria… Solo ve y gestiona lo que tiene asignado. |
| **Administrador** | El responsable de la gestoría. Gestiona clientes, empleados, periodos y asignaciones. Además puede tener clientes asignados como cualquier empleado. |

> **Decisión:** el administrador tiene **una sola cuenta** con rol `ADMIN`. Esa cuenta incluye la vista "Mis asignaciones" (igual que un empleado) y además el menú de administración. Así puede asignarse clientes a sí mismo y no tiene que cambiar de cuenta.

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
| 349 | Operaciones intracomunitarias (solo trimestral) | ✔ | ✔ |
| 202 | Pago fraccionado Impuesto de Sociedades | | ✔ |
| 200 | Impuesto de Sociedades | | ✔ |

El 200 y el 202 se muestran en la vista trimestral de sociedades, igual que en los cuadrantes actuales. **No** forman parte de la vista anual.

### 3.2 Anuales

La sección anual incluye **solo** estos modelos:

| Modelo | Concepto | Resume a | Personas físicas | Sociedades |
|---|---|---|:-:|:-:|
| 390 | Resumen anual IVA | 303 | ✔ | ✔ |
| 190 | Resumen anual retenciones | 111 | ✔ | ✔ |
| 180 | Resumen anual alquileres | 115 | ✔ | ✔ |
| 347 | Operaciones con terceros | — | ✔ | ✔ |
| 100 | Declaración de la Renta | — | ✔ | |

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
- **RF-11** Cada sección se divide en dos pestañas: **Personas físicas** y **Sociedades**. Si un empleado no tiene clientes de un tipo en ese periodo, la pestaña aparece vacía o se oculta. Ningún empleado está limitado a un tipo: por ejemplo, Guillermo hoy solo tiene personas físicas, pero el administrador puede asignarle sociedades en cualquier momento.
- **RF-12** Por defecto se muestra el periodo en curso. El empleado puede consultar periodos anteriores.
- **RF-13** El listado muestra solo los clientes asignados al empleado en ese periodo. De cada cliente se ve, como información, qué modelos le corresponden (igual que las "X" del cuadrante actual), junto con su estado y su importe.
- **RF-14** Al **pulsar sobre un cliente** del listado se abre su **ficha del periodo**. En ella el empleado indica, **para el cliente en conjunto** (no por modelo):
  1. **Hecho**: el trabajo del cliente está realizado.
  2. **Listo para mandar**: está revisado y preparado para enviar.
  3. **Importe**: campo numérico.

  El sistema guarda automáticamente la fecha y el usuario de cada cambio.
- **RF-15** El empleado puede corregir el importe o volver a un estado anterior mientras el periodo esté abierto.
- **RF-15b** Solo existen tres estados: *Pendiente*, *Hecho* y *Listo para mandar*. En el listado se ven de un vistazo, por ejemplo con colores.
- **RF-16** El empleado puede añadir una **observación** al cliente en el periodo.
- **RF-17** El empleado ve su progreso, por ejemplo "45 de 67 listos para mandar", y puede filtrar por estado y buscar por nombre.

### 4.3 Gestión de clientes (administrador)

- **RF-20** Dar de alta un cliente: nombre o razón social, NIF/CIF, tipo (física o sociedad), fecha de alta y notas.
- **RF-21** Editar los datos de un cliente.
- **RF-22** Dar de **baja** a un cliente con fecha de baja. El cliente no se borra, para conservar el histórico. Se aplica la **regla de vigencia** (RN-01): sigue apareciendo en el periodo que contiene la fecha de baja y desaparece de los siguientes.
- **RF-23** Reactivar a un cliente dado de baja.
- **RF-24** Configurar qué **modelos** lleva cada cliente, cada uno con su **fecha de alta y de baja** (por ejemplo, "baja en IVA el 31-3-25").
- **RF-25** Listar y buscar clientes, filtrando por tipo, estado y empleado habitual.
- **RF-26** Eliminar definitivamente un cliente, solo si nunca ha estado asignado en ningún periodo.

### 4.3.1 Reglas de negocio: vigencia de clientes y modelos

- **RN-01 Vigencia en un periodo.** Un cliente entra en un periodo si ha estado de alta **al menos un día** dentro de ese periodo:

  `fecha_alta ≤ fin_periodo` **y** (`fecha_baja` vacía **o** `fecha_baja ≥ inicio_periodo`)

  - Trimestres: 1T = 1 ene – 31 mar, 2T = 1 abr – 30 jun, 3T = 1 jul – 30 sep, 4T = 1 oct – 31 dic.
  - Anual: 1 ene – 31 dic.
- **RN-02** La misma regla se aplica a **cada modelo** del cliente, según sus fechas de alta y baja en ese modelo (por ejemplo, "baja IVA 31-3-25": el 303 aparece en el 1T 2025 y ya no en el 2T).
- **RN-03 Baja en un periodo ya creado.** Si el administrador da de baja a un cliente con fecha dentro de un periodo ya creado, el cliente **no se quita** de ese periodo: sigue asignado y pendiente de hacer. En el periodo siguiente ya no aparece, salvo que antes se le haya vuelto a dar de alta.
- **RN-04 Alta en un periodo ya creado.** Si se da de alta a un cliente con fecha dentro de un periodo ya creado y abierto, el sistema propone añadirlo a ese periodo y asignarlo a un empleado.
- **RN-05** En las pantallas de asignación **solo aparecen los clientes vigentes** del periodo según RN-01. Los clientes que se dieron de baja dentro del periodo se muestran con una etiqueta "Baja dd/mm/aaaa", para que se sepa que es su último periodo.

**Ejemplo:** el administrador da de baja a un cliente el 24-11-2026.
- 4T 2026 (1 oct – 31 dic): **sí aparece**, porque ha estado de alta parte del trimestre.
- 1T 2027: **no aparece**, salvo que se reactive antes.
- Anual 2026: **sí aparece**.
- Anual 2027: **no aparece**.

### 4.4 Gestión de empleados (administrador)

- **RF-30** Crear, editar y desactivar usuarios.
- **RF-31** Restablecer la contraseña de un usuario.

### 4.5 Periodos y asignaciones (administrador)

- **RF-40** Crear un **periodo trimestral** (año + trimestre) o un **periodo anual** (año).
- **RF-41** Al crear un periodo, ofrecer **copiar las asignaciones del periodo anterior** del mismo tipo. Solo se incluyen los clientes vigentes según RN-01: los que no están vigentes en el nuevo periodo se excluyen y los clientes nuevos quedan "sin asignar".
- **RF-42** Asignar y reasignar clientes a empleados, uno a uno o varios a la vez.
- **RF-43** Ver los clientes **sin asignar** de un periodo.
- **RF-44** Al crear un periodo, el sistema incluye a todos los clientes vigentes (RN-01), cada uno en estado *Pendiente*. Los modelos que se muestran de cada cliente son los vigentes en ese periodo (RN-02). En un periodo anual solo se incluyen los clientes que tienen al menos un modelo anual vigente.
- **RF-45** **Cerrar** un periodo. A partir de ahí queda en solo lectura para los empleados, y el administrador puede reabrirlo.

### 4.6 Seguimiento e informes (administrador)

- **RF-50** Panel con el progreso de cada empleado en el periodo: pendientes, hechos, listos para mandar y porcentaje.
- **RF-51** Ver el detalle de cualquier empleado, con la misma vista que tiene él.
- **RF-52** Ver el histórico de un cliente: todos sus periodos, con estado, importe y empleado.
- **RF-53** *(Deseable)* Exportar un periodo a Excel.

### 4.7 Migración

- **RF-60** Importar la carga inicial de clientes y sus modelos desde los cuadrantes Excel actuales. Puede ser un script de un solo uso.

---

## 5. Requisitos no funcionales

- **RNF-01** Aplicación de **escritorio para Windows**, desarrollada en **C# con WPF (.NET 8)** en Visual Studio, con la interfaz en español.
- **RNF-02** **Base de datos compartida:** todos los puestos (los 4 empleados) trabajan contra la misma base de datos, de modo que lo que asigna el administrador lo ve el empleado al momento.
- **RNF-03** Contraseñas guardadas con hash seguro (BCrypt). Las conexiones a la base de datos van cifradas cuando salen de la red local.
- **RNF-04b** Control de acceso: un empleado nunca puede ver ni modificar las asignaciones de otro. La comprobación se hace en la capa de servicios de la aplicación, no solo ocultando botones.
- **RNF-08** **Distribución:** la aplicación se publica desde Visual Studio como instalador (ClickOnce o MSIX) para instalarla en cada PC. Con ClickOnce, las actualizaciones se instalan solas al abrir la aplicación.
- **RNF-09** Arquitectura **MVVM** para separar la interfaz de la lógica y poder probar las reglas de negocio (por ejemplo, RN-01) con tests unitarios.
- **RNF-04** Protección de datos (RGPD): se tratan NIF y datos fiscales de terceros. Acceso restringido y copias de seguridad periódicas de la base de datos.
- **RNF-05** Trazabilidad: guardar quién y cuándo cambió el estado o el importe de cada cliente.
- **RNF-06** El histórico de periodos se conserva indefinidamente.
- **RNF-07** Pensado para menos de 10 usuarios simultáneos y unos 500 clientes. No hacen falta requisitos de escalabilidad especiales.

---

## 6. Modelo de datos (propuesta)

```
Usuario (id, nombre, usuario, hash_password, rol, activo)

Cliente (id, nombre, nif, tipo[FISICA|SOCIEDAD], notas)

AltaCliente (cliente_id, fecha_alta, fecha_baja)   -- historial; permite bajas y reactivaciones

Modelo (codigo, descripcion, periodicidad[TRIMESTRAL|ANUAL],
        aplica_fisica, aplica_sociedad, orden)

ClienteModelo (cliente_id, modelo_codigo, fecha_alta, fecha_baja, notas)

Periodo (id, anio, trimestre[1-4|null=anual], estado[ABIERTO|CERRADO])

Asignacion (id, periodo_id, cliente_id, usuario_id [null = sin asignar],
            estado[PENDIENTE|HECHO|LISTO_PARA_MANDAR],
            importe, observaciones,
            fecha_hecho, fecha_listo, modificado_por, modificado_en)
            -- única por (periodo_id, cliente_id)

HistorialAsignacion (asignacion_id, campo, valor_anterior, valor_nuevo,
                     usuario_id, fecha)   -- auditoría (RNF-05)
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
  3. El sistema muestra sus clientes asignados, con los modelos que les corresponden, su estado (Pendiente / Hecho / Listo para mandar) y su importe.
  4. Puede cambiar de periodo, buscar por nombre y filtrar por estado.
- **Alternativo:** si no tiene nada asignado en ese periodo, se muestra un mensaje informativo.

### CU-04 Ver mis asignaciones anuales
- Igual que CU-03, con los modelos anuales (390, 190, 180, 347 y Renta/100).

### CU-05 Actualizar el estado e importe de un cliente
- **Actor:** Empleado
- **Precondición:** periodo abierto y cliente asignado al empleado.
- **Flujo principal:**
  1. En el listado (por ejemplo, *Trimestral → Personas físicas*), el empleado **pulsa sobre un cliente**.
  2. Se abre la ficha del cliente para ese periodo, con los modelos que le corresponden (solo informativo).
  3. Marca el cliente como **Hecho** y/o **Listo para mandar**.
  4. Introduce el **importe** del cliente. Puede ser 0 o negativo (a devolver o compensar).
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
- **Flujo:** añade o edita una nota libre en el cliente para ese periodo, por ejemplo "falta factura de octubre".

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
- **Flujo:** indica la fecha de baja. El cliente se mantiene en el periodo que contiene esa fecha (RN-03) y deja de aparecer en los siguientes. Su histórico se conserva.
- **Reactivar:** se registra una nueva fecha de alta y el cliente vuelve a aparecer desde el periodo que la contiene. **[S]** Se guarda el historial de altas y bajas, porque un cliente puede darse de baja y volver más tarde.

### CU-14 Configurar modelos de un cliente
- **Actor:** Administrador
- **Flujo:** añade o quita modelos a un cliente con fecha de alta o baja (por ejemplo, "alta en 115 el 17-07-2026" o "baja IVA 31-3-25").

### CU-15 Crear periodo
- **Actor:** Administrador
- **Flujo principal:**
  1. Elige el tipo (trimestral o anual), el año y, si es trimestral, el trimestre.
  2. El sistema calcula los **clientes vigentes** del periodo (RN-01) y propone **copiar las asignaciones** del periodo anterior del mismo tipo.
  3. El sistema crea una asignación *Pendiente* por cada cliente vigente. Los que estaban asignados en el periodo anterior conservan su empleado y el resto quedan sin asignar.
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
- **Flujo:** muestra, por empleado, los pendientes, los hechos, los listos para mandar y el porcentaje en el periodo elegido. Se puede entrar al detalle de cada empleado.

### CU-19 Histórico de cliente
- **Actor:** Administrador
- **Flujo:** muestra la ficha del cliente con todos sus periodos: empleado, estado e importe de cada uno.

### CU-20 Exportar periodo a Excel *(deseable)*
- **Actor:** Administrador
- **Flujo:** descarga un .xlsx con el formato de cuadrante, por empleado o completo.

---

## 8. Propuesta de issues (backlog)

### Hito 0 — Arranque
1. Crear la solución en Visual Studio: proyecto WPF, proyecto de lógica/datos y proyecto de tests.
2. Configurar la base MVVM (CommunityToolkit.Mvvm), la inyección de dependencias y la navegación entre vistas.
3. Crear el esquema de base de datos con Entity Framework Core y sus migraciones (sección 6).
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
14. RN-01…RN-05 Cálculo de vigencia de clientes y modelos por periodo, con tests de los casos límite.
15. CU-15 Crear periodo y generar asignaciones.
16. RF-41 Copiar asignaciones del periodo anterior.
17. CU-16 Asignación individual y masiva.
18. CU-17 Cerrar y reabrir periodo.

### Hito 4 — Vista del empleado
19. CU-03 Vista trimestral (pestañas físicas/sociedades).
20. CU-04 Vista anual.
21. CU-05 / CU-06 Ficha del cliente: Hecho, Listo para mandar e importe.
22. CU-07 Observaciones.
23. RF-17 Progreso, filtros y búsqueda.

### Hito 5 — Seguimiento
24. CU-18 Panel de administrador.
25. CU-19 Histórico de cliente.
26. RNF-05 Registro de cambios (auditoría).

### Hito 6 — Extras y despliegue
27. CU-20 Exportar a Excel.
28. Instalar la base de datos compartida y configurar sus copias de seguridad.
29. Publicar el instalador (ClickOnce/MSIX) y desplegarlo en los PCs.

---

## 9. Decisiones tomadas

| # | Pregunta | Decisión |
|---|---|---|
| 1 | ¿Hecho, Listo para mandar e Importe van por modelo o por cliente? | **Por cliente.** Los modelos solo se muestran como información. |
| 2 | ¿Hacen falta más estados? | **No.** Solo *Pendiente*, *Hecho* y *Listo para mandar*. |
| 3 | ¿Qué modelos entran en lo anual? | **Solo** 390, 190, 180, 347 y Renta (100). El 200 y el 202 no entran en lo anual. |
| 4 | ¿Los clientes de Guillermo son todos personas físicas? | Por ahora sí, pero el administrador puede asignarle sociedades en cualquier momento. No hay restricción por empleado. |
| 5 | ¿Una o dos cuentas para el administrador? | **Una sola cuenta** con rol `ADMIN`, que incluye "Mis asignaciones". |
| 6 | ¿Qué tecnología? | **Aplicación de escritorio WPF** (C#, .NET 8, Visual Studio) con MVVM, Entity Framework Core y una base de datos compartida. Se distribuye con un instalador. |

## 10. Dudas abiertas

1. **Dónde va la base de datos** (depende de dónde trabajan los empleados):
   - Si todos trabajan en la **misma oficina y red**: **SQL Server Express** (gratuito) en el PC de la oficina que más tiempo esté encendido o en un servidor. Es la opción recomendada.
   - Si alguno trabaja **desde casa**: una base de datos en la nube (Azure SQL o PostgreSQL gestionado). Tiene un coste mensual pequeño.
   - Se descarta usar SQLite en una carpeta compartida, porque falla cuando varios usuarios escriben a la vez.
2. **[S] Modelos 200 y 202 en la vista trimestral:** se muestran en todos los trimestres, igual que en los Excel actuales. ¿Correcto?
