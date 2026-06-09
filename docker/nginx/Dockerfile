FROM nginx:1.25-alpine

# Install curl for healthcheck
RUN apk add --no-cache curl

# Remove default configuration file
RUN rm /etc/nginx/conf.d/default.conf

# Copy our custom configuration file
COPY massar.conf /etc/nginx/conf.d/massar.conf

EXPOSE 80
