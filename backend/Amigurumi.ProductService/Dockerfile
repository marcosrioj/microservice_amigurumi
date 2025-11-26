FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY .. .
RUN dotnet restore Amigurumi.sln --ignore-failed-sources
RUN dotnet publish Amigurumi.ProductService/Amigurumi.ProductService.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /out .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Amigurumi.ProductService.dll"]
