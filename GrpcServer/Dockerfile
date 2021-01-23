# Has to updated to .net 5 :)

FROM mcr.microsoft.com/dotnet/core/runtime:3.0.0-preview6-disco AS base

# Install ASP.NET Core
ENV ASPNETCORE_VERSION 3.0.0-preview6.19307.2

RUN curl -SL --output aspnetcore.tar.gz https://dotnetcli.blob.core.windows.net/dotnet/aspnetcore/Runtime/$ASPNETCORE_VERSION/aspnetcore-runtime-$ASPNETCORE_VERSION-linux-x64.tar.gz \
    && aspnetcore_sha512='bd0e712d7f7888f9061901d5990bed53faa34fbe095d64360cc6da1772f08edb11ed8cbe746dc065a2c14e6b0f988fc27348800ce00e6c8e4547f7973ba49aa0' \
    && echo "$aspnetcore_sha512  aspnetcore.tar.gz" | sha512sum -c - \
    && tar -zxf aspnetcore.tar.gz -C /usr/share/dotnet ./shared/Microsoft.AspNetCore.App \
    && rm aspnetcore.tar.gz

WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.0-disco AS build
WORKDIR /src

COPY ["GrpcServer.csproj", "./"]
RUN dotnet restore "GrpcServer.csproj"
COPY . ./GrpcServer
WORKDIR "/src/GrpcServer"
RUN dotnet build "GrpcServer.csproj" -c Release -o /app

FROM build AS publish
RUN dotnet publish "GrpcServer.csproj" -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "GrpcServer.dll"]