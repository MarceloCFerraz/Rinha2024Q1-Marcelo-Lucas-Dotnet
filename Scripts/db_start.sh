#!/usr/bin/env bash

docker run -v ./DbSchema:/docker-entrypoint-initdb.d -e POSTGRES_PASSWORD=mystrongpassword -e POSTGRES_USER=admin -e POSTGRES_DB=rinha -p 5432:5432 --name=postgres-dev -d postgres:16.2-bullseye
