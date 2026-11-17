FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Pulse.slnx ./
COPY backend ./backend
COPY database ./database
RUN dotnet publish backend/Pulse.Api -c Release -o /out/api
RUN dotnet publish backend/Pulse.Worker -c Release -o /out/worker
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
COPY --from=build /out/api .
ENV ASPNETCORE_HTTP_PORTS=8080
USER app
ENTRYPOINT ["dotnet","Pulse.Api.dll"]
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS worker
WORKDIR /app
COPY --from=build /out/worker .
USER app
ENTRYPOINT ["dotnet","Pulse.Worker.dll"]
