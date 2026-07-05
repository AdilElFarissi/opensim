#!/bin/bash
echo "🔍 Fetching open security alerts for ${REPO_PATH}..."

# 1. Fetch open alerts from GitHub and store to a local json file
curl -s -L \
  -H "Authorization: Bearer ${GITHUB_TOKEN}" \
  -H "Accept: application/vnd.github+json" \
  -H "X-GitHub-Api-Version: 2022-11-28" \
  "https://github.com{REPO_PATH}/code-scanning/alerts?state=open&per_page=100" > alerts.json

# 2. Extract alert numbers matching critical or error severity
ALERT_NUMBERS=$(jq -r '.[] | select(.rule.severity == "critical" or .rule.severity == "error") | .number' alerts.json 2>/dev/null)

if [ -z "${ALERT_NUMBERS}" ]; then
  echo "🎉 No critical or error level alerts found matching your criteria."
  exit 0
fi

# 3. Request Copilot Autofix suggestions for each target alert
for NUM in ${ALERT_NUMBERS}; do
  echo "🤖 Requesting Autofix for Alert #${NUM}..."
  
  STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
    -X POST \
    -H "Authorization: Bearer ${GITHUB_TOKEN}" \
    -H "Accept: application/vnd.github+json" \
    -H "X-GitHub-Api-Version: 2022-11-28" \
    "https://github.com{REPO_PATH}/code-scanning/alerts/${NUM}/autofix")

  if [ "$STATUS" = "200" ] || [ "$STATUS" = "201" ]; then
    echo "✅ Fix successfully initiated for Alert #${NUM}!"
  else
    echo "⚠️ Alert #${NUM} skip response status: ${STATUS}"
  fi

  sleep 1
done
