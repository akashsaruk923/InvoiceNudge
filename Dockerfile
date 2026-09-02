# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY InvoiceNudge.sln ./
COPY src/InvoiceNudge.Domain/*.csproj src/InvoiceNudge.Domain/
COPY src/InvoiceNudge.Application/*.csproj src/InvoiceNudge.Application/
COPY src/InvoiceNudge.Infrastructure/*.csproj src/InvoiceNudge.Infrastructure/
COPY src/InvoiceNudge.Web/*.csproj src/InvoiceNudge.Web/
COPY tests/InvoiceNudge.Application.Tests/*.csproj tests/InvoiceNudge.Application.Tests/
RUN dotnet restore src/InvoiceNudge.Web/InvoiceNudge.Web.csproj

COPY . .
RUN dotnet publish src/InvoiceNudge.Web/InvoiceNudge.Web.csproj -c Release -o /app --no-restore

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# Hosts inject PORT; bind to it (fall back to 8080).
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
# Container hosts (Render, Fly) cap inotify instances low; config-file watching then
# throws IOException at startup. We don't hot-reload config in prod, so turn it off.
ENV DOTNET_hostBuilder__reloadConfigOnChange=false
EXPOSE 8080

ENTRYPOINT ["dotnet", "InvoiceNudge.Web.dll"]
