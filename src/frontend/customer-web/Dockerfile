# Build the Vite application.
FROM node:22-alpine AS build

WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY ./ ./
RUN npm run build

# Serve the compiled static site and proxy API requests.
FROM nginx:alpine AS runtime

COPY --from=build /app/dist /usr/share/nginx/html

COPY nginx/default.conf.template /etc/nginx/templates/default.conf.template

ENV API_GATEWAY_PROXY_TARGET=http://api-gateway:8080
ENV NGINX_ENVSUBST_FILTER="^API_GATEWAY_PROXY_TARGET$"

EXPOSE 80
