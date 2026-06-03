#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../Src"
export PATH="$PATH:$HOME/.dotnet/tools"
dotnet ef database update --project AgoneSentimentSales.Infrastructure --startup-project AgoneSentimentSales.API
echo "Done — schema sentimentsales"
