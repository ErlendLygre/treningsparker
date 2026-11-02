# Build context: repository root (needs data/parks.seed.json)
FROM node:22-alpine AS build
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY data/ /src/data/
COPY frontend/ ./
# VITE_DEMO_MODE=true builds a backend-less version (used for GitHub Pages).
ARG VITE_DEMO_MODE=false
ARG VITE_API_URL=
ARG VITE_BASE=/
ENV VITE_DEMO_MODE=$VITE_DEMO_MODE VITE_API_URL=$VITE_API_URL VITE_BASE=$VITE_BASE
RUN npm run build

# Export stage: `docker build --target static --output type=local,dest=out .` dumps the static site.
FROM scratch AS static
COPY --from=build /src/frontend/dist /

FROM nginx:1.27-alpine AS web
ENV API_UPSTREAM=http://api:8080 NGINX_ENTRYPOINT_LOCAL_RESOLVERS=1
COPY frontend/nginx.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /src/frontend/dist /usr/share/nginx/html
EXPOSE 80
