#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:6.0-alpine AS base
RUN apk add --no-cache bash
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["NuGet.config", "NuGet.config"]
COPY ["SGL.NugetUnityRepackager/SGL.NugetUnityRepackager.csproj", "SGL.NugetUnityRepackager/"]
ARG SGL_PKG_USER
ARG SGL_PKG_TOKEN
RUN echo 'dotnet nuget update source SGL --username ${SGL_PKG_USER} --password ${SGL_PKG_TOKEN} --store-password-in-clear-text' > pkg-login.sh &&\
	chmod +x pkg-login.sh && ./pkg-login.sh &&\
	dotnet restore --disable-parallel "SGL.NugetUnityRepackager/SGL.NugetUnityRepackager.csproj" &&\
	dotnet nuget remove source SGL && rm pkg-login.sh
COPY . .
WORKDIR "/src/SGL.NugetUnityRepackager"
RUN dotnet build --no-restore "SGL.NugetUnityRepackager.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish --no-restore "SGL.NugetUnityRepackager.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY ["SGL.NugetUnityRepackager/NugetUnityRepackager.sh","/usr/local/bin/NugetUnityRepackager"]
RUN chmod +x /usr/local/bin/NugetUnityRepackager
COPY --from=publish /app/publish .
ENTRYPOINT ["/bin/bash"]
