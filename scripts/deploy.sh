#!/bin/bash
set -e

# --- CONFIGURATION ---
IMAGE_NAME="ghcr.io/<YOUR_GITHUB_USERNAME>/campsite-availability-scanner:latest"
CONTAINER_NAME="campsite-scanner"
SECRETS_DIR="/home/molly/secrets"

# GitHub Secrets to mount as Docker secrets
# Make sure these files exist on your Pi: slack_bot_token, slack_signing_secret, channel_id, ngrok_authtoken
# For security, restrict permissions
mkdir -p $SECRETS_DIR
chmod 700 $SECRETS_DIR

# --- PULL LATEST IMAGE ---
echo "[Deploy] Pulling latest Docker image..."
docker pull $IMAGE_NAME

# --- STOP AND REMOVE OLD CONTAINER ---
if [ "$(docker ps -q -f name=$CONTAINER_NAME)" ]; then
    echo "[Deploy] Stopping old container..."
    docker stop $CONTAINER_NAME
fi

if [ "$(docker ps -aq -f name=$CONTAINER_NAME)" ]; then
    echo "[Deploy] Removing old container..."
    docker rm $CONTAINER_NAME
fi

# --- RUN NEW CONTAINER ---
echo "[Deploy] Starting new container..."
docker run -d \
    --name $CONTAINER_NAME \
    --restart unless-stopped \
    -p 5167:5167 \
    --secret source=slack_bot_token,target=slack_bot_token \
    --secret source=slack_signing_secret,target=slack_signing_secret \
    --secret source=channel_id,target=channel_id \
    --secret source=ngrok_authtoken,target=ngrok_authtoken \
    $IMAGE_NAME

echo "[Deploy] Container started successfully!"
