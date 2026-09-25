# Etapa de compilación
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar csproj y restaurar dependencias
COPY ["PlataformaCreditos.csproj", "./"]
RUN dotnet restore "PlataformaCreditos.csproj"

# Copiar el resto del código y compilar
COPY . .
RUN dotnet publish "PlataformaCreditos.csproj" -c Release -o /app/publish

# Etapa de ejecución
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Definir el puerto que Render asigna automáticamente (default 8080 en .NET 8+)
ENV ASPNETCORE_HTTP_PORTS=10000
# Para asegurar compatibilidad con Render (Render usa la variable PORT)
# Configuraremos Program.cs o el Entrypoint para usar el puerto de Render

ENTRYPOINT ["dotnet", "PlataformaCreditos.dll"]
