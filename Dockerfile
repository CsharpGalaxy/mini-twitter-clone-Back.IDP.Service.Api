# مرحله پایه برای اجرای اپلیکیشن
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

# مرحله ساخت پروژه
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# کپی فایل پروژه و restore
COPY IDP.Service.Api.csproj ./
RUN dotnet restore "IDP.Service.Api.csproj"

# کپی کل پروژه و build
COPY . ./
RUN dotnet build "IDP.Service.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# مرحله publish
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "IDP.Service.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# مرحله نهایی برای اجرای اپلیکیشن
FROM base AS final
WORKDIR /app

# کپی فایل‌های منتشرشده
COPY --from=publish /app/publish .

# تنظیم آدرس اجرا
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "IDP.Service.Api.dll"]
