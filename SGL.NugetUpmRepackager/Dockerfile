#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine AS base
RUN apk add --no-cache bash
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["NuGet.config", "NuGet.config"]
COPY ["SGL.NugetUpmRepackager/SGL.NugetUpmRepackager.csproj", "SGL.NugetUpmRepackager/"]
ARG SGL_PKG_USER
ARG SGL_PKG_TOKEN
RUN echo 'dotnet nuget update source SGL --username ${SGL_PKG_USER} --password ${SGL_PKG_TOKEN} --store-password-in-clear-text' > pkg-login.sh &&\
	chmod +x pkg-login.sh && ./pkg-login.sh &&\
	dotnet restore --disable-parallel "SGL.NugetUpmRepackager/SGL.NugetUpmRepackager.csproj" &&\
	dotnet nuget remove source SGL && rm pkg-login.sh
COPY . .
WORKDIR "/src/SGL.NugetUpmRepackager"
RUN dotnet build --no-restore "SGL.NugetUpmRepackager.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish --no-restore "SGL.NugetUpmRepackager.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY ["SGL.NugetUpmRepackager/NugetUpmRepackager.sh","/usr/local/bin/NugetUpmRepackager"]
RUN chmod +x /usr/local/bin/NugetUpmRepackager
COPY --from=publish /app/publish .
ENTRYPOINT [ "/bin/bash", "-l", "-c" ]
CMD ["bash"]
