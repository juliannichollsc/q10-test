FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine
WORKDIR /source
COPY Directory.Build.props OrderFlow.sln ./
COPY database/ database/
COPY src/ src/
COPY tests/ tests/
RUN dotnet restore OrderFlow.sln
ENTRYPOINT ["dotnet", "test", "OrderFlow.sln", "--no-restore", "--logger", "console;verbosity=normal"]
