# Makefile for .NET + React Monorepo
# Build and push Docker images to GitHub Container Registry

.PHONY: help build build-server build-client docker-build-server docker-build-client docker-build-all push-server push-client push-all deploy login test dev dev-server dev-client version-info version-up rollout-restart rollout-status
.PHONY: typecheck fmt-server fmt-client fmt-all lint-server lint-client lint-all check-server check-client check-all fix-all
.PHONY: k8s-deploy k8s-deploy-server k8s-deploy-client k8s-update k8s-status k8s-logs-server k8s-logs-client
.PHONY: k8s-reload-server k8s-reload-client k8s-stop k8s-delete k8s-generate-secret k8s-create-image-pull-secret
.PHONY: k8s-pods k8s-services k8s-describe k8s-scale-server k8s-scale-client k8s-reload k8s-delete-namespace

# Configuration
REGISTRY := ghcr.io
GITHUB_USER ?= $(shell echo $$GITHUB_USER)
GITHUB_TOKEN ?= $(shell echo $$GITHUB_TOKEN)
IMAGE_VERSION ?= latest
PROJECT_NAME := simple-sso
SERVER_PROJECT := server/Server.csproj
SERVER_OUTPUT := server/bin
SERVER_VERSION_FILE := server/Version.cs
CLIENT_PACKAGE_FILE := client/package.json

# Image names
SERVER_IMAGE := $(REGISTRY)/$(GITHUB_USER)/$(PROJECT_NAME)-server
CLIENT_IMAGE := $(REGISTRY)/$(GITHUB_USER)/$(PROJECT_NAME)-client

# Kubernetes Configuration
K8S_NAMESPACE := simple-sso
K8S_CONTEXT ?= $(shell kubectl config current-context)
REPLICAS ?= 2

# Colors for output
BLUE := \033[0;34m
GREEN := \033[0;32m
YELLOW := \033[0;33m
RED := \033[0;31m
NC := \033[0m # No Color

help: ## Show this help message
	@echo "$(BLUE).NET + React Monorepo$(NC)"
	@echo ""
	@echo "$(YELLOW)Prerequisites:$(NC)"
	@echo "  export GITHUB_USER=your-github-username"
	@echo "  export GITHUB_TOKEN=your-github-token"
	@echo ""
	@echo "$(YELLOW)Usage:$(NC) make [target]"
	@echo ""
	@echo "$(YELLOW)Targets:$(NC)"
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | awk 'BEGIN {FS = ":.*?## "}; {printf "  $(GREEN)%-20s$(NC) %s\n", $$1, $$2}'

check-env: ## Check required environment variables
	@echo "$(BLUE)Checking environment variables...$(NC)"
	@if [ -z "$(GITHUB_USER)" ]; then \
		echo "$(RED)Error: GITHUB_USER is not set$(NC)"; \
		echo "Run: export GITHUB_USER=your-github-username"; \
		exit 1; \
	fi
	@if [ -z "$(GITHUB_TOKEN)" ]; then \
		echo "$(RED)Error: GITHUB_TOKEN is not set$(NC)"; \
		echo "Run: export GITHUB_TOKEN=your-github-token"; \
		exit 1; \
	fi
	@echo "$(GREEN)✓ GITHUB_USER: $(GITHUB_USER)$(NC)"
	@echo "$(GREEN)✓ GITHUB_TOKEN: [REDACTED]$(NC)"
	@echo "$(GREEN)✓ IMAGE_VERSION: $(IMAGE_VERSION)$(NC)"

login: check-env ## Login to GitHub Container Registry
	@echo "$(BLUE)Logging in to GitHub Container Registry...$(NC)"
	@echo "$(GITHUB_TOKEN)" | docker login $(REGISTRY) -u $(GITHUB_USER) --password-stdin
	@echo "$(GREEN)✓ Successfully logged in to $(REGISTRY)$(NC)"

build: build-server build-client ## Build both server and client locally
	@echo "$(GREEN)✓ All builds completed successfully$(NC)"

build-server: ## Build .NET server (server/bin)
	@echo "$(BLUE)Building .NET server...$(NC)"
	dotnet publish $(SERVER_PROJECT) -c Release -o $(SERVER_OUTPUT) /p:UseAppHost=false
	@echo "$(GREEN)✓ Server built: $(SERVER_OUTPUT)$(NC)"

build-client: ## Build React client (client/dist)
	@echo "$(BLUE)Building React client...$(NC)"
	bun run --filter='@monorepo/client' build
	@echo "$(GREEN)✓ Client built: client/dist$(NC)"

docker-build-server: ## Build server Docker image
	@echo "$(BLUE)Building server Docker image...$(NC)"
	docker build -f server/Dockerfile -t $(SERVER_IMAGE):$(IMAGE_VERSION) -t $(SERVER_IMAGE):latest .
	@echo "$(GREEN)✓ Server image built: $(SERVER_IMAGE):$(IMAGE_VERSION)$(NC)"

docker-build-client: ## Build client Docker image (VITE_API_URL is now runtime!)
	@echo "$(BLUE)Building client Docker image...$(NC)"
	docker build -f client/Dockerfile -t $(CLIENT_IMAGE):$(IMAGE_VERSION) -t $(CLIENT_IMAGE):latest .
	@echo "$(GREEN)✓ Client image built: $(CLIENT_IMAGE):$(IMAGE_VERSION)$(NC)"
	@echo "$(GREEN)✓ Note: VITE_API_URL is configured at runtime$(NC)"

docker-build-all: docker-build-server docker-build-client ## Build all Docker images (server + client)
	@echo "$(GREEN)✓ All Docker images built successfully$(NC)"

push-server: check-env ## Push server image to GitHub Container Registry
	@echo "$(BLUE)Pushing server image...$(NC)"
	docker push $(SERVER_IMAGE):$(IMAGE_VERSION)
	docker push $(SERVER_IMAGE):latest
	@echo "$(GREEN)✓ Server image pushed: $(SERVER_IMAGE):$(IMAGE_VERSION)$(NC)"

push-client: check-env ## Push client image to GitHub Container Registry
	@echo "$(BLUE)Pushing client image...$(NC)"
	docker push $(CLIENT_IMAGE):$(IMAGE_VERSION)
	docker push $(CLIENT_IMAGE):latest
	@echo "$(GREEN)✓ Client image pushed: $(CLIENT_IMAGE):$(IMAGE_VERSION)$(NC)"

push-all: push-server push-client ## Push all images to GitHub Container Registry
	@echo "$(GREEN)✓ All images pushed successfully$(NC)"

deploy: login docker-build-all push-all ## Complete deployment workflow (login + build + push)
	@echo "$(GREEN)✓ Deployment complete!$(NC)"
	@echo ""
	@echo "$(YELLOW)Next steps:$(NC)"
	@echo "  1. Apply Kubernetes manifests: make k8s-deploy"
	@echo "  2. Check status: make k8s-status"

clean: ## Remove local Docker images
	@echo "$(BLUE)Removing local images...$(NC)"
	-docker rmi $(SERVER_IMAGE):$(IMAGE_VERSION) $(SERVER_IMAGE):latest
	-docker rmi $(CLIENT_IMAGE):$(IMAGE_VERSION) $(CLIENT_IMAGE):latest
	@echo "$(GREEN)✓ Local images removed$(NC)"

info: ## Show image information
	@echo "$(BLUE)Image Information:$(NC)"
	@echo "  Registry:      $(REGISTRY)"
	@echo "  User:          $(GITHUB_USER)"
	@echo "  Project:       $(PROJECT_NAME)"
	@echo "  Version:       $(IMAGE_VERSION)"
	@echo ""
	@echo "$(YELLOW)Server Image:$(NC)"
	@echo "  $(SERVER_IMAGE):$(IMAGE_VERSION)"
	@echo "  $(SERVER_IMAGE):latest"
	@echo ""
	@echo "$(YELLOW)Client Image:$(NC)"
	@echo "  $(CLIENT_IMAGE):$(IMAGE_VERSION)"
	@echo "  $(CLIENT_IMAGE):latest"

# Development commands (local)
dev: ## Start local development environment (server + client)
	@echo "$(BLUE)Starting server (.NET) and client (React)...$(NC)"
	bun run dev

dev-server: ## Start .NET server in watch mode
	@echo "$(BLUE)Starting .NET server...$(NC)"
	cd server && dotnet watch run

dev-client: ## Start React client dev server
	@echo "$(BLUE)Starting React client...$(NC)"
	bun run --filter='@monorepo/client' dev

test: ## Run tests locally
	bun test

version-info: ## Show current application and image versions
	@echo "$(BLUE)Version Information:$(NC)"
	@SERVER_VERSION=$$(grep 'const string Version = ' $(SERVER_VERSION_FILE) | sed 's/.*"\(.*\)".*/\1/'); \
	CLIENT_VERSION=$$(grep '"version"' $(CLIENT_PACKAGE_FILE) | sed 's/.*"version": "\(.*\)".*/\1/'); \
	ROOT_VERSION=$$(grep '"version"' package.json | sed 's/.*"version": "\(.*\)".*/\1/'); \
	DOTNET_VERSION=$$(dotnet --version); \
	BUN_VERSION=$$(bun --version); \
	echo "  Server (.NET): $$SERVER_VERSION ($(SERVER_VERSION_FILE))"; \
	echo "  Client:        $$CLIENT_VERSION ($(CLIENT_PACKAGE_FILE))"; \
	echo "  Root package:  $$ROOT_VERSION (package.json)"; \
	echo "  Image version: $(IMAGE_VERSION)"; \
	echo "  .NET SDK:      $$DOTNET_VERSION"; \
	echo "  Bun:           $$BUN_VERSION"

version-up: ## Bump patch version in both client and server
	@echo "$(BLUE)Bumping patch version...$(NC)"
	@echo "$(YELLOW)Updating server version...$(NC)"
	@CURRENT_VERSION=$$(grep 'const string Version = ' $(SERVER_VERSION_FILE) | sed 's/.*"\(.*\)".*/\1/'); \
	MAJOR=$$(echo $$CURRENT_VERSION | cut -d. -f1); \
	MINOR=$$(echo $$CURRENT_VERSION | cut -d. -f2); \
	PATCH=$$(echo $$CURRENT_VERSION | cut -d. -f3); \
	NEW_PATCH=$$(($$PATCH + 1)); \
	NEW_VERSION="$$MAJOR.$$MINOR.$$NEW_PATCH"; \
	sed -i.bak "s/const string Version = \".*\"/const string Version = \"$$NEW_VERSION\"/" $(SERVER_VERSION_FILE) && rm $(SERVER_VERSION_FILE).bak; \
	echo "$(GREEN)✓ Server version: $$CURRENT_VERSION → $$NEW_VERSION$(NC)"
	@echo "$(YELLOW)Updating client version...$(NC)"
	@CURRENT_VERSION=$$(grep '"version"' $(CLIENT_PACKAGE_FILE) | sed 's/.*"version": "\(.*\)".*/\1/'); \
	MAJOR=$$(echo $$CURRENT_VERSION | cut -d. -f1); \
	MINOR=$$(echo $$CURRENT_VERSION | cut -d. -f2); \
	PATCH=$$(echo $$CURRENT_VERSION | cut -d. -f3); \
	NEW_PATCH=$$(($$PATCH + 1)); \
	NEW_VERSION="$$MAJOR.$$MINOR.$$NEW_PATCH"; \
	sed -i.bak "s/\"version\": \".*\"/\"version\": \"$$NEW_VERSION\"/" $(CLIENT_PACKAGE_FILE) && rm $(CLIENT_PACKAGE_FILE).bak; \
	echo "$(GREEN)✓ Client version: $$CURRENT_VERSION → $$NEW_VERSION$(NC)"
	@echo "$(GREEN)✓ All versions bumped!$(NC)"

rollout-restart: ## Restart server and client deployments (rollout restart)
	@echo "$(BLUE)Restarting deployments...$(NC)"
	kubectl rollout restart deployment/server-deployment -n $(K8S_NAMESPACE)
	kubectl rollout restart deployment/client-deployment -n $(K8S_NAMESPACE)
	@echo "$(GREEN)✓ Rollout restart initiated for both server and client$(NC)"
	@echo "$(YELLOW)Run 'make rollout-status' to monitor progress$(NC)"

rollout-status: ## Check rollout status of server and client deployments
	@echo "$(BLUE)Checking rollout status...$(NC)"
	@echo ""
	@echo "$(YELLOW)Server:$(NC)"
	kubectl rollout status deployment/server-deployment -n $(K8S_NAMESPACE)
	@echo ""
	@echo "$(YELLOW)Client:$(NC)"
	kubectl rollout status deployment/client-deployment -n $(K8S_NAMESPACE)
	@echo ""
	@echo "$(GREEN)✓ Rollout complete$(NC)"

# ============================================================================
# Kubernetes Deployment Commands
# ============================================================================

k8s-check-context: ## Check current Kubernetes context
	@echo "$(BLUE)Current Kubernetes context:$(NC)"
	@echo "  Context: $(K8S_CONTEXT)"
	@echo "  Namespace: $(K8S_NAMESPACE)"
	@kubectl cluster-info

k8s-generate-secret: ## Generate Kubernetes secret from environment variables
	@echo "$(BLUE)Generating Kubernetes secret...$(NC)"
	@if [ -z "$$JWT_SECRET" ]; then \
		echo "$(YELLOW)Warning: JWT_SECRET not set, generating random$(NC)"; \
		JWT_SECRET=$$(openssl rand -base64 32); \
	else \
		JWT_SECRET="$$JWT_SECRET"; \
	fi; \
	kubectl create secret generic monorepo-secret \
		--from-literal=JWT_SECRET="$$JWT_SECRET" \
		--from-literal=AZURE_TENANT_ID="$$AZURE_TENANT_ID" \
		--from-literal=AZURE_CLIENT_ID="$$AZURE_CLIENT_ID" \
		--from-literal=AZURE_CLIENT_SECRET="$$AZURE_CLIENT_SECRET" \
		--from-literal=AZURE_REDIRECT_URI="$$AZURE_REDIRECT_URI" \
		--namespace=$(K8S_NAMESPACE) \
		--dry-run=client -o yaml > k8s/secret.yaml
	@echo "$(GREEN)✓ Secret generated: k8s/secret.yaml$(NC)"
	@echo "$(YELLOW)Note: Review k8s/secret.yaml before applying$(NC)"

k8s-create-image-pull-secret: check-env ## Create image pull secret for GitHub Container Registry
	@echo "$(BLUE)Creating image pull secret for GitHub Container Registry...$(NC)"
	kubectl create secret docker-registry ghcr-secret \
		--docker-server=$(REGISTRY) \
		--docker-username=$(GITHUB_USER) \
		--docker-password=$(GITHUB_TOKEN) \
		--namespace=$(K8S_NAMESPACE) \
		--dry-run=client -o yaml | kubectl apply -f -
	@echo "$(GREEN)✓ Image pull secret created$(NC)"

k8s-apply-namespace: ## Create/update namespace
	@echo "$(BLUE)Creating namespace...$(NC)"
	kubectl apply -f k8s/namespace.yaml
	@echo "$(GREEN)✓ Namespace created/updated$(NC)"

k8s-apply-configmap: ## Apply ConfigMap
	@echo "$(BLUE)Applying ConfigMap...$(NC)"
	kubectl apply -f k8s/configmap.yaml
	@echo "$(GREEN)✓ ConfigMap applied$(NC)"

k8s-apply-secret: ## Apply Secret (must exist in k8s/secret.yaml)
	@echo "$(BLUE)Applying Secret...$(NC)"
	@if [ ! -f k8s/secret.yaml ]; then \
		echo "$(RED)Error: k8s/secret.yaml not found$(NC)"; \
		echo "$(YELLOW)Run 'make k8s-generate-secret' first$(NC)"; \
		exit 1; \
	fi
	kubectl apply -f k8s/secret.yaml
	@echo "$(GREEN)✓ Secret applied$(NC)"

k8s-deploy-server: ## Deploy server to Kubernetes
	@echo "$(BLUE)Deploying server to Kubernetes...$(NC)"
	kubectl apply -f k8s/server-deployment.yaml
	kubectl apply -f k8s/server-service.yaml
	@echo "$(GREEN)✓ Server deployed$(NC)"

k8s-deploy-client: ## Deploy client to Kubernetes
	@echo "$(BLUE)Deploying client to Kubernetes...$(NC)"
	kubectl apply -f k8s/client-deployment.yaml
	kubectl apply -f k8s/client-service.yaml
	@echo "$(GREEN)✓ Client deployed$(NC)"

k8s-deploy-ingress: ## Deploy ingress
	@echo "$(BLUE)Deploying ingress...$(NC)"
	kubectl apply -f k8s/ingress.yaml
	@echo "$(GREEN)✓ Ingress deployed$(NC)"

k8s-deploy: k8s-apply-namespace k8s-apply-configmap k8s-deploy-server k8s-deploy-client k8s-deploy-ingress ## Deploy all resources to Kubernetes
	@echo "$(GREEN)✓ All resources deployed successfully!$(NC)"
	@echo ""
	@echo "$(YELLOW)Next steps:$(NC)"
	@echo "  1. Create secret: make k8s-generate-secret && make k8s-apply-secret"
	@echo "  2. Create image pull secret: make k8s-create-image-pull-secret"
	@echo "  3. Check status: make k8s-status"

k8s-update: ## Update deployments (rolling update)
	@echo "$(BLUE)Updating deployments...$(NC)"
	kubectl apply -f k8s/configmap.yaml
	kubectl apply -f k8s/server-deployment.yaml
	kubectl apply -f k8s/client-deployment.yaml
	kubectl rollout status deployment/server-deployment -n $(K8S_NAMESPACE)
	kubectl rollout status deployment/client-deployment -n $(K8S_NAMESPACE)
	@echo "$(GREEN)✓ Deployments updated$(NC)"

k8s-status: ## Check deployment status
	@echo "$(BLUE)Checking deployment status...$(NC)"
	@echo ""
	@echo "$(YELLOW)Deployments:$(NC)"
	kubectl get deployments -n $(K8S_NAMESPACE)
	@echo ""
	@echo "$(YELLOW)Pods:$(NC)"
	kubectl get pods -n $(K8S_NAMESPACE)
	@echo ""
	@echo "$(YELLOW)Services:$(NC)"
	kubectl get services -n $(K8S_NAMESPACE)
	@echo ""
	@echo "$(YELLOW)Ingress:$(NC)"
	kubectl get ingress -n $(K8S_NAMESPACE)

k8s-pods: ## List all pods
	@echo "$(BLUE)Listing pods...$(NC)"
	kubectl get pods -n $(K8S_NAMESPACE) -o wide

k8s-services: ## List all services
	@echo "$(BLUE)Listing services...$(NC)"
	kubectl get services -n $(K8S_NAMESPACE) -o wide

k8s-describe: ## Describe all resources
	@echo "$(BLUE)Describing deployments...$(NC)"
	kubectl describe deployments -n $(K8S_NAMESPACE)
	@echo ""
	@echo "$(BLUE)Describing services...$(NC)"
	kubectl describe services -n $(K8S_NAMESPACE)

k8s-describe-server: ## Describe server resources
	@echo "$(BLUE)Describing server deployment...$(NC)"
	kubectl describe deployment server-deployment -n $(K8S_NAMESPACE)

k8s-describe-client: ## Describe client resources
	@echo "$(BLUE)Describing client deployment...$(NC)"
	kubectl describe deployment client-deployment -n $(K8S_NAMESPACE)

k8s-logs-server: ## View server logs
	@echo "$(BLUE)Fetching server logs...$(NC)"
	kubectl logs -l component=server -n $(K8S_NAMESPACE) --tail=100

k8s-logs-client: ## View client logs
	@echo "$(BLUE)Fetching client logs...$(NC)"
	kubectl logs -l component=client -n $(K8S_NAMESPACE) --tail=100

k8s-logs-server-follow: ## Follow server logs
	@echo "$(BLUE)Following server logs (Ctrl+C to stop)...$(NC)"
	kubectl logs -f -l component=server -n $(K8S_NAMESPACE)

k8s-logs-client-follow: ## Follow client logs
	@echo "$(BLUE)Following client logs (Ctrl+C to stop)...$(NC)"
	kubectl logs -f -l component=client -n $(K8S_NAMESPACE)

k8s-logs-all: ## View all logs
	@echo "$(BLUE)Fetching all logs...$(NC)"
	kubectl logs -l app=monorepo -n $(K8S_NAMESPACE) --tail=50 --prefix=true

k8s-exec-server: ## Execute shell in server pod
	@echo "$(BLUE)Opening shell in server pod...$(NC)"
	kubectl exec -it deployment/server-deployment -n $(K8S_NAMESPACE) -- /bin/sh

k8s-exec-client: ## Execute shell in client pod
	@echo "$(BLUE)Opening shell in client pod...$(NC)"
	kubectl exec -it deployment/client-deployment -n $(K8S_NAMESPACE) -- /bin/sh

k8s-reload-server: ## Restart server pods (rollout restart)
	@echo "$(BLUE)Restarting server pods...$(NC)"
	kubectl rollout restart deployment/server-deployment -n $(K8S_NAMESPACE)
	kubectl rollout status deployment/server-deployment -n $(K8S_NAMESPACE)
	@echo "$(GREEN)✓ Server restarted$(NC)"

k8s-reload-client: ## Restart client pods (rollout restart)
	@echo "$(BLUE)Restarting client pods...$(NC)"
	kubectl rollout restart deployment/client-deployment -n $(K8S_NAMESPACE)
	kubectl rollout status deployment/client-deployment -n $(K8S_NAMESPACE)
	@echo "$(GREEN)✓ Client restarted$(NC)"

k8s-reload: k8s-reload-server k8s-reload-client ## Restart all pods
	@echo "$(GREEN)✓ All pods restarted$(NC)"

k8s-scale-server: ## Scale server deployment (REPLICAS=N)
	@echo "$(BLUE)Scaling server to $(REPLICAS) replicas...$(NC)"
	kubectl scale deployment/server-deployment --replicas=$(REPLICAS) -n $(K8S_NAMESPACE)
	@echo "$(GREEN)✓ Server scaled to $(REPLICAS) replicas$(NC)"

k8s-scale-client: ## Scale client deployment (REPLICAS=N)
	@echo "$(BLUE)Scaling client to $(REPLICAS) replicas...$(NC)"
	kubectl scale deployment/client-deployment --replicas=$(REPLICAS) -n $(K8S_NAMESPACE)
	@echo "$(GREEN)✓ Client scaled to $(REPLICAS) replicas$(NC)"

k8s-stop: ## Stop all deployments (scale to 0)
	@echo "$(BLUE)Stopping all deployments...$(NC)"
	kubectl scale deployment/server-deployment --replicas=0 -n $(K8S_NAMESPACE)
	kubectl scale deployment/client-deployment --replicas=0 -n $(K8S_NAMESPACE)
	@echo "$(GREEN)✓ All deployments stopped (scaled to 0)$(NC)"

k8s-start: ## Start all deployments (scale to default replicas)
	@echo "$(BLUE)Starting all deployments...$(NC)"
	kubectl scale deployment/server-deployment --replicas=2 -n $(K8S_NAMESPACE)
	kubectl scale deployment/client-deployment --replicas=2 -n $(K8S_NAMESPACE)
	@echo "$(GREEN)✓ All deployments started$(NC)"

k8s-rollback-server: ## Rollback server deployment
	@echo "$(BLUE)Rolling back server deployment...$(NC)"
	kubectl rollout undo deployment/server-deployment -n $(K8S_NAMESPACE)
	kubectl rollout status deployment/server-deployment -n $(K8S_NAMESPACE)
	@echo "$(GREEN)✓ Server rolled back$(NC)"

k8s-rollback-client: ## Rollback client deployment
	@echo "$(BLUE)Rolling back client deployment...$(NC)"
	kubectl rollout undo deployment/client-deployment -n $(K8S_NAMESPACE)
	kubectl rollout status deployment/client-deployment -n $(K8S_NAMESPACE)
	@echo "$(GREEN)✓ Client rolled back$(NC)"

k8s-history-server: ## View server deployment history
	@echo "$(BLUE)Server deployment history:$(NC)"
	kubectl rollout history deployment/server-deployment -n $(K8S_NAMESPACE)

k8s-history-client: ## View client deployment history
	@echo "$(BLUE)Client deployment history:$(NC)"
	kubectl rollout history deployment/client-deployment -n $(K8S_NAMESPACE)

k8s-delete: ## Delete all deployments and services (keep namespace)
	@echo "$(BLUE)Deleting all resources...$(NC)"
	kubectl delete -f k8s/ingress.yaml --ignore-not-found=true
	kubectl delete -f k8s/client-deployment.yaml --ignore-not-found=true
	kubectl delete -f k8s/client-service.yaml --ignore-not-found=true
	kubectl delete -f k8s/server-deployment.yaml --ignore-not-found=true
	kubectl delete -f k8s/server-service.yaml --ignore-not-found=true
	@echo "$(GREEN)✓ All resources deleted$(NC)"

k8s-delete-namespace: ## Delete entire namespace (WARNING: deletes everything)
	@echo "$(RED)WARNING: This will delete the entire namespace and all resources!$(NC)"
	@echo -n "Are you sure? [y/N] " && read ans && [ $${ans:-N} = y ]
	kubectl delete namespace $(K8S_NAMESPACE)
	@echo "$(GREEN)✓ Namespace deleted$(NC)"

k8s-port-forward-server: ## Port forward to server (localhost:3000)
	@echo "$(BLUE)Port forwarding to server on localhost:3000...$(NC)"
	@echo "$(YELLOW)Press Ctrl+C to stop$(NC)"
	kubectl port-forward -n $(K8S_NAMESPACE) service/server-service 3000:3000

k8s-port-forward-client: ## Port forward to client (localhost:8080)
	@echo "$(BLUE)Port forwarding to client on localhost:8080...$(NC)"
	@echo "$(YELLOW)Press Ctrl+C to stop$(NC)"
	kubectl port-forward -n $(K8S_NAMESPACE) service/client-service 8080:80

k8s-events: ## View recent events in namespace
	@echo "$(BLUE)Recent events in namespace:$(NC)"
	kubectl get events -n $(K8S_NAMESPACE) --sort-by='.lastTimestamp'

k8s-top-pods: ## Show pod resource usage
	@echo "$(BLUE)Pod resource usage:$(NC)"
	kubectl top pods -n $(K8S_NAMESPACE)

k8s-top-nodes: ## Show node resource usage
	@echo "$(BLUE)Node resource usage:$(NC)"
	kubectl top nodes

k8s-info: ## Show Kubernetes deployment information
	@echo "$(BLUE)Kubernetes Deployment Information:$(NC)"
	@echo "  Context:       $(K8S_CONTEXT)"
	@echo "  Namespace:     $(K8S_NAMESPACE)"
	@echo "  Server Image:  $(SERVER_IMAGE):$(IMAGE_VERSION)"
	@echo "  Client Image:  $(CLIENT_IMAGE):$(IMAGE_VERSION)"
	@echo ""
	@echo "$(YELLOW)Available Commands:$(NC)"
	@echo "  make k8s-deploy              - Deploy all resources"
	@echo "  make k8s-status              - Check status"
	@echo "  make k8s-logs-server         - View server logs"
	@echo "  make k8s-logs-client         - View client logs"
	@echo "  make k8s-reload-server       - Restart server"
	@echo "  make k8s-reload-client       - Restart client"
	@echo "  make k8s-stop                - Stop all deployments"
	@echo "  make k8s-delete              - Delete all resources"

k8s-full-deploy: login docker-build-all push-all k8s-deploy ## Complete K8s workflow (build, push, deploy)
	@echo "$(GREEN)✓ Complete Kubernetes deployment finished!$(NC)"
	@echo ""
	@echo "$(YELLOW)Deployment Summary:$(NC)"
	@make k8s-status

# ============================================================================
# Code Quality
# ============================================================================

typecheck: ## Type-check tests and client with tsgo
	@echo "$(BLUE)Type-checking tests and client with tsgo...$(NC)"
	bunx tsgo -p tsconfig.json --noEmit && cd client && bunx tsgo -b --noEmit
	@echo "$(GREEN)✓ Type-check passed$(NC)"

fmt-server: ## Format .NET code
	cd server && dotnet format

fmt-client: ## Format client code
	cd client && bun run format

fmt-all: fmt-server fmt-client ## Format all code

lint-server: ## Lint .NET code (format check)
	cd server && dotnet format --verify-no-changes

lint-client: ## Lint client code
	cd client && bun run lint

lint-all: lint-server lint-client ## Lint all code

check-server: ## Check .NET code (format + build)
	dotnet format $(SERVER_PROJECT)
	dotnet build $(SERVER_PROJECT) -c Release

check-client: ## Check client code (format + lint + fix)
	cd client && bun run check

check-all: check-server check-client ## Check all code

fix-all: check-all ## Fix all code (alias for check-all)

# ============================================================================
# Dependency Management
# ============================================================================
.PHONY: deps-upgrade deps-server deps-client

deps-upgrade: deps-server deps-client ## Upgrade/check all dependencies (.NET + Bun)
	@echo "$(GREEN)✓ All dependencies upgraded$(NC)"

deps-server: ## List outdated .NET server dependencies
	@echo "$(BLUE)Checking .NET dependencies...$(NC)"
	@cd server && dotnet list package --outdated
	@echo "$(GREEN)✓ Dependency check complete (update versions in Server.csproj)$(NC)"

deps-client: ## Upgrade React client dependencies to latest versions
	@echo "$(BLUE)Upgrading client dependencies...$(NC)"
	@cd client && bun update
	@echo "$(GREEN)✓ Client dependencies upgraded$(NC)"
