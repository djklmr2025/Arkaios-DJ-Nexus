# Arkaios DJ Nexus (v1.0.0) 🎧🌐

**Arkaios DJ Nexus** es el ecosistema inteligente definitivo para DJs. Nace como una herramienta local ultra-optimizada para la predicción armónica, y evoluciona hacia una plataforma en la nube conectada con descargas asíncronas y motores multimedia.

## 🚀 Fase 1: El Asistente Local (Completado - v1.0)
Un sistema de escritorio desarrollado en `C#` (.NET) capaz de conectarse directamente a la base de datos y al historial en vivo de **VirtualDJ**.
- **Motor Armónico Avanzado:** Utiliza las reglas de *Mixed In Key* para predecir canciones compatibles, incluyendo *Energy Boosts* de +1 y +2 semitonos.
- **Inteligencia Predictiva por Géneros:** Agrupa la música en "Familias de Géneros" (ej. Familia Latina) para predecir la atmósfera de la pista de baile.
- **Ghost Tracks (Pistas Fantasma):** Detecta canciones del historial que ya no existen localmente y permite descargarlas en vivo.
- **YouTube Engine:** Descarga y convierte al vuelo canciones recomendadas desde YouTube usando `yt-dlp`.

---

## ☁️ Fase 2: Arquitectura Web y APIs (En Desarrollo)
Arkaios DJ Nexus expandirá sus horizontes hacia una infraestructura basada en la web (Node.js/Cloud), permitiendo una sincronización global de cuentas de DJ, perfiles y estadísticas (Trackpads).

### Integración de Módulos (APIs de Arkaios)
Para evitar la redundancia y crear un verdadero ecosistema, la plataforma web expondrá **Endpoints de API** para comunicarse bidireccionalmente con los otros dos grandes módulos del sistema Arkaios:

#### 1. API: Arkaios Media Cutter Studio
*Sistema de extracción y partición de Mixes enteros en Tracks individuales.*
- **Flujo Documentado:** Si Arkaios DJ Nexus detecta que un "Ghost Track" es muy difícil de conseguir como single, pero existe dentro de un Megamix de YouTube, el servidor mandará una orden al Media Cutter mediante API para descargar el Mix completo, cortarlo usando marcas de tiempo (timestamps) y devolverle a este sistema exclusivamente el track faltante ya procesado.

#### 2. API: Arkaios Karaoke & Video Request
*Sistema especializado de descargas directas de Video y Audio (Karaokes).*
- **Flujo Documentado:** Ideal para escenarios donde el DJ no solo necesita el MP3, sino el Video Musical Oficial (MP4) para proyección en pantallas. El asistente DJ conectará con el módulo de Karaoke para solicitar resoluciones específicas o extraer la pista vocal/instrumental, unificando ambas bases de datos.

---
*Hecho por Arkaios & Antigravity IDE.*
