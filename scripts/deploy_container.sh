#!/usr/bin/env bash
set -e

# Go to app directory
cd /app

echo "[DEPLOY] Pulling latest Docker image..."
docker-compose pull

echo "[DEPLOY] Creating fresh .env file..."
cat > .env <<EOF
ENV=production
SLACK_BOT_TOKEN=${SLACK_BOT_TOKEN}
SLACK_SIGNING_SECRET=${SLACK_SIGNING_SECRET}
NGROK_URL=/slack/events
PORT=${PORT}
CHANNEL_ID=${CHANNEL_ID}
NGROK_AUTHTOKEN=${NGROK_AUTHTOKEN}
EOF

echo "[DEPLOY] Starting ngrok tunnel..."
# Kill existing ngrok if running
pkill -f "ngrok http" || true
# Start ngrok in background
nohup ngrok http ${PORT} --log=stdout &

# Give ngrok a few seconds to start
sleep 5

# Fetch ngrok public URL
NGROK_PUBLIC_URL=$(curl --silent http://127.0.0.1:4040/api/tunnels | jq -r '.tunnels[0].public_url')
echo "[DEPLOY] Ngrok public URL: $NGROK_PUBLIC_URL"

# Update Slack request URL dynamically
echo "[DEPLOY] Updating Slack request URL..."
curl -X POST -H "Authorization: Bearer $SLACK_BOT_TOKEN" \
     -H "Content-type: application/json" \
     --data "{\"url\":\"${NGROK_PUBLIC_URL}/slack/events\"}" \
     https://slack.com/api/apps.event.authorizations.list # Replace with correct Slack API endpoint for updating request URL

echo "[DEPLOY] Restarting container..."
docker-compose down
docker-compose up -d --build

echo "[DEPLOY] Deployment complete!"
