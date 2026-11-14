# Build stage
FROM node:22-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

# Run stage
FROM nginx:alpine
COPY --from=build /app/dist/tickpulse-web/browser /usr/share/nginx/html
# nginx.conf is a template: ${PORT} and ${API_URL} are rendered at container
# start so the API origin and listen port come from the environment. Defaults
# keep local docker-compose working unchanged (API at http://api:5000, port 80).
# Only PORT and API_URL are substituted; nginx variables like $uri are kept.
COPY nginx.conf /etc/nginx/conf.d/default.conf.template
EXPOSE 80
CMD ["/bin/sh", "-c", "export PORT=${PORT:-80} API_URL=${API_URL:-http://api:5000}; envsubst '${PORT} ${API_URL}' < /etc/nginx/conf.d/default.conf.template > /etc/nginx/conf.d/default.conf && exec nginx -g 'daemon off;'"]
