FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /app

# copy csproj and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore

# copy and publish app and libraries
COPY . .
RUN dotnet publish -c release -o out --no-cache


# final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:6.0 
EXPOSE 8000
WORKDIR /app
COPY --from=build /app/out .
#CMD ["./door"] 
ENTRYPOINT ["dotnet","cdn.dll"]


