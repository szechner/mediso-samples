FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 5000
    
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/Mediso.PaymentSample.Api/Mediso.PaymentSample.Api.csproj", "src/Mediso.PaymentSample.Api/"]
COPY ["src/Mediso.PaymentSample.Application/Mediso.PaymentSample.Application.csproj", "src/Mediso.PaymentSample.Application/"]
COPY ["src/Mediso.PaymentSample.Domain/Mediso.PaymentSample.Domain.csproj", "src/Mediso.PaymentSample.Domain/"]
COPY ["src/Mediso.PaymentSample.Infrastructure/Mediso.PaymentSample.Infrastructure.csproj", "src/Mediso.PaymentSample.Infrastructure/"]
COPY ["src/Mediso.PaymentSample.SharedKernel/Mediso.PaymentSample.SharedKernel.csproj", "src/Mediso.PaymentSample.SharedKernel/"]

RUN dotnet restore "src/Mediso.PaymentSample.Api/Mediso.PaymentSample.Api.csproj"
COPY . .
WORKDIR "/src/src/Mediso.PaymentSample.Api"
RUN dotnet build "Mediso.PaymentSample.Api.csproj" -c Release

FROM build AS publish
RUN dotnet publish "Mediso.PaymentSample.Api.csproj" -c Release -o /app/publish --no-build --no-restore /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

USER $APP_UID
ENTRYPOINT ["dotnet", "Mediso.PaymentSample.Api.dll"]
