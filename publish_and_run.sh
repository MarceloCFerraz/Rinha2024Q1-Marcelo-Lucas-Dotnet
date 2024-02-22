#!/usr/bin/env bash

docker-compose down                                    # stop composer if it's running
docker stop $(docker ps -aq) && docker container prune # stop any running containers and delete them (not images)

# shellcheck source=publish_image.sh
source publish_image.sh # run publish script

docker compose up -d # start compose detached
