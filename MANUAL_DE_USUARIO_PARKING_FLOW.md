# 🚗 MANUAL INTEGRAL DE USUARIO — PARKING FLOW
## Guía Operativa y Administrativa Paso a Paso (Punto de Venta WPF y Plataforma Web PWA)

---

> **Bienvenido a Parking Flow**. Este manual ha sido redactado con un lenguaje sencillo, claro y sin términos técnicos complejos, pensado para que cualquier operador de patio, cajero, supervisor o administrador del parqueadero domine todas las funciones del sistema desde su primer día de trabajo.
> 
> A lo largo de este documento encontrarás recuadros especiales donde podrás pegar las capturas de pantalla reales de tu sistema para crear una guía visual personalizada para tu equipo.

---

# 📑 TABLA DE CONTENIDO

1. [PARTE 1: CLIENTE DE ESCRITORIO (WPF) — OPERACIÓN DIARIA EN TERMINAL](#-parte-1-cliente-de-escritorio-wpf--operación-diaria-en-terminal)
   - [1.1 Inicio de Sesión y Selección de Sede](#11-inicio-de-sesión-y-selección-de-sede)
   - [1.2 Pantalla Principal y Monitoreo de Patio](#12-pantalla-principal-y-monitoreo-de-patio)
   - [1.3 Registro de Entrada de Vehículos (Check-In)](#13-registro-de-entrada-de-vehículos-check-in)
   - [1.4 Cobro, Liquidación y Salida de Vehículos (Check-Out)](#14-cobro-liquidación-y-salida-de-vehículos-check-out)
   - [1.5 Aplicación de Convenios y Comercios Aliados](#15-aplicación-de-convenios-y-comercios-aliados)
   - [1.6 Emisión de Factura Electrónica DIAN en Caja](#16-emisión-de-factura-electrónica-dian-en-caja)
   - [1.7 Previsualización e Impresión Térmica de Tiquetes (58mm / 80mm)](#17-previsualización-e-impresión-térmica-de-tiquetes-58mm--80mm)
   - [1.8 Casos Especiales: Tiquete Extraviado o Dañado](#18-casos-especiales-tiquete-extraviado-o-dañado)
   - [1.9 Gestión de Mensualidades y Vehículos Abonados](#19-gestión-de-mensualidades-y-vehículos-abonados)
   - [1.10 Cierre de Turno y Arqueo de Caja (Informe Z)](#110-cierre-de-turno-y-arqueo-de-caja-informe-z)
   - [1.11 Relevo de Turno entre Operadores](#111-relevo-de-turno-entre-operadores)
   - [1.12 Modo Sin Conexión (Offline) y Sincronización Automática](#112-modo-sin-conexión-offline-y-sincronización-automática)

2. [PARTE 2: PLATAFORMA WEB ADMINISTRATIVA (PWA) — GESTIÓN EN LA NUBE](#-parte-2-plataforma-web-administrativa-pwa--gestión-en-la-nube)
   - [2.1 Acceso al Panel Web Administrativo](#21-acceso-al-panel-web-administrativo)
   - [2.2 Panel de Control (Dashboard) y Métricas en Tiempo Real](#22-panel-de-control-dashboard-y-métricas-en-tiempo-real)
   - [2.3 Configuración de Sedes y Patios](#23-configuración-de-sedes-y-patios)
   - [2.4 Configuración de Tarifas y Tiempos de Gracia](#24-configuración-de-tarifas-y-tiempos-de-gracia)
   - [2.5 Creación y Administración de Convenios Comerciales](#25-creación-y-administración-de-convenios-comerciales)
   - [2.6 Configuración de Facturación Electrónica DIAN / Siigo Nube](#26-configuración-de-facturación-electrónica-dian--siigo-nube)
   - [2.7 Registro de Resoluciones de Facturación DIAN por Sede](#27-registro-de-resoluciones-de-facturación-dian-por-sede)
   - [2.8 Directorio de Clientes y Registro de Vehículos](#28-directorio-de-clientes-y-registro-de-vehículos)
   - [2.9 Reportes Financieros, Liquidaciones y Auditoría](#29-reportes-financieros-liquidaciones-y-auditoría)

---

# 🖥️ PARTE 1: CLIENTE DE ESCRITORIO (WPF) — OPERACIÓN DIARIA EN TERMINAL

El cliente de escritorio es el programa instalado en el computador físico del parqueadero. Está diseñado para funcionar a máxima velocidad con teclado, lector de código de barras e impresora térmica, incluso si se cae el servicio de internet.

---

### 1.1 Inicio de Sesión y Selección de Sede

> 📸 **[PEGAR AQUÍ CAPTURA DE PANTALLA: Ventana de Inicio de Sesión - Formulario con Usuario, Contraseña y Logo de Parking Flow]**

#### ¿Para qué sirve esta pantalla?
Es la puerta de entrada segura al sistema. Identifica al cajero u operador responsable del turno de trabajo y valida que el programa se encuentre actualizado.

#### Campos y Botones Explicados Uno a Uno:
1. **Campo `Usuario o Correo Electrónico`**: Escribe el nombre de usuario o correo asignado por el administrador.
2. **Campo `Contraseña`**: Escribe tu clave secreta de acceso. Los caracteres se ocultarán por seguridad.
3. **Botón con Ícono de Ojo `Ver / Ocultar Contraseña`**: Permite hacer visible la clave por un momento para verificar que la hayas escrito correctamente sin equivocaciones.
4. **Botón `Iniciar Sesión`**: Verifica tus credenciales. Si los datos son válidos, el sistema te dará la bienvenida.
5. **Selector de Sede (`¿En qué sede vas a operar hoy?`)**:
   > 📸 **[PEGAR AQUÍ CAPTURA DE PANTALLA: Selector de Sede - Ventana emergente con las tarjetas o botones de cada parqueadero]**
   - Si tu usuario administra varias sedes, aparecerá un listado con los nombres de los parqueaderos disponibles.
   - Haz un solo clic sobre la sede en la que estás trabajando físicamente en ese momento.

---

### 1.2 Pantalla Principal y Monitoreo de Patio

> 📸 **[PEGAR AQUÍ CAPTURA DE PANTALLA: Pantalla Principal del Punto de Venta - Vista general con tarjetas de ocupación, reloj, barra de estado y panel de entrada]**

#### ¿Para qué sirve esta pantalla?
Es tu centro de mando operativo durante todo el turno. Desde aquí registras las entradas, buscas vehículos para cobrar y vigilas en tiempo real cuántos cupos libres tienes.

#### Elementos de la Barra Superior:
* **Indicador de Conexión (`En Línea` / `Sin Conexión`)**:
  - **Punto Verde (`En Línea`)**: El computador tiene internet y está sincronizado con la nube al instante.
  - **Punto Naranja (`Trabajando Local / Fuera de Línea`)**: No hay internet, pero **el parqueadero sigue trabajando al 100% con normalidad**. Tus cobros y entradas se guardan de forma segura en la base de datos local y se subirán a la nube automáticamente cuando vuelva la conexión.
* **Botón `Sincronizar` (Flechas circulares)**: Permite forzar el envío inmediato de cualquier venta pendiente hacia el servidor central.
* **Nombre de la Sede Activa**: Muestra en qué parqueadero estás operando (ejemplo: *Sede Calle 139*).
* **Nombre del Operador y Rol**: Muestra el nombre del cajero activo (ejemplo: *Carlos Rodríguez - Cajero*).
* **Botón `Relevo de Turno`**: Permite cambiar de cajero en caliente sin apagar la terminal.
* **Botón `Cerrar Sesión`**: Cierra la sesión activa de forma segura.

#### Tarjetas de Capacidad y Ocupación en Tiempo Real:
* **Tarjeta `Vehículos en Patio`**: Número total de vehículos que están estacionados actualmente adentro.
* **Tarjeta `Cupos Disponibles`**: Cantidad de espacios libres que quedan en el parqueadero. Si llega a 0, se encenderá en color rojo indicando *"CUPO LLENO"*.
* **Tarjetas por Categoría**: Muestra el conteo desglosado para:
  - 🚗 **Carros** (Ocupados / Capacidad máxima).
  - 🏍️ **Motos** (Ocupados / Capacidad máxima).
  - 🚲 **Bicicletas** (Ocupados / Capacidad máxima).
  - 🚚 **Pesados / Camiones** (si la sede lo tiene habilitado).
* **Tarjeta `Recaudo del Turno`**: Dinero total recaudado en efectivo y medios digitales durante el turno en curso.

---

### 1.3 Registro de Entrada de Vehículos (Check-In)

> 📸 **[PEGAR AQUÍ CAPTURA DE PANTALLA: Módulo de Registro de Entrada - Formulario con campo de placa gigante, selector de tipo de vehículo y botón de ingreso]**

#### Pasos para Ingresar un Vehículo:
1. **Digita la Placa**: Escribe las letras y números de la placa en el campo principal. El sistema convierte automáticamente las letras a mayúsculas.
   - *Ejemplo Carro:* `ABC123`
   - *Ejemplo Moto:* `XYZ12D`
2. **Selecciona el Tipo de Vehículo**:
   - Haz clic en el botón correspondiente: **Carro**, **Moto**, **Bicicleta** o **Pesado**.
   - El sistema cargará automáticamente la tarifa por minuto o por hora asignada a ese tipo de vehículo.
3. **Casilla `Bahía / Celda` (Opcional)**: Si tu parqueadero tiene puestos numerados, puedes escribir el número de celda (ej: `A-12`). Si no manejas celdas numeradas, puedes dejarla en blanco.
4. **Casilla `Teléfono del Conductor` (Opcional)**: Puedes digitar el número celular del cliente (ej: `3001234567`). Esto le permitirá al cliente consultar su tiempo y cobro en línea escaneando el QR del tiquete con su propio teléfono móvil.
5. **Casilla `Observaciones / Novedades` (Opcional)**: Utilízala para registrar detalles físicos del vehículo antes de que ingrese (ej: *"Rayón en puerta derecha"*, *"Deja dos cascos negros"*).
6. **Presiona el Botón Verde `Ingresar Vehículo / Imprimir Tiquete`** (o presiona la tecla `ENTER`):
   - El sistema guardará la hora exacta de ingreso (con segundos).
   - Se abrirá la tiquetera e imprimirá el **Tiquete de Entrada Oficial** con código de barras y QR.
   - El cupo disponible se descontará de inmediato en el panel superior.

---

### 1.4 Cobro, Liquidación y Salida de Vehículos (Check-Out)

> 📸 **[PEGAR AQUÍ CAPTURA DE PANTALLA: Ventana de Cobro y Salida - Modal emergente con tiempos, cálculo de tarifa, métodos de pago y desglose numérico]**

#### ¿Cómo buscar un vehículo para darle salida?
Tienes tres métodos rápidos:
1. **Lector de Código de Barras**: Pasa la pistola lectora sobre el código de barras impreso en el tiquete que te entrega el conductor. La ventana de cobro se abrirá de inmediato.
2. **Búsqueda por Placa**: Escribe las letras de la placa en el buscador rápido. El sistema filtrará la lista en tiempo real.
3. **Clic directo en la Lista de Patio**: Busca el vehículo en la tabla de vehículos activos y haz doble clic sobre él.

#### Explicación de la Ventana de Cobro:
Al seleccionar el vehículo, se abrirá la pantalla de liquidación económica:
* **Cronómetro y Tiempo de Estadía**:
  - Muestra la hora exacta en que entró, la hora actual de salida y el tiempo total transcurrido (ejemplo: `1 hora 24 minutos 12 segundos`).
* **Tarifa Aplicada**: Muestra el valor configurado por hora o fracción de minuto.
* **TOTAL NETO A PAGAR (Cifra Gigante en Pantalla)**:
  - Es el valor exacto y definitivo que se le debe cobrar al conductor.
* **Selector de `Método de Pago`**:
  - **Efectivo**: Para pagos con billetes y monedas.
  - **Transferencia Digital**: Para pagos por Nequi, Daviplata, Bancolombia QR o llaves digitales.
  - **Tarjeta Débito / Crédito**: Para cobros con datáfono físico.
* **Sección `Monto en Efectivo Recibido ($)`**:
  - Por defecto se auto-ajusta al valor exacto a pagar.
  - Si el cliente te entrega un billete de mayor denominación (ejemplo: la cuenta es de $3.000 y te entrega un billete de $10.000), tienes dos opciones:
    a) Escribir con el teclado `10000`.
    b) Hacer clic en los botones de acceso rápido: **`$5K`**, **`$10K`**, **`$50K`** o **`Exacto`**.
* **CAMBIO / DEVUELTA AL CLIENTE (Texto Destacado)**:
  - El sistema calcula automáticamente la resta matemática (`Efectivo Recibido - Total a Pagar`) y te muestra en grande el dinero exacto que debes devolverle en la mano al conductor.
  - Si la cuenta es de $3.000 y recibes $10.000, la pantalla dirá: **`CAMBIO / DEVUELTA: $7,000`**.
  - Si el pago es exacto o el valor a pagar es $0, la devuelta indicará **`$0.00`**.
* **Botón `Cobrar y Registrar Salida`**:
  - Registra el pago en caja, libera el cupo en el patio e imprime la factura/recibo de salida.

---

### 1.5 Aplicación de Convenios y Comercios Aliados

> 📸 **[PEGAR AQUÍ CAPTURA DE PANTALLA: Galería de Convenios Comerciales - Botones con logotipos de Archies, Puppis, etc., y badges de descuento]**

#### ¿Qué es un Convenio Comercial?
Es un descuento o tiempo de cortesía que el parqueadero le otorga a los clientes que consumieron en negocios cercanos aliados (como restaurantes, veterinarias, gimnasios, bancos o almacenes).

#### ¿Cómo aplicar un Convenio?
1. En la parte inferior de la ventana de cobro verás la galería con los **logos de los comercios aliados** (ejemplo: *Archies*, *Puppis*).
2. **Haz un solo clic sobre el logo del comercio**:
   - Aparecerá un chulito verde (`✓`) en la esquina del logo confirmando que está seleccionado.
   - En la parte superior derecha de la sección aparecerá una etiqueta destacada: **`Descuento: NOMBRE DEL COMERCIO -$X.XXX`**.
   - El sistema recalculará el **TOTAL NETO A PAGAR** al instante aplicando la rebaja.
3. **¿Qué pasa con el Monto en Efectivo y el Cambio?**
   - **Caso 1: El descuento cubre el 100% de la estadía (Neto a pagar $0.00)**:
     El campo de efectivo recibido se adapta automáticamente a **`$0.00`** y el cambio queda en **`$0.00`**. El conductor no paga nada y el sistema no genera falsas devueltas.
   - **Caso 2: Descuento parcial (Ejemplo: la estadía era de $1.150 y el convenio descuenta $1.000)**:
     El total neto queda en **`$150.00`**. El campo de efectivo recibido se adapta automáticamente a **`$150.00`** con cambio en **`$0.00`**. Si el conductor te paga con una moneda de $500, escribes `500` y el sistema te indica cambio de `$350`.
4. **Convenios con Compra Mínima**:
   - Si el convenio exige que el cliente haya comprado una cantidad mínima en el comercio (ejemplo: mínimo $50.000 en el restaurante), se desplegará una casilla que dice **`VALOR DE COMPRA EN ALIANZA ($)`**.
   - Digita el valor de la factura que te muestra el cliente. Si cumple con el monto requerido, el badge cambiará a verde indicando `✓ Cumple compra mínima`.
5. **Convenios con Límite de Tiempo**:
   - Si un convenio solo cubre hasta 2 horas y el vehículo se quedó 3 horas, el sistema te avisará con una advertencia en pantalla o cobrará únicamente el tiempo excedente, protegiendo las finanzas del parqueadero.
6. **Para Quitar o Desmarcar un Convenio**:
   - Vuelve a hacer clic sobre el mismo logo. El convenio se apagará y el cobro volverá a su valor original de inmediato.

---

### 1.6 Emisión de Factura Electrónica DIAN en Caja

> 📸 **[PEGAR AQUÍ CAPTURA DE PANTALLA: Sección de Factura Electrónica - Selector de Resolución, Interruptor de Factura Electrónica y Búsqueda de Adquirente]**

#### ¿Cuándo se utiliza?
Cuando el conductor solicita una **Factura Electrónica con su Cédula o NIT de Empresa** para efectos contables y tributarios ante la DIAN, o cuando la empresa opera 100% bajo Factura Electrónica de Venta.

#### Pasos para Emitir Factura Electrónica:
1. En la ventana de cobro, activa el interruptor **`Emitir Factura Electrónica`**.
2. **Selecciona la Resolución / Documento**:
   - En el menú desplegable de resoluciones, asegúrate de que esté seleccionada la resolución de Factura Electrónica de tu sede (ejemplo: `POS - 18764113848904` o `FE - Factura Electrónica`).
3. **Buscar o Seleccionar al Cliente (Adquirente)**:
   - En el buscador de clientes, escribe el número de Cédula, NIT o Nombre de la persona/empresa.
   - Si el cliente ya vino antes, aparecerá en la lista desplegable; haz clic sobre su nombre para seleccionarlo.
4. **Registro Rápido de Nuevo Cliente (si no existe)**:
   - Si es la primera vez que el cliente solicita factura, haz clic en el botón **`+ Nuevo Cliente`**.
   - Se abrirá un formulario compacto dentro de la misma pantalla:
     - **Tipo de Documento**: Cédula de Ciudadanía (CC), NIT (Empresas), Cédula de Extranjería (CE) o Pasaporte.
     - **Número de Documento**: Digita el número sin puntos ni guiones (si es NIT, el sistema calcula automáticamente el dígito de verificación).
     - **Nombre Completo o Razón Social**: Nombre de la persona o razón social de la empresa.
     - **Correo Electrónico (Fundamental)**: Dirección de e-mail donde la DIAN y el sistema le enviarán el PDF y XML de la factura electrónica.
     - **Teléfono y Dirección**.
     - **Ciudad / Municipio**: Selecciona la ciudad (ej: Bogotá, Medellín, Cali).
   - Haz clic en **`Guardar Cliente`**.
5. **Finalizar el Cobro**:
   - Presiona **`Cobrar y Registrar Salida`**.
   - El sistema despachará la factura al motor de sincronización DIAN en la nube.
   - La tiquetera imprimirá la factura con su número consecutivo oficial, el código **CUFE** y el **Código QR oficial de la DIAN**.

---

### 1.7 Previsualización e Impresión Térmica de Tiquetes (58mm / 80mm)

> 📸 **[PEGAR AQUÍ CAPTURA DE PANTALLA: Ventana de Previsualización de Tirilla - Vista previa del tiquete con selector de formato de papel y botón de imprimir]**

#### ¿Para qué sirve esta ventana?
Antes de gastar papel o cuando deseas confirmar los datos, el sistema te muestra en pantalla la tirilla exactamente igual a como saldrá físicamente en la impresora térmica.

#### Opciones de la Ventana:
* **Badge Interactivo de Formato de Papel (`Formato: 58 mm ⇄` / `Formato: 80 mm ⇄`)**:
  - Muestra el ancho de papel configurado en la sede.
  - **Puedes hacer clic sobre este badge en cualquier momento**: el diseño cambiará instantáneamente entre el formato de tirilla delgada (58mm) y tirilla ancha (80mm), adaptando las letras, márgenes y códigos para que nunca salgan cortados.
* **Visualización de la Tirilla Limpia**:
  - Gracias al diseño desacoplado, la barra de desplazamiento (scroll) queda en el fondo oscuro de la ventana y **nunca tapa los precios, totales ni textos del tiquete**.
  - Todas las letras del margen izquierdo (`Tiquete #:`, `Fecha:`, `Entrada:`, `Salida:`, `Total:`) se muestran completas sin cortes mecánicos.
* **Botón `Imprimir` (Ícono de Impresora)**: Envía la orden directa al cabezal térmico de la tiquetera física conectada por USB o Red.
* **Botón `Cerrar [X]`**: Cierra la ventana una vez impreso el comprobante.

---

### 1.8 Casos Especiales: Tiquete Extraviado o Dañado

> 📸 **[PEGAR AQUÍ CAPTURA DE PANTALLA: Checkbox de Tiquete Extraviado en Check-out con el recargo configurado]**

#### ¿Qué hacer si el cliente perdió su tiquete físico?
1. En la pantalla principal, busca el vehículo por su placa para abrir la liquidación.
2. Marca la casilla **`Tiquete Extraviado / Dañado`**.
3. El sistema agregará de forma automática el valor del recargo administrativo configurado para tu sede (ejemplo: `Recargo: +$15.000`).
4. En el campo `Observaciones de Salida`, escribe el nombre y cédula del conductor que retira el vehículo tras verificar la tarjeta de propiedad.
5. Procede a cobrar normalmente. En la factura impresa saldrá desglosado el cobro del tiempo de estadía más el concepto claro de *"Recargo por Tiquete Extraviado"*.

---

### 1.9 Gestión de Mensualidades y Vehículos Abonados

> 📸 **[PEGAR AQUÍ CAPTURA DE PANTALLA: Módulo de Mensualidades - Listado de abonados con estados en verde (Vigente), amarillo (Por Vencer) y rojo (Vencido)]**

#### ¿Cómo funciona un vehículo abonado en Parking Flow?
Los vehículos que pagan mensualidad tienen un tratamiento especial:
* **Ingreso y Salida Automática**: Al digitar la placa de un abonado vigente en la entrada o salida, el sistema reconoce su contrato activo, registra el movimiento en patio para controlar el cupo y **cobra $0.00 pesos de estadía**.

#### Pasos para Crear una Nueva Mensualidad:
1. Dirígete a la pestaña o botón **`Mensualidades`**.
2. Haz clic en el botón superior **`+ Nueva Mensualidad`**.
3. Diligencia los datos requeridos:
   - **Cliente / Titular**: Nombre, Cédula/NIT, Teléfono y Correo.
   - **Placa del Vehículo**: Placa asignada al plan mensual.
   - **Tipo de Vehículo**: Carro o Moto.
   - **Fecha de Inicio y Fecha de Vencimiento**: El sistema sugiere 30 días calendario por defecto.
   - **Valor del Plan Mensual**: Digita el valor pactado (ej: `$150.000`).
   - **Método de Pago**: Efectivo, Transferencia o Tarjeta.
4. Presiona **`Guardar y Emitir Comprobante`**. Se imprimirá el recibo de pago de la mensualidad para el cliente.

---

### 1.10 Cierre de Turno y Arqueo de Caja (Informe Z)

> 📸 **[PEGAR AQUÍ CAPTURA DE PANTALLA: Diálogo de Cierre de Turno y Arqueo de Caja - Desglose de ingresos por medio de pago, campo de conteo ciego y balance]**

#### ¿Para qué sirve?
Es el procedimiento obligatorio al finalizar tu jornada de trabajo. Garantiza que el dinero físico que tienes en el cajón de dinero coincida con las ventas registradas por el sistema.

#### Pasos para Realizar el Cierre:
1. En la parte superior de la pantalla principal, haz clic en **`Cierre de Turno`**.
2. **Revisa el Resumen Automático del Sistema**:
   - Total recaudado por tiempo de estadía.
   - Total recaudado por tiquetes extraviados.
   - Total cobrado por mensualidades.
   - Total descuentos otorgados por convenios comerciales.
   - Desglose por método de pago: cuánto fue en **Efectivo**, cuánto en **Bancos / Transferencias** y cuánto en **Tarjetas**.
3. **Sección `Conteo Físico de Efectivo (Arqueo)`**:
   - Cuenta los billetes y monedas que tienes físicamente en la gaveta (sin contar la base inicial de sencillo).
   - Escribe esa cifra en la casilla **`Efectivo Físico en Caja`**.
4. **Validación del Balance**:
   - Si la cifra coincide exactamente, el sistema mostrará en verde: **`✓ Caja Cuadrada`** (Diferencia: $0).
   - Si hay sobrante, indicará en azul: `Sobrante: +$X.XXX`.
   - Si falta dinero, indicará en rojo: `Faltante: -$X.XXX`.
5. **Casilla `Observaciones de Cierre`**: Escribe cualquier novedad ocurrida durante el turno (ej: *"Se dejaron $50.000 de base para el turno de la noche"*).
6. **Presiona el Botón `Cerrar Turno e Imprimir Acta`**:
   - El turno quedará formalmente cerrado en el sistema y no se podrán alterar los números.
   - La impresora térmica imprimirá el **Informe de Cierre de Caja (Informe Z)** con el balance, las firmas del cajero y del supervisor.

---

### 1.11 Relevo de Turno entre Operadores

> 📸 **[PEGAR AQUÍ CAPTURA DE PANTALLA: Ventana de Relevo de Turno - Cambio de cajero sin cerrar la jornada global]**

#### ¿Cuándo se utiliza?
Cuando un cajero sale a almorzar o entrega su puesto a un compañero de turno:
1. Haz clic en el botón superior **`Relevo de Turno`**.
2. El cajero saliente confirma el estado del patio.
3. El cajero entrante digita su usuario y contraseña.
4. El sistema sincroniza los permisos de la nueva persona al instante sin necesidad de reiniciar la computadora.

---

### 1.12 Modo Sin Conexión (Offline) y Sincronización Automática

> 📸 **[PEGAR AQUÍ CAPTURA DE PANTALLA: Ventana de Progreso de Sincronización - Barra de porcentaje y estado de subida de ventas locales a la nube]**

#### ¿Qué debes saber si se va el internet en el parqueadero?
* **Cero Preocupaciones**: Parking Flow tiene tecnología *Offline-First* de misión crítica. La base de datos vive dentro de tu propio computador.
* Podrás seguir ingresando vehículos, aplicando convenios, cobrando e imprimiendo tiquetes con total normalidad.
* En cuanto la conexión a internet retorne, verás una pequeña notificación verde y el motor interno subirá todas las ventas en cola hacia la nube sin que tengas que hacer nada manual.

---

# 🌐 PARTE 2: PLATAFORMA WEB ADMINISTRATIVA (PWA) — GESTIÓN EN LA NUBE

La plataforma web (PWA) permite a los dueños, administradores y contadores gestionar el parqueadero desde cualquier lugar del mundo (desde un computador portátil, tablet o teléfono celular con solo abrir el navegador web).

---

### 2.1 Acceso al Panel Web Administrativo

> 📸 **[PEGAR AQUÍ CAPTURA DE PANTALLA: Login de la Plataforma Web PWA en navegador de escritorio o móvil]**

1. Abre tu navegador web (Google Chrome, Microsoft Edge o Safari) e ingresa a la dirección web de tu sistema (ejemplo: `https://app.tuparkingflow.com`).
2. Digita tu correo electrónico de administrador y tu contraseña.
3. Si administras varias empresas o sedes, la barra superior te permitirá seleccionar en qué sede deseas consultar información en ese instante.

---

### 2.2 Panel de Control (Dashboard) y Métricas en Tiempo Real

> 📸 **[PEGAR AQUÍ CAPTURA DE PANTALLA: Dashboard Web Principal - Gráficos de barras de rotación vehicular, ingresos diarios y donas de ocupación]**

#### ¿Qué información visual encuentras aquí?
* **Ingresos de Hoy**: Total de dinero facturado durante el día en curso en tiempo real.
* **Ocupación Actual**: Porcentaje de ocupación del patio y cupos libres disponibles en este segundo.
* **Gráfico de Horas Pico**: Muestra a qué horas del día entra más tráfico al parqueadero, ideal para planear turnos de personal.
* **Gráfico de Medios de Pago**: Porcentaje recaudado en efectivo vs transferencias digitales y tarjetas.
* **Monitor de Facturación DIAN**: Cantidad de facturas electrónicas enviadas y aceptadas exitosamente ante la DIAN.

---

### 2.3 Configuración de Sedes y Patios

> 📸 **[PEGAR AQUÍ CAPTURA DE PANTALLA: Módulo de Configuración de Sedes - Formulario de capacidades, dirección y parámetros de impresión]**

#### En este módulo configuras las características físicas de cada parqueadero:
* **Nombre de la Sede**: (Ej: *Sede Norte Calle 139*).
* **Dirección Física y Teléfono**: Los cuales aparecerán impresos en el encabezado de todos los tiquetes térmicos.
* **Capacidad Máxima de Vehículos**:
  - Cupos para Carros (ej: `45`).
  - Cupos para Motos (ej: `30`).
  - Cupos para Bicicletas (ej: `10`).
* **Ancho de Papel Predeterminado**: Selecciona si las impresoras de esa sede usan papel de **58 mm** o papel de **80 mm**.

---

### 2.4 Configuración de Tarifas y Tiempos de Gracia

> 📸 **[PEGAR AQUÍ CAPTURA DE PANTALLA: Módulo de Tarifas - Tablas de precios por tipo de vehículo con tiempos de gracia]**

#### ¿Cómo configurar los precios del parqueadero?
Para cada tipo de vehículo (Carro, Moto, Bicicleta, Pesado) puedes definir:
1. **Modalidad de Cobro**:
   - **Por Minuto**: Cobro fraccionado exacto por cada minuto de estadía.
   - **Por Hora Plena**: Cobro por cada hora o fracción de hora que transcurra.
2. **Valor Hora / Minuto**: Digita el precio oficial (ejemplo: Carro `$3.600` la hora, Moto `$2.000` la hora).
3. **Tiempo de Gracia de Entrada (Minutos de Tolerancia)**:
   - Tiempo de cortesía inicial para vehículos que entran y salen de inmediato (ejemplo: si configuras `10 minutos`, un conductor que entre a dejar un paquete y salga antes de 10 minutos pagará `$0.00`).
4. **Tiempo de Gracia de Salida (Tolerancia tras Pagar)**:
   - Minutos que tiene el conductor para subir a su vehículo y desocupar el parqueadero una vez pagó en caja sin que se le cobre tiempo extra (ejemplo: `15 minutos`).
5. **Valor Recargo por Tiquete Perdido**: Tarifa fija a cobrar al usuario que no presente el tiquete de ingreso (ejemplo: `$15.000`).

---

### 2.5 Creación y Administración de Convenios Comerciales

> 📸 **[PEGAR AQUÍ CAPTURA DE PANTALLA: Formulario de Creación de Convenio Comercial - Nombre, logo, tipo de descuento y reglas]**

#### Pasos para Crear una Alianza con un Negocio Cercano:
1. Ingresa a la sección **`Convenios y Alianzas`** y presiona **`+ Nuevo Convenio`**.
2. **Nombre del Comercio**: Escribe el nombre del negocio (ejemplo: *Restaurante Archies*, *Veterinaria Puppis*).
3. **Cargar Logotipo**: Sube una imagen con el logo del comercio. **Este logo aparecerá automáticamente en el botón de la pantalla de cobro del cajero**.
4. **Regla de Descuento**:
   - **Descuento en Porcentaje**: (Ej: `50%` de rebaja o `100%` de gratuidad).
   - **Descuento en Dinero Fijo**: (Ej: Descontar `-$2.000` pesos fijos de la cuenta).
   - **Horas o Minutos Gratis**: (Ej: `2 Horas Gratis` de parqueadero).
5. **Condición de Compra Mínima (Opcional)**:
   - Si el negocio exige que el cliente compre algo para darle el descuento, escribe el valor mínimo (ejemplo: `$50.000`).
6. **Límite de Tiempo Máximo Aplicable (Opcional)**:
   - Máximo de horas en que aplica el convenio (ej: solo aplica si el cliente no supera las 2 horas).
7. Presiona **`Guardar Convenio`**. El convenio se sincronizará de inmediato con todas las terminales físicas de caja.

---

### 2.6 Configuración de Facturación Electrónica DIAN / Siigo Nube

> 📸 **[PEGAR AQUÍ CAPTURA DE PANTALLA: Módulo de Configuración Siigo - Credenciales API, interruptores de modo producción y parámetros contables]**

#### ¿Para qué sirve este módulo?
Permite enlazar Parking Flow directamente con el software contable **Siigo Nube** para que todas las ventas se timbren de forma automática ante la DIAN sin digitar facturas a mano en dos sistemas.

#### Campos de Configuración Explicados:
* **Interruptor `Facturación Electrónica Activa`**: Enciende o apaga la emisión de factura electrónica en la empresa.
* **Usuario API (`apiUsername`)**: Correo electrónico registrado para el usuario API de Siigo.
* **Clave de Acceso (`Access Key`)**: Llave secreta suministrada por Siigo para conectar con su API.
* **Selector de Modo (`Modo Pruebas / Sandbox` vs `Modo Producción DIAN`)**:
  - **Modo Pruebas**: Para capacitar cajeros sin enviar facturas reales a la DIAN.
  - **Modo Producción**: Conecta directamente con la DIAN en vivo. Las facturas son oficiales, generan CUFE y tienen validez tributaria.
* **Interruptor `Timbrado Automático DIAN (stamp.send)`**: Debe estar **Activado (`true`)** en producción para que Siigo envíe la factura a validar ante la DIAN al instante.
* **Interruptor `Enviar Correo al Cliente (mail.send)`**: Si se activa, Siigo le despachará un e-mail al cliente con su factura en PDF y XML automáticamente.
* **ID Vendedor (`sellerId`)**: Código numérico del usuario de Siigo que firma la factura.
* **Código de Producto (`defaultProductCode`)**: Código del servicio de parqueadero en el catálogo de Siigo (ejemplo: `PARK-01`).

---

### 2.7 Registro de Resoluciones de Facturación DIAN por Sede

> 📸 **[PEGAR AQUÍ CAPTURA DE PANTALLA: Módulo de Resoluciones de Facturación - Formulario con Número de Formulario 1876, Prefijo, Rango y Asignación de Sede]**

#### ¿Cómo registrar una Resolución de la DIAN (como el Formulario 1876)?
1. Ingresa a **`Facturación ➔ Resoluciones de Facturación`** y haz clic en **`+ Nueva Resolución`**.
2. **Sede Asignada**: Selecciona a qué parqueadero pertenece esta resolución (ej: *Sede Calle 139*). **Cada sede debe tener su propia resolución independiente**.
3. **Número de Resolución (Casilla 4 del Formulario 1876)**: Digita el número de formulario (ejemplo: `18764113848904`).
4. **Prefijo Autorizado (Casilla 31)**: Escribe el prefijo exacto en mayúsculas (ejemplo: `POS` o `FE`).
5. **Rango Desde / Rango Hasta (Casillas 32 y 33)**: Digita los números de folio autorizados (ejemplo: Desde `10001` Hasta `500000`).
6. **Vigencia (Válida Desde / Válida Hasta)**: Selecciona las fechas de formalización y vencimiento de la resolución (ejemplo: desde el `11/08/2026` hasta el `11/08/2028`).
7. **ID Documento Siigo (`SiigoDocumentId`)**: Escribe el identificador numérico que Siigo le asignó a este comprobante en su plataforma (ejemplo: `32450`).
8. **Clave Técnica (Technical Key)**: Pega la clave alfanumérica larga que entrega la DIAN en el portal MUISCA.
9. **Casilla `Es Resolución Electrónica`**: Marca esta casilla en `SÍ`.
10. Presiona **`Guardar Resolución`**. A partir de ese momento, la sede facturará estrictamente con estos parámetros oficiales ante la DIAN.

---

### 2.8 Directorio de Clientes y Registro de Vehículos

> 📸 **[PEGAR AQUÍ CAPTURA DE PANTALLA: Módulo de Clientes - Directorio con búsqueda por cédula, NIT, nombres y correo electrónico]**

* **Directorio Centralizado**: Consulta los datos de clientes frecuentes, empresas y adquirentes de factura electrónica.
* Puedes editar correos electrónicos mal escritos, actualizar direcciones o registrar números de teléfono para que la comunicación sea impecable.

---

### 2.9 Reportes Financieros, Liquidaciones y Auditoría

> 📸 **[PEGAR AQUÍ CAPTURA DE PANTALLA: Centro de Reportes - Filtros por rango de fechas, sedes, selector de formato Excel / PDF y gráficos]**

#### Tipos de Reportes Disponibles para Descargar:
1. **Reporte General de Ventas**: Detalle de todos los tiquetes cobrados (fecha, hora, placa, tiempo, método de pago, valor cobrado y cajero que atendió).
2. **Reporte de Liquidación de Convenios**: Muestra cuántos vehículos y cuánto dinero en descuentos se le aplicó a cada comercio aliado (ideal para pasarle la cuenta de cobro mensual al restaurante o veterinaria aliada).
3. **Reporte de Cierres de Turno**: Historial de actas de caja de todos los operadores para auditoría de descuadres o faltantes.
4. **Exportación a Excel / PDF**: Cualquier reporte se puede exportar con un solo clic a formato Excel para trabajo contable o a formato PDF listo para imprimir.

---

# 💡 PREGUNTAS FRECUENTES Y SOLUCIÓN RÁPIDA DE DUDAS

### 1. ¿Qué hago si la tiquetera física no imprime el papel?
* Verifica que el cable USB o de red esté bien conectado al computador.
* Abre la tapa de la impresora y asegúrate de que el rollo térmico tenga papel y esté colocado en la dirección correcta (el papel térmico solo se imprime por una de sus caras).
* Si el sistema arrojó la tirilla en pantalla, haz clic directamente en el botón **`Imprimir`** para reenviar la orden.

### 2. ¿Qué pasa si el cliente pagó por Nequi o Daviplata pero la plata no ha caído a la cuenta?
* En la ventana de cobro selecciona **`Transferencia Digital`** y no des salida al vehículo hasta que el conductor te enseñe el comprobante de transferencia exitosa en su aplicación bancaria con la fecha, hora y valor exacto.

### 3. ¿Cómo sé si una factura electrónica ya fue aprobada por la DIAN?
* En el tiquete impreso verás el texto **`CUFE:`** acompañado de una cadena de letras y números larga, junto al código QR oficial. Si escaneas ese código QR con la cámara de cualquier teléfono móvil, se abrirá la página web oficial de la DIAN confirmando que la factura fue aceptada y registrada.

### 4. ¿Puedo trabajar en dos sedes al mismo tiempo desde computadores distintos?
* **Totalmente sí**. Cada computador tiene configurada su propia sede activa. Las ventas de la Sede 1 y de la Sede 2 viajan de manera independiente a la nube sin mezclarse en ningún momento.

---

> **Parking Flow — Sistema Integral de Control de Estacionamientos y Facturación Electrónica DIAN.**  
> *Manual elaborado para operarios, supervisores y administradores.*
