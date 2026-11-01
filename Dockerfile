# Docker image ARGS
ARG BASE_IMAGE_REPO=mcr.microsoft.com
ARG BASE_IMAGE_BUILD=dotnet/sdk
ARG BASE_IMAGE_BUILD_TAG=10.0-alpine
ARG BASE_IMAGE_RUNTIME_REPO=public.ecr.aws
ARG BASE_IMAGE_RUNTIME=lambda/dotnet
ARG BASE_IMAGE_RUNTIME_TAG=10

# Setup Build Image
FROM ${BASE_IMAGE_REPO}/${BASE_IMAGE_BUILD}:${BASE_IMAGE_BUILD_TAG} AS build

# Build, Test and Publish ARGS
ARG VERSION_PREFIX=1.0.0.0
ARG VERSION_SUFFIX
ARG ENVIRONMENT=docker

# Build, Test and Publish ENVS
ENV DOTNET_ENVIRONMENT=${ENVIRONMENT}

WORKDIR /sln

# Dotnet Restore
COPY ./*.sln ./NuGet.config ./Directory.Build.props ./Directory.Packages.props ./
COPY src/*/*.csproj ./
RUN for file in $(ls *.csproj); do mkdir -p src/${file%.*}/ && mv $file src/${file%.*}/; done
COPY tests/*/*.csproj ./
RUN for file in $(ls *.csproj); do mkdir -p tests/${file%.*}/ && mv $file tests/${file%.*}/; done

RUN dotnet restore -v minimal

# Copy Remaining Files
COPY . .

# Dotnet Build
RUN dotnet build --no-restore -c Release -v minimal -p:VersionPrefix=${VERSION_PREFIX} -p:VersionSuffix=${VERSION_SUFFIX}

# Dotnet Test
# The exit code is saved rather than failing the build, so the results can still be exported
# and reported. Coverlet fails the run if line coverage drops below the threshold.
FROM build AS test
ARG COVERAGE_THRESHOLD=95
RUN mkdir -p /sln/tests/results; \
    dotnet test --no-restore --no-build -c Release -v minimal -m:1 \
      --logger "trx;LogFilePrefix=tests" --results-directory /sln/tests/results \
      -p:CollectCoverage=true -p:CoverletOutput=../results/ -p:MergeWith="../results/coverage.json" \
      -p:CoverletOutputFormat=opencover%2cjson -p:ExcludeByFile="**/Migrations/*.cs%2c**/obj/**/*.cs" \
      -p:ExcludeByAttribute="GeneratedCodeAttribute" \
      -p:Threshold=${COVERAGE_THRESHOLD} -p:ThresholdType=line -p:ThresholdStat=total; \
    echo $? > /sln/tests/results/exit-code

FROM scratch AS results
COPY --from=test /sln/tests/results/ ./

# Dotnet Publish
FROM build AS publish
ARG VERSION_PREFIX
ARG VERSION_SUFFIX
RUN dotnet publish ./src/**/Presentation.csproj --no-build -c Release -v quiet -o app -p:VersionPrefix=${VERSION_PREFIX} -p:VersionSuffix=${VERSION_SUFFIX} --no-self-contained

# Runtime Image
FROM ${BASE_IMAGE_RUNTIME_REPO}/${BASE_IMAGE_RUNTIME}:${BASE_IMAGE_RUNTIME_TAG} AS run

# Apply OS security fixes published since the base image was built. Amazon Linux
# 2023 pins packages to the release the image was built from, so ask for the latest.
RUN dnf upgrade -y --releasever=latest && dnf clean all

WORKDIR /var/task
COPY --from=publish /sln/app .
CMD [ "Presentation::CleanLambdaFunction.Presentation.LambdaFunction.Handler::GetMovies" ]
