# 1단계: 빌드 환경
# .NET 8 SDK 이미지를 기반으로 빌드 환경을 설정합니다.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

# 프로젝트 파일(*.csproj)을 먼저 복사하고 종속성을 복원합니다.
# 이렇게 하면 소스 코드가 변경되어도 종속성은 캐시된 레이어를 사용해 빌드 속도가 향상됩니다.
COPY ./*.csproj .
RUN dotnet restore

# 나머지 소스 코드를 복사하고 애플리케이션을 게시(publish)합니다.
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

# 2단계: 런타임 환경
# .NET 8 런타임 이미지를 기반으로 최종 이미지를 생성합니다.
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# appsettings.json에 정의된 포트들을 노출합니다.
# API 서버 (HTTP/HTTPS), Battle 서버
EXPOSE 8080
EXPOSE 443
EXPOSE 7788

# 컨테이너가 시작될 때 실행할 명령어를 설정합니다.
# Server.dll은 실제 프로젝트의 어셈블리 이름으로 변경해야 할 수 있습니다.
ENTRYPOINT ["dotnet", "Server.dll"]