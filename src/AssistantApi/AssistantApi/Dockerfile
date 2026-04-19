FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["AssistantApi/AssistantApi.csproj", "AssistantApi/"]
RUN dotnet restore "AssistantApi/AssistantApi.csproj"

COPY . .
WORKDIR "/src/AssistantApi"
RUN dotnet build "AssistantApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AssistantApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# OCR runtime dependencies for scanned PDF processing.
ENV DEBIAN_FRONTEND=noninteractive

RUN set -eux; \
	for attempt in 1 2 3 4 5; do \
		apt-get update && \
		apt-get install -y --no-install-recommends --fix-missing \
			-o Acquire::Retries=5 \
			ocrmypdf \
			tesseract-ocr \
			tesseract-ocr-rus \
			tesseract-ocr-eng \
			ghostscript && \
		apt-get -f install -y && \
		command -v ocrmypdf && \
		break; \
		if [ "$attempt" -eq 5 ]; then \
			echo "OCR package install failed after retries"; \
			exit 1; \
		fi; \
		echo "Retrying OCR package install (attempt $attempt failed)"; \
		sleep 5; \
	done; \
	rm -rf /var/lib/apt/lists/*

EXPOSE 8080
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AssistantApi.dll"]
