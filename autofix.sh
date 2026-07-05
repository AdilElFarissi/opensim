#!/bin/bash
echo "🔍 Fetching open security alerts for ${REPO_PATH}..."

# 1. Fetch open alerts from GitHub and store to a local json file
curl -s -L \
  -H "Authorization: Bearer ${GITHUB_TOKEN}" \
  -H "Accept: application/vnd.github+json" \
  -H "X-GitHub-Api-Version: 2022-11-28" \
  "https://github.com{REPO_PATH}/code-scanning/alerts?state=open&per_page=100" > alerts.json

# Check if the API request failed completely
if [ ! -s alerts.json ] || grep -q "message" alerts.json; then
  echo "❌ Error reading alerts from GitHub API. Server response:"
  cat alerts.json
  exit 1
fi

# 2. Broader filter: Extract alert numbers matching critical, high, or error severity levels
ALERT_NUMBERS=$(jq -r '.[] | select(.rule.security_severity_level == "critical" or .rule.security_severity_level == "high" or .rule.severity == "error") | .number' alerts.json 2>/dev/null)

# Count how many total raw alerts were downloaded
TOTAL_RAW=$(jq '. | length' alerts.json 2>/dev/null)
echo "📋 Total open alerts found in repo: ${TOTAL_RAW}"

if [ -z "${ALERT_NUMBERS}" ]; then
  echo "🎉 No alerts matched the critical, high, or error filters. Nothing to fix!"
  exit 0
fi

# 3. Request Copilot Autofix suggestions for each target alert
for NUM in ${ALERT_NUMBERS}; do
  echo "🤖 Requesting Autofix for Alert #${NUM}..."
  
  STATUS=$(curl -s -o response_details.json -w "%{http_code}" \
    -X POST \
    -H "Authorization: Bearer ${GITHUB_TOKEN}" \
    -H "Accept: application/vnd.github+json" \
    -H "X-GitHub-Api-Version: 2022-11-28" \
    "https://github.com{REPO_PATH}/code-scanning/alerts/${NUM}/autofix")

  if [ "$STATUS" = "200" ] || [ "$STATUS" = "201" ]; then
    echo "✅ Fix successfully initiated for Alert #${NUM}!"
  else
    echo "⚠️ Alert #${NUM} skipped. Status code: ${STATUS}"
    echo "💬 Server Message: $(cat response_details.json)"
  fi

  sleep 2
done
