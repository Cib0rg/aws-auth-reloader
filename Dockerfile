# Build the operator
FROM mcr.microsoft.com/dotnet/sdk:6.0 as build
WORKDIR /operator

COPY ./ ./
RUN mkdir out
RUN dotnet publish -c Release -o out AwsAuthSync.sln

# The runner for the application
FROM mcr.microsoft.com/dotnet/aspnet:6.0 as final

RUN addgroup k8s-operator && useradd -G k8s-operator operator-user

WORKDIR /operator
COPY --from=build /operator/out/ ./
RUN chown operator-user:k8s-operator -R .

USER root

ENTRYPOINT ["dotnet", "AwsAuthSync.dll"]
