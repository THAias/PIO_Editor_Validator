FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build-env
ARG TARGETARCH
WORKDIR /app

# Copy everything
COPY . ./
# Restore as distinct layers
RUN dotnet restore -a $TARGETARCH
# Build and publish a release
RUN dotnet publish -a $TARGETARCH -c Release --framework net8.0 --no-restore -o out

# Build runtime image
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:8.0-alpine
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production
COPY --from=build-env /app/out .
COPY --from=build-env /app/src/Firely.Fhir.Validation.R4/structureDefinitions /Firely.Fhir.Validation.R4/structureDefinitions
ENTRYPOINT ["./WebAPI"]
EXPOSE 5212