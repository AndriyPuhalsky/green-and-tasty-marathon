#!/usr/bin/env bash
# Деплой Green & Tasty у свій AWS-акаунт з AWS CloudShell (регіон us-west-2).
# Порядок як у README: Terraform -> backend (Syndicate) -> frontend (S3 + CloudFront).
# Потрібен Python 3.10+ (у CloudShell зараз 3.13); Terraform, .NET 10 і Node 24 ставляться в /tmp/tools.
# Запуск: bash infra/cloudshell-deploy.sh [tools|config|state|infra|backend|frontend|info|all]
# Оновити вже розгорнутий бекенд з іншої гілки: BRANCH=hw5-backend bash infra/cloudshell-deploy.sh update
# Код береться з репозиторію (BRANCH=main за замовчуванням, можна BRANCH=hw5-backend).
set -euo pipefail

REGION=us-west-2
REPO_URL="${REPO_URL:-https://github.com/AndriyPuhalsky/green-and-tasty-marathon.git}"
BRANCH="${BRANCH:-main}"
WORK=/tmp/m26
TOOLS=/tmp/tools
export PATH="$TOOLS/bin:$TOOLS/dotnet:$TOOLS/node/bin:$HOME/.dotnet/tools:$PATH"
export DOTNET_ROOT="$TOOLS/dotnet"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export AWS_DEFAULT_REGION=$REGION

ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)
TF_DIR="$WORK/infra/terraform"
BE_DIR="$WORK/backend/restaurant-backend-app"
FE_DIR="$WORK/frontend/restaurant-frontend-app"

log() { printf '\n==== %s ====\n' "$*"; }

step_tools() {
  log "tools"
  mkdir -p "$TOOLS/bin"
  local arch; arch=$(uname -m); [ "$arch" = "x86_64" ] && arch=amd64 || arch=arm64

  if ! command -v terraform >/dev/null; then
    local tfv; tfv=$(curl -s https://checkpoint-api.hashicorp.com/v1/check/terraform | python3 -c 'import sys,json;print(json.load(sys.stdin)["current_version"])')
    curl -sSLo /tmp/tf.zip "https://releases.hashicorp.com/terraform/${tfv}/terraform_${tfv}_linux_${arch}.zip"
    unzip -o -q /tmp/tf.zip -d "$TOOLS/bin" && rm /tmp/tf.zip
  fi
  terraform version | head -1

  if ! command -v dotnet >/dev/null || ! dotnet --list-sdks | grep -q '^10\.'; then
    curl -sSLo /tmp/dotnet-install.sh https://dot.net/v1/dotnet-install.sh
    bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "$TOOLS/dotnet" >/dev/null
  fi
  dotnet --version

  if ! command -v node >/dev/null || [ "$(node -v | cut -d. -f1)" != "v24" ]; then
    local narch; [ "$arch" = "amd64" ] && narch=x64 || narch=arm64
    local nfile; nfile=$(curl -s https://nodejs.org/dist/latest-v24.x/SHASUMS256.txt | awk '{print $2}' | grep "linux-${narch}.tar.xz$")
    curl -sSLo /tmp/node.tar.xz "https://nodejs.org/dist/latest-v24.x/${nfile}"
    mkdir -p "$TOOLS/node" && tar -xJf /tmp/node.tar.xz -C "$TOOLS/node" --strip-components=1 && rm /tmp/node.tar.xz
  fi
  node -v

  python3 -c 'import sys; assert sys.version_info >= (3, 10), sys.version'
  python3 --version
}

step_config() {
  log "config (account $ACCOUNT_ID)"
  rm -rf "$WORK"
  git clone -q --depth 1 -b "$BRANCH" "$REPO_URL" "$WORK"

  sed -i "s/{your_account_id}/$ACCOUNT_ID/" "$TF_DIR/backend.tf"
  sed -i "s/\"your_account_id\"/\"$ACCOUNT_ID\"/" "$TF_DIR/variables.tf"

  local cfg="$BE_DIR/.syndicate-config-restaurant-backend-app"
  sed -i -e "s/\"your_account_id\"/\"$ACCOUNT_ID\"/" -e "s/{your_account_id}/$ACCOUNT_ID/" \
         -e "s#^project_path:.*#project_path: \"$BE_DIR\"#" "$cfg/syndicate.yml"
  sed -i -e "s/\"your_account_id\"/\"$ACCOUNT_ID\"/" -e "s/{your_account_id}/$ACCOUNT_ID/g" "$cfg/syndicate_aliases.yml"

  grep -n "$ACCOUNT_ID" "$TF_DIR/backend.tf" "$TF_DIR/variables.tf" "$cfg/syndicate.yml" "$cfg/syndicate_aliases.yml"
}

step_state() {
  log "terraform state bucket"
  local b="restaurant-terraform-state-$ACCOUNT_ID"
  if aws s3api head-bucket --bucket "$b" 2>/dev/null; then
    echo "bucket $b already exists"
  else
    aws s3api create-bucket --bucket "$b" --region $REGION --create-bucket-configuration LocationConstraint=$REGION >/dev/null
    aws s3api put-bucket-versioning --bucket "$b" --versioning-configuration Status=Enabled
    aws s3api put-public-access-block --bucket "$b" \
      --public-access-block-configuration "BlockPublicAcls=true,IgnorePublicAcls=true,BlockPublicPolicy=true,RestrictPublicBuckets=true"
    aws s3api put-bucket-encryption --bucket "$b" \
      --server-side-encryption-configuration '{"Rules":[{"ApplyServerSideEncryptionByDefault":{"SSEAlgorithm":"AES256"}}]}'
    echo "bucket $b created"
  fi
}

step_infra() {
  log "terraform"
  cd "$TF_DIR"
  terraform init -input=false
  terraform fmt -check -recursive || true
  terraform validate
  terraform plan -input=false -out=tfplan
  terraform apply -input=false -auto-approve tfplan
}

step_backend() {
  log "backend (syndicate)"
  cd "$BE_DIR"
  [ -d syndicate_venv ] || python3 -m venv syndicate_venv
  # shellcheck disable=SC1091
  source syndicate_venv/bin/activate
  pip install -q --upgrade pip
  pip show aws-syndicate >/dev/null 2>&1 || pip install -q aws-syndicate
  export SDCT_CONF="$BE_DIR/.syndicate-config-restaurant-backend-app"
  local art="marathon2026-restaurant-artifacts-$ACCOUNT_ID"
  aws s3api head-bucket --bucket "$art" 2>/dev/null && echo "bucket $art already exists" || syndicate create-deploy-target-bucket
  syndicate build
  syndicate deploy --deploy_name dev
  deactivate
}

step_update() {
  log "backend update ($BRANCH)"
  cd "$WORK"
  git fetch -q --depth 1 origin "$BRANCH"
  git checkout -q -B "$BRANCH" FETCH_HEAD
  cd "$BE_DIR"
  # shellcheck disable=SC1091
  source syndicate_venv/bin/activate
  export SDCT_CONF="$BE_DIR/.syndicate-config-restaurant-backend-app"
  syndicate build
  syndicate update --deploy-name dev --replace-output
  deactivate
}

backend_url() {
  local api_id
  api_id=$(aws apigateway get-rest-apis --region $REGION --query "items[?name=='restaurant-backend-api'].id | [0]" --output text)
  echo "https://$api_id.execute-api.$REGION.amazonaws.com/develop"
}

step_frontend() {
  log "frontend"
  cd "$FE_DIR"
  export BACKEND_URL; BACKEND_URL=$(backend_url)
  echo "BACKEND_URL=$BACKEND_URL"
  npm ci --no-audit --no-fund
  npm run build
  aws s3 sync dist/restaurant-frontend-app/browser "s3://restaurant-frontend-$ACCOUNT_ID/" --delete --only-show-errors
  local dist_id; dist_id=$(cd "$TF_DIR" && terraform output -raw cloudfront_distribution_id)
  aws cloudfront create-invalidation --distribution-id "$dist_id" --paths '/*' --query 'Invalidation.Status' --output text
}

step_info() {
  log "result"
  cd "$TF_DIR"
  echo "Frontend: https://$(terraform output -raw cloudfront_distribution_domain_name)"
  echo "Backend:  $(backend_url)"
  aws lambda list-functions --query "Functions[?starts_with(FunctionName,'api-handler')||starts_with(FunctionName,'report')].[FunctionName,Runtime,LastModified]" --output table
}

case "${1:-all}" in
  tools) step_tools ;;
  config) step_config ;;
  state) step_state ;;
  infra) step_infra ;;
  backend) step_backend ;;
  update) step_update ;;
  frontend) step_frontend ;;
  info) step_info ;;
  all) step_tools; step_config; step_state; step_infra; step_backend; step_frontend; step_info ;;
  *) echo "unknown step: $1"; exit 1 ;;
esac
