#!/usr/bin/env python3
"""Render the root-only production app environment without logging values."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path
from urllib.parse import quote


COPIED_OPTIONAL_KEYS = (
    "GEMINI_API_KEY",
    "GOOGLE_CLOUD_VISION_API_KEY",
    "BUNNY_STREAM_LIBRARY_ID",
    "BUNNY_STREAM_API_KEY",
    "BUNNY_STREAM_TUS_UPLOAD_EXPIRY_MINUTES",
    "TELEGRAM_API_ID",
    "TELEGRAM_API_HASH",
    "TELEGRAM_STRING_SESSION",
    "TELEGRAM_DOWNLOADER_BOT",
    "EVOLUTION_API_BASE_URL",
    "EVOLUTION_API_KEY",
    "EVOLUTION_API_INSTANCE",
    "WHATSAPP_CLOUD_ACCESS_TOKEN",
    "WHATSAPP_CLOUD_PHONE_NUMBER_ID",
)


def parse_env(path: Path) -> dict[str, str]:
    values: dict[str, str] = {}
    for raw_line in path.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, value = line.split("=", 1)
        values[key.strip()] = value.strip().strip("'").strip('"')
    return values


def read_secret(directory: Path, name: str) -> str:
    path = directory / name
    value = path.read_text(encoding="utf-8").strip()
    if not value or "\n" in value or "\r" in value:
        raise ValueError(f"invalid secret file: {name}")
    return value


def safe_line(key: str, value: str) -> str:
    if "\n" in value or "\r" in value:
        raise ValueError(f"multiline environment value rejected: {key}")
    return f"{key}={value}"


def render(source: dict[str, str], secrets: Path) -> list[str]:
    postgres_password = read_secret(secrets, "postgres-app")
    redis_password = read_secret(secrets, "redis")
    values = {
        "ASPNETCORE_ENVIRONMENT": "Production",
        "ASPNETCORE_URLS": "http://+:5245",
        "ConnectionStrings__DefaultConnection": (
            "Host=host.docker.internal;Port=6432;Database=massar_platform;"
            f"Username=massar_app;Password={postgres_password};"
            "Pooling=true;Minimum Pool Size=0;Maximum Pool Size=50;"
            "Connection Idle Lifetime=300;Timeout=15;Command Timeout=60"
        ),
        "Redis__Sentinels": "10.77.0.11:26379,10.77.0.12:26379,10.77.0.13:26379",
        "Redis__SentinelServiceName": "massar-redis",
        "Redis__Password": redis_password,
        "REDIS_SENTINELS": "10.77.0.11:26379,10.77.0.12:26379,10.77.0.13:26379",
        "REDIS_SENTINEL_MASTER": "massar-redis",
        "REDIS_PASSWORD": redis_password,
        "DB_CONNECTION_STRING": (
            "postgresql://massar_app:"
            f"{quote(postgres_password, safe='')}@host.docker.internal:6432/massar_platform"
        ),
        "JwtSettings__Secret": read_secret(secrets, "jwt"),
        "JwtSettings__Issuer": "MassarPlatformAPI",
        "JwtSettings__Audience": "MassarPlatformClients",
        "JwtSettings__ExpirationMinutes": "60",
        "JwtSettings__RefreshExpirationDays": "30",
        "API_CALLBACK_SECRET": read_secret(secrets, "api-callback"),
        "AI_CALLBACK_SECRET": read_secret(secrets, "ai-callback"),
        "WORKER_ADMIN_TOKEN": read_secret(secrets, "worker-admin"),
        "WORKER_ADMIN_ENABLED": "true",
        "ParentReports__SigningSecret": read_secret(secrets, "parent-signing"),
        "CORS_ALLOWED_ORIGINS": (
            "https://massar-academy.net,https://app.massar-academy.net,"
            "https://admin.massar-academy.net,https://teacher.massar-academy.net,"
            "https://staff.massar-academy.net"
        ),
        "Cors__AllowedOrigins": (
            "https://massar-academy.net,https://app.massar-academy.net,"
            "https://admin.massar-academy.net,https://teacher.massar-academy.net,"
            "https://staff.massar-academy.net"
        ),
        "CookieSettings__Domain": ".massar-academy.net",
        "ForwardedHeaders__KnownProxies": "172.29.0.10",
        "SeedDefaults__Enabled": "false",
        "SeedDemoCatalog__Enabled": "false",
        "LANDING_PUBLIC_ORIGIN": "https://massar-academy.net",
        "STUDENT_PUBLIC_ORIGIN": "https://app.massar-academy.net",
        "ADMIN_PUBLIC_ORIGIN": "https://admin.massar-academy.net",
        "TEACHER_PUBLIC_ORIGIN": "https://teacher.massar-academy.net",
        "ASSISTANT_PUBLIC_ORIGIN": "https://staff.massar-academy.net",
        "NEXT_PUBLIC_APP_DOMAIN": "massar-academy.net",
        "NEXT_PUBLIC_API_URL": "https://api.massar-academy.net/api",
        "NEXT_PUBLIC_BACKEND_URL": "https://api.massar-academy.net",
        "NEXT_PUBLIC_WS_URL": "https://ws.massar-academy.net",
        "INTERNAL_API_URL": "http://backend:5245/api",
        "INTERNAL_BACKEND_URL": "http://backend:5245",
        "WORKER_URL": "http://worker:3001",
        "NODE_ENV": "production",
        "TZ": "Africa/Cairo",
    }
    for key in COPIED_OPTIONAL_KEYS:
        if source.get(key):
            values[key] = source[key]
    if not values.get("GEMINI_API_KEY"):
        raise ValueError("GEMINI_API_KEY is required for the Gemini Developer API")
    return [safe_line(key, value) for key, value in values.items()]


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-env", type=Path, required=True)
    parser.add_argument("--secret-dir", type=Path, required=True)
    args = parser.parse_args()
    try:
        print("\n".join(render(parse_env(args.source_env), args.secret_dir)))
    except (OSError, ValueError) as exc:
        print(f"app environment render failed: {exc}", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
