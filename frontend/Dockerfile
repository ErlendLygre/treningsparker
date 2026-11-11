# Build context: repository root
FROM node:26-alpine AS build
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM nginx:1.30-alpine AS web
ENV API_UPSTREAM=http://api:8080 NGINX_ENTRYPOINT_LOCAL_RESOLVERS=1
COPY frontend/nginx.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /src/frontend/dist /usr/share/nginx/html
EXPOSE 80
