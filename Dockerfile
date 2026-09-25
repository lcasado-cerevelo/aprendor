FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY *.csproj ./
RUN dotnet restore
COPY . ./
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app ./
# App_Data (logs y archivos subidos) es lo único donde la app escribe.
RUN mkdir -p /app/App_Data && chown -R $APP_UID /app/App_Data
# Sin root: el usuario sin privilegios que traen las imágenes de .NET 8.
USER $APP_UID
# Los secretos (APRENDOR_Jwt__Key, APRENDOR_Email__ApiKey, APRENDOR_App__BaseUrl...)
# van como variables de entorno del contenedor, nunca dentro de la imagen.
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "TrainingPlatform.dll"]
