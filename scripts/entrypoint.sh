#!/bin/bash
set -e

# Load secrets into environment variables
export SLACK_BOT_TOKEN=$(cat /run/secrets/slack_bot_token)
export SLACK_SIGNING_SECRET=$(cat /run/secrets/slack_signing_secret)
export CHANNEL_ID=$(cat /run/secrets/channel_id)
export NGROK_AUTHTOKEN=$(cat /run/secrets/ngrok_authtoken)
export APP_URL=$(cat /run/secrets/cloud_run_url)

# Start your .NET bot
dotnet campsite-availability-scanner.dll
