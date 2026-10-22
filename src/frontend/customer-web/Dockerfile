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

RUN mkdir -p /etc/nginx/templates && cat > /etc/nginx/templates/default.conf.template <<'NGINX'
server {
    listen 80;
    server_name _;
    root /usr/share/nginx/html;
    index index.html;

    location /api/ {
        proxy_pass http://${API_PROXY_TARGET}/;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    location / {
        try_files $uri $uri/ /index.html;
    }
}
NGINX

ENV API_PROXY_TARGET=host.docker.internal:8080
ENV NGINX_ENVSUBST_FILTER=^API_PROXY_TARGET$

EXPOSE 80
