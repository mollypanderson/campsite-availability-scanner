#!/bin/bash
set -e

# Load secrets into environment variables
export SLACK_BOT_TOKEN=$(cat /run/secrets/slack_bot_token)
export SLACK_SIGNING_SECRET=$(cat /run/secrets/slack_signing_secret)
export CHANNEL_ID=$(cat /run/secrets/channel_id)
export NGROK_AUTHTOKEN=$(cat /run/secrets/ngrok_authtoken)

# Start ngrok
./scripts/start_ngrok.sh &

# Wait for ngrok to initialize
sleep 5

# Fetch public ngrok URL
NGROK_URL=$(curl -s http://127.0.0.1:4040/api/tunnels | jq -r '.tunnels[0].public_url')

# Update Slack Events URL dynamically
./scripts/update_slack_url.sh "$NGROK_URL/slack/events"

# Start your .NET bot
dotnet campsite-availability-scanner.dll
