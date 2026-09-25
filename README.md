# Examen Parcial - Plataforma de Créditos

Este repositorio contiene la implementación de la Plataforma Web de Gestión de Solicitudes de Crédito, desarrollada como parte del Examen Parcial.

## 🛠️ Tecnologías Usadas
- ASP.NET Core MVC (.NET 10)
- Identity & Entity Framework Core (con SQLite)
- Redis (Session y Cache)
- RabbitMQ (CloudAMQP) para colas de mensajería asíncrona
- PieSocket para notificaciones WebSocket en tiempo real
- Despliegue en Render.com

---

## 🚀 Pasos para ejecutar localmente

### 1. Clonar y Restaurar
```bash
git clone https://github.com/Luis-AH/practica-2-2026-II.git
cd practica-2-2026-II
dotnet restore
```

### 2. Configurar Variables de Entorno (o `appsettings.json`)
Asegúrate de contar con tus conexiones activas en `appsettings.Development.json` o como variables de entorno locales:
- `ConnectionStrings:RedisConnection`
- `PieSocket:AppId`, `ApiKey`, `ApiSecret`, `Cluster`
- `RabbitMq:ConnectionString`

### 3. Migraciones y Base de Datos
La base de datos SQLite se genera automáticamente al iniciar el proyecto junto con la data semilla (DataSeeder), por lo que no es estrictamente necesario correr `dotnet ef database update`. Solo ejecuta:
```bash
dotnet run
```

### 4. Usuarios de Prueba (Data Semilla)
El sistema crea 3 usuarios automáticamente para que puedas probar (todos usan la misma contraseña: `Password123!`):
1. `analista@banco.com` (Rol Analista)
2. `cliente1@banco.com` (Rol Cliente - Con solicitud pendiente)
3. `cliente2@banco.com` (Rol Cliente - Con solicitud aprobada)

---

## ☁️ Despliegue en Render
El proyecto está configurado para desplegarse como un **Web Service** usando Docker.
- **URL de Render:** *(Reemplazar con la URL final de Render)*
- Las variables de entorno (`RabbitMq__ConnectionString`, `Redis__ConnectionString`, `PieSocket__ApiKey`, etc.) deben configurarse directamente en el Dashboard de Render.
- `ASPNETCORE_URLS=http://0.0.0.0:${PORT}` está manejado desde el `Dockerfile` y `Program.cs`.

### ¿Cómo se conserva SQLite entre despliegues?
En la capa gratuita de Render, el almacenamiento es efímero. Para **conservar SQLite entre despliegues y reinicios**, es necesario configurar un **Render Disk** (Disco Persistente).
El disco persistente se debe montar en una ruta específica (ej. `/data`), y la cadena de conexión en Render debe apuntar a ese volumen (`Data Source=/data/app.db`). Si no se usa un Render Disk, la base de datos se reiniciará a su estado inicial (Data Seeder) en cada nuevo *deploy*.

---

## 📸 Evidencias (Preguntas 6 y 7)

*(Nota: Reemplazar los placeholders a continuación con tus capturas reales según lo indicado).*

### Evidencias WebSocket (Pregunta 6)
- **Captura 1:** Sesión del analista aprobando la solicitud y la pantalla del cliente recibiendo la notificación instantánea sin recargar.
  <img width="1892" height="872" alt="paso 6 1" src="https://github.com/user-attachments/assets/5df89802-5968-4134-918b-2b16396109e6" />
  <img width="1891" height="872" alt="paso 6 2" src="https://github.com/user-attachments/assets/a062e262-2598-40ce-8941-7f5b247fcf86" />
  <img width="1046" height="902" alt="paso 6 3" src="https://github.com/user-attachments/assets/cb8dbbfa-dce0-4d59-b1a2-2512a81f31ff" />


- **Captura 2:** Verificación en la pestaña "Network/Red" del navegador mostrando la conexión WebSocket (WS) establecida con éxito.
  <img width="1917" height="992" alt="Captura de pantalla 2026-09-25 092245" src="https://github.com/user-attachments/assets/8d518d52-ce3e-4d06-9b76-4ad9e815de3d" />
<img width="1907" height="833" alt="Captura de pantalla 2026-09-25 092417" src="https://github.com/user-attachments/assets/0f1d4d59-b5e2-41da-9d35-5ba7ce512b3f" />

- **Captura 3:** Prueba de conexión anónima al Hub `/hubs/solicitudes` siendo rechazada (Error 401).
  <img width="1067" height="972" alt="paso 6 4" src="https://github.com/user-attachments/assets/bf7817cb-6e5d-447a-b353-907d6fd75d8b" />


### Evidencias Cloud MQ / RabbitMQ (Pregunta 7)
- **Captura 1 (Consumidor Apagado):** Modificando `RabbitMq:ConsumerEnabled` a `false`, creando una solicitud y mostrando en el panel de CloudAMQP que el mensaje está encolado (Ready: 1).
  <img width="1492" height="305" alt="Captura de pantalla 2026-09-25 102558" src="https://github.com/user-attachments/assets/fe7edf98-7e43-4b86-98cd-bbbfe72a9060" />

- **Captura 2 (Consumo Exitoso):** Reactivando el consumidor, mostrando la cola vacía y la notificación en la vista del cliente.
  <img width="948" height="862" alt="Captura de pantalla 2026-09-25 113308" src="https://github.com/user-attachments/assets/32b5481c-e7fc-41a4-a03b-f6e752734c13" />

- **Captura 3 (No Duplicidad):** Log de la terminal donde se reenvía el mismo `MessageId` desde CloudAMQP y el consumidor advierte "Mensaje ya procesado", evitando duplicados.
 <img width="1262" height="280" alt="Captura de pantalla 2026-09-25 115314" src="https://github.com/user-attachments/assets/fc27a47a-cc74-417b-b0db-a3a28397bd82" />


