#!/bin/bash
set -e

NGROK_URL=$1

if [ -z "$NGROK_URL" ]; then
  echo "Usage: $0 <ngrok-public-url>"
  exit 1
fi

echo "Updating Slack request URL to: $NGROK_URL"

# Use Slack API to update your app's event subscription URL
curl -X POST https://slack.com/api/apps.event.authorizations.list \
  -H "Authorization: Bearer $SLACK_BOT_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
        \"event_subscription_url\": \"$NGROK_URL\"
      }"

echo "Slack URL updated successfully."
