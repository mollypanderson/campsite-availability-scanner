#!/bin/bash
set -e

# Port your bot is listening on
PORT=${PORT:-5167}

# Start ngrok in the background
ngrok http $PORT --log=stdout &
